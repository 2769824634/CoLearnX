# v7 首轮测试问题修复与验证记录

本文保留 2026-10-02 修复阶段的历史快照，“未提交”及测试数量均指记录生成时。当前逐项证据见 [2026-10-03 复测](20261003-v7-public-retest.md)，上传前检查见 [发布验证记录](20261003-master-publish.md)。

日期：2026-10-02，Asia/Shanghai。本文对应用户要求“按目前存在的所有问题，在 v7 版本中进行修改完善”，以首轮 MD/PDF 的 79 个问题编号为范围。

## 结果与修改基线

79 个编号已逐项处理：**65 项本轮修复并完成相应本地验证，7 项原已实现并完成本轮回归，3 项本地改善但需外部核对，4 项保留环境、数据或规则核对边界**。这份记录证明本地实现与检查结果，不能作为线上 79 项全部关闭的结论。

修改目录：`C:/Users/user/Documents/CoLearnX/.local-deploy/20260929_v7`。HEAD 为 `b4ad6c60a6098af68c8c97c11793b3238a6a7d60`；本轮从该目录已有的未提交 v7 实现继续，未使用 HEAD 覆盖原有功能。修改前的 348 个 tracked/non-ignored untracked 文件已保存到 `C:/Users/user/Documents/CoLearnX/.local-deploy/20261002_v7_before_first_round_fixes_083232`，附 `snapshot-manifest.json` 和 `git-status-before.txt`。本轮文件变化以该快照为基线。

最终比对为 85 个文件修改、21 个文件新增、0 删除，包含实现、测试和验证文档。变更文件及修改前后 SHA-256 记录在 change-manifest.json（截图或证据保留本地，未随源码发布）；快照中的 348 个文件哈希全部核验成功。79 个编号与原逐项台账集合完全相同，无漏项、重复或新增编号。

父目录 `C:/Users/user/Documents/CoLearnX` 的既有工作没有切换、清理、合并或覆盖。未 stage、commit、push、部署，也未操作线上密码、支付、积分、审批、退款或数据。前后端浏览器预览使用本轮独立 SQLite 数据库，检查后已停止预览进程并恢复浏览器视口。

核查来源为此前的 [MD + PDF 核查记录](20261002-first-round-issues-code-audit.md)。原 PDF 共 72 页，37 张精选截图，52 个测试用例、79 个不同问题编号；文档里的历史测试安排没有作为本轮指令。本文保留关联复现的 E 组编号，但不将其视为不同根因。

|本轮状态|编号数|含义|
|---|---:|---|
|本轮修复（本地）|65|已修改代码，并纳入相应自动验证或浏览器检查；仍需部署后的目标环境验收。|
|原已实现，本轮回归|7|保留原有实现，运行相关回归，没有重复制造相同功能。|
|本地改善，待外部核对|3|修复展示、来源或 MIME 等代码问题，原邮件客户端、课程内容或历史所有权仍需核对。|
|环境/数据/规则待核对|4|没有足够事实安全地改写历史证书、资源关联、邮件信誉或班次完成规则。|

## 关键行为变化

普通 Intake 草稿、材料版本和争议创建的显式事务均放入 EF 执行策略。重放采用稳定标识或已有记录，提交后响应丢失时不重复创建实体和审计。上传保存文件后若数据库明确没有引用，会补偿删除；若记录已提交或数据库结果不能确认，保留文件并记录日志，避免删除有效材料。

全额争议退款同时释放实体场次占用座位；部分退款保留座位和报名状态，线上场次的容量为 0 时不产生虚假座位错误。积分与退款的写失败显示“结果尚未确认”，要求先检查当前记录，保留幂等 key。退款检查后原争议消失时，不自动选中下一条并沿用旧理由，必须重新选案，理由与退款金额重新初始化。

Trainer 和 Member 资格统计统一按全部班次场次、全部考核计算。两次课、仅一次 Present、一个未评分考核的共同样本，两端都是 **50% 出勤、0/1 通过**；Trainer 另显示已评分数量。资格卡展示进度、具体考核、分数、通过线与未评分状态，没有放宽发证条件。

