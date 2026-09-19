# CoLearnX Member 新增功能本地说明 README

| 项目 | 内容 |
| --- | --- |
| 文档版本 | `v5.0` |
| 文档日期 | `2026-09-19` |
| 文档性质 | 本地新增功能说明与验证记录 |
| 工作目录 | `C:\Users\user\Documents\CoLearnX\.local-deploy\CoLearnX-master-20260918` |
| 变更状态 | 本地已提交、待手动上传、未部署 |
| 数据库 | `CoLearnX.Server\App_Data\colearnx-member-next-local.db` |
| 后端地址 | `http://localhost:5088` |
| 前端地址 | `https://localhost:55128` |

这是项目根目录的 v5.0 README，用于替换原有 v3.1 说明。本文说明本次对话中完成的 Member 功能、现有注册行为、忘记密码的真实 Gmail 验证、运行方式、测试证据和当前边界。

## 1. 本次新增和完善的功能

### 1.1 Member 徽章与证书页面

入口：`/member/badges`

页面展示当前登录用户已经签发的证书，包括：

- 证书阶段和阶段编号；
- 课程编号和课程标题；
- 签发时间；
- 唯一验证编号；
- 加载、空状态、错误和重试状态。

代码入口：

- 后端：`CoLearnX.Server\Services\AppServices.cs`
- 工作流：`CoLearnX.Server\Services\CertificateWorkflowService.cs`
- 前端：`colearnx.client\src\pages\member\MemberBadgesPage.jsx`

当前仓库没有独立的 Badge 计算规则，因此页面不会虚构另一套“已获得徽章”规则。现有证书阶段被作为可验证的 Member 成果展示。

### 1.2 证书申请

Member 可以在证书页面查看服务端计算出的申请资格和不满足原因，并提交证书申请。

当前资格规则沿用已有业务规则：

- Enrollment 完成度为 100%；
- 出勤率达到 80%，其中 `Present` 和 `Late` 计入；
- 所有相关 Assessment 均已通过。

申请按钮具有单次提交保护，已有申请会显示当前状态，重复请求不会创建重复申请。

新增接口：

```http
GET  /api/certificates/eligibility
GET  /api/certificates/requests/my
POST /api/certificates/requests
```

证书审核仍然保持两级边界：

```text
Member     提交证书申请
Trainer    审核本人 Intake 下的申请
Admin      对 TrainerApproved 申请执行最终签发或拒绝
```

Trainer 初审和 Admin 终审不能跳过，拒绝后的终态不能重新提交。并发和重复操作会被服务端事务、状态检查和唯一约束保护。

### 1.3 本人证书审批进度

Member 可以在同一个证书页面查看自己的申请状态、审核备注以及审核时间。

支持的状态包括：

```text
Submitted
TrainerApproved
TrainerRejected
AdminRejected
Issued
```

Member 只能读取自己的申请，不能读取其他用户的审批记录。Trainer 和 Admin 的审核备注会在对应审核完成后显示给申请人。

### 1.4 站内通知

Member Shell 右上角增加统一通知入口和未读数量。通知记录持久化在现有 `Notifications` 表中，支持：

```http
GET /api/notifications/my
PUT /api/notifications/{id}/read
PUT /api/notifications/read-all
```

通知覆盖以下证书流程事件：

- Member 提交申请；
- Trainer 通过或拒绝；
- Admin 最终签发或拒绝。

通知链接只允许跳转到受控的站内业务路径，不能通过通知数据构造任意外部跳转。读取操作按当前用户隔离，重复标记已读是幂等的。

相关代码：

- `CoLearnX.Server\Controllers\NotificationsController.cs`
- `CoLearnX.Server\Services\NotificationService.cs`
- `colearnx.client\src\components\MemberNotifications.jsx`
- `colearnx.client\src\components\MemberNotificationsProvider.jsx`
- `colearnx.client\src\components\memberNotificationsState.js`

### 1.5 忘记密码与真实 Gmail 验证

登录页增加 **Forgot password?**，对应页面和接口如下：

```http
POST /api/auth/forgot-password
POST /api/auth/reset-password
```

实现特点：

