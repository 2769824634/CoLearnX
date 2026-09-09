# CoLearnX B/D 当前困难与 A/C 待交付清单

| 项目 | 内容 |
|---|---|
| 文档版本 | `20260908-v0` |
| 对应分支 | `codex/bd-web-base-integration-20260908-v0` |
| 基线 | `origin/web-base`，commit `728a417bc847479f0a714a2a93007b444ea7548a` |
| 当前 B/D 集成提交 | `f1a4314` |
| 文档目的 | 说明 B/D 当前已完成边界、实际困难、等待 A/C 交付的内容，以及 BD-1 最终验收顺序 |

> 本文是 `20260908-v0` 的 Phase 1 历史快照。第 10 节列出的 Later Phase 已在本地 `codex/bd-later-phase-20260908-v1` 中继续实现；当前结果和剩余边界以 `BD_LATER_PHASE_IMPLEMENTATION_20260909_V1.md` 为准。

## 1. 结论摘要

B 的 Phase 1 功能批次 B1–B4，以及 D 的业务功能批次 D1–D4，已经在当前集成分支中实现，并完成后端测试、前端测试、Lint、构建和本地 HTTP 权限验证。

当前尚不能宣布整个 Phase 1 完成，主要原因不是 B1–B4 或 D1–D4 仍缺核心代码，而是以下跨模块事项尚未闭合：

1. A 尚未交付普通用户侧的 RoleRequest 创建流程；D 的 RoleRequest 审核目前只能消费种子数据或测试夹具。
2. C 尚未交付 Creator 的 Course 创建、编辑和提交流程；`POST /api/courses` 当前仍返回 `501 Not Implemented`。
3. A 的 Enrollment 仍是旧的 Course/Session 兼容模型，尚未转换为最终的 CourseIntake 报名模型。
4. A/C 最终模型未全部冻结，因此 D 负责的正式 EF Core migration、旧数据转换和最终数据库验收尚未执行。
5. BD-1 规定的全角色端到端流程尚不能从新用户注册开始完整重复。
6. GitHub 远程写权限仍阻止当前本地集成提交发布到 `2769824634/CoLearnX`。

## 2. 当前生效的职责边界

本项目按 0902 英文分工执行，并应用团队已经确认的唯一例外：

- `CourseIntake` / `CourseSession` 的实体、DTO、状态机、校验和核心服务归 B；
- B 同时负责 Trainer Intake、Session 和日常交付功能；
- A 负责 User、UserRole、用户侧 RoleRequest、Enrollment、Credits、Payment 和 Member 页面；
- C 负责 Course、Creator Course 页面、公开 Course/Intake 查询，以及 Creator 业务契约；
- D 负责独立 Admin、Role/Course 审核、AuditLog、共享集成和最终 migration。

不可改变的业务规则：

- Trainer 创建、提交和管理本人负责的 CourseIntake；
- Course 对应的 Creator 确认或驳回 Intake；
- Admin 不审核 CourseIntake；
- `/api/admin/intakes` 必须不存在；
- `AppRole` 只包含 Member、Trainer、Creator，Admin 使用独立 `AdminAccount`；
- 唯一结构为 `Course -> CourseIntake -> CourseSession`，不得再创建第二套 Intake/Batch/Cohort 模型。

## 3. B/D 当前完成情况

### 3.1 B 已完成

- CourseIntake / CourseSession 实体与 EF Core 配置；
- Intake 状态、UTC 时间、Session 范围和交付位置校验；
- Trainer 本人 Intake 列表、详情、创建、修改和提交；
- CourseSession 添加、修改和删除；
- 服务端分配 TrainerId，拒绝客户端伪造所有者；
- Draft / Rejected 编辑，PendingApproval 编辑锁定；
- Creator Intake Application 列表、详情、Confirm 和 Reject；
- Reject reason、重复审核幂等和相反决定冲突；
- Published / InProgress 的 Meeting Link 日常交付修改；
- Published / InProgress 重大结构修改的 Change Request 和 Creator 重确认；
- Intake Version 并发控制；
- Enrollment / Attendance 历史存在时的 Session 结构保护；
- Trainer、Creator 权限隔离、自审阻止和 AuditLog；
- Trainer/Creator 真实 API 和前端页面。

