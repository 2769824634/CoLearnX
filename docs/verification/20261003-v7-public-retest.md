# v7 首轮 PDF 逐项复测公开记录

本文是根据 v7 本地复测记录整理的 GitHub 可公开逐项复测索引，共覆盖 79 个唯一 PDF 问题编号。原始 PDF、本机路径和本地执行 artifacts 不放入本文。

逐项复测证据窗口为 2026-10-03 14:08–14:39（UTC+8）。该证据集记录 backend 313/313、frontend Vitest 99/99 加 Node 20/20，以及 ESLint/Vite build 通过。SQL Server LocalDB 用例 Approve_Succeeds_WhenSqlServerRetryOnFailureIsEnabled 因 LocalDB 不可用而排除，没有计入通过。Chrome 浏览器复测保留 41 张截图在本地；本文不链接截图或本地 artifacts。

下列数量和结果是 v7 已记录的历史证据。本次文档整理没有运行测试或启动浏览器，也不宣称已完成 Production、SQL Server、SMTP、真实移动设备或外部邮箱验收。上传前再次检查记录见 [20261003-master-publish.md](20261003-master-publish.md)：该记录显示 2026-10-03 20:03–20:05（UTC+8）再次确认 backend 313/313、frontend 119/119（Node 20/20 加 Vitest 99/99）及 lint/build 成功；各项浏览器结果仍来自下午复测窗口。

## 证据字段说明

- **PDF title（PDF 标题）**：对应唯一问题编号的 PDF 标题。
- **Change（修改）**：v7 对该问题的修改，或保留既有行为的记录决定。
- **State / freshResult / evidenceLayer（状态 / 复测结果 / 证据层）**：原始记录中的分类和证据范围。“Local”表示本地 checkout/证据层，不表示 Production。
- **Tests（测试）**：该编号记录的测试文件链接和精确测试方法名。短横线表示该项仅有浏览器、数据/合同核对，或没有映射的自动化测试。
- **Key limitations（关键限制）**：解释结果时必须保留的原始证据边界。

## 汇总

| State（状态） | Count（数量） |
| --- | ---: |
| 本地改善，待外部核对 | 3 |
| 本轮修复（本地） | 65 |
| 环境/数据/规则待核对 | 4 |
| 原已实现，本轮回归 | 7 |

## 逐项复测结果

### S01-F01 — 公开管理员页面暴露演示凭据并预填表单

- **PDF title（PDF 标题）**： 公开管理员页面暴露演示凭据并预填表单
- **Change（修改）**： 移除公开/预填 Admin 凭据，Production 阻断共享演示密码并禁用未轮换种子账号；线上轮换仍需部署环境处理。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [FirstRoundSecurityMailTests.cs](../../CoLearnX.Server.Tests/FirstRoundSecurityMailTests.cs) — Production_rejects_the_shared_demo_admin_password
  - [FirstRoundSecurityMailTests.cs](../../CoLearnX.Server.Tests/FirstRoundSecurityMailTests.cs) — Production_seed_disables_shared_admin_but_preserves_rotated_password
- **Key limitations（关键限制）**：
  - 测试没有连接真实 Production 部署，也没有替用户执行线上密码轮换。
  - 没有验证生产密钥、管理员密码存储和部署配置是否已经在目标服务器更新。

### S01-UX01 — 普通登录页浏览器标题标为 Member

- **PDF title（PDF 标题）**： 普通登录页浏览器标题标为 Member
- **Change（修改）**： 默认标题改为中性 CoLearnX，角色页显示其实际标题。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 浏览器
- **Tests（测试）**：
  - —
- **Key limitations（关键限制）**：
  - 本轮真实 Chrome 标签/页面标题已核对；没有专门 document.title 自动断言。

### A01-UX01 — 弱密码错误缺少具体原因（P3）

- **PDF title（PDF 标题）**： 弱密码错误缺少具体原因（P3）
- **Change（修改）**： 弱密码提交列出具体未满足条件，复用密码规则。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [RegisterPage.test.jsx](../../colearnx.client/src/pages/RegisterPage.test.jsx) — shows the exact password rule that failed and clears it after editing
- **Key limitations（关键限制）**：
  - 用例通过 Testing Library 模拟输入，不覆盖真实 Chrome 原生 required/minLength 校验或移动端键盘行为。

### A01-UX02 — 已修正密码后仍保留不一致提示（P3）

- **PDF title（PDF 标题）**： 已修正密码后仍保留不一致提示（P3）
- **Change（修改）**： 密码和确认栏变化清除旧错误，避免过时提示。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化
- **Tests（测试）**：
  - [RegisterPage.test.jsx](../../colearnx.client/src/pages/RegisterPage.test.jsx) — shows the exact password rule that failed and clears it after editing
- **Key limitations（关键限制）**：
  - 自动用例直接验证 Password 修改后的清除效果；Confirm password 单独清错及原生空值阻断需要 Chrome 手工补测。

### A02-UX01 — 首次注册成功反馈未显示待验证的目标邮箱，用户不容易确认是否填错；建议显示 适当掩码后的目标邮箱

- **PDF title（PDF 标题）**： 首次注册成功反馈未显示待验证的目标邮箱，用户不容易确认是否填错；建议显示 适当掩码后的目标邮箱
- **Change（修改）**： 注册成功展示掩码邮箱及验证邮件的等待/垃圾箱说明。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化
- **Tests（测试）**：
  - [RegisterPage.test.jsx](../../colearnx.client/src/pages/RegisterPage.test.jsx) — masks the target mailbox and explains spam-folder delivery after registration
- **Key limitations（关键限制）**：
  - 自动用例 mock 了 register 响应，没有真实创建账户或发送邮件；实际邮箱投递需外部环境复核。

### MAIL-F01 — 验证邮件正文暴露 HTML 源码（P2，用户截图确认）

- **PDF title（PDF 标题）**： 验证邮件正文暴露 HTML 源码（P2，用户截图确认）
- **Change（修改）**： 邮件统一 UTF-8、plain/HTML alternative，真实序列化回归通过；原手机邮件 App、SMTP 投递未核对。
- **State（状态）**： 本地改善，待外部核对
- **freshResult（复测结果）**： 本地检查通过；外部内容待核对
- **evidenceLayer（证据层）**： 自动化
- **Tests（测试）**：
  - [AccountMailTests.cs](../../CoLearnX.Server.Tests/AccountMailTests.cs) — Verification_mail_is_formal_html_with_button_and_team_signature
  - [AccountMailTests.cs](../../CoLearnX.Server.Tests/AccountMailTests.cs) — Password_reset_mail_is_formal_html_with_button_and_team_signature
  - [AccountMailTests.cs](../../CoLearnX.Server.Tests/AccountMailTests.cs) — Mail_message_uses_display_name_html_body_and_plain_text_fallback
  - [FirstRoundSecurityMailTests.cs](../../CoLearnX.Server.Tests/FirstRoundSecurityMailTests.cs) — Account_eml_has_plain_text_first_and_html_last
- **Key limitations（关键限制）**：
  - 未连接真实 SMTP，也未在 PDF 所示原手机邮件客户端中重新收信。
  - 邮件进入收件箱、垃圾邮件分类、域名信誉和内嵌浏览器行为不能由这些本地 .eml 测试确认。

### MAIL-UX01 — 成功页未提示检查垃圾箱（P3）

- **PDF title（PDF 标题）**： 成功页未提示检查垃圾箱（P3）
- **Change（修改）**： 注册与找回页面补等待、垃圾箱及一次性链接提示。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [RegisterPage.test.jsx](../../colearnx.client/src/pages/RegisterPage.test.jsx) — masks the target mailbox and explains spam-folder delivery after registration
- **Key limitations（关键限制）**：
  - 注册成功文字由组件用例验证；ForgotPassword 垃圾箱/等待提示已在 Chrome 查看。真实投递仍未验证。

### A04-UX01 — 已使用链接与失效/过期链接使用同一通用提示；本账号已经验证，此时要求 Request a new lin

- **PDF title（PDF 标题）**： 已使用链接与失效/过期链接使用同一通用提示；本账号已经验证，此时要求 Request a new lin
- **Change（修改）**： 失效/已用验证链接提示若已验证可直接登录，保留一次性校验。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 本地部分验证；保留明确缺口
- **evidenceLayer（证据层）**： 自动化
- **Tests（测试）**：
  - [EmailVerificationPage.test.jsx](../../colearnx.client/src/pages/EmailVerificationPage.test.jsx) — consumes the email token, removes it from the address, and offers sign in
  - [EmailVerificationApiTests.cs](../../CoLearnX.Server.Tests/EmailVerificationApiTests.cs) — Invalid_email_verification_token_is_rejected_with_stable_error
- **Key limitations（关键限制）**：
  - 自动 UI 用例只覆盖成功验证后的 Sign in；没有专门断言已使用/过期分支的页面文案，服务端用例也不等同于 Chrome 页面验收。

### A05-UX01 — 仅有一个身份的新账号仍需在登录后额外点击 Continue as Member

- **PDF title（PDF 标题）**： 仅有一个身份的新账号仍需在登录后额外点击 Continue as Member
- **Change（修改）**： 单一身份直接进入，多个身份继续使用工作区选择弹窗。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [LoginPage.test.jsx](../../colearnx.client/src/pages/LoginPage.test.jsx) — logs a single-role account in directly without an identity chooser
- **Key limitations（关键限制）**：
  - 单身份真实直达 Member、多身份真实选择 Trainer/Creator 均已操作；身份列表通用键盘行为与弹窗见 U02。

