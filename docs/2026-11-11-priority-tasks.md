# CoLearnX 最后阶段任务与分工

日期：2026-11-11，截止约 2026-11-18。4 人，一人一个板块。工作量按一个熟悉代码的人粗估。

相关文档：

- 角色与专业标签：`docs/2026-11-11-role-specialty-design.md`
- 学习路径与删除 Category：`docs/2026-11-11-learning-path-design.md`
- 管理员整合与 Member 账单：`docs/2026-11-11-admin-overview-member-billing-design.md`
- 新用户体验评审：<https://claude.ai/artifact/Fjh9dinQx8Vun8YDFyc3gu>

## 已定规则

| 规则 | 决定 |
|---|---|
| 账号与角色 | 一个账号只担任一个角色（Member / Trainer / Creator）；清空现有的多身份账号 |
| 认证 | Trainer、Creator 注册后必须认证才能进入工作台；认证时选 1 到 5 个专业标签 |
| 专业标签 | 直接使用现有的 12 个课程科目（`CourseSubject`） |
| Creator 授课 | **不授课**，只能邀请 Trainer；本人想上课需另外注册一个 Trainer 账号 |
| Trainer 加入课程 | 两种方式：被 Creator 邀请后接受；主动申请后由 Creator 同意。只限自己的专业标签 |
| Staff | 上级已驳回，删除 |
| Category | 不影响流程，删除 |
| 学习路径 | 按专业自动生成，推荐时同标签、更高等级的课程优先；**不拦截报名** |
| 完成一门课 | 以拿到该课程的课程证书为准 |

## 分工

| 板块 | 负责人 | 任务（按顺序） | 粗估 |
|---|---|---|---|
| 1. 角色与认证 | A | 1. **第 1 天上午提交 P0-1**（其他人在这之后拉取）<br>2. 清空多身份账号（数据脚本）<br>3. Trainer 主动申请教课，Creator 同意<br>4. 去掉 Creator 授课<br>5. 小修<br>6. 有余力：支援 C，或做显式权限层 | 2.5 天 + 机动 |
| 2. 清理与账单 | B | 1. 删除 Category<br>2. 清理线上测试课和误导文案（含首页的分类文案）<br>3. 删除 Staff（等 A 提交后开始）<br>4. Member 账单易读化 | 3.5 天 |
| 3. 学习路径 | C | 1. 路径按专业生成，阶段由等级决定<br>2. 推荐排序与先修提示<br>3. 自动颁发阶段证书和 Graduation 证书<br>4. 页面：我的学习路径、详情页提示、目录筛选、建课表单去掉 Learning path | 4.5 天 |
| 4. 管理员视图 | D | 1. 班级总览页<br>2. 用户详情按角色分标签<br>3. 管理员导航精简（含删除 Staff 入口） | 4 天 |
| 收尾 | 全员 | 第 5 天：联调、回归测试、重置演示数据、部署 | 1 天 |

**避免冲突**

- A 提交 P0-1 之前，其他人只做不碰 `Program.cs`、`SeedData.cs`、`AppRouter.jsx` 的部分：B 先删 Category，C 先做数据设计，D 先写班级总览的查询接口。
- `RoleShell.jsx` 里的管理员导航只由 D 改；B 删除 Staff 时不动导航。
- C 直接使用现有的 `CourseSubject` 和 P0-1 新增的 `UserSpecialties`，不依赖 A 后续的工作。
- 每人在自己的分支上开发，每天下班前合并到 master 一次。

**本周不做**：显式权限层（A 有余力时可做）、课程级先修和管理员排除开关、导出 CSV、下文 P2 的各项。

## 板块 1：角色与认证（A）

### 1.1 单角色注册 + 认证 + 专业标签（P0-1）：本地已完成，未提交

