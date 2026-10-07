export const MAIN_CONTENT_ID = 'main-content';

// Shared by MemberShell and RoleShell. Focuses <main> directly so the router URL keeps no hash.
export default function SkipToContent() {
  return (
    <a
      className="skip-link"
      href={`#${MAIN_CONTENT_ID}`}
      onClick={(event) => {
        event.preventDefault();
        document.getElementById(MAIN_CONTENT_ID)?.focus();
      }}
    >
      Skip to main content
    </a>
  );
}