### A10-UX01 — 账号页刷新加载申请时，页面同时显示 Loading applications…、Trainer Not

- **PDF title（PDF 标题）**： 账号页刷新加载申请时，页面同时显示 Loading applications…、Trainer Not
- **Change（修改）**： 申请状态加载时不显示假 Not requested，也不允许重复 Apply。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化
- **Tests（测试）**：
  - [MemberAccountPage.test.jsx](../../colearnx.client/src/pages/member/MemberAccountPage.test.jsx) — does not show application actions or Not requested while requests are loading
- **Key limitations（关键限制）**：
  - 自动用例用 mock Promise 控制加载时序，不验证真实网络延迟、取消请求或后端超时。

### A10-UX02 — 管理员申请表提交时间显示 30 Sept 2026, 04:39 pm，但表格本身没有标明时区 ；香港本次

- **PDF title（PDF 标题）**： 管理员申请表提交时间显示 30 Sept 2026, 04:39 pm，但表格本身没有标明时区 ；香港本次
- **Change（修改）**： 申请及已审时间明确 UTC，表头标时区。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [AdminAttachmentReview.test.jsx](../../colearnx.client/src/pages/admin/AdminAttachmentReview.test.jsx) — labels role submission timestamps explicitly in UTC, including unqualified server timestamps
- **Key limitations（关键限制）**：
  - 自动用例针对角色申请列表；其他 Admin 队列的时间字段需要分别在 Chrome 检查。

### A11-UX01 — 已登录用户访问其他身份工作区时直接返回本身份首页或另一登录入口，没有说明 目标工作区需要其他身份/独立管理

- **PDF title（PDF 标题）**： 已登录用户访问其他身份工作区时直接返回本身份首页或另一登录入口，没有说明 目标工作区需要其他身份/独立管理
- **Change（修改）**： 权限跳转显示原因并可关闭，下一次导航消耗旧提示。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [RequireAuth.test.jsx](../../colearnx.client/src/auth/RequireAuth.test.jsx) — explains the role boundary when redirecting an authenticated user
  - [RoleShell.test.jsx](../../colearnx.client/src/layouts/RoleShell.test.jsx) — shows a role-boundary notice once in the %s shell and consumes route state
  - [RoleShell.test.jsx](../../colearnx.client/src/layouts/RoleShell.test.jsx) — shows and consumes the same notice in the Member shell
- **Key limitations（关键限制）**：
  - 自动用例覆盖普通角色路由和 Member/RoleShell；Admin 独立登录边界及真实浏览器导航仍需 Chrome 复核。

### A12-UX01 — 找回密码反馈和初始说明未提醒检查垃圾邮件；此前验证首封确实进入垃圾箱

- **PDF title（PDF 标题）**： 找回密码反馈和初始说明未提醒检查垃圾邮件；此前验证首封确实进入垃圾箱
- **Change（修改）**： 找回密码提交后提示等待与检查垃圾箱，不泄露账号是否存在。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [PasswordRecovery.test.jsx](../../colearnx.client/src/pages/PasswordRecovery.test.jsx) — requests reset and shows the same privacy-preserving response
- **Key limitations（关键限制）**：
  - 未知测试邮箱在 Chrome 得到中性反馈、掩码邮箱、等待及垃圾箱说明；真实 SMTP 投递未验证。

### A13-UX01 — 用户在手机重置表单输入两次密码时，可见性“小眼睛”只出现一次，回到密码栏 后无法再次显示，不能方便核对两次

- **PDF title（PDF 标题）**： 用户在手机重置表单输入两次密码时，可见性“小眼睛”只出现一次，回到密码栏 后无法再次显示，不能方便核对两次
- **Change（修改）**： 重置密码两栏有稳定显示/隐藏控制；原手机自动填充行为仍需设备复测。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化
- **Tests（测试）**：
  - [PasswordRecovery.test.jsx](../../colearnx.client/src/pages/PasswordRecovery.test.jsx) — offers an independent show and hide control for each reset password field
- **Key limitations（关键限制）**：
  - 自动用例和 Chrome 桌面键盘复核不覆盖原 PDF 中的真实手机自动填充行为。

### A13-UX02 — 密码重置后旧会话被退出，却显示 This account signed in on another de

- **PDF title（PDF 标题）**： 密码重置后旧会话被退出，却显示 This account signed in on another de
- **Change（修改）**： 受保护请求 401 改为中性过期说明，不一律断言其他设备登录。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [api-client.test.js](../../colearnx.client/tests/api-client.test.js) — authenticated 401 on a session API signs the user out
  - [api-client.test.js](../../colearnx.client/tests/api-client.test.js) — login 401 does not treat the failure as a replaced session
- **Key limitations（关键限制）**：
  - Node API 用例验证事件和 token 状态，不验证真实 Chrome 页面文案、网络代理或设备会话。

### A13-UX03 — 已用链接重新打开仍要求填写完整重置表单，若提交阶段才提示失效会使用户白填 ；建议在可行条件下预检查链接状态

- **PDF title（PDF 标题）**： 已用链接重新打开仍要求填写完整重置表单，若提交阶段才提示失效会使用户白填 ；建议在可行条件下预检查链接状态
- **Change（修改）**： 填写前检查 token；已用/过期阻断，检查网络失败允许重试，检查本身不消耗 token。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [FirstRoundPresentationRegressionTests.cs](../../CoLearnX.Server.Tests/FirstRoundPresentationRegressionTests.cs) — Reset_link_status_checks_validity_without_consuming_or_disclosing_an_account
  - [PasswordResetApiTests.cs](../../CoLearnX.Server.Tests/PasswordResetApiTests.cs) — Reset_changes_password_invalidates_session_and_cannot_be_replayed
  - [PasswordResetApiTests.cs](../../CoLearnX.Server.Tests/PasswordResetApiTests.cs) — Expired_forged_and_inactive_user_credentials_are_rejected
  - [PasswordRecovery.test.jsx](../../colearnx.client/src/pages/PasswordRecovery.test.jsx) — removes the token from the address and consumes it only after matching passwords
  - [PasswordRecovery.test.jsx](../../colearnx.client/src/pages/PasswordRecovery.test.jsx) — explains missing credentials and provides a fresh-link route
  - [PasswordRecovery.test.jsx](../../colearnx.client/src/pages/PasswordRecovery.test.jsx) — keeps the reset form available when link preflight is temporarily unavailable
- **Key limitations（关键限制）**：
  - 这些是后端 API 测试，不替代 Chrome 中 ResetPasswordPage 的表单预检和显示结果复核。
  - 没有在真实邮件客户端中点击一次性链接。
  - 自动 UI/API 用例不等同于真实邮件链接、Chrome 网络断线或手机浏览器行为；SQL/线上 token 存储仍需目标环境验收。

### MAIL-OBS02 — 此次手机邮件内嵌浏览器顶部显示“防欺诈盗号，请勿支付或输入相关密码”警 告

- **PDF title（PDF 标题）**： 此次手机邮件内嵌浏览器顶部显示“防欺诈盗号，请勿支付或输入相关密码”警 告
- **Change（修改）**： 收件信誉、垃圾邮件分类及应用内浏览器表现依赖原投递与客户端，未捏造已关闭。
- **State（状态）**： 环境/数据/规则待核对
- **freshResult（复测结果）**： 待外部/数据/规则核对
- **evidenceLayer（证据层）**： 无当前执行证据
- **Tests（测试）**：
  - —
- **Key limitations（关键限制）**：
  - 待外部核对；本轮没有投递、收信或原手机内嵌浏览器证据。

### M01-UX01 — 首页刷新加载时，余额仍 120，但其他统计临时显示 0，并出现 No active programs

- **PDF title（PDF 标题）**： 首页刷新加载时，余额仍 120，但其他统计临时显示 0，并出现 No active programs
- **Change（修改）**： 首页等待成功数据再显示统计或真实空态，失败给重试入口。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [FirstRoundBusiness.test.jsx](../../colearnx.client/src/pages/FirstRoundBusiness.test.jsx) — shows loading rather than empty programs and false zero totals on the dashboard
- **Key limitations（关键限制）**：
  - 自动用例只覆盖组件级 loading 状态，没有在 Chrome 中模拟真实首页请求失败；种子数据是否代表线上状态仍需数据核对。

### M01-OBS01 — Frontend React Bootcamp 已有四阶段证书，日期 2026/4/12 至 7/12；

- **PDF title（PDF 标题）**： Frontend React Bootcamp 已有四阶段证书，日期 2026/4/12 至 7/12；
- **Change（修改）**： 四张预置演示证书和手设进度来源已定位；没有擅自删历史证书或重新发证。
- **State（状态）**： 环境/数据/规则待核对
- **freshResult（复测结果）**： 待外部/数据/规则核对
- **evidenceLayer（证据层）**： 浏览器
- **Tests（测试）**：
  - —
- **Key limitations（关键限制）**：
  - 观察到四张既有证书；资格统计已可见，历史数据来源与线上迁移仍需核对，未重新发证。

### M02-UX01 — 搜索不存在的内容或选择无匹配组合时，结果区域空白，缺少“没有找到匹配课程 ”、修改条件或清除筛选的引导

