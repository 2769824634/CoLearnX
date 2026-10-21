# 课程授课邀请与可选 Trainer 名单（第一块）

分支：`feat/course-trainer-invitations`，从最新的 `master` 切出。

## 背景

期次（Intake）的创建、课节管理、发布和取消将从 Trainer 移交给 Creator，分三块完成：

1. **本块**：授课邀请与每门课的可选 Trainer 名单。
2. `feat/creator-intake-management`：期次管理移交给 Creator，Creator 从名单里指定 Trainer，去掉开班审批与变更审批。
3. `feat/intake-date-inputs`：报名按「开始日期 + 几点 + 天数」，授课只选日期，课节按「日期 + 开始时间 + 时长」。

本块只建立「哪些 Trainer 可以教哪门课」的关系，**不改变现有的期次流程**，可以单独上线。

## 范围

做：

- Creator 在课程页向 Trainer 发出这门课的授课邀请，查看名单，撤回邀请，移出 Trainer，重新邀请。
- Trainer 在新页面查看邀请，接受或拒绝，查看自己已加入的课程。
- 双方收到对应通知；Trainer 收到邀请时实时提醒并发邮件。
- 已经开过某门课期次的 Trainer，自动加入这门课的名单，状态为「已接受」。
- 名单的每次变动写审计记录；Admin 审课时能看到这门课的授课老师（见「Admin 可见性」）。

不做：

- 期次相关的任何改动（第二块）。
- Trainer 主动退出某门课的名单。
- 一次邀请多门课、按 Creator 整体邀请。
- 邀请过期。

## 状态

每一对「课程 + Trainer」只有一条记录，状态如下：

| 状态 | 含义 | Creator 可做 | Trainer 可做 |
|---|---|---|---|
| `Invited` | 已邀请，等待回复 | 撤回（变为 `Removed`） | 接受、拒绝 |
| `Accepted` | 在可选名单里 | 移出（变为 `Removed`） | — |
| `Declined` | Trainer 拒绝 | 重新邀请（变为 `Invited`） | — |
| `Removed` | 被撤回或移出 | 重新邀请（变为 `Invited`） | — |

规则：

- 只能邀请当前账号有效、拥有 Trainer 身份的用户。
- 课程的任何状态（草稿、待审核、已发布、被退回）都可以邀请。
- Creator 只能管理自己课程的名单；Trainer 只能回复发给自己的邀请。
- Creator 本人如果也是 Trainer，可以邀请自己；邀请自己时直接记为 `Accepted`，不发通知。
- 移出 Trainer 不影响他正在教的期次；第二块据此只禁止把他指定到新期次。
- 重复操作要安全：对 `Invited` 或 `Accepted` 再次邀请返回冲突；对已经是目标状态的记录再次接受、拒绝、撤回、移出，返回当前状态，不报错也不重复发通知。

## 数据

新增实体 `CourseTrainer`（`Domain/Entities/Entities.cs`）和枚举 `CourseTrainerStatus { Invited = 0, Accepted = 1, Declined = 2, Removed = 3 }`（`Domain/Enums/DomainEnums.cs`）。

| 字段 | 类型 | 规则 |
|---|---|---|
| `Id` | int | 主键 |
| `CourseId` | int | 外键 `Courses`，禁止级联删除 |
| `TrainerId` | int | 外键 `Users`，禁止级联删除 |
| `Status` | int | `CourseTrainerStatus` |
| `InvitedByUserId` | int | 最近一次邀请人 |
| `InvitedAt` | datetime | 最近一次邀请时间 |
| `RespondedAt` | datetime，可空 | 最近一次接受或拒绝时间 |
| `RemovedAt` | datetime，可空 | 最近一次撤回或移出时间 |

索引：`(CourseId, TrainerId)` 唯一；`TrainerId` 普通索引。

`CoLearnXDbContext` 新增 `DbSet<CourseTrainer> CourseTrainers`。

### 数据库升级

新建 `Data/CourseTrainerSchema.cs`，照 `PricingSchema.cs` 的写法：SQLite 和 SQL Server 各一段，表不存在才建，索引用「不存在才建」。在 `SeedData.InitializeAsync` 里和其他 Schema 一起调用。

### 导入历史授课关系

`CourseTrainerSchema` 建表后执行一次插入：对 `CourseIntakes` 里每一对不同的 `(CourseId, TrainerId)`，如果 `CourseTrainers` 里还没有这一对，就插入一条 `Accepted` 记录，`InvitedByUserId` 取课程的 `CreatorId`，`InvitedAt` 和 `RespondedAt` 取当前时间。

- 每次启动都执行，只补缺失的组合，不改已有记录；因此被移出的 Trainer 不会被重新加回。
- 第二块上线前，Trainer 仍会自己开期次，这条插入让名单随之保持一致。
- 不发通知。

演示数据：`SeedData` 里已有的演示期次会通过同一条插入进入名单，无需单独写种子。

## 接口

错误统一返回现有的 `ApiError { code, message, fieldErrors }`。

### Creator（`[Authorize(Policy = CreatorAuthorization.PolicyName)]`）