目录、课程详情、报名和审批分别显示真实 Creator 与实际 Intake Trainer；完整展示简介、级别、学习路径、学习成果、授课模式和 UTC 起止日期。缺课程成果或附件元数据时明确显示缺失，未填入虚构数据。Creator 待审/发布后的课程定义完整只读；已发布课程不能通过误导性提示获得不存在的修改流程。

Admin 增加真实用户查询页和接口，包含没有账本记录的用户，支持跳到指定用户账本。积分调整显示可用/保留余额和调整前后预览；理由、整数、范围与非负结果均有检查。审批材料显示课程上下文、文件名、大小和提交时间；历史缺失元数据仍保留审批记录并说明不可用。

登录页不再公开或预填管理员凭据。Production 不创建共享演示 Admin，并禁用仍使用共享演示密码的既有种子 Admin、更新 SessionStamp；已轮换密码的账户不被覆盖。Production 管理员登录也拒绝该共享密码。此代码行为没有替代线上密码轮换或安全 Admin 账号配置。

账号注册/重置补具体密码规则、掩码邮箱、等待与垃圾箱提示、已验证登录说明、一次性 token 的填写前检查和显示/隐藏密码。邮件创建显式使用 UTF-8 的 text/plain + text/html 两个 MIME alternative，HTML 在后。拾取到实际 `.eml` 的回归通过；原 SMTP、手机邮件 App 与应用内浏览器尚未复测。

## 证据索引

以下简称只用于逐项表定位代码与测试。源码路径均以 v7 根目录为起点。

|简称|实现与自动证据|
|---|---|
|SEC|`Services/AdminAuthService.cs`、`Data/SeedData.cs`、`Program.cs`；`FirstRoundSecurityMailTests.cs`。|
|ACC|登录、注册、验证、找回/重置、账户页、角色申请、权限壳与头像；`RegisterPage.test.jsx`、`PasswordRecovery.test.jsx`、`LoginPage.test.jsx`、`RequireAuth.test.jsx`、`RoleShell.test.jsx`、`UserAvatar.test.jsx`、`AdminLoginPage.test.jsx`。|
|MAIL|`Services/AccountMail.cs`、两类邮件发送器；`FirstRoundSecurityMailTests.cs`、`AccountMailTests.cs`、`NotificationEmailTests.cs`。|
|TX|`CourseIntakeService.cs`、`MaterialVersionService.cs`、`AdminFinanceService.cs`；`FirstRoundRetryStrategyTests.cs` 的三个真实提交后 ACK 丢失回归。|
|STORE|`AppServices.cs` MaterialService、四个 storage 实现；`FirstRoundPresentationRegressionTests.cs` 的失败清理/已提交保留文件两例。|
|COURSE|`AppServices.cs` CourseService、`EnrollmentService.cs`、`AdminCourseReviewService.cs` 与扩展 DTO；`FirstRoundPresentationRegressionTests.cs`、`FirstRoundBusiness.test.jsx`、`MemberCourseDetailPage.test.jsx`。|
|MEMBER|首页、目录、详情、Programs、Badges、Disputes；`FirstRoundBusiness.test.jsx`、现有 Member 页回归。|
|STATS|`TrainerLaterPhaseService.cs`、`CertificateWorkflowService.cs` 及 DTO；`LaterPhaseWorkflowIntegrationTests.cs`、`FirstRoundPresentationRegressionTests.cs`、`FirstRoundBusiness.test.jsx`。|
|CREATOR|课程只读、课程列表、上传、Usage、Intake 申请；`MaterialUsageSchema.cs`、`MaterialVersionService.cs`；`FirstRoundBusiness.test.jsx`、`CreatorUsagePage.test.jsx`、旧表升级回归。|
|ADMIN|`AdminController.cs` 用户查询、Admin Users/账本/争议/审批页；`FirstRoundPresentationRegressionTests.cs`、`AdminFirstRound.test.jsx`、`AdminAttachmentReview.test.jsx`、`AdminFinancialRegression.test.jsx`。|
|FIN|`AdminFinanceService.cs` 退款/失败审计；`LaterPhaseWorkflowIntegrationTests.cs`、`FirstRoundRetryStrategyTests.cs`、Admin 财务回归。|
|ERROR|`ApiError.TraceId`、LaterPhase/Trainer 错误过滤器、报名/上传控制器、`api/client.js`、共享业务提示；`tests/api-client.test.js`、上述 UI 错误分支。|
|UI|账户/Creator/Admin CSS、MemberShell/RoleShell、共享 Modal；`Modal.test.jsx`、`RoleShell.test.jsx`、浏览器 390/768/1440 和真实键盘复核。|

