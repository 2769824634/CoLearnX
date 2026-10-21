# 课程授课邀请 实施计划

设计：`docs/superpowers/specs/2026-10-21-course-trainer-invitations-design.md`

**目标：** Creator 按课程邀请 Trainer，Trainer 接受后进入该课程的可选名单。

**做法：** 新表 `CourseTrainers`（一对课程 + Trainer 一条记录），一个服务 `CourseTrainerService` 承担全部规则，两个薄控制器分别给 Creator 和 Trainer；前端一张课程卡片和一个 Trainer 页面。

**技术：** ASP.NET Core .NET 10 + EF Core（SQLite / SQL Server）、xUnit；React 19 + Vitest。

## 全局约束

- 分支 `feat/course-trainer-invitations`，从 `master` 18ed189 切出；只在本地提交，推送前确认。
- 代码、界面文案、提交说明用英文；`PowerShell` 用 `;` 连接命令。
- 后端测试：`dotnet test CoLearnX.Server.Tests`；前端测试：`cd colearnx.client; npm test`。
- `master` 上已有 notificationRealtime 相关的前端测试失败（修复在 `fix/notification-realtime-url`），不属于本分支；本分支只要求新增和改动的测试通过、失败数不增加。

## 任务 1：数据表与历史导入

**文件：**
- 改：`CoLearnX.Server/Domain/Enums/DomainEnums.cs`（`CourseTrainerStatus`）
- 改：`CoLearnX.Server/Domain/Entities/Entities.cs`（`CourseTrainer`）
- 改：`CoLearnX.Server/Data/CoLearnXDbContext.cs`（`DbSet` + 索引 + 外键）
- 建：`CoLearnX.Server/Data/CourseTrainerSchema.cs`（`EnsureAsync` 建表，`BackfillAsync` 导入）
- 改：`CoLearnX.Server/Data/SeedData.cs`（启动时建表；种子数据之后导入）
- 测：`CoLearnX.Server.Tests/CourseTrainerSchemaTests.cs`

**接口：**
- `CourseTrainerStatus { Invited = 0, Accepted = 1, Declined = 2, Removed = 3 }`
- `CourseTrainer { Id, CourseId, TrainerId, Status, InvitedByUserId, InvitedAt, RespondedAt?, RemovedAt?, Course, Trainer }`
- `CourseTrainerSchema.EnsureAsync(db, ct)`、`CourseTrainerSchema.BackfillAsync(db, ct)`

步骤：
- [ ] 写测试：旧库升级后表结构、索引、外键与 `EnsureCreated` 一致；导入为每对 (课程, Trainer) 建一条 `Accepted`，再次执行不重复，`Removed` 不被改回。
- [ ] 运行，确认失败（类型不存在）。
- [ ] 实现实体、配置、Schema、SeedData 调用。
- [ ] 运行通过；跑 `SeedDataTests` 等现有测试确认未受影响。
- [ ] 提交：`Add the course trainer roster table so Creators can track which Trainers may teach each course`

## 任务 2：邀请服务与接口

**文件：**
- 建：`CoLearnX.Server/Contracts/Dtos/CourseTrainerDtos.cs`
- 建：`CoLearnX.Server/Services/CourseTrainerService.cs`
- 建：`CoLearnX.Server/Controllers/CourseTrainersController.cs`（Creator 与 Trainer 两个控制器）
- 建：`CoLearnX.Server/Controllers/CourseTrainerApiErrorsAttribute.cs`
- 改：`CoLearnX.Server/Services/NotificationTargetPath.cs`、`NotificationRealtime.cs`、`Program.cs`
- 测：`CoLearnX.Server.Tests/CourseTrainerInvitationTests.cs`、`NotificationTargetPathTests.cs`

**接口：**
- `CourseTrainerDto(int TrainerId, string FullName, string? Headline, string Status, DateTime InvitedAt, DateTime? RespondedAt, DateTime? RemovedAt)`
- `TrainerCandidateDto(int Id, string FullName, string? Headline, string? Specialisations)`
- `CourseInvitationDto(int Id, int CourseId, string CourseCode, string CourseTitle, string CreatorName, string Status, DateTime InvitedAt, DateTime? RespondedAt, DateTime? RemovedAt)`
- `InviteTrainerRequest(int TrainerId)`
- `ICourseTrainerService`：`ListAsync(creatorId, courseId)`、`SearchCandidatesAsync(search)`、`InviteAsync(creatorId, courseId, trainerId)`、`RemoveAsync(creatorId, courseId, trainerId)`、`ListInvitationsAsync(trainerId)`、`RespondAsync(trainerId, invitationId, accept)`

步骤：
- [ ] 写集成测试（设计文档「测试 / 后端」的每一条）。
- [ ] 运行，确认 404 / 编译失败。
- [ ] 实现 DTO、服务、控制器、过滤器、通知映射与实时推送、注册服务。
- [ ] 运行新测试和全部后端测试通过。
- [ ] 提交：`Let Creators invite Trainers per course and Trainers answer so each course has a roster of willing Trainers`

## 任务 3：前端 API 与通知跳转

**文件：**
- 建：`colearnx.client/src/api/courseTrainers.js`
- 改：`colearnx.client/src/components/memberNotificationsState.js`
- 测：`colearnx.client/src/components/MemberNotifications.test.jsx`

步骤：
- [ ] 测试：`/trainer/invitations`、`/creator/courses` 通过白名单；邀请通知属于重要通知。
- [ ] 实现并通过。

## 任务 4：Creator 课程页「Trainers」卡片

**文件：**
- 建：`colearnx.client/src/pages/creator/CourseTrainersCard.jsx`
- 改：`colearnx.client/src/pages/creator/CreatorCourseFormPage.jsx`（已有课程时渲染卡片）
- 测：`colearnx.client/src/pages/creator/CourseTrainersCard.test.jsx`

步骤：
- [ ] 测试：各状态的按钮；少于 2 个字符不搜索；邀请后刷新；移出需确认；错误显示。
- [ ] 实现并通过。

## 任务 5：Trainer「Teaching invitations」页面

**文件：**
- 建：`colearnx.client/src/pages/trainer/TrainerInvitationsPage.jsx`
- 改：`colearnx.client/src/routes/AppRouter.jsx`、`colearnx.client/src/layouts/RoleShell.jsx`
- 测：`colearnx.client/src/pages/trainer/TrainerInvitationsPage.test.jsx`

步骤：
- [ ] 测试：三个分组；接受、拒绝后分组变化；失败时行内错误；导航高亮。
- [ ] 实现并通过；跑全部前端测试和 `npx eslint` 检查新文件。
- [ ] 提交：`Give Creators a roster card and Trainers an invitations page so invitations can be sent and answered in the app`
