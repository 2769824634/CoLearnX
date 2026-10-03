export const PASSWORD_RULES = [
  { id: 'length', label: 'Password must be 10–72 characters.', hint: '10–72 characters', test: (password) => password.length >= 10 && password.length <= 72 },
  { id: 'case', label: 'Password must include uppercase and lowercase letters.', hint: 'Uppercase and lowercase letters', test: (password) => /[a-z]/.test(password) && /[A-Z]/.test(password) },
  { id: 'number', label: 'Password must include a number.', hint: 'A number', test: (password) => /\d/.test(password) },
  { id: 'symbol', label: 'Password must include a symbol.', hint: 'A symbol', test: (password) => /[^A-Za-z0-9]/.test(password) },
];

export function passwordIssues(password, email = '') {
  const issues = PASSWORD_RULES.filter((rule) => !rule.test(password)).map((rule) => rule.label);
  const normalizedPassword = password.toLowerCase();
  const normalizedEmail = email.trim().toLowerCase();
  const local = normalizedEmail.split('@')[0] || '';
  if (normalizedEmail && normalizedPassword.includes(normalizedEmail)) {
    issues.push('Password must not include your email address.');
  } else if (local.length >= 3 && normalizedPassword.includes(local)) {
    issues.push('Password must not include your email name.');
  }
  return issues;
}
