# CoLearnX B/D Later Phase 的 A/C 配合要求

| 项目 | 内容 |
|---|---|
| 文档版本 | `20260909-v1` |
| B/D 本地分支 | `codex/bd-later-phase-20260908-v1` |
| B/D 基线提交 | `f1a4314` |
| 适用范围 | B/D Later Phase 与 A/C Member、Enrollment、Creator Course、Material 工作的集成 |
| 规则优先级 | 当前用户指令与项目 `AGENTS.md` 优先；CourseIntake/CourseSession 归 B，其余按英文分工 |

## 1. 结论

B/D 已完成 Trainer 交付工作区、Attendance、Learner List、Assessment、Grading、证书初审、Admin Credit Ledger、Manual Credit Adjustment、Dispute/Refund、证书终审和材料版本审批。A/C 不需要重新实现这些模块，也不能建立第二套平行状态机或审批 API。

A/C 的主要任务是补齐工作流的入口与上游数据：

- A 完成 Member 页面和最终 Enrollment → CourseIntake 契约；
- C 完成 Creator Course 与 LearningMaterial 版本工作流；
- A/C 冻结共享模型后，由 D 生成正式 migration 并做旧库转换；
- A/C 必须保留 Trainer 管理 Intake、Creator 确认/拒绝 Intake、Admin 不审核 Intake 的治理规则。

## 2. 当前 B/D 已提供、A/C 应直接复用的接口

### 2.1 A 可复用的 Member 接口

```text
POST /api/certificates/requests
Body: { "enrollmentId": number }

POST /api/disputes
Body: { "enrollmentId": number, "reason": string }

GET /api/disputes/my
```

这些接口已经执行 Member 当前有效角色检查，不能由 A 仅在前端隐藏按钮代替后端权限控制。

### 2.2 C 可复用的 Creator 接口

```text
POST /api/materials
Body:
{
  "title": string,
  "description": string | null,
  "filePath": "materials/...",
  "format": string,
  "category": string
}
```

这个接口创建 `LearningMaterial` 和第一个 `CourseMaterialVersion`，状态为 `PendingApproval`，然后进入 D 的 Admin 材料审批队列。

Creator 对 CourseIntake 的确认/拒绝必须继续复用：

```text
GET  /api/creator/intake-applications
GET  /api/creator/intake-applications/{courseIntakeId}
POST /api/creator/intake-applications/{courseIntakeId}/review
```

### 2.3 A/C 不应修改的审批边界

```text
Trainer：创建、修改、提交和交付 CourseIntake
Creator：确认或拒绝本人 Course 对应的 CourseIntake
Admin：不审核 CourseIntake
```

`/api/admin/intakes` 必须继续不存在。CourseIntake 的申请状态使用 `PendingApproval`，不能重新命名为 `Submitted`；`CertificateRequestStatus.Submitted` 保持有效。

## 3. A 必须完成的工作

### A-1：冻结最终 Enrollment → CourseIntake 契约

当前 `Enrollment` 仍主要依赖：

```text
Enrollment.CourseId
Enrollment.CourseSessionId
Enrollment.CourseSession.CourseIntakeId
```

A 需要交付明确的最终模型，至少回答并实现以下问题：

1. Enrollment 是否必须新增非空 `CourseIntakeId`；
2. 一个 Intake 有多个 Session 时，Member 是报名整个 Intake，还是报名其中一个 Session；
3. 如果报名整个 Intake，Session roster 使用 `Enrollment` 直接推导，还是增加单独的 Session registration/join 表；
4. Capacity、重复报名、取消、退款和 Completed 状态分别作用于 Intake 还是 Session；
5. 旧 `CourseId + CourseSessionId` 报名数据如何转换。

推荐的最小兼容交付是：

```text
Enrollment.CourseIntakeId：必填外键
Enrollment.CourseSessionId：只有产品明确允许选择单个 Session 时才保留为可空字段
Enrollment.CourseId：短期兼容保留，但服务端必须验证它与 CourseIntake.CourseId 一致
```

A 的完成标准：

- Member 不能把自己报名到未发布、已结束或不属于该 Course 的 Intake；
- 同一 Member 对同一 Intake 的重复报名规则有数据库唯一约束；
- Enrollment、信用扣减和席位占用在同一事务内；
- 取消或退款不会造成席位和 CreditBalance 不一致；
- 提供单 Session 和多 Session Intake 的集成测试；
- 给 B/D 一份冻结后的字段、外键、唯一约束和状态迁移表。

