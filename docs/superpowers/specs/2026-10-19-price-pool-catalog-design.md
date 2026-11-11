# Price pool catalog (block 2)

Branch: `feat/price-pool-catalog`, from the latest `master`.

This block adds the pricing reference data that later blocks read when a course is priced. It adds three tables, seeds them, and gives Admin a Pricing page to manage domains and read the pool.

This block does **not** change `Course`, the course form, enrollment, or any price a learner pays today.

## Background

Later blocks price a course from four choices fixed when the course is submitted:

- Course type: `Theory` or `Practice`. A practice course is led by a Trainer.
- Domain. The domain selects a price tier.
- Level: the existing `Beginner`, `Intermediate`, `Advanced` (`CourseLevels` table).
- Sale mode: `Intake` or `CoursePack`. A course pack contains theory materials only.

The unit is credits per learner per contact hour. One credit equals one Australian dollar at the base top-up rate. Credit top-up stays `CreditPackage`. A course pack is `CoursePack`, and the interface says "course pack".

Block 2 only stores the reference data. Blocks 3–6 use it (see "Rules for later blocks").

## Scope

In scope:

1. Entities and tables `PriceTier`, `CourseDomain`, `PricePool`, plus enums `CourseType` and `SaleMode`.
2. An idempotent schema upgrade for existing SQLite and SQL Server databases.
3. Seed data: 5 tiers, 8 domains, 30 intake cells, 3 course-pack cells.
4. Admin API: list tiers, list domains, create domain, edit domain, deactivate domain, list pool.
5. An audit log row for each domain create, edit, and deactivate.
6. Admin Pricing page with a Domains tab and a Price pool tab.

Out of scope:

- Any column on `Course` (domain, course type, sale mode, hours, hourly price).
- Any change to the creator course form or Admin course review.
- Editing tiers or pool cells from the interface. Both are read-only in this block.
- Reactivating or deleting a domain.
- A creator-facing domain list. Block 3 adds it and shows active domains only.

## Data model

Add the entities to `CoLearnX.Server/Domain/Entities/Entities.cs` and the enums to `CoLearnX.Server/Domain/Enums/DomainEnums.cs`. Add `DbSet`s `PriceTiers`, `CourseDomains`, `PricePools` to `CoLearnXDbContext`.

```csharp
public enum CourseType { Theory = 0, Practice = 1 }
public enum SaleMode { Intake = 0, CoursePack = 1 }
```

### PriceTier

| Column | Type | Rule |
|---|---|---|
| `Id` | int | Primary key |
| `Code` | string, max 1 | `A`–`E`, unique |
| `TheoryMinHourlyCredits` | int | L of the theory band |
| `TheoryMaxHourlyCredits` | int | H of the theory band |
| `PracticeMinHourlyCredits` | int | L of the practice band |
| `PracticeMaxHourlyCredits` | int | H of the practice band |
| `SortOrder` | int | A = 1 … E = 5 |

### CourseDomain

| Column | Type | Rule |
|---|---|---|
| `Id` | int | Primary key |
| `Name` | string, max 80 | Required, trimmed, unique case-insensitively across active and inactive domains |
| `PriceTierId` | int | Foreign key to `PriceTiers`, delete restricted |
| `IsActive` | bool | Defaults to `true` |
| `CreatedAt` | DateTime (UTC) | Set on create |
| `UpdatedAt` | DateTime (UTC) | Set on create, edit, and deactivate |

### PricePool

One row per price cell.

