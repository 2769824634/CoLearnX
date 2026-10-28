# Admin 待办提醒与审计筛选（Admin 对齐第 2 项）

分支：`feat/admin-pending-alerts`，从 `master`（`18ed189`）切出，在单独的工作目录 `CoLearnX-admin-alerts` 开发。

## 背景

- Admin 首页只统计角色申请和待审课程。资料版本、证书终审、争议三类待办只能逐个页面去看。
- 审计日志的「资源」筛选只有 3 项，系统实际写入 11 类资源；「结果」筛选只列 5 个值。
- 现有实时推送只认用户账号，Admin 账号连不上。

## 范围

做：

- 一个接口返回五类待办数量；Admin 首页、导航角标、审批页页签都用它。
- Admin 页面定时刷新待办数量，数量变多时弹提示。
- 审计日志的资源和结果筛选项由后端按实际日志提供。

不做：

- Admin 实时推送（选定方案 A：定时刷新；以后需要再加，接口可沿用）。
- Admin 通知列表或已读状态。
- 修改任何审批流程本身。

## 待办口径

与各队列页面现在显示的一致：

| 字段 | 统计条件 | 对应队列 |
|---|---|---|
| `roleRequests` | `RoleRequest.Status == Pending` | `/admin/approvals?queue=roles` |
| `courses` | `Course.Status == PendingApproval` | `/admin/approvals?queue=courses` |
| `materialVersions` | `CourseMaterialVersion.Status == PendingApproval` | `/admin/approvals?queue=materials` |
| `certificates` | `CertificateRequest.Status == TrainerApproved` | `/admin/approvals?queue=certificates` |
| `disputes` | `Dispute.Status == Open` | `/admin/disputes` |

## 接口

两个接口都要求 `AdminAuthorization.PolicyName`。

- `GET api/admin/pending-counts` → `AdminPendingCountsDto(int RoleRequests, int Courses, int MaterialVersions, int Certificates, int Disputes)`。放在 `AdminController`，只做五个 `CountAsync`。
- `GET api/admin/audit-logs/filters` → `AuditLogFilterOptionsDto(IReadOnlyList<string> EntityTypes, IReadOnlyList<string> Results)`。`AuditLogService` 新增 `FilterOptionsAsync`，对 `EntityType`、`Result` 各取去重值，按序数排序。

## 前端

### 待办数量（`pages/admin/AdminPendingProvider.jsx`）

- `AdminRoleShell` 外面包一层 `AdminPendingProvider`；`useAdminPending()` 取到 `{ counts, refresh }`，没有 Provider 时返回 `null`，单独渲染的页面不受影响。
- 刷新时机：挂载时一次；之后每 30 秒一次；窗口重新可见（`visibilitychange`）或获得焦点时一次。页面在后台（`document.hidden`）时跳过定时刷新。
- 取数失败：保留上一次的数字，不显示错误，下次照常再试。
- 新提交提示：和上一次成功取到的结果比，五类合计变多时，在页面右上角显示一条提示「N new items are waiting for review」，链接到 `/admin/approvals`，可以关闭。第一次加载不提示。
- 审批后立即刷新：角色申请、课程、资料、证书、争议五处审批成功后调用 `refresh()`。

### 导航角标

- `Approvals` 显示 `roleRequests + courses + materialVersions + certificates`；`Disputes` 显示 `disputes`。
- 为 0 或还没取到时不显示；超过 99 显示 `99+`。
- 角标对读屏隐藏，链接的无障碍名称写成「Approvals, 4 pending」。

### 审批页

四个队列页签各显示自己的数量（为 0 不显示）。

### 首页

- 五张卡片：Role requests、Courses to review、Material versions、Certificates、Disputes，各显示数量并链接到对应队列。
- 首页自己取一次 `pending-counts` 和最近 5 条日志，不再为数个数去取完整列表。
- 五类都为 0 时显示「All caught up. Nothing is waiting for review.」。

### 审计日志筛选

- 资源和结果下拉框的选项来自 `audit-logs/filters`；接口失败时只保留「All」选项，日志本身照常显示。
- `adminDisplay.js` 新增 `entityTypeLabel`：已知类型给友好名称，未知类型显示原名。

| 资源类型 | 显示 |
|---|---|
| `AdminAccount` | Admin accounts |
| `CertificateRequest` | Certificate requests |
| `Course` | Courses |
| `CourseDomain` | Course domains |
| `CourseIntake` | Intakes |
| `CourseMaterialVersion` | Material versions |
| `CourseTrainer` | Course trainers |
| `Dispute` | Disputes |
| `Enrollment` | Enrollments |
| `RoleRequest` | Role requests |
| `User` | Users |

结果值沿用现有 `resultLabel`。

## 测试

后端：

- 五类数量只统计对应的待办状态（每类各放一条待办和一条非待办）。
- 筛选项按实际日志去重并排序。
- 没有 Admin 令牌、或用用户令牌调用两个接口都被拒绝。

前端（Vitest）：

- Provider：挂载取数；30 秒后再取；窗口重新可见时再取；后台时不取（模拟时钟）。
- 合计变多时出现提示，第一次加载和数量不变或变少时不出现；取数失败时保留旧数字。
- 导航角标：数量和无障碍名称正确，为 0 不显示，超过 99 显示 `99+`。
- 首页五张卡片和「All caught up」。
- 审批页页签数量。
- 审计筛选项来自接口，未知类型显示原名。

现有测试保持通过。

## 与其他分支的关系

- 「Course trainers」友好名称对应第 1 块 `feat/course-trainer-invitations` 写入的 `CourseTrainer`。第 1 块合并前日志里不会出现这个类型，不影响。
- `fix/notification-realtime-url` 尚未合并时，`master` 上部分前端测试会因实时连接地址失败；本块验证时临时套用该修复，跑完撤掉。