### A-2：适配 B 的 Attendance 和 Learner List

B 当前按 `CourseIntake + CourseSession` 验证 Trainer 所有权，并从 Session 下的 Enrollment 生成 roster。A 修改 Enrollment 后，必须与 B 一起确认：

- `GET /api/trainer/intakes/{intakeId}/learners` 应返回一名 Member 一行，还是每个 Session 一行；
- `PUT /api/trainer/intakes/{intakeId}/sessions/{sessionId}/attendance` 如何确定该 Session 的合法学习者；
- 一个 Member 在同一 Intake 的多 Session Attendance 如何计算；
- `Present`、`Late`、`Absent` 中哪些计入证书所需的 80% 出勤率；
- 退款或取消后历史 Attendance 是否保留、是否还能修改。

A 不应直接删除 B 当前依赖的 `CourseSession` 导航。在迁移期间如需替换，必须同时提交 B/D 服务适配和回归测试，不能只改实体导致 Later Phase 编译或运行失败。

### A-3：完成 Member CertificateRequest 页面和本人查询

已有提交接口：

```text
POST /api/certificates/requests
```

A 需要完成：

- Member 在本人 Completed Enrollment 上发起证书请求的页面；
- 展示不满足 100% progress、80% attendance 或 assessment pass 时的具体原因；
- 增加本人证书请求查询契约，例如 `GET /api/certificates/requests/my`；
- 展示 `Submitted`、`TrainerApproved`、`TrainerRejected`、`AdminRejected` 和 `Issued`；
- Issued 后跳转或关联已有 UserCertificate 页面；
- 禁止 Member 查询或提交其他用户的 Enrollment。

状态机必须保持：

```text
Member Submitted
    → TrainerApproved → Admin Issued
    → TrainerApproved → AdminRejected
    → TrainerRejected
```

A 不能绕过 Trainer 初审直接把请求发送到 Admin 终审。

### A-4：完成 Member Dispute 页面

后端已有：

```text
POST /api/disputes
GET  /api/disputes/my
```

A 需要完成：

- 在本人 Enrollment 详情提供“提交争议”入口；
- 输入并校验最多 1000 字符的 reason；
- 展示 Open、ResolvedRefund、Rejected 状态和 Admin resolution note；
- 已 Cancelled 或 Refunded Enrollment 不允许再次发起；
- 同一 Enrollment 同时只有一个 Open dispute；
- 前端不能自行修改 refundCredits 或直接调用 Admin review 接口。

### A-5：补齐 Member 对材料和录播的消费入口

B 已实现 Trainer 向 Intake 绑定已批准材料，以及向 Session 添加 Recording。若最终产品要求 Member 使用这些资源，A 需要与 B 确认并增加只读 Member 契约：

```text
GET /api/enrollments/{enrollmentId}/materials
GET /api/enrollments/{enrollmentId}/recordings
```

该接口必须先验证当前 Member 拥有对应有效 Enrollment，不能直接复用 Trainer API，也不能向未报名用户公开受限文件路径或录播链接。

### A-6：仍需完成的既有 Phase 1 接口

这些内容不是本次 Later Phase 新增功能，但仍会阻止完整 B/D 验收：

- 普通用户发起 RoleRequest；
- 普通用户查看本人 RoleRequest 状态；
- Trainer/Creator 角色获批后的导航和 active role 切换；
- 从新用户注册开始重复完整 Member → Trainer/Creator 流程。

## 4. C 必须完成的工作

### C-1：完成 Creator Course 创建、编辑和提交

C 需要提供真实 Creator 工作流，不能继续依赖 SeedData 中的 Course：

- Creator 创建 Course；
- 仅编辑本人拥有、允许编辑状态的 Course；
- 提交为 `PendingApproval`；
- Admin approve/reject 后显示结果和理由；
- 通过 `Course.CreatorId` 保证所有权；
- 不能让客户端提交或覆盖 CreatorId、审核人、审核时间等服务端字段。

这项完成后，D 的 Admin Course Review 才能从真实 Creator 页面收到数据，而不是只消费种子或测试夹具。

### C-2：完成 Creator LearningMaterial 管理页面

当前 `POST /api/materials` 可以创建第一个待审批版本。C 需要增加正式页面：