- **PDF title（PDF 标题）**： 搜索不存在的内容或选择无匹配组合时，结果区域空白，缺少“没有找到匹配课程 ”、修改条件或清除筛选的引导
- **Change（修改）**： 无筛选匹配时有明确说明和清除筛选入口。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [FirstRoundBusiness.test.jsx](../../colearnx.client/src/pages/FirstRoundBusiness.test.jsx) — explains a filtered empty catalog and restores the results after clearing
- **Key limitations（关键限制）**：
  - 自动用例使用内存状态和固定课程，不覆盖真实 API 空响应、接口错误或线上课程目录。

### M03-F01 — 两门课程详情均未显示课程简介、级别、授课模式；用户无法在详情页完整了解内 容与上课方式

- **PDF title（PDF 标题）**： 两门课程详情均未显示课程简介、级别、授课模式；用户无法在详情页完整了解内 容与上课方式
- **Change（修改）**： 详情补简介、级别、路径、授课模式、Creator 和实际 Trainer。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [FirstRoundBusiness.test.jsx](../../colearnx.client/src/pages/FirstRoundBusiness.test.jsx) — shows course definition and actual roles without invented credentials or certificate claims
- **Key limitations（关键限制）**：
  - 自动用例验证单个固定 fixture；真实课程资料、发布数据和各课程角色关联仍需 Chrome 线上数据核对。

### M03-F02 — 4010 的 Learning Outcomes 卡只有标题和空白正文，刷新加载完成后仍为空

- **PDF title（PDF 标题）**： 4010 的 Learning Outcomes 卡只有标题和空白正文，刷新加载完成后仍为空
- **Change（修改）**： 缺学习成果时明确显示未提供；4010 权威课程内容仍待确认，没有编造补齐。
- **State（状态）**： 本地改善，待外部核对
- **freshResult（复测结果）**： 本地检查通过；外部内容待核对
- **evidenceLayer（证据层）**： 自动化
- **Tests（测试）**：
  - [FirstRoundBusiness.test.jsx](../../colearnx.client/src/pages/FirstRoundBusiness.test.jsx) — shows course definition and actual roles without invented credentials or certificate claims
  - [AdminFirstRound.test.jsx](../../colearnx.client/src/pages/admin/AdminFirstRound.test.jsx) — shows learning outcomes and learning path before publishing a course
- **Key limitations（关键限制）**：
  - 自动用例中的 Member fixture 只间接覆盖定义渲染；4010 的线上权威课程资料和发布要求未被本地测试证明。

### M03-F03 — 结束时间早于同一行开始时间，但结束日期省略，也没有时区

- **PDF title（PDF 标题）**： 结束时间早于同一行开始时间，但结束日期省略，也没有时区
- **Change（修改）**： 场次完整显示开始/结束日期及 UTC，跨日不再只有结束时刻。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [FirstRoundBusiness.test.jsx](../../colearnx.client/src/pages/FirstRoundBusiness.test.jsx) — shows both calendar dates and UTC for a cross-day session
- **Key limitations（关键限制）**：
  - 自动用例验证 UTC fixture 的文本输出；没有覆盖真实用户本地时区、报名关闭时间缺失或线上数据错误。

### M03-UX01 — 详情页将学习成果和关键场次挤在左侧窄栏，Session 名称和时间多行折行；右侧 宽栏仅有讲师和费用，剩余

- **PDF title（PDF 标题）**： 详情页将学习成果和关键场次挤在左侧窄栏，Session 名称和时间多行折行；右侧 宽栏仅有讲师和费用，剩余
- **Change（修改）**： 课程详情恢复合理内容宽度，学习成果和场次不再被挤入固定窄栏。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 浏览器
- **Tests（测试）**：
  - —
- **Key limitations（关键限制）**：
  - Chrome 1440px 已查看真实详情布局；没有专门布局自动断言，未覆盖所有课程长文本。

### M03-OBS01 — 两门均显示 Senior Trainer 和 Certificate included

- **PDF title（PDF 标题）**： 两门均显示 Senior Trainer 和 Certificate included
- **Change（修改）**： 移除固定 Senior Trainer/Certificate included，显示实际身份与证书申请条件。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [FirstRoundBusiness.test.jsx](../../colearnx.client/src/pages/FirstRoundBusiness.test.jsx) — shows course definition and actual roles without invented credentials or certificate claims
- **Key limitations（关键限制）**：
  - 自动用例只证明固定文案被移除；真实 Trainer 职级和证书资格规则由服务端及业务数据决定。

### M03-OBS02 — 4010 7月1日场次在本次10月1日测试时已过去，仍显示15 seats left及Enrol Now

- **PDF title（PDF 标题）**： 4010 7月1日场次在本次10月1日测试时已过去，仍显示15 seats left及Enrol Now
- **Change（修改）**： 保留不可报名场次禁用，增加缺失/非法时间保守拒绝覆盖。
- **State（状态）**： 原已实现，本轮回归
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [MemberCourseDetailPage.test.jsx](../../colearnx.client/src/pages/member/MemberCourseDetailPage.test.jsx) — disables reservations for unavailable sessions: %s
  - [FirstRoundBusiness.test.jsx](../../colearnx.client/src/pages/FirstRoundBusiness.test.jsx) — blocks a legacy session with unknown dates before offering a balance top-up
- **Key limitations（关键限制）**：
  - 自动用例覆盖前端状态判断；真实历史记录的时间字段完整性、服务端报名窗口和 SQL Server 行为仍需目标环境核对。

### M04-UX01 — 对明确过去的场次仍展示可用报名按钮和剩余名额，点击后优先要求充值并返回报 名，没有告知场次已结束/报名关闭

- **PDF title（PDF 标题）**： 对明确过去的场次仍展示可用报名按钮和剩余名额，点击后优先要求充值并返回报 名，没有告知场次已结束/报名关闭
- **Change（修改）**： 先判断报名窗口/场次状态，再判断余额；确认飞行中防重复提交。
- **State（状态）**： 原已实现，本轮回归
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [MemberCourseDetailPage.test.jsx](../../colearnx.client/src/pages/member/MemberCourseDetailPage.test.jsx) — disables reservations for unavailable sessions: %s
  - [FirstRoundBusiness.test.jsx](../../colearnx.client/src/pages/FirstRoundBusiness.test.jsx) — sends only one reservation while confirmation is in flight
- **Key limitations（关键限制）**：
  - 自动用例没有连接真实数据库或 SQL Server retry；服务端再次校验、余额和座位原子更新需后端/目标环境测试。

### M04-F01 — 正常界面最终提交返回上述SQL Server执行策略与手动事务不兼容异常，报名没有 完成

- **PDF title（PDF 标题）**： 正常界面最终提交返回上述SQL Server执行策略与手动事务不兼容异常，报名没有 完成
- **Change（修改）**： 保留报名执行策略事务、余额/座位条件更新及一次 Hold；SQL Server 仍待目标环境验收。
- **State（状态）**： 原已实现，本轮回归
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化
- **Tests（测试）**：
  - [CreditReservationWorkflowTests.cs](../../CoLearnX.Server.Tests/CreditReservationWorkflowTests.cs) — EnrolmentHoldsCredits_ThenCancellationReleasesThemOnce
  - [CreditReservationWorkflowTests.cs](../../CoLearnX.Server.Tests/CreditReservationWorkflowTests.cs) — FullPhysicalSessionRejectsHoldWithoutMovingCredits
  - [CourseIntakeCoreTests.cs](../../CoLearnX.Server.Tests/CourseIntakeCoreTests.cs) — LegacyPublishedSession_StillEnrolsOnceWithMatchingCreditLedger
- **Key limitations（关键限制）**：
  - 测试使用 SQLite；没有在真实 SQL Server provider 上验证 retrying execution strategy、锁和并发冲突。
  - 没有把本地 API 结果当作线上部署验收。

### M04-F02 — 页面直接向会员展示数据库/框架类名、方法名及开发修复说明，没有给用户可理解 的失败原因或恢复指引

- **PDF title（PDF 标题）**： 页面直接向会员展示数据库/框架类名、方法名及开发修复说明，没有给用户可理解 的失败原因或恢复指引
- **Change（修改）**： 报名异常不返回原始 InvalidOperationException，显示业务说明和 Reference。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化
- **Tests（测试）**：
  - [EnrollmentApiTests.cs](../../CoLearnX.Server.Tests/EnrollmentApiTests.cs) — Enrol_returns_INSUFFICIENT_CREDITS_when_balance_too_low
  - [FirstRoundErrorBoundaryTests.cs](../../CoLearnX.Server.Tests/FirstRoundErrorBoundaryTests.cs) — Unknown_write_failure_hides_internal_detail_and_retains_trace
  - [FirstRoundErrorBoundaryTests.cs](../../CoLearnX.Server.Tests/FirstRoundErrorBoundaryTests.cs) — Enrollment_unknown_failure_returns_safe_message_and_trace
- **Key limitations（关键限制）**：
  - 未知异常已补控制器级直接调用测试；正常报名 API 和前端反馈各自通过，未在浏览器注入真实服务器未知故障或验证 SQL Server。

### M07-OBS01 — 进度12%、65%及100%的既有课程没有可见材料/录播，可能是预设进度、没有材料 关联或权限过滤；需要负