- [x] 注册时只选 Member / Trainer / Creator 中的一个；不合法的角色返回 `ROLE_NOT_ALLOWED`
- [x] `UserRole.CertifiedAt`：Member 注册时写入；Trainer/Creator 为空，等批准后写入；已有角色在加列时回填
- [x] Trainer/Creator 策略增加认证检查，未认证返回 403 `CERTIFICATION_REQUIRED`；角色申请接口保持对未认证用户开放
- [x] 认证页：选 1 到 5 个专业标签，并上传简历、证件和说明；Pending 状态下隐藏表单，被拒后可以重新提交
- [x] 新表 `UserSpecialties`：管理员批准时写入；审核页显示申请的标签
- [x] 邀请名单和邀请只接受"已认证、并持有该课程科目标签"的 Trainer（`TRAINER_SUBJECT_MISMATCH` / `TRAINER_NOT_FOUND`）
- [x] 前端认证墙：未认证的 Trainer/Creator 只能停留在 `/trainer/certification` 或 `/creator/certification`
- [x] `RoleApplicationsPanel`：未认证的角色不再显示为 Active
- [x] 测试：后端 489 个全部通过；前端 332 个 + node 22 个全部通过。lint 的 9 个错误是原本就有的，没有新增
- [ ] **提交**：master 上同时有组长原来未提交的改动，两者改到了相同的文件。建议新建分支一起提交，再合并到 master

### 1.2 清空多身份账号

账户清单 45 个账号中有 11 个持有多个角色。每个账号只保留一个角色；收回的角色名下如果有课程或班级，要先转给别人。

| 账号 | 现有角色 | 保留（待 A 确认） |
|---|---|---|
| gu.yincheng@colearnx.com | Member + Trainer + Creator | Trainer（演示 Trainer） |
| zou.ruiqi@colearnx.com | Member + Trainer + Creator | Creator（演示 Creator） |
| youshengh1@colearnx.xyz | Member + Trainer + Creator | 待定 |
| huang.yousheng@colearnx.com | Member + Creator | Member（演示 Member） |
| zouruiqi5@gmail.com | Member + Creator | Creator |
| 2769824634@qq.com、18267711719@163.com | Member + Trainer | Trainer |
| qa.trainer@colearnx.test、qa.trainer2/3/4@colearnx.test | Member + Trainer | Trainer |

同时修改种子数据：演示 Trainer、Creator 不再附带 Member 角色。

### 1.3 Trainer 主动申请教课

- `CourseTrainerStatus` 增加 `Requested`。
- Trainer 可以申请自己标签内的已发布课程；Creator 在课程的讲师名单里同意或拒绝，双方都会收到通知。
- 主要改动：`CourseTrainerService.cs`、`CourseTrainersController.cs`、`CourseTrainersCard.jsx`、`TrainerInvitationsPage.jsx`。

### 1.4 去掉 Creator 授课：已完成（本地，未提交）

Creator 邀请自己返回 400 `CREATOR_CANNOT_TEACH`，候选名单也不再列出自己。


- 删除 `CourseTrainerService.InviteAsync` 里"邀请自己即自动接受"的 `self` 分支，改为不允许邀请自己。
- 删除对应测试：`A_creator_who_is_also_a_trainer_joins_their_own_roster_directly`、`A_creator_adding_themselves_is_audited_as_accepted`。
- 开班时本来就只能选已加入的 Trainer，这部分不用改。

### 1.5 小修

- Creator 只能在自己的专业标签内建课：后端校验 `CourseSubjectId`。
- 更新 `docs/superpowers/specs/2026-11-11-role-signup-certification-design.md`：单一领域改为专业标签；删除"不删除 Staff"。

### 1.6 有余力时：显式权限层

- 新增权限常量和"角色 → 权限"字典，只写在代码里，不进数据库；现有的 5 条策略改为按权限生成。
- 24 处只要求登录的 `[Authorize]` 逐个核对，换成对应的权限策略。

## 板块 2：清理与账单（B）

### 2.1 删除 Category

删除课程和资料的类别字段、`CourseService` 的筛选参数、`courseCategories.js` 和资料类别下拉框；数据库列保留不读。`Interest.Category` 不删。详见学习路径文档。