服务端完整路径为 `CoLearnX.Server/Services/...`、`CoLearnX.Server/Controllers/...`、`CoLearnX.Server/Data/...`；服务端测试位于 `CoLearnX.Server.Tests`；前端实现/测试位于 `colearnx.client/src` 与 `colearnx.client/tests`。各模块职责和接口路径保持原有合同；新增 DTO 字段为扩展，Trainer/Creator/Admin 的审批权限没有调换。

## 79 个问题逐项结果

|编号|本轮状态|处理结果及保留边界|证据|
|---|---|---|---|
|S01-F01|本轮修复（本地）|移除公开/预填 Admin 凭据，Production 阻断共享演示密码并禁用未轮换种子账号；线上轮换仍需部署环境处理。|SEC、ACC|
|S01-UX01|本轮修复（本地）|默认标题改为中性 CoLearnX，角色页显示其实际标题。|ACC、CREATOR|
|A01-UX01|本轮修复（本地）|弱密码提交列出具体未满足条件，复用密码规则。|ACC|
|A01-UX02|本轮修复（本地）|密码和确认栏变化清除旧错误，避免过时提示。|ACC|
|A02-UX01|本轮修复（本地）|注册成功展示掩码邮箱及验证邮件的等待/垃圾箱说明。|ACC|
|MAIL-F01|本地改善，待外部核对|邮件统一 UTF-8、plain/HTML alternative，真实序列化回归通过；原手机邮件 App、SMTP 投递未核对。|MAIL|
|MAIL-UX01|本轮修复（本地）|注册与找回页面补等待、垃圾箱及一次性链接提示。|ACC、MAIL|
|A04-UX01|本轮修复（本地）|失效/已用验证链接提示若已验证可直接登录，保留一次性校验。|ACC|
|A05-UX01|本轮修复（本地）|单一身份直接进入，多个身份继续使用工作区选择弹窗。|ACC、UI|
|A10-UX01|本轮修复（本地）|申请状态加载时不显示假 Not requested，也不允许重复 Apply。|ACC|
|A10-UX02|本轮修复（本地）|申请及已审时间明确 UTC，表头标时区。|ADMIN|
|A11-UX01|本轮修复（本地）|权限跳转显示原因并可关闭，下一次导航消耗旧提示。|ACC、UI|
|A12-UX01|本轮修复（本地）|找回密码提交后提示等待与检查垃圾箱，不泄露账号是否存在。|ACC|
|A13-UX01|本轮修复（本地）|重置密码两栏有稳定显示/隐藏控制；原手机自动填充行为仍需设备复测。|ACC|
|A13-UX02|本轮修复（本地）|受保护请求 401 改为中性过期说明，不一律断言其他设备登录。|ACC、ERROR|
|A13-UX03|本轮修复（本地）|填写前检查 token；已用/过期阻断，检查网络失败允许重试，检查本身不消耗 token。|ACC、Presentation 测试|
|MAIL-OBS02|环境/数据/规则待核对|收件信誉、垃圾邮件分类及应用内浏览器表现依赖原投递与客户端，未捏造已关闭。|邮件外部验收|
|M01-UX01|本轮修复（本地）|首页等待成功数据再显示统计或真实空态，失败给重试入口。|MEMBER|
|M01-OBS01|环境/数据/规则待核对|四张预置演示证书和手设进度来源已定位；没有擅自删历史证书或重新发证。|SeedData、浏览器|
|M02-UX01|本轮修复（本地）|无筛选匹配时有明确说明和清除筛选入口。|MEMBER|
|M03-F01|本轮修复（本地）|详情补简介、级别、路径、授课模式、Creator 和实际 Trainer。|COURSE、MEMBER|
|M03-F02|本地改善，待外部核对|缺学习成果时明确显示未提供；4010 权威课程内容仍待确认，没有编造补齐。|COURSE、MEMBER|
|M03-F03|本轮修复（本地）|场次完整显示开始/结束日期及 UTC，跨日不再只有结束时刻。|COURSE、MEMBER|
|M03-UX01|本轮修复（本地）|课程详情恢复合理内容宽度，学习成果和场次不再被挤入固定窄栏。|MEMBER、UI|
|M03-OBS01|本轮修复（本地）|移除固定 Senior Trainer/Certificate included，显示实际身份与证书申请条件。|COURSE、MEMBER|
|M03-OBS02|原已实现，本轮回归|保留不可报名场次禁用，增加缺失/非法时间保守拒绝覆盖。|MemberCourseDetail、availability 测试|
|M04-UX01|原已实现，本轮回归|先判断报名窗口/场次状态，再判断余额；确认飞行中防重复提交。|MEMBER、报名回归|
|M04-F01|原已实现，本轮回归|保留报名执行策略事务、余额/座位条件更新及一次 Hold；SQL Server 仍待目标环境验收。|Enrollment API/生命周期测试|
|M04-F02|本轮修复（本地）|报名异常不返回原始 InvalidOperationException，显示业务说明和 Reference。|ERROR、MEMBER|
|M07-OBS01|环境/数据/规则待核对|旧材料/录播和 Intake 关联、手设进度需数据来源核对；没有凭空关联资源或把进度清零。|SeedData、Learning Hub|
|M07-UX01|本轮修复（本地）|资源刷新有稳定完成反馈，更新后仍无资源会明确说明。|MEMBER|
|M08-UX01|本轮修复（本地）|Programs 标签、选中报名与课程写入 URL，刷新和深链接保留选择。|MEMBER、浏览器|
|M09-UX01|本轮修复（本地）|资格卡显示当前进度、所有考核、分数、通过线与未评分状态。|STATS、MEMBER|
|M12-UX01|本轮修复（本地）|头像入口常驻 PNG/JPEG/WebP、2 MB 限制说明。|ACC|
|M13-UX01|本轮修复（本地）|无可争议报名时说明前置条件；Reserved 排除并指向保留报名取消流程。|MEMBER|
|M13-OBS01|原已实现，本轮回归|保留 Programs 学习中心的争议入口和 enrollmentId 深链接。|MEMBER|
|M13-F01|本轮修复（本地）|争议创建整段在执行策略内，重放复用已建 Open 争议。|TX、FIN|
|M13-UX02|本轮修复（本地）|争议写失败显示结果未确认及 Reference，不展示 SQL/事务异常。|ERROR、MEMBER|
|T01-OBS01|环境/数据/规则待核对|到开始时间的结算规则保留；班次 EndsAt 后自动 Completed 的合同尚未明确，不能代替学员人工完成/发证流程。|IntakeSettlementService、生命周期合同|
|T02-F01|本轮修复（本地）|普通草稿创建使用执行策略与稳定 Version，ACK 丢失重放只创建一次。|TX|
|T02-UX01|本轮修复（本地）|Trainer 日期错误映射可读字段，未知写失败提供恢复说明与 Reference。|ERROR、Trainer UI|
|T05-UX01|本轮修复（本地）|出勤选择显示课程代码/名称、Intake 和完整 UTC 场次日期。|STATS、浏览器|
|T06-F01|本轮修复（本地）|Trainer/Member 使用相同全部场次/考核分母；共同样本均为 50% 和 0/1。|STATS、浏览器|
|C01-UX01|本轮修复（本地）|待审/发布课程完整保留只读定义；Published 提示不承诺不存在的编辑路径。|CREATOR、浏览器|
|C01-UX02|本轮修复（本地）|Creator 加载与保存错误区分，遮蔽技术码，标题展示实际课程。|CREATOR、ERROR|
|C01-UX03|本轮修复（本地）|代码列可换行，避免与标题重叠，1440px 实际检查无页面横向溢出。|CREATOR、UI|
|C02-F01|本轮修复（本地）|PendingApproval/Published 共用的材料版本事务纳入执行策略，加入存储补偿与已提交保留。|TX、STORE|
|C02-UX01|本轮修复（本地）|上传失败提示业务限制、未确认结果和 Reference，不透传实现异常。|ERROR、CREATOR|
|C03-UX01|本轮修复（本地）|新使用事件记录 IntakeId；DTO/UI 显示 Intake/起止时间；旧未知记录保留 null。|CREATOR、旧表升级测试|
|C03-UX02|本轮修复（本地）|区分无使用库与筛选无匹配，并提供清除筛选。|CREATOR|
|C03-OBS01|本轮修复（本地）|Usage 筛选和展示统一明确 UTC，页标题与功能一致。|CREATOR|
|C04-UX01|本轮修复（本地）|0 待审不显示 Action required，加载失败不伪装空队列。|CREATOR|
|C05-UX01|本轮修复（本地）|账户产品文案移除 modal/store 实现术语，说明申请用途。|ACC|
|C05-OBS01|本轮修复（本地）|明确 Full name 与 Display name 的用途，保留合法姓名/显示名字段差异。|ACC|
|D01-UX01|本轮修复（本地）|附件按钮按对应路径显示；await 下载并展示忙/启动/失败，不声称文件已打开。|ADMIN、ERROR|
|D02-UX01|本轮修复（本地）|课程审批 DTO/UI 提供完整定义、学习成果和学习路径。|COURSE、ADMIN|
|D02-UX02|本轮修复（本地）|Creator 与实际 Intake Trainer 分开，Enrollment 也使用实际授课者。|COURSE、浏览器|
|D02-UX03|原已实现，本轮回归|没有可报名场次时保持禁用，条件说明和回归继续通过。|MEMBER|
|D02-OBS01|本地改善，待外部核对|提交人投影改用 CreatorId/Creator；历史线上课程所有权仍需数据核对。|COURSE、Presentation 测试|
|D03-UX01|本轮修复（本地）|审批材料显示课程名/状态、文件名/真实大小和 UTC；缺失值逐项说明。文件名是存储下载名，原始上传名未追溯。|ADMIN、MaterialVersionService|
|D05-UX01|本轮修复（本地）|用户查询覆盖无账本账户，显示可用/保留钱包与调整前后预览。|ADMIN|
|D05-F01|本轮修复（本地）|/admin/users 连接真实查询页和 Admin-only API，不再重定向角色审批。|ADMIN、浏览器|
|D05-F02|原已实现，本轮回归|调整的执行策略、幂等冲突和一次流水/审计继续通过；没有声称 SQL Server 已验收。|FIN、生命周期回归|
|D05-UX02|本轮修复（本地）|写失败与加载失败分开，先检查记录、保留幂等 key，不再声称没有任何变化。|ADMIN、ERROR|
|D05-OBS02|本轮修复（本地）|确定业务拒绝追加 Failed 审计；数据库未知结果记录追踪日志，不虚构成功/失败结论。|FIN、ERROR|
|D06-UX01|本轮修复（本地）|争议卡展示报名、Intake、Session、完整 UTC 日期与报名状态。|FIN、ADMIN|
|D06-F01|本轮修复（本地）|全额退款原子释放物理座位；部分退款不释放；重复请求不二次流水/退款。|FIN|
|D06-UX02|本轮修复（本地）|未确认退款先刷新核对；原争议消失时不自动接续下一案，清空旧理由与金额。|ADMIN、财务专项回归|
|D06-OBS02|本轮修复（本地）|退款/拒绝的确定业务失败留 Failed 审计，未知异常保留 trace/server log。|FIN、ERROR|
|E01-F01|本轮修复（本地）|关联 T02-F01，所有账号/课程共用的草稿事务路径已修复。|TX|
|E01-UX01|原已实现，本轮回归|关联无场次禁用行为，真实角色展示另由 D02-UX02 修复。|MEMBER、COURSE|
|E01-UX02|本轮修复（本地）|关联 Trainer 写失败文案与 Reference。|ERROR|
|E02-F01|本轮修复（本地）|关联 C02-F01，Published 上传也走相同修复路径。|TX、STORE|
|E02-UX01|本轮修复（本地）|关联上传安全错误和恢复提示。|ERROR、CREATOR|
|U01-F01|本轮修复（本地）|去掉账户内联双栏，900px 下单栏和按钮换行；390px 文档宽度为 390。|UI、浏览器|
|U01-F02|本轮修复（本地）|768px 审批栏两列完整显示，修复绝对定位辅助标签造成的额外溢出；文档宽度为 768。|UI、浏览器|
|U01-OBS01|本轮修复（本地）|窄屏壳增加更多导航/横向滚动提示和 aria-describedby；真实手机触摸尚未操作。|UI、浏览器|
|U02-F01|本轮修复（本地）|Modal 补 dialog/aria-modal、自动聚焦、Tab/ShiftTab 循环与关闭后回到触发按钮。|UI、Modal 测试、浏览器键盘|
|U02-F02|本轮修复（本地）|Escape 关闭顶层 Modal，嵌套弹窗不同时关闭。|UI、Modal 测试、浏览器键盘|

