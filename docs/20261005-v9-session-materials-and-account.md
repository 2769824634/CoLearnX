# CoLearnX 20261005_v9 功能与验收记录

日期：2026-10-05，Asia/Shanghai。基础版本：`20261005_v8`，基础提交：`de28a1275e78d4b931c6f76a7dce15aede7d9922`，包含 v8 当时尚未提交的全部源码增量。

## 本轮功能

- 单节课资料：在 `Course → CourseIntake → CourseSession` 层级下为具体 Session 上传和下载资料。Trainer 的 Intake 详情页提供一个独立的 `Session materials` 入口；Member 的 My Programs 学习中心按 Session 显示资料。Trainer 只能管理自己的课节；Member 只能通过本人 `Active` 或 `Completed` 报名记录访问该课节资料，`Reserved` 不开放学习中心。
  - 上传时填写资料标题并选择文件，支持 PDF、PPTX、DOCX、PNG、JPG/JPEG，单文件最大 20 MiB。
  - 下载保留格式扩展名；公开 DTO 不暴露文件存储路径。上传后的数据库保存失败会尝试删除已写入文件。
  - 已有资料的课节不能直接删除或修改结构；结构变更提交和 Creator 确认阶段都会重新检查，返回 `409 SESSION_HAS_MATERIALS`，保留资料及课节。
- 邮件密码重置：点击一次性邮件链接后只填写新密码和确认密码。账号由服务端 token 关联的用户确定，继续检查密码规则、过期、已用 token 与用户状态，并使旧登录会话失效。
- 密码显示：登录、注册、Admin 登录和重置表单提供独立的显示/隐藏按钮，初始隐藏，按钮不提交表单。
- 个人信息编辑：弹窗只在打开和关闭时处理初始焦点与焦点恢复；输入值变化时保持当前字段和光标。Trainer、Creator、Member 使用稳定布尔状态，Member 字段补齐 label 关联。

## 本地运行

在仓库根目录执行：

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Start-V9Preview.ps1
```

打开 `https://localhost:5099`。`-Port` 可指定其他端口，完成构建后可用 `-NoBuild` 启动。脚本采用 `Testing` 环境、独立 SQLite 数据库和资料存储，捕获邮件至本机，不加载开发者 user-secrets，也不运行定时成班 worker。预览种子账户及密码与现有 `SeedData` 保持一致；实际部署继续使用现有 SMTP、存储与数据库配置。

## 本轮验证

原 v8/v7 文档中的测试数量和浏览器结果属于历史记录。本轮日志、JSON 证据和截图保存在本地 `artifacts/verification/20261005-v9/`，该目录不属于公共源码交付。

| 检查 | 本轮结果与覆盖范围 |
| --- | --- |
| 后端全量测试 | 最终代码 346/346 通过，0 failed、0 skipped；运行前仅排除需要本机 SQL Server LocalDB 的 `Approve_Succeeds_WhenSqlServerRetryOnFailureIsEnabled` |
| 资料关联的结构变更保护 | 49/49 相关测试通过；真实上传后提交删除、待审核快照确认和直接删除均拒绝，资料及文件保持完整；上传的 `201 Location` 可以直接 GET |
| 前端 UI 测试 | 完整检查 39 个文件、166/166 通过；移除重复资料入口后，受影响的三个文件再次检查 19/19 通过 |
| Node 测试 | 22/22 通过 |
| ESLint、Vite build、服务器 build | 通过；Vite 仍有既有的大 chunk 提示，构建仍有 NuGet/npm 依赖安全告警，未在本轮升级依赖 |
| SQLite 旧库升级 | 从真实 v8 SQLite 文件建立只读备份副本；启动 v9 后自动建立资料表和索引，原 6 个用户、6 个课节数量保持不变 |
| 真实 HTTPS 资料 API | Trainer 上传 PNG 后，Trainer 与 Member 下载内容的 SHA-256 都与原文件一致；匿名下载 401，Member 调用 Trainer 上传接口 403 |
| 真实 HTTPS 密码 API | 捕获本地邮件链接后，不传邮箱重置成功 200；重复 token 400，旧会话 401，新密码登录 200 |
| Chrome 页面 | 登录与重置页显示/隐藏密码正确，重置页无邮箱字段且 token 从地址栏移除；Trainer 六个个人信息字段及 Member Bio 连续输入保持焦点；课节资料仅一个上传入口，Member 按课节显示资料 |

Chrome 自动选文件被浏览器扩展的 `Allow access to file URLs` 权限阻止，因此浏览器里的“选择文件 → 提交”完整上传操作尚未验收；HTTP 上传、服务端下载内容和页面列表已经实际验证。浏览器显示了 Trainer 的下载启动提示，但自动化没有收到下载事件，文件内容核对使用的是实际 HTTPS API。

SQL Server 新表 DDL 完成静态核对，本轮没有连接真实 SQL Server。预览邮件为本地捕获，没有验证外部 SMTP；文件存储使用本地目录，没有执行云存储故障注入。

可复现的自动化检查：

```powershell
dotnet test CoLearnX.Server.Tests/CoLearnX.Server.Tests.csproj --no-restore --filter "FullyQualifiedName!~Approve_Succeeds_WhenSqlServerRetryOnFailureIsEnabled"
cd colearnx.client
npm run test:ui -- --reporter=dot
npm run test:node
npm run lint
npm run build
```

## 交付范围

本版本在本地独立目录中开发，分支为 `codex/20261005-v9`。对照启动时保存的 v8 源码 SHA-256，391 个文件均未变化，v8 的 Git 状态和 HEAD 也保持不变。本轮增量为 40 个修改文件、11 个新增文件，详情保存在本地 `source-diff.json`；没有删除基线源码文件。源码和说明可审查；尚未 commit、push 或线上部署。
