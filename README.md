# CoLearnX B/D 集成声明与运行 README（v3.1）

| 项目 | 内容 |
|---|---|
| 声明版本 | `v3.1` |
| GitHub 仓库 | [`2769824634/CoLearnX`](https://github.com/2769824634/CoLearnX) |
| 运行分支 | [`20290909TrainerAdmin`](https://github.com/2769824634/CoLearnX/tree/20290909TrainerAdmin) |
| 分支发布提交 | `c1bc3368ce042ea93d50d16446e5db94dac4b9c1` |
| 范围 | B（Trainer / CourseIntake / CourseSession）与 D（Admin / Audit / Later Phase）集成实现 |
| 技术栈 | React + Vite、ASP.NET Core .NET 10、EF Core、SQLite、JWT |

> 这是一份 **B/D 代码完成范围声明与 GitHub 运行说明**。它描述当前分支实际已经提交的功能，并清楚区分仍需 A/C 配合完成的页面、业务契约与正式 migration；不将未完成部分表述为已完成。

## 1. 版本结论

`20290909TrainerAdmin` 是完整的 `web-base` 本地集成版本，不是增量代码包。克隆该分支后，项目已经包含后端、React 前端、测试、B/D 实现说明与 A/C 协作说明；首次安装依赖并启动后即可本地运行。

GitHub 分支有意不提交本地 SQLite 演示数据库、ZIP 工作包、`node_modules`、`bin`、`obj` 与 `dist`。应用首次启动时会从当前模型创建 `CoLearnX.Server/colearnx-later-v1.db`，并由 SeedData 写入演示账号与基础数据。因此，从 GitHub 运行时不需要下载本地 `v3` ZIP，也不要把旧数据库复制进来。

## 2. B 部分已完成的功能

### 2.1 Trainer CourseIntake / CourseSession 核心流程

- Trainer 创建、编辑、提交 CourseIntake；
- Trainer 维护 CourseSession，并进行 Intake 与 Session 的时间、地点、容量和交付信息校验；
- Creator 对本人 Course 的 CourseIntake 执行确认或拒绝；
- Trainer 对已发布或进行中的 Intake 更新交付链接；
- Trainer 令牌与角色会在关键写操作中重新验证，已禁用账号或已撤销 Trainer 角色的旧令牌不能继续操作。

职责边界固定如下：

```text
Trainer  创建、提交并交付 CourseIntake
Creator  确认或拒绝本人 Course 的 CourseIntake
Admin    不审核 CourseIntake
```

所以 `GET /api/admin/intakes` 返回 `404` 是正确的设计结果。

### 2.2 Trainer Learning Material 与 Recording

- 查看 Admin 已批准的 `CourseMaterialVersion`；
- 将具体材料版本绑定到本人负责的 CourseIntake；
- 查看 Intake 已绑定的材料；
- 为本人 Intake 下的 CourseSession 添加 HTTP/HTTPS Recording URL；
- 阻止绑定 PendingApproval/Rejected 材料、跨 Trainer 操作和重复录播 URL；
- 写入 `MaterialUsageLog` 与 `AuditLog`。

### 2.3 Attendance、Learner List、Assessment 与 Grading

- Trainer 以 Intake 和 Session 为范围批量保存 `Present`、`Late`、`Absent` 出勤；
- 查看学习者姓名、邮箱、Enrollment 状态、进度、出勤率、评分及证书申请状态；
- 创建 Assessment，设置最高分、及格分和截止时间；
- 对本 Intake 下的 Enrollment 评分，自动判断是否通过并保存 feedback；
- 阻止跨 Intake、跨 Session、重复 Enrollment 或非法状态写入。

### 2.4 CertificateRequest Trainer 初审

Trainer 可审核本人 Intake 的证书申请：

```text
Submitted → TrainerApproved
Submitted → TrainerRejected
```

拒绝必须带原因，终态不可反向修改。Admin 只能处理 `TrainerApproved` 请求，不能跳过 Trainer 初审。

## 3. D 部分已完成的功能

### 3.1 独立 Admin 身份、审批与审计

- `AdminAccount` 独立登录和独立 Admin token；
- Role Request 审批；
- Course 审批；
- Audit Log 查询与筛选；
- Admin 不被加入普通 Member/Trainer/Creator 的角色选择器。

### 3.2 Credit Ledger 与 Manual Credit Adjustment

- 查看全部 `CreditTransaction`；
- 按姓名、邮箱、描述或交易类型筛选；
- 管理员对指定用户进行 `-10000` 至 `10000` 范围内的余额调整；
- 拒绝金额为零、空理由以及会让余额变为负数的操作；
- 同时写入 CreditTransaction 和 AuditLog；
- 使用 IdempotencyKey，防止相同请求重复扣加余额。

### 3.3 Dispute 与 Refund

- Member Dispute 的后端提交和本人查询 API；
- Admin 查看 Open、ResolvedRefund、Rejected 状态；
- Admin 输入 resolution note 后执行 Refund 或 Reject；
- Refund 在一个事务中更新 Dispute、Enrollment、CreditBalance、CreditTransaction、Notification 与 AuditLog；
- 已处理的争议不可再次处理。

### 3.4 Later Phase Admin Approvals

- 审核 `CourseMaterialVersion`：Approve 或带原因 Reject；
- 只允许 Trainer 使用已批准的材料版本；
- 对 `TrainerApproved` 的 CertificateRequest 执行最终签发或拒绝；
- 签发时生成 `UserCertificate` 与唯一 `VerificationCode`；
- 已审核材料和已完成证书决定不可反向更改。

## 4. 从 GitHub 克隆并运行

### 4.1 环境要求

建议环境：

```text
.NET SDK 10.x（项目目标框架：net10.0）
Node.js 26.x 或与 Vite 8 兼容的 Node.js 版本
npm 11.x
Windows PowerShell
```

检查环境：

```powershell
dotnet --version
node --version
npm.cmd --version
```

### 4.2 克隆指定分支

在新目录中执行：

```powershell
git clone `
  --branch 20290909TrainerAdmin `
  --single-branch `
  https://github.com/2769824634/CoLearnX.git `
  CoLearnX-v3.1

Set-Location '.\CoLearnX-v3.1'
git rev-parse HEAD
```

发布时的预期提交为：

```text
c1bc3368ce042ea93d50d16446e5db94dac4b9c1
```

如果未来分支继续更新，`git rev-parse HEAD` 可以不同；此时应以 GitHub 分支页面和当前提交说明为准。

### 4.3 安装依赖

在仓库根目录运行：

```powershell
dotnet restore .\CoLearnX.Server\CoLearnX.Server.csproj

Set-Location '.\colearnx.client'
npm.cmd ci
```

`node_modules`、后端构建输出和 SQLite 数据库均由本机生成，不在 GitHub 中提交。

### 4.4 启动后端

打开第一个 PowerShell，进入仓库根目录：

```powershell
Set-Location '.\CoLearnX-v3.1'

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Logging__EventLog__LogLevel__Default = 'None'

dotnet run `
  --project .\CoLearnX.Server\CoLearnX.Server.csproj `
  --no-launch-profile `
  --urls http://localhost:5088
```

看到以下文字即表示 API 已启动：

```text
Now listening on: http://localhost:5088
Application started
```

首次启动会创建：

```text
CoLearnX.Server\colearnx-later-v1.db
```

该库来自 `EnsureCreated` 和 SeedData，适合本地课程演示，不是正式 EF Core migration。

### 4.5 启动前端

打开第二个 PowerShell：

```powershell
Set-Location '.\CoLearnX-v3.1\colearnx.client'

$env:ASPNETCORE_URLS = 'http://localhost:5088'
$env:DEV_SERVER_PORT = '55128'

npm.cmd run dev -- --host localhost
```

浏览器访问：

```text
https://localhost:55128
```

Vite 会将 `/api` 请求代理到 `http://localhost:5088`。首次遇到本地 HTTPS 证书提示时，可在普通用户 PowerShell 运行：

```powershell
dotnet dev-certs https --trust
```

然后重新启动前端。

## 5. 演示账号

| 身份 | 入口 | 账号 | 密码 |
|---|---|---|---|
| Member | 普通登录页 | `huang.yousheng@colearnx.com` | `Password123!` |
| Trainer | 普通登录页 | `gu.yincheng@colearnx.com` | `Password123!` |
| Creator | 普通登录页 | `zou.ruiqi@colearnx.com` | `Password123!` |
| Admin | `/admin/login` 或 operations sign-in | `zhu.zirui@colearnx.com` | `Password123!` |

Trainer/Creator/Member 需要使用普通登录页，并选择各自的 active role。Admin 必须从独立入口登录；Admin 不会出现在普通角色选择器中。

## 6. 可运行的主要页面与演示顺序

| 身份 | 推荐入口 | 可演示内容 |
|---|---|---|
| Trainer | Intake Detail | 材料绑定、Recording、Intake 交付信息。 |
| Trainer | Attendance | 按 Intake/Session 维护出勤。 |
| Trainer | Learner List | 查看学习者、创建 Assessment、评分、证书初审。 |
| Admin | Approvals | 材料版本审批、证书终审。 |
| Admin | Credit Ledger | 查询流水、执行手工 Credit Adjustment。 |
| Admin | Disputes | 处理争议、退款或拒绝，并回看流水。 |

审批、退款和证书签发均会改变本地 SQLite 数据。若需要重复演示，请先停止后端并备份 `CoLearnX.Server/colearnx-later-v1.db`，不要在后端运行期间替换数据库文件。

## 7. 验证命令

后端测试：

```powershell
Set-Location '.\CoLearnX-v3.1'
dotnet test .\CoLearnX.Server.Tests\CoLearnX.Server.Tests.csproj --no-restore
```

前端测试、Lint 与生产构建：

```powershell
Set-Location '.\CoLearnX-v3.1\colearnx.client'

node --test `
  tests\api-client.test.js `
  tests\trainer-intake-form.test.js `
  tests\b4-workflow.test.js `
  tests\later-phase-api.test.js

npm.cmd run lint
npm.cmd run build
```

本次发布前的已记录验证结果为：后端 125 项测试通过、前端 17 项测试通过、ESLint 0 errors、Vite production build 通过。不同电脑若受 Windows Event Log 权限影响，应先保留第 4.4 节的 EventLog 环境变量，并以当前机器重新运行的结果为准。

## 8. 当前边界：仍需 A/C 配合完成的部分

| 协作方 | 尚未完成或尚待冻结的内容 |
|---|---|
| A | 最终 `Enrollment.CourseIntakeId` 与多 Session 语义；Member CertificateRequest 提交/查询页面；Member Enrollment/Dispute 页面。 |
| C | Creator Course 创建、编辑、提交页面；材料版本历史（v2+）；真实二进制上传、对象存储及下载授权。 |
| A/C + B/D | 在契约冻结后补正式 EF Core migration、旧数据库升级，以及由真实 Member/Creator 页面发起的全角色浏览器端到端验收。 |

更详细的字段、状态和接口协作约束见：

```text
docs/AC_COLLABORATION_REQUIREMENTS_FOR_BD_LATER_PHASE_20260909_V1.md
```

## 9. 相关文档

| 文档 | 用途 |
|---|---|
| `docs/BD_LATER_PHASE_IMPLEMENTATION_20260909_V1.md` | B/D Later Phase 的实现、权限与验证边界。 |
| `docs/AC_COLLABORATION_REQUIREMENTS_FOR_BD_LATER_PHASE_20260909_V1.md` | A/C 与 B/D 的字段、状态、页面和迁移配合要求。 |
| `docs/BD_COMPLETE_WEB_BASE_V3_RUN_GUIDE_20260909_V3.md` | 本地完整 v3 ZIP 的解压、运行、测试和数据库恢复说明。 |

## 10. 使用声明

本分支用于课程项目演示与团队协作。当前配置、JWT signing key、PayPal 空配置和演示账号均为开发/教学用途；部署到真实环境前，必须改用受保护的环境变量或 secret store，并替换演示账号与开发密钥。