### 3.2 D 已完成

- 独立 AdminAccount；
- 独立 Admin 登录、JWT subject、token storage 和授权 Policy；
- 活动 AdminAccount 检查，禁用账号后已发 token 失效；
- 普通 User token 与 Admin token 双向隔离；
- RoleRequest 列表、Approve、Reject、reason、幂等和审计；
- Course 列表、Approve、Reject、reason、幂等和审计；
- AuditLog actor XOR 数据库约束；
- AuditLog 查询、筛选和基于 ID 的游标分页；
- Admin 首页、审批队列、审计页面、账号页和独立登录入口；
- 明确移除 Admin Intake 审核入口。

### 3.3 当前验证证据

| 验证 | 结果 |
|---|---|
| 后端测试 | 118/118 通过 |
| 前端 Node 测试 | 14/14 通过 |
| ESLint | 0 error |
| Vite production build | 通过 |
| Trainer 登录及 Intake API | 通过 |
| Creator 登录及 Intake Application API | 通过 |
| Admin 独立登录及 AuditLog API | 通过 |
| 跨角色越权请求 | 正确返回 401/403 |
| `/api/admin/intakes` | 404，不存在该路由 |

这些证据只能证明当前 B/D 模块和已有兼容路径正常，不能替代从 RoleRequest、Course 创建到 Intake Enrollment 的完整 Phase 1 验收。

## 4. 当前遇到的困难

| 编号 | 优先级 | 困难 | 当前影响 | 当前处理状态 |
|---|---|---|---|---|
| BLK-01 | P0 | C 的 Creator Course 创建/提交未实现 | D 的 Course Review 和 B 的 Intake 创建只能使用种子或测试 Course，无法从 Creator 页面开始真实流程 | 等待 C |
| BLK-02 | P0 | A 的用户侧 RoleRequest 未实现 | D 可以审核，但普通用户无法产生真实 Trainer/Creator 申请 | 等待 A |
| BLK-03 | P0 | A 的 Enrollment 未改为 CourseIntake 模型 | Member 不能按最终契约报名 Intake，BD-1 无法闭环 | 等待 A |
| BLK-04 | P0 | 最终共享数据库契约尚未冻结 | D 现在生成 migration 会在 A/C 模型变化后重复或冲突 | 等待 A/C 后由 D 执行 |
| BLK-05 | P0 | BD-1 全角色验收未完成 | 不能宣布 Phase 1 完成 | 等待 A/C 后联合执行 |
| DEL-01 | P1 | GitHub 远程写权限不足 | 本地分支和 commit 已完成，但无法推送目标仓库 | 等待仓库权限 |
| RISK-01 | P1 | 现有 .NET/JavaScript 依赖存在高危告警 | 不影响当前测试通过，但不能视为生产安全门禁通过 | 建议独立升级批次 |
| ENV-01 | P2 | Windows EventLog、NuGet/JavaScript SDK 权限和 Vite 文件锁 | 会让测试或启动表现为环境失败，容易被误判为源码错误 | 已找到规避方法，非当前产品缺陷 |

## 5. 等待 Developer A 完成的部分

### 5.1 A-1：用户侧 RoleRequest

需要提供的 API：

```text
POST /api/role-requests
GET  /api/role-requests/my
```

最小功能要求：

- 当前登录 User 可以申请 Trainer 或 Creator；
- 请求体不能申请 Admin；
- 同一 User、同一目标角色不能存在重复 Pending 请求；
- 已经拥有目标角色时不能再次申请；
- 保存审核所需的申请资料；
- 用户可以查看本人申请及状态；
- API 返回团队统一的 `code / message / fieldErrors` 错误格式；
- 创建后 D 的 `GET /api/admin/role-requests` 可以直接查询，无需 D 再复制数据；
- D 审核通过后，A 的 `/api/auth/available-roles` 和重新登录结果能看到新角色；
- 刷新后申请状态和角色仍然存在。

A 的验收条件：

