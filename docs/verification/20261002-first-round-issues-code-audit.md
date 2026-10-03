# 首轮测试问题对照最新本地代码核查（MD + PDF复核）

> 本文保留修改前的核查结论。随后按用户要求在 v7 修复，最新逐项状态与验证证据见 [v7 首轮问题修复与验证记录](20261002-first-round-issues-fixes.md)，请勿将下文的“仍存在”直接当作修复后状态。

核查日期：2026-10-02（Asia/Shanghai）。

原始材料：CLX-ONLINE-20260930-首轮测试问题汇总-精选截图标注版.md（外部原件，保留本地）。

PDF复核材料：同目录的 `CLX-ONLINE-20260930-首轮测试问题汇总-精选截图标注版.pdf`，共72页。2026-10-02按用户补充要求重新读取全部正文、查看37张精选截图并核对当前关键源码。PDF正文台账的79个编号与本报告一一对应，无漏项或新增编号；PDF复核没有重新操作线上，也没有重复运行MD核查轮的自动测试。

## 结论与版本边界

最新本地 v7 不能视为已关闭首轮问题。报名、管理员积分调整和争议退款已有重试事务包装；普通 Intake 草稿创建、材料版本提交及争议创建仍有直接事务路径。大量信息展示、错误反馈和键盘问题仍在源码中，Trainer/Member 的资格统计分母差异已定位。

检查主目录：`C:/Users/user/Documents/CoLearnX/.local-deploy/20260929_v7`。当前为 master，HEAD `b4ad6c60a6098af68c8c97c11793b3238a6a7d60`；包含未提交的 v7 功能修改和新增文件。近期实际进度记录是 docs/verification/20260930-member-ui.md 与 20261001-notifications-mail-demo.md。HEAD 本身不能代表当前未提交实现。

父目录仍为 feature/member-core、HEAD 255f0cb，存在既有未提交工作；本轮未切换或修改其应用代码。未拉取远端、未验证线上部署SHA、未复测线上，也未轮换密码、支付、审批或改动线上数据。MD核查轮新增、PDF复核轮补充本报告，没有修复应用代码。

原文52项是测试用例数量；完整问题台账抽取到79个不同的缺陷/体验/观察编号。关联E组复现保留独立编号，但不是新增独立根因。文档里的“等待安排”“不执行测试”等是原测试交接内容，不作为本次检查指令。MD核查轮检查当前代码并运行现有本地测试；PDF复核轮重新核对正文、截图及关键源码，没有重复运行测试。

状态含义：**仍存在**为当前源码保留缺口（涉及SQL或像素表现时仍注明未作对应环境复测）；**已实现待线上复测**表示实现和相关本地自动检查已有证据，不能关闭线上问题；**部分改善**表示子问题或完整链路仍缺；**需环境或数据核对**保留原观察边界；**产品取舍**不当作必须修复的功能错误。

|核查分类|编号数|
|---|---:|
|仍存在|55|
|部分改善|5|
|需环境或数据核对|10|
|产品取舍|2|
|已实现待线上复测|7|

## 优先处理的阻塞

1. **普通建草稿 T02-F01/E01-F01**：CourseIntakeService.CreateAsync 第92行直接 BeginTransactionAsync。第33行的包装属于 PostponeAsync，不能据此判断普通建草稿已修复。
2. **材料上传 C02-F01/E02-F01**：上传写文件后调用 MaterialVersionService.CreateAsync，该方法第56行直接开事务；PendingApproval和Published课程都走这里。文件写入与数据库失败后的存储清理也应一并补验。
3. **争议创建 M13-F01**：AdminFinanceService.CreateDisputeAsync 第122行直接开事务，当前只有调整及退款方法有策略包装。
4. **公开管理员凭据 S01-F01**：UI默认值及公开提示仍存在；本地代码没有证明线上密码已经轮换，隐藏UI也不等于使旧凭据失效。
5. **跨身份资格 T06-F01**：先统一业务统计口径，再修服务和DTO，至少加入“整班2次课、仅1条Present、1个考核尚未评分”的两端一致回归。

Program.cs 第42行的 SQL Server 配置启用 EnableRetryOnFailure。在该配置下，直接手动事务需要作为整体放进执行策略；上述三条未包装路径因此是高可信的当前代码问题。此判断来自源码与 [Microsoft EF Core 连接可靠性文档](https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency)，不是本轮线上服务器日志结论。本轮没有可用SQL Server完成复现；SQLite成功不能证明这些路径在SQL Server正常。

### T06 口径的具体差异

|相同输入：整班2次课、1条Present、1项未评分考核|Trainer列表|Member资格|
|---|---:|---:|
|出勤分母|已有记录1条|全部Session 2次|
|出勤率|1/1 = 100%|1/2 = 50%|
|考核分母|已有结果0条|全部考核1项|
|界面通过数|0/0|0/1|

该例按当前两个服务公式演算，准确解释原报告差异；本轮未重新操作同一线上账号。Member侧资格检查仍用100%完成、80%出勤、全部考核通过；这里没有证据证明已经错误发证。