- **PDF title（PDF 标题）**： 进度12%、65%及100%的既有课程没有可见材料/录播，可能是预设进度、没有材料 关联或权限过滤；需要负
- **Change（修改）**： 旧材料/录播和 Intake 关联、手设进度需数据来源核对；没有凭空关联资源或把进度清零。
- **State（状态）**： 环境/数据/规则待核对
- **freshResult（复测结果）**： 待外部/数据/规则核对
- **evidenceLayer（证据层）**： 浏览器
- **Tests（测试）**：
  - —
- **Key limitations（关键限制）**：
  - 仍观察到既有进度但无关联资源的种子样本；已明确空态，关联数据仍待核对。

### M07-UX01 — 本次两次点击Refresh resources后观察到页面文本保持原空状态，未捕获明确刷 新完成时间/提示

- **PDF title（PDF 标题）**： 本次两次点击Refresh resources后观察到页面文本保持原空状态，未捕获明确刷 新完成时间/提示
- **Change（修改）**： 资源刷新有稳定完成反馈，更新后仍无资源会明确说明。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [FirstRoundBusiness.test.jsx](../../colearnx.client/src/pages/FirstRoundBusiness.test.jsx) — retains completed tab and selected enrollment in the URL and confirms a resource refresh
  - [MemberProgramsPage.test.jsx](../../colearnx.client/src/pages/member/MemberProgramsPage.test.jsx) — loads the selected enrollment learning hub and shows its materials and recordings
- **Key limitations（关键限制）**：
  - 自动用例覆盖成功刷新和空资源文本，不覆盖真实网络失败、旧材料关联或录播数据来源。

### M08-UX01 — 在Completed课程详情位置刷新后回到Active第一门，用户需重新切换寻找原课程

- **PDF title（PDF 标题）**： 在Completed课程详情位置刷新后回到Active第一门，用户需重新切换寻找原课程
- **Change（修改）**： Programs 标签、选中报名与课程写入 URL，刷新和深链接保留选择。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [FirstRoundBusiness.test.jsx](../../colearnx.client/src/pages/FirstRoundBusiness.test.jsx) — retains completed tab and selected enrollment in the URL and confirms a resource refresh
- **Key limitations（关键限制）**：
  - 自动用例使用 MemoryRouter，不等于真实 Chrome 硬刷新和直接粘贴 URL 的完整验收；线上路由服务器回退也需核对。

### M09-UX01 — 资格卡列出完成要求却未显示当前进度，需要返回My Programs核对；考核不足只 显示0/1及全部通过要

- **PDF title（PDF 标题）**： 资格卡列出完成要求却未显示当前进度，需要返回My Programs核对；考核不足只 显示0/1及全部通过要
- **Change（修改）**： 资格卡显示当前进度、所有考核、分数、通过线与未评分状态。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [FirstRoundBusiness.test.jsx](../../colearnx.client/src/pages/FirstRoundBusiness.test.jsx) — shows current certificate progress and ungraded assessments as outstanding requirements
- **Key limitations（关键限制）**：
  - 自动用例验证展示模型；资格是否可申请、历史证书和最终发证仍以服务端规则为准。

### M12-UX01 — 上传前页面只显示Upload avatar，未提前显示允许的PNG/JPEG/WebP类型及2MB限 制，

- **PDF title（PDF 标题）**： 上传前页面只显示Upload avatar，未提前显示允许的PNG/JPEG/WebP类型及2MB限 制，
- **Change（修改）**： 头像入口常驻 PNG/JPEG/WebP、2 MB 限制说明。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 浏览器
- **Tests（测试）**：
  - —
- **Key limitations（关键限制）**：
  - Chrome 390px 账户页已看到常驻类型和2MB说明；本轮没有实际头像文件上传。

### M13-UX01 — 无报名时仍显示可展开的Choose a program，但没有“当前无可申请争议的报名” 等原因说明，用户

- **PDF title（PDF 标题）**： 无报名时仍显示可展开的Choose a program，但没有“当前无可申请争议的报名” 等原因说明，用户
- **Change（修改）**： 无可争议报名时说明前置条件；Reserved 排除并指向保留报名取消流程。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [FirstRoundBusiness.test.jsx](../../colearnx.client/src/pages/FirstRoundBusiness.test.jsx) — explains the dispute prerequisite and excludes reserved enrollments
- **Key limitations（关键限制）**：
  - 自动用例使用固定 enrolled 状态；真实取消、退款和争议资格由服务端数据决定。

### M13-OBS01 — 当前Member侧栏六项不含Disputes，本次通过计划中的直达网址进入；争议入口 在其他页面是否可发现

- **PDF title（PDF 标题）**： 当前Member侧栏六项不含Disputes，本次通过计划中的直达网址进入；争议入口 在其他页面是否可发现
- **Change（修改）**： 保留 Programs 学习中心的争议入口和 enrollmentId 深链接。
- **State（状态）**： 原已实现，本轮回归
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 浏览器
- **Tests（测试）**：
  - —
- **Key limitations（关键限制）**：
  - Chrome 实际从 Programs 的争议入口带 enrollmentId=1 打开并提交，新增 Open 争议可见。

### M13-F01 — 存在可选报名、已填写有效长度说明且提交按钮正常启用时，正常争议提交返回 Internal Server E

- **PDF title（PDF 标题）**： 存在可选报名、已填写有效长度说明且提交按钮正常启用时，正常争议提交返回 Internal Server E
- **Change（修改）**： 争议创建整段在执行策略内，重放复用已建 Open 争议。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [FirstRoundRetryStrategyTests.cs](../../CoLearnX.Server.Tests/FirstRoundRetryStrategyTests.cs) — DisputeCreate_RetriesAfterCommittedAckLoss_WithoutDuplicate
- **Key limitations（关键限制）**：
  - 该测试使用 SQLite、替代 ExecutionStrategyFactory 和事务拦截器模拟 ACK 丢失；不是 SQL Server retrying provider 验收。
  - 没有连接线上数据库或真实网络故障。

### M13-UX02 — 失败只显示Internal Server Error，没有易懂业务说明、是否提交成功/可否重试 的解释或追

- **PDF title（PDF 标题）**： 失败只显示Internal Server Error，没有易懂业务说明、是否提交成功/可否重试 的解释或追
- **Change（修改）**： 争议写失败显示结果未确认及 Reference，不展示 SQL/事务异常。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化
- **Tests（测试）**：
  - [LaterPhaseWorkflowIntegrationTests.cs](../../CoLearnX.Server.Tests/LaterPhaseWorkflowIntegrationTests.cs) — DeterministicDisputeFailureWritesFailedAudit
  - [LaterPhaseWorkflowIntegrationTests.cs](../../CoLearnX.Server.Tests/LaterPhaseWorkflowIntegrationTests.cs) — DeterministicDisputeReviewFailureWritesFailedAuditWithoutFinancialChanges
- **Key limitations（关键限制）**：
  - 通用写失败消息/TraceId 已补过滤器级测试，前端未知结果反馈用受控 mock 验证；未在实际 HTTP 争议服务中注入未知数据库故障。

### T01-OBS01 — 四条Intake交付结束日期已过去，但仍Published，首页将其计入Confirmed delive

- **PDF title（PDF 标题）**： 四条Intake交付结束日期已过去，但仍Published，首页将其计入Confirmed delive
- **Change（修改）**： 到开始时间的结算规则保留；班次 EndsAt 后自动 Completed 的合同尚未明确，不能代替学员人工完成/发证流程。
- **State（状态）**： 环境/数据/规则待核对
- **freshResult（复测结果）**： 待外部/数据/规则核对
- **evidenceLayer（证据层）**： 数据/合同只读核对
- **Tests（测试）**：
  - —
- **Key limitations（关键限制）**：
  - Intake #1/#2 的 EndsAt 为 2026-08-01，状态仍为 Published；#3/#4 已被结算为 Cancelled。没有擅自把所有历史班次改成 Completed。

### T02-F01 — 符合页面日期顺序要求的未来Intake创建草稿返回服务器内部错误，未能取得草稿 记录，阻塞排期、添加Ses

- **PDF title（PDF 标题）**： 符合页面日期顺序要求的未来Intake创建草稿返回服务器内部错误，未能取得草稿 记录，阻塞排期、添加Ses
- **Change（修改）**： 普通草稿创建使用执行策略与稳定 Version，ACK 丢失重放只创建一次。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [FirstRoundRetryStrategyTests.cs](../../CoLearnX.Server.Tests/FirstRoundRetryStrategyTests.cs) — OrdinaryIntakeCreate_RetriesAfterCommittedAckLoss_WithoutDuplicate
  - [CourseIntakeCoreTests.cs](../../CoLearnX.Server.Tests/CourseIntakeCoreTests.cs) — DraftToPending_PersistsSingleHierarchy_AndUserAudit
- **Key limitations（关键限制）**：
  - ACK 丢失用自定义 SQLite 测试策略模拟；不能证明真实 SQL Server retrying provider 已通过。
  - 没有执行目标线上数据库连接测试。

### T02-UX01 — 错误显示Internal Server Error/HTTP_ERROR；倒置日期提示还显示INVALI

- **PDF title（PDF 标题）**： 错误显示Internal Server Error/HTTP_ERROR；倒置日期提示还显示INVALI
- **Change（修改）**： Trainer 日期错误映射可读字段，未知写失败提供恢复说明与 Reference。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [FirstRoundBusiness.test.jsx](../../colearnx.client/src/pages/FirstRoundBusiness.test.jsx) — shows friendly field labels and keeps database details out of workspace errors
  - [FirstRoundErrorBoundaryTests.cs](../../CoLearnX.Server.Tests/FirstRoundErrorBoundaryTests.cs) — Unknown_write_failure_hides_internal_detail_and_retains_trace
