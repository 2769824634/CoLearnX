# 20261030_v12 实现与验收记录

验收日期：2026-10-30（Asia/Shanghai）。工作目录：`.local-deploy/20261030_v12`；分支与 `VERSION`：`20261030_v12`。起点提交：`df32c8bd33170f38dd347233d4747c27405156df`。本轮修改通过 GitHub 同名独立分支交付，未合并到 `master`，未部署。

## 实现结果

- Admin 侧边栏新增 **Class dates**，路径 `/admin/class-dates`。可查看平台和 Creator 的班次，并编辑教学起止日期。
- Staff 从 **Classes → Open → Edit teaching dates** 编辑自己负责的平台班次。
- 可编辑 Draft、Rejected、Published、InProgress；Completed、Cancelled、PendingApproval 保持只读。已有 Reserved / Active 学员不阻止教学日期窗口调整。
- 开课日期不得早于原报名截止时间，结束时间须晚于开课时间，日期窗口必须包含全部既有 Session。保存不移动 Session，不修改报名、学员、学分、审核或完成状态。
- 表单按浏览器本地时区显示首个/最后教学日，最后一天包含全天；修改后的结束时间保存为次日本地零点对应的 UTC。未修改字段保留原始时间精度。
- 保存携带 `version`；冲突返回 409，并在界面提供 **Reload class**。审计动作是 `CourseIntakeTeachingDatesUpdated`，记录正确的 AdminAccountId / StaffAccountId 与前后时间。
- Member 的 **Edit Profile / Edit interests** 使用专属紧凑布局，按钮高 34px、字号 13px、间距 8px。Trainer / Creator 共用资料页的 Edit Profile 同步使用该尺寸；点击行为保持原样。
- 通用 `Modal` 统一 portal 到 `document.body`，保留 Escape、Tab 焦点约束与关闭后焦点恢复；多个弹窗共享滚动锁。删除 ProfileEditor 自己的重复 portal，并约束小窗口中的弹窗高度。

## 接口范围

用户已明确同意“允许新增，现有接口保持不变”。只新增以下 HTTP 能力：

| 方法 | 路径 | 用途 |
| --- | --- | --- |
| GET | `/api/admin/courses/intakes` | Admin 班次日期列表 |
| GET | `/api/admin/courses/intakes/{courseIntakeId}` | Admin 班次详情 |
| PATCH | `/api/admin/courses/intakes/{courseIntakeId}/teaching-dates` | Admin 更新教学日期 |
| PATCH | `/api/staff/intakes/{courseIntakeId}/teaching-dates` | Staff 更新自己平台班次的教学日期 |

PATCH 请求字段仅为 UTC `startsAt`、UTC `endsAt`、`version`，不允许额外字段。响应复用 `CourseIntakeDetailDto`。原有 HTTP 路径、请求与响应 DTO 字段、前端 API 方法均未改名或重写。`ApiDtos.cs`、`CourseIntakeDtos.cs`、`api/client.js` 无 diff，无数据库结构变更。

## 自动验证

| 检查 | 结果 |
| --- | --- |
| 服务端 `dotnet build ... --no-restore -p:SkipSpaPublish=true` | 通过，0 errors；现有依赖产生 4 个警告 |
| 新增 `IntakeTeachingDatesIntegrationTests` | 13/13 通过 |
| 现有相关服务端回归 | 86/86 通过；与最初 10 个新增用例一起执行时为 96/96 |
| 前端全量 `npm run test:ui` | 68 个测试文件，314/314 通过 |
| 共享日期逻辑抽取后的相关组件测试 | 3 个文件，8/8 通过 |
| `npm run test:node` | 24/24 通过 |
| 前端 `npm run build` | 通过；已有 SignalR 注解和大文件提示 |
| 本次新增、修改的 JS / JSX 文件 ESLint | 通过 |
| 全量 `npm run lint` | 未通过：8 errors / 2 warnings，均位于本轮未修改文件 |
| `git diff --check` | 通过 |

现有服务端回归范围：PlatformStaffIntegrationTests、AdminCourseReviewIntegrationTests、CourseIntakeCoreTests、TrainerIntakesIntegrationTests、StaffDeliveryRouteTests。

全量 ESLint 原有问题所在文件：

- `src/api/useCachedQuery.js`：1 error。
- `src/components/UserAvatar.jsx`：1 error。
- `src/pages/account/RoleApplicationsPanel.jsx`：1 error。
- `src/pages/member/MemberCourseDetailPage.jsx`：1 error。
- `src/pages/member/MemberDataContext.jsx`：4 errors。
- `src/pages/member/memberDataState.js`：2 warnings。

## 浏览器验收

使用 Chrome、独立临时 SQLite 数据及本地服务完成以下操作；未连接生产数据库：

1. Member 账户页：读取实际按钮尺寸，Edit Profile 为约 90×34px，Edit interests 为约 102×34px。
2. 默认桌面窗口 2504×1355 与手机窗口 390×844：资料弹窗遮罩范围均等于 `[0, 0, viewportWidth, viewportHeight]`，portal 父级为 BODY；打开时 body overflow 为 hidden。390px 下按钮正常排列，没有页面横向溢出。
3. Admin：在 Class dates 打开进行中的测试班次，把最后教学日保存为 2026-11-15；列表显示成功提示与新日期。数据库写入 EndsAt `2026-11-15 16:00:00` UTC，Admin 审计身份正确。
4. Staff：在同一班次详情把开课日改为 2026-10-27、最后教学日改为 2026-11-16；保存后刷新，日期仍保留，状态仍为 InProgress。数据库保存 StartsAt `2026-10-26 16:00:00` UTC、EndsAt `2026-11-16 16:00:00` UTC；原 Session 保持 2026-10-28 01:00–03:00 UTC；Staff 审计身份正确。

截图：

- [Member 按钮](member-buttons.png)
- [手机账户页](member-mobile.png)
- [资料弹窗](profile-modal.png)
- [手机资料弹窗](profile-mobile.png)
- [Admin 日期弹窗与全屏遮罩](admin-date-modal.png)
- [Admin 保存成功](admin-date-saved.png)
- [Staff 日期弹窗](staff-date-modal.png)
- [Staff 保存成功](staff-date-saved.png)

## 审查与边界

已按本次 diff 检查正确性、可读性、架构、角色权限、审计、性能和可用性；未发现待处理的阻断问题。Admin 继续使用独立 Admin policy；Staff 继续使用独立 Staff policy，服务端校验 active account、班次所有权和平台归属。新增日期管理不提供班次审核操作。

业务模型继续使用 Course → CourseIntake → CourseSession。Admin / Staff 复用同一表单与保存逻辑，公共 API 继续复用现有 `apiRequest`，没有第二套传输层。SQL Server 与生产环境未做运行验证；本轮没有数据库结构变更。

验收结束后关闭本轮临时服务、清理临时数据库和本轮本地邮件样本；保留本记录及截图作为可复查证据。
