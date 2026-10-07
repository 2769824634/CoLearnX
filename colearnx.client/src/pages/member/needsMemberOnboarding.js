export function needsMemberOnboarding(user) {
  return Boolean(user && 'onboardingCompletedAt' in user && !user.onboardingCompletedAt && !user.onboardingSkippedAt);
}