## 新鲜验证

### 自动检查

前端最后执行时间 2026-10-02 13:27（Asia/Shanghai），在 `v7/colearnx.client`：

```powershell
npm.cmd run lint
npm.cmd test
npm.cmd run build
```

三条命令均退出 0。ESLint 无错误/警告；Node 测试 **20/20**，Vitest UI **99/99（27 个文件）**，合计 **119/119**；Vite production build 成功。后续仅写验证文档，没有再修改前端应用代码。

后端最后执行时间 2026-10-02 13:35–13:36（Asia/Shanghai），在 v7 根目录：

```powershell
$env:Logging__EventLog__LogLevel__Default='None'
$env:Logging__LogLevel__Default='Critical'
dotnet test CoLearnX.Server.Tests/CoLearnX.Server.Tests.csproj --no-restore -p:SkipSpaPublish=true -p:UseAppHost=false --filter 'FullyQualifiedName!~Approve_Succeeds_WhenSqlServerRetryOnFailureIsEnabled' --logger 'console;verbosity=minimal' --results-directory artifacts/verification/20261002 --logger 'trx;LogFileName=backend-local.trx'
```

结果 **306/306 通过，0 失败，0 skipped，退出 0**。其中 Presentation 新回归 8 例包含旧 Usage 表重复升级、真实 Intake Trainer、reset token 无消耗验证、失败文件清理和提交后响应丢失保留文件；Security/Mail 4 例；执行策略 ACK 丢失 3 例。

