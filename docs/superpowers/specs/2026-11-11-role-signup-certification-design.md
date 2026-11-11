# 注册时选择角色，并用认证状态拦住 Trainer 与 Creator

日期：2026-11-11。实现时只做本文。不要删除 Staff，不要把课表放进邀请，不要改令牌存储。

## 已定规则

注册页提供三个入口：Sign up as Member、Sign up as Trainer、Sign up as Creator。注册成功时立刻写入所选的那一个 `UserRole`，不附带其他角色。

Member 保持现在的能力，不上传认证资料。`CertifiedAt` 在注册时写成当前时间。

Trainer 和 Creator 也立刻拥有对应角色，但 `CertifiedAt` 为空。登录后只能打开认证页。管理员批准现有的角色申请后写入 `CertifiedAt`，用户才能使用该角色已有的功能。拒绝时 `CertifiedAt` 保持为空，申请人可以重新提交。

Trainer 注册时必须选择一个领域，保存到 `TrainerProfile.SubjectArea`。领域使用已有的 `SubjectArea`：`DigitalTechnology`、`BusinessManagement`、`HumanitiesHealthCreative`。认证期间只读，不能改。Creator 按课程科目所属的领域筛选已认证 Trainer。

服务器是真正的拦截点。页面上的认证墙只负责把未认证用户留在认证页。

## 数据

`UserRole` 增加可空的 `CertifiedAt`（UTC）。

- 新 Member：注册时写入。
- 新 Trainer、新 Creator：保持 `null`，直到申请被批准。
- 已有的 Trainer、Creator：回填为该行的 `GrantedAt`，避免现有账号被锁在认证页外。
- 已有的 Member：回填为 `GrantedAt`。Member 接口不读这个字段。

`TrainerProfile` 增加可空的 `SubjectArea`。新 Trainer 注册时必填。已有 Trainer 不猜测领域，保持 `null`；`null` 的 Trainer 不出现在邀请名单里。演示种子里的 Trainer 设为 `DigitalTechnology`，与现有 “UI/UX Design, Cybersecurity awareness” 一致。

表结构沿用 `PlatformStaffSchema` 的做法：SQLite 与 SQL Server 都在列不存在时追加，并在 `SeedData.InitializeAsync` 里调用。不要新开一套迁移框架。

`UserMeDto` 增加：

- `DateTime? CertifiedAt`：当前 `activeRole` 对应的 `UserRole.CertifiedAt`。
- `string? SubjectArea`：Trainer 的领域；其他角色为 `null`。

## 注册

`RegisterRequest` 增加必填的 `Role`，以及 Trainer 才使用的 `SubjectArea`。

| 条件 | 结果 |
|---|---|
| `Role` 不是 Member、Trainer、Creator | 400 `ROLE_NOT_ALLOWED` |
| Trainer 未提交合法的 `SubjectArea` | 400 `SUBJECT_REQUIRED` |
| Member 或 Creator 提交了 `SubjectArea` | 400 `SUBJECT_NOT_ALLOWED` |

`AuthService.RegisterAsync`：

- 只添加所选角色这一条 `UserRole`。
- Member 的 `CertifiedAt` 为当前时间。Trainer 和 Creator 的 `CertifiedAt` 为 `null`。
- Trainer 同时创建 `TrainerProfile`，写入 `SubjectArea`。
- 邮箱验证、密码规则、不向内部邮箱注册，保持现状。
- 返回的 `activeRole` 是所选角色。

注册页三个按钮各自提交对应的 `Role`。选中 Trainer 后才显示领域下拉，三个选项的显示名是 Digital Technology、Business and Management、Humanities, Health and Creative。去掉 “New accounts start as Member”。

## 认证申请

资料仍是简历、身份证件和不超过 300 字的说明。接口仍是 `POST /api/role-requests`。该控制器保持只要求已登录，不套 Trainer 或 Creator 策略，否则未认证用户无法上传。

`RoleRequestService.SubmitAsync` 修改「已经拥有角色」的判断：

- 该角色的 `CertifiedAt` 已有值：仍返回 409 `ROLE_ALREADY_GRANTED`。
- 角色已存在但 `CertifiedAt` 为空：允许提交，这就是认证申请。
- 同一角色已有 Pending 申请：仍返回 409 `ROLE_REQUEST_PENDING`。
- 申请的角色必须等于该用户已有、且尚未认证的 Trainer 或 Creator。不能用这份申请改成另一个角色。不符合时返回 400 `ROLE_NOT_REQUESTABLE`。

`AdminRoleRequestService.ReviewAsync`：

