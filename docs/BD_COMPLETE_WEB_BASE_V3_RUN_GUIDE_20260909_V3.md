# CoLearnX B/D 完整 web-base `v3` 工作包解压与运行说明

| 项目 | 内容 |
|---|---|
| 工作包 | `CoLearnX_BD_Complete_WebBase_20260909_v3.zip` |
| 包内根目录 | `CoLearnX_BD_Complete_WebBase_20260909_v3` |
| 适用范围 | 完整可运行的 CoLearnX 项目，而非增量覆盖包 |
| 上游框架 | GitHub `web-base` 提交 `728a417bc847479f0a714a2a93007b444ea7548a` |
| B/D 集成起点 | 本地集成提交 `f1a4314f49ffbd820e2b89ec00c18b96f4908cbd` |
| 本次包含 | B1–B4、D1–D4、Later Phase、SQLite 演示数据库、测试与文档 |
| 默认数据库 | `CoLearnX.Server/colearnx-later-v1.db` |

## 1. 先确认：`v3` 可以独立使用

`v3` 是完整项目工作包。它已经把以下内容放在同一个目录中：

```text
GitHub web-base 完整框架
  + B1–B4 / D1–D4 本地集成
  + B/D Later Phase 源码、测试和前端页面
  + colearnx-later-v1.db 演示数据库
  + 运行、实现和 A/C 协作说明
```

因此解压到一个空目录后，**不需要再下载 GitHub `web-base`，也不需要把增量包覆盖到其他项目**。安装 .NET 和 npm 依赖后即可运行。

为避免把历史工作包再嵌套进来，`v3` 不包含 `.git`、`node_modules`、`bin`、`obj`、`dist`、旧 `deliverables` 文件夹和其他旧 SQLite 数据库。它可以运行，但不是保留 Git 提交历史的 Git 仓库；若需要分支、提交或推送，请在解压后的项目中另行初始化或连接 Git 远端。

旧的 `v1`、`v2` 是增量包，仍保留在原工作树中；请优先使用这个独立 `v3` 包。

## 2. 环境要求

本包打包前使用的环境：

```text
.NET SDK 10.0.303
Node.js v26.2.0
npm 11.13.0
Windows PowerShell
```

后端目标框架为 `net10.0`。先在 PowerShell 中检查：

```powershell
dotnet --version
node --version
npm.cmd --version
```

建议使用 .NET 10 和与项目当前 Vite 版本兼容的 Node/npm。若要最接近本次验证环境，请使用上列版本。

## 3. 解压到新目录

以下示例假设 ZIP 在 `C:\Downloads`，目标目录为 `C:\CoLearnX-v3`。目标目录应是新的空位置，避免混入旧项目的数据库或依赖文件。

```powershell
Expand-Archive `
  -LiteralPath 'C:\Downloads\CoLearnX_BD_Complete_WebBase_20260909_v3.zip' `
  -DestinationPath 'C:\CoLearnX-v3'

Set-Location 'C:\CoLearnX-v3\CoLearnX_BD_Complete_WebBase_20260909_v3'
```

解压后先确认完整项目和演示数据库都在：

```powershell
Test-Path -LiteralPath '.\CoLearnX.Server\CoLearnX.Server.csproj'
Test-Path -LiteralPath '.\CoLearnX.Server.Tests\CoLearnX.Server.Tests.csproj'
Test-Path -LiteralPath '.\colearnx.client\package.json'
Test-Path -LiteralPath '.\colearnx.client\package-lock.json'
Test-Path -LiteralPath '.\CoLearnX.Server\colearnx-later-v1.db'
Test-Path -LiteralPath '.\docs\BD_COMPLETE_WEB_BASE_V3_RUN_GUIDE_20260909_V3.md'
```

六项都应返回 `True`。若其中任一项是 `False`，请删除这次不完整的解压目录后重新从 `v3` ZIP 解压，不要从 `v1`/`v2` 补文件。

## 4. 第一次安装依赖

在项目根目录运行：

```powershell
dotnet restore .\CoLearnX.Server\CoLearnX.Server.csproj

Set-Location '.\colearnx.client'
npm.cmd ci
```

`node_modules` 和后端构建输出没有打进 ZIP，这是正常的；上述两条命令会在本机生成它们。

如果 `npm.cmd ci` 出现 `EPERM`、`unlink` 或文件被占用，先关闭正在使用该项目的 Vite、Visual Studio/VS Code 终端及 Node 进程，再重试。这类现象通常是 Windows 进程锁，不代表 B/D 源码逻辑失败。

## 5. 推荐启动方法：前后端分别启动

### 5.1 第一个 PowerShell：启动 API

回到项目根目录后运行：

```powershell
Set-Location 'C:\CoLearnX-v3\CoLearnX_BD_Complete_WebBase_20260909_v3'

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Logging__EventLog__LogLevel__Default = 'None'

dotnet run `
  --project .\CoLearnX.Server\CoLearnX.Server.csproj `
  --no-launch-profile `
  --urls http://localhost:5088
