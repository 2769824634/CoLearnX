# CoLearnX 当前代码说明

本文说明仓库**现在实际怎么跑**，以代码为准。运行/演示账号见根目录 `README.md`；课程资料与 PayPal 联调见 `README_MATERIALS_PAYPAL_AZURE.md` 和 `DEVLOG_20260910_AZURE_PAYPAL.md`。设计线框见本目录其余文件，不要和实现混为一谈。

技术栈：React + Vite 前端，ASP.NET Core（`net10.0`）后端，EF Core + SQLite，JWT。Visual Studio F5 会同时拉起 API（默认 `https://localhost:7238`）和 Vite SPA（`https://localhost:55128`）；前端把 `/api` 代理到后端。

## 1. 仓库怎么分层

```text
CoLearnX.sln
├── CoLearnX.Server/              ASP.NET API + 启动时托管前端产物
│   ├── Program.cs                DI、JWT 双方案、存储选择、Seed
│   ├── Controllers/              HTTP 入口，尽量不写业务
│   ├── Services/                 业务规则
│   ├── Domain/Entities + Enums   表对应的实体与状态
│   ├── Data/                     DbContext、SeedData、EnsureCreated
│   ├── Contracts/Dtos/           请求/响应
│   ├── Auth/                     普通用户 JWT 与 Admin JWT
│   ├── Storage/                  本地磁盘或 Azure Blob
│   └── Payments/                 PayPal REST
├── CoLearnX.Server.Tests/        后端集成测试
└── colearnx.client/              Vite React SPA
    ├── src/api/                  fetch 封装
    ├── src/auth/                 两套登录态
    ├── src/routes/AppRouter.jsx  四角色路由
    ├── src/pages/{member,trainer,creator,admin}/
    └── src/layouts/RoleShell.jsx Trainer / Creator / Admin 壳
```

一次请求的路径：

```text
页面 → api/*.js → fetch('/api/...')
     → Vite proxy（开发）或同源静态托管（F5 生产式）
     → Controller → Service → SQLite / Azure / PayPal
     → DTO JSON 回前端
```

没有 SignalR。别人页面要看到新数据，需要 Refresh 或重新进入该页。

## 2. 两种身份，不要混用

| | 普通用户 | 管理员 |
|---|---|---|
| 表 | `User` + `UserRole` | 独立表 `AdminAccount` |
| 登录 | `/login` → `POST /api/auth/login` | 地址栏打开 `/admin/login` → `POST /api/admin/auth/login` |
| Token | `localStorage` 键 `colearnx.token` | `colearnx.admin.token` |
| JWT 方案 | 默认 Bearer，claim `sub_type=user` | 另一套 Bearer，`sub_type=admin` |
| 角色 | JWT 里的 `active_role`：Member / Trainer / Creator | 固定 Admin 策略 |

`AuthService` 登录时如果发现该邮箱是 Admin 账号，会拒绝并提示走 operations sign-in。注册默认只给 Member。一人可有多个 `UserRole`，`POST /api/auth/switch-role` 换 active role 并重发 token。

前端守卫：`RequireAuth` 按 `activeRole` 卡 `/member` `/trainer` `/creator`；`RequireAdmin` 卡 `/admin/*`。`App.jsx` 同时包了 `AuthProvider` 和 `AdminAuthProvider`。

写操作策略在 `Program.cs`：

- Trainer：`active_role=Trainer` 且 Role=Trainer
- Creator：`active_role=Creator` 且 Role=Creator
- Admin：Admin 方案 + 账号仍有效

## 3. 领域对象（读代码时抓住这几张表）

核心不是「一门课一个班级」，而是 **Course 定义** 和 **CourseIntake 开班** 分开。

```text
User ──UserRole── AppRole (Member/Trainer/Creator)
  │
  ├─ Course.CreatorId     谁拥有课程、确认 Intake、上传资料
  ├─ Course.TrainerId     历史/交付侧关联（Intake 上还有自己的 TrainerId）
  │
Course  1──* CourseIntake  1──* CourseSession  1──* Enrollment
  │            │
  │            ├─ CourseIntakeApplication   Creator 确认/拒绝
  │            └─ CourseIntakeMaterial      已批准资料绑到开班
  │
  └─ CourseMaterial ── LearningMaterial ── CourseMaterialVersion
```

状态机（名字在 `Domain/Enums`）：

