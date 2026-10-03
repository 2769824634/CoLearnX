# v7 master 提交前验证记录

日期：2026-10-03，Asia/Shanghai。发布目标为 [2769824634/CoLearnX 的 master](https://github.com/2769824634/CoLearnX/tree/master)。本记录说明上传前执行的检查及提交范围，不表示目标服务器或外部支付、邮件已经验收。

## 比较基线与提交范围

发布准备开始时，`git fetch origin master` 成功，`origin/master...HEAD` 为 `0 0`，共同提交为 `b4ad6c60a6098af68c8c97c11793b3238a6a7d60`。本版把该基线之后多轮对话产生的本地功能与修复纳入提交：兴趣引导与推荐、完成评分、预约积分生命周期、开班/取消/延期、通知邮件、PDF 问题修复及对应测试。

提交包含 `CoLearnX.Server`、`CoLearnX.Server.Tests`、`colearnx.client` 的实际变更，README、代码概览、生命周期说明和 Markdown 验证记录。文件使用明确清单暂存；本地 SQLite、上传文件、捕获邮件、原始日志、截图、ZIP、旧 README 备份及私人配置不进入提交。原始证据保留本地 `artifacts/verification/`；公开文档使用仓库相对源码链接。

## 上传前再次执行的自动化

| 检查 | 时间 / 结果 |
| --- | --- |
| 后端 build + test | 20:03:45–20:04:37 +08:00；313/313 Passed，0 failed，0 skipped，退出 0 |
| 前端 Node | 20/20 passed |
| 前端 Vitest | 27 个测试文件、99/99 passed |
| ESLint | 退出 0 |
| Vite production build | 退出 0；120 modules；生成 dist 资源 |
| 前端检查记录 | 20:04:52 +08:00，test/lint/build 均退出 0 |

从仓库根目录运行后端：

```powershell
$env:Logging__EventLog__LogLevel__Default = 'None'
$env:Logging__LogLevel__Default = 'Warning'
dotnet test CoLearnX.Server.Tests/CoLearnX.Server.Tests.csproj --no-restore -p:SkipSpaPublish=true -p:UseAppHost=false --filter 'FullyQualifiedName!~Approve_Succeeds_WhenSqlServerRetryOnFailureIsEnabled'
```

从 `colearnx.client` 运行前端：

```powershell
npm.cmd test
npm.cmd run lint
npm.cmd run build
```

313 个后端用例不包含 `Approve_Succeeds_WhenSqlServerRetryOnFailureIsEnabled`。本机没有 SQL Server LocalDB，该专项明确排除，没有算作通过或 skipped。SQLite 集成测试和自定义 ACK 丢失重放用例不能替代真实 SQL Server 的 provider、事务隔离、连接重试和 schema 升级验收。

本轮构建仍报告既有 NU1903：Microsoft.OpenApi 2.0.0、SQLitePCLRaw.lib.e_sqlite3 2.1.11。本次上传没有升级这些依赖。

## 浏览器与 PDF 证据

原 PDF 79 个问题均有对应记录，见 [逐项复测文档](20261003-v7-public-retest.md)。分类口径为 65 项本地修复、7 项 PDF 修复前已实现回归、3 项本地改善待外部核对、4 项环境/数据/规则待核对；这不表示线上 79 项全部关闭。

同日下午在独立 SQLite 预览库中使用 Chrome 完成 42 条操作记录并保存 41 张截图；此次上传准备没有重复操作浏览器。用户先要求 Edge，之后明确允许使用可用浏览器，所以实际记录为 Chrome。

浏览器完整文件选择受扩展 file URL 权限阻断；一个参数化 API 测试的两个真实 HTTP 分支覆盖 PendingApproval/Published 的小 PNG 上传、审批元数据和下载字节。浏览器只预览 Admin 调整/退款，没有实际执行财务写操作；自动化验证相应事务。真实 SMTP、原手机邮件 App、生产存储、历史数据及 Intake 结束后自动 Completed 的合同仍保留限制。

## 发布文档检查

README 与新增/更新的验证文档不链接未提交的本机文件。公开逐项文档保留 79 个唯一编号、测试名称和各项验证限制；原 PDF、完整截图包和测试日志保留本地。提交前检查 `git diff --cached --check`，并逐一确认 Markdown 相对链接目标在 Git index 中存在。

代码上传与运行环境部署分开核对。本版没有触发仓库的手动 Azure 部署工作流；也没有操作线上密码、积分、退款或审批数据。