```

看到以下内容表示 API 已启动：

```text
Now listening on: http://localhost:5088
Application started
```

包内 `CoLearnX.Server/appsettings.json` 默认连接：

```text
Data Source=colearnx-later-v1.db
```

它会解析为 `CoLearnX.Server\colearnx-later-v1.db`，不会使用其他旧数据库。

### 5.2 第二个 PowerShell：启动 React 前端

新开一个 PowerShell：

```powershell
Set-Location 'C:\CoLearnX-v3\CoLearnX_BD_Complete_WebBase_20260909_v3\colearnx.client'

$env:ASPNETCORE_URLS = 'http://localhost:5088'
$env:DEV_SERVER_PORT = '55128'

npm.cmd run dev -- --host localhost
```

打开：

```text
https://localhost:55128
```

前端的 `/api` 请求会由 Vite proxy 转发到 `http://localhost:5088`。首次使用本地 HTTPS 时，如浏览器提示开发证书问题，可在普通用户 PowerShell 中执行一次：

```powershell
dotnet dev-certs https --trust
```

之后关闭并重新启动前端。

### 5.3 Visual Studio 启动方式

也可以打开根目录的 `CoLearnX.slnx`（VS 2026）或 `CoLearnX.sln`，将 `CoLearnX.Server` 设为启动项目，选择 `https` 后按 F5。手动双终端启动更适合排查 API 和前端分别出现的问题。

## 6. 演示账号和登录入口

| 身份 | 登录入口 | 账号 | 密码 |
|---|---|---|---|
| Member | 普通登录页 | `huang.yousheng@colearnx.com` | `Password123!` |
| Trainer | 普通登录页 | `gu.yincheng@colearnx.com` | `Password123!` |
| Creator | 普通登录页 | `zou.ruiqi@colearnx.com` | `Password123!` |
| Admin | `/admin/login` 或普通页的 operations sign-in | `zhu.zirui@colearnx.com` | `Password123!` |

Member、Trainer、Creator 使用普通账号登录，并选择该账号绑定的 active role。Admin 是独立 `AdminAccount`，只能用 Admin 入口登录，不能在普通角色选择器中选择 Admin。

## 7. 本包可用的 B/D 功能

| 模块 | 已实现并已纳入 v3 |
|---|---|
| B：CourseIntake / CourseSession | Trainer 创建、编辑、提交 Intake 与 Session；Creator 对本人 Course 的 Intake 确认或拒绝；Trainer 可更新已发布/进行中 Intake 的交付链接。 |
| B：Materials / Recordings | Trainer 查看 Admin 已批准材料版本、绑定到本人 Intake、查看绑定材料、添加 Session HTTP/HTTPS 录播；写入使用记录和审计。 |
| B：Attendance / Learners | Trainer 按本人 Intake 和 Session 保存 Present/Late/Absent，查看学习者、进度、出勤率、评分与证书申请状态。 |
| B：Assessment / Certificate | 创建 Assessment、评分并计算是否通过；对本人 Intake 的 CertificateRequest 进行初审。 |
| D：Admin 基础流程 | 独立 Admin 登录、Role Request 审批、Course 审批、Audit Log 查询筛选。 |
| D：Credit Ledger | 查询 CreditTransaction，按文本或类型过滤；执行带范围、理由、余额保护和幂等性的手工余额调整。 |
| D：Dispute / Refund | Member Dispute API；Admin 查看争议并退款或拒绝。退款在一个事务内更新争议、Enrollment、余额、流水、通知和审计。 |
| D：Later approvals | Admin 审核 CourseMaterialVersion；仅对 TrainerApproved 的证书申请做最终签发或拒绝，签发时生成 UserCertificate 和 VerificationCode。 |

关键职责边界仍然是：

```text
Trainer  创建、提交并交付 CourseIntake
Creator  确认或拒绝本人 Course 的 CourseIntake
Admin    不审核 CourseIntake
```

因此 `GET /api/admin/intakes` 返回 `404` 是预期的权限结果，不是少打进包的页面或 API。

Trainer 常用入口：Intake Detail 的 Materials & Recordings、Attendance、Learner List。Admin 常用入口：Approvals、Credit Ledger、Disputes。详细流程和 API 边界见 `docs/BD_LATER_PHASE_IMPLEMENTATION_20260909_V1.md`。

## 8. 可直接演示的流程

### 8.1 Trainer 交付与学习跟踪

1. 用 Trainer 账号进入普通登录页；
2. 打开 Trainer Intake 详情，绑定已批准材料并为 Session 添加 Recording；
3. 进入 Attendance，选择 Intake 和 Session 后保存出勤；
4. 打开 Learner List，创建 Assessment 并对学习者评分；
5. 在 Certificate gate 对符合条件的申请做 Trainer 初审。