| 对象 | 常见路径 |
|---|---|
| `Course` | Draft → PendingApproval → Published（可 Rejected 后改再提） |
| `CourseIntake` | Draft → PendingApproval（提交给 Creator）→ Published |
| `CourseIntakeApplication` | Pending → Confirmed / Rejected |
| `LearningMaterial` + `CourseMaterialVersion` | 上传即 PendingApproval → Admin Approve/Reject |
| `Enrollment` | Active / Completed / Cancelled / Refunded |
| `CertificateRequest` | Submitted → TrainerApproved → Admin Issued（Trainer 也可拒） |

要点：

- **Admin 不审核 Intake**。Trainer 提交后由该课的 Creator 确认。
- Member 报名的是某个 **Published Intake 下的 Session**，扣 `User.CreditBalance`，写 `CreditTransaction`。
- 资料必须有 `CourseMaterials` 行，不能当孤立文件。Trainer 只能把 **Approved** 且属于该 Intake 之 Course 的版本绑上去。

SQLite 文件是 `CoLearnX.Server/colearnx-later-v1.db`。`SeedData.InitializeAsync` 用 `EnsureCreated`，**没有 EF migration**。旧库缺 `CreatorId` / later-phase 表会直接抛错，需要删 db 后重启。

## 4. 后端：Controller 只转发，规则在 Service

`Program.cs` 把接口和实现成对注册。业务主要在：

| 文件 | 负责 |
|---|---|
| `AppServices.cs` | 登录、目录、Creator 建课、报名、积分/PayPal、资料上传下载 |
| `CourseIntakeService.cs` | Trainer Intake/Session CRUD、提交、Creator 审核申请 |
| `TrainerDeliveryService.cs` | 已发布 Intake 改会议链接 |
| `MaterialVersionService.cs` | 材料版本、Admin 批准 |
| `TrainerLaterPhaseService.cs` | 绑资料、录播、出勤、学员、评分 |
| `AdminCourseReviewService.cs` / `AdminRoleRequestService.cs` | 课程发布、角色申请 |
| `AdminFinanceService.cs` | 调账、争议退款 |
| `CertificateWorkflowService.cs` | 证书两级审核 |
| `AuditLogService.cs` | 审计；Admin 操作与 User 操作分列，约束「恰好一个 actor」 |

Controller 按 URL 切开，不要在一个文件里找全部 API：

| 前缀 | 类 | 谁能调 |
|---|---|---|
| `/api/auth` `/api/users` `/api/courses` `/api/enrollments` `/api/credits` `/api/materials` `/api/certificates` | `ApiControllers.cs` | 匿名目录 + 登录用户 |
| `/api/creator/courses` | `CreatorCoursesController` | Creator |
| `/api/creator/intake-applications` | `CreatorIntakeApplicationsController` | Creator |
| `/api/trainer/intakes` 等 | `TrainerIntakesController` | Trainer |
| `/api/trainer/learning-materials` 出勤评分等 | `TrainerLaterPhaseController` | Trainer |
| `/api/admin/auth` | `AdminAuthController` | 匿名登录 |
| `/api/admin/courses` `/api/admin/role-requests` `/api/admin/audit-logs` | 各 Admin*Controller | Admin |
| `/api/admin/material-versions` 证书/调账/争议 | `AdminLaterPhaseController` | Admin |
| `/api/disputes` | `DisputesController` | Member 提交，Admin 在 later-phase 处理 |

错误形态：普通接口多用 `ApiError(code, message)`；Intake / later-phase 用特性把领域异常打成字段级错误。

## 5. 前端：按角色拆页面，API 模块对齐后端

`AppRouter.jsx` 是路由表。

- **Member**（自有顶栏，不走 `RoleShell`）：目录、课程详情、已报名 Programs、Credit Wallet、徽章、账号。数据在 `MemberDataContext`。
- **Trainer** `RoleShell`：Homepage、Intakes 列表/新建/详情、出勤、学员。详情页里嵌 `TrainerResourcesPanel`（绑资料 + 录播 URL）。
- **Creator** `RoleShell`：Home、课程列表、Create/Edit Course（可带文件）、Upload Material、Intake 确认队列。`usage` 和部分账号页仍是占位。
- **Admin** `AdminRoleShell`：Homepage、Approvals（角色/课程/材料/证书队列）、Ledger、Disputes、Audit。普通登录页**没有** Admin 入口。

`src/api/client.js` 的 `apiRequest` 自动带用户 token；Admin 调用时显式传入 admin token（见 `adminLaterPhase.js`）。上传走 `FormData`（`asForm: true`）。

## 6. 四条主链路在代码里怎么走

### 6.1 建课并上传资料

