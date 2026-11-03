const shareRoles = [
  ['Delivery share retained', 'retained'],
  ['Content share', 'content'],
  ['Delivery share', 'delivery'],
  ['Platform share', 'platform'],
];

export function shareRole(description) {
  const text = description.startsWith('Reversal of ') ? description.slice('Reversal of '.length) : description;
  return shareRoles.find(([prefix]) => text.startsWith(prefix))?.[1] ?? null;
}

export function shareTotals(rows) {
  const byUser = new Map();
  for (const row of rows) {
    const role = shareRole(row.description || '');
    if (!role) continue;
    const current = byUser.get(row.userId) || {
      userId: row.userId, name: row.userName, email: row.userEmail, content: 0, delivery: 0, retained: 0, platform: 0,
    };
    current[role] += row.delta;
    byUser.set(row.userId, current);
  }
  return [...byUser.values()]
    .map((row) => ({ ...row, net: row.content + row.delivery + row.retained + row.platform }))
    .sort((left, right) => left.name.localeCompare(right.name));
}
