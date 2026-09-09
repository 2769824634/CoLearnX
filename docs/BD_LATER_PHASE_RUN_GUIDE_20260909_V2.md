# CoLearnX B/D Later Phase ZIP 解压、运行与功能说明

| 项目 | 内容 |
|---|---|
| 文档版本 | `20260909-v2` |
| 推荐压缩包 | `CoLearnX_BD_Later_Phase_20260909_v2.zip` |
| B/D 本地分支 | `codex/bd-later-phase-20260908-v1` |
| 适配基线 | `f1a4314`（B/D 与 `web-base` 的本地集成提交） |
| 数据库 | `CoLearnX.Server/colearnx-later-v1.db` |

## 1. 先确认：这是增量包，不是完整项目包

压缩包只包含本次 B/D Later Phase 新增或修改的代码、测试、文档和 SQLite 数据库，不包含完整基线中的所有文件，例如：

```text
CoLearnX.Server/CoLearnX.Server.csproj
colearnx.client/package.json
colearnx.client/package-lock.json
README.md
完整的既有 A/B/C/D 源代码
```

因此，不能把 ZIP 单独解压到一个空目录后直接运行。正确方式是：

1. 准备完整的 CoLearnX `f1a4314` 基线项目；
2. 将 ZIP 中的同名文件按原目录结构覆盖到基线项目；
3. 安装依赖并启动后端、前端。

如果是在本机当前开发环境继续运行，不需要再次覆盖 ZIP，直接进入以下工作树：

```text
C:\Users\user\Documents\CoLearnX\.worktrees\bd-web-base-integration
```

## 2. 压缩包目录结构

解压后最外层目录为：

```text
CoLearnX_BD_Later_Phase_20260909_v2
```

主要内容：

```text
CoLearnX_BD_Later_Phase_20260909_v2/
├─ CoLearnX.Server/
│  ├─ Contracts/Dtos/LaterPhaseDtos.cs
│  ├─ Controllers/
│  ├─ Data/
│  ├─ Domain/
│  ├─ Services/
│  ├─ appsettings.json
│  └─ colearnx-later-v1.db
├─ CoLearnX.Server.Tests/
│  └─ LaterPhaseWorkflowIntegrationTests.cs
├─ colearnx.client/
│  ├─ src/api/
│  ├─ src/pages/admin/
│  ├─ src/pages/trainer/
│  ├─ src/styles/
│  └─ tests/later-phase-api.test.js
├─ docs/
│  ├─ BD_LATER_PHASE_IMPLEMENTATION_20260909_V1.md
│  ├─ AC_COLLABORATION_REQUIREMENTS_FOR_BD_LATER_PHASE_20260909_V1.md
│  └─ BD_LATER_PHASE_RUN_GUIDE_20260909_V2.md
└─ .gitignore
```

## 3. 环境要求

本次验证使用：

```text
.NET SDK 10.0.303
Node.js v26.2.0
npm 11.13.0
Windows PowerShell
```

项目目标框架为：

```xml
<TargetFramework>net10.0</TargetFramework>
```

运行前检查：

```powershell
dotnet --version
node --version
npm.cmd --version
```

只要使用兼容当前项目和 Vite 8 的 Node/npm 版本即可；若希望最大程度复现本次验证，使用上述版本。

## 4. 将增量包覆盖到完整基线

以下示例假设：

```text
完整基线：C:\CoLearnX-run
解压目录：C:\Downloads\CoLearnX_BD_Later_Phase_20260909_v2
```

先确认基线目录中存在：

```powershell
Test-Path -LiteralPath 'C:\CoLearnX-run\CoLearnX.Server\CoLearnX.Server.csproj'
Test-Path -LiteralPath 'C:\CoLearnX-run\colearnx.client\package.json'
```

两个结果都必须为 `True`。然后执行覆盖：

```powershell
Copy-Item `
  -Path 'C:\Downloads\CoLearnX_BD_Later_Phase_20260909_v2\*' `
  -Destination 'C:\CoLearnX-run' `
  -Recurse `
  -Force
```

覆盖完成后确认关键文件：

```powershell
Test-Path -LiteralPath 'C:\CoLearnX-run\CoLearnX.Server\Services\TrainerLaterPhaseService.cs'
Test-Path -LiteralPath 'C:\CoLearnX-run\CoLearnX.Server\Services\AdminFinanceService.cs'
Test-Path -LiteralPath 'C:\CoLearnX-run\CoLearnX.Server\colearnx-later-v1.db'
Test-Path -LiteralPath 'C:\CoLearnX-run\colearnx.client\src\pages\admin\AdminDisputesPage.jsx'
```