1. Creator：`CreatorCourseFormPage` → `POST /api/creator/courses`（`CourseService.CreateForCreatorAsync`，Draft）。
2. 同一页选文件：课程已有 id 则立刻 `POST /api/materials` multipart（`courseId` 必填）；新建则先排队，保存成功再传。
3. `MaterialService.UploadAsync`：校验 Creator 拥有该课 → `IFileStorage.SaveAsync` → `MaterialVersionService.CreateAsync` 写 `LearningMaterial` + version + `CourseMaterials`。
4. `POST .../submit` 把 Course 打成 PendingApproval。Pending/Published 后创建页锁定，不能再从这里加文件（Upload 页仍可给未锁的课传）。

文件：`Storage/AzureBlobFileStorage`（配置了连接串）或 `LocalFileStorage`（`App_Data/uploads`）。键形如 `materials/{creatorId}/{guid}.pdf`。列表/下载：本人或 `Approved` 才能看文件；pending 不能给 Member 下。

### 6.2 Admin 发布课程并批准材料

- 课程：`AdminCourseReviewService`，`POST /api/admin/courses/{id}/review`。
- 材料：`GET /api/admin/material-versions`，Approve 后 `LearningMaterial.Status = Approved`，Trainer 下拉框才会出现。

### 6.3 Trainer 开班，Creator 确认，Member 报名

1. 仅 **Published** Course 可 `POST /api/trainer/courses/{id}/intakes`。
2. 加 Session（线上要有会议链接，线下要有地址和容量）。提交校验至少 1 个 Session。
3. 提交后 Intake=`PendingApproval`，生成 `CourseIntakeApplication`。
4. Creator：`/creator/courses/intake-applications` → Confirm 则 Intake=`Published`。
5. Member 目录来自 `GET /api/courses`（已发布）。`EnrollmentService.EnrolAsync` 扣积分、占座。`SeatsTaken >= PhysicalCapacity` 即满员，**容量 0 永远报满**。

### 6.4 资料出现在别人页面

同一进程、同一 SQLite、同一 Blob。Creator 上传后 Admin 刷新材料队列即可看到 Pending；批准后 Trainer 打开 Intake 资源面板刷新可选；绑到 Intake 后已报名 Member 再进 Learning Hub 刷新。不是推送。

### 6.5 PayPal 充值

`MemberPaymentPage` 加载套餐和 PayPal Buttons。`createOrder` → `POST /api/credits/paypal/create-order`（带 `origin/member/payment`）。`PayPalReturnUrls` 只允许 localhost / `*.devtunnels.ms`。capture 成功写 `PaymentTransaction` + `CreditTransaction`，加余额。已配置 PayPal 时模拟 `POST /api/credits/topup` 应关闭（UI 已走按钮）。密钥用 user-secrets，见开发日志。

### 6.6 出勤、评分、证书、争议

Trainer later-phase：按 Intake/Session 写出勤、建 Assessment、给 Enrollment 打分。学员提交证书申请 → Trainer 初审 → Admin 终审签发 `UserCertificate`。Member 可开 Dispute；Admin Refund 会改报名状态并退积分，写审计。

## 7. 启动时务必看的几处

- `Program.cs`：选 Azure 还是本地存储，并打 `File storage: ...` 日志。
- `SeedData.cs`：演示账号（密码 `Password123!`）和种子课。Admin 邮箱在 `AdminAccounts`，不在 `Users`。
- `vite.config.js`：`allowedHosts: true` 是为了 Dev Tunnel。
- 测试：`CoLearnX.Server.Tests`（工厂里强制 `LocalFileStorage`）；前端有 Creator 课程表单和若干 Intake/API 单测。F5 占用 `CoLearnX.Server.exe` 时，测后端用 `dotnet test -p:UseAppHost=false`。

## 8. 读代码的推荐顺序

1. `Program.cs` + `AppRouter.jsx`（系统边界）
2. `Domain/Entities/Entities.cs` + `LaterPhaseEntities.cs` + 两个 Enums 文件（状态）
3. `CourseService` / `CourseIntakeService` / `EnrollmentService`（主业务）
4. `MaterialService` + `Storage/` + `MaterialVersionService`（文件）
5. `CreditService` + `Payments/PayPalClient.cs`（钱）
6. 对应 `pages/*` 看 UI 如何调 `src/api`

仍是占位、不要当成已完成产品的：Creator Usage Records、部分 My Account 编辑、正式 EF migration、自定义生产域名的 PayPal return URL（需改 `PayPalReturnUrls`）。