## PDF复核补充（2026-10-02）

本次新证据是PDF全部72页正文与37张精选截图，以及对v7关键源码的重新读取。79个编号的分类计数保持不变；未把E组关联复现当作新的独立根因，也未把52项测试用例当作52项全部通过。报告逐项表增加正文与配图页码，便于返回原证据。

|问题/范围|PDF证据|复核后的准确判断|
|---|---|---|
|S01-F01|正文8；图01–02，35–36页|正文先保留早期“未验证有效性”，随后明确A-07已正常登录Admin首页，最终证据应采用后者。当前UI仍含公开/预填凭据；本次未验证现在的账号密码或全部管理写权限。|
|MAIL-F01|正文10；图06，40页|同时影响验证和重置邮件。截图证明原手机实际显示HTML源码，但没有邮件原始头，不能仅凭截图确定MIME、发送器或客户端哪一环节错误。必须分别复测两类邮件。|
|C02-F01/E02-F01|正文20、27；图24/34，58/68页|同一合法小PNG在PendingApproval和Published均失败，不能解释为只因课程待审批。当前两种状态共用MaterialVersionService.CreateAsync的未包装事务。UploadAsync先存文件再提交版本，没有数据库失败后的补偿删除，存在遗留文件的可能；未枚举线上存储，不能声称原失败已留下孤立文件。|
|T02-F01/E01-F01|正文18、27；图18/33，52/67页|合法未来日期在原课程、不同Trainer及新Published课程上均失败，不能当作日期倒置或仅旧样本问题。当前普通CreateAsync仍直接开事务；PDF中的通用500不能单独证明线上与报名同根因，仍需部署SHA和请求日志。|
|M13-F01|正文17、重点2；图16，50页|PDF已从无报名的受阻状态更新为H有效2051报名、231字符原因提交失败。当前CreateDisputeAsync仍缺重试包装，但PDF里的Internal Server Error本身不证明线上具体根因。|
|D05-F02/D06-F01|正文24–26；图31–32，65–66页|PDF记录的是当时有效+1调整、全额25退款失败，不是无效输入或缺条件。当前调整和退款已有策略包装，不能把旧截图直接当当前运行结果；SQL Server/线上仍需验收。退款座位释放缺口及失败文案问题继续保留。|
|T06-F01|正文19；图21–22，55–56页|截图对应同一UI2051学员：Trainer100%/0/0，Member50%/0/1。重新读取两端服务确认统计分母不同；不据此宣称Member规则错误或发生违规发证。|
|M03-F03|正文14；图12，46页|17:30–02:30缺结束日期/时区，属于表达歧义。当前映射仍省略结束日期；不能将该图解释为后台结束时间早于开始。|
|U01-F01/U01-F02|正文28；图35–36，69–70页|390px账户页是可横向滚动的挤压/越界；768px审批栏则被overflow:hidden裁切，不能横向查看完整标签。当前内联双栏覆盖媒体查询、审批断点760px的两处原因仍在。未在本次真实浏览器重测尺寸。|
|U02-F01/U02-F02|正文28；图37，71页|截图只是按Esc后的结果；完整键盘过程由正文/原轨迹支持。本次完整读取共享Modal，确认没有自动聚焦、Tab循环、焦点回归或Escape监听。源码确认缺口，不替代真实键盘/读屏验收。|

PDF原文“复测等待安排”“不执行测试”等属于原报告的交接说明；本次按用户当前要求完成文档和代码复核，没有把这些句子当作新指令，也没有代替用户安排线上支付、审批或改密。

## 逐项对照

证据键指向下方的当前源码入口；必要的具体行在核查说明中列出。PDF页码为原PDF页脚页码；“正文”指完整台账，“图”指精选截图。无单独截图的条目仍按正文的人工反馈或动态记录核查，不因缺图而删除。

