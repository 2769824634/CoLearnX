# 统一编辑展示信息，按角色选择显示或隐藏

日期：2026-10-28。只写设计，本文件不包含实现。

会员、培训师、创作者三处「我的账户」使用同一个编辑窗口。窗口一次编辑该用户持有的全部展示信息。每个已持有角色有一个显示开关，只决定该角色的信息是公开还是隐藏。

## 背景

三处入口：

- `/member/account`，`MemberAccountPage`
- `/trainer/account`，`TrainerAccountPage` 传入 `extraKind="trainer"`
- `/creator/account`，`CreatorAccountPage` 传入 `extraKind="creator"`

后两处共用 `UserAccountPage`。编辑窗口按 `extraKind` 决定出现哪些角色字段，保存时也只提交当前页面那一组：

- 培训师页只提交 `trainerHeadline`、`specialisations`
- 创作者页只提交 `creatorHeadline`、`expertiseTags`

同时持有两个角色的人，在其中一个页面保存时，另一组展示文字不会进入请求。服务端对请求里省略的属性保持原值，所以另一组不会被清空，但也改不了。

会员编辑窗口有全名、显示名、电话、简介，以及会员、培训师两个显示开关。没有创作者显示开关，也没有培训师或创作者的展示字段。只读卡片也不展示简介和角色展示文字。

`PUT /api/users/{id}` 已能在一次请求里更新共用资料、培训师资料、创作者资料和 `identityVisibility`。`UserService.UpdateProfileAsync` 只在用户持有对应角色时写入该角色资料；请求属性为 `null` 时不覆盖已保存的值。`identityVisibility` 写入该用户已有 `UserRole.IsVisible`。没有对应角色行时，这个键被忽略。

本设计不增加字段，也不改这套写入规则。要改的是三个入口的编辑窗口和只读展示。

## 目标

从任一「我的账户」打开 Edit Profile，看到并可以修改该用户当前持有角色的全部展示信息。保存一次提交这些内容。

显示开关与字段是否出现无关。隐藏只把该角色标为不公开，已填写的头衔、专长和标签仍保留，下次打开编辑窗口仍然可见、可改。

## 不做什么

- 不在编辑窗口里改邮箱、头像、学习兴趣、通知偏好或角色申请。邮箱继续只脱敏展示。头像仍用现有上传。会员页的「Edit interests」保持独立。
- 不给未持有的角色增加字段或开关。未开通角色在只读区仍显示 Not activated、Hidden / pending。
- 不因为隐藏而删除 `TrainerProfile` 或 `CreatorProfile` 里的文字。
- 不新增公开主页，也不改变课程、邀请或目录现在如何读取培训师和创作者资料。显示状态继续只写 `UserRole.IsVisible`，并由「我的账户」只读区显示 Visible 或 Hidden。
- 不改服务端资料模型，不改 `UpdateProfileRequest`。

## 编辑窗口

三个入口的字段相同，只按该用户已持有的角色取舍。

共用，始终出现：

| 字段 | 请求属性 |
|---|---|
| Full Name | `fullName` |
| Display name | `displayName` |
| Phone | `phone` |
| Bio | `bio` |

持有 Trainer 才出现：

| 字段 | 请求属性 |
|---|---|
| Trainer headline | `trainerHeadline` |
| Specialisations | `specialisations` |

持有 Creator 才出现：

| 字段 | 请求属性 |
|---|---|
| Creator headline | `creatorHeadline` |
| Expertise tags | `expertiseTags` |

显示开关只为已持有角色出现，文案保持 Member visibility、Trainer visibility、Creator visibility。开关状态来自 `identityVisibility`，保存时原样放进 `identityVisibility`。未持有角色的键不出现在窗口里；若草稿里仍有该键，请求可以带上，服务端会忽略没有角色行的键。

只持有 Member 的人，窗口只有四项共用字段和会员开关。同时持有 Trainer 与 Creator 的人，无论从培训师页还是创作者页打开，两组角色字段和三个开关都在。

## 保存

一次 `PUT /api/users/{id}`：

- 始终提交四项共用字段和 `identityVisibility`。
- 持有 Trainer 时提交 `trainerHeadline` 与 `specialisations`，包括用户清空后的空字符串。空字符串是一次明确的清空，不是省略。
- 持有 Creator 时同样提交 `creatorHeadline` 与 `expertiseTags`。
- 未持有的角色不提交对应属性，避免以后有角色行时被空值覆盖。省略与现在的服务端规则一致：`null` 不改已有文字。

保存成功后用返回的用户刷新当前登录资料。会员页、培训师页、创作者页看到的是同一份资料。

## 只读区

个人资料卡片展示全部已保存的展示信息，不按当前工作区删减：

- 共用：全名、显示名、脱敏邮箱、脱敏电话、简介。
- 持有 Trainer：培训师头衔与专长。
- 持有 Creator：创作者头衔与专长标签。

身份可见性卡片仍逐行列出 Member、Trainer、Creator。已持有且 `IsVisible` 为真显示 Visible，已持有且为假显示 Hidden，未持有显示 Hidden / pending。这一列只反映开关，不代替上面的文字。

## 验收

- 只持有 Trainer 的人在 `/trainer/account` 看到培训师字段，看不到创作者字段。
- 只持有 Creator 的人在 `/creator/account` 看到创作者字段，看不到培训师字段。
- 同时持有 Trainer 和 Creator 的人，在 `/trainer/account` 与 `/creator/account` 打开的是同一组字段；改创作者头衔后保存，请求体同时带上原有培训师头衔。
- 把 Creator 设为隐藏后保存，`identityVisibility.creator` 为 false，创作者头衔仍在返回的用户资料里，只读区该行是 Hidden。
- `/member/account` 对同时持有两个角色的人展示两组字段，以及会员、培训师、创作者三个开关。只持有 Member 时，不出现另外两组字段，也不出现另外两个开关。
