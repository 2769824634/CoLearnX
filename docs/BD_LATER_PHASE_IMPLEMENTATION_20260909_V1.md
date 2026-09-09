# CoLearnX B/D Later Phase 本地实现报告

| 项目 | 内容 |
|---|---|
| 文档版本 | `20260909-v1` |
| 本地工作树 | `.worktrees/bd-web-base-integration` |
| 本地分支 | `codex/bd-later-phase-20260908-v1` |
| 基线提交 | `f1a4314`（B/D 与 `web-base` 集成） |
| 远程状态 | 未 commit、未 push、未创建 PR，未修改 GitHub |

## 1. 当前结论

原 `BD_BLOCKERS_AND_AC_DEPENDENCIES_20260908_V0.md` 第 10 节列出的 B/D Later Phase 功能，现已完成本地模块实现、权限边界、数据持久化、前端入口和自动化回归。Admin 仍不参与 CourseIntake 审核；`/api/admin/intakes` 继续不存在，Trainer 创建/提交 Intake、Creator 确认或拒绝 Intake 的既定治理没有改变。

这里的“完成”仅指 B/D Later Phase 模块在当前兼容模型下可运行，不等于整个 A/B/C/D 产品已经完成。A/C 的正式页面、最终 Enrollment 契约、正式 migration 和真实文件存储仍有明确边界，见第 6 节。

## 2. B Later Phase 已实现

| 功能 | 后端 | 前端 | 状态 |
|---|---|---|---|
| Trainer LearningMaterial | 只列出 Admin 已批准的 `CourseMaterialVersion`，按 Intake 绑定并记录使用与审计 | Intake 详情的 Materials & Recordings 面板 | 已实现 |
| Recording | Session 归属校验、HTTP/HTTPS URL 校验、重复 URL 幂等 | 按 Session 查看和添加录播链接 | 已实现 |
| Attendance | 按 Intake + Session 批量保存，校验 Trainer 所有权、Enrollment 归属和重复学习者 | Attendance 页面 | 已实现 |
| Learner List | 汇总报名状态、进度、出勤、考核和证书请求状态 | Learner List 页面 | 已实现 |
| Assessment | Trainer 在本人 Intake 创建考核，校验满分、及格线和 UTC 截止时间 | Learner List 页面中的创建表单 | 已实现 |
| Grading | 按 Assessment + Enrollment 写入或更新成绩与反馈，并写审计 | Learner List 页面中的评分表单 | 已实现 |
| CertificateRequest 初审 | 仅对应 Intake 的 Trainer 可批准或拒绝；终态不能反向改写 | Learner List 页面中的 Trainer review 队列 | 已实现 |

主要入口：

```text
GET  /api/trainer/learning-materials
GET  /api/trainer/intakes/{intakeId}/learning-materials
POST /api/trainer/intakes/{intakeId}/learning-materials
GET  /api/trainer/intakes/{intakeId}/sessions/{sessionId}/recordings
POST /api/trainer/intakes/{intakeId}/sessions/{sessionId}/recordings
PUT  /api/trainer/intakes/{intakeId}/sessions/{sessionId}/attendance
GET  /api/trainer/intakes/{intakeId}/learners
GET  /api/trainer/intakes/{intakeId}/assessments
POST /api/trainer/intakes/{intakeId}/assessments
PUT  /api/trainer/assessments/{assessmentId}/grades/{enrollmentId}
GET  /api/trainer/certificate-requests
POST /api/trainer/certificate-requests/{requestId}/review
```

## 3. D Later Phase 已实现

| 功能 | 后端 | 前端 | 状态 |
|---|---|---|---|
| Admin Credit Ledger | 支持按用户/描述搜索及 Transaction Type 过滤，返回余额变化和关联引用 | Credit Ledger 完整表格与过滤 | 已实现 |
| Manual Credit Adjustment | 非零范围校验、余额不能为负、Transaction + AuditLog、幂等冲突保护 | 调整表单；失败重试复用同一幂等键，成功后才换键 | 已实现 |
| Dispute / Refund | Member 发起；Admin 一次性 Refund/Reject；退款同步 Enrollment、余额、Ledger、Notification 和 AuditLog | Admin Disputes & Refunds 案件页 | 已实现 |
| CertificateRequest 终审 | 只接收 TrainerApproved；批准后生成 UserCertificate，拒绝和签发终态不能反转 | Approval Desk 的 Certificates 队列 | 已实现 |
| CourseMaterialVersion 审批 | PendingApproval → Approved/Rejected；拒绝必须说明原因；终态不能反转 | Approval Desk 的 Material versions 队列 | 已实现 |