- **Key limitations（关键限制）**：
  - 浏览器验证真实草稿保存及 Session 缺地点反馈，组件测试覆盖日期错误；未知写失败用 Trainer 过滤器级测试，未在浏览器制造真实 500。

### T05-UX01 — 出勤页Intake选项只显示编号和Published，Session只显示Session1/2；本页未呈

- **PDF title（PDF 标题）**： 出勤页Intake选项只显示编号和Published，Session只显示Session1/2；本页未呈
- **Change（修改）**： 出勤选择显示课程代码/名称、Intake 和完整 UTC 场次日期。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [FirstRoundBusiness.test.jsx](../../colearnx.client/src/pages/FirstRoundBusiness.test.jsx) — identifies the attendance course and full session schedule
- **Key limitations（关键限制）**：
  - 自动用例使用 mock /api/trainer/intakes、/api/courses 数据；真实 Trainer 课程目录和权限仍需运行环境核对。

### T06-F01 — Trainer学员列表显示出勤100%、考核0/0，当前Member资格显示50%、0/1；刷新 Memb

- **PDF title（PDF 标题）**： Trainer学员列表显示出勤100%、考核0/0，当前Member资格显示50%、0/1；刷新 Memb
- **Change（修改）**： Trainer/Member 使用相同全部场次/考核分母；共同样本均为 50% 和 0/1。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [LaterPhaseWorkflowIntegrationTests.cs](../../CoLearnX.Server.Tests/LaterPhaseWorkflowIntegrationTests.cs) — LearnerStatisticsUseAllIntakeSessionsAndAssessmentsAsDenominators
  - [FirstRoundBusiness.test.jsx](../../colearnx.client/src/pages/FirstRoundBusiness.test.jsx) — counts every required assessment while distinguishing the ungraded count
  - [FirstRoundBusiness.test.jsx](../../colearnx.client/src/pages/FirstRoundBusiness.test.jsx) — shows current certificate progress and ungraded assessments as outstanding requirements
- **Key limitations（关键限制）**：
  - 本轮在同一预览数据库先后以 Member/Trainer 核对 UI2051：两端均进度65%、出勤50%、0/1通过，Trainer 显示0已评分。线上/SQL Server 统计数据仍需验收。

### C01-UX01 — PendingApproval详情锁定同时移除了完整课程定义，剩编号、标题、状态、锁定 提示与材料

- **PDF title（PDF 标题）**： PendingApproval详情锁定同时移除了完整课程定义，剩编号、标题、状态、锁定 提示与材料
- **Change（修改）**： 待审/发布课程完整保留只读定义；Published 提示不承诺不存在的编辑路径。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [FirstRoundBusiness.test.jsx](../../colearnx.client/src/pages/FirstRoundBusiness.test.jsx) — keeps a submitted Course definition visible as read-only
- **Key limitations（关键限制）**：
  - 自动用例覆盖 PendingApproval fixture；不同拒绝原因、真实 Published 权限和线上课程定义仍需浏览器数据核对。

### C01-UX02 — 无归属课程提示还公开显示COURSE_NOT_FOUND技术码及Try again；权限拒绝用“ 未找到”

- **PDF title（PDF 标题）**： 无归属课程提示还公开显示COURSE_NOT_FOUND技术码及Try again；权限拒绝用“ 未找到”
- **Change（修改）**： Creator 加载与保存错误区分，遮蔽技术码，标题展示实际课程。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 本地部分验证；保留明确缺口
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [FirstRoundBusiness.test.jsx](../../colearnx.client/src/pages/FirstRoundBusiness.test.jsx) — shows friendly field labels and keeps database details out of workspace errors
  - [FirstRoundBusiness.test.jsx](../../colearnx.client/src/pages/FirstRoundBusiness.test.jsx) — shows a support reference for an unconfirmed upload without leaking database details
- **Key limitations（关键限制）**：
  - Chrome 不存在课程的404反馈已遮蔽技术码；现有组件测试证明通用错误文本安全，但未单独验证无归属403与保存/加载重试的全部区分。

### C01-UX03 — 课程列表代码区域与标题区域发生文字重叠

- **PDF title（PDF 标题）**： 课程列表代码区域与标题区域发生文字重叠
- **Change（修改）**： 代码列可换行，避免与标题重叠，1440px 实际检查无页面横向溢出。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 浏览器
- **Tests（测试）**：
  - —
- **Key limitations（关键限制）**：
  - Chrome 1440px 实际列表代码列与标题分开，document width=1440；未覆盖所有窄屏列表长文本。

### C02-F01 — 合法小尺寸PNG材料上传失败，未形成可验证的PendingApproval材料记录

- **PDF title（PDF 标题）**： 合法小尺寸PNG材料上传失败，未形成可验证的PendingApproval材料记录
- **Change（修改）**： PendingApproval/Published 共用的材料版本事务纳入执行策略，加入存储补偿与已提交保留。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 本地部分验证；保留明确缺口
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [FirstRoundRetryStrategyTests.cs](../../CoLearnX.Server.Tests/FirstRoundRetryStrategyTests.cs) — MaterialVersionCreate_RetriesByStableFilePath_WithoutDuplicate
  - [FirstRoundPresentationRegressionTests.cs](../../CoLearnX.Server.Tests/FirstRoundPresentationRegressionTests.cs) — Failed_material_submission_removes_the_uploaded_file
  - [FirstRoundPresentationRegressionTests.cs](../../CoLearnX.Server.Tests/FirstRoundPresentationRegressionTests.cs) — Uploaded_file_is_retained_if_submission_committed_before_the_response_failed
  - [MaterialApiTests.cs](../../CoLearnX.Server.Tests/MaterialApiTests.cs) — Small_png_upload_creates_pending_version_with_downloadable_metadata
- **Key limitations（关键限制）**：
  - 合法 PNG 在待审/已发布课程的 HTTP 上传、审核元数据及下载字节已通过；SQLite 自定义策略不替代 SQL Server；浏览器文件选择受扩展 file URLs 权限阻断。生产对象存储未验收。

### C02-UX01 — 上传错误直接公开数据库执行策略、DbContext方法等内部技术细节

- **PDF title（PDF 标题）**： 上传错误直接公开数据库执行策略、DbContext方法等内部技术细节
- **Change（修改）**： 上传失败提示业务限制、未确认结果和 Reference，不透传实现异常。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 本地部分验证；保留明确缺口
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [MaterialApiTests.cs](../../CoLearnX.Server.Tests/MaterialApiTests.cs) — Upload_rejects_disallowed_extension
  - [FirstRoundBusiness.test.jsx](../../colearnx.client/src/pages/FirstRoundBusiness.test.jsx) — shows a support reference for an unconfirmed upload without leaking database details
  - [FirstRoundBusiness.test.jsx](../../colearnx.client/src/pages/FirstRoundBusiness.test.jsx) — shows friendly field labels and keeps database details out of workspace errors
  - [FirstRoundErrorBoundaryTests.cs](../../CoLearnX.Server.Tests/FirstRoundErrorBoundaryTests.cs) — Upload_unknown_failure_returns_safe_message_and_trace
- **Key limitations（关键限制）**：
  - 已补未知上传异常的控制器级 message/TraceId 测试，前端 Reference 文案由组件 mock 验证；浏览器上传动作未能越过文件选择权限，未实际触发服务器上传错误。

### C03-UX01 — 使用记录没有目标Intake编号、名称或详情入口

- **PDF title（PDF 标题）**： 使用记录没有目标Intake编号、名称或详情入口
- **Change（修改）**： 新使用事件记录 IntakeId；DTO/UI 显示 Intake/起止时间；旧未知记录保留 null。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [LaterPhaseWorkflowIntegrationTests.cs](../../CoLearnX.Server.Tests/LaterPhaseWorkflowIntegrationTests.cs) — TrainerDelivery_AttendanceRosterRecordingAndApprovedMaterial_AreIntakeScoped
  - [FirstRoundPresentationRegressionTests.cs](../../CoLearnX.Server.Tests/FirstRoundPresentationRegressionTests.cs) — Usage_schema_upgrade_is_repeatable_and_preserves_legacy_rows
  - [CreatorUsageIntegrationTests.cs](../../CoLearnX.Server.Tests/CreatorUsageIntegrationTests.cs) — Usage_shows_only_owned_material_events_with_course_and_trainer_details
- **Key limitations（关键限制）**：
  - 新挂载事件的 IntakeId=200 由集成测试直接断言；本轮浏览器旧样本显示 Intake unavailable。usage DTO 的全部新日期字段没有逐项新断言；线上旧关联仍需核对。

### C03-UX02 — 无匹配筛选复用了“Trainer关联材料后才出现记录”的空库说明

- **PDF title（PDF 标题）**： 无匹配筛选复用了“Trainer关联材料后才出现记录”的空库说明
- **Change（修改）**： 区分无使用库与筛选无匹配，并提供清除筛选。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [FirstRoundBusiness.test.jsx](../../colearnx.client/src/pages/FirstRoundBusiness.test.jsx) — labels filtered usage separately and offers to clear filters
  - [CreatorUsagePage.test.jsx](../../colearnx.client/src/pages/creator/CreatorUsagePage.test.jsx) — shows recorded material uses and filters the visible records by course
