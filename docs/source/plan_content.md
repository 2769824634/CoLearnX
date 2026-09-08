# CoLearnX Full Project Plan — Source Document

> English COMP3851A project plan source. Generated content aligns with `scripts/plan_data.py` and `CoLearnX_Project_Plan_Full.docx`.

## Document Control

| Field | Value |
|-------|-------|
| Project | CoLearnX Training Platform |
| Course | COMP3851A Semester 1, 2026 |
| Authors | Huang Yousheng C3546426; Zhu Zirui C3543467; Zou Ruiqi C3543468; Gu Yincheng C3543317 |
| Supervisor | Desmond Lee |
| Stack | React 19 + Vite 8 + ASP.NET Core 10 + EF Core + Bootstrap |

## Part I — Project Foundation

See `CoLearnX_Project_Plan_Technology_Updated.docx` for full academic Background, Aims, Methods, Ethics, and References. This document extends that base with SRS, use cases, user stories, diagrams, UI inventory, data model, API catalogue, and delivery plan.

## Part II — Requirements

### Personas

| Name | Role | Email | Notes |
|------|------|-------|-------|
| Huang Yousheng | Member | huang.yousheng@colearnx.com | 120 credits |
| Jane Smith | Trainer | jane.smith@colearnx.com | INFT2051 trainer |
| Alex Lee | Creator | alex.lee@colearnx.com | Content uploader |
| Desmond Tan | Administrator | desmond.tan@colearnx.com | Platform admin |

### Functional Requirements (FR-01 to FR-35)

See `scripts/plan_data.py` → `FUNCTIONAL_REQUIREMENTS` for the complete list covering Authentication, Member, Trainer, Creator, Admin, Credits, Certificates, and Notifications.

### Non-Functional Requirements (NFR-01 to NFR-10)

Performance, security (JWT/BCrypt), privacy (APPs), usability (Bootstrap responsive), maintainability, and transactional reliability for credit enrolment.

## Part III — Use Cases

### Catalogue (18 use cases)

| ID | Name | Actor | Priority | Screen |
|----|------|-------|----------|--------|
| UC-01 | Register Account | Guest | P0 | GLB-02 |
| UC-02 | Login | All | P0 | GLB-03 |
| UC-05 | Enrol with Credits | Member | P0 | MBR-03/04 |
| UC-07 | Upload Material | Creator | P0 | CRT-01 |
| UC-08 | Review Content | Admin | P0 | ADM-02 |
| UC-09 | Create Program | Trainer | P0 | TRN-02 |

Full catalogue in `plan_data.USE_CASE_CATALOGUE`.

### UC-05 Detailed Specification (Core)

**Enrol in Program with Credits**

- **Preconditions:** Member authenticated; program published; session open; balance ≥ cost
- **Main flow:** Open detail → select session → Enrol Now → confirm modal → atomic debit → redirect My Programs
- **Alternatives:** Insufficient credits; session full; user cancels
- **Business rules:** BR-01, BR-02, BR-04, BR-05

## Part IV — User Stories (28 stories)

Examples:

- **US-MBR-03:** As a member, I want to enrol using credits, so that I join without cash payment. (P0, UC-05, MBR-03/04)
- **US-TRN-03:** As a trainer, I want to attach library materials, so that content is attributed. (P0, UC-09)
- **US-ADM-01:** As an admin, I want to review content, so that library quality is maintained. (P0, UC-08)

Full backlog in `plan_data.USER_STORIES`.

## Part V — Diagrams

Mermaid sources: `docs/diagrams/*.mmd`  
Rendered PNGs: `docs/diagrams/*.png` (via `scripts/render_diagrams.py`)

| Figure | File | Description |
|--------|------|-------------|
| Fig 2 | fig02_architecture | System architecture |
| Fig 3 | fig03_use_case | Use case diagram |
| Fig 4 | fig04_er | ER diagram |
| Fig 5-9 | fig05-09_seq_* | Sequence diagrams |
| Fig 10 | fig10_activity_enrol | Enrolment activity |
| Fig 11 | fig11_state_enrollment | Enrollment states |

## Part VI — UI/UX

### Screen Inventory (27 screens)

Global: GLB-01 to GLB-06  
Member: MBR-01 to MBR-08  
Trainer: TRN-01 to TRN-05  
Creator: CRT-01 to CRT-03  
Admin: ADM-01 to ADM-05

### Wireframe mapping (low-fi PDF)

| PDF Frames | Screen IDs |
|------------|------------|
| 1-2 | GLB-03, GLB-02 |
| 6-7 | Onboarding |
| 9-10 | MBR-01 |
| 20-28 | MBR-03, MBR-04 |
| 29-34 | GLB-05 |
| 36-37 | MBR-08 |
| 42-44 | MBR-06 |

## Part VII — Data Design

### Core entities (27 total)

**User domain:** User, UserPreference, Interest, UserInterest, TrainerProfile, RefreshToken, PasswordResetToken, ExternalLogin

**Course domain:** Course, CourseLearningOutcome, CourseSession, LearningMaterial, MaterialTag, CourseMaterial, MaterialUsageLog, WishlistItem

**Enrolment:** Enrollment, CreditTransaction

**Certificates:** CertificateTemplate, UserCertificate, UserLearningProgress

**Payment:** SubscriptionPlan, UserSubscription, PaymentMethod, Invoice, PaymentTransaction

**Notification:** Notification

### Business rules

- BR-01: CreditBalance never negative
- BR-02: Every credit change has CreditTransaction
- BR-03: Material ownership immutable; usage logged
- BR-04: Only Member can enrol
- BR-05: Session must have capacity

## Part VIII — API

See `plan_data.API_ENDPOINTS` for REST catalogue. Key endpoints:

- POST `/api/auth/login`
- POST `/api/enrollments`
- POST `/api/materials`
- GET `/api/admin/credits/ledger`

## Part IX — Delivery

| Week | Focus |
|------|-------|
| 1-2 | Scaffold, routing, Bootstrap |
| 3-4 | Wireframe UI pages |
| 5-7 | EF Core, JWT, enrolment |
| 8-12 | Creator/Admin, testing, report |

## Regenerate DOCX

```bash
pip install python-docx matplotlib
python docs/scripts/build_plan_docx.py
```

Output: `docs/CoLearnX_Project_Plan_Full.docx`