四个结果都应为 `True`。

不要把增量包覆盖到未确认版本的旧项目。旧项目如果没有 `f1a4314` 中的 CourseIntake、Admin auth、Creator Intake review 和共享 DTO，可能出现编译冲突或路由缺失。

## 5. 安装依赖

打开 PowerShell：

```powershell
Set-Location 'C:\CoLearnX-run'
dotnet restore .\CoLearnX.Server\CoLearnX.Server.csproj

Set-Location '.\colearnx.client'
npm.cmd ci
```

如果是在已经安装过依赖的当前本地工作树，可以跳过 `npm.cmd ci`。如果执行时出现 `EPERM` 或文件被占用，先关闭正在运行的 Vite、Visual Studio 或对应 Node 进程，再重试；这通常是 Windows 文件锁，不是 Later Phase 源代码错误。

## 6. 推荐运行方式：后端和前端分开启动

分开启动更容易看清后端和前端错误，也不依赖 Visual Studio 的 SpaProxy 自动启动。

### 6.1 第一个 PowerShell：启动后端

```powershell
Set-Location 'C:\CoLearnX-run'

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Logging__EventLog__LogLevel__Default = 'None'

dotnet run `
  --project .\CoLearnX.Server\CoLearnX.Server.csproj `
  --no-launch-profile `
  --urls http://localhost:5088
```

看到以下内容表示后端已启动：

```text
Now listening on: http://localhost:5088
Application started
```

默认数据库由 `CoLearnX.Server/appsettings.json` 指向：

```text
Data Source=colearnx-later-v1.db
```

相对路径会被解析为：

```text
C:\CoLearnX-run\CoLearnX.Server\colearnx-later-v1.db
```

### 6.2 第二个 PowerShell：启动前端

```powershell
Set-Location 'C:\CoLearnX-run\colearnx.client'

$env:ASPNETCORE_URLS = 'http://localhost:5088'
$env:DEV_SERVER_PORT = '55128'

npm.cmd run dev -- --host localhost
```

Vite 启动后访问：

```text
https://localhost:55128
```

前端通过 Vite proxy 将 `/api` 请求转发到：

```text
http://localhost:5088
```

第一次运行如果浏览器提示本地 HTTPS 证书问题，可以在普通用户 PowerShell 中执行：

```powershell
dotnet dev-certs https --trust
```

然后关闭并重新启动前端。

### 6.3 当前本机工作树直接运行

当前机器不需要解压覆盖，使用：

```powershell
Set-Location 'C:\Users\user\Documents\CoLearnX\.worktrees\bd-web-base-integration'

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Logging__EventLog__LogLevel__Default = 'None'

dotnet run `
  --project .\CoLearnX.Server\CoLearnX.Server.csproj `
  --no-launch-profile `
  --urls http://localhost:5088
```

第二个 PowerShell：

```powershell
Set-Location 'C:\Users\user\Documents\CoLearnX\.worktrees\bd-web-base-integration\colearnx.client'

$env:ASPNETCORE_URLS = 'http://localhost:5088'
$env:DEV_SERVER_PORT = '55128'

npm.cmd run dev -- --host localhost
```

## 7. 演示账号

| 身份 | 登录入口 | 账号 | 密码 |
|---|---|---|---|
| Member | 普通登录页 | `huang.yousheng@colearnx.com` | `Password123!` |
| Trainer | 普通登录页 | `gu.yincheng@colearnx.com` | `Password123!` |
| Creator | 普通登录页 | `zou.ruiqi@colearnx.com` | `Password123!` |
| Admin | `/admin/login` 或 operations sign-in | `zhu.zirui@colearnx.com` | `Password123!` |

Member、Trainer、Creator 使用普通用户认证，并选择账号已有的 active role。Admin 使用独立 `AdminAccount`、独立登录入口和独立 Admin token，不能在普通角色选择中选择 Admin。

## 8. 本次实现的功能

### 8.1 B：Trainer LearningMaterial

Trainer 可以：

- 查看 Admin 已批准的 `CourseMaterialVersion`；
- 把具体材料版本绑定到本人负责的 CourseIntake；
- 查看 Intake 已绑定的材料；
- 绑定时写入 `MaterialUsageLog` 和 `AuditLog`。