- **Key limitations（关键限制）**：
  - 自动用例覆盖筛选状态，不覆盖真实数据库为空、历史未知关联或多个过滤条件组合。

### C03-OBS01 — 日期筛选明确标UTC，记录时间本身未标时区；不能断言转换错误，后续核对并明 确时间展示口径

- **PDF title（PDF 标题）**： 日期筛选明确标UTC，记录时间本身未标时区；不能断言转换错误，后续核对并明 确时间展示口径
- **Change（修改）**： Usage 筛选和展示统一明确 UTC，页标题与功能一致。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [FirstRoundBusiness.test.jsx](../../colearnx.client/src/pages/FirstRoundBusiness.test.jsx) — labels filtered usage separately and offers to clear filters
  - [CreatorUsagePage.test.jsx](../../colearnx.client/src/pages/creator/CreatorUsagePage.test.jsx) — shows recorded material uses and filters the visible records by course
- **Key limitations（关键限制）**：
  - Chrome 使用记录展示完整UTC时间，与UTC日期筛选说明一致；其他本地时区、边界时间和线上旧数据未逐项复测。

### C04-UX01 — 0 waiting旁仍显示ACTION REQUIRED，与“没有待审批”状态措辞冲突

- **PDF title（PDF 标题）**： 0 waiting旁仍显示ACTION REQUIRED，与“没有待审批”状态措辞冲突
- **Change（修改）**： 0 待审不显示 Action required，加载失败不伪装空队列。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [FirstRoundBusiness.test.jsx](../../colearnx.client/src/pages/FirstRoundBusiness.test.jsx) — removes action-required language from an empty Creator approval queue
- **Key limitations（关键限制）**：
  - 自动用例覆盖成功空队列；真实请求失败、权限不足和 Creator 队列数据需 Chrome/服务端环境核对。

### C05-UX01 — My Account的用户说明含“Edit Profile modal”“role-requests s

- **PDF title（PDF 标题）**： My Account的用户说明含“Edit Profile modal”“role-requests s
- **Change（修改）**： 账户产品文案移除 modal/store 实现术语，说明申请用途。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 浏览器
- **Tests（测试）**：
  - —
- **Key limitations（关键限制）**：
  - Chrome 账户页已核对产品文案；未发现 modal/store 实现术语。

### C05-OBS01 — 修改Display name时个人资料明确显示新值，但顶部及头像下主要名称继续显示 Full Name Z

- **PDF title（PDF 标题）**： 修改Display name时个人资料明确显示新值，但顶部及头像下主要名称继续显示 Full Name Z
- **Change（修改）**： 明确 Full name 与 Display name 的用途，保留合法姓名/显示名字段差异。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 浏览器
- **Tests（测试）**：
  - —
- **Key limitations（关键限制）**：
  - Chrome 已核对 Full name/Display name 用途说明；本轮没有再保存显示名并验证所有角色页顶栏的更新。

### D01-UX01 — 本轮两个附件按钮点击都没有可见成功/失败反馈，也未捕获下载；附件审核是权 限授予的重要依据，需要确认用户浏

- **PDF title（PDF 标题）**： 本轮两个附件按钮点击都没有可见成功/失败反馈，也未捕获下载；附件审核是权 限授予的重要依据，需要确认用户浏
- **Change（修改）**： 附件按钮按对应路径显示；await 下载并展示忙/启动/失败，不声称文件已打开。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 本地部分验证；保留明确缺口
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [AdminAttachmentReview.test.jsx](../../colearnx.client/src/pages/admin/AdminAttachmentReview.test.jsx) — offers only the role evidence file that the request actually declares
  - [AdminAttachmentReview.test.jsx](../../colearnx.client/src/pages/admin/AdminAttachmentReview.test.jsx) — awaits a role download, prevents repeat requests and reports only browser download startup
  - [AdminAttachmentReview.test.jsx](../../colearnx.client/src/pages/admin/AdminAttachmentReview.test.jsx) — handles an evidence download failure with a friendly message and reference
- **Key limitations（关键限制）**：
  - 真实附件下载点击得到 File was not found，错误反馈通过；本地种子附件不存在，成功下载分支未通过浏览器验证。组件测试 mock 下载不等于实际文件保存。

### D02-UX01 — Admin审核弹窗没有显示学习目标、完整Learning path或完整课程预览入口

- **PDF title（PDF 标题）**： Admin审核弹窗没有显示学习目标、完整Learning path或完整课程预览入口
- **Change（修改）**： 课程审批 DTO/UI 提供完整定义、学习成果和学习路径。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [FirstRoundPresentationRegressionTests.cs](../../CoLearnX.Server.Tests/FirstRoundPresentationRegressionTests.cs) — Course_and_admin_review_show_creator_actual_trainers_and_learning_definition
  - [AdminFirstRound.test.jsx](../../colearnx.client/src/pages/admin/AdminFirstRound.test.jsx) — shows learning outcomes and learning path before publishing a course
- **Key limitations（关键限制）**：
  - 后端测试只验证 DTO 字段；Review modal 的可视布局、折行和截图由 Chrome UI 测试负责。
  - 自动用例覆盖 DTO/UI 的预览字段；真实审批列表权限、线上课程内容和提交人数据仍需 Chrome/服务端数据核对。

### D02-UX02 — Member目录将H标为Trainer，详情Trainer Information又称Huang You

- **PDF title（PDF 标题）**： Member目录将H标为Trainer，详情Trainer Information又称Huang You
- **Change（修改）**： Creator 与实际 Intake Trainer 分开，Enrollment 也使用实际授课者。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [FirstRoundPresentationRegressionTests.cs](../../CoLearnX.Server.Tests/FirstRoundPresentationRegressionTests.cs) — Course_and_admin_review_show_creator_actual_trainers_and_learning_definition
  - [FirstRoundPresentationRegressionTests.cs](../../CoLearnX.Server.Tests/FirstRoundPresentationRegressionTests.cs) — Enrollment_identifies_the_actual_intake_trainer
  - [FirstRoundBusiness.test.jsx](../../colearnx.client/src/pages/FirstRoundBusiness.test.jsx) — shows course definition and actual roles without invented credentials or certificate claims
- **Key limitations（关键限制）**：
  - 公开课程 trainerNames 的测试只断言非空，没有逐项断言每个名称都来自所有公开 Intake。
  - 历史线上数据中的 Creator/Trainer 所有权仍需数据核对。
  - 自动用例主要验证 Member 详情；Admin 角色映射和历史课程所有权需要真实 API 数据复核，旧记录可能仍缺关联。

### D02-UX03 — 详情明确“没有published session，Trainer须开Intake并经Creator确认”

- **PDF title（PDF 标题）**： 详情明确“没有published session，Trainer须开Intake并经Creator确认”
- **Change（修改）**： 没有可报名场次时保持禁用，条件说明和回归继续通过。
- **State（状态）**： 原已实现，本轮回归
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化
- **Tests（测试）**：
  - [MemberCourseDetailPage.test.jsx](../../colearnx.client/src/pages/member/MemberCourseDetailPage.test.jsx) — does not crash when a published course has no sessions yet
  - [MemberCourseDetailPage.test.jsx](../../colearnx.client/src/pages/member/MemberCourseDetailPage.test.jsx) — disables reservations for unavailable sessions: %s
- **Key limitations（关键限制）**：
  - 自动用例覆盖前端场景矩阵；真实线上场次状态和后端报名接口仍需目标环境复核。

### D02-OBS01 — Admin既有Published#1-4行Submitted by显示Gu Yincheng，而Z Cr

- **PDF title（PDF 标题）**： Admin既有Published#1-4行Submitted by显示Gu Yincheng，而Z Cr
- **Change（修改）**： 提交人投影改用 CreatorId/Creator；历史线上课程所有权仍需数据核对。
- **State（状态）**： 本地改善，待外部核对
- **freshResult（复测结果）**： 本地检查通过；外部内容待核对
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [FirstRoundPresentationRegressionTests.cs](../../CoLearnX.Server.Tests/FirstRoundPresentationRegressionTests.cs) — Course_and_admin_review_show_creator_actual_trainers_and_learning_definition
- **Key limitations（关键限制）**：
  - 测试使用本地种子/fixture，不能证明线上历史课程的 CreatorId、提交记录和显示姓名已经正确迁移。
  - 该问题的线上历史数据部分仍需人工查询和核对。

### D03-UX01 — 材料卡片只显示“Zou Ruiqi · Course · PDF · version1”，没有实际关联课

- **PDF title（PDF 标题）**： 材料卡片只显示“Zou Ruiqi · Course · PDF · version1”，没有实际关联课
- **Change（修改）**： 审批材料显示课程名/状态、文件名/真实大小和 UTC；缺失值逐项说明。文件名是存储下载名，原始上传名未追溯。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [MaterialApiTests.cs](../../CoLearnX.Server.Tests/MaterialApiTests.cs) — Creator_file_upload_appears_in_admin_pending_material_queue
  - [FirstRoundRetryStrategyTests.cs](../../CoLearnX.Server.Tests/FirstRoundRetryStrategyTests.cs) — MaterialVersionCreate_RetriesByStableFilePath_WithoutDuplicate
  - [AdminAttachmentReview.test.jsx](../../colearnx.client/src/pages/admin/AdminAttachmentReview.test.jsx) — shows material course context, file metadata and submission date in UTC
  - [AdminAttachmentReview.test.jsx](../../colearnx.client/src/pages/admin/AdminAttachmentReview.test.jsx) — states missing material metadata and hides download for a missing attachment
  - [MaterialApiTests.cs](../../CoLearnX.Server.Tests/MaterialApiTests.cs) — Small_png_upload_creates_pending_version_with_downloadable_metadata
