import { useState } from 'react';
import useAdminAuth from '../../auth/useAdminAuth';
import { adminPricingApi } from '../../api';
import Modal from '../../components/Modal';
import AdminDataState from './AdminDataState';
import useAdminQuery from './useAdminQuery';

function loadTiers(token, _key, signal) {
  return adminPricingApi.tiers(token, signal);
}

function loadDomains(token, _key, signal) {
  return adminPricingApi.domains(token, signal);
}

function loadPool(token, _key, signal) {
  return adminPricingApi.pool(token, signal);
}

function band(min, max) {
  return `${min}–${max}`;
}

function fieldMessage(error, field) {
  const messages = error?.fieldErrors?.[field];
  return Array.isArray(messages) ? messages[0] : '';
}

function PricingTabs({ tab, onChange }) {
  return (
    <div className="admin-review-filters" role="tablist" aria-label="Pricing sections">
      <button type="button" role="tab" aria-selected={tab === 'domains'} className={tab === 'domains' ? 'active' : ''} onClick={() => onChange('domains')}>Domains</button>
      <button type="button" role="tab" aria-selected={tab === 'pool'} className={tab === 'pool' ? 'active' : ''} onClick={() => onChange('pool')}>Price pool</button>
    </div>
  );
}

export default function AdminPricingPage() {
  const [tab, setTab] = useState('domains');

  return (
    <section className="admin-review-page">
      <header className="admin-review-header">
        <div>
          <p className="admin-form-eyebrow">Reference data</p>
          <h1>Pricing</h1>
          <p>Price tiers, domains, and the read-only price pool.</p>
        </div>
      </header>
      {tab === 'domains' ? <DomainsTab onTab={setTab} /> : <PoolTab onTab={setTab} />}
    </section>
  );
}

