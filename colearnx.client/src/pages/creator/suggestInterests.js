// Up to two interests whose name shares a word with the course title.
export function suggestInterests(title, leaves) {
  const words = title.toLowerCase().split(/[^a-z0-9]+/).filter((word) => word.length > 2);
  return leaves.filter((leaf) => words.some((word) => leaf.name.toLowerCase().split(/[^a-z0-9]+/).includes(word))).slice(0, 2).map((leaf) => leaf.id);
}