- 批准：若角色不存在则补写；无论是否已存在，都把该 `UserRole.CertifiedAt` 设为当前时间。不重复插入角色。
- 拒绝：不修改 `CertifiedAt`。现有的拒绝原因和通知保留。

## 功能拦截

在 `TrainerIntakeAccess` 和 `CreatorIntakeAccess` 两条策略上增加同一个要求：当前 `active_role` 对应的 `UserRole.CertifiedAt` 不为空。从数据库读取，不信任令牌里的旧声明。

未认证时返回 403，错误码 `CERTIFICATION_REQUIRED`，消息说明必须先完成身份认证。不要复用 `TRAINER_REQUIRED` 或 `CREATOR_REQUIRED`，前端靠这个错误码留在认证页。`TrainerAuthorizationResultHandler` 需要识别这个失败原因。

因此这些现有控制器在未认证时全部不可用：Creator 课程、期次、邀请名单，以及 Trainer 的期次、邀请回复和后续授课接口。

以下接口继续对未认证用户开放：登录、邮箱验证、`GET /api/auth/me`、登出、`GET/POST /api/role-requests`。

`PUT /api/users/{id}` 若请求体试图修改 `SubjectArea`，返回 400 `SUBJECT_LOCKED`。本阶段不提供修改领域的接口。

## 前端

登录后，若 `activeRole` 是 Trainer 或 Creator 且 `certifiedAt` 为空：

- Trainer 只能停留在 `/trainer/certification`。
- Creator 只能停留在 `/creator/certification`。
- 工作台其他路由重定向到对应认证页。
- 认证页展示只读领域（仅 Trainer）、申请状态、拒绝原因，以及简历、身份证件和说明的上传表单。
- 已有 Pending 申请时不显示再次提交。Rejected 或不存在申请时可以提交。
- 提交成功后停留在该页并显示等待审核。
- 批准后，用户刷新 `me`；`certifiedAt` 有值才进入 `/trainer/home` 或 `/creator/home`。认证页提供进入工作台的按钮，点击时重新请求 `me`。

Member 的兴趣引导、首页和账号页不因本次改动改变。Trainer 和 Creator 不进入 Member 引导。

## 邀请名单

`GET /api/creator/trainers` 增加必填的 `courseId`。只返回同时满足以下条件的用户：

- 账号有效。
- 拥有 Trainer 角色且该行 `CertifiedAt` 不为空。
- `TrainerProfile.SubjectArea` 等于这门课 `CourseSubject.Subject`。
- 课程属于当前 Creator。课程不存在或不属于该 Creator 时返回 404 `COURSE_NOT_FOUND`。
- 姓名搜索规则不变：空搜索返回按姓名排序的前 20 条；有搜索词时在上述范围内按全名和显示名匹配。

`InviteAsync` 再次检查同一条件。未认证返回 404 `TRAINER_NOT_FOUND`。领域不一致返回 409 `TRAINER_SUBJECT_MISMATCH`。不要只靠前端过滤。

邀请对话框改为把当前 `courseId` 传给搜索接口。结果中显示领域名称。未认证或领域不符的 Trainer 不会出现。

## 测试

- Member 注册只得到 Member，且 `CertifiedAt` 有值，不要求 `SubjectArea`。
- Trainer 注册得到 Trainer 和领域；缺领域或非法领域被拒绝；`CertifiedAt` 为空。
- Creator 注册得到 Creator，提交领域被拒绝；`CertifiedAt` 为空。
- 未认证的 Trainer 或 Creator 调用各自的功能接口得到 403 `CERTIFICATION_REQUIRED`，但仍能提交角色申请。
- 已有角色且未认证时可以提交申请；已认证后再次申请得到 409。
- 管理员批准后 `CertifiedAt` 有值，且不会插入第二条角色。拒绝后 `CertifiedAt` 仍为空。
- 邀请搜索和邀请动作只接受同领域且已认证的 Trainer。
- 已有 Trainer、Creator 在回填后仍可使用原功能。

## 不做

- 不删除 Staff，不改平台课。
- 不把邀请从课程名单改到期次，不在邀请里附带课表。
- 不把令牌从 `localStorage` 改到 Cookie。
- 不增加登录失败锁定、退出时更换 `SessionStamp`、切换角色时重新输入密码。
- 不让一个新注册账号同时拥有 Member 和 Trainer 或 Creator。已有的「已有角色后再申请另一个角色」流程本次不改，除非它因为 `ROLE_ALREADY_GRANTED` 的新判断而失败；那种情况仍按「已认证才拒绝」处理。