Trainer 不能绑定 PendingApproval 或 Rejected 的材料，也不能向其他 Trainer 的 Intake 绑定材料。

前端入口：

```text
Trainer → Course / Intake Detail → Materials & Recordings
```

### 8.2 B：Session Recording

Trainer 可以：

- 按 CourseSession 查看录播链接；
- 添加 HTTP/HTTPS 录播 URL；
- 防止同一 Session 重复添加相同 URL；
- 只能操作本人 Intake 下的 Session。

前端入口：

```text
Trainer → Course / Intake Detail → Materials & Recordings
```

### 8.3 B：Attendance

Trainer 可以：

- 选择本人 CourseIntake；
- 选择 Intake 下的 CourseSession；
- 查看该 Session 的学习者；
- 批量保存 `Present`、`Late`、`Absent`；
- 更新已有 AttendanceRecord；
- 记录操作审计。

后端会拒绝：

- 不属于当前 Trainer 的 Intake；
- 不属于当前 Intake 的 Session；
- 不属于当前 Session 的 Enrollment；
- 同一请求中重复的 Enrollment；
- 不存在的 AttendanceStatus。

前端入口：

```text
Trainer → Attendance
```

### 8.4 B：Learner List

Trainer 可以按 Intake 查看：

- 学习者姓名和邮箱；
- Enrollment 状态；
- ProgressPercent；
- Attendance rate；
- 已评分 Assessment 数量；
- 已通过 Assessment 数量；
- CertificateRequest 状态。

前端入口：

```text
Trainer → Learner List
```

### 8.5 B：Assessment 与 Grading

Trainer 可以：

- 为本人 CourseIntake 创建 Assessment；
- 设置 title、maxScore、passScore 和 dueAt；
- 对 Intake 中的 Enrollment 评分；
- 更新分数和 feedback；
- 自动判断是否达到 passScore；
- 创建 AuditLog。

前端入口：

```text
Trainer → Learner List → Create assessment / Grade learner
```

### 8.6 B：CertificateRequest Trainer 初审

证书申请状态流程：

```text
Member Submitted
→ TrainerApproved
→ TrainerRejected
```

Trainer 只能审核本人 Intake 的请求。拒绝必须填写原因；已完成的决定不能反向修改。

前端入口：

```text
Trainer → Learner List → Certificate gate
```

### 8.7 D：Admin Credit Ledger

Admin 可以：

- 查看所有 CreditTransaction；
- 按姓名、邮箱或描述搜索；
- 按 TopUp、Enrolment、Refund、Royalty、AdminAdjustment 过滤；
- 查看 Delta、BalanceAfter、Enrollment/Dispute 引用和 Admin 操作人。

前端入口：

```text
Admin → Credit Ledger
```

### 8.8 D：Manual Credit Adjustment

Admin 可以对指定 User 执行手工信用调整：

- Delta 必须在 `-10000` 到 `10000` 之间且不能为 0；
- 负数调整不能让 CreditBalance 小于 0；
- 必须填写 reason；
- 同时生成 CreditTransaction 和 AuditLog；
- 失败重试复用同一 IdempotencyKey；
- 相同 IdempotencyKey 不能用于不同用户、金额或理由。

前端入口：

```text
Admin → Credit Ledger → Manual credit adjustment
```

### 8.9 D：Dispute 与 Refund

Member 可以对本人的有效 Enrollment 提交 Dispute。Admin 可以：

- 查看 Open、ResolvedRefund、Rejected；
- 输入 resolution note；
- Refund 指定信用点数；
- Reject 争议；
- 防止同一处理重复执行。

退款会在一个事务中同步：

```text
Dispute
Enrollment
User.CreditBalance
CreditTransaction
Notification
AuditLog
```

前端入口：

```text
Admin → Disputes
```

Member 的 Dispute 页面仍属于 A，当前包已经提供 Member API 和 Admin 页面。

### 8.10 D：CourseMaterialVersion Admin 审批

Admin 可以：

- 查看 PendingApproval 材料版本；
- Approve 后允许 Trainer 使用；
- Reject 并填写原因；
- 查看 Creator、格式、版本号和材料路径；
- 防止已审核版本被反向改判。

前端入口：

```text
Admin → Approvals → Material versions
```

包内数据库已经准备一个 PendingApproval 演示材料，可以直接进入该队列测试。

### 8.11 D：CertificateRequest Admin 终审

Admin 只能审核 `TrainerApproved` 请求：

```text
TrainerApproved → Issued
TrainerApproved → AdminRejected
```