|原编号|当前结论|核查说明|源码证据|PDF正文/配图页|
|---|---|---|---|---|
|S01-F01|仍存在|公开演示凭据、预填密码仍在 AdminLoginPage。PDF最终记录已确认原测试时能进入Admin首页，应保留该证据；本次未验证当前凭据是否仍有效或是否已轮换。|A1|正文8；图01(35页)、图02(36页)|
|S01-UX01|仍存在|index.html 默认标题仍为 CoLearnX — Member，普通 LoginPage 没有改为中性标题。|A2|正文8；图03(37页)|
|A01-UX01|部分改善|已有四条实时密码规则，但弱密码提交仍只给笼统提示，未列未满足条件。|A3|正文9；图05(39页)|
|A01-UX02|仍存在|密码和确认栏 onChange 只更新字段，未清除旧 error；原生必填阻断提交时旧提示可继续保留。|A3|正文9；图04(38页)|
|A02-UX01|仍存在|成功页面和服务端注册 message 均未显示待验证目标邮箱或掩码邮箱。|A3,B6|正文9|
|MAIL-F01|需环境或数据核对|PDF明确覆盖验证邮件和密码重置邮件；图06是实际收到的重置邮件源码。当前发送代码设置 IsBodyHtml=true 并提供纯文本备用正文，MD核查轮模板测试通过；两类邮件的线上MIME与原手机客户端仍未复测，不能标为已修复。|B7|正文10；图06(40页)|
|MAIL-UX01|仍存在|注册及验证反馈没有检查 spam/junk 的提示。|A3,A4|正文10|
|A04-UX01|仍存在|服务端对已使用/过期令牌均返回无效结果；页面有 Sign in，但没有“若已验证可直接登录”的说明。|A4,B8|正文10；图07(41页)|
|A05-UX01|产品取舍|LoginPage 对一项或多项 roles 均打开身份弹窗；是现有统一流程，不是授权错误。|A5|正文10|
|A10-UX01|仍存在|RoleApplicationsPanel 在 loading 时仍遍历角色，默认 Not requested；Apply 只因 busyRole 禁用。|A6|正文11|
|A10-UX02|仍存在|AdminRoleRequestsPage 的 Intl formatter 没有 timeZone/timeZoneName，表头也没有时区。|A7|正文11|
|A11-UX01|仍存在|RequireAuth 不匹配角色直接跳当前身份首页，RequireAdmin 跳独立登录；未显示跳转原因。|A8|正文11|
|A12-UX01|仍存在|ForgotPasswordPage 及通用成功 message 没有垃圾箱/等待提示。|A9|正文11|
|A13-UX01|仍存在|ResetPasswordPage 两栏固定 type=password，没有应用级显示/隐藏按钮；相同手机行为本轮未复现。|A9|正文12|
|A13-UX02|仍存在|api/client.js 对受保护请求的 401 统一标为 another device，无密码重置原因区分。|A10|正文12；图08(42页)|
|A13-UX03|仍存在|重置页面只检查 token 是否存在，未在填写前检查已用/失效；后台一次性拦截已有测试。|A9|正文12；图09(43页)|
|MAIL-OBS02|需环境或数据核对|内嵌浏览器警告、邮件信誉及域名分类须在原客户端核对；源码不足以判定黑名单或漏洞。|B7|正文12|
|M01-UX01|仍存在|MemberHomePage 在 state.loading 时仍渲染统计数字和 active.length===0 的真实空态。|M1|正文13|
|M01-OBS01|需环境或数据核对|SeedData 直接预置四张阶段证书及 100% 进度，不经申请链；可解释本地演示差异，线上数据来源仍待确认。|B9|正文13|
|M02-UX01|仍存在|Catalog 仅 list.map，没有过滤后零结果说明及清除筛选入口。|M2|正文13；图10(44页)|
|M03-F01|仍存在|数据映射已有 description/level，但详情未渲染简介和级别；授课模式也未明确展示。|M3,M4|正文13；图11(45页)|
|M03-F02|需环境或数据核对|默认种子仅为 2051 添加四条 LearningOutcomes，4010 没有；详情空数组直接渲染空白。线上须查实际课程资料和发布要求。|M3,B9|正文14；图11(45页)|
|M03-F03|仍存在|when 仍用开始 toLocaleString + 结束 toLocaleTimeString，结束日期和时区省略；报名关闭时间新增展示但无时区。|M3,M4|正文14；图12(46页)|
|M03-UX01|仍存在|详情继续复用 240px 1fr 的 grid-2-1，学习成果/场次在窄栏；本轮未做桌面像素复测。|M3,U1|正文14；图11(45页)|
|M03-OBS01|仍存在|Senior Trainer、Certificate included 仍是固定文案，未接真实职级或获取条件。|M3|正文14|
|M03-OBS02|已实现待线上复测|现有 sessionAvailability 会根据 Intake 状态/报名窗口禁用历史不可报名场次；截止状态测试在MD核查轮通过。历史数据若时间字段缺失或错误仍须核对。|M5|正文14；图13(47页)|
|M04-UX01|已实现待线上复测|tryEnrol 先判场次可预约，再判余额；报名关闭、取消、进行中、完成、满员、无场次按钮均有禁用测试。服务端再校验报名窗口。|M3,M5,B1|正文15；图13(47页)|
|M04-F01|已实现待线上复测|EnrollmentService 已用 CreateExecutionStrategy().ExecuteAsync 包住 Serializable 事务；余额与座位的条件更新、Hold 流水有MD核查轮 SQLite API 测试。SQL Server 和线上未验收。|B1|正文15；图14(48页)|
|M04-F02|仍存在|EnrollmentsController 仍把 InvalidOperationException.Message 直接放进 ENROL_FAILED；前端直接显示 e.message，不能因为原事务路径改善就关闭异常暴露。|B6,M3|正文15；图14(48页)|
|M07-OBS01|需环境或数据核对|种子手设 12/65/100 进度；Learning Hub 只读已关联 Intake 的 Approved 版本，SeedData 未补 CourseIntakeMaterials/录播；本地空资源可由演示数据解释，线上关联须查。|B9,B10|正文15|
|M07-UX01|部分改善|已有 Loading resources… 和网络刷新；没有稳定刷新完成时间/“已更新仍无资源”反馈，短时加载仍可能看不见。|M6|正文16|
|M08-UX01|仍存在|tab 仅从 URL 识别 reserved，其他默认 active，selectedId 为内存状态；点击 Completed/选择课程未写 URL。|M6|正文16|
|M09-UX01|仍存在|Badges 卡只有出勤/通过总数和 reasons；Eligibility DTO 未提供 progressPercent 或具体考核/成绩明细。|M7,B3|正文16；图15(49页)|
|M12-UX01|仍存在|AvatarEditor 的类型和 2MB 限制只在校验错误中，入口旁没有常驻说明。|A11|正文16|
|M13-UX01|仍存在|无 enrollment 时选择框仍是 Choose a program，未显示“无可争议报名”；页面已有 My Programs 返回入口。|M8|正文17；图17(51页)|
|M13-OBS01|已实现待线上复测|已确认 My Programs 的 Learning Hub 内有 View or submit a dispute，并携 enrollmentId；不能说全站没有入口。|M6|正文17|
|M13-F01|仍存在|CreateDisputeAsync 直接 BeginTransactionAsync 后 SaveChangesAsync，未放进重试策略；启用 SQL Server retries 时有明确不兼容路径。SQLite 成功不覆盖它。|B2|正文17；图16(50页)|
|M13-UX02|仍存在|页面直接使用 cause.message；LaterPhaseApiErrors 只处理业务异常和 DbUpdateException，没有覆盖该 InvalidOperationException，缺业务结果/追踪编号。|M8,B5|正文17；图16(50页)|
|T01-OBS01|需环境或数据核对|Settlement 会将已确认且到开始时间的 Published 转 InProgress，但没有按 EndsAt 自动 Completed；历史 Active 与 ConfirmedToRunAt、人工完成规则需核对。|B11|正文18|
|T02-F01|仍存在|普通 CourseIntakeService.CreateAsync 在第92行直接开启事务；第33行的策略包装属于 PostponeAsync，不能套用到普通建草稿。|B4|正文18；图18(52页)|
|T02-UX01|仍存在|TrainerError 仍打印 error.code 和字段名；日期业务信息存在，但技术码/字段名及一般500的恢复/追踪说明未统一。|T1|正文18；图18(52页)、图19(53页)|
|T05-UX01|仍存在|出勤选项仍为 Intake #id · status 和 session.label，没有课程名及 Session 日期摘要。|T2|正文18；图20(54页)|
|T06-F01|仍存在|已确认分母不同：Trainer 用已有 learnerAttendance.Count 和 learnerResults.Count；Member 用 Intake 全部 sessionIds.Count 和 assessments.Count。可解释原100%/0/0对50%/0/1。|B3,T3|正文19；图21(55页)、图22(56页)|
|C01-UX01|仍存在|editable=false 时只展示锁定提示；PendingApproval/Published 没有完整只读 CourseFields。|C1|正文20|
|C01-UX02|仍存在|CreatorError 仍显示 error.code；加载失败的 retry 未按归属错误区分。CreatorHeader 尚未挂载时会沿用默认 Member 标题。|C2,C1,A2|正文20|
|C01-UX03|仍存在|课程代码放在56px列，index 没有换行约束；正常长度代码可越过18px gap。未做本轮像素复测。|C3,U2|正文20；图23(57页)|
|C02-F01|仍存在|UploadAsync 调用 MaterialVersionService.CreateAsync；后者第56行直接开启事务。课程只检查归属，不限制 PendingApproval，所以不能归因成正常状态拒绝。|B6,B12|正文20；图24(58页)|
|C02-UX01|仍存在|MaterialsController.UploadFile 捕获 InvalidOperationException 后直接返回 UPLOAD_FAILED + ex.Message；CreatorUploadPage 显示该文本。|B6,C4|正文20；图24(58页)|
|C03-UX01|仍存在|CreatorMaterialUsageDto、usage 查询和页面都没有 IntakeId；只补界面也无法定位原始事件。|B6,C5|正文21；图25(59页)|
|C03-UX02|仍存在|visible.length===0 统一使用 Trainer 关联后的空库说明，未区分有记录但筛选无匹配。|C5|正文21|
|C03-OBS01|仍存在|筛选用 UTC 日期片段，展示 new Date(...).toLocaleString 未标时区；标题固定 Creator review。现象存在，不认定瞬时存储错误。|C5,C2|正文21|
|C04-UX01|仍存在|Action required 标题无条件渲染，即使 pending.length=0。|C6|正文21；图26(60页)|
|C05-UX01|仍存在|UserAccountPage 仍有 Edit Profile modal；共享申请面板仍出现 role-requests store, not course materials。|A12,A6|正文21|
|C05-OBS01|产品取舍|资料保存 displayName，顶栏/头像等显示 fullName；两个字段用途未统一，不能将此判为保存失败。|A12|正文22|
|D01-UX01|部分改善|下载 API/文件接口存在，但 AdminRoleRequestsPage 两个 onClick 未 await/catch 或显示成功反馈；MD核查轮 SQLite 附件测试不代表用户浏览器下载完成。|A7|正文23|
|D02-UX01|仍存在|AdminCourseDto 和 Review modal 都没有 LearningOutcomes/LearningPath/完整预览；是DTO到UI共同缺口。|D1,B13|正文23；图27(61页)|
|D02-UX02|仍存在|Creator 建课时 TrainerId=creatorUserId，目录/详情取 Course.Trainer.FullName；UI又固定 Senior Trainer。应按 Creator 与实际 Intake Trainer 分开展示。|B6,M2,M3|正文23；图28(62页)|
|D02-UX03|已实现待线上复测|“无可报名场次仍可点击”已由 disabled={!availability.canReserve} 修正，MD核查轮 no-session UI 测试通过；Certificate included 文案仍由 M03-OBS01 保留。|M3,M5|正文23；图28(62页)|
|D02-OBS01|需环境或数据核对|SubmittedBy 的DTO实际取 Course.TrainerId/Trainer.FullName，未取提交记录或 CreatorId；已确认语义来源错配，历史线上所有权是否正确仍须查数据。|B13|正文23|
|D03-UX01|部分改善|MaterialVersionDto 已有 CourseId/Code/Title/SubmittedAt；卡片现在用 courseCode，旧版本可能显示占位 Course。仍未呈现课程名称/状态、文件名/大小、提交时间。|D2,B12|正文24；图29(63页)|
|D05-UX01|仍存在|User ID 候选只来自当前 query.data 的账本，非全站查询；未显示当前钱包或调整前后预览。|D3|正文24|
|D05-F01|仍存在|路由明确将 /admin/users Navigate 到 /admin/approvals?queue=roles，用户查询页没有实现。|R1|正文24；图30(64页)|
|D05-F02|已实现待线上复测|AdjustAsync 已整段用策略包装事务，幂等key重放、冲突、一次流水/审计的SQLite测试本轮通过；原线上故障根因及SQL回归仍未确认。|B2|正文24；图31(65页)|
|D05-UX02|仍存在|mutationError 与 query.error 同用 AdminDataState，仍说 Records could not be loaded / No changes have been made；Try again 只是清空错误。|D3,D5|正文25；图31(65页)|
|D05-OBS02|需环境或数据核对|服务里只有成功 CreditAdjusted 审计；没有独立的金融失败审计写入。是否要求 Failed事件及线上服务器日志需按审计合同确认。|B2,B5|正文25|
|D06-UX01|仍存在|DTO有 enrollmentId，但界面不显示；DTO也缺 Intake/Session日期及报名状态。|D4,B2|正文25；图32(66页)|
|D06-F01|部分改善|ReviewDisputeAsync 已策略包装、退款状态/钱包/流水/通知/成功审计在同事务，SQLite重复退款只记一次测试通过；SQL/线上未验收，全额退款还未减少 Session.SeatsTaken。|B2|正文25；图32(66页)|
|D06-UX02|仍存在|继续把写操作错误交给 AdminDataState；失败后用旧Open数据提供退款按钮，未先刷新核对结果。同页重试key保持，可减低重复退款，但不能解决误导。|D4,D5|正文25；图32(66页)|
|D06-OBS02|需环境或数据核对|只有成功 DisputeRefunded/Rejected审计；失败金融审计和线下日志覆盖待合同及服务器核对。|B2,B5|正文26|
|E01-F01|仍存在|关联 T02-F01：不同账号/课程仍共用同一个普通 CreateAsync，事务包装未修正。|B4|正文27；图33(67页)|
|E01-UX01|已实现待线上复测|关联 D02-UX03：无场次按钮禁用已有MD核查轮UI覆盖；角色误标仍归 D02-UX02。|M3,M5|正文27；图28(62页)|
|E01-UX02|仍存在|关联 T02-UX01：技术码和通用500反馈仍没有业务结果/追踪编号。|T1,B5|正文27；图33(67页)|
|E02-F01|仍存在|关联 C02-F01：Published 与 PendingApproval 材料共用直接事务的 MaterialVersionService.CreateAsync。|B12,B6|正文27；图34(68页)|
|E02-UX01|仍存在|关联 C02-UX01：上传异常文本仍被 ex.Message 直接返回。|B6,C4|正文27；图34(68页)|
|U01-F01|仍存在|MemberAccountPage 内联 gridTemplateColumns='240px 1fr' 覆盖 max-width:900px 的单栏CSS；新增 Edit interests 也位于不换行的按钮头部。PDF的390px测量表明main可横向滚动，属于挤压/越界，不能说按钮完全不可访问。|M9,U1|正文28；图35(69页)|
|U01-F02|仍存在|审批栏最小列宽160+4×150再加gap，overflow:hidden；两列断点为760px，768px仍使用大布局。PDF测量表明标签超出容器并被裁切，该栏不可横向滚动；与U01-F01的可滚动溢出不同。|U3|正文28；图36(70页)|
|U01-OBS01|需环境或数据核对|窄屏sidebar仍横向滚动，无更多导航提示；真实触摸、表格列可发现性仍须390/768/1440浏览器复测。|U1|正文28|
|U02-F01|仍存在|共享 Modal 没有role=dialog/aria-modal、自动聚焦、Tab循环或关闭后焦点回归。|U4,A5|正文28；图37(71页)|
|U02-F02|仍存在|Modal 仅支持关闭按钮和遮罩点击，无 Escape 监听。|U4|正文28；图37(71页)|