### 2.2 线上内容与文案

- 下架约 30 门测试/演示课，例如 `Demo:`、`DEMO-*`、`Live test`、`20261111_1_ForSignUp(Testing)`。
- 修改简介里写着 "Waiting for admin approval" 的已发布课程。
- 首页的 Subjects 改为 3 个领域 / 12 个科目，与专业标签一致；统一学习阶段数。
- 首页的 "Programs open" 只统计有班可报的课；公开页面加上 credit 换算说明。
- 课程卡片以标题为主，统一显示 "Trainer: X · Creator: Y"；注册页增加"返回首页"链接。

### 2.3 删除 Staff

只删功能层，保留数据层（`StaffAccounts` 表、`AuditLogs.StaffAccountId`、`StaffAccount` 实体、`PlatformStaffSchema`）。

- 后端：`StaffAuthorization`、`StaffTokenService`、`StaffAuthController`、`StaffAuthService`、`StaffCoursesController`、`StaffDeliveryResourcesController`、`StaffCourseService`、`AdminStaffController`、`AdminStaffService`、`CourseIntakeService` 里的 `*ForStaff*` 方法、`Program.cs` 里的 Staff 认证方案、种子里的 `EnsureDemoStaffAsync`。
- 前端：`pages/staff/`、`RequireStaff`、`StaffAuthContext`、`StaffAuthProvider`、`useStaffAuth`、`StaffDetails`、`AdminStaffPage`、`staff-v10.css`、`/staff/*` 路由。管理员导航由 D 处理。
- 测试：`PlatformStaffIntegrationTests`、`PlatformStaffSqlServerTests`、`StaffDeliveryRouteTests`、前端 Staff 测试，以及其他测试里的 Staff 用例。
- Staff 创建的隐藏 Trainer 用户改为停用；已发布的平台课保留展示，删除 `/member/platform` 页面。

### 2.4 Member 账单易读化

在 Wallet 页的 ledger 标签上改：类型换成易懂文案（充值、报名冻结、课程扣费、冻结退回、退款、违约扣除、管理员调整）；按类型和时间筛选；顶部汇总；与课程相关的记录可以点击。详见管理员整合文档第四节。

## 板块 3：学习路径（C）

详见 `docs/2026-11-11-learning-path-design.md`。

- 路径 = 专业（12 条），阶段由课程等级决定：Beginner 对应 Foundation，Intermediate 对应 Intermediate，Advanced 对应 Advanced。
- 推荐：同标签、更高等级的课程优先；同一档内沿用现有推荐分数。未满足先修只做提示，不拦截报名。
- 证书：完成一个阶段要求的课程数，自动颁发阶段证书；三个阶段都完成，颁发 Graduation 证书。
- 页面：我的学习路径、课程详情页的先修提示、目录按路径和阶段筛选、建课表单去掉 Learning path 下拉框。

## 板块 4：管理员视图（D）

详见 `docs/2026-11-11-admin-overview-member-billing-design.md`。

- 班级总览页：课程、Creator、讲师、起止时间、剩余天数、学员 / 名额、出勤率、状态，可以按 Trainer、Creator、状态筛选。
- 用户详情按角色分标签：Trainer 显示正在教的班；Creator 显示创建的课程；Member 显示学习情况和流水。
- 管理员导航从 9 个入口精简到 6 个，并删除 Staff 入口。

## P2：本周不做（备忘）

- 合并公开目录 `/courses` 和 `/member/courses`。
- 删除 Creator 导航里的 "Create Course" 和 "Approval status" 单独页面。
- Trainer 的 Attendance / Learners 页面没有任何入口：要么接到期次详情里，要么删除。
- 期次在价格和日期合规时自动发布。
- 收藏的课程开班时发送通知。
- 把 "Badges" 页面改名为 "Certificates & badges"。

## 账户清单说明

账户清单里的密码没有写进本文件，也没有在线上使用。