过滤的唯一 SQL Server 专用测试 `Approve_Succeeds_WhenSqlServerRetryOnFailureIsEnabled` 先前在完整运行中因 SQL LocalDB 缺失、数据库连接 error 52 失败，尚未进入业务逻辑。没有把过滤计作 skipped 或通过，也未安装 SQL Server。三个 ACK 丢失回归使用 SQLite 真提交、自定义 EF ExecutionStrategyFactory 和 DbTransactionInterceptor，证明应用级重放/去重行为，不能证明 SQL Server provider 和线上连接已经通过。

首次完整后端运行 305 例中 4 例失败：两例旧角色期望、一例未解码 MIME 的原始正文断言、一例 LocalDB 缺失。前三例按已生效的 Member+Trainer/Creator 角色合同和实际编码正文修正后，旧完整本地集 304 例通过；最后增加两例实质边界回归后为本次 306 例。原诊断保存在 `artifacts/verification/20261002/backend-full.trx`，最终结果为 `backend-local.trx`。

构建仍提示既有 NU1903：`Microsoft.OpenApi 2.0.0`、`SQLitePCLRaw.lib.e_sqlite3 2.1.11`。本轮没有将依赖升级混入 PDF 问题修复；这两项需另做兼容性验证和依赖修复。`git diff --check` 退出 0；LF/CRLF 提示不属于空白错误。