function DomainsTab({ onTab }) {
  const { token } = useAdminAuth();
  const tiers = useAdminQuery(loadTiers, 'tiers');
  const domains = useAdminQuery(loadDomains, 'domains');
  const [editor, setEditor] = useState(null);
  const [confirming, setConfirming] = useState(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  function openAdd() {
    setError(null);
    setEditor({ name: '', priceTierId: String(tiers.data[0].id) });
  }

  function openEdit(domain) {
    setError(null);
    setEditor({ id: domain.id, name: domain.name, priceTierId: String(domain.priceTierId) });
  }

  async function save() {
    if (!editor || busy) return;
    setBusy(true);
    setError(null);
    const body = { name: editor.name, priceTierId: Number(editor.priceTierId) };
    try {
      if (editor.id) await adminPricingApi.updateDomain(token, editor.id, body);
      else await adminPricingApi.createDomain(token, body);
      setEditor(null);
      domains.refresh();
    } catch (saveError) {
      setError(saveError);
    } finally {
      setBusy(false);
    }
  }

  async function deactivate() {
    if (!confirming || busy) return;
    setBusy(true);
    setError(null);
    try {
      await adminPricingApi.deactivateDomain(token, confirming.id);
      setConfirming(null);
      domains.refresh();
    } catch (deactivateError) {
      setError(deactivateError);
    } finally {
      setBusy(false);
    }
  }

  const nameError = fieldMessage(error, 'name');
  const tierError = fieldMessage(error, 'priceTierId');
  const formError = error && !nameError && !tierError ? error.message : '';

  return (
    <>
      <AdminDataState loading={domains.loading || tiers.loading} error={domains.error || tiers.error} onRetry={() => { domains.refresh(); tiers.refresh(); }} />
      {domains.data && tiers.data ? (
        <section className="admin-review-workspace" aria-label="Domains">
          <div className="admin-review-toolbar">
            <PricingTabs tab="domains" onChange={onTab} />
            <button type="button" className="btn btn-primary" onClick={openAdd}>Add domain</button>
          </div>
          <div className="admin-review-table-wrap">
            <table className="admin-review-table">
              <thead>
                <tr><th scope="col">Name</th><th scope="col">Tier</th><th scope="col">Status</th><th scope="col">Actions</th></tr>
              </thead>
              <tbody>
                {domains.data.map((domain) => (
                  <tr key={domain.id}>
                    <td><strong>{domain.name}</strong></td>
                    <td>{domain.tierCode}</td>
                    <td><span className={`admin-status ${domain.isActive ? 'approved' : 'rejected'}`}>{domain.isActive ? 'Active' : 'Inactive'}</span></td>
                    <td>
                      {domain.isActive ? (
                        <div className="admin-pricing-actions">
                          <button type="button" className="btn btn-ghost btn-sm" onClick={() => openEdit(domain)}>Edit</button>
                          <button type="button" className="btn btn-ghost btn-sm" onClick={() => { setError(null); setConfirming(domain); }}>Deactivate</button>
                        </div>
                      ) : null}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      ) : null}
      <Modal open={Boolean(editor)} title={editor?.id ? 'Edit domain' : 'Add domain'} onClose={() => { if (!busy) { setEditor(null); setError(null); } }}>
        <form className="admin-pricing-form" onSubmit={(event) => { event.preventDefault(); save(); }}>
          {formError ? <div className="admin-review-inline-error" role="alert">{formError}</div> : null}
          <div className="form-group">
            <label htmlFor="domain-name">Name</label>
            <input id="domain-name" value={editor?.name ?? ''} disabled={busy} aria-invalid={nameError ? 'true' : undefined} onChange={(event) => setEditor((current) => ({ ...current, name: event.target.value }))} />
            {nameError ? <div className="admin-review-inline-error" role="alert">{nameError}</div> : null}
          </div>
          <div className="form-group">
            <label htmlFor="domain-tier">Tier</label>
            <select id="domain-tier" value={editor?.priceTierId ?? ''} disabled={busy} aria-invalid={tierError ? 'true' : undefined} onChange={(event) => setEditor((current) => ({ ...current, priceTierId: event.target.value }))}>
              {tiers.data?.map((tier) => (
                <option key={tier.id} value={tier.id}>
                  {`${tier.code} · theory ${band(tier.theoryMinHourlyCredits, tier.theoryMaxHourlyCredits)} · practice ${band(tier.practiceMinHourlyCredits, tier.practiceMaxHourlyCredits)}`}
                </option>
              ))}
            </select>
            {tierError ? <div className="admin-review-inline-error" role="alert">{tierError}</div> : null}
          </div>
          <div className="modal-actions">
            <button type="button" className="btn btn-ghost" disabled={busy} onClick={() => { setEditor(null); setError(null); }}>Cancel</button>
            <button type="submit" className="btn btn-primary" disabled={busy}>{busy ? 'Saving…' : 'Save domain'}</button>
          </div>
        </form>
      </Modal>
      <Modal open={Boolean(confirming)} title="Deactivate domain" onClose={() => { if (!busy) { setConfirming(null); setError(null); } }}>
        {formError ? <div className="admin-review-inline-error" role="alert">{formError}</div> : null}
        <p>{`Deactivate ${confirming?.name}? New courses can no longer choose it. Courses that already use it keep it.`}</p>
        <div className="modal-actions">
          <button type="button" className="btn btn-ghost" disabled={busy} onClick={() => { setConfirming(null); setError(null); }}>Cancel</button>
          <button type="button" className="btn admin-reject-button" disabled={busy} onClick={deactivate}>Deactivate</button>
        </div>
      </Modal>
    </>
  );
}

function PoolTab({ onTab }) {
  const pool = useAdminQuery(loadPool, 'pool');
  const tiers = useAdminQuery(loadTiers, 'tiers');
  const [tier, setTier] = useState('');
  const intake = (pool.data ?? []).filter((row) => row.saleMode === 'Intake' && (!tier || row.tierCode === tier));
  const packs = (pool.data ?? []).filter((row) => row.saleMode === 'CoursePack');

  return (
    <>
      <AdminDataState loading={pool.loading} error={pool.error} onRetry={pool.refresh} />
      {pool.data ? (
        <section className="admin-review-workspace" aria-label="Price pool">
          <div className="admin-review-toolbar">
            <PricingTabs tab="pool" onChange={onTab} />
          </div>
          {tiers.data ? (
            <div className="admin-pricing-ladder" aria-label="Price tiers">
              {tiers.data.map((item) => (
                <button type="button" key={item.id} className={tier === item.code ? 'active' : ''} aria-pressed={tier === item.code} onClick={() => setTier(tier === item.code ? '' : item.code)}>
                  <strong>{item.code}</strong>
                  <span>{`Theory ${band(item.theoryMinHourlyCredits, item.theoryMaxHourlyCredits)}`}</span>
                  <span>{`Practice ${band(item.practiceMinHourlyCredits, item.practiceMaxHourlyCredits)}`}</span>
                </button>
              ))}
            </div>
          ) : null}
          <div className="admin-section-heading">
            <div><h2>Intake</h2><p>Each row is one tier, type, and level. Bold numbers are the hourly credits and hours.</p></div>
            <label className="admin-status-filter" htmlFor="tier-filter">
              Tier filter
              <select id="tier-filter" value={tier} onChange={(event) => setTier(event.target.value)}>
                <option value="">All</option>
                {['A', 'B', 'C', 'D', 'E'].map((code) => <option key={code} value={code}>{code}</option>)}
              </select>
            </label>
          </div>
          <div className="admin-review-table-wrap">
            <table className="admin-review-table admin-pricing-pool" aria-label="Intake prices">
              <thead>
                <tr><th scope="col">Tier</th><th scope="col">Type</th><th scope="col">Level</th><th scope="col">Hourly credits</th><th scope="col">Hours</th><th scope="col">Content</th><th scope="col">Delivery</th><th scope="col">Platform</th></tr>
              </thead>
              <tbody>
                {intake.map((row, index) => (
                  <tr key={row.id} className={index > 0 && row.tierCode !== intake[index - 1].tierCode ? 'admin-pricing-tier-start' : ''}>
                    <td>{row.tierCode}</td>
                    <td>{row.courseType}</td>
                    <td>{row.levelName}</td>
                    <td className="admin-pricing-figure">{band(row.minHourlyCredits, row.maxHourlyCredits)}</td>
                    <td className="admin-pricing-figure">{band(row.minHours, row.maxHours)}</td>
                    <td className="admin-pricing-share">{row.contentSharePercent}%</td>
                    <td className="admin-pricing-share">{row.deliverySharePercent}%</td>
                    <td className="admin-pricing-share">{row.platformSharePercent}%</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="admin-section-heading"><div><h2>Course pack</h2><p>One total for the whole pack. Delivery share is zero.</p></div></div>
          <div className="admin-review-table-wrap">
            <table className="admin-review-table admin-pricing-pool" aria-label="Course pack prices">
              <thead>
                <tr><th scope="col">Level</th><th scope="col">Total credits</th><th scope="col">Content</th><th scope="col">Platform</th></tr>
              </thead>
              <tbody>
                {packs.map((row) => (
                  <tr key={row.id}>
                    <td><strong>{row.levelName}</strong></td>
                    <td className="admin-pricing-figure">{band(row.minTotalCredits, row.maxTotalCredits)}</td>
                    <td className="admin-pricing-share">{row.contentSharePercent}%</td>
                    <td className="admin-pricing-share">{row.platformSharePercent}%</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      ) : null}
    </>
  );
}