主要入口：

```text
GET  /api/admin/credits/ledger
POST /api/admin/credits/adjustments
GET  /api/admin/disputes
POST /api/admin/disputes/{disputeId}/review
GET  /api/admin/certificate-requests
POST /api/admin/certificate-requests/{requestId}/review
GET  /api/admin/material-versions
POST /api/admin/material-versions/{versionId}/review
```

为闭合 B/D 工作流增加的最小跨角色适配接口：

```text
POST /api/materials                  Creator 提交材料版本元数据
POST /api/certificates/requests      Member 提交证书请求
POST /api/disputes                   Member 提交 Enrollment 争议
GET  /api/disputes/my                Member 查询本人争议
```

这些接口只是 B/D 工作流的最小 HTTP 适配，不代表 A 的 Member 页面或 C 的 Creator 材料管理页面已完成。

## 4. 新增数据与一致性约束

新增实体：

- `CourseMaterialVersion`
- `CourseIntakeMaterial`
- `SessionRecording`
- `Assessment`
- `AssessmentResult`
- `CertificateRequest`

扩展实体：

- `CreditTransaction` 增加 `RelatedDisputeId`、`AdminAccountId` 和 `IdempotencyKey`；
- `Dispute` 增加 `RefundCredits`、`ResolutionKey` 和 Enrollment 导航。

关键约束：

- 同一材料的版本号唯一；
- 同一 Intake 不能重复绑定同一材料版本；
- 同一 Session 的录播 URL 唯一；
- 同一 Session + User 只有一条 AttendanceRecord；
- 同一 Assessment + Enrollment 只有一条 AssessmentResult；
- 同一 Enrollment 只有一个 CertificateRequest；
- 信用调整和争议处理使用唯一幂等键；相同键但不同业务参数返回冲突；
- 材料路径只接受 `materials/` 下不含绝对路径、盘符或 `..` 的相对路径；
- 服务层会重新检查当前用户是否仍为 Active Trainer/Creator/Member，不能只依赖旧 JWT 中的角色声明。

## 5. 新鲜验证证据

验证日期：2026-09-09。

```text
dotnet test .\CoLearnX.Server.Tests\CoLearnX.Server.Tests.csproj --no-restore
结果：125 passed，0 failed，0 skipped

npm.cmd run lint
结果：0 errors

node --test tests\api-client.test.js tests\trainer-intake-form.test.js tests\b4-workflow.test.js tests\later-phase-api.test.js
结果：17 passed，0 failed

npm.cmd run build
结果：Vite production build passed，93 modules transformed

git diff --check
结果：无 whitespace error；只有工作树 LF/CRLF 提示
```

后端使用独立临时 SQLite 数据库在 `http://127.0.0.1:5089` 启动，并完成只读 API 烟雾验证：

```text
Trainer login role              = Trainer
Trainer owned Intakes           = 4
Trainer approved materials      = 1
Admin login                     = zhu.zirui@colearnx.com
Admin ledger rows               = 3
Admin disputes                  = 1
Admin material versions         = 2
GET /api/admin/intakes          = 404
```

烟雾测试数据库及 WAL/SHM 临时文件已删除。启动时当前受限 Windows 会话仍报告 Data Protection key 的 DPAPI/写权限警告，但 JWT 登录和上述 API 请求均实际成功；这是本地宿主环境证据，不是源代码全平台运行证明。

Later Phase 集成测试覆盖：

- 被撤销角色的旧 Trainer token 不能读取材料库；
- 被撤销角色的旧 Member token 不能读取本人争议；
- 不安全材料路径被拒绝，已审核材料不能反向改判；
- Trainer 只能操作本人 Intake/Session/Enrollment；
- 材料、录播、出勤、Learner List、考核和评分闭环；
- Member 资格检查 → Trainer 初审 → Admin 终审签发；
- Manual Credit Adjustment 幂等、冲突和审计；
- Dispute Refund 只入账一次，并同步争议、余额、Enrollment、Ledger、Notification 和 AuditLog。