## MD核查轮自动验证记录（PDF复核未重复执行）

2026-10-02先前MD核查轮执行现有测试，未新建行为测试或修改既有测试。下表保留该轮实际输出；本次PDF复核未重新运行这些自动测试，新增验证为PDF正文/截图核对和关键源码重读。

|检查|MD核查轮输出|覆盖边界|
|---|---|---|
|npm.cmd test|退出0：Node 19/19、UI 56/56，共75/75；18个UI文件通过|jsdom/Node，不是390/768真实浏览器、手机或线上验收|
|选定后端回归|退出1：通过139，失败1，总计140|普通用例主要SQLite；SQL Server专用用例因缺LocalDB无法初始化|
|失败用例|AdminRoleRequestIntegrationTests.Approve_Succeeds_WhenSqlServerRetryOnFailureIsEnabled|SqlException error52：无法定位 Local Database Runtime；失败在EnsureDeletedAsync、第209行，未到审批业务|
|后端编译|测试命令构建出Server和Tests DLL|编译成功不等于所有测试通过|

后端构建仍报告 Microsoft.OpenApi 2.0.0 与 SQLitePCLRaw.lib.e_sqlite3 2.1.11 的既有 NU1903 提示。本轮未运行完整后端测试、build/lint前端检查或生产部署检查；读取历史验证记录用于理解进度，没有把历史结果当本轮通过。

