# 通知、业务邮件和推荐演示数据修改记录

完成日期：2026-10-01。工作目录：`C:\Users\user\Documents\CoLearnX\.local-deploy\20260929_v7`。

## 修改内容

| 要求 | 对应修改与接入 |
| --- | --- |
| 5. 通知跳到 Intake | Notification 增加可空 `IntakeId`；预约满员、空位释放、达到最低人数、临近截止、成班、取消、退出、延期通知写入关联 ID。NotificationService 生成 `/trainer/courses/intakes/{id}` 或 `/creator/courses/intake-applications/{id}`；前端仅放行固定路径和严格的正整数详情路径。旧通知无 ID 时仍回到原列表。 |
| 6. 取消及延期说明 | 人数不足释放积分后，消息明确说明等待最多 7 天、具体 UTC 截止时间、延期班发布后在 My Programs 重新选场次预约、等待期间积分可用。`N-class-cancelled` 和实际使用的 `N-postponement-offered` 均跳 My Programs。 |
| 7. 业务邮件 | 复用 PasswordResetMailSender 的 SMTP/本地邮件通道；成班、整班取消、最低人数不足释放、开课提醒、延期邀请在写站内通知时标记待发。事务提交后由既有后台扫描发送，尊重 EmailNotifications 偏好；发送失败保留重试，不回滚结算。 |
| 8. 演示数据 | Huang 的旧演示目标规范为 `professional`，预置 UX + Cybersecurity；初始配置标记已完成引导。2051、2002 各 3 条评分，分别为 5/4/5 和 4/5/4，来自明确标注的 Demo Reviewer，关联 Completed Enrollment。重复启动不增加重复评分，不覆盖用户后来修改的目标或兴趣。 |

Creator 的历史班次可能没有申请记录。详情接口在确认课程所有权后，允许返回 `application: null` 和真实 `currentIntake`，页面显示只读日程和当前状态；普通审核记录及审核操作保持原有行为。

## 文件入口

- 通知持久化与路由：`Domain/Entities/Entities.cs`、`Contracts/Dtos/NotificationDtos.cs`、`Services/NotificationService.cs`、`Data/NotificationSchema.cs`。
- 业务触发：`Services/EnrollmentService.cs`、`Services/IntakeSettlementService.cs`、`Services/CourseIntakeService.cs`。
- 邮件：`Services/NotificationEmailDispatcher.cs`、`Services/PasswordResetMailSender.cs`、`Services/AccountMail.cs`、`Program.cs`。
- 种子及启动升级：`Data/RecommendationSeed.cs`、`Data/SeedData.cs`。
- 前端：`colearnx.client/src/components/memberNotificationsState.js`、`pages/creator/CreatorIntakeApplicationDetailPage.jsx`。
- 新增测试：`IntakeNotificationTests.cs`、`NotificationEmailTests.cs`、`NotificationSchemaTests.cs`、`RecommendationDemoSeedTests.cs`、`CreatorIntakeNotification.test.jsx`；补充通知组件测试，并让既有推荐测试明确按 Huang 查询完成记录。

## 邮件配置与运行方式

沿用现有 `PasswordReset` 配置：`SmtpHost`、`SmtpPort`、`SmtpUsername`、`SmtpPassword`、`FromAddress`、`ClientBaseUrl`；Development 如需真实 SMTP，设置 `DeliveryMode=Smtp`。`ClientBaseUrl` 应为用户能访问的前端地址，邮件链接使用该地址加既有业务路径。

后台启动时和之后每分钟处理通知邮件，每批最多 50 条；失败约五分钟后重试，单封发送最多 30 秒。Testing 和默认 Development 将邮件捕获为 `App_Data/mail/*.eml`。没有可用通道时保留待发状态；关闭邮件偏好的用户仍收到站内通知。历史通知升级后默认不补发邮件。

发送器采用数据库领取和成功标记来避免正常扫描重复发送。SMTP 与数据库不是同一个事务：如果邮件已送达、但进程在写成功标记前崩溃，重试可能重复，不能承诺严格 exactly-once。邮件失败不会撤销 Hold/Capture/Release/Refund。

`NotificationSchema.EnsureAsync` 已在启动 SeedData 流程接入，兼容现有 SQLite 和 SQL Server 的缺失列升级。SQLite 重复升级保留旧消息且不补发历史邮件已有测试；本轮未连接真实 SQL Server。

## 验证结果

- 后端相关回归 **88 通过，0 失败**：通知、推荐、预留积分、结算边界、延期、Creator 审核、密码重置、账号验证。
- 最后补强邮件领取时间及重复报名后的 seed 幂等性后，对应邮件/seed **8 项再次通过**。
- 前端 **75 通过**：19 个 Node 测试、56 个 UI 测试；末次路径校验修改后相关 11 项再次通过。
- 前端生产构建通过；修改前端文件定向 ESLint 通过。
- 全项目 ESLint 保留既有 6 个错误和 1 个警告，涉及 PayPalPackageButtons、UserAvatar、LoginPage、RoleApplicationsPanel、AdminLoginPage，均非本次改动。
- 后端仍提示既有 Microsoft.OpenApi 2.0.0 / SQLitePCLRaw.lib.e_sqlite3 2.1.11 的 NU1903 警告。
- 本地独立测试数据库浏览器确认 Professional、UX/Cybersecurity、目录 4.7★(3 ratings) 与 4.3★(3 ratings)。邮件真实发送器已验证本地 `.eml` 捕获、配置链接及 HTML 编码；未向外部邮箱发送测试邮件。
- 实际点击 Trainer 和 Creator 的取消通知，分别进入 Intake #4 的对应详情；均显示 Cancelled。Creator 历史班次无申请记录时成功显示只读日程，浏览器控制台无错误。

回归命令（v7 根目录）：

```powershell
$env:Logging__EventLog__LogLevel__Default='None'
$env:Logging__LogLevel__Default='Critical'
dotnet test CoLearnX.Server.Tests/CoLearnX.Server.Tests.csproj --no-restore -p:SkipSpaPublish=true -p:UseAppHost=false --filter 'FullyQualifiedName~Notification|FullyQualifiedName~Recommendation|FullyQualifiedName~CreditReservationWorkflowTests|FullyQualifiedName~IntakeSettlementBoundaryTests|FullyQualifiedName~IntakePostponementTests|FullyQualifiedName~B4WorkflowIntegrationTests|FullyQualifiedName~PasswordReset|FullyQualifiedName~EmailVerification' --logger 'console;verbosity=minimal'
```

前端目录执行 `npm.cmd test`、`npm.cmd run build`、`npm.cmd run lint`。这些验证不代表已发布生产环境。

现有五门种子课的日期和级别仍按原数据；Huang 已完成 Frontend 课程后，推荐按已完成课程的标签与级别筛选。没有开放且匹配的课程时首页仍显示空状态，不为展示评分而绕过推荐规则。

两门课程的种子评分（截图或证据保留本地，未随源码发布）

Creator 取消通知进入具体 Intake（截图或证据保留本地，未随源码发布）