## 6. 仍未完成且不能算作 B/D Later Phase 完成证据的部分

### 6.1 等待 A

- Member 端提交和查看 CertificateRequest 的正式页面；
- Member 端提交和查看 Dispute 的正式页面；
- 最终 `Enrollment.CourseIntakeId` 契约，以及公开 Intake 查询/报名页面；
- 多 Session Intake 下，报名、出勤和证书资格的最终产品语义。

当前证书资格按整个 Intake 的 Session 计算 80% 出勤，但旧 Enrollment 仍绑定单个 CourseSession。单 Session Intake 可完成全流程；多 Session Intake 需要 A/B/C 冻结最终 Enrollment 契约后再做最终集成验收。

### 6.2 等待 C

- Creator 材料上传/版本历史的正式页面；
- 在既有 LearningMaterial 上创建 v2、v3 等新版本的完整 Creator 工作流；
- Creator Usage / Performance 与 royalty 统计；
- Creator Course 创建、编辑、提交页面与现有 Intake review 导航整合。

当前 `POST /api/materials` 只接收经过约束的文件路径和材料元数据，不负责二进制文件上传、对象存储、病毒扫描或下载授权。因此不能把当前材料功能描述为“真实文件上传平台”。

### 6.3 D 的最终数据库工作仍需等待共享模型冻结

- 当前仍使用 `EnsureCreated`，没有生成正式 EF Core migration；
- `appsettings.json` 改用新的 `colearnx-later-v1.db`，用于避免把新模型强行套到旧库；
- A/C 冻结 `Enrollment`、Course、CourseIntake 和材料版本契约后，D 仍需生成唯一正式 migration；
- 还需验证空库 migration Up、启动、种子、查询，以及旧 `colearnx.db` / `colearnx-b1.db` / `colearnx-d1a.db` 的转换方案。

### 6.4 依赖安全门禁

本轮测试输出仍报告已知依赖告警，包括 `.NET` 的 `Microsoft.OpenApi 2.0.0`、`SQLitePCLRaw.lib.e_sqlite3 2.1.11`，以及前端依赖链中的 `brace-expansion`、`browserslist`、`nanoid`、`postcss`、`react-router` / `react-router-dom`。本轮没有擅自升级依赖，因为升级需要单独做兼容回归；这些告警在发布前必须建立独立修复批次。

## 7. 主要代码位置

```text
CoLearnX.Server/Domain/Entities/LaterPhaseEntities.cs
CoLearnX.Server/Domain/Enums/LaterPhaseEnums.cs
CoLearnX.Server/Contracts/Dtos/LaterPhaseDtos.cs
CoLearnX.Server/Data/LaterPhaseModelConfiguration.cs
CoLearnX.Server/Services/MaterialVersionService.cs
CoLearnX.Server/Services/TrainerLaterPhaseService.cs
CoLearnX.Server/Services/CertificateWorkflowService.cs
CoLearnX.Server/Services/AdminFinanceService.cs
CoLearnX.Server/Controllers/TrainerLaterPhaseController.cs
CoLearnX.Server/Controllers/AdminLaterPhaseController.cs
CoLearnX.Server/Controllers/DisputesController.cs
CoLearnX.Server.Tests/LaterPhaseWorkflowIntegrationTests.cs
colearnx.client/src/pages/trainer/TrainerAttendancePage.jsx
colearnx.client/src/pages/trainer/TrainerLearnersPage.jsx
colearnx.client/src/pages/trainer/TrainerResourcesPanel.jsx
colearnx.client/src/pages/admin/AdminCreditLedgerPage.jsx
colearnx.client/src/pages/admin/AdminDisputesPage.jsx
colearnx.client/src/pages/admin/AdminLaterApprovalsPage.jsx
colearnx.client/tests/later-phase-api.test.js
```

## 8. 下一验收顺序

1. A/C 评审并冻结最小适配接口，尤其是 Enrollment 与多 Session Intake 语义；
2. 补齐 A/C 正式页面和 Creator 材料版本历史；
3. D 生成正式 migration，并完成空库和旧库转换验证；
4. 运行 Member → Trainer → Creator/Admin 的真实浏览器跨角色流程；
5. 单独升级存在安全告警的依赖并重跑全部测试；
6. 用户明确要求后，再决定是否 commit、push 或创建 PR。
