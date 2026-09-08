# CoLearnX Design Tokens & UI Framework

> **Status:** Confirmed — sync with the whole team  
> **Sources:** `docs/wireframes/drawio/` · `colearnx.client/src/styles/member.css`  
> **Rule:** Four roles share **one** palette and **one** shell. Do not invent role-specific themes.

---

## 1. Brand colours

| Token | Hex | CSS variable | Use |
|-------|-----|--------------|-----|
| Primary Purple | `#7241FF` | `--purple` | Primary buttons, active nav, brand emphasis, links |
| Teal | `#10D2AF` | `--teal` | Success / confirm, secondary accent, positive state |
| Charcoal | `#333333` | `--charcoal` | Headings, body text |
| Slate | `#5F6F81` | `--slate` | Secondary / helper text |
| White | `#FFFFFF` | `--white` | Cards, content surface |
| Border | `#E8ECF0` | `--border` | Borders, dividers |
| Chrome | `#F8F9FB` | `--chrome` | Topbar / sidebar light chrome |
| Page BG | `#EEF1F5` | `--page-bg` | Outer page background |
| Purple Soft | `#F0EBFF` | `--purple-soft` | Active nav background, soft purple fill |
| Teal Soft | `#E6FAF6` | `--teal-soft` | Soft success / pill background |
| Warn | `#F59E0B` | `--warn` | Warning |
| Danger (Admin) | `#EF4444` | `--danger` | Reject, refund, high-risk Admin actions |

### Colour rules

- All roles use the **same** palette.
- Primary CTA → **Purple**. Confirm / success → **Teal**.
- Only Admin high-risk actions use **Red `#EF4444`**.

---

## 2. App shell (base layout)

```text
┌─────────────────────────────────────────────┐
│ Topbar: Logo · Search · Notify · User/Logout │
├──────────┬──────────────────────────────────┤
│ Sidebar  │ Content                          │
│ Nav      │ Page title + subtitle + body     │
└──────────┴──────────────────────────────────┘
```

| Region | Spec |
|--------|------|
| Topbar | White / Chrome; `:neXt` logo left; search centre; notify + user right |
| Sidebar | Fixed left nav; active = Purple Soft fill + left Purple bar |
| Content | White surface; title Charcoal; subtitle Slate |
| Radius | `8px` (`--radius`) |
| Font | `'Segoe UI', 'Nunito Sans', system-ui, sans-serif` |

Frontend reference: `MemberShell` + `colearnx.client/src/styles/member.css`.

---

## 3. Role navigation (do not rename)

| Role | Nav items |
|------|-----------|
| **Member** | Home · Courses · My Programs · Payment · Badges · My Account |
| **Trainer** | Homepage · Course · Attendance · Learner List · My Account |
| **Creator** | Home · Courses · Upload Material · Usage Records · My Account |
| **Admin** | Homepage · Approvals · Users · Credit Ledger · Disputes · Audit Log · My Account |

---

## 4. Shared interaction rules

1. **Login:** email / password + **Continue as** (Member / Trainer / Creator). Admin uses a **separate** invited entry.
2. **My Account:** page is read-only with masked contact; edits go through **Edit Profile** modal.
3. **Buttons:** Primary = Purple; confirm / success = Teal; danger = Red (Admin only).
4. **Payment:** PayPal only; six fixed credit packages; no custom amount.

---

## 5. CSS variables (copy into shared styles)

```css
:root {
  --purple: #7241ff;
  --purple-soft: #f0ebff;
  --teal: #10d2af;
  --teal-soft: #e6faf6;
  --charcoal: #333333;
  --slate: #5f6f81;
  --white: #ffffff;
  --border: #e8ecf0;
  --chrome: #f8f9fb;
  --page-bg: #eef1f5;
  --warn: #f59e0b;
  --danger: #ef4444;
  --radius: 8px;
  --font: 'Segoe UI', 'Nunito Sans', system-ui, sans-serif;
}
```

Wireframe generators use the same hex values in `docs/wireframes/generators/*.py`.

---

## 6. Team sync note (short)

Brand colours are locked: Primary `#7241FF`, Teal `#10D2AF`, Charcoal `#333333`, Slate `#5F6F81`; Admin danger `#EF4444`. Four roles share one shell (Topbar + Sidebar + Content) and one palette — no separate themes. Navigation labels follow the wireframes. Reuse CSS variables in `member.css`; do not hard-code new colours on new pages.