- 仅 Member、Trainer、Creator 等普通 User 身份参与；独立 Admin 身份不通过普通忘记密码入口处理；
- 请求忘记密码时返回统一消息，避免泄露邮箱是否注册；
- 重置令牌使用随机 256 bit 值生成；数据库只保存 SHA-256 哈希；
- 令牌默认 30 分钟有效，只能使用一次；
- 每个普通账号只保留一个当前令牌；
- 帐号冷却时间默认 60 秒，IP 请求限制为 10 分钟内 8 次；
- 重置密码时同时更新密码和 `SessionStamp`，旧 JWT 会失效；
- 新密码复用统一的 `PasswordRules`，不会在 API 响应中返回密码或令牌。

相关代码：

- `CoLearnX.Server\Controllers\AuthController.PasswordReset.cs`
- `CoLearnX.Server\Services\PasswordResetService.cs`
- `CoLearnX.Server\Services\PasswordResetMailSender.cs`
- `CoLearnX.Server\Domain\Entities\PasswordResetToken.cs`
- `colearnx.client\src\pages\ForgotPasswordPage.jsx`
- `colearnx.client\src\pages\ResetPasswordPage.jsx`

本地 Gmail SMTP 配置通过以下脚本写入 .NET user-secrets，密码只在本机隐藏输入，不写入仓库：

```powershell
Set-Location 'C:\Users\user\Documents\CoLearnX\.local-deploy\CoLearnX-master-20260918'
& '.\scripts\Configure-GmailPasswordReset.ps1' -Email 'jianglingmuse@gmail.com'
```

本次真实验证结果：

- Gmail SMTP 配置已加载，使用 `smtp.gmail.com:587`；
- 2026-09-19 16:36（Asia/Shanghai）从本地忘记密码页面发送真实邮件；
- Gmail 收件箱收到主题为 `Reset your CoLearnX password` 的邮件；
- 邮件中的本地链接成功打开 `/reset-password` 页面；
- 页面加载后会从地址栏清除 token fragment；
- 新密码输入和最终提交由用户本人完成，因此“真实账号完成重置并使用新密码登录”的证据仍需用户提交后再补充。

