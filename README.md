# CoLearnX

课设培训平台：React（Vite）+ ASP.NET Core + EF Core（SQLite）+ JWT + PayPal 学分。

## 用 Visual Studio 启动

1. 打开仓库根目录的 **`CoLearnX.slnx`**（VS 2026）或 **`CoLearnX.sln`**
2. 启动项目设为 **CoLearnX.Server**
3. 工具栏选 **https**，按 F5

不要用 IIS Express，也不要单独启动前端。前端由 SpaProxy 拉起。

- 页面：https://localhost:55128
- API：https://localhost:7238

| 角色 | 账号 | 密码 |
|------|------|------|
| Member | `huang.yousheng@colearnx.com` | `Password123!` |
| Trainer | `jane.smith@colearnx.com` | `Password123!` |
| Creator | `alex.lee@colearnx.com` | `Password123!` |
| Admin | `desmond.tan@colearnx.com` | `Password123!` |

登录后选 **Continue as** 对应角色。

## 仓库结构

| 路径 | 说明 |
|------|------|
| `CoLearnX.Server/` | API、领域模型、JWT、学分/PayPal |
| `CoLearnX.Server.Tests/` | 接口测试（xUnit） |
| `colearnx.client/` | React 前端 |
| `docs/` | 现行设计规格（线框、UML、配色、项目计划） |

测试：`dotnet test CoLearnX.Server.Tests/CoLearnX.Server.Tests.csproj`