### 先失败、后修复的行为证据

目标回归先暴露了课程/Creator 投影、实际 Intake Trainer、用户查询、资格明细、reset status、上传补偿、Production 演示密码及邮件 MIME 顺序等缺口。业务 UI 首组 11 个目标行为失败；附件审批首组 8 个目标行为失败；财务未知结果与用户预览、报名重复确认也有失败证据，随后修复通过。

收尾新增的“原争议消失后自动选下一案并沿用理由”回归先失败，修复后通过；旧 Usage 表升级和已提交文件保留的两个边界回归随后通过。测试关注用户行为与重要副作用，没有为单纯文案和低影响 CSS 添加镜像测试。审查/简化仅在行为绿灯后进行，未重构本轮范围外的付款或角色流程。

主任务完成最终正确性、可读性、架构、安全、性能和可用性复审：重点检查重试后的实体/审计去重、上传未知结果补偿、用户查询的独立 Admin 身份校验、Production 演示密码防护、金融幂等 key 与选案、头像/异步数据的身份归属、Modal 键盘行为、课程角色语义和窄屏边界。没有将子任务未完成的最后复审称为独立审查通过；最终结论由主任务读取代码、执行检查并复核浏览器形成。

### 实际浏览器检查

使用 Chrome、本轮独立 Development SQLite fixture、HTTP loopback 预览。没有使用生产账号，没有通过浏览器执行积分、退款、审批、支付、上传、改密码或发证操作。涉及写入的正确性由 API/服务集成测试验证。

