import { materialsApi } from '../api';
import { ApiError } from '../api/client';

async function copyText(text) {
  if (navigator.clipboard?.writeText) {
    await navigator.clipboard.writeText(text);
    return;
  }
  const input = document.createElement('textarea');
  input.value = text;
  document.body.appendChild(input);
  input.select();
  document.execCommand('copy');
  input.remove();
}

// Shared library table. Keep: MaterialList
export default function MaterialList({ items, cloudLinks, onError, onCopied }) {
  async function onDownload(item) {
    try {
      await materialsApi.download(item.id, `${item.title}.${String(item.format || 'bin').toLowerCase()}`);
    } catch (err) {
      onError?.(err instanceof ApiError ? err.message : 'Download failed.');
    }
  }

  async function onCopyCloudLink(item) {
    try {
      const link = await materialsApi.cloudLink(item.id);
      await copyText(link.url);
      onCopied?.('Cloud link copied.');
    } catch (err) {
      onError?.(err instanceof ApiError ? err.message : 'Cloud link is not available for this file.');
    }
  }

  if (!items.length) {
    return <p style={{ margin: 0, color: 'var(--slate)', fontSize: 13 }}>No materials yet.</p>;
  }

  return (
    <table className="data-table">
      <thead>
        <tr>
          <th>Title</th>
          <th>Course</th>
          <th>Format</th>
          <th>Status</th>
          <th>Creator</th>
          <th />
        </tr>
      </thead>
      <tbody>
        {items.map((item) => (
          <tr key={item.id}>
            <td>{item.title}</td>
            <td>{item.courseCode ? `${item.courseCode} — ${item.courseTitle}` : '—'}</td>
            <td>{item.format}</td>
            <td>
              <span className={`pill${item.status === 'Approved' ? ' success' : ' muted'}`}>{item.status}</span>
            </td>
            <td>{item.creatorName}</td>
            <td>
              <button type="button" className="btn btn-ghost btn-sm" onClick={() => onDownload(item)}>
                Download
              </button>
              {cloudLinks ? (
                <button type="button" className="btn btn-ghost btn-sm" onClick={() => onCopyCloudLink(item)}>
                  Copy cloud link
                </button>
              ) : null}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
