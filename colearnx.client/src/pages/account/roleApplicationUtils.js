export function countStatementWords(text) {
  if (!text) return 0;
  let count = 0;
  let inLatin = false;
  for (const ch of text) {
    const code = ch.codePointAt(0);
    const isHan = (code >= 0x3400 && code <= 0x4dbf)
      || (code >= 0x4e00 && code <= 0x9fff)
      || (code >= 0xf900 && code <= 0xfaff);
    if (isHan) {
      if (inLatin) {
        count += 1;
        inLatin = false;
      }
      count += 1;
    } else if (/\s/.test(ch)) {
      if (inLatin) {
        count += 1;
        inLatin = false;
      }
    } else if (/[\p{L}\p{N}]/u.test(ch)) {
      inLatin = true;
    }
  }
  if (inLatin) count += 1;
  return count;
}
