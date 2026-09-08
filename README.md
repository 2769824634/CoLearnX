# CoLearnX

课设培训平台：React（Vite）+ ASP.NET Core + EF Core（SQLite）+ JWT + PayPal 学分。

当前 B/D 集成版本：`20260908-v0`。本分支以 `web-base` 的
`728a417bc847479f0a714a2a93007b444ea7548a` 为基线，加入 Trainer/Creator 的
Course Intake 工作流以及独立 Admin 审批、审计工作流。

## 用 Visual Studio 启动

1. 打开仓库根目录的 **`CoLearnX.slnx`**（VS 2026）或 **`CoLearnX.sln`**
2. 启动项目设为 **CoLearnX.Server**
3. 工具栏选 **https**，按 F5

不要用 IIS Express，也不要单独启动前端。前端由 SpaProxy 拉起。

- 页面：https://localhost:55128
- API：https://localhost:7238

| 角色 | 姓名 | 账号 | 密码 |
|------|------|------|------|
| Member | Huang Yousheng | `huang.yousheng@colearnx.com` | `Password123!` |
| Trainer | Gu Yincheng | `gu.yincheng@colearnx.com` | `Password123!` |
| Creator | Zou Ruiqi | `zou.ruiqi@colearnx.com` | `Password123!` |
| Admin | Zhu Zirui | `zhu.zirui@colearnx.com` | `Password123!` |

Member、Trainer、Creator 从普通登录页进入；校验邮箱和密码后选择账号绑定的角色。
Admin 是独立身份，从普通登录页底部的 **operations sign-in** 或 `/admin/login` 进入。
每个演示账号只有一种身份。

本分支使用独立数据库 `CoLearnX.Server/colearnx-bd.db`，不会覆盖原有
`colearnx.db`、`colearnx-d1a.db` 或 `colearnx-b1.db`。如果需要重建本分支的演示数据，
只删除 `CoLearnX.Server/colearnx-bd.db` 后重新 F5。

## B/D 已接入的功能

- B：Trainer 创建、编辑、提交 Course Intake 和 Course Session；Creator 确认、驳回及审核变更申请；Trainer 在已发布或进行中的 Intake 更新交付链接。
- D：独立 Admin 登录；Role Request 审批；Course 审批；Audit Log 查询及筛选。
- 权限边界：Creator 审核 Intake，Admin 不审核 Intake，因此不存在 `/api/admin/intakes`。
- 兼容 web-base：保留普通账号的 available-roles 登录、Member 课程/报名/愿望单和原有演示账号。

## 仓库结构

| 路径 | 说明 |
|------|------|
| `CoLearnX.Server/` | API、领域模型、JWT、学分/PayPal |
| `CoLearnX.Server.Tests/` | 接口测试（xUnit） |
| `colearnx.client/` | React 前端 |
| `docs/` | 现行设计规格（线框、UML、配色、项目计划） |

验证命令：

```powershell
dotnet test CoLearnX.Server.Tests/CoLearnX.Server.Tests.csproj
Set-Location colearnx.client
node --test tests/*.test.js
npm run lint
npm run build
```
