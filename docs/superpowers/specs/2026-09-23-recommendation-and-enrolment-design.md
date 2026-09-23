# CoLearnX 升学推荐、积分冻结与开班确认

Date: 2026-09-23  
Status: draft, pending review  
Replaces: `2026-09-23-course-recommendation-design.md`、`2026-09-23-grab-wallet-enrolment-design.md`（内容已并入本文，实现以本文为准）

三块一起做：

1. **升学推荐** — 从某一方向的 Beginner 学到 Advanced  
2. **Grab 钱包** — 报名先冻结积分，开班再实扣  
3. **腾讯云开班规则** — 最低人数、截止判定、退费比例、Trainer 满员通知，并补现行报名漏洞  

PayPal 只负责充值进钱包，不对单次报名做卡授权。

---

## 1. 目标

### A. 智能升学推荐

- 预置英文两级兴趣树（大类 → 叶子），`SeedData` 一次写入，不必 Admin 逐条创建。
- 注册后 3 步引导（可 Skip）；首页推匹配兴趣的 **高星 Beginner**。
- Creator 给课打 1–4 个叶子标签；级别仍用 `CourseLevel`（Beginner / Intermediate / Advanced）。
- 学完才能打 1–5 星；星级只排序，不决定能不能升学。
- 已完成某门课：够格推同标签下一级；不够格推同标签同级。规则代码，无 AI。

### B. Grab 钱包

- Enrol = **Hold**：可用减少、冻结增加、占座、状态 `Reserved`。
- 开班确认 = **Capture**：冻结清零、可用不变、状态 `Active`。
- 人数不足或确认前取消 = **Release**：冻结立刻回可用。
- 顶栏主数字是可用积分，另标 On hold。

### C. 腾讯云开班 + 补漏洞