| Column | Type | Intake row | Course-pack row |
|---|---|---|---|
| `Id` | int | Primary key | Primary key |
| `SaleMode` | `SaleMode` | `Intake` | `CoursePack` |
| `PriceTierId` | int, nullable | Required | `null` (all domains) |
| `CourseType` | `CourseType` | `Theory` or `Practice` | `Theory` |
| `CourseLevelId` | int | Foreign key to `CourseLevels` | Foreign key to `CourseLevels` |
| `MinHourlyCredits` | int, nullable | Required | `null` |
| `MaxHourlyCredits` | int, nullable | Required | `null` |
| `MinHours` | int, nullable | Required | `null` |
| `MaxHours` | int, nullable | Required | `null` |
| `MinTotalCredits` | int, nullable | `null` | Required |
| `MaxTotalCredits` | int, nullable | `null` | Required |
| `ContentSharePercent` | int | Creator share | Creator share |
| `DeliverySharePercent` | int | Teaching (theory) or practice guidance (practice) | `0` |
| `PlatformSharePercent` | int | Platform share | Platform share |

Rules:

- Unique index on `(SaleMode, PriceTierId, CourseType, CourseLevelId)`.
- The three share columns add up to 100 on every row.
- Shares apply to collected tuition after materials and venue fees.

## Seed data

### Tiers and domains

| Tier | Theory band (L–H) | Practice band (L–H) | Domains |
|---|---:|---:|---|
| A | 40–120 | 60–150 | Business and Management; Education and Career |
| B | 30–100 | 40–120 | IT and Data; Design and Media |
| C | 20–60 | 30–100 | Health and Care; Household and Hospitality |
| D | 15–40 | 25–70 | Lifestyle and Crafts |
| E | 15–50 | 25–80 | Languages and Arts |

### How an intake cell is derived

For a band from L to H, the midpoint M is `(L + H) / 2`, rounded up.

| Level | Hourly credits | Theory hours | Practice hours |
|---|---|---:|---:|
| Beginner | L to M | 2–12 | 2–16 |
| Intermediate | L to H | 12–40 | 16–60 |
| Advanced | M to H | 24–80 | 40–200 |

Shares: theory is content 25, delivery 25, platform 50. Practice is content 10, delivery 45, platform 45.

The seed computes the cells from the tier bands with this formula. A test checks the result against the table below.

### The 30 intake cells

Hourly credits per learner per contact hour.

| Tier | Type | Beginner | Intermediate | Advanced |
|---|---|---:|---:|---:|
| A | Theory | 40–80 | 40–120 | 80–120 |
| A | Practice | 60–105 | 60–150 | 105–150 |
| B | Theory | 30–65 | 30–100 | 65–100 |
| B | Practice | 40–80 | 40–120 | 80–120 |
| C | Theory | 20–40 | 20–60 | 40–60 |
| C | Practice | 30–65 | 30–100 | 65–100 |
| D | Theory | 15–28 | 15–40 | 28–40 |
| D | Practice | 25–48 | 25–70 | 48–70 |
| E | Theory | 15–33 | 15–50 | 33–50 |
| E | Practice | 25–53 | 25–80 | 53–80 |

Hours on each row come from the level table above: theory 2–12, 12–40, 24–80, and practice 2–16, 16–60, 40–200.

### The 3 course-pack cells

All domains. `PriceTierId` is `null` and `CourseType` is `Theory`.

| Level | Total credits | Content | Delivery | Platform |
|---|---:|---:|---:|---:|
| Beginner | 10–100 | 50 | 0 | 50 |
| Intermediate | 50–400 | 50 | 0 | 50 |
| Advanced | 150–1000 | 50 | 0 | 50 |

### Seeding rules

- Add `PricingSeed.EnsureAsync(db)` and call it from `SeedData.InitializeAsync` after `EnsureCourseTaxonomyAsync`, because pool rows reference `CourseLevels`.
- Insert a tier only when its `Code` is missing. Insert a domain only when its `Name` is missing. Insert a pool row only when its unique key is missing.
- Never overwrite existing rows. Running the seed again must not undo an Admin edit to a domain.
- Seed in production as well. This is reference data, not demo data.

## Schema upgrade

`EnsureCreated` builds the three tables on a new database from the EF model. It does not add tables to an existing database.

Add `CoLearnX.Server/Data/PricingSchema.cs`, following `SessionMaterialSchema.cs`:

