import { useCallback, useEffect, useRef, useState } from 'react';
import MemberShell from '../../components/MemberShell';
import Modal from '../../components/Modal';
import PayPalPackageButtons from '../../components/PayPalPackageButtons';
import { creditsApi } from '../../api';
import { useMemberSlices } from './memberDataState';
import { formatCount } from '../businessPresentation';

const LEDGER_PILL = {
  TopUp: 'success',
  Enrolment: 'purple',
  Refund: 'info',
  Royalty: 'success',
  AdminAdjustment: 'neutral',
  Hold: 'warn',
  Capture: 'neutral',
  Release: 'info',
  Forfeit: 'warn',
};

// "AdminAdjustment" -> "Admin adjustment", "TopUp" -> "Top up".
function ledgerTypeLabel(type) {
  const words = String(type ?? '').replace(/([a-z])([A-Z])/g, '$1 $2');
  return words.charAt(0) + words.slice(1).toLowerCase();
}

// Fixed credit packages + PayPal.
export default function MemberPaymentPage() {
  const { state, showToast, applyLedgerTopUp, reload } = useMemberSlices('billing');
  const [tab, setTab] = useState('topup');
  const [selectedId, setSelectedId] = useState(null);
  const [success, setSuccess] = useState(null);
  const [payError, setPayError] = useState('');
  const capturingReturn = useRef(false);

  const packages = state.packages || [];
  const selected = packages.find((p) => p.id === selectedId) || packages[0];

  const onCaptured = useCallback(
    async (ledger) => {
      await applyLedgerTopUp(ledger);
      setPayError('');
      setSuccess({
        credits: ledger.delta,
        balance: ledger.balanceAfter,
        description: ledger.description,
      });
    },
    [applyLedgerTopUp],
  );

  const onPayPalError = useCallback(
    (msg) => {
      if (!msg || msg === 'Payment cancelled') return;
      setPayError(msg);
      showToast(msg);
    },
    [showToast],
  );

  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    if (params.get('cancelled') === 'true') {
      showToast('Payment cancelled');
      window.history.replaceState({}, '', '/member/payment');
      return undefined;
    }

    const orderId = params.get('token') || params.get('orderID');
    if (!orderId || capturingReturn.current) return undefined;
    capturingReturn.current = true;

    creditsApi.capturePayPalOrder(orderId)
      .then((ledger) => onCaptured(ledger))
      .catch((error) => showToast(error.message || 'PayPal capture failed'))
      .finally(() => {
        window.history.replaceState({}, '', '/member/payment');
      });
    return undefined;
  }, [onCaptured, showToast]);

  return (
    <>
      <MemberShell
        title="Credit Wallet"
      >
        <div className="tabs">
          <button type="button" className={`tab${tab === 'topup' ? ' active' : ''}`} onClick={() => setTab('topup')}>
            Top Up
          </button>
          <button type="button" className={`tab${tab === 'ledger' ? ' active' : ''}`} onClick={() => setTab('ledger')}>
            Credit Ledger
          </button>
        </div>

        <div className="cx-wallet-balance">
          <span className="cx-wallet-balance-label">Available credits</span>
          <strong className="cx-wallet-balance-amount">{state.credits}</strong>
          <span className="cx-wallet-balance-sub">On hold: {formatCount(state.heldCredits ?? 0, 'credit')} · Total: {formatCount(state.totalCredits ?? state.credits, 'credit')}</span>
        </div>

        {state.loading && !(state.packages || []).length ? <p role="status">Loading packages…</p> : null}
        {state.loadError ? <div className="callout warn" role="alert">{state.loadError} <button type="button" className="btn btn-ghost" onClick={reload}>Retry</button></div> : null}

        {tab === 'topup' ? (
          <>
            <h3 className="cx-wallet-heading">Choose a Credit Package</h3>
            <div className="grid-3 cx-wallet-packs">
              {packages.map((p) => {
                const active = (selectedId ?? packages[0]?.id) === p.id;
                return (
                  <button
                    key={p.id}
                    type="button"
                    className={`pack-card${p.best ? ' best' : ''}${active ? ' selected' : ''}`}
                    aria-pressed={active}
                    onClick={() => setSelectedId(p.id)}
                  >
                    {active ? <span className="cx-pack-check" aria-hidden="true">✓</span> : null}
                    {p.best ? <span className="pill purple">Best Value</span> : null}
                    <div className="amount">{p.credits}</div>
                    <div className="cx-pack-unit">Credits</div>
                    <div className="cx-pack-price">{p.price}</div>
                    {p.note ? <div className="cx-pack-note">{p.note}</div> : null}
                  </button>
                );
              })}
            </div>

            <div className="card cx-wallet-pay">
              <div className="card-header">Pay with PayPal</div>
              <div className="card-body">
                <p className="cx-wallet-selected">
                  Selected: <strong>{selected ? `${formatCount(selected.credits, 'credit')} · ${selected.price}` : '—'}</strong>
                </p>
                {payError ? (
                  <div className="callout warn" style={{ marginBottom: 12 }}>
                    <div className="callout-title">Payment failed</div>
                    {payError}
                  </div>
                ) : null}
                {selected ? (
                  <PayPalPackageButtons
                    key={selected.id}
                    packageId={selected.id}
                    onCaptured={onCaptured}
                    onError={onPayPalError}
                  />
                ) : null}
              </div>
            </div>
          </>
        ) : (
          <div className="card cx-wallet-ledger">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Date</th>
                  <th>Type</th>
                  <th>Description</th>
                  <th className="num">Credits</th>
                  <th className="num">Balance</th>
                  <th className="num">On hold</th>
                </tr>
              </thead>
              <tbody>
                {state.ledger.map((l, i) => (
                  <tr key={`${l.date}-${i}`}>
                    <td>{l.date}</td>
                    <td><span className={`pill ${LEDGER_PILL[l.type] || 'neutral'}`}>{ledgerTypeLabel(l.type)}</span></td>
                    <td>{l.desc}</td>
                    <td className={`num cx-ledger-delta${l.delta > 0 ? ' in' : ''}`}>
                      {l.delta > 0 ? '+' : ''}
                      {l.delta}
                    </td>
                    <td className="num">{l.balance}</td>
                    <td className="num">{l.heldAfter ?? '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </MemberShell>

      <Modal open={Boolean(success)} title="Top-Up Successful" onClose={() => setSuccess(null)} width={440}>
        {success ? (
          <div style={{ textAlign: 'center' }}>
            <div className="cx-wallet-success" aria-hidden="true">✓</div>
            <strong style={{ display: 'block', margin: '12px 0 8px', fontSize: 18 }}>
              Credits added to your wallet
            </strong>
            <p style={{ fontSize: 14, color: 'var(--slate)', margin: '0 0 16px' }}>
              +{formatCount(success.credits, 'credit')} · Balance now{' '}
              <strong style={{ color: 'var(--purple)' }}>{formatCount(success.balance, 'credit')}</strong>
            </p>
            <div className="callout info" style={{ textAlign: 'left', marginBottom: 16 }}>
              <div className="callout-title">Payment confirmed</div>
              {success.description || 'PayPal sandbox payment captured successfully.'}
            </div>
            <div className="modal-actions" style={{ justifyContent: 'center' }}>
              <button
                type="button"
                className="btn btn-ghost"
                onClick={() => {
                  setSuccess(null);
                  setTab('ledger');
                }}
              >
                View Ledger
              </button>
              <button type="button" className="btn btn-primary" onClick={() => setSuccess(null)}>
                Done
              </button>
            </div>
          </div>
        ) : null}
      </Modal>
    </>
  );
}