后端命令（在v7根目录运行）：

```powershell
$env:Logging__EventLog__LogLevel__Default='None'
$env:Logging__LogLevel__Default='Critical'
dotnet test CoLearnX.Server.Tests/CoLearnX.Server.Tests.csproj --no-restore -p:SkipSpaPublish=true -p:UseAppHost=false --filter 'FullyQualifiedName~EnrollmentApiTests|FullyQualifiedName~CreditReservationWorkflowTests|FullyQualifiedName~MemberCourseAvailabilityTests|FullyQualifiedName~TrainerIntakesIntegrationTests|FullyQualifiedName~CourseIntakeCoreTests|FullyQualifiedName~MaterialApiTests|FullyQualifiedName~LaterPhaseWorkflowIntegrationTests|FullyQualifiedName~B4WorkflowIntegrationTests|FullyQualifiedName~CreatorUsageIntegrationTests|FullyQualifiedName~EmailVerificationApiTests|FullyQualifiedName~PasswordResetApiTests|FullyQualifiedName~AccountMailTests|FullyQualifiedName~AdminRoleRequestIntegrationTests' --logger 'console;verbosity=minimal'
```

前端在 `C:/Users/user/Documents/CoLearnX/.local-deploy/20260929_v7/colearnx.client` 执行 `npm.cmd test`。