- 新注册 Member 可以提交 Trainer/Creator RoleRequest；
- 重复 Pending 请求被拒绝；
- Admin 审批后 UserRole 正确增加；
- Reject 后不增加 UserRole；
- 申请人不能读取其他用户的申请；
- D 的 Admin 页面能够看到通过真实用户 API 创建的申请。

### 5.2 A-2：Enrollment 转为 CourseIntake 报名

当前兼容实现仍使用：

```text
Enrollment.CourseId
Enrollment.CourseSessionId
```

最终模型至少需要：

```text
Enrollment.CourseIntakeId
```

需要 A 与 B/C/D 一起确认：

- Enrollment 是否保留 CourseId 快照，还是只通过 CourseIntake 访问 Course；
- 在线参加与线下参加的请求 DTO；
- CourseSessionId 是否只在选择具体 Session/线下席位时使用；
- OfflineSessionRegistration 是否独立建模；
- 每个 User 对每个 CourseIntake 的 Active Enrollment 唯一约束；
- 旧 Enrollment.CourseId/CourseSessionId 如何映射到新的 CourseIntakeId。

Enrollment 必须实现的规则：

- 只有 Published CourseIntake 可报名；
- 当前时间必须位于 RegistrationOpensAt 和 RegistrationClosesAt 之间；
- 同一用户不能重复报名同一个 Intake；
- 同一 Course 的不同 Intake 可以分别报名；
- 在线报名不受 PhysicalCapacity 限制；
- 只有线下席位预订才增加 SeatsTaken；
- 线下报名必须检查 PhysicalBookingDeadline 和剩余席位；
- Credit 扣减、Enrollment、CreditTransaction 和席位更新在同一事务；
- 任一环节失败时余额和席位均不得改变；
- 并发争抢最后一个线下席位时只能有一个成功；
- MemberProgramsPage 刷新后仍显示正确 Intake、Session、Credits 和 Enrollment 状态。

A 的验收条件：

- 余额不足、重复报名、关闭窗口和满席均不扣款；
- 在线 Intake 不因 PhysicalCapacity 为 0 被错误判断为满席；
- 同一 Course 的第二个 Intake 可以独立报名；
- 报名成功后 B 的 Intake/Session 历史保护可以查询到真实 Enrollment；
- D 的 migration 可以建立最终外键和唯一索引。

### 5.3 A-3：Member Intake 页面和 API 契约

需要完成：

- Member 课程详情中展示 Published CourseIntake；
- MemberIntakeDetailPage；
- 选择 Intake，而不是只选择抽象 Course；
- 必要时选择在线/线下参加方式和 Session；
- `enrollmentsApi` 使用最终 CourseIntake 请求 DTO；
- MemberProgramsPage 展示 Intake 级别报名记录。

## 6. 等待 Developer C 完成的部分

### 6.1 C-1：Creator Course 创建、编辑和提交

需要提供的 API：

```text
GET  /api/creator/courses
GET  /api/creator/courses/{courseId}
POST /api/creator/courses
PUT  /api/creator/courses/{courseId}
POST /api/creator/courses/{courseId}/submit
```

当前通用 `POST /api/courses` 仍返回：

```text
501 Not Implemented
Course creation wizard coming next.
```

最小功能要求：

- Course.CreatorId 由当前 Creator UserId 在服务器端赋值；
- Creator 只能查询和修改本人拥有的 Course；
- Course.Code 唯一；
- Title、Category、CreditCost 以及最终确认的 Level/LearningPath 字段合法；
- CreditCost 是正整数；
- Draft / Rejected 可以编辑和重新提交；
- Submit 使用专门命令转为 PendingApproval，通用 Update DTO 不能直接改状态；
- Admin D 的 Course Review 直接消费同一个 Course，不复制审核模型；
- Admin Approve 后 Course 变为 Published，B 才能创建 Intake；
- Reject reason 可以返回 Creator 页面并支持修改后重新提交。

C 的验收条件：

- Creator 不能创建归属于其他 Creator 的 Course；
- 非 Creator 不能访问写接口；
- 重复 Course.Code 被拒绝；
- 非 Draft/Rejected Course 不能普通编辑；
- Admin 可以看到真实提交的 PendingApproval Course；
- Admin 审核发布后 Trainer 可以基于该 Course 创建 Intake。

