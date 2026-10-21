export const COURSE_CATEGORIES = ['Programming', 'Design', 'Business'];

export function categoryChoices(courseCategory) {
  const current = courseCategory?.trim();
  return current && !COURSE_CATEGORIES.includes(current) ? [current, ...COURSE_CATEGORIES] : COURSE_CATEGORIES;
}

export function startingCategory(courseCategory) {
  const current = courseCategory?.trim();
  return current && categoryChoices(courseCategory).includes(current) ? current : 'Design';
}