| 方法 | 路径 | 说明 |
|---|---|---|
| `GET` | `api/creator/courses/{courseId}/trainers` | 这门课的名单，含全部状态，按状态（已接受、已邀请、已拒绝、已移出）再按姓名排序 |
| `GET` | `api/creator/trainers?search=` | 可邀请的 Trainer：账号有效且有 Trainer 身份；按姓名或显示名模糊搜索，按姓名排序，最多 20 条；`search` 为空时返回排序后的前 20 条 |
| `POST` | `api/creator/courses/{courseId}/trainers` | 请求体 `{ trainerId }`；新建或重新邀请 |
| `POST` | `api/creator/courses/{courseId}/trainers/{trainerId}/remove` | 撤回邀请或移出名单 |

名单项 `CourseTrainerDto`：`trainerId, fullName, headline, status, invitedAt, respondedAt, removedAt`。

可邀请 Trainer 项 `TrainerCandidateDto`：`id, fullName, headline, specialisations`。**不返回邮箱**，避免 Creator 批量获取用户联系方式。

### Trainer（`[Authorize(Policy = TrainerAuthorization.PolicyName)]`）

| 方法 | 路径 | 说明 |
|---|---|---|
| `GET` | `api/trainer/course-invitations` | 发给自己的全部记录，`Invited` 在前，再按时间倒序 |
| `POST` | `api/trainer/course-invitations/{id}/accept` | 接受 |
| `POST` | `api/trainer/course-invitations/{id}/decline` | 拒绝 |

邀请项 `CourseInvitationDto`：`id, courseId, courseCode, courseTitle, creatorName, status, invitedAt, respondedAt, removedAt`。

### 错误码

| 代码 | 状态码 | 场景 |
|---|---|---|
| `COURSE_NOT_FOUND` | 404 | 课程不存在或不属于当前 Creator |
| `TRAINER_NOT_FOUND` | 404 | 被邀请的用户不存在、已停用或没有 Trainer 身份 |
| `TRAINER_ALREADY_INVITED` | 409 | 对 `Invited` 或 `Accepted` 再次邀请 |
| `COURSE_TRAINER_NOT_FOUND` | 404 | 名单里没有这位 Trainer |
| `INVITATION_NOT_FOUND` | 404 | 邀请不存在或不是发给当前 Trainer |
| `INVITATION_NOT_PENDING` | 409 | 回复一条已不是 `Invited` 的邀请，且目标状态和当前状态不同 |

业务异常复用 `CourseException`。新增过滤器 `CourseTrainerApiErrorsAttribute`，Creator 和 Trainer 两个新控制器共用：把 `CourseException` 转成 `ApiError`；两次邀请同时写入触发唯一索引冲突时返回 `TRAINER_ALREADY_INVITED`（现有的 `CreatorCourseApiErrors` 会把唯一冲突误报为课程代码重复，所以不复用）。

## 通知

| 代码 | 收件人 | 时机 | 跳转 | 实时推送 | 邮件 |
|---|---|---|---|---|---|
| `N-course-invitation` | Trainer | 收到邀请（含重新邀请） | `/trainer/invitations` | 是 | 是 |
| `N-course-invitation-accepted` | Creator | Trainer 接受 | `/creator/courses` | 是 | 否 |
| `N-course-invitation-declined` | Creator | Trainer 拒绝 | `/creator/courses` | 是 | 否 |
| `N-course-trainer-removed` | Trainer | 被撤回或移出 | `/trainer/invitations` | 否 | 否 |

- 正文写明课程代码和标题，例如「Alex Chen accepted your invitation to teach DES-101 Inclusive Design.」
- `NotificationTargetPath.For` 增加上表的映射；`NotificationRealtime` 的重要通知集合加入前三个代码。
- 前端 `memberNotificationsState.js` 的安全跳转白名单加入 `/trainer/invitations`（`/creator/courses` 已在白名单之外，一并加入）；`importantNotificationTypes` 加入前三个代码，使其弹出提示。

## Admin 可见性

### 审计记录

名单状态每变一次，`CourseTrainerService` 在同一次保存里写一条 `AuditLog`：

| 操作 | 操作人（`UserId`） | `Result` |
|---|---|---|
| `CourseTrainerInvited` | Creator | `Invited`；Creator 指定自己时为 `Accepted` |
| `CourseTrainerRemoved` | Creator（含撤回未回复的邀请） | `Removed` |
| `CourseInvitationAccepted` | Trainer | `Accepted` |
| `CourseInvitationDeclined` | Trainer | `Declined` |

- `EntityType` 为 `CourseTrainer`，`EntityId` 为「课程编号/Trainer 编号」，例如 `12/305`。第一次邀请时名单记录在保存前还没有编号，用这对编号才能让记录和改动在同一次保存里写入；每对「课程 + Trainer」只有一条名单记录，两者一一对应。
- 没有实际变化的操作不写：重复移出已移出或已拒绝的人、重复同样的回复。
- 启动时导入以前的授课关系不写记录，因为不是某个人的操作。
- Admin 审计日志的资源筛选暂不加 `CourseTrainer`，选「All resources」可见；筛选项由后续的 `feat/admin-pending-alerts` 统一补齐。