### 6.2 C-2：公开 Course / Intake 查询

需要提供并冻结：

```text
GET /api/courses
GET /api/courses/{courseId}
GET /api/courses/{courseId}/intakes
GET /api/intakes/{courseIntakeId}
```

要求：

- 未登录和 Member 只能看到允许公开的 Published 数据；
- Course DTO 与 Intake DTO 不混为同一层；
- Course 可以存在多个 Intake；
- Intake 详情包含其 CourseSession，但 Session 不再直接作为 Course 的唯一开课实例；
- 查询契约必须能被 A 的 Member 页面和 Enrollment 使用；
- 不返回 PasswordHash、内部审核字段或不需要公开的审计数据。

### 6.3 C-3：接受当前 Creator Intake Review 契约

当前 B/D 集成分支为了闭合 B4，已经实现：

```text
GET  /api/creator/intake-applications
GET  /api/creator/intake-applications/{courseIntakeId}
POST /api/creator/intake-applications/{courseIntakeId}/review
```

C 不应再创建第二套 Creator Intake Review。需要做的是：

- 审查并接受当前 `Course.CreatorId` 所有权判定；
- 复用现有 CourseIntakeApplication 和 Intake Version；
- 将 Creator Course 页面与现有 Intake Application 页面合并导航；
- 保持 Confirm/Reject、confirmationNote、幂等和自审禁止契约；
- 保持 Admin 无 Intake Review 权限。

### 6.4 C 的 Later Phase，不阻塞当前 BD-1

以下内容不应混入当前 Phase 1 阻塞修复：

- CourseMaterial / CourseMaterialVersion；
- LearningMaterialVersion 的 Creator Review；
- CourseIntakeMaterial；
- Creator Usage 和 Performance；
- 完整材料版本历史和 royalty 统计。

## 7. A/C 交付后 D 必须完成的工作

### 7.1 冻结最终数据库契约

D 需要组织确认：

- User、UserRole、RoleRequest；
- Course.CreatorId 和 Course 状态；
- CourseIntake、CourseSession 和 CourseIntakeApplication；
- Enrollment.CourseIntakeId；
- 在线/线下报名与席位字段；
- CreditTransaction 和 PaymentTransaction；
- AdminAccount 和 AuditLog；
- 主键、外键、唯一约束、并发 token、DeleteBehavior 和枚举值。

### 7.2 创建唯一正式 EF Core migration

完成条件：

- 只有 D 或明确指定的集成人生成 migration；
- 不继续依赖 `EnsureCreated` 作为正式升级机制；
- migration Up 能在空数据库完整执行；
- 应用能在 migration 后启动；
- Trainer/Creator/Admin/Member 核心查询正常；
- SQLite foreign keys 生效；
- 不修改已经被共享环境使用过的历史 migration。

### 7.3 设计旧数据库转换

必须明确：

- 旧 Course.TrainerId 如何转换或保留，以及 CreatorId 从哪里获得；
- 旧 Session 如何创建或映射 CourseIntake；
- 旧 Enrollment 如何映射 CourseIntakeId；
- 无法唯一推断的数据如何报告，不得静默猜测；
- User、余额、CreditTransaction、PaymentTransaction 不被重复写入；
- 原始 `colearnx.db` 先备份，不直接覆盖；
- 转换失败时的恢复方案。

## 8. BD-1 最终联合验收流程

必须在新的空数据库上重复以下流程，不能用预置 Published Course 或预置 RoleRequest 替代：

