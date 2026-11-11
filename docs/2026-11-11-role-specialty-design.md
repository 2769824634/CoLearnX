# 角色与专业标签设计

日期：2026-11-11（09:00 按组长决定更新）。实施分工见 `docs/2026-11-11-priority-tasks.md` 板块 1。

## 一句话总结

一个账号只担任一个角色，各司其职。Trainer 和 Creator 在认证时选择 1 到 5 个专业标签。Creator 只负责出课，并按课程专业邀请同标签的已认证 Trainer；**Creator 不授课**。Trainer 可以被邀请，也可以主动申请。

## 规则

1. **一账号一角色。** 每个账号只持有 Member、Trainer、Creator 中的一个。清空现有的多身份账号。
2. **Creator 不授课。** Creator 只能邀请 Trainer。Creator 本人想上课，需要另外注册一个 Trainer 账号，并通过认证。
3. **专业标签。** Trainer 和 Creator 在认证时选择 1 到 5 个标签，管理员批准认证时一并生效。认证后如需修改，要重新提交审核。
4. **课程专业。** Creator 创建课程时选择一个专业，并且只能从自己的标签中选。
5. **按专业匹配。** 邀请 Trainer 时，系统只列出持有该课程专业标签、并且已经认证的 Trainer；后端也会再检查一遍。
6. **两种加入方式。**
   - Creator 邀请，Trainer 接受（现有流程）。
   - Trainer 申请，Creator 同意（新增）。Trainer 只能申请自己标签内的已发布课程。
7. **讲师按班指定。** 开班时，Creator 从课程讲师名单里选一位已加入的 Trainer。

## 权限划分

| 操作 | Member | Trainer | Creator |
|---|---|---|---|
| 浏览、报名课程，申请证书 | ✓ | | |
| 创建、编辑、提交课程 | | | ✓ 自己的课程，限自己的标签 |
| 邀请 Trainer；同意或拒绝 Trainer 的申请 | | | ✓ 自己的课程 |
| 申请教某门课；接受或拒绝邀请 | | ✓ 限自己的标签 | |
| 开班、为该班指定讲师 | | | ✓ 自己的课程 |
| 授课：上课链接、考勤、资料、评估、签发 | | ✓ 被指定为该班讲师时 | |

Trainer 不能修改课程，Creator 不能进入授课页面。这对应 RBAC 里的静态职责分离：出课的人和上课的人永远是两个账号。

## 与代码的对应

| 需要的东西 | 实现 | 状态 |
|---|---|---|
| 专业标签 | 直接使用现有的 12 个 `CourseSubject`（3 个领域，每个领域 4 个科目） | 已完成 |
| 用户的标签 | 新表 `UserSpecialties(UserId, CourseSubjectId)`；认证申请的 `RoleRequest.SpecialtyIds` 在管理员批准时写入 | 已完成 |
| 认证 | `UserRole.CertifiedAt`；未认证返回 403 `CERTIFICATION_REQUIRED` | 已完成 |
| 按标签邀请 | `GET /api/creator/trainers?courseId=`，以及 `InviteAsync` 中的校验 | 已完成 |
| Trainer 申请 | `CourseTrainerStatus` 增加 `Requested` | 未开始 |
| 去掉 Creator 自己加入 | 删除 `CourseTrainerService.InviteAsync` 中"邀请自己即自动接受"的分支 | 未开始 |
| Creator 限标签建课 | 建课和改课时校验 `CourseSubjectId` 属于 Creator 的标签 | 未开始 |
| 清空多身份账号 | 数据脚本；种子里的演示账号不再附带 Member | 未开始 |

## 清空多身份账号

账户清单里有 11 个多角色账号。保留哪个角色，由 A 确认：

| 账号 | 现有角色 | 建议保留 |
|---|---|---|
| gu.yincheng@colearnx.com | Member + Trainer + Creator | Trainer（演示 Trainer） |
| zou.ruiqi@colearnx.com | Member + Trainer + Creator | Creator（演示 Creator） |
| youshengh1@colearnx.xyz | Member + Trainer + Creator | 待定 |
| huang.yousheng@colearnx.com | Member + Creator | Member（演示 Member） |
| zouruiqi5@gmail.com | Member + Creator | Creator |
| 2769824634@qq.com、18267711719@163.com | Member + Trainer | Trainer |
| qa.trainer@colearnx.test、qa.trainer2/3/4@colearnx.test | Member + Trainer | Trainer |

被收回的角色名下如果有课程或班级，要先转给其他账号，否则会变成无人管理。

## 待改的旧文档

`docs/superpowers/specs/2026-11-11-role-signup-certification-design.md`：

- 单一领域 `SubjectArea` 改为 1 到 5 个专业标签，Creator 也要选。
- 注册时只选角色，选标签放到认证时。
- 删除"不做"一节里的"不删除 Staff"。