批准后生成 `UserCertificate` 和唯一 VerificationCode。Admin 不能跳过 Trainer 初审，也不能反向修改已签发或已拒绝的请求。

前端入口：

```text
Admin → Approvals → Certificates
```

### 8.12 保持不变的 Intake 审批边界

```text
Trainer：创建、修改、提交和交付 CourseIntake
Creator：确认或拒绝本人 Course 的 CourseIntake
Admin：不审核 CourseIntake
```

因此：

```text
GET /api/admin/intakes = 404
```

这是正确权限结果，不是缺少 Admin 页面。

## 9. 当前可以从 UI 演示的流程

### 9.1 Trainer 交付流程

1. 使用 Trainer 账号登录；
2. 进入 Trainer Intake 详情；
3. 绑定 Admin 已批准材料；
4. 为 Session 添加 Recording；
5. 进入 Attendance 保存出勤；
6. 进入 Learner List 创建 Assessment；
7. 为学习者评分；
8. 查看 Certificate gate。

### 9.2 Admin 材料审批

1. 使用 Admin 独立入口登录；
2. 进入 `Approvals`；
3. 切换到 `Material versions`；
4. 审核包内数据库中的 PendingApproval 材料；
5. 重新登录 Trainer，在 Intake 详情中绑定批准后的版本。

### 9.3 Admin Dispute/Refund

1. 使用 Admin 登录；
2. 进入 `Disputes`；
3. 选择包内数据库中的 Open dispute；
4. 填写 resolution note；
5. 执行 Refund 或 Reject；
6. 进入 Credit Ledger 检查退款记录。

不要在需要重复演示时直接复用已经处理过的同一数据库，因为争议和审批终态不能反向恢复。需要重复演示时，先备份当前数据库，再从 ZIP 中重新复制原始 `colearnx-later-v1.db`。

## 10. 当前只能通过 API 或测试覆盖的流程

由于 A/C 页面仍未完成，以下入口目前不是完整 UI 流程：

| 功能 | 已完成 | 仍缺 |
|---|---|---|
| Member CertificateRequest | 后端提交、资格校验、Trainer/Admin 审核 | A 的 Member 提交和本人查询页面 |
| Member Dispute | 后端提交和本人查询 | A 的 Member Enrollment/Dispute 页面 |
| Creator Material | 创建 v1 元数据、Admin 审批 | C 的 Creator 材料页面、v2+、真实文件上传 |
| Creator Course | Admin review 和 Intake Creator review 已有 | C 的 Course 创建、编辑、提交页面 |
| 多 Session Enrollment | B 的 Intake/Session/Attendance 服务已存在 | A 的最终 Enrollment.CourseIntakeId 契约 |

不能因为后端测试或 SeedData 中存在数据，就声称这些 A/C 页面已经完成。

## 11. 快速 API 健康检查

后端启动后，可以在另一个 PowerShell 检查 Trainer 登录和 Later Phase API：

```powershell
$base = 'http://localhost:5088'

$body = @{
  email = 'gu.yincheng@colearnx.com'
  password = 'Password123!'
  activeRole = 'Trainer'
} | ConvertTo-Json

$login = Invoke-RestMethod `
  -Method Post `
  -Uri "$base/api/auth/login" `
  -ContentType 'application/json' `
  -Body $body

$headers = @{
  Authorization = "Bearer $($login.accessToken)"
}

Invoke-RestMethod `
  -Uri "$base/api/trainer/learning-materials" `
  -Headers $headers
```

Admin 检查：

```powershell
$adminBody = @{
  email = 'zhu.zirui@colearnx.com'
  password = 'Password123!'
} | ConvertTo-Json

$adminLogin = Invoke-RestMethod `
  -Method Post `
  -Uri "$base/api/admin/auth/login" `
  -ContentType 'application/json' `
  -Body $adminBody

$adminHeaders = @{
  Authorization = "Bearer $($adminLogin.accessToken)"
}

Invoke-RestMethod `
  -Uri "$base/api/admin/credits/ledger" `
  -Headers $adminHeaders

Invoke-RestMethod `
  -Uri "$base/api/admin/material-versions" `
  -Headers $adminHeaders
```

Admin 登录会写入 AuditLog，因此它不是完全只读操作。

## 12. 运行测试

### 12.1 后端全量测试

```powershell
Set-Location 'C:\CoLearnX-run'

dotnet test `
  .\CoLearnX.Server.Tests\CoLearnX.Server.Tests.csproj `
  --no-restore
```