现有 LaterPhaseWorkflowIntegrationTests.AdminCreditAdjustment_IsIdempotentAndAudited 验证调整重放只一条流水/审计，DisputeRefund_ChangesBalanceOnceAndClosesCase 验证争议创建与退款、余额、终态、一次退款和冲突拦截。它们使用SQLite，且退款测试没有断言退还SeatTaken；不能据测试通过关闭SQL事务问题或座位问题。

## 待补测和产品规则

- **线上部署与SQL Server**：确认SHA及未提交功能是否进入部署；用有效未来Intake/足额账号回测报名、草稿、上传、争议、调整和退款，逐项查数据库、钱包、座位、流水、通知及审计。源码存在实现不能代替线上通过。
- **退款座位**：ReviewDisputeAsync全额退款只置 Refunded，没有减少CourseSession.SeatsTaken。原复测准备要求核对名额，修复时应明确全额/部分退款与释放座位的规则，并补回归。Reserved在服务端不能争议，但MemberDisputesPage仍把它显示为可选且未排除；这是v7衔接需一并核对的新缺口。
- **真实邮件**：保留原始.eml头和正文，核对text/html、纯文本备用体、实际部署发送器及原手机客户端。AccountMail测试只证明本地构造正确，不证明已解决HTML源码显示、垃圾箱及内嵌浏览器信誉警告。
- **样本数据**：4010学习成果、旧阶段证书、预置进度、材料/录播关联、旧Intake终结状态均查实际线上数据；种子只能解释本地样本。
- **UI浏览器**：390/768/1440复测账户、课程代码长文本、审批导航；键盘验证进入/循环/回归焦点及Esc。共享Modal当前缺失功能已由源码确认，但本轮没有浏览器像素或读屏验收。
- **业务取舍**：单身份自动进入、Display name与Full Name用途、Published课程更新路径、Completed触发、失败金融审计要求需明确。现代码Published后编辑锁定，Admin只有PendingApproval可审核，尚无正式课程改版入口。
- **原先受阻链路**：通知已读/具体Intake跳转、Creator审批、权限下载、证书全链路已有相关实现和现有测试，仍需按原清单补有效样本复测；PayPal Sandbox/Buyer、真实支付至退款及手机行为本轮没有执行，不标成通过。

