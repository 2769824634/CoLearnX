# 首轮问题修复：Member、Creator、Trainer 业务界面

本文保留 2026-10-02 业务 UI 修复阶段的历史快照，未提交状态与测试数量均指当时。当前状态见 [2026-10-03 逐项复测](20261003-v7-public-retest.md)。

修复基线：`C:/Users/user/Documents/CoLearnX/.local-deploy/20260929_v7` 的当前未提交实现。修改前快照：`C:/Users/user/Documents/CoLearnX/.local-deploy/20261002_v7_before_first_round_fixes_083232`。本文件只记录业务 UI 组的新增修改，既有 v7 推荐、积分保留/扣除/释放、延期和通知流程继续保留。

## 修复范围

|问题编号|修改|
|---|---|
|M01-UX01|首页统计和真正的空态等待 Member 数据加载成功；加载失败给出重试入口。Continue 带 enrollmentId 打开相应学习中心。|
|M02-UX01|目录区分加载、失败、真实空库与无匹配；清除筛选恢复 All Programs。|
|M03-F01/F02/F03、M03-UX01|详情补简介、等级、学习路径、真实 Creator/Trainer；移除固定 Senior Trainer 与 Certificate included，显示申请条件。课程摘要取代大块代码占位，场次完整显示起止日期、UTC 和上课模式。缺学习成果会明确说明，未虚构样本成果。|
|M03-OBS02、M04-UX01|缺起止时间、过期/进行中场次保守禁报名，状态检查在余额检查之前。报名确认在飞行中禁重复提交并显示 Reserving。|
|M04-F02、M13-UX02、C02-UX01、E01-UX02、E02-UX01、T02-UX01|本组展示错误使用业务提示，遮蔽异常实现细节和错误码；保留服务端 traceId 为 Reference。Trainer 字段错误映射人类可读标签。服务器事务和真实数据库验证由后端组处理。|
|M07-UX01、M08-UX01|资源刷新显示稳定完成反馈；Programs 标签、选中课程和报名写入 URL，支持 completed/history 与深链接。|
|M09-UX01|资格卡消费 progressPercent、全量 assessments，显示完成目标、出勤目标、未评分/通过/未通过以及分数、通过线。资格决策继续以服务器为准。|
|M13-UX01/OBS01|没有符合报名时解释争议前置条件；Reserved 不可选择，给出保留报名取消入口。Programs 学习中心明确争议/退款审查入口。|
|T05-UX01|出勤选项和摘要显示课程代码/名称、Intake、场次完整 UTC 起止时间。课程名从既有 catalog API 取。|
|T06-F01（UI）|通过数分母使用 assessmentsTotal，独立显示 assessmentsGraded；统计公式由后端组统一。|
|C01-UX01/UX02/UX03|锁定后的 Course definition 全部保留、禁编辑；Creator 加载和写操作错误语义区分；页面标题包含实际标题；课程代码列可换行，容器防止代码/标题重叠。|
|C03-UX01/UX02/OBS01|Usage 显示 IntakeId 及其完整时间，日期明确 UTC；有筛选但无结果与空库说明分开，并提供清除筛选。|
|C04-UX01|0 待审不显示 Action required，加载/失败也不显示假0队列。|
|U01-F01（CSS）|member-account-grid 在宽屏为 240px + 自适应，900px 以下单栏；MemberAccount 内联布局由账号组删除。|

页面 DTO 均为已有字段上的扩展消费。课程新增 `creatorName`、`trainerNames`、`learningPath`；资格新增 `progressPercent` 与 `assessments`；使用记录新增 `intakeId`、`intakeStartsAt`、`intakeEndsAt`；Trainer learner 新增 `assessmentsTotal`。缺扩展字段时显示未提供信息，避免把旧 Course.Trainer 字段误认作授课者。Enrollment 中授课者映射需由后端统一为 Intake.Trainer。

## 新鲜自动验证