- SQLite: create each table when it is missing from `sqlite_master`, then `CREATE UNIQUE INDEX IF NOT EXISTS` for the unique keys.
- SQL Server: `IF OBJECT_ID(N'dbo.<Table>', N'U') IS NULL` create, then add each index only when missing from `sys.indexes`.
- Call `PricingSchema.EnsureAsync(db)` in `SeedData.InitializeAsync` next to `SessionMaterialSchema.EnsureAsync(db)`, before the seed.
- Table names, column names, types, foreign keys, and indexes must match what `EnsureCreated` produces from the EF model, so a new database and an upgraded database are the same.

## Admin API

New controller `AdminPricingController`, route `api/admin/pricing`, `[Authorize(Policy = AdminAuthorization.PolicyName)]`. Business rules live in a new `IAdminPricingService` / `AdminPricingService`. Errors use `LaterPhaseException` (code, message, status, field), like the existing later-phase endpoints. The admin id comes from `User.GetAdminAccountId()`.

| Method and path | Body | Result |
|---|---|---|
| `GET /api/admin/pricing/tiers` | — | Tiers ordered by `SortOrder`, with both bands |
| `GET /api/admin/pricing/domains` | — | All domains, active and inactive, ordered by tier then name, each with tier code |
| `POST /api/admin/pricing/domains` | `{ name, priceTierId }` | 201 with the domain |
| `PUT /api/admin/pricing/domains/{id}` | `{ name, priceTierId }` | 200 with the domain |
| `POST /api/admin/pricing/domains/{id}/deactivate` | — | 200 with the domain, `isActive: false` |
| `GET /api/admin/pricing/pool` | — | All 33 rows: intake rows ordered by tier, type, level, then course-pack rows by level. Each row includes tier code (or `null`) and level name |

Validation and errors:

| Case | Status | Code | Field |
|---|---|---|---|
| Name empty after trim, or longer than 80 | 400 | `DOMAIN_NAME_INVALID` | `name` |
| Name already used by another domain, ignoring case | 409 | `DOMAIN_NAME_TAKEN` | `name` |
| Tier id does not exist | 400 | `PRICE_TIER_NOT_FOUND` | `priceTierId` |
| Domain id does not exist | 404 | `DOMAIN_NOT_FOUND` | — |
| Edit or deactivate an inactive domain | 409 | `DOMAIN_INACTIVE` | — |
| Caller is not an Admin | 401 or 403 | Existing behaviour | — |

Deactivate sets `IsActive = false` and keeps the row. Nothing is deleted.

### Audit log

Call `IAuditLogService.AppendAdminAsync` once per successful change. Failed requests write no audit row.

| Action | `EntityType` | `EntityId` | `Result` | `Reason` |
|---|---|---|---|---|
| `CourseDomainCreated` | `CourseDomain` | domain id | `Success` | `Name: <name>; Tier: <code>` |
| `CourseDomainUpdated` | `CourseDomain` | domain id | `Success` | Changed fields only, for example `Tier: B -> A` or `Name: IT -> IT and Data` |
| `CourseDomainDeactivated` | `CourseDomain` | domain id | `Success` | `Name: <name>` |

## Admin interface

- Add `{ to: '/admin/pricing', label: 'Pricing' }` to the admin nav in `colearnx.client/src/layouts/RoleShell.jsx`, between Disputes and Audit Log.
- Add route `pricing` under the admin routes in `AppRouter.jsx`, page `AdminPricingPage`.
- Add `adminPricingApi` to `colearnx.client/src/api/index.js` for the six endpoints.
- Use the existing admin page styles, `data-table`, `Modal`, `btn` classes, and the existing error presentation. Do not add a new design language.

### Domains tab

- Table columns: Name, Tier, Status (Active or Inactive), Actions.
- Add domain opens a `Modal` with Name (text) and Tier (select A–E, each option shows its theory and practice band).
- Edit opens the same modal filled in. Not offered on inactive rows.
- Deactivate opens a confirmation modal: "Deactivate <name>? New courses can no longer choose it. Courses that already use it keep it." Not offered on inactive rows.
- Show server field errors next to the field, and other errors at the top of the modal.