- 创建 LearningMaterial；
- 查看本人材料列表；
- 查看 PendingApproval、Approved、Rejected 和拒绝原因；
- 只允许 Creator 管理本人拥有的材料；
- 不允许编辑已提交版本的文件路径、格式和内容；
- Admin 拒绝后应创建新版本，不能原地覆盖已审核版本。

### C-3：完成 v2、v3 等材料版本历史

建议新增兼容接口：

```text
POST /api/materials/{learningMaterialId}/versions
GET  /api/materials/{learningMaterialId}/versions
```

版本规则：

- `LearningMaterial.CreatorId` 创建后不可更改；
- VersionNumber 由服务端按同一 LearningMaterial 单调递增；
- `(LearningMaterialId, VersionNumber)` 保持唯一；
- 每个版本有独立 FilePath、Format、Status、SubmittedAt、ReviewedAt 和 reviewer；
- 已 Approved/Rejected 的版本不可反向改判；
- Trainer 绑定的是具体 `CourseMaterialVersionId`，不是会被覆盖的“最新材料”；
- 新版本批准后不能自动替换已经绑定到历史 Intake 的旧版本，除非另有明确业务操作和审计记录。

D 当前的 Admin material-version 队列会读取所有 `CourseMaterialVersion`，因此 C 不应创建第二套 MaterialReview 实体或 Admin API。

### C-4：实现真实文件上传和受控访问

当前接口只保存 `materials/...` 相对路径和元数据，不包含真实二进制上传。C 或项目指定的共享基础设施负责人仍需完成：

- 文件上传；
- 文件大小、扩展名和 MIME type 校验；
- 对象存储或受控本地存储；
- 防止路径穿越和可执行文件；
- 病毒/恶意内容扫描策略；
- Creator 预览、Admin 审核、Trainer/Member 下载的授权边界；
- 删除或替换失败时的孤儿文件清理；
- 数据库事务与文件存储不能原子提交时的补偿策略。

在这部分完成前，当前功能只能称为“材料版本元数据审批”，不能称为“真实材料上传系统”。

### C-5：整合 Creator Course 和 Intake Review 导航

C 应在 Creator 工作区复用已有 Intake Application API，并完成：

- 查看本人 Course 下 Trainer 提交的 Intake；
- 查看原版本和变更 proposal；
- Confirm/Reject；
- Reject 必须填写 confirmationNote；
- 禁止 Creator 审核不属于本人 Course 的 Intake；
- 禁止 Trainer 自审；
- 已完成决定保持幂等，不能无审计反向修改。

C 不能把 Intake 确认重新交给 Admin，也不能新增 `/api/admin/intakes`。

### C-6：Usage、Performance 和 Royalty

B 在 Trainer 把材料绑定到 Intake 时已经写入 `MaterialUsageLog`。C 需要完成：

- Creator 查看本人材料的使用次数；
- 按 Course、Intake、Trainer 和时间范围查询；
- royalty 计算规则和入账时点；
- Royalty 类型 `CreditTransaction`；
- 重试幂等、余额一致性和 AuditLog；
- 明确退款后 royalty 是否冲正。

Royalty 规则冻结前，B/D 只保存 usage，不应猜测或自动发放收益。

## 5. A/C 修改时最容易冲突的共享文件

| 文件 | 当前 B/D 使用方式 | A/C 修改要求 |
|---|---|---|
| `CoLearnX.Server/Domain/Entities/Entities.cs` | 包含 Enrollment、CreditTransaction、Dispute 的基础实体和本轮扩展字段 | A 改 Enrollment、C 改 Course/Material 时必须保留 B/D 新字段和导航 |
| `CoLearnX.Server/Data/CoLearnXDbContext.cs` | 注册 Later Phase DbSet 和模型配置 | 不得删除 `LaterPhaseModelConfiguration.Configure(modelBuilder)` |
| `CoLearnX.Server/Controllers/ApiControllers.cs` | 包含最小 Member Certificate 和 Creator Material 适配接口 | A/C 应扩展而不是复制相同 route |
| `CoLearnX.Server/Program.cs` | 注册 B/D Later Phase services 和 Admin/Trainer policies | 不得删除 DI 注册或把 Admin 合并进 AppRole |
| `CoLearnX.Server/Data/SeedData.cs` | 创建可运行的 Later Phase 演示数据 | A/C 新 seed 必须幂等，不能绕过业务资格伪造终态 |
| `colearnx.client/src/api/index.js` | 导出共享 API client | A/C 合并时保留现有 admin/trainer exports |
| `colearnx.client/src/routes/AppRouter.jsx` | 已接入 Trainer Attendance/Learners 和 Admin Ledger/Disputes | A/C 添加页面时不能覆盖 B/D 路由树 |
| `colearnx.client/src/layouts/RoleShell.jsx` | 已开放 B/D Later Phase 菜单 | A/C 继续使用统一 token、导航和设计变量 |

