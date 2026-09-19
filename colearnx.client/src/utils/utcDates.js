// Server audit timestamps are UTC; SQLite materializes DateTime without its Kind.
export function utcDate(value) {
  return new Date(/(?:Z|[+-]\d{2}:\d{2})$/i.test(value) ? value : `${value}Z`);
}