- **Key limitations（关键限制）**：
  - 新增 PNG 集成用例断言课程名/状态、文件名、真实大小和下载字节；SubmittedAt 的逐字段后端断言仍未补。UI 组件验证完整 mock DTO；浏览器旧种子缺失字段逐项 Unavailable。文件名是存储下载名，未追溯原始上传名。

### D05-UX01 — 调整只能手填User ID，无账号选择/身份与初值预览

- **PDF title（PDF 标题）**： 调整只能手填User ID，无账号选择/身份与初值预览
- **Change（修改）**： 用户查询覆盖无账本账户，显示可用/保留钱包与调整前后预览。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [FirstRoundPresentationRegressionTests.cs](../../CoLearnX.Server.Tests/FirstRoundPresentationRegressionTests.cs) — Admin_can_find_a_user_without_ledger_entries_and_members_cannot
- **Key limitations（关键限制）**：
  - Chrome 搜索到无账本用户并展示0/0余额和+1预览；没有点击执行积分调整。实际写操作/幂等/失败审计由后端和组件用例验证；SQL Server 未验收。

### D05-F01 — 点击侧栏Users（实际链接/admin/users）后落在/admin/approvals?queue

- **PDF title（PDF 标题）**： 点击侧栏Users（实际链接/admin/users）后落在/admin/approvals?queue
- **Change（修改）**： /admin/users 连接真实查询页和 Admin-only API，不再重定向角色审批。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [FirstRoundPresentationRegressionTests.cs](../../CoLearnX.Server.Tests/FirstRoundPresentationRegressionTests.cs) — Admin_can_find_a_user_without_ledger_entries_and_members_cannot
  - [AdminAuthorizationIntegrationTests.cs](../../CoLearnX.Server.Tests/AdminAuthorizationIntegrationTests.cs) — AdminPolicy_RejectsOrdinaryUserToken
- **Key limitations（关键限制）**：
  - Chrome 实际进入 /admin/users、查到无账本用户并可跳到账本；Admin-only API 权限用例已通过。

### D05-F02 — 明确有效对象H#1、非零小额+1和审核说明齐全，正常管理员调整提交失败，期望 120→121未实现

- **PDF title（PDF 标题）**： 明确有效对象H#1、非零小额+1和审核说明齐全，正常管理员调整提交失败，期望 120→121未实现
- **Change（修改）**： 调整的执行策略、幂等冲突和一次流水/审计继续通过；没有声称 SQL Server 已验收。
- **State（状态）**： 原已实现，本轮回归
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化
- **Tests（测试）**：
  - [LaterPhaseWorkflowIntegrationTests.cs](../../CoLearnX.Server.Tests/LaterPhaseWorkflowIntegrationTests.cs) — AdminCreditAdjustment_IsIdempotentAndAudited
- **Key limitations（关键限制）**：
  - 测试数据库为 SQLite，不能证明 SQL Server provider、线上连接和真实并发冲突已经通过。

### D05-UX02 — 写入失败被描述为“Records could not be loaded/检查连接”，不能区分调整失 败与

- **PDF title（PDF 标题）**： 写入失败被描述为“Records could not be loaded/检查连接”，不能区分调整失 败与
- **Change（修改）**： 写失败与加载失败分开，先检查记录、保留幂等 key，不再声称没有任何变化。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化
- **Tests（测试）**：
  - [AdminFirstRound.test.jsx](../../colearnx.client/src/pages/admin/AdminFirstRound.test.jsx) — does not promise no changes when a financial write result is unknown
  - [AdminFinancialRegression.test.jsx](../../colearnx.client/src/pages/admin/AdminFinancialRegression.test.jsx) — never carries an unknown refund operation into a different case after refresh
- **Key limitations（关键限制）**：
  - 前端用例 mock API 失败和刷新结果，不能证明真实 SQL Server 提交后 ACK 丢失；服务端未知结果需结合日志/目标数据库验收。

### D05-OBS02 — 本次失败没有可见Audit Failed事件；若审计要求覆盖失败的管理员金融操作， 需补充失败追踪与原因

- **PDF title（PDF 标题）**： 本次失败没有可见Audit Failed事件；若审计要求覆盖失败的管理员金融操作， 需补充失败追踪与原因
- **Change（修改）**： 确定业务拒绝追加 Failed 审计；数据库未知结果记录追踪日志，不虚构成功/失败结论。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化
- **Tests（测试）**：
  - [LaterPhaseWorkflowIntegrationTests.cs](../../CoLearnX.Server.Tests/LaterPhaseWorkflowIntegrationTests.cs) — DeterministicCreditAdjustmentFailureWritesFailedAudit
- **Key limitations（关键限制）**：
  - 没有测试注入数据库异常并断言日志中的 unknown outcome/TraceId，也没有验证审计合同是否要求所有未知失败落库。
  - 该测试只覆盖确定的 USER_NOT_FOUND 分支。

### D06-UX01 — Admin争议详情有会员/课程/费用/原因，但未显示Enrollment编号、 Intake/Sessio

- **PDF title（PDF 标题）**： Admin争议详情有会员/课程/费用/原因，但未显示Enrollment编号、 Intake/Sessio
- **Change（修改）**： 争议卡展示报名、Intake、Session、完整 UTC 日期与报名状态。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [LaterPhaseWorkflowIntegrationTests.cs](../../CoLearnX.Server.Tests/LaterPhaseWorkflowIntegrationTests.cs) — DisputeRefund_ChangesBalanceOnceAndClosesCase
  - [FirstRoundRetryStrategyTests.cs](../../CoLearnX.Server.Tests/FirstRoundRetryStrategyTests.cs) — DisputeCreate_RetriesAfterCommittedAckLoss_WithoutDuplicate
- **Key limitations（关键限制）**：
  - Chrome 真实争议详情展示报名、Intake/Session、UTC 日期及状态；未在浏览器执行退款。

### D06-F01 — 有效Open争议、默认全额25和非空说明下，用户提交后出现错误；重访未见退款入 账、争议终态、报名变化、账

- **PDF title（PDF 标题）**： 有效Open争议、默认全额25和非空说明下，用户提交后出现错误；重访未见退款入 账、争议终态、报名变化、账
- **Change（修改）**： 全额退款原子释放物理座位；部分退款不释放；重复请求不二次流水/退款。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化
- **Tests（测试）**：
  - [LaterPhaseWorkflowIntegrationTests.cs](../../CoLearnX.Server.Tests/LaterPhaseWorkflowIntegrationTests.cs) — DisputeRefund_ChangesBalanceOnceAndClosesCase
  - [LaterPhaseWorkflowIntegrationTests.cs](../../CoLearnX.Server.Tests/LaterPhaseWorkflowIntegrationTests.cs) — PartialDisputeRefundKeepsThePhysicalSeat
  - [LaterPhaseWorkflowIntegrationTests.cs](../../CoLearnX.Server.Tests/LaterPhaseWorkflowIntegrationTests.cs) — OnlineFullDisputeRefundDoesNotRequirePhysicalSeat
- **Key limitations（关键限制）**：
  - 测试使用 SQLite；SQL Server/线上事务隔离和并发冲突仍需目标环境验收。

### D06-UX02 — 写操作失败展示“记录无法加载/检查连接”，直接声称无变更，且继续提供 Process refund和Try

- **PDF title（PDF 标题）**： 写操作失败展示“记录无法加载/检查连接”，直接声称无变更，且继续提供 Process refund和Try
- **Change（修改）**： 未确认退款先刷新核对；原争议消失时不自动接续下一案，清空旧理由与金额。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化
- **Tests（测试）**：
  - [AdminFinancialRegression.test.jsx](../../colearnx.client/src/pages/admin/AdminFinancialRegression.test.jsx) — never carries an unknown refund operation into a different case after refresh
- **Key limitations（关键限制）**：
  - 自动用例模拟 API 返回丢失；真实数据库事务、网络重试和 Chrome 真实点击仍需环境验收。

### D06-OBS02 — 失败退款没有可见Failed审计事件，与D05失败观察相似，需确认失败审计要求并 补充追踪

- **PDF title（PDF 标题）**： 失败退款没有可见Failed审计事件，与D05失败观察相似，需确认失败审计要求并 补充追踪
- **Change（修改）**： 退款/拒绝的确定业务失败留 Failed 审计，未知异常保留 trace/server log。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化
- **Tests（测试）**：
  - [LaterPhaseWorkflowIntegrationTests.cs](../../CoLearnX.Server.Tests/LaterPhaseWorkflowIntegrationTests.cs) — DeterministicDisputeReviewFailureWritesFailedAuditWithoutFinancialChanges
- **Key limitations（关键限制）**：
  - 已补真实 HTTP 确定性超额退款失败的 Failed 审计与无金融副作用检查；未注入未知数据库故障并断言服务器日志，不把无法确认的数据库结果伪装为 Failed 落库。

### E01-F01 — 不同Trainer及专用Published课程的有效未来草稿仍返回Internal Server Err