建议 A/C 优先新增自己的文件，只在必要时最小修改上述共享文件。合并前必须以当前 B/D 工作树为基线做三方 diff，不能用旧分支文件整文件覆盖。

## 6. A/C 需要向 B/D 提交的冻结清单

### A 的交付清单

```text
[ ] Enrollment 最终字段与关系图
[ ] Intake/Session 报名语义
[ ] 唯一约束、席位和退款规则
[ ] Member CertificateRequest 页面和本人查询 API
[ ] Member Dispute 页面
[ ] 多 Session Attendance/Certificate 集成测试夹具
[ ] 旧 Enrollment 数据转换规则
```

### C 的交付清单

```text
[ ] Creator Course 创建、编辑、PendingApproval 提交页面/API
[ ] Creator LearningMaterial 列表与创建页面
[ ] CourseMaterialVersion v2+ 创建和版本历史 API
[ ] 真实文件上传与访问授权方案
[ ] Creator Intake Review 导航整合
[ ] Usage/Performance/Royalty 规则及测试
[ ] 旧 LearningMaterial 数据转换规则
```

## 7. A/C 完成后 D 才能执行的数据库工作

A/C 冻结前，D 不应生成多套相互冲突的 migration。冻结后 D 需要：

1. 对比最终 `User`、`RoleRequest`、`Course`、`LearningMaterial`、`CourseIntake`、`CourseSession`、`Enrollment` 和 Later Phase 实体；
2. 生成唯一正式 EF Core migration；
3. 验证空库 Up、启动和 SeedData；
4. 设计旧 `colearnx.db`、`colearnx-b1.db`、`colearnx-d1a.db` 和 `colearnx-bd.db` 的升级路径；
5. 验证唯一约束、外键、Check Constraint 和幂等键；
6. 跑全量后端、前端和真实浏览器跨角色流程。

本次 ZIP 中的 `colearnx-later-v1.db` 是通过 `EnsureCreated` 建立的本地演示数据库，不是正式 migration，也不能作为旧生产数据库升级包。

## 8. 最终联合验收流程

### 流程一：Course 与 Intake

```text
Creator 创建并提交 Course
→ Admin 审核发布 Course
→ Trainer 创建 CourseIntake/CourseSession
→ Trainer 提交 PendingApproval
→ Creator Confirm/Reject
→ Admin 始终不参与 Intake review
```

### 流程二：材料版本

```text
Creator 上传 LearningMaterial v1/v2
→ Admin 审核具体 CourseMaterialVersion
→ Trainer 只绑定 Approved Version
→ Member 只通过有效 Enrollment 读取材料
→ MaterialUsageLog 可由 Creator 查询
```

### 流程三：学习与证书

```text
Member 报名 Intake
→ Trainer 记录 Session Attendance
→ Trainer 创建 Assessment 并评分
→ Member 达到 progress/attendance/pass 条件并提交 CertificateRequest
→ Trainer 初审
→ Admin 终审并签发 UserCertificate
```

### 流程四：争议退款

```text
Member 对本人 Enrollment 提交 Dispute
→ Admin Refund/Reject
→ Refund 同步 Dispute、Enrollment、CreditBalance、CreditTransaction、Notification、AuditLog
→ 同一幂等键重试不重复退款
```

只有以上四条流程都能从真实 A/C 页面发起，并通过 B/D 当前 API 和权限检查，才能宣布 A/B/C/D Later Phase 全角色集成完成。

## 9. 禁止作为完成证据的替代项

- SeedData 中有 Course，不代表 C 的 Course 创建页面完成；
- 数据库中有 Pending Material，不代表 C 的材料上传和版本历史完成；
- `POST /api/materials` 保存路径成功，不代表二进制文件上传完成；
- Trainer 页面能看到 learner，不代表 A 的最终 Intake Enrollment 完成；
- B/D 自动化测试通过，不代表 A/C 页面或多 Session 语义已经验收；
- `EnsureCreated` 生成数据库，不代表正式 migration 和旧库升级完成；
- 菜单或路由存在，不代表真实浏览器跨角色流程通过。
