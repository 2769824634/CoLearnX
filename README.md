# CoLearnX

CoLearnX 是一个培训课程网站。访客可以浏览已发布的课程；登录后按身份进入各自的工作区。

## 现在能做什么

- 浏览课程和课程详情
- 会员：选课与学习、我的课程、积分支付、徽章和账户
- 讲师：准备班期、出勤和学员
- 课程创建者：创建课程、确认排期、上传资料
- 管理员：审批、用户个人资料与学分账本、员工资料与密码重置、争议和审计记录
- 平台员工：管理平台课程和班级、查看本人账户和资料

会员、讲师和课程创建者使用同一套账号，可以在自己的身份之间切换。管理员使用单独的登录入口。

## 本地运行

项目需要 .NET 10 SDK、Node.js 和 npm。本机验证环境为 .NET SDK 10.0.402、Node.js 26.2.0、npm 11.13.0。

### 一条命令启动本地预览（Windows PowerShell）

在项目根目录执行：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Start-Local.ps1
```

脚本首次运行时安装缺少的前端依赖，然后构建并启动网站。等待终端出现 `Now listening on` 后，打开：

- 首页：<http://127.0.0.1:5144>
- 注册：<http://127.0.0.1:5144/register>
- 管理员：<http://127.0.0.1:5144/admin/login>
- 员工：<http://127.0.0.1:5144/staff/login>

保持终端窗口运行，按 Ctrl+C 停止。更换端口可加 `-Port 5145`；源码未变化时可加 `-NoBuild` 跳过构建。

预览使用独立 SQLite 数据库 `artifacts/local-preview/preview.db`，文件保存在该目录的 `uploads`；再次启动会保留已有本地数据。这些运行数据不会提交 Git。邮箱验证和重置邮件在本地捕获，保存在 `CoLearnX.Server/App_Data/mail`，不发送真实邮件。PayPal 付款需要另行配置。

演示账户信息在 `CoLearnX.Server/Data/SeedData.cs` 的 `MemberEmail`、`TrainerEmail`、`CreatorEmail`、`AdminEmail` 和 `DemoPassword` 常量中。预览会生成演示管理员及会员账户；员工请由管理员进入 Staff 创建，然后使用页面一次性显示的初始密码登录。

### 开发模式（修改代码时使用）

先在前端目录安装依赖，再从根目录运行：

```powershell
cd colearnx.client
npm ci
cd ..
dotnet run --project .\CoLearnX.Server --launch-profile https
```

开发模式使用前端 Vite 和 ASP.NET Core，前端地址为 <https://localhost:55128>，后端地址为 <https://localhost:7238>。首次启动需按本机提示配置并信任 .NET 开发证书。也可用 Visual Studio 打开 `CoLearnX.slnx` 并启动 `CoLearnX.Server`。

### 获取此版本

```powershell
git clone --branch 20261104_v14 --single-branch https://github.com/2769824634/CoLearnX.git
cd CoLearnX
powershell -ExecutionPolicy Bypass -File .\scripts\Start-Local.ps1
```