本次打包前验证结果：

```text
125 passed
0 failed
0 skipped
```

### 12.2 前端测试、Lint 和 Build

```powershell
Set-Location 'C:\CoLearnX-run\colearnx.client'

node --test `
  tests\api-client.test.js `
  tests\trainer-intake-form.test.js `
  tests\b4-workflow.test.js `
  tests\later-phase-api.test.js

npm.cmd run lint
npm.cmd run build
```

本次打包前验证结果：

```text
Frontend tests    17 passed, 0 failed
ESLint            0 errors
Vite build        passed
```

## 13. 数据库说明

包内数据库：

```text
CoLearnX.Server/colearnx-later-v1.db
```

打包前验证：

```text
SQLite integrity_check     ok
Users                      3
AdminAccounts              1
CourseIntakes              4
CourseMaterialVersions     2
Assessments                1
Disputes                   1
```

该数据库由 `EnsureCreated` 建立，用于本地演示，不是正式 migration。不要用旧的 `colearnx.db`、`colearnx-b1.db`、`colearnx-d1a.db` 或 `colearnx-bd.db` 覆盖它；旧库缺少 Later Phase 表时，应用会停止并提示使用新的隔离数据库。

如果要恢复 ZIP 中的初始演示状态：

1. 先停止后端；
2. 把当前 `colearnx-later-v1.db` 重命名备份；
3. 从 ZIP 中重新复制原始数据库；
4. 再启动后端。

不要在后端运行期间替换 SQLite 文件。

## 14. 常见问题

### 14.1 Trainer 或 Creator 无法登录

Trainer、Creator 不是 Admin，不应使用 `/admin/login`。应从普通登录页进入，输入账号密码后选择对应 active role。

如果直接调用 API，请确保 body 包含：

```json
{
  "email": "gu.yincheng@colearnx.com",
  "password": "Password123!",
  "activeRole": "Trainer"
}
```

### 14.2 Admin 无法通过普通角色登录

这是设计要求。Admin 是独立 `AdminAccount`，使用：

```text
/admin/login
```

不能把 Admin 加入 Member/Trainer/Creator 的 role selector。

### 14.3 提示数据库缺少 Later Phase 表

现象通常是：项目仍连接旧 `colearnx-bd.db` 或更早数据库。确认 `appsettings.json`：

```json
{
  "ConnectionStrings": {
    "Default": "Data Source=colearnx-later-v1.db"
  }
}
```

并确认包内数据库已复制到 `CoLearnX.Server` 目录。

### 14.4 Windows 显示 EventLog 或 Data Protection 权限错误

在普通本机运行时先使用：

```powershell
$env:Logging__EventLog__LogLevel__Default = 'None'
```

Codex 或受限沙箱中的 DPAPI 写权限错误属于宿主会话权限问题。本次验证中即使出现该警告，JWT 登录和 Later Phase API 仍实际成功；在普通 Windows 用户终端中通常不会出现相同限制。

### 14.5 `/api/admin/intakes` 返回 404

这是正确结果。CourseIntake 由 Trainer 管理并由 Creator确认或拒绝，Admin 不审核 Intake。

### 14.6 材料链接没有真实文件

当前 Creator 最小接口只保存 `materials/...` 相对路径和元数据。真实二进制上传、对象存储和下载授权属于 C/共享基础设施尚未完成的部分。

## 15. 当前完成边界

可以确认完成：

- B/D Later Phase 后端实体、服务、控制器和权限；
- Trainer Materials、Recordings、Attendance、Learner List、Assessment、Grading 和证书初审页面；
- Admin Credit Ledger、Manual Adjustment、Dispute/Refund、材料审批和证书终审页面；
- SQLite 演示数据库；
- 125 个后端测试、17 个前端测试、Lint 和生产构建；
- Trainer/Admin 本地登录和 Later Phase API 烟雾验证。

仍未完成：

- A 的最终 Intake Enrollment、多 Session 语义和 Member 页面；
- C 的 Creator Course、材料版本历史和真实文件上传；
- A/C 冻结后的正式 EF Core migration；
- 旧数据库升级；
- 从真实 A/C 页面开始的全角色浏览器端到端验收；
- 已报告依赖安全告警的独立升级批次。

详细 A/C 配合内容见：

```text
docs/AC_COLLABORATION_REQUIREMENTS_FOR_BD_LATER_PHASE_20260909_V1.md
```