## 源码索引

- A1：[colearnx.client/src/pages/admin/AdminLoginPage.jsx](../../colearnx.client/src/pages/admin/AdminLoginPage.jsx#L11)。
- A2：[colearnx.client/index.html](../../colearnx.client/index.html#L8)。
- A3：[colearnx.client/src/pages/RegisterPage.jsx](../../colearnx.client/src/pages/RegisterPage.jsx#L42)。
- A4：[colearnx.client/src/pages/EmailVerificationPage.jsx](../../colearnx.client/src/pages/EmailVerificationPage.jsx#L14)。
- A5：[colearnx.client/src/pages/LoginPage.jsx](../../colearnx.client/src/pages/LoginPage.jsx#L43)。
- A6：[colearnx.client/src/pages/account/RoleApplicationsPanel.jsx](../../colearnx.client/src/pages/account/RoleApplicationsPanel.jsx#L135)。
- A7：[colearnx.client/src/pages/admin/AdminRoleRequestsPage.jsx](../../colearnx.client/src/pages/admin/AdminRoleRequestsPage.jsx#L7)。
- A8：[colearnx.client/src/auth/RequireAuth.jsx](../../colearnx.client/src/auth/RequireAuth.jsx#L25)。
- A9：[colearnx.client/src/pages/ResetPasswordPage.jsx](../../colearnx.client/src/pages/ResetPasswordPage.jsx#L10)。
- A10：[colearnx.client/src/api/client.js](../../colearnx.client/src/api/client.js#L13)。
- A11：[colearnx.client/src/pages/account/AvatarEditor.jsx](../../colearnx.client/src/pages/account/AvatarEditor.jsx#L38)。
- A12：[colearnx.client/src/pages/account/UserAccountPage.jsx](../../colearnx.client/src/pages/account/UserAccountPage.jsx#L107)。
- B1：[CoLearnX.Server/Services/EnrollmentService.cs](../../CoLearnX.Server/Services/EnrollmentService.cs#L20)。
- B2：[CoLearnX.Server/Services/AdminFinanceService.cs](../../CoLearnX.Server/Services/AdminFinanceService.cs#L63)。
- B3：[CoLearnX.Server/Services/CertificateWorkflowService.cs](../../CoLearnX.Server/Services/CertificateWorkflowService.cs#L223)。
- B4：[CoLearnX.Server/Services/CourseIntakeService.cs](../../CoLearnX.Server/Services/CourseIntakeService.cs#L81)。
- B5：[CoLearnX.Server/Controllers/LaterPhaseApiErrorsAttribute.cs](../../CoLearnX.Server/Controllers/LaterPhaseApiErrorsAttribute.cs#L26)。
- B6：[CoLearnX.Server/Services/AppServices.cs](../../CoLearnX.Server/Services/AppServices.cs#L441)。
- B7：[CoLearnX.Server/Services/AccountMail.cs](../../CoLearnX.Server/Services/AccountMail.cs#L52)。
- B8：[CoLearnX.Server/Services/EmailVerificationService.cs](../../CoLearnX.Server/Services/EmailVerificationService.cs#L66)。
- B9：[CoLearnX.Server/Data/SeedData.cs](../../CoLearnX.Server/Data/SeedData.cs#L278)。
- B10：[CoLearnX.Server/Services/MemberLearningHubService.cs](../../CoLearnX.Server/Services/MemberLearningHubService.cs#L22)。
- B11：[CoLearnX.Server/Services/IntakeSettlementService.cs](../../CoLearnX.Server/Services/IntakeSettlementService.cs#L85)。
- B12：[CoLearnX.Server/Services/MaterialVersionService.cs](../../CoLearnX.Server/Services/MaterialVersionService.cs#L21)。
- B13：[CoLearnX.Server/Services/AdminCourseReviewService.cs](../../CoLearnX.Server/Services/AdminCourseReviewService.cs#L137)。
- M1：[colearnx.client/src/pages/member/MemberHomePage.jsx](../../colearnx.client/src/pages/member/MemberHomePage.jsx#L31)。
- M2：[colearnx.client/src/pages/member/MemberCatalogPage.jsx](../../colearnx.client/src/pages/member/MemberCatalogPage.jsx#L89)。
- M3：[colearnx.client/src/pages/member/MemberCourseDetailPage.jsx](../../colearnx.client/src/pages/member/MemberCourseDetailPage.jsx#L120)。
- M4：[colearnx.client/src/pages/member/MemberDataContext.jsx](../../colearnx.client/src/pages/member/MemberDataContext.jsx#L29)。
- M5：[colearnx.client/src/pages/member/sessionAvailability.js](../../colearnx.client/src/pages/member/sessionAvailability.js#L4)。
- M6：[colearnx.client/src/pages/member/MemberProgramsPage.jsx](../../colearnx.client/src/pages/member/MemberProgramsPage.jsx#L43)。
- M7：[colearnx.client/src/pages/member/MemberBadgesPage.jsx](../../colearnx.client/src/pages/member/MemberBadgesPage.jsx#L51)。
- M8：[colearnx.client/src/pages/member/MemberDisputesPage.jsx](../../colearnx.client/src/pages/member/MemberDisputesPage.jsx#L35)。
- M9：[colearnx.client/src/pages/member/MemberAccountPage.jsx](../../colearnx.client/src/pages/member/MemberAccountPage.jsx#L37)。
- T1：[colearnx.client/src/pages/trainer/TrainerUi.jsx](../../colearnx.client/src/pages/trainer/TrainerUi.jsx#L18)。
- T2：[colearnx.client/src/pages/trainer/TrainerAttendancePage.jsx](../../colearnx.client/src/pages/trainer/TrainerAttendancePage.jsx#L57)。
- T3：[CoLearnX.Server/Services/TrainerLaterPhaseService.cs](../../CoLearnX.Server/Services/TrainerLaterPhaseService.cs#L198)。
- C1：[colearnx.client/src/pages/creator/CreatorCourseFormPage.jsx](../../colearnx.client/src/pages/creator/CreatorCourseFormPage.jsx#L135)。
- C2：[colearnx.client/src/pages/creator/CreatorUi.jsx](../../colearnx.client/src/pages/creator/CreatorUi.jsx#L15)。
- C3：[colearnx.client/src/pages/creator/CreatorCoursesPage.jsx](../../colearnx.client/src/pages/creator/CreatorCoursesPage.jsx#L26)。
- C4：[colearnx.client/src/pages/creator/CreatorUploadPage.jsx](../../colearnx.client/src/pages/creator/CreatorUploadPage.jsx#L1)。
- C5：[colearnx.client/src/pages/creator/CreatorUsagePage.jsx](../../colearnx.client/src/pages/creator/CreatorUsagePage.jsx#L45)。
- C6：[colearnx.client/src/pages/creator/CreatorIntakeApplicationsPage.jsx](../../colearnx.client/src/pages/creator/CreatorIntakeApplicationsPage.jsx#L18)。
- D1：[colearnx.client/src/pages/admin/AdminCourseReviewsPage.jsx](../../colearnx.client/src/pages/admin/AdminCourseReviewsPage.jsx#L279)。
- D2：[colearnx.client/src/pages/admin/AdminLaterApprovalsPage.jsx](../../colearnx.client/src/pages/admin/AdminLaterApprovalsPage.jsx#L50)。
- D3：[colearnx.client/src/pages/admin/AdminCreditLedgerPage.jsx](../../colearnx.client/src/pages/admin/AdminCreditLedgerPage.jsx#L56)。
- D4：[colearnx.client/src/pages/admin/AdminDisputesPage.jsx](../../colearnx.client/src/pages/admin/AdminDisputesPage.jsx#L58)。
- D5：[colearnx.client/src/pages/admin/AdminDataState.jsx](../../colearnx.client/src/pages/admin/AdminDataState.jsx#L11)。
- R1：[colearnx.client/src/routes/AppRouter.jsx](../../colearnx.client/src/routes/AppRouter.jsx#L152)。
- U1：[colearnx.client/src/styles/member.css](../../colearnx.client/src/styles/member.css#L206)。
- U2：[colearnx.client/src/styles/creator-intakes.css](../../colearnx.client/src/styles/creator-intakes.css#L15)。
- U3：[colearnx.client/src/styles/admin.css](../../colearnx.client/src/styles/admin.css#L236)。
- U4：[colearnx.client/src/components/Modal.jsx](../../colearnx.client/src/components/Modal.jsx#L1)。

其他直接证据：[异常原文返回](../../CoLearnX.Server/Controllers/ApiControllers.cs#L252)、[上传异常返回](../../CoLearnX.Server/Controllers/ApiControllers.cs#L432)、[争议创建事务](../../CoLearnX.Server/Services/AdminFinanceService.cs#L122)、[退款策略包装](../../CoLearnX.Server/Services/AdminFinanceService.cs#L169)、[普通草稿事务](../../CoLearnX.Server/Services/CourseIntakeService.cs#L92)、[材料事务](../../CoLearnX.Server/Services/MaterialVersionService.cs#L56)、[窄屏CSS](../../colearnx.client/src/styles/member.css#L1367)、[审批栏断点](../../colearnx.client/src/styles/admin.css#L692)。