|检查|实际结果|
|---|---|
|Member 登录/权限|单身份直接进入 Member；访问 Creator 路由返回 Member 并显示原因，后续导航清除旧提示。|
|Member 390px 账户|viewport/document 均 390，main client/scroll 均 375，单栏和按钮可用，更多导航提示可见。|
|Modal 键盘|Edit Profile 自动聚焦；Save 后 Tab 到 Close，ShiftTab 回 Save；Escape 关闭，焦点回 Edit Profile。|
|Programs 深链接|Completed 点击后 URL 保留 tab=completed；刷新仍处于 Completed；选课保留 enrollmentId/courseId。|
|Admin 登录/用户|登录栏不预填凭据；/admin/users 实际显示无账本样本用户，Review credits 到对应钱包预览。|
|Admin 768px 审批|四个入口两列显示，document=768；每列 343px，按钮均在视口内，无裁切/额外溢出。|
|课程/材料审批|课程完整 Creator/学习路径/学习成果预览；旧材料元数据缺失明确 unavailable，失效下载显示友好错误。|
|Trainer|多身份选择仍可用；出勤选项有课程和 UTC 时间；共同样本为进度 65%、出勤 50%、考核 0/1、已评分 0。|
|Creator 1440px|页面/document 均 1440；课程代码不越界；Published 课程完整只读定义。|
|Member 资格/目录|共同样本显示进度 65%、出勤 50%、0/1、未评分和通过线 60；目录 Creator/实际 Trainer 分开。|

截图：

Admin 768px 审批入口（截图或证据保留本地，未随源码发布）

Member 390px 账户布局（截图或证据保留本地，未随源码发布）

这轮是浏览器视口与键盘验证；没有把它称为真实手机触摸、屏幕阅读器、原邮件客户端、SMTP、PayPal 或线上 SQL 验收。预览期间日志中的多集合 Include 性能提示和缺 HTTPS 端口提示未阻断上述界面；HTTP 仅用于 loopback 测试，没有改生产 HTTPS 配置。

## 剩余验收与数据边界

1. **目标 SQL Server/线上版本**：需在目标 provider 上执行专用用例，并在实际部署后按原 PDF 的合法输入复验报名、草稿、PendingApproval/Published 材料、争议创建、积分调整及全/部分退款。当前没有确认线上部署 SHA 或运行本轮修改。
2. **邮件**：重新核对真实投递、原手机客户端、HTML/纯文本选择、一次性链接与垃圾邮件分类。MIME 序列化通过不能替代原客户端结果。
3. **历史内容与数据**：确认 4010 学习成果、课程 Creator 所有权、旧资源/录播的 Intake 关联、预置证书与进度来源。新 Usage 事件可定位 Intake，旧事件只保留未知，未猜测回填。
4. **班次完成规则**：确认 EndsAt 自动完成或由 Trainer 明确完成的业务合同，以及与学员进度/资格的关系；目前不任意自动把所有学员变成 100% 或发证。

本轮代码和证据已可审查。修改前快照、最终 TRX、响应丢失回归、前端测试与截图可用于下一轮验收；不需要通过 Git reset 或覆盖父工作区重现基线。
