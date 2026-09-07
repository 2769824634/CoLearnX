import { useCallback, useState } from 'react';
import MemberShell from '../../components/MemberShell';
import Modal from '../../components/Modal';
import PayPalPackageButtons from '../../components/PayPalPackageButtons';
import { useMemberData } from './MemberDataContext';

// Fixed credit packages + PayPal.
export default function MemberPaymentPage() {
  const { state, showToast, applyLedgerTopUp } = useMemberData();
  const [tab, setTab] = useState('topup');
  const [selectedId, setSelectedId] = useState(null);
  const [success, setSuccess] = useState(null);

  const selected = state.packages.find((p) => p.id === selectedId) || state.packages[0];

  const onCaptured = useCallback(
    async (ledger) => {
      await applyLedgerTopUp(ledger);
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
      if (msg && msg !== 'Payment cancelled') showToast(msg);
    },
    [showToast],
  );

  return (
    <>
      <MemberShell
        title="Credit Wallet"
        onNotify={() => showToast('No new notifications')}
      >
        <div className="tabs">
          <button type="button" className={`tab${tab === 'topup' ? ' active' : ''}`} onClick={() => setTab('topup')}>
            Top Up
          </button>
          <button type="button" className={`tab${tab === 'ledger' ? ' active' : ''}`} onClick={() => setTab('ledger')}>
            Credit Ledger
          </button>
        </div>

        <div className="card purple-bg" style={{ maxWidth: 300, marginBottom: 20 }}>
          <div className="card-body">
            <div style={{ fontSize: 12, color: 'var(--slate)' }}>Current Balance</div>
            <div style={{ fontSize: 40, fontWeight: 700, color: 'var(--purple)' }}>{state.credits}</div>
            <div style={{ fontSize: 12, color: 'var(--teal)' }}>Credits Available</div>
          </div>
        </div>

        {tab === 'topup' ? (
          <>
            <h3 style={{ fontSize: 15, marginBottom: 12 }}>Choose a Credit Package</h3>
            <div className="grid-3" style={{ marginBottom: 20 }}>
              {state.packages.map((p) => {
                const active = (selectedId ?? state.packages[0]?.id) === p.id;
                return (
                  <button
                    key={p.id}
                    type="button"
                    className={`pack-card${p.best ? ' best' : ''}`}
                    style={{
                      cursor: 'pointer',
                      borderColor: active ? 'var(--purple)' : undefined,
                      outline: active ? '2px solid var(--purple)' : undefined,
                    }}
                    onClick={() => setSelectedId(p.id)}
                  >
                    {p.best ? <span className="pill purple">Best Value</span> : null}
                    <div className="amount" style={{ marginTop: 8 }}>
                      {p.credits}
                    </div>
                    <div style={{ fontSize: 12, color: 'var(--slate)' }}>Credits</div>
                    <div style={{ fontWeight: 700, margin: '8px 0' }}>{p.price}</div>
                    {p.note ? <div style={{ fontSize: 11, color: 'var(--slate)' }}>{p.note}</div> : null}
                  </button>
                );
              })}
            </div>

            <div className="card" style={{ maxWidth: 480 }}>
              <div className="card-header">Pay with PayPal</div>
              <div className="card-body">
                <p style={{ fontSize: 13, marginBottom: 12 }}>
                  Selected: <strong>{selected ? `${selected.credits} credits · ${selected.price}` : '—'}</strong>
                </p>
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
          <div className="card">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Date</th>
                  <th>Type</th>
                  <th>Description</th>
                  <th>Credits</th>
                  <th>Balance</th>
                </tr>
              </thead>
              <tbody>
                {state.ledger.map((l, i) => (
                  <tr key={`${l.date}-${i}`}>
                    <td>{l.date}</td>
                    <td>{l.type}</td>
                    <td>{l.desc}</td>
                    <td style={{ color: l.delta > 0 ? 'var(--teal)' : 'var(--danger)' }}>
                      {l.delta > 0 ? '+' : ''}
                      {l.delta}
                    </td>
                    <td>{l.balance}</td>
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
            <div style={{ fontSize: 48, color: 'var(--teal)', lineHeight: 1 }}>✓</div>
            <strong style={{ display: 'block', margin: '12px 0 8px', fontSize: 18 }}>
              Credits added to your wallet
            </strong>
            <p style={{ fontSize: 14, color: 'var(--slate)', margin: '0 0 16px' }}>
              +{success.credits} credits · Balance now{' '}
              <strong style={{ color: 'var(--purple)' }}>{success.balance}</strong>
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