Gmail 配置说明和数据库兼容说明见 [PASSWORD_RESET_LOCAL.md](PASSWORD_RESET_LOCAL.md)。Google 应用专用密码说明见 [Google 官方帮助](https://support.google.com/mail/answer/185833?hl=en)。

## 2. 当前新用户注册行为

注册入口：`/register`

接口：

```http
POST /api/auth/register
```

请求示例：

```json
{
  "email": "new.user@example.com",
  "password": "StrongPass123!",
  "fullName": "New User"
}
```

当前注册流程如下：

1. 服务端将邮箱去除首尾空格并转换为小写。
2. 同时检查 `Users` 和 `AdminAccounts`，防止普通注册占用管理员邮箱。
3. 校验邮箱、姓名和密码格式。
4. 使用 BCrypt 生成密码哈希，数据库不保存明文密码。
5. 创建普通 User，并自动增加 `Member` 角色。
6. 创建默认用户偏好，初始学分为 `0`。
7. 生成新的会话标识和 JWT。
8. 返回登录令牌和 Member 用户信息，前端自动跳转到 `/member/home`。

密码规则定义在 `CoLearnX.Server\Auth\PasswordRules.cs`：

- 10–72 个字符；
- 至少一个大写字母；
- 至少一个小写字母；
- 至少一个数字；
- 至少一个特殊符号；
- 不能包含完整邮箱；
- 不能包含长度至少为 3 的邮箱前缀。

前端注册页面会提前显示密码检查项，但服务端仍然会再次校验，不能只依赖浏览器校验。

当前注册完成后只拥有 `Member` 角色。Trainer 和 Creator 角色需要通过后续角色申请与审核流程获得，不能在公开注册表单中直接选择高权限角色。

当前注册功能**没有邮箱验证步骤**：提交邮箱后会立即创建账号并自动登录。Gmail 真实投递目前用于忘记密码流程；如果以后要求注册也必须验证邮箱，需要增加邮箱验证令牌、验证链接、重发接口、过期机制以及未验证账号的登录限制。

注册相关代码：

- `CoLearnX.Server\Contracts\Dtos\ApiDtos.cs`：`RegisterRequest`
- `CoLearnX.Server\Controllers\ApiControllers.cs`：`POST /api/auth/register`
- `CoLearnX.Server\Services\AppServices.cs`：`AuthService.RegisterAsync`
- `colearnx.client\src\pages\RegisterPage.jsx`
- `colearnx.client\src\auth\AuthProvider.jsx`
- `colearnx.client\src\api\index.js`

## 3. 本地运行

所有命令都应在本地独立副本执行：

```powershell
Set-Location 'C:\Users\user\Documents\CoLearnX\.local-deploy\CoLearnX-master-20260918'
```

启动后端：

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Logging__EventLog__LogLevel__Default = 'None'
$env:Logging__LogLevel__Default = 'Warning'
$env:ConnectionStrings__Default = 'Data Source=App_Data/colearnx-member-next-local.db'

dotnet run `
  --project '.\CoLearnX.Server\CoLearnX.Server.csproj' `
  --no-launch-profile `
  --urls 'http://localhost:5088'
```

另开 PowerShell 启动前端：

```powershell
Set-Location 'C:\Users\user\Documents\CoLearnX\.local-deploy\CoLearnX-master-20260918\colearnx.client'
$env:ASPNETCORE_URLS = 'http://localhost:5088'
$env:DEV_SERVER_PORT = '55128'
npm.cmd run dev -- --host localhost
```

浏览器入口：

```text
https://localhost:55128/login
https://localhost:55128/register
https://localhost:55128/member/badges
```

本地演示账号：

```text
Member:  huang.yousheng@colearnx.com / Password123!
Trainer: gu.yincheng@colearnx.com / Password123!
Admin:   zhu.zirui@colearnx.com / Password123!  （使用 /admin/login）
```

## 4. 测试和本地流程证据

截至本 README 日期，已有以下验证记录：

| 验证项 | 结果 |
| --- | --- |
| 后端完整测试 | `205 passed, 0 failed` |
| 前端 Node 测试 | `19 passed` |
| 前端 UI 测试 | `23 passed across 12 files` |
| 前端生产构建 | 通过 |
| 新增组件和路由定向 ESLint | 通过 |
| `git diff --check` | 通过 |
| 注册相关 API 测试 | `18 passed, 0 failed` |
| 真实 Gmail SMTP 发信 | 已收到真实邮件 |
| 忘记密码链接打开 | 已打开本地重置页面 |
| 真实账号完成新密码提交和再次登录 | 等待用户完成最后提交 |

注册 API 定向测试可以这样运行。当前 Windows 测试环境需要关闭 EventLog 默认写入，否则系统可能因权限不足导致测试宿主失败：

```powershell
$env:Logging__EventLog__LogLevel__Default = 'None'
$env:Logging__LogLevel__Default = 'Warning'

dotnet test `
  '.\CoLearnX.Server.Tests\CoLearnX.Server.Tests.csproj' `
  --no-restore `
  --no-build `
  --filter 'FullyQualifiedName~AuthApiTests' `
  --verbosity quiet
```

注册测试覆盖正常创建、BCrypt 密码哈希、弱密码、重复邮箱、管理员邮箱冲突和密码包含邮箱前缀等行为。

证书流程曾在本地浏览器完整验证：Member 提交申请，Trainer 初审，Admin 终审签发，Member 查看新证书和三条流程通知，通知标记已读后刷新仍保持已读。

## 5. 数据库和安全边界

- 新增 `PasswordResetTokens` 表和唯一令牌哈希索引；
- 旧 SQLite 数据库启动时会补齐找回密码表，不删除已有用户数据；
- 现有 SQL Server 若缺少新表，会按说明停止并要求先检查 `scripts\PasswordReset.SqlServer.sql`；
- Gmail 应用专用密码保存在 user-secrets 或环境变量中，不进入源代码、README、日志和 API 响应；
- 本地数据库、邮件捕获目录和 Gmail 验证状态文件位于忽略路径；
- 当前未执行生产部署、未连接正式 SQL Server、未建立公开证书验证站点，也未实现 PDF 导出；
- 当前没有独立 Badge award engine，证书阶段展示不会被表述为独立徽章规则。

## 6. 当前未完成事项

以下项目没有被本次功能范围伪装成已完成：

1. 用户需要完成当前浏览器中的新密码输入和提交，才能补齐真实 Gmail 重置后的再次登录与旧密码失效证据。
2. 注册流程还没有邮箱验证；目前注册后立即成为可登录的 Member。
3. 全仓 lint 仍有基线问题：`LoginPage`、`AdminLoginPage` 的 effect 状态更新，以及 `RoleApplicationsPanel` 的混合导出和依赖警告。
4. SQL Server 结构脚本已提供，但本地验证使用的是 SQLite，未声称 SQL Server 联调完成。

本 README 只保存在当前独立本地副本中。除非用户另行授权，不进行 commit、push、上传或部署。