- **PDF title（PDF 标题）**： 不同Trainer及专用Published课程的有效未来草稿仍返回Internal Server Err
- **Change（修改）**： 关联 T02-F01，所有账号/课程共用的草稿事务路径已修复。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [FirstRoundRetryStrategyTests.cs](../../CoLearnX.Server.Tests/FirstRoundRetryStrategyTests.cs) — OrdinaryIntakeCreate_RetriesAfterCommittedAckLoss_WithoutDuplicate
  - [CourseIntakeCoreTests.cs](../../CoLearnX.Server.Tests/CourseIntakeCoreTests.cs) — DraftToPending_PersistsSingleHierarchy_AndUserAudit
- **Key limitations（关键限制）**：
  - 使用 SQLite 自定义执行策略，不代表 SQL Server retrying provider 已通过。

### E01-UX01 — 无场次时可点击Enrol Now，但点击只再提示无法报名

- **PDF title（PDF 标题）**： 无场次时可点击Enrol Now，但点击只再提示无法报名
- **Change（修改）**： 关联无场次禁用行为，真实角色展示另由 D02-UX02 修复。
- **State（状态）**： 原已实现，本轮回归
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化
- **Tests（测试）**：
  - [MemberCourseDetailPage.test.jsx](../../colearnx.client/src/pages/member/MemberCourseDetailPage.test.jsx) — does not crash when a published course has no sessions yet
  - [MemberCourseDetailPage.test.jsx](../../colearnx.client/src/pages/member/MemberCourseDetailPage.test.jsx) — disables reservations for unavailable sessions: %s
- **Key limitations（关键限制）**：
  - 该编号复用 D02-UX03 的 UI 回归；角色误标由 D02-UX02 单独覆盖，真实服务端状态仍需核验。

### E01-UX02 — 正常排期失败仅Internal Server Error / HTTP_ERROR，无明确恢复方式/追踪

- **PDF title（PDF 标题）**： 正常排期失败仅Internal Server Error / HTTP_ERROR，无明确恢复方式/追踪
- **Change（修改）**： 关联 Trainer 写失败文案与 Reference。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化
- **Tests（测试）**：
  - [FirstRoundBusiness.test.jsx](../../colearnx.client/src/pages/FirstRoundBusiness.test.jsx) — shows friendly field labels and keeps database details out of workspace errors
  - [FirstRoundErrorBoundaryTests.cs](../../CoLearnX.Server.Tests/FirstRoundErrorBoundaryTests.cs) — Unknown_write_failure_hides_internal_detail_and_retains_trace
  - [FirstRoundErrorBoundaryTests.cs](../../CoLearnX.Server.Tests/FirstRoundErrorBoundaryTests.cs) — Enrollment_unknown_failure_returns_safe_message_and_trace
- **Key limitations（关键限制）**：
  - 错误组件和 Trainer 过滤器的未知结果/TraceId 已验证；浏览器真实未来草稿成功。未在浏览器注入排期未知服务器故障。

### E02-F01 — 课程从PendingApproval变为Published后，合法PNG正常上传仍返回同一数据库事务 执行

- **PDF title（PDF 标题）**： 课程从PendingApproval变为Published后，合法PNG正常上传仍返回同一数据库事务 执行
- **Change（修改）**： 关联 C02-F01，Published 上传也走相同修复路径。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 本地部分验证；保留明确缺口
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [FirstRoundRetryStrategyTests.cs](../../CoLearnX.Server.Tests/FirstRoundRetryStrategyTests.cs) — MaterialVersionCreate_RetriesByStableFilePath_WithoutDuplicate
  - [FirstRoundPresentationRegressionTests.cs](../../CoLearnX.Server.Tests/FirstRoundPresentationRegressionTests.cs) — Uploaded_file_is_retained_if_submission_committed_before_the_response_failed
  - [MaterialApiTests.cs](../../CoLearnX.Server.Tests/MaterialApiTests.cs) — Small_png_upload_creates_pending_version_with_downloadable_metadata
- **Key limitations（关键限制）**：
  - 与 C02-F01 使用同一真实 PNG API 参数化测试；Published 分支已通过。浏览器文件选择和真实 SQL Server/生产对象存储仍未验收。

### E02-UX01 — 材料上传把SqlServerRetryingExecutionStrategy/DbContext.Da

- **PDF title（PDF 标题）**： 材料上传把SqlServerRetryingExecutionStrategy/DbContext.Da
- **Change（修改）**： 关联上传安全错误和恢复提示。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 本地部分验证；保留明确缺口
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [MaterialApiTests.cs](../../CoLearnX.Server.Tests/MaterialApiTests.cs) — Upload_rejects_disallowed_extension
  - [FirstRoundBusiness.test.jsx](../../colearnx.client/src/pages/FirstRoundBusiness.test.jsx) — shows a support reference for an unconfirmed upload without leaking database details
  - [FirstRoundErrorBoundaryTests.cs](../../CoLearnX.Server.Tests/FirstRoundErrorBoundaryTests.cs) — Upload_unknown_failure_returns_safe_message_and_trace
- **Key limitations（关键限制）**：
  - 同 C02-UX01：未知上传错误已补控制器级测试；Published 上传成功由 PNG API 用例验证，浏览器完整上传仍被文件访问权限阻断。

### U01-F01 — 左侧头像/余额占据大部分宽度，右侧账号资料严重挤压，Edit Profile按钮 left366.6/ri

- **PDF title（PDF 标题）**： 左侧头像/余额占据大部分宽度，右侧账号资料严重挤压，Edit Profile按钮 left366.6/ri
- **Change（修改）**： 去掉账户内联双栏，900px 下单栏和按钮换行；390px 文档宽度为 390。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 浏览器
- **Tests（测试）**：
  - —
- **Key limitations（关键限制）**：
  - Chrome 390×844 模拟视口实际量测账户布局无页面横向溢出；未替代真实手机触摸测试。

### U01-F02 — 四个审批标签与左标题同行，末标签left646.7/right796.7，外容器clientWidth7

- **PDF title（PDF 标题）**： 四个审批标签与左标题同行，末标签left646.7/right796.7，外容器clientWidth7
- **Change（修改）**： 768px 审批栏两列完整显示，修复绝对定位辅助标签造成的额外溢出；文档宽度为 768。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 浏览器
- **Tests（测试）**：
  - —
- **Key limitations（关键限制）**：
  - Chrome 768×1024 模拟视口实际量测四审批标签两列可见；未替代真实平板触摸测试。

### U01-OBS01 — 四角色窄屏横向导航无明显向右更多内容指引，Creator在768仍需横向看My Account；宽表格手

- **PDF title（PDF 标题）**： 四角色窄屏横向导航无明显向右更多内容指引，Creator在768仍需横向看My Account；宽表格手
- **Change（修改）**： 窄屏壳增加更多导航/横向滚动提示和 aria-describedby；真实手机触摸尚未操作。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 本地部分验证；保留明确缺口
- **evidenceLayer（证据层）**： 浏览器
- **Tests（测试）**：
  - —
- **Key limitations（关键限制）**：
  - 本轮模拟窄屏检查 Member 账户和 Admin 审批，导航规则/角色壳由对应代码与测试支持；未逐个角色完成全部窄屏表格、触摸和屏幕阅读器验收。

### U02-F01 — 身份选择弹窗打开时未将焦点移入，且未限制Tab/Shift+Tab在弹窗内循环

- **PDF title（PDF 标题）**： 身份选择弹窗打开时未将焦点移入，且未限制Tab/Shift+Tab在弹窗内循环
- **Change（修改）**： Modal 补 dialog/aria-modal、自动聚焦、Tab/ShiftTab 循环与关闭后回到触发按钮。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 对应本地检查通过
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [Modal.test.jsx](../../colearnx.client/src/components/Modal.test.jsx) — sets dialog semantics, focuses the first control, traps Tab, and restores focus on close
- **Key limitations（关键限制）**：
  - 真实键盘核对 Edit Profile 的初始聚焦、Tab/Shift+Tab循环及焦点返回；多角色选择器实测初始焦点在选项。通用 Modal 测试不代替每个角色弹窗的完整辅助技术验收。

### U02-F02 — 身份选择弹窗不响应Esc

- **PDF title（PDF 标题）**： 身份选择弹窗不响应Esc
- **Change（修改）**： Escape 关闭顶层 Modal，嵌套弹窗不同时关闭。
- **State（状态）**： 本轮修复（本地）
- **freshResult（复测结果）**： 本地部分验证；保留明确缺口
- **evidenceLayer（证据层）**： 自动化 + 浏览器
- **Tests（测试）**：
  - [Modal.test.jsx](../../colearnx.client/src/components/Modal.test.jsx) — closes on Escape and restores the trigger focus after the modal unmounts
- **Key limitations（关键限制）**：
  - 真实 Esc 关闭 Edit Profile 并返回 Edit Profile 按钮；身份选择器共用 Modal 的 Esc 行为由通用测试支持，本轮未单独按 Esc 关闭角色选择器。

## 公开验证边界

上述本地证据支持各项所标证据层中的 v7 修改结论，但不能证明 SQL Server retrying provider 已通过、Production 部署行为正常、外部 SMTP 或邮箱投递成功、真实移动设备交互已完成，也不能排除历史数据不一致。浏览器截图集仍只保留在工作树中供项目维护者查看。
