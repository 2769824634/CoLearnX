# CoLearnX Framework Map
# Aligns with docs/diagrams/fig02_architecture.mmd

## Backend (CoLearnX.Server)
Domain/Enums + Domain/Entities   # BR entities (User/Course/Enrollment/Credit/...)
Data/CoLearnXDbContext + SeedData
Auth/JwtTokenService             # JWT + active_role claim
Services/*                       # Business rules (enrol, top-up, ledger)
Controllers/*                    # /api/auth|courses|enrollments|credits|...
Contracts/Dtos                   # Request/response contracts

## Frontend (colearnx.client)
api/                             # HTTP client + resource modules
auth/                            # AuthProvider + RequireAuth (RBAC routes)
routes/AppRouter.jsx             # /member|/trainer|/creator|/admin
pages/member/*                   # Member UI wired to API
layouts/RoleShell.jsx            # Trainer/Creator/Admin shells (stubs)

## Demo accounts (password: Password123!)
Member(+Trainer): huang.yousheng@colearnx.com
Trainer:          jane.smith@colearnx.com
Creator:          alex.lee@colearnx.com
Admin:            desmond.tan@colearnx.com