### 审课弹窗显示授课老师

- `AdminCourseDto` 增加 `Trainers`，元素为 `AdminCourseTrainerDto(TrainerId, FullName, Status)`。只含 `Accepted` 和 `Invited`，先列已接受，再按姓名排序。
- `AdminCourseReviewsPage` 的审课弹窗在 Creator 下面加「Trainers」区块，每人一行「姓名 · 状态」；没有时显示「No trainers on this course yet.」。
- 只读，Admin 不能在这里改名单。

## 前端

### Creator：课程页的「Trainers」卡片

新组件 `pages/creator/CourseTrainersCard.jsx`，显示在 `CreatorCourseFormPage` 已有课程的页面上（新建课程时不显示），放在表单下方。

- 名单表格：姓名、简介、状态标签（Available / Invited / Declined / Removed）、时间。
- 每行操作：`Invited` 显示「Withdraw」；`Accepted` 显示「Remove」，点击后二次确认；`Declined` 和 `Removed` 显示「Invite again」。
- 卡片右上角「Invite trainer」按钮，打开弹窗（复用 `components/Modal.jsx`）：
  - 输入姓名搜索，防抖 300 毫秒，至少 2 个字符才搜索。
  - 结果列表显示姓名、简介、专长；已在名单里且为 `Invited` 或 `Accepted` 的人显示状态，不显示邀请按钮。
  - 点「Invite」后弹窗内显示成功，名单刷新。
- 加载中、出错重试、名单为空（「No trainers yet. Invite one to teach this course.」）都有对应显示；接口错误用 `userFacingError` 显示。

新 API 封装 `api/courseTrainers.js`：`list`、`searchTrainers`、`invite`、`remove`、`myInvitations`、`accept`、`decline`。

### Trainer：「Teaching invitations」页面

新页面 `pages/trainer/TrainerInvitationsPage.jsx`，路由 `/trainer/invitations`，导航在「Course」后面加一项「Teaching invitations」。

- **Waiting for you**：`Invited` 的邀请，显示课程代码、标题、Creator、邀请时间，按钮「Accept」「Decline」。
- **My courses**：`Accepted` 的课程。
- **History**：`Declined` 和 `Removed`，只读。
- 每块为空时有一句说明；操作中按钮禁用，失败时在该行显示错误。

## 测试

后端（`CoLearnX.Server.Tests`，新文件 `CourseTrainerInvitationTests.cs`）：

- 邀请、接受后出现在 Creator 名单和 Trainer 的 My courses。
- 拒绝后可以重新邀请；移出后可以重新邀请。
- 对 `Invited`、`Accepted` 再次邀请返回 `TRAINER_ALREADY_INVITED`。
- 邀请非 Trainer、已停用用户返回 `TRAINER_NOT_FOUND`。
- 别的 Creator 的课程返回 `COURSE_NOT_FOUND`；回复别人的邀请返回 `INVITATION_NOT_FOUND`。
- 重复接受、重复移出返回当前状态，不重复发通知。
- Creator 邀请自己直接为 `Accepted`，不发通知。
- 每个通知代码在对应操作后生成，跳转路径正确。
- 候选 Trainer 接口不返回邮箱。
- 邀请、接受、拒绝、移出各写一条审计记录，操作人和结果正确；重复操作不写；Creator 指定自己记为 `Accepted`；启动导入不写。
- Admin 课程列表带上授课老师，只含已接受和已邀请（`AdminCourseReviewIntegrationTests.cs`）。
- 升级：已有期次的 Trainer 自动进入名单；再次执行不重复插入；被移出的不会被加回。SQLite 升级后的表结构与新建数据库一致（参照 `PricingSchemaUpgradeTests.cs`）；测试环境没有 SQL Server，SQL Server 建表语句与现有 Schema 文件同样只做代码审查。

前端（Vitest）：

- `CourseTrainersCard.test.jsx`：名单各状态的按钮、搜索防抖与最少字符、邀请后刷新、移出二次确认、错误显示。
- `TrainerInvitationsPage.test.jsx`：三个分组、接受和拒绝后分组变化、失败时行内错误。
- `memberNotificationsState` 新路径通过白名单。
- `AdminCourseReviewsPage.test.jsx`：审课弹窗列出授课老师和状态；没有时显示提示。

现有测试保持通过。

## 与其他分支的关系

- `feat/course-materials-upload`（尚未推送）也改了 `CreatorCourseFormPage.jsx`。本块只在该页面加一行渲染卡片，合并时冲突很小。
- 第二块会读取 `CourseTrainers` 中 `Accepted` 的记录，作为指定 Trainer 的可选范围。

## 工作方式

按 `docs/ENGINEERING.md`：

1. 从最新 `master` 切出 `feat/course-trainer-invitations`。
2. 先写失败的测试，再写最少的实现。
3. 一个分支一个主题；提交说明用一行英文写清原因。
4. 前后端测试都跑完、读过输出，才算完成。
5. 列出分支、提交和影响范围，经确认后再推送或合并。
