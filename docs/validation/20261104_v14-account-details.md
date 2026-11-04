# 20261104_v14 员工与用户详情验证

日期：2026-11-04（Asia/Shanghai）。分支：20261104_v14；起点提交：22001ab。

## 已实现

- Admin / Staff：点击员工整行或姓名打开详情，展示姓名、公开名称、编号、联系方式、状态、创建时间、上传文件、负责课程、班级状态与起止时间。
- Staff：新增 My Account 与首页个人信息卡片；本人资料与文件接口从当前登录身份读取员工 ID。
- Admin：可重置员工密码，新密码只在当前操作中显示；保存 BCrypt 哈希、更新会话标识并写入审计。原密码无法还原，不返回密码哈希。
- Users：整行点击打开个人资料，包含联系方式、角色、个人简介、学习与专业信息、可用和冻结学分。
- Credit ledger 整合到 Users；用户详情按准确 userId 查询账本并锁定调整账户。保留全局交易视图；旧 /admin/ledger 链接跳转到 Users。
- 学分调整保留原有幂等键与原因要求，成功后更新资料余额、账户列表和账本，并保留操作结果提示。

## 验证证据

- RED：新增员工详情和用户详情后端测试在实现前失败；员工行点击测试在实现前因缺少详情弹窗失败。
- 服务端构建：dotnet build CoLearnX.Server --no-restore -p:BuildProjectReferences=false --nologo -v quiet，通过。
- 服务端测试：dotnet test CoLearnX.Server.Tests --no-restore -p:BuildProjectReferences=false --filter "FullyQualifiedName~PlatformStaffIntegrationTests|FullyQualifiedName~LaterPhaseWorkflowIntegrationTests|FullyQualifiedName~AdminAuthorizationIntegrationTests" --nologo -v quiet，35/35 通过。
- 前端 npm test：Node 24/24，Vitest 317/317 通过。
- 最后一次学分成功提示修复后，AdminUsersPage、AdminStaffPage、AdminLoginPage、AdminFinancialRegression 共 7/7 测试再次通过。
- 最终 npm run build 通过；全部修改的 JS/JSX 文件单独 ESLint 检查通过；git diff --check 通过。
- 全项目 npm run lint 未通过：8 errors / 2 warnings，均在未修改文件 useCachedQuery.js、UserAvatar.jsx、RoleApplicationsPanel.jsx、MemberCourseDetailPage.jsx、MemberDataContext.jsx、memberDataState.js。
- Chrome + 隔离 SQLite 实测：点击用户余额单元格打开详情，账本只显示所选用户；员工整行打开详情；员工本人首页卡片与 My Account；旧账本链接带 userId 跳到对应用户详情。
- 授权测试覆盖未登录、员工访问 Admin 被拒绝、缺失记录、受保护文件下载、重置后旧会话失效与新密码登录。课程关联测试验证新建员工课程出现在详情。

## 审查与边界

仅审查本次 diff；复用原有 API client、Modal、角色授权、文件存储及财务服务。未修改数据库结构、角色职责或 Course → CourseIntake → CourseSession 层级。

SQL Server、Azure Blob 与线上部署未实测。构建仍报告已有依赖安全公告及前端分包大小提示；本轮未升级依赖。

状态：本地工作区修改，未提交、未推送、未部署。VERSION 在开始时已为 20261104_v14，保留该命名。


## 补充修复：角色拒绝按钮与注册密码显示

- 将拒绝按钮样式限定为 `.btn.admin-reject-button`，避免被随后加载的通用 `.btn` 覆盖；补充悬停及键盘焦点颜色。
- 注册页 Password / Confirm password 复用 PasswordInput，分别提供显示与隐藏按钮，默认隐藏。切换不提交表单、不改变输入值。
- 新增密码可见性测试实现前失败（缺少 Show password），修复后注册、PasswordInput、审核附件相关 17 项测试全部通过。
- 本次修改的 RegisterPage.jsx / RegisterPage.test.jsx ESLint 检查通过；npm run build 与 git diff --check 通过。
- Chrome 本地隔离测试：修复前拒绝按钮为白底深字，修复后计算颜色为 rgb(239,68,68) / rgb(255,255,255)；两个密码框均实测 password → text → password。
- 审查确认仅改变界面样式和可见性，不涉及注册接口、密码规则、角色决定或提交逻辑。
- 修改仍保存在 20261104_v14，本次未提交、推送或部署。


## GitHub 交付与本地启动

- 新增 scripts/Start-Local.ps1；默认在 http://127.0.0.1:5144 提供构建后的前后端，使用 artifacts/local-preview 下的独立数据库和文件。
- 已完整运行脚本完成前后端构建，启动日志确认 Testing、Local storage、独立 SQLite，HTTP /health 返回 ok，/register 返回 200 且包含应用入口。
- README 补充本地启动、开发模式和按 20261104_v14 分支克隆的步骤。
- 上文“未提交、未推送”为各次本地验证结束时的状态；本轮将这些改动作为 20261104_v14 交付到现有 GitHub 仓库的同名分支。远端最终提交以 GitHub 分支记录为准。
