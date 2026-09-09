export default function CreatorPageFrame({ eyebrow, title, description, children }) {
  return (
    <section aria-labelledby="creator-page-title">
      <p className="section-title">{eyebrow}</p>
      <h1 id="creator-page-title" className="page-title">{title}</h1>
      <p className="page-sub">{description}</p>
      {children}
    </section>
  );
}
