# 角色与专业标签设计

日期：2026-11-11。一个账号只担任 Member、Trainer、Creator 中的一个角色。Trainer 和 Creator 在认证时选择 1 到 5 个专业标签；Creator 只负责出课，不授课。

## 已实现规则

- 演示账号种子固定为单角色；`scripts/single-role-cleanup.sql` 清理已知多角色账号。无法确认用途的 `youshengh1@colearnx.xyz` 按最小权限保留 Member。
- Creator 只能从自己的 `UserSpecialties` 中选择课程专业；创建和修改接口都会再次校验。
- Creator 只能邀请已认证且专业匹配的 Trainer，不能邀请自己。
- Trainer 可以申请自己专业内的已发布课程；`CourseTrainerStatus.Requested` 表示等待 Creator 审批。
- Creator 可以在自己的课程中接受或拒绝申请；接受后 Trainer 才进入可开班讲师名单。
- 开班时仍从课程中状态为 `Accepted` 的 Trainer 里指定讲师。

## Staff 下线

Staff 的认证、接口、服务、页面、路由、管理员入口与测试已删除。历史兼容层仍保留 `StaffAccounts`、`AuditLogs.StaffAccountId`、`StaffAccount` 实体和 `PlatformStaffSchema`；初始化时会停用 Staff 对应的隐藏用户并轮换会话标记。已有平台课程继续通过公共课程目录显示，但不再提供新建入口，`/member/platform` 独立页面已删除。