TDD 首次执行 `FirstRoundBusiness.test.jsx`：11 个目标行为均失败；实现后 11/11 通过。随后新增上传加载/安全错误回归：2 个目标行为失败后修复通过；报名重复确认回归观察到 2 次调用，补在飞行中禁用后变成 1 次。新测试集共17例，其余用例覆盖 UTC 跨日、真实角色、资格明细、出勤上下文、完整考核分母等。

最终精准前端命令（在 v7 `colearnx.client`）：

```powershell
npx.cmd vitest run --environment jsdom src/pages/FirstRoundBusiness.test.jsx src/pages/member/MemberCourseDetailPage.test.jsx src/pages/member/MemberProgramsPage.test.jsx src/pages/member/MemberBadgesPage.test.jsx src/pages/member/MemberDisputesPage.test.jsx src/pages/creator/CreatorCoursesPage.test.jsx src/pages/creator/CreatorUsagePage.test.jsx src/pages/creator/CreatorIntakeNotification.test.jsx src/pages/trainer/TrainerPostponement.test.jsx --reporter=dot
```

2026-10-02 08:52:18（Asia/Shanghai）执行：退出0，9个文件通过，46例通过。旧课程详情测试中的合法未来场次补齐真实 DTO 起止时间；保留所有原始断言，不以未知时间可报名作为契约。

## 审查和边界

对本组相对快照的变化完成正确性、可读性、架构、安全、性能、可用性审查。复用既有 API client、角色壳、Modal、token；未增加权限旁路或改变金额、报名副作用。审查中修复重复报名确认、上传忙时切换表单导致资源归属显示错乱、失败空态误导和技术异常泄露。仅共享 UTC/error 展示逻辑，未做无关重构。

自动证据是 jsdom UI 行为，不证明实际手机、390/768像素、键盘/读屏、SQL Server、邮件或线上系统已通过。全量测试、build/lint 和角色浏览器验证由主任务合并后执行；本组没有另行发布、提交或推送。演示证书/旧材料关联等样本观察仍需数据修复及真实环境验收。

## Admin 角色附件与材料审批补充

主任务追加分配 `AdminRoleRequestsPage.jsx`、`AdminLaterApprovalsPage.jsx` 和其回归测试；本组未修改 AdminDataState、财务/争议/课程审批页面或 admin.css。

|问题编号|补充修改|
|---|---|
|A10-UX02|角色申请与已审日期统一明确 UTC，并将无时区后缀的服务端时间按 UTC 解析，表头为 Submitted (UTC)。|
|D01-UX01|简历与身份证按钮分别依据对应附件路径显示；下载 await，显示等待、浏览器下载启动反馈、友好失败和 Reference。忙时禁重复下载、切换决定和提交审核。成功文案不声称文件已打开或用户已阅读。|
|D03-UX01|材料版本卡显示课程标题/代码/状态、真实文件名/文件大小、提交 UTC；缺失字段分别显示 unavailable。下载使用真实 fileName 优先、显示请求过程和启动/失败反馈，避免 Open submitted file 的误导。|
|审批反馈|审核写失败与队列加载失败分开；失败审查可 Check current queue，下载失败留在对应附件，保留当前已加载卡片。|

新增 `AdminAttachmentReview.test.jsx` 的前8个目标行为在实现前全部失败，实现后8/8通过；另补材料下载失败分支，共9例。2026-10-02 09:07:16合并本组定向执行：11文件57例通过，退出0。包括上述原9文件、AdminAttachmentReview、主任务的 AdminFirstRound 回归。

同轮对本组21个JS/JSX文件执行定向 ESLint，退出0。主任务发现两处 effect 内同步状态设置后，已改为身份关联 snapshot：课程详情由 courseId 匹配派生 loading/error，上传库由 token+courseId 匹配派生加载与数据；未加入 lint 忽略或微任务绕过。

附件按钮依据服务端路径声明，UI没有独立探测实际服务器磁盘/blob；缺附件元数据和下载失败均有明确反馈，仍需真实附件/存储浏览器验收。`downloadFile` 对失败 traceId 的保留由主任务处理共享 API client，本组没有越界修改。