1. 注册 Alice、Bob、Carol，三人初始只有 Member；
2. Bob 通过 A 的 API 申请 Trainer；
3. Carol 通过 A 的 API 申请 Creator；
4. Zhu Zirui 通过独立 Admin 登录；
5. Admin 审核并批准两个真实 RoleRequest；
6. Bob、Carol 重新登录后分别获得 Trainer、Creator；
7. Carol 通过 C 的页面创建并提交 Course；
8. Admin 审核并发布该 Course；
9. Bob 通过 B 的页面创建 CourseIntake 和至少一个 CourseSession；
10. Bob 提交 Intake，状态变为 PendingApproval；
11. Carol 确认属于自己 Course 的 Intake，状态变为 Published；
12. Admin 页面不存在 Intake 审核入口；
13. Bob 更新本人 Session 的交付链接；
14. Alice 通过 A/C 的公开查询查看 Published Intake；
15. Alice 购买或拥有足够 Credits，并按 CourseIntake 报名；
16. Enrollment、CreditTransaction 和余额在同一事务中更新；
17. 刷新应用后，Alice 仍能看到正确 Intake、Session、Enrollment 和余额；
18. Bob 能看到真实学员历史，且不能危险删除有关联历史的 Session；
19. AuditLog 能查到 Admin、Trainer 和 Creator 的关键状态变化；
20. 重复执行关键回调、审核和提交不会产生重复角色、重复扣款或重复状态变化。

BD-1 通过后，才能宣布 Phase 1 完成并生成后续版本，例如 `202609XX-v1`。

## 9. 不得用作完成证据的替代方案

- SeedData 中存在 Published Course，不代表 Creator Course 创建流程完成；
- SeedData 中存在 Pending RoleRequest，不代表用户侧申请流程完成；
- SeedData 中存在 Published Intake，不代表 Creator 确认链路被真实执行；
- `EnsureCreated` 能生成新库，不代表 migration 完成；
- B/D 模块测试全绿，不代表 A/C 跨模块流程完成；
- Member 通过 CourseSession 兼容接口报名，不代表最终 CourseIntake Enrollment 完成；
- Admin 能查询 Course，不代表 Creator 能创建和提交 Course；
- 页面菜单存在，不代表对应服务、API 和持久化已完成。

## 10. Later Phase 未完成项

以下功能仍未实现，但不属于当前 Phase 1 的 A/C 阻塞项。

### B Later Phase

- Trainer LearningMaterial；
- Recording；
- Attendance；
- Learner List；
- Assessment；
- Grading；
- Trainer 对 CertificateRequest 的初审。

### D Later Phase

- Admin Credit Ledger 完整前端；
- Manual Credit Adjustment；
- Dispute / Refund；
- Admin 对 CertificateRequest 的终审；
- CourseMaterialVersion 的 Admin 审批。

这些项目应在 BD-1 和 Phase 1 验收后单独排期，不应为了填充当前菜单而提前扩展数据库和权限范围。

## 11. 推荐执行顺序

| 顺序 | Owner | 交付 |
|---|---|---|
| 1 | A | RoleRequest 创建/本人查询 API 和 Member 页面 |
| 2 | C | Creator Course 创建、编辑、提交 API 和页面 |
| 3 | C + D | 真实 Course 提交到 Admin Review 并发布 |
| 4 | A + B + C | Enrollment.CourseIntakeId 和公开 Intake 查询契约 |
| 5 | D | 冻结共享实体、外键、枚举和唯一约束 |
| 6 | D | 生成唯一正式 migration，并验证空库 Up/启动/查询 |
| 7 | A + B + C + D | 执行 BD-1 全角色流程和回归测试 |
| 8 | D | 设计和验证旧数据库转换 |
| 9 | 仓库 Owner | 授予写权限并推送版本分支 |

## 12. 当前交付与权限状态

本地集成分支和 commit 已存在：

```text
branch: codex/bd-web-base-integration-20260908-v0
commit: f1a4314 Integrate B and D workflows with web-base
```

远程推送当前失败：

```text
Permission to 2769824634/CoLearnX.git denied to GuYincheng.
HTTP 403
```

仓库 Owner 需要给 `GuYincheng` Write 权限，或者在本机切换到有写权限的 GitHub 身份。不要在聊天中发送 GitHub 密码或 Personal Access Token。

## 13. 参考依据

- 原始工作区 `docs/0902_CoLearnX_Team_Code_Allocation_and_Development_Rules_EN.md`
- 原始工作区 `docs/0902_CoLearnX_Final_Design_and_Class_Diagram_EN.md`
- 原始工作区 `docs/V3_0902_CoLearnX_代码分工与开发规则.md`
- 原始工作区 `docs/V3_0902_CoLearnX_最终设计与类图.md`
- 当前分支 B/D 源码、测试结果和 HTTP 权限验证结果