### Price pool tab

Read-only.

- Intake table: Tier, Type, Level, Hourly credits (`min–max`), Hours (`min–max`), Content / Delivery / Platform shares.
- A tier filter (All, A–E) above the intake table.
- Course pack table: Level, Total credits (`min–max`), Content / Platform shares.

## Decisions made in this spec

- **Tier changes apply to later submissions.** Block 2 stores only the current tier on the domain and does not version pool cells. Block 3 copies the tier, hourly range, hours range, and share rates onto the course when it is submitted, so a later tier change cannot affect a submitted course.
- **Course-pack cells** use `PriceTierId = null` and `CourseType = Theory`, because pack prices apply to all domains and a pack holds theory materials only.
- **Share columns** are named by role, not by sale mode: content goes to the creator, delivery to the trainer, platform to the platform.
- **Fee cap, payout delay, and completion thresholds** are not stored in block 2. They are listed under "Rules for later blocks".
- **One Pricing page** with two tabs instead of two nav entries.

Change any of these before implementation starts, not during it.

## Tests

Backend, `dotnet test CoLearnX.Server.Tests -p:UseAppHost=false`:

- Seed on a new database creates 5 tiers, 8 domains, 30 intake cells, and 3 course-pack cells.
- Every intake cell matches the 30-cell table, and every row's shares add up to 100.
- Running the seed twice creates no duplicates and does not undo an edited domain name or tier.
- Schema upgrade: an existing database without the three tables gets them, and running the upgrade twice succeeds. Follow `SessionMaterialSchemaUpgradeTests`.
- A non-admin caller gets 401 or 403 on every endpoint.
- Create, edit, and deactivate each succeed and each write exactly one audit row with the action above.
- Duplicate name (any case) returns 409 `DOMAIN_NAME_TAKEN`. Unknown tier returns 400 `PRICE_TIER_NOT_FOUND`.
- Deactivate keeps the row, and the domain list still returns it with `isActive: false`.
- Editing or deactivating an inactive domain returns 409 `DOMAIN_INACTIVE` and writes no audit row.

Frontend, from `colearnx.client`, `npm test`:

- The admin nav shows Pricing and highlights it on `/admin/pricing`.
- The Domains tab lists domains with tier and status.
- Add domain posts name and tier, and the new row appears.
- A 409 name error shows next to the Name field.
- Deactivate asks for confirmation, then the row shows Inactive with no Edit or Deactivate action.
- The Price pool tab shows 30 intake rows, filters by tier, and shows 3 course-pack rows.

Existing tests stay passing.

## Rules for later blocks (context only, do not implement in block 2)

- A deactivated domain cannot be chosen for a new course. Block 3 lists active domains only and rejects an inactive domain on submit.
- Total credits equal contact hours times the hourly price.
- Intermediate and Advanced courses include at least one assessment item.
- Practice hours are more than half of a practice course.
- Materials and venue fees sit inside the total and are at most 30 percent of it.
- A course-pack price change needs approval again and applies only to later buyers.
- The creator share of a course pack becomes payable 14 days after purchase.
- Share rates are copied from the pool onto the course when it is saved.
- Minimum enrollment stays 2–200. No one-to-one classes.
- Completion (block 6): theory attendance at least 80 percent, practice at least 90 percent. A creator may raise a threshold, not lower it.
- First aid, responsible service of alcohol, construction white card, food-safety supervision, and vocational qualification courses stay outside the price pool.

## Workflow

Follow `docs/ENGINEERING.md`:

1. Branch `feat/price-pool-catalog` from the latest `master`.
2. Write a failing test, then the smallest implementation that passes.
3. One topic per branch. Commit subjects are one English line and state why.
4. Run both test commands and read the output before calling the work done.
5. List the branch, commits, and impact, and push or merge only after confirmation.