对照 [腾讯云产业互联网学堂报名指引](https://cloud.tencent.com/document/product/658/41043)。

- 默认最低 **10** 人；报名截止必须 ≤ 开课前 10 天；截止日全班判定。
- 不够人数：全额解冻；Trainer 7 天内可开延期班，原学员可再占位（不另扣「延期次数」）。
- 学员自退：确认前 100%；确认后距开课 5–10 天 **70%**，≤5 天不可退。平台取消始终 100%。
- 过期、满员、未开放、过线下预订截止：后端拒绝，按钮禁用。
- 开课前 7 天再发时间/地点或会议链接。
- **Trainer 通知**：满员、又空出、已达最低人数、截止前仍不足、开班/取消；Creator 收开班与整班取消。Trainer/Creator 顶栏加铃铛。

### 1.1 现行代码要补的漏洞

| 漏洞 | 现在 | 补完后 |
|------|------|--------|
| 报名不检查开放/截止 | 过期仍能报 | `REGISTRATION_NOT_OPEN` / `CLOSED` |
| 不检查线下预订截止 | `PhysicalBookingDeadline` 形同虚设 | `PHYSICAL_BOOKING_CLOSED` |
| 当场扣光积分 | 人数不足只能事后争议退 | Hold → Capture / Release |
| 无最低开班人数 | 1 人也会「上课」 | 默认 min 10，截止日判定 |
| 无开班确认通知 | 只有 Enrolment confirmed | 确认开班 / 未开班已退回 / 开课前提醒 |
| Trainer 收不到满员等运营通知 | 通知 API `RequireMember`；RoleShell 无铃铛 | 满员、达最低人数、截止前人数不足、结算结果推给 Trainer |
| 详情没有截止、满员、已取消 | 只有 Full / Online | open / full / closed / cancelled |
| 最后席位并发未测 | 可能超卖或双扣 | 条件更新占座，失败不冻积分 |
| 线上容量 0 | 文档仍写「永远报满」，代码已当不限 | 0 = 不限人数；仍受最低开班人数 |
| 无升学推荐 | 首页靠 Featured | 兴趣冷启动 + 标签升学 |
| 兴趣引导未接 | `Interest` 表空，注册无 onboarding | UC-03 三步 + 预置词表 |

---

## 2. 非目标

- 向量模型、大模型、协同过滤。
- 把级别做成 `#初级` 标签。
- 证书签发才能升学。
- 未登录游客推荐。
- 删除或替换现有 `LearningPath`、`Course.Category`（不再作为推荐轴）。
- Admin 词表编辑台（词表以种子为准）。
- 报名时 PayPal / 银行卡 pre-authorization（卡授权约 7 天会过期）。
- Grab 的「取消后授权再留 30 分钟」、Auto Top-Up。
- 候补名单、打电话。
- Reserved 期间进 Learning Hub、评分、申请证书。

---

## 3. 已锁定规则

| 项 | 决定 |
|---|---|
| 词表 | 两级英文；Creator/学员只能点选叶子 |
| 级别 | `CourseLevel`：Beginner → Intermediate → Advanced |
| 推荐引擎 | 规则筛选 + 打分，无 AI |
| 评分 | 复用 `ProgramRating`；学完才能评；每人每课一票 |
| 新用户 | 匹配兴趣的高星 Beginner；Skip 则全站高星 Beginner |
| 升学轴 | **已完成课程的叶子标签**，不是注册时勾的全部兴趣 |
| 升学门槛 | `Completed` 且进度 100%；有 Assessment 则须全部及格 |
| 推荐候选课 | Published，且有**可报名** Intake（窗口内、未取消；线下至少一节未满） |
| 钱包 | 可用 `CreditBalance` + 冻结 `HeldCredits` |
| 最低人数 | 默认 10，Trainer 可改 2–200 |
| 确认日 | `RegistrationClosesAt`，且必须 ≤ 开课前 10 个**自然日** |
| 自退比例 | 确认前 100% Release；确认后 5–10 天 70%；≤5 天不可退 |
| 平台取消 | 始终 100% |
| 占座 | Reserved + Active 都计入 `SeatsTaken` |
| PayPal | 只 TopUp |

Hobby vs Professional 只写入 `UserPreference.LearningGoals`，不加权推荐。

---

## 4. 对照

### Grab 钱包

| Grab | CoLearnX |
|------|----------|
| GrabPay Credits | `CreditBalance`（可用）+ `HeldCredits`（冻结） |
| 下单 hold | Enrol → `Reserved` + Hold 流水 |
| 行程完成 capture | 开班确认日全班 Capture → `Active` |
| 取消立刻退回钱包 | 人数不足 / 确认前取消 → Release |
| 卡解冻可能数天 | 不用卡做报名冻结 |
| App 余额 = 可花的 | 顶栏 = 可用，另标 On hold |
| 司机完成后结算 | Royalty 只在 Capture 之后 |

### 腾讯云面授

| 腾讯云 | CoLearnX |
|--------|----------|
| 开班前 10 天按最低人数决定 | 截止日全班判定 |
| 电话 + 邮件 | 站内通知（有邮件通道则加一封）；无电话 |
| 开课前一周发时间地点 | `StartsAt - 7 days` |
| 不开班：延期或退款 | 默认 Release；7 天内可转到替补班 |
| 自退 100% / 70% / 0% | 第 8.4 节（自然日） |
| 延期不消耗延期次数 | 替补班重新 Hold，不另扣权益 |

### 原 CoLearnX 线框

- UC-01 → UC-03 兴趣引导 → UC-06 首页：`docs/diagrams/member_use_case.mmd`
- 三步线框：`docs/wireframes/drawio/CoLearnX-Member-Wireframe.drawio`（03/04/05）
- 评分弹窗 MBR-07：同文件 `21-Modal-Rating`
- 已有未接线：`Interest`、`UserInterest`、`UserPreference`、`ProgramRating`

---

## 5. 数据模型

### 5.1 `Interest`

现有 `Name` + 字符串 `Category` 改为树：

| 字段 | 说明 |
|------|------|
| Id | PK |
| Slug | 稳定键，种子 upsert，如 `it-software`、`cybersecurity` |
| Name | 英文展示名 |
| ParentId | null = 大类；非空 = 叶子 |
| SortOrder | 同级排序 |
| IsActive | 种子全部 true |

最多两级；Slug 全局唯一。课程和学员兴趣只能绑叶子。

### 5.2 `CourseInterest`

`CourseId + InterestId`，叶子，每课 1–4 个。提交发布时必填。

### 5.3 `UserInterest`

已有表，补外键。只存叶子。Skip 则 0 行。最多 8 个。

### 5.4 `UserPreference`

已有 `LearningGoals`（`hobby` \| `professional` \| null）。新增 `OnboardingCompletedAt`、`OnboardingSkippedAt`。二者都空则首次进 Member 强制引导（可 Skip）。

### 5.5 `ProgramRating`（已有）

按 `EnrollmentId` 一对一。新增 `UpdatedAt`。仅 `Completed` 可评；1–5 星；评论可选 ≤500。每人每课一票（按 CourseId 覆盖）。列表/详情：`averageStars`、`ratingCount`。

### 5.6 钱包

`User.CreditBalance` **改义为可用**。新增 `HeldCredits`。

```text
HeldCredits == SUM(Reserved enrollment.CreditsSpent)
CreditBalance >= 0
HeldCredits >= 0
展示总额 = CreditBalance + HeldCredits
```

Admin 调账只动可用，不能为负，不能直接改冻结。

`CreditTransactionType` 增加 `Hold`、`Capture`、`Release`、`Forfeit`。`Enrolment` 仅保留历史。`Refund` 用于 Capture 之后的退款。`BalanceAfter` 记可用；增加可选 `HeldAfter`。Hold/Capture/Release 必带 `RelatedEnrollmentId`，每种每报名最多一次。

### 5.7 Enrollment

新增 `Reserved`。

```text
Reserved  --Capture--> Active --complete--> Completed
    |                    |
    |                    +--withdraw 70%--> Refunded
    |                    +--platform cancel 100%--> Refunded
    +--Release--> Cancelled
```

Trainer **Mark complete** 仅 Active：进度 100%；若有 Assessment 须全部及格。无考核可直接 Complete。Member 不能自己结课。

### 5.8 CourseIntake

| 字段 | 规则 |
|------|------|
| `MinEnrollment` | 默认 10，范围 2–200 |
| `ConfirmationScheduledAt` | = `RegistrationClosesAt` |
| `ConfirmedToRunAt` | 判定开班的时间 |
| `CancelledAt` | 人数不足或人工取消 |
| `ReplacementForIntakeId` | 延期班指向旧班 |

```text
RegistrationOpensAt < RegistrationClosesAt
RegistrationClosesAt <= StartsAt - 10 days
StartsAt < EndsAt
```

线下 Session：`PhysicalCapacity >= MinEnrollment`。线上：`PhysicalCapacity = 0` 表示不限，不做容量≥最低人数校验。

---

## 6. 权限（报名状态）

| 状态 | 目录/详情 | Learning Hub | 出勤评分证书 | 升学 |
|------|-----------|--------------|--------------|------|
| Reserved | 已占位、冻结金额 | 否 | 否 | 不算学过 |
| Active | 已入学 | 是 | 是 | 未完成 |
| Completed | — | 按现有 | 是 | **锚点** |
| Cancelled / Refunded | 可再报其他班 | 否 | 否 | 不算学过 |

---

## 7. 推荐

首页：Continue Learning（Active）之下是 **Recommended for you**，不再以 Featured 为主。

```text
注册 → 兴趣引导 → 冷启动（高星 Beginner）
选课完成 → 该课叶子 + CourseLevel 升学
  够格 → 同叶子下一级
  不够格 → 同叶子同级
  星级只排序
```

跨大类不混链。有 Completed 后不用注册兴趣做主推荐。排除已有 Reserved/Active/Completed 的课。Cancelled/Refunded 可再推。

### 7.1 排序

```text
m = 5, C = 3.0
bayes = (v/(v+m))*R + (m/(v+m))*C     // 无评分时 R=C, v=0
jaccard = |A∩B| / |A∪B|                // 空集为 0
score = 0.55*(bayes/5) + 0.30*jaccard + 0.15*hasOpenIntake
```

冷启动：A = 用户叶子，B = 课程叶子。升学：A = 锚点课叶子，B = 候选叶子。  
`hasOpenIntake`：存在窗口内、未取消、（线下则未满）的 Published Intake。

最多 8 门。

- 无 Completed：`mode = coldStart`，仅 Beginner；有兴趣则至少重叠 1 个叶子。
- 有 Completed：取最近完成课的叶子 T。够格则 `nextLevel`（SortOrder+1）；已是 Advanced 或无更高则 `mastery`（同标签其他 Advanced）。不够格则 `sameLevel`。

---

## 8. 报名与资金

### 8.1 时间窗（Enrol 必须全过）

- Intake `Published` 且未取消
- `now ∈ [RegistrationOpensAt, RegistrationClosesAt)`
- 线下：`now <= PhysicalBookingDeadline` 且 `SeatsTaken < PhysicalCapacity`
- 可用积分 ≥ 课费
- 该 Course 无 Active/Reserved

错误码：`REGISTRATION_NOT_OPEN`、`REGISTRATION_CLOSED`、`PHYSICAL_BOOKING_CLOSED`、`SESSION_FULL`、`INSUFFICIENT_CREDITS`、`ALREADY_ENROLLED`。

### 8.2 Hold

同一事务：减可用、加冻结、占座、`Reserved`、流水 Hold、通知 N-01。

### 8.3 截止日结算（全班一次）

作业扫 `Published && RegistrationClosesAt <= utcNow && ConfirmedToRunAt == null`。

- `reservedCount >= MinEnrollment`：全员 Capture → Active；写 `ConfirmedToRunAt`；Status 保持 Published 直到 `StartsAt` 再变 InProgress；通知学员和 Trainer/Creator。
- 否则：全员 Release → Cancelled；Intake Cancelled；通知可等 7 天延期班。Trainer 7 天内创建 `ReplacementForIntakeId` 同课 Intake；学员 `accept-postponement` 对新班再 Hold。

禁止一部分 Capture、一部分仍 Hold。幂等。`StartsAt - 7 days` 再发时间地点（若确认日已晚于 T-7 则确认时合并发）。

### 8.4 取消与自退

| 谁 | 何时 | 积分 |
|----|------|------|
| 学员 `cancel-reservation` | Reserved | 100% Release，退座 |
| 学员 `withdraw` | Active，距开课 >5 且 ≤10 天 | 70% Refund + 30% Forfeit |
| 学员 `withdraw` | Active，≤5 天或已开课 | `WITHDRAW_TOO_LATE` |
| Trainer 确认前取消班 | — | 全员 100% Release |
| Trainer 确认后取消班 | — | 全员 100% Refund |
| 人数不足系统取消 | — | 全员 100% Release |

`daysLeft` 用 Intake `StartsAt` 的自然日。Capture 发生在 T-10，确认后立刻自退落在 70% 档。确认后不重开报名。

### 8.5 并发

线下最后席位：`UPDATE ... WHERE SeatsTaken < PhysicalCapacity`，影响行数 ≠ 1 则 `SESSION_FULL`、不写 Hold。

历史已是 Active 且扣过 `Enrolment` 的种子报名视为已 Capture，不回放 Hold。

---

## 9. 通知

`NotificationService` 改为任意有效 User 可读自己的收件箱。Trainer/Creator `RoleShell` 加铃铛。路径由 Code 映射。

满员：Session **从未满→满** 发一次 `N-session-full`；退座后再满再发。不要每人报名都推 Trainer。线上不限人数不发满员。

| Code | 接收人 | 何时 | 跳转 |
|------|--------|------|------|
| `N-01` | Member | Hold 成功 | `/member/programs` |
| `N-hold-released` | Member | 解冻 | `/member/payment` |
| `N-class-confirmed` | Member | Capture | `/member/programs` |
| `N-class-reminder` | Member | 开课前 7 天 | `/member/programs` |
| `N-withdraw-70` | Member | 70% 自退 | `/member/payment` |
| `N-postpone-offer` | Member | 替补班可领 | `/member/programs` |
| `N-session-full` | Trainer | 该节满员 | 对应 Intake |
| `N-session-reopened` | Trainer | 截止前从满变可报 | 对应 Intake |
| `N-min-reached` | Trainer | 第一次 ≥ MinEnrollment | 对应 Intake |
| `N-under-enrolled` | Trainer | 截止前 3 天仍不足 | 对应 Intake |
| `N-intake-confirmed` | Trainer + Creator | 结算开班 | 对应 Intake |
| `N-intake-cancelled` | Trainer + Creator | 人数不足或取消 | 对应 Intake |
| `N-learner-withdrew` | Trainer | 学员 70% 退出 | `/trainer/learners` |

N-02 / N-08 本轮不做。

---

## 10. API

| 方法 | 路径 | 谁 | 作用 |
|------|------|----|------|
| GET | `/api/interests` | 登录 | 兴趣树 |
| PUT/GET | `/api/users/me/interests` | Member | 引导结果 |
| GET | `/api/recommendations` | Member | `{ mode, items[], anchorCourseId? }` |
| POST | `/api/enrollments/{id}/rating` | Member | 1–5 星 |
| POST | `/api/enrollments` | Member | Hold |
| POST | `/api/enrollments/{id}/cancel-reservation` | Member | 100% Release |
| POST | `/api/enrollments/{id}/withdraw` | Member | 70% 或拒绝 |
| POST | `/api/enrollments/{id}/accept-postponement` | Member | 转到替补班 |
| GET | `/api/enrollments/my` | Member | 含 Reserved、heldCredits |
| GET | `/api/courses` / `{id}` | 已有 | 标签、星、报名状态、minEnrollment |
| POST/PUT | `/api/creator/courses` | Creator | `interestIds` 1–4 |
| POST | `/api/trainer/enrollments/{id}/complete` | Trainer | 结课 |
| POST | `/api/trainer/intakes/{id}/cancel` | Trainer | 确认前 Release / 确认后全额 Refund |
| POST | `/api/trainer/intakes/{id}/postpone` | Trainer | 指定替补 Intake |
| GET | `/api/credits/ledger` | Member | 含 Hold/Capture/Release |
| GET | `/api/notifications/my` | 任意 User | 去掉 Must-be-Member |
| GET | `/api/auth/me` | 登录 | `creditBalance`、`heldCredits`、`totalCredits` |

结算无 Member 按钮；测试调用 `IIntakeSettlementService.SettleIfDueAsync`。

错误码另含：`INTEREST_NOT_LEAF`、`INTEREST_NOT_FOUND`、`INTEREST_LIMIT`、`COURSE_INTERESTS_REQUIRED`、`RATING_NOT_COMPLETED`、`ENROLLMENT_NOT_COMPLETABLE`、`WITHDRAW_TOO_LATE`。

---

## 11. 界面

- `/member/onboarding`：Hobby/Professional → 大类 → 叶子；可 Skip；Account 可改兴趣。
- 首页：可用积分 + On hold；Recommended for you（级别、叶子、星，无评显示 New）；Continue Learning 保留。
- 详情：叶子芯片、星、人数、min、报名状态；确认框写清冻结/最低人数/70% 规则；学完可 Rate。
- Programs：Reserved / Active / 延期领取。
- Creator 表单：按大类多选最多 4 个叶子。
- Trainer Intake：Min enrollment 默认 10、线下容量 ≥ min、截止 ≤ 开课前 10 天、取消/延期。
- Trainer 学员：Reserved vs Active；Active 可 Mark complete。
- Trainer/Creator 顶栏铃铛。

---

## 12. 预置标签库

来源：Udemy 大类、Coursera 的 Data/Language、原线框 Programming/Design/Art/Business/Music/Language、用户举例 IT → Cybersecurity、Art → Sketching。只两级；叶子是升学链粒度，不含 React/Photoshop 等工具名。种子按 slug upsert，不删已被引用的行。

### 12.1 大类（15）

| Sort | Slug | Name |
|---:|---|---|
| 1 | `development` | Development |
| 2 | `it-software` | IT & Software |
| 3 | `data-ai` | Data Science & AI |
| 4 | `design` | Design |
| 5 | `art` | Art |
| 6 | `business` | Business |
| 7 | `finance-accounting` | Finance & Accounting |
| 8 | `marketing` | Marketing |
| 9 | `office-productivity` | Office Productivity |
| 10 | `personal-development` | Personal Development |
| 11 | `photography-video` | Photography & Video |
| 12 | `health-fitness` | Health & Fitness |
| 13 | `music` | Music |
| 14 | `language-learning` | Language Learning |
| 15 | `teaching-academics` | Teaching & Academics |

### 12.2 叶子

**Development:** `web-development`, `frontend-development`, `backend-development`, `mobile-development`, `game-development`, `programming-languages`, `software-testing`, `software-engineering`, `devops`, `no-code-development`

**IT & Software:** `cybersecurity`, `ethical-hacking`, `network-security`, `cloud-computing`, `operating-systems`, `it-certifications`, `hardware`, `it-operations`

**Data Science & AI:** `data-science`, `machine-learning`, `generative-ai`, `data-engineering`, `data-analysis`, `business-intelligence`

**Design:** `user-experience-design`, `ui-visual-design`, `ux-research`, `web-design`, `graphic-design`, `design-systems`, `3d-animation`, `game-design`, `fashion-design`, `interior-design`

**Art:** `sketching`, `drawing`, `painting`, `digital-painting`, `illustration`, `calligraphy`, `comics`

**Business:** `entrepreneurship`, `management`, `project-management`, `product-management`, `sales`, `communication`, `human-resources`, `operations`, `business-strategy`, `e-commerce`

**Finance & Accounting:** `accounting`, `finance`, `investing`, `financial-modeling`, `cryptocurrency`, `taxes`

**Marketing:** `digital-marketing`, `seo`, `social-media-marketing`, `content-marketing`, `branding`, `paid-advertising`, `marketing-analytics`

**Office Productivity:** `microsoft-office`, `google-workspace`, `apple-productivity`, `sap`, `data-entry-automation`

**Personal Development:** `career-development`, `leadership`, `personal-productivity`, `public-speaking`, `confidence`, `stress-management`

**Photography & Video:** `digital-photography`, `video-production`, `photo-editing`, `cinematography`

**Health & Fitness:** `fitness`, `yoga`, `nutrition`, `mental-health`, `sports`, `meditation`

**Music:** `music-production`, `instruments`, `music-fundamentals`, `vocals`, `music-software`

**Language Learning:** `english`, `academic-english`, `chinese`, `japanese`, `korean`, `spanish`, `french`, `german`

**Teaching & Academics:** `teacher-training`, `math`, `science`, `humanities`, `social-science`, `test-prep`, `engineering`

展示名与旧规格一致（如 `graphic-design` = Graphic Design & Illustration）。约 105 个叶子。

### 12.3 种子课打标

| Code | Title | Level | 叶子 |
|------|-------|-------|------|
| INFT 2051 | UI/UX Design Fundamentals | Beginner | `user-experience-design`, `ui-visual-design` |
| INFT 2002 | Frontend React Bootcamp | Beginner | `frontend-development`, `web-development` |
| INFT 3030 | Cybersecurity Essentials | Intermediate | `cybersecurity`, `network-security` |
| INFT 4010 | UX Research Methods | Intermediate | `ux-research`, `user-experience-design` |
| INFT 4025 | Responsible AI for Learning Design | Advanced | `generative-ai`, `user-experience-design` |

演示学员 Huang：Professional + `user-experience-design` + `cybersecurity`。已完成 INFT 2002 则首页走 Frontend/Web 的 Intermediate。给 2051、2002 各 2–3 条种子评分以便演示高星初级。

---

## 13. 测试要点

**推荐**

- 兴趣树 15 大类；`cybersecurity` 父级 `it-software`；`sketching` 父级 `art`。
- 绑大类 → `INTEREST_NOT_LEAF`。Skip → 全是 Beginner。只选 cybersecurity → 不含 Sketching。
- Completed 且考核全过 → nextLevel；未过 → sameLevel；Advanced 无更高 → mastery。
- 1 人 5 星 vs 20 人 4.5 星：后者 score 更高。未 Complete 不能评。0 叶子不能提交发布。考核未过不能结课。

**钱包与开班**

- 可用 20、课费 20：Hold 后可用 0、冻结 20，不能再报。
- 9 人 min 10 过截止：全员 Release，Intake Cancelled。
- 10 人：Capture；T-7 发时间地点；Trainer/Creator 收到确认。
- 确认后距开课 8 天 withdraw：70%；3 天：`WITHDRAW_TOO_LATE`。
- 延期班 accept-postponement：再次 Hold。
- 截止后 Enrol → `REGISTRATION_CLOSED`。
- 满员下一人失败且余额不变；Trainer 恰好一条 `N-session-full`；退座后再满再发。
- 达 min 只发一条 `N-min-reached`。
- Trainer 无 Member 角色也能拉通知。
- 结算跑两次不重复 Capture。PayPal 只加可用。Admin 调账不得可用为负。

---

## 14. 实现边界

- 实体 / DbContext / SeedData（词表、打标、评分、兴趣、MinEnrollment、HeldCredits）
- 新 Service：兴趣、推荐、评分、Hold/Settle、结课；**不要再堆 `AppServices.cs`**
- `IntakeSettlementHostedService`
- `NotificationService` 去掉 Member-only，补 Code 路径
- 前端：onboarding、首页推荐、详情报名状态、评分、Programs 冻结、Payment 账本、Creator 标签、Trainer 开班字段/取消/结课、Trainer/Creator 铃铛
- 集成测试覆盖第 13 节

实现完成后更新 `docs/CODE_OVERVIEW.md`（及若恢复的 `FEATURES.md`）。