### 8.2 Admin 材料审批与 Trainer 使用

1. 用 Admin 账号在 `/admin/login` 登录；
2. 打开 Approvals，切换到 Material versions；
3. 审批 Seed Data 中的 `PendingApproval` 材料；
4. 再以 Trainer 登录，在 Intake 详情中绑定已经批准的材料版本。

### 8.3 Admin 处理争议和退款

1. 用 Admin 登录并打开 Disputes；
2. 选择 Open dispute，填写 resolution note；
3. 执行 Refund 或 Reject；
4. 打开 Credit Ledger，确认产生对应流水。

审批、退款和证书终态不能反向修改。若要重复演示，请使用下节的数据库恢复方法，而不要试图把已处理的记录改回 Open/Pending。

## 9. 数据库备份、恢复与范围

包内演示库为：

```text
CoLearnX.Server\colearnx-later-v1.db
```

它由 `EnsureCreated` 建立，供本地演示使用，**不是正式 EF Core migration**。打包前已执行 SQLite `PRAGMA integrity_check`，结果为 `ok`；种子数据包括 3 个普通用户、1 个 AdminAccount、4 个 CourseIntake、2 个 CourseMaterialVersion、1 个 Assessment 和 1 个 Dispute。

开始会改动数据的演示前，先停止 API，再备份：

```powershell
Copy-Item `
  -LiteralPath '.\CoLearnX.Server\colearnx-later-v1.db' `
  -Destination '.\CoLearnX.Server\colearnx-later-v1.backup.db'
```

若要恢复初始状态：停止 API，将 ZIP 中同路径的原始 `colearnx-later-v1.db` 复制回项目的 `CoLearnX.Server` 目录。后端运行期间不要替换 SQLite 文件。

不要用早期的 `colearnx.db`、`colearnx-b1.db`、`colearnx-d1a.db` 或 `colearnx-bd.db` 覆盖当前库；旧库可能没有 Later Phase 所需的表。

## 10. 验证命令

安装依赖后，在项目根目录运行后端测试：

```powershell
dotnet test .\CoLearnX.Server.Tests\CoLearnX.Server.Tests.csproj --no-restore
```

前端检查：

```powershell
Set-Location '.\colearnx.client'

node --test `
  tests\api-client.test.js `
  tests\trainer-intake-form.test.js `
  tests\b4-workflow.test.js `
  tests\later-phase-api.test.js

npm.cmd run lint
npm.cmd run build
```

打包前的最新结果：

```text
Backend tests     125 passed, 0 failed
Frontend tests    17 passed, 0 failed
ESLint            0 errors
Vite build        passed
SQLite integrity  ok
```

这些结果是本包源码的本地验证证据；解压后的新环境仍应按上面命令重新执行，以确认本机 SDK、依赖和证书配置无误。

## 11. A/C 仍需配合的边界

`v3` 已包含 B/D 可运行实现，但不会把 A/C 尚未完成的功能宣称为完成：

| 协作方 | 仍需完成或冻结的契约 |
|---|---|
| A | 最终 `Enrollment.CourseIntakeId` 与多 Session 语义；Member CertificateRequest 提交/查询页面；Member Enrollment/Dispute 页面。 |
| C | Creator Course 创建、编辑、提交页面；材料版本历史（v2+）；真实二进制上传、对象存储和下载授权。 |
| A/C + B/D | 契约冻结后创建正式 EF Core migration、旧数据库升级，以及从真实 Member/Creator 页面发起的全角色浏览器端到端验收。 |

完整的字段、状态和接口配合要求见：

```text
docs\AC_COLLABORATION_REQUIREMENTS_FOR_BD_LATER_PHASE_20260909_V1.md
```

## 12. 常见问题

### Trainer 或 Creator 无法登录

不要用 `/admin/login`。Trainer/Creator 使用普通登录页，API 登录时也要传入正确的 `activeRole`，例如：

```json
{
  "email": "gu.yincheng@colearnx.com",
  "password": "Password123!",
  "activeRole": "Trainer"
}
```

### Admin 无法在普通账号角色选择中出现

这是预期设计。Admin 使用独立 `AdminAccount` 与 `/admin/login`，不属于 Member/Trainer/Creator 角色选择器。

### 后端提示数据库缺少 Later Phase 表

检查 `CoLearnX.Server/appsettings.json` 的连接字符串仍是：

```json
"Default": "Data Source=colearnx-later-v1.db"
```

再确认 `CoLearnX.Server\colearnx-later-v1.db` 存在。通常是误用了早期数据库，而不是 ZIP 内的完整项目缺少源码。

### EventLog 或 Data Protection 权限警告

普通本机运行优先使用第 5 节的 `$env:Logging__EventLog__LogLevel__Default = 'None'`。受限宿主中的 DPAPI 权限警告属于运行会话权限限制；本次本地验证中 JWT 登录和 Later Phase API 仍可成功执行。
