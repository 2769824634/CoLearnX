# CoLearnX v7 — 本地功能增量与问题修复

本文记录 2026-09-23 至 2026-10-03 多轮开发在本地 v7 中实现的功能、首轮 PDF 问题修复、已有验证证据和运行方式。功能状态以当前代码为准；设计稿用于对照需求，PDF 用于对照问题与复测结果。

| 项目 | 当前状态 |
| --- | --- |
| 文档更新 | 2026-10-03，Asia/Shanghai |
| 本地修改目录 | `C:/Users/user/Documents/CoLearnX/.local-deploy/20260929_v7` |
| 对照仓库 | [2769824634/CoLearnX](https://github.com/2769824634/CoLearnX) |
| GitHub 默认分支 | `master`，2026-10-03 使用 `git ls-remote --symref origin HEAD` 实时核对 |
| 增量比较基线 | [b4ad6c60a6098af68c8c97c11793b3238a6a7d60](https://github.com/2769824634/CoLearnX/commit/b4ad6c60a6098af68c8c97c11793b3238a6a7d60)，2026-09-23 19:29:08 +08:00；发布准备开始时 GitHub 与本地 HEAD 一致 |
| 基线提交说明 | Fix Azure SQL admin review 500s by wrapping transactions in the retry strategy. |
| v7 交付范围 | 多轮功能、修复、测试和本文纳入本版 Git 提交，发布目标为 GitHub master；目标运行环境部署未验收 |
| 最近应用验证 | 后端 313/313；前端 Vitest 99/99 + Node 20/20；ESLint 和 Vite production build 通过 |
| PDF 复测 | 79 个问题编号全部建档，Chrome 实际调试，保存 41 张截图；各项证据范围见完整报告 |

GitHub 的提交已经包含 Member 证书/徽章、证书申请进度、站内通知、忘记密码，以及 Creator/Admin/Trainer 的基础业务链。v7 在这些基础上接通推荐、评分和预约资金生命周期，并加固跨角色写操作、数据投影、错误反馈和页面可用性。GitHub 源码状态不等同于线上部署状态；本文没有核验线上是否运行此版本。

旧 v5.0 README 和差异审计保留在本机 `artifacts/verification/20261003-readme-update/`。GitHub 随代码提供本文、相对路径的逐项复测文档和发布验证记录；数据库、捕获邮件、原始日志与截图 ZIP 保留本地。

## 1. 相对 GitHub，这些能力发生了什么变化

| 能力 | GitHub 基线已有内容 | 多轮对话后的 v7 增量 |
| --- | --- | --- |
| 角色与课程组织 | Member、Trainer、Creator，独立 Admin 身份；Course → CourseIntake → CourseSession | 保持角色契约；补单身份直达、多身份选择、越权跳转原因和更清楚的导航 |
| 兴趣与首页 | Interest/UserInterest 基础表；首页 Featured/目录 | 两级兴趣树、三步 onboarding、Skip、Account 编辑兴趣及规则推荐 |
| Creator 课程 | Draft、编辑、提交 Admin 审批、提交后锁定 | 课程叶子标签、提交前 1–4 标签约束、完整只读定义和审批预览 |
| 完成与评分 | ProgramRating 数据模型、进度/考核基础 | Trainer 完成课程动作、完成后 Member 评分、目录/详情/推荐评分汇总 |
| 课程报名 | 基础报名、积分扣款、场次与容量检查 | Reserved 预约、Hold/Capture/Release、可用/保留余额、事务重试与防重复副作用 |
| 成班与取消 | Trainer 排期、Creator 确认 Intake | 最低人数、报名截止批量结算、财务成班时间、整班取消、学员取消与退出 |
| 延期 | 没有完整的延期与重新预约链 | 人数不足后 7 天延期窗口、替代 Draft/Creator 确认、一次重新预约和原价保留 |
| 通知与邮件 | 站内通知、账号验证/重置邮件通道 | 通知定位具体 Intake、取消/延期说明、业务邮件偏好与提交后重试发送 |
| Member 学习中心 | 课程、资源、争议、证书与资格页面 | Reserved 入口限制、URL 保持选择、资源刷新结果、真实资格与考核明细 |
| Trainer 业务 | 创建 Intake、Session、出勤与考核 | 草稿创建重试防重、日程可读错误、出勤上下文和统一统计分母 |
| Admin 财务 | 调整、争议退款、基础幂等与审计 | 真实 Users 查询、无账本用户、钱包预览、失败审计、未知结果追踪和退款座位规则 |
| 材料与界面 | 上传/审批/使用记录，基础 Modal | 上传事务与存储补偿、审批元数据、Usage Intake/UTC、窄屏布局和键盘焦点管理 |

证书签发、材料审核、PayPal 充值和基础身份审批在 GitHub 已经存在。本次对这些模块的工作主要是接入新状态、补齐数据与反馈，以及修复具体问题。

## 2. 新接通的功能与规则

### 2.1 兴趣引导、推荐和完成后评分

预置兴趣词表包含 **15 个大类、105 个叶子兴趣**。Member 首次进入时依次选择 Hobby/Professional、大类和叶子，最多保存 8 个叶子；可以 Skip，之后从 Account 的 Edit interests 重新编辑。前端非 Skip 流程要求至少选择一个叶子，后端 API 当前允许空兴趣集合。学习目标会保存，但不参与推荐权重。

Creator 使用同一棵兴趣树设置课程标签。Draft 可以先保存为空标签，提交审批前必须选 **1–4 个有效叶子**；后端拒绝父节点、重复、失效和超限输入。这个时序保留了先建草稿再补定义的流程。

| 推荐模式 | 当前实现 |
| --- | --- |
| coldStart | 没有 Completed 课程时推荐可报名的 Beginner；有兴趣时至少匹配一个叶子，没有兴趣（包括 Skip）时允许所有符合候选条件的开放 Beginner |
| nextLevel | 以最近完成课程的叶子为锚点，进度 100% 且该 Intake 的全部 assessment 通过后，从 Beginner/Intermediate 推荐下一级 |
| sameLevel | 完成记录尚未满足升学资格时，沿锚点课程叶子推荐同级 |
| mastery | Advanced 或没有可报名的更高阶段时推荐同级巩固课程 |

候选必须是 Published 课程，具备开放报名窗口和可报名 Session，课程标签为 1–4 个；线下检查席位和 PhysicalBookingDeadline，线上容量 0 表示不限。用户已有 Active、Reserved、Completed 的课程被排除，最多返回 8 门。

候选先经过开放报名窗口和可报名 Session 过滤。排序使用 Bayesian 星级均值（m=5、C=3）和叶子 Jaccard 重合度，权重分别为 0.55 和 0.30；代码保留的开放班次项 +0.15 对所有入选候选都是常量，不产生候选间的排序差异。同分按 CourseId 稳定排序。**评分影响排序，assessment 与进度决定升学资格。**没有符合条件的班次时显示真实空态。

Trainer 可以完成自己 Intake 中的 Active enrollment；存在 assessment 时必须全部通过。完成后状态为 Completed、进度为 100%。该 Member 才能提交 1–5 星、最多 500 字评论；同一用户同一课程再次评分更新已有记录。证书继续走既有申请和审核链。

本地种子补齐五门演示课程标签、Huang 的 Professional/UX/Cybersecurity profile，以及 INFT 2051、INFT 2002 各三条明确标注的 Demo Reviewer 评分。重复启动不重复添加评分，也不覆盖用户后来修改的兴趣与目标。

主要代码与测试：[RecommendationService](CoLearnX.Server/Services/RecommendationService.cs)、[RecommendationSeed](CoLearnX.Server/Data/RecommendationSeed.cs)、[MemberOnboardingPage](colearnx.client/src/pages/member/MemberOnboardingPage.jsx)、[RecommendationWorkflowTests](CoLearnX.Server.Tests/RecommendationWorkflowTests.cs)、[RecommendationDemoSeedTests](CoLearnX.Server.Tests/RecommendationDemoSeedTests.cs)。

### 2.2 预约积分、最低人数与整班结算

积分充值仍沿用既有 PayPal/钱包。v7 改变了报名后的积分处理：先预约并保留积分，达到成班条件后才正式扣除。

| 动作 | Enrollment 状态与资金效果 |
| --- | --- |
| 预约 | 创建 Reserved；Hold 从可用余额转到保留余额 |
| 成班 | Capture 消耗保留积分，Reserved → Active |
| 未成班或确认前取消预约 | Release 退回全部保留积分，预约变为 Cancelled |
| Trainer 整班取消 | Reserved 全额 Release；Active 全额 Refund |
| 符合窗口的 Active 自行退出 | 退回 70%，其余记 Forfeit，钱包不额外扣一次 |

例如，初始可用/保留余额为 120/0，预约 35 credits 后为 85/35；成班 Capture 后为 85/0，未成班 Release 后恢复为 120/0。

- MinEnrollment 默认 10，可设 2–200；不能高于物理场次容量，线上容量 0 保持不限。
- 日期要求为 `RegistrationOpensAt < RegistrationClosesAt <= StartsAt - 10 days` 且 `StartsAt < EndsAt`。当前报名截止按 **UTC 时间戳**校验至少提前 10 天。
- 后台在启动时及每分钟扫描到期 Published Intake。以整个 Intake、跨 Session 的预约人数判断成班，在同一 Serializable 事务内处理整班 Capture 或 Release；中途失败整班回滚。
- 达到人数时写 ConfirmedToRunAt。Intake 在开课前保持 Published，到 StartsAt 后变为 InProgress；ConfirmedToRunAt 与 Creator 的排期确认是两个不同事件。
- 财务确认后不能继续预约。已有 Reserved/Active 或已经财务确认时，结构变更不能重新打开报名窗口；常规 meeting link 更新保留。
- 历史已扣款 Active 不再二次 Capture。重放取消、结算或退款不会重复返还积分。
- Reserved 不开放学习资源、完成、评分、争议退款或证书流程；确认前通过取消预约取回保留积分。

报名写操作在数据库执行策略中使用事务、条件余额/席位更新和稳定防重。该代码解决了 GitHub 旧报名路径直接手动事务与启用重试策略不兼容的实现问题；真实 SQL Server 验收仍见第 5 节边界。

主要代码与测试：[EnrollmentService](CoLearnX.Server/Services/EnrollmentService.cs)、[IntakeSettlementService](CoLearnX.Server/Services/IntakeSettlementService.cs)、[CreditReservationWorkflowTests](CoLearnX.Server.Tests/CreditReservationWorkflowTests.cs)、[IntakeSettlementBoundaryTests](CoLearnX.Server.Tests/IntakeSettlementBoundaryTests.cs)。

### 2.3 延期、一次重新预约与 70% 退出

只有因最低人数不足而取消的 Intake（MinimumEnrollmentNotMet）可以提供延期；Trainer 主动取消（TrainerCancelled）不产生邀请。

1. 原 Trainer 在 CancelledAt 后 **7 个经过日**内，为同一 Course 创建一个更晚的 replacement Draft。复制 Session 与平移后的日程，不复制出勤、报名或资源历史。
2. Draft 经原有 Creator 排期审核链发布；Creator 确认也必须在原 7 天窗口内。发布后才通知符合资格的原学员，在 My Programs / History 选择替代 Session。
3. 原学员在同一窗口、开放报名时间且有余额/容量时重新预约。再次 Hold 使用**原 enrollment 的价格快照**；延期没有额外费用，等待期间释放的积分可用。
4. PostponedFromEnrollmentId 唯一关联原预约与新预约。同一邀请只能使用一次，即使取消新预约也不能再使用。学员此前自行取消的预约没有延期邀请。
5. 拒绝或到期无需操作，已 Release 的积分保留在可用余额。

Active 自行退出只开放在开课前 **6–10 个 UTC 日历日**；超过 10 日或不多于 5 日均拒绝。退款为 70%，按整数 credits 四舍五入，半数远离零；剩余记录 Forfeit。替代班成班后的退出以替代 Intake 的 StartsAt 判断。

规则、API 和历史验证见 [INTAKE_LIFECYCLE.md](docs/INTAKE_LIFECYCLE.md)，延期用例见 [IntakePostponementTests](CoLearnX.Server.Tests/IntakePostponementTests.cs)。该历史文档中的测试数量描述当时快照；当前汇总采用第 4 节的 2026-10-03 结果。

### 2.4 通知、业务邮件和旧数据库升级

通知新增 IntakeId，Trainer/Creator 可从通知进入自己权限下的具体 Intake。旧通知没有 ID 时回到列表；前端仅放行已知路径和合法正整数详情路径。Member 的取消、延期邀请进入 My Programs，并说明最多等待 7 天、UTC 截止时间、积分已经释放及如何重新预约。

成班、整班取消、人数不足释放、开课提醒、延期邀请接入既有邮件通道。站内通知在业务事务中保存，邮件在提交后由后台领取发送，尊重 EmailNotifications 偏好。每批最多 50 封，失败约 5 分钟后重试，单封最多等待 30 秒；邮件失败不会撤销积分结算。SMTP 已送达但成功标记前进程崩溃时可能重发，当前实现不承诺严格 exactly-once 投递。

账号与业务邮件统一 UTF-8，并生成 plain text / HTML alternative。Development/Testing 默认保存为本地 `.eml`；本轮验证过真实邮件序列化和本地捕获，没有重做外部收件箱或原手机邮件客户端验收。

启动 SeedData 流程接入 RecommendationSeed、CreditReservationSchema、NotificationSchema、MaterialUsageSchema，补齐旧库缺失字段与索引，保留既有余额、报名和消息。历史通知默认不补发，未知 Usage Intake 保留为空；这套升级使用显式 schema 检查，不是新增完整 EF migration 发布流程。SQLite 重复升级已有集成证据，SQL Server 分支未在真实库执行。

详情见 [通知、邮件和演示数据记录](docs/verification/20261001-notifications-mail-demo.md)、[NotificationEmailDispatcher](CoLearnX.Server/Services/NotificationEmailDispatcher.cs)、[NotificationSchemaTests](CoLearnX.Server.Tests/NotificationSchemaTests.cs)、[IntakeSchemaUpgradeTests](CoLearnX.Server.Tests/IntakeSchemaUpgradeTests.cs)。

## 3. 首轮 PDF 问题修复对照

原 PDF 为《CLX-ONLINE-20260930-首轮测试问题汇总-精选截图标注版》，共 72 页、79 个独立问题编号。下表保留全部编号，按共同修复行为归组；E 组部分编号复现了其他组的同一问题，不当作独立根因重复计算。

完整逐项台账采用 **65 项本地修复 + 7 项原已实现回归 + 3 项本地改善待外部核对 + 4 项环境/数据/规则待核对**。这里“原已实现”是相对开始 PDF 修复前的本地 v7，包含更早几轮开发，不能直接解释成 GitHub 基线已全部实现。例如报名、Admin 调整的基础能力在 GitHub 已有，当前事务重试与状态边界仍是 v7 增量。

| PDF 编号 | 问题与本地处理 | 结果范围 |
| --- | --- | --- |
| S01-F01 | Admin 登录移除公开/预填演示凭据；Production 阻断共享演示密码，种子停用未轮换共享账号 | 本地实现与测试；线上凭据轮换未执行 |
| S01-UX01 | 普通登录浏览器标题改为 CoLearnX，角色页显示实际标题 | 本地修复 |
| A01-UX01、A01-UX02、A02-UX01 | 注册列出具体密码规则；编辑后清除旧错误；成功页展示掩码邮箱 | 本地修复 |
| MAIL-F01、MAIL-UX01、MAIL-OBS02 | 修正 UTF-8/MIME 正文并提示等待、垃圾箱；原邮件客户端的源码/防欺诈警告需外部复测 | MIME 本地改善；原客户端观察未关闭 |
| A04-UX01 | 验证链接失效/已用提示说明若已验证可直接登录；成功验证后提供 Sign in，保留一次性校验 | 本地实现；成功验证/无效 token 已验证，已用/过期分支页面文案未单独复测 |
| A05-UX01、A11-UX01 | 单身份直达工作区，多身份保留选择；越权跳转说明目标身份要求 | 本地修复 |
| A10-UX01、A10-UX02 | 身份申请加载不显示假 Not requested 或允许重复 Apply；申请/审核时间标 UTC | 本地修复 |
| A12-UX01、A13-UX01、A13-UX02、A13-UX03 | 找回密码保持防枚举反馈；两栏稳定显示/隐藏；401 用中性会话说明；填写前预检 token，网络失败可重试 | 本地修复；原手机自动填充未复测 |
| M01-UX01、M02-UX01 | 首页加载不闪假零值/假空态；目录无匹配提供清除筛选入口 | 本地修复 |
| M01-OBS01 | 定位演示证书与手设进度来源，保留历史证书 | 历史数据仍待核对 |
| M03-F01、M03-F03、M03-UX01、M03-OBS01 | 详情补简介、级别、路径、模式、实际 Creator/Trainer；跨日显示完整 UTC 日期；改善栏宽；移除固定 Senior Trainer/Certificate included | 本地修复 |
| M03-F02 | Learning Outcomes 缺失时明确说明未提供，不编造 4010 内容 | 本地改善，权威内容待确认 |
| M03-OBS02、M04-UX01、M04-F01、M04-F02、D02-UX03、E01-UX01 | 先检查时间/状态/容量再检查余额；不可预约禁用；提交防重；执行策略事务；安全错误和 Reference | 含 PDF 回归项与 GitHub 增量；SQL Server 待验收 |
| M07-UX01、M08-UX01、M09-UX01 | 资源刷新明确反馈；Programs 标签/课程/报名写入 URL；资格显示进度、全部考核、分数、通过线与未评分 | 本地修复 |
| M07-OBS01 | 旧材料、录播与 Intake/进度关联不凭空补齐 | 历史资源数据仍待核对 |
| M12-UX01 | 头像上传前常驻显示 PNG/JPEG/WebP、2 MB 限制 | 本地修复 |
| M13-UX01、M13-OBS01、M13-F01、M13-UX02 | 无可争议报名解释原因；保留学习中心深链接；Open 争议防重；事务重放不重复创建；未知结果提供 Reference | 含 PDF 回归项与本地修复 |
| T02-F01、T02-UX01、E01-F01、E01-UX02 | Trainer 草稿创建纳入执行策略，ACK 丢失只创建一次；日期错误和写失败可读 | 本地修复 |
| T05-UX01、T06-F01 | 出勤页显示课程/Intake/完整 UTC 场次；Trainer 和 Member 使用全部 Session/assessment 作为分母，区分未评分 | 本地修复 |
| T01-OBS01 | 保留开课时状态推进；EndsAt 后 Intake 自动 Completed 的业务合同尚未明确 | 规则待确认，不代替 Trainer 完成学员或发证 |
| C01-UX01、C01-UX02、C01-UX03 | PendingApproval/Published 保留完整只读定义；加载/保存错误分开；代码列换行不与标题重叠 | 本地实现；只读页、布局、404 和通用安全错误已验证，无归属/403 及保存/加载重试的全部分支待补测 |
| C02-F01、C02-UX01、E02-F01、E02-UX01 | 两种课程状态共用上传执行策略、稳定路径防重、失败清理与已提交文件保留；隐藏数据库异常 | API 成功上传已验证；浏览器文件选择受工具权限阻断 |
| C03-UX01、C03-UX02、C03-OBS01 | Usage 新事件记录 Intake，展示日程/UTC；区分空库与无匹配筛选；旧未知关联明确 unavailable | 本地修复，历史空关联未伪造 |
| C04-UX01 | 0 waiting 不显示 Action required；加载失败不伪装空队列 | 本地修复 |
| C05-UX01、C05-OBS01 | Account 文案移除实现术语，说明 Full name 与 Display name 用途 | 本地修复 |
| D01-UX01 | 角色申请附件按真实路径显示，await 下载并给忙碌/启动/失败反馈 | 本地修复；缺实体文件的演示附件无法成功下载 |
| D02-UX01、D02-UX02、D02-OBS01 | Admin 审批提供完整课程定义；Creator 与实际 Trainer 分开；Submitted by 使用 Creator 投影 | 展示本地修复；历史所有权仍待核对 |
| D03-UX01 | 材料审批显示课程/状态、下载文件名、真实大小和 UTC，缺值逐项说明 | 本地修复；旧原始上传名不可追溯 |
| D05-F01、D05-UX01、D05-F02、D05-UX02、D05-OBS02 | /admin/users 接真实 Admin-only 查询，覆盖无账本用户；调整预览可用/保留余额；重试/幂等；确定拒绝写 Failed 审计，未知结果保留 trace | 含 PDF 回归项与 GitHub 增量；浏览器未实际调整钱包 |
| D06-UX01、D06-F01、D06-UX02、D06-OBS02 | 退款显示完整报名上下文；全额实体退款释放席位，部分保留，线上无实体席位依赖；重复不二次退款；未知结果先核对，换案清空旧金额/理由；确定失败留审计 | 本地实现与 API 测试；浏览器未实际退款 |
| U01-F01、U01-F02、U01-OBS01 | 390px Account 单栏且按钮换行；768px 审批标签完整显示；窄屏补 More/滚动发现提示 | Chrome 视口验证；真实手机触摸待验收 |
| U02-F01、U02-F02 | Modal 补 dialog/aria-modal、标题关联、初始焦点、Tab/Shift+Tab 循环、顶层 Escape 和关闭后焦点恢复 | 自动化与部分真实键盘验证；屏幕阅读器未验收 |

每个编号的问题标题、修改、实际结果、测试名称和限制见 [GitHub 逐项复测文档](docs/verification/20261003-v7-public-retest.md)。包含操作步骤、预期和截图的原始完整报告保留在本机 `artifacts/verification/20261003-pdf-edge-retest/`。早期修复记录见 [20261002-first-round-issues-fixes.md](docs/verification/20261002-first-round-issues-fixes.md)。

## 4. 最新验证证据

以下为 **2026-10-03 已执行并保存的应用验证**。上传准备阶段再次运行后端、前端测试、ESLint 和生产构建；浏览器结果来自同日下午的逐项复测，没有重复操作浏览器。公开摘要与相对源码链接随本版提交，原始日志保留本地。

| 验证层 | 最近结果 | 可检查证据 |
| --- | --- | --- |
| 后端自动化 | 20:03:45–20:04:37 +08:00，313/313 Passed，0 failed | [发布验证记录](docs/verification/20261003-master-publish.md) |
| 前端 UI / Node | 上传前再次验证 Vitest 99/99，Node 20/20，共 119 passed | [发布验证记录](docs/verification/20261003-master-publish.md) |
| ESLint / production build | 上传前再次运行，均 exit 0 | [发布验证记录](docs/verification/20261003-master-publish.md) |
| 浏览器 | 用户允许使用可用浏览器后实际使用 Chrome；42 条操作记录、41 张截图，捕获的 console 无 warning/error | [逐项复测摘要](docs/verification/20261003-v7-public-retest.md)，原图保留本地 |
| PDF 台账与交付包 | 79 编号无遗漏/重复；原报告 391 个链接存在；41 张图片可解码；ZIP CRC/内容哈希通过 | [逐项复测摘要](docs/verification/20261003-v7-public-retest.md)，原始 ZIP 保留本地 |

浏览器在独立 SQLite 预览库中验证了账户/弹窗、跨角色页面、课程定义、学习者统计、Admin 用户查询/财务预览、Creator Usage，以及新建争议和 Trainer Draft → Session → Submit → Creator Confirm。预览库与临时服务已在复测结束后清理，截图和日志保留。

C02/E02 的成功小 PNG 上传由一个参数化用例的两个真实 HTTP API 执行分支覆盖 PendingApproval/Published：创建待审材料版本，核对 Admin 审批元数据，下载后逐字节核对 image/png。Chrome 扩展未开放 file URL 权限，浏览器完整选择文件步骤没有通过。安全错误的部分用例直接验证控制器/过滤器，不代表向真实 SQL Server 注入故障。

**SQL Server 专项未执行：**本机没有 LocalDB，`Approve_Succeeds_WhenSqlServerRetryOnFailureIsEnabled` 被排除，未计入 313 个通过。SQLite 集成和模拟提交 ACK 丢失重试证明本地行为，不能替代 Azure SQL 的真实 provider、隔离和连接重试验收。

截图与复测证据 ZIP 保留在本机 `artifacts/verification/20261003-pdf-edge-retest/CLX-v7-逐项复测证据-20261003.zip`。目录名沿用 `pdf-edge-retest`，实际浏览器是 Chrome；公开复测文档对此有明确记录。

## 5. 本地运行

技术栈为 ASP.NET Core / EF Core 10、React 19、Vite 8；本地可用 SQLite，代码也支持 SQL Server/Azure SQL。文件存储可用本地或 Azure Blob；PayPal 充值沿用既有 Sandbox 配置。

版本锁定见 [global.json](global.json)（SDK 10.0.400、latestFeature roll-forward）和 [.node-version](.node-version)（Node 24.19.0）。先安装匹配运行环境，并准备 .NET HTTPS 开发证书：

```powershell
dotnet --version
node --version
dotnet dev-certs https --trust
```

以下方式分别运行后端和 Vite，避免旧 README 的工作目录与旧预览端口。后端使用单独的本地数据库，邮件使用捕获模式。

**终端 1：后端，从 v7 根目录运行。**

```powershell
# 在克隆后的仓库根目录执行
New-Item -ItemType Directory -Force 'CoLearnX.Server/App_Data' | Out-Null
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://localhost:5088'
$env:ConnectionStrings__Default = 'Data Source=App_Data/v7-local.db'
$env:PasswordReset__DeliveryMode = 'Capture'
$env:PasswordReset__ClientBaseUrl = 'https://localhost:55128'
dotnet restore CoLearnX.Server/CoLearnX.Server.csproj -p:SkipSpaPublish=true
dotnet run --project CoLearnX.Server/CoLearnX.Server.csproj --no-launch-profile -p:SkipSpaPublish=true
```

**终端 2：前端，显式将 /api 代理到该后端。**

```powershell
Set-Location colearnx.client
$env:ASPNETCORE_HTTPS_PORT = $null
$env:ASPNETCORE_URLS = 'http://localhost:5088'
npm.cmd ci
npm.cmd run dev -- --host localhost --port 55128 --strictPort
```

打开 [https://localhost:55128](https://localhost:55128)。Vite 的 /api 代理读取本终端环境，优先检查 ASPNETCORE_HTTPS_PORT，因此上面将其清空。使用其他端口时，同时修改前端代理目标和 PasswordReset__ClientBaseUrl。

启动会创建/升级该 SQLite 库并加载种子；本地邮件在 `CoLearnX.Server/App_Data/mail/*.eml`。Development 保留演示数据与管理员种子，Production 不启用共享演示管理员。普通注册走邮箱验证；Admin 使用独立登录及账号体系。本文不公开账号密码。

真实邮件需将 DeliveryMode 设为 Smtp，并配置 PasswordReset 下的 SmtpHost、SmtpPort、SmtpUsername、SmtpPassword、真实 FromAddress、用户可访问的 ClientBaseUrl。部署配置还需要实际 JWT、数据库、存储及 PayPal 参数；当前本地 Capture、SQLite 和测试结果不表示这些外部通道已验收。

### 5.1 复现自动化检查

后端从 v7 根目录运行。没有 LocalDB 时使用与本地证据相同的排除条件；有可用 SQL Server 测试环境时，应按其连接配置单独补跑该专项。

```powershell
dotnet test CoLearnX.Server.Tests/CoLearnX.Server.Tests.csproj -p:SkipSpaPublish=true -p:UseAppHost=false --filter 'FullyQualifiedName!~Approve_Succeeds_WhenSqlServerRetryOnFailureIsEnabled'
```

前端从 colearnx.client 目录运行：

```powershell
npm.cmd test
npm.cmd run lint
npm.cmd run build
```

测试数量会随后续代码变化。现有 NU1903 提示涉及 Microsoft.OpenApi 2.0.0、SQLitePCLRaw.lib.e_sqlite3 2.1.11，本轮未升级这些依赖。

## 6. 阅读与维护入口

| 用途 | 文件 |
| --- | --- |
| 当前架构、实体和模块入口 | [CODE_OVERVIEW.md](docs/CODE_OVERVIEW.md) |
| 推荐/报名设计对照 | [2026-09-23-recommendation-and-enrolment-design.md](docs/superpowers/specs/2026-09-23-recommendation-and-enrolment-design.md) |
| 结算、取消、延期合同与 API | [INTAKE_LIFECYCLE.md](docs/INTAKE_LIFECYCLE.md) |
| Member 钱包/预约 UI 修改 | [20260930-member-ui.md](docs/verification/20260930-member-ui.md) |
| 通知、业务邮件、演示数据 | [20261001-notifications-mail-demo.md](docs/verification/20261001-notifications-mail-demo.md) |
| PDF 问题修复记录 | [20261002-first-round-issues-fixes.md](docs/verification/20261002-first-round-issues-fixes.md) |
| 最新逐项复测入口 | [20261003-pdf-issue-by-issue-retest.md](docs/verification/20261003-pdf-issue-by-issue-retest.md) |
| GitHub 逐项复测文档 | [20261003-v7-public-retest.md](docs/verification/20261003-v7-public-retest.md) |
| 上传前验证与范围 | [20261003-master-publish.md](docs/verification/20261003-master-publish.md) |

对照原件：

- 设计稿：`2026-09-23-recommendation-and-enrolment-design.md`。仓库 docs 副本与本机原件已核对一致。
- 问题 PDF：`CLX-ONLINE-20260930-首轮测试问题汇总-精选截图标注版.pdf`。本轮问题台账对应的 SHA-256 为 `39e5fde3d679e2fd723642921ddaed504251d8668ed5ffabf814991b3b88d813`，原 PDF 保留本地。

### 6.1 尚未关闭的范围

79 条台账完整不等于 79 条线上问题全部关闭。当前仍需目标环境验证真实 SQL Server schema/重试、SMTP 投递与原手机邮件客户端、生产对象存储、真实手机触摸/屏幕阅读器，以及历史证书、资源关联和课程所有权。浏览器财务操作只验证预览，实际调整/退款由自动化覆盖；本轮没有改线上数据、轮换线上管理员密码或重新验证 PayPal 实际支付。

Intake 在 EndsAt 后自动 Completed 尚缺明确合同；Trainer 完成学员与证书审批仍按各自规则执行。评分弹窗、部分 Creator 标签边界和推荐排序细节存在源码/后端证据，但没有每个分支的单独浏览器验收。

2026-09-23 设计稿标记为 draft, pending review。AI/向量推荐、协同过滤、游客推荐、Admin 兴趣词表编辑、按单个 enrollment 做 PayPal 预授权、候补队列、自动充值均为该设计列出的非目标，本地 v7 没有实现这些能力。
