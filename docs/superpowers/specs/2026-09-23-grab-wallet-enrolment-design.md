# CoLearnX 积分钱包（Grab 模式）与开班确认 — 设计规格

> **已合并。** 请以 [`2026-09-23-recommendation-and-enrolment-design.md`](./2026-09-23-recommendation-and-enrolment-design.md) 为准。下文仅作归档。

Date: 2026-09-23  
Status: superseded  
Related: `2026-09-23-course-recommendation-design.md`（升学推荐，资金模型以本文件为准）

报名不再当场扣光积分。学 GrabPay：先冻结、开班条件满足再实扣、取消则立刻解冻回钱包。PayPal 只负责把钱充进钱包，不对单次报名做卡授权。

## 1. 目标

资金学 Grab 钱包；**开班判定、通知、学员退课比例学腾讯云面授**，用来补上现行报名接口的漏洞。

- 学员点 Enrol 后，课费从**可用积分**转入**冻结**，席位被占住。
- 报名截止（开课前至少 10 天）按最低人数判定：达标全班 Capture 开课；不足则不开课。
- 人数不足：默认立刻 Release 回可用；若 Trainer 在 7 天内给出延期班次，学员可改选「转到新班」而不再扣一次权益（腾讯云：因人数不足延期不消耗延期次数）。
- 平台/人数不足取消 = 全额退冻结或已扣积分，不走 Dispute。
- 学员自己退：确认前 100%；确认后按距开课天数 70% / 0%（见第 6.4 节）。
- 过报名窗口、满员、未到开放时间、过线下预订截止，后端拒绝，详情页按钮禁用。
- **满员、即将不足、开班/取消等事件要通知 Trainer**（现有通知接口只允许 Member 读取，Trainer 顶栏也没有铃铛）。
- 开班结果通知 + 开课前约 7 天再发一次时间/地点（或会议链接）。
- 顶栏和账本分清可用、冻结、已消费。

## 1.1 现行代码漏洞（本规格要补上）

| 漏洞 | 现在 | 补完后 |
|------|------|--------|
| 报名不检查开放/截止 | 过期仍能报 | `REGISTRATION_NOT_OPEN` / `CLOSED` |
| 不检查线下预订截止 | `PhysicalBookingDeadline` 形同虚设 | `PHYSICAL_BOOKING_CLOSED` |
| 当场扣光积分 | 人数不足只能事后争议退 | Hold → Capture / Release |
| 无最低开班人数 | 1 人也会「上课」 | 默认 min 10，截止日判定 |
| 无开班确认通知 | 只有 Enrolment confirmed | 确认开班 / 未开班已退回 / 开课前提醒 |
| Trainer 收不到满员等运营通知 | 通知 API `RequireMember`；RoleShell 无铃铛 | 满员、达最低人数、截止前人数不足、结算结果都推给该 Intake 的 Trainer |
| 详情没有截止、满员、已取消 | 只有 Full / Online | open / full / closed / cancelled |
| 最后席位并发未测 | 可能超卖或双扣 | 条件更新占座，失败不冻积分 |
| 线上容量 0 | 文档仍写「永远报满」，代码已当不限 | 规格写死：0 = 不限；仍受最低人数 |

## 2. 非目标

- 不在报名时对 PayPal / 银行卡做 pre-authorization（卡授权约 7 天过期，开班确认常超过这个窗口）。
- 不按行程结束后的「可变车费」多扣或少扣；课费在报名时已固定，Hold 金额 = Capture 金额。
- 不复制 Grab 的「取消后授权再留 30 分钟给下一单」。解冻立即回可用。
- 不复制 Auto Top-Up。
- 本轮不做候补名单、不打电话（腾讯云有人工电话；我们只用站内通知，有邮件则加一封）。
- 不把冻结中的报名当成已上课：不能进 Learning Hub、不能评分、不能申请证书。

## 3. 与 Grab 的对应

| Grab | CoLearnX |
|------|----------|
| GrabPay Credits 钱包 | Member `CreditBalance`（可用）+ `HeldCredits`（冻结） |
| 下单授权 hold，未真正入账给 Grab | Enrol → `Reserved`，写 Hold 流水 |
| 行程完成 capture | 开班确认日全班 Capture → `Active` |
| 取消行程，Credits **立刻**回钱包 | 人数不足 / 取消本班 → Release |
| 卡支付解冻可能要数天 | **不用卡做报名冻结**；PayPal 仅 TopUp |
| App 里看到的余额是可花的 | 顶栏 Credit Balance = 可用，另标 On hold |
| 授权额 = 预估车费上限 | Hold = 该课 `CreditCost`（无附加费） |
| 司机完成后才结算 | Trainer Royalty 只在 Capture 之后 |

## 3.1 与腾讯云面授的对应

来源：[腾讯云产业互联网学堂报名指引](https://cloud.tencent.com/document/product/658/41043)。

| 腾讯云面授 | CoLearnX |
|------------|----------|
| 开班前 10 天按最低人数决定是否开班 | `RegistrationClosesAt <= StartsAt - 10 days`，截止日全班判定 |
| 专人电话 + 邮件通知开班/不开班 | 站内通知（+ 已配置的邮件通道）；无电话 |
| 开班则开课前一周发时间地点 | `StartsAt - 7 days` 再发 Session 时间、地址或会议链接 |
| 不开班：延期或退款；人数不足的延期不消耗延期次数 | 默认 Release；7 天内 Trainer 发布替补 Intake 时可「转移占位」，不另扣积分 |
| 学员取消：>10 工作日 100%，5–10 工作日 70%，≤5 工作日不退 | 见 6.4（用自然日，与 10 天确认窗对齐） |
| 延期成功后不可再退 | 转移到新班后，退课规则按**新班** `StartsAt` 重算；本轮转移后确认前仍可 100% Release |

确认日用自然日（不是工作日），避免和周末排课纠缠。7 天开课提醒同理。

## 4. 钱包模型

### 4.1 账户字段

现有 `User.CreditBalance` **改义为可用积分**（能拿去报下一门课的数）。新增：

| 字段 | 含义 |
|------|------|
| `CreditBalance` | 可用。Enrol Hold 减少它；Release / TopUp 增加它 |
| `HeldCredits` | 冻结中。所有 `Reserved` 报名的课费之和。必须 `>= 0` |
| 不单独存「总额」 | 展示用 `CreditBalance + HeldCredits` |

不变量：

```text
HeldCredits == SUM(Reserved enrollment.CreditsSpent)
CreditBalance >= 0
HeldCredits >= 0
```

Admin 调账只动 `CreditBalance`，不能把可用调到负，也不能直接改 `HeldCredits`。要动冻结必须先 Release 对应报名。

### 4.2 流水类型

现有 `CreditTransactionType` 增加：

| Type | Delta（对可用） | Held | 何时 |
|------|-----------------|------|------|
| `TopUp` | +N | 不变 | PayPal 充值（已有） |
| `Hold` | −课费 | +课费 | 报名成功 |
| `Capture` | 0 | −课费 | 开班确认。可用不变，钱从冻结变为已消费 |
| `Release` | +课费 | −课费 | 未开班或确认前取消 |
| `Enrolment` | 不再用于新报名 | — | 仅保留历史种子/旧数据 |
| `Refund` | +N | 0 | Capture 之后：争议全额、平台取消全额、或学员 70% 自行退出 |
| `Forfeit` | 0 | 0 | 学员 70% 退时留下的 30%，只审计不回可用 |
| `Royalty` | Trainer/Creator | — | **仅** Capture 之后 |
| `AdminAdjustment` | ±N 对可用 | 0 | 已有 |

每笔 Hold / Capture / Release 必须带 `RelatedEnrollmentId`。同一 enrollment 每种类型最多成功一次（幂等）。`BalanceAfter` 记**可用**余额，与现有账本列兼容。流水增加可选 `HeldAfter`，账本页能显示冻结变化。

### 4.3 `/api/auth/me` 与首页

返回：

```text
creditBalance        // 可用
heldCredits
totalCredits         // 二者之和，只读计算
```

Member 首页：主数字是可用；旁边「On hold: N」；点开可看到冻结来自哪几门 Reserved 课。Payment Ledger 增加 Hold / Capture / Release 筛选。

## 5. 报名与开班状态

### 5.1 Enrollment

新增 `Reserved = 4`（或插在 Active 前并迁移种子；实现时用明确枚举名 `Reserved`）。

```text
Reserved  --Capture--> Active --complete--> Completed
    |                    |
    |                    +--withdraw 70%--> Refunded
    |                    +--platform cancel 100%--> Refunded
    +--Release--> Cancelled     // 未开班或确认前取消。积分已回可用。
```

`Refunded` 用于 **Active 之后** 的退款（争议、平台取消、70% 自行退出）。确认前退出用 `Cancelled` + `Release`。

### 5.2 CourseIntake 新字段

| 字段 | 规则 |
|------|------|
| `MinEnrollment` | 默认 **10**。范围 2–200。Trainer 开班时可改。 |
| `ConfirmationScheduledAt` | 等于 `RegistrationClosesAt`。报名一关就开始判定。 |
| `ConfirmedToRunAt` | 判定为开班的时间 |
| `CancelledAt` / 已有 `Cancelled` | 人数不足或人工取消 |

日期约束（在现有 `opens < closes <= starts < ends` 上收紧）：

```text
RegistrationClosesAt <= StartsAt - 10 days
```

这样确认日至少早于开课 10 天（腾讯云面授「开课前 10 天通知」）。若开课太近无法满足，禁止提交该 Intake。

线下 Session：`PhysicalCapacity` 仍由 Trainer 自定义，且 **>= MinEnrollment**（否则永远开不了班）。线上 Session：`PhysicalCapacity = 0` 表示不限人数，不做「容量 >= 最低人数」校验，但仍受 Intake 最低人数约束。

满员：线下 `SeatsTaken >= PhysicalCapacity` 关闭该 Session 通道。`SeatsTaken` 统计 **Reserved + Active**（冻结中的人也占座）。线上不限则只受最低人数逻辑，没有满员。

### 5.3 时间窗（报名接口必须检查）

现在 `EnrolAsync` 不看时间。改为必须同时满足：

- Intake `Published`
- `now ∈ [RegistrationOpensAt, RegistrationClosesAt)`
- 线下：`now <= PhysicalBookingDeadline`，且未满员
- 用户可用积分 `>= CreditCost`
- 该用户对该 Course 没有 Active/Reserved 报名
- Intake 尚未判定取消

否则 400，错误码：

- `REGISTRATION_NOT_OPEN`
- `REGISTRATION_CLOSED`
- `PHYSICAL_BOOKING_CLOSED`
- `SESSION_FULL`
- `INSUFFICIENT_CREDITS`（指**可用**不足，冻结中的不算可用）
- `ALREADY_ENROLLED`（含 Reserved）

详情页按钮与错误码一致，禁止只靠前端隐藏。

## 6. 主路径

### 6.1 Enrol = Grab 下单 Hold

同一事务：

1. 校验第 5.3 节。
2. `CreditBalance -= cost`，`HeldCredits += cost`。
3. `SeatsTaken += 1`。
4. Enrollment `Reserved`，`CreditsSpent = cost`。
5. 流水 `Hold`。
6. 通知：credits on hold for {course}。

学员 Programs 显示 **Reserved — credits on hold**。不能进 Hub。

### 6.2 确认日 = Grab 行程完成，但是全班一次

后台作业（或请求时惰性判定，以作业为准，避免漏跑）：

当 `now >= RegistrationClosesAt` 且 Intake 仍为 `Published`：

- `reservedCount = COUNT(Reserved on this intake)`（按人，一 Intake 一报名）
- 若 `reservedCount >= MinEnrollment`：
  - 每个 Reserved：`HeldCredits -= cost`（用户），流水 `Capture`（可用不变），Enrollment → `Active`
  - Intake → `InProgress`（到 `StartsAt` 也可仍保持 Published 直到开课；**资金上已确认**）。为避免和现有 InProgress 语义打架：增加明确「已确认开班」——实现用 `ConfirmedToRunAt != null` 且 Status 保持 `Published` 直到 `StartsAt`，再变 `InProgress`。
  - 通知全班：class confirmed，credits captured。
- 否则（人数不足，腾讯云「不开班」）：
  - **默认立刻全员 Release**（积分回可用，Enrollment `Cancelled`，Intake `Cancelled`）。
  - 通知：未达最低人数，积分已退回；若 7 天内开出延期班，可从 Programs 领取转移（不另扣积分）。
  - Trainer 在 7 天内把新 Intake 标为 `ReplacementForIntakeId = 旧班` 且同一 Course：被 Release 的学员收到「Join postponed class」；接受则对新班重新 Hold（此时可用里已含刚退回的课费，等价腾讯云延期不消耗权益）。拒绝或 7 天不点：无需再操作，钱已在可用余额。

全班同一结果：禁止一部分 Capture、一部分仍 Hold。作业必须按 Intake 加事务锁，幂等（已 Capture/已 Cancelled 的跳过）。

确认开班后再发第二次通知：`StartsAt - 7 days`（若确认日晚于 T-7 则确认时合并发送）——线下地址 / 线上会议链接。

### 6.3 Trainer / Creator 取消已发布班

确认日之前「Cancel intake」：全员 Release（等同人数不足的退款侧）。  
确认日之后（已 Capture）平台取消：每个 Active **全额** `Refund`（100%，不走 70% 阶梯），Enrollment `Refunded`，Intake `Cancelled`。学员个人原因退课才走 6.4。

### 6.4 学员自己退课（腾讯云比例，自然日）

`daysLeft = 开课日 0:00 UTC − 现在`，按 Intake `StartsAt`。

| 阶段 | 操作 | 积分 |
|------|------|------|
| 仍是 Reserved（尚未确认开班） | `cancel-reservation` | **100% Release**，退座 |
| Active，距开课 **> 5 天且 ≤ 10 天** | `withdraw` | **70% Refund** 回可用；30% 记 `Forfeit`（平台留存，不回可用） |
| Active，距开课 **≤ 5 天** 或已开课 | 拒绝 | `WITHDRAW_TOO_LATE` |
| 人数不足 / 平台取消 | 系统处理 | **始终 100%**，不受上表限制 |

因为报名截止已经 ≤ 开课前 10 天，Capture 发生在 T-10，所以确认后立刻自行退出落在 70% 档。确认前退出永远 100%，对应腾讯云「开课前较早就取消」。

70% 退款同一事务：`CreditBalance += floor(cost * 0.7)`，流水 `Refund`；`Forfeit` 记 `cost - 退回额`（Delta 0 对可用，仅审计）。Enrollment `Refunded`，退座。满员班空出的位子可以再被报名（若窗口未关；确认后窗口已关则不再开放，除非 Trainer 明确加名额——本轮确认后不重开报名）。

### 6.5 通知（Member + Trainer）

现有 `NotificationService` 只允许 Member 读收件箱，Trainer 工作区没有铃铛。本轮改为：**任意有效 User 可读自己的通知**；Trainer / Creator `RoleShell` 使用同一套铃铛组件。链接仍由 `Code` 映射，禁止用 body 里的任意 URL。

每个 Session **从非满员变为满员时发一次** `N-session-full`（Eventbrite sold out）。有人在截止前退座后又满，再发一次。不要每人报名都打扰 Trainer。

| Code | 接收人 | 何时 | 跳转 |
|------|--------|------|------|
| `N-01` | Member | Hold 成功（已有报名确认，文案改为 credits on hold） | `/member/programs` |
| `N-hold-released` | Member | 人数不足 / 自己确认前取消 / 平台确认前取消 | `/member/payment` |
| `N-class-confirmed` | Member | 全班 Capture | `/member/programs` |
| `N-class-reminder` | Member | 开课前 7 天（腾讯云一周提醒） | `/member/programs` |
| `N-withdraw-70` | Member | 自行 70% 退 | `/member/payment` |
| `N-postpone-offer` | Member | Trainer 发布替补班 | `/member/programs` |
| `N-session-full` | **该 Intake 的 Trainer** | 该 Session 席位首次或再次满员 | `/trainer/courses` 对应 Intake |
| `N-session-reopened` | Trainer | 截止前因退座从满员变回可报 | 同上 |
| `N-min-reached` | Trainer | Reserved 人数第一次 ≥ MinEnrollment | 同上 |
| `N-under-enrolled` | Trainer | 截止前 3 天仍 < MinEnrollment | 同上 |
| `N-intake-confirmed` | Trainer（+ 课的 Creator） | 结算为开班 | 同上 |
| `N-intake-cancelled` | Trainer（+ Creator） | 人数不足或人工取消 | 同上 |
| `N-learner-withdrew` | Trainer | Active 学员 70% 退出 | `/trainer/learners` |

线上不限人数的 Session **不发** `N-session-full`。最低人数通知按 Intake 计，与是否满员无关。

原设计 N-02 Session reminder、N-08 Low balance 本轮不做（低余额仍只在报名时弹窗）。

## 7. 学习权限

| 状态 | 目录/详情 | Learning Hub / 资料 | 出勤评分证书 |
|------|-----------|---------------------|--------------|
| Reserved | 显示已占位、冻结金额 | 否 | 否 |
| Active | 已入学 | 是 | 是 |
| Cancelled（Release） | 可重新报其他班 | 否 | 否 |
| Completed | — | 按现有 | 是 |

升学推荐只把 **Completed** 当完成；Reserved / 被 Release 的不算学过。

## 8. API

| 方法 | 路径 | 行为 |
|------|------|------|
| POST | `/api/enrollments` | Hold。返回 `status=Reserved`，`availableAfter`，`heldCredits` |
| POST | `/api/enrollments/{id}/cancel-reservation` | 仅 Reserved；100% Release |
| POST | `/api/enrollments/{id}/withdraw` | 仅 Active；按 6.4 的 70% / 拒绝 |
| POST | `/api/enrollments/{id}/accept-postponement` | 人数不足已 Release 后，7 天内转到替补 Intake（重新 Hold） |
| POST | `/api/trainer/intakes/{id}/cancel` | 确认前全员 Release；确认后全员 100% Refund |
| POST | `/api/trainer/intakes/{id}/postpone` | 为已取消班指定替补 Intake |
| GET | `/api/enrollments/my` | 含 Reserved，以及 `heldCredits` |
| GET | `/api/courses/{id}` | 报名状态：open / notOpen / closed / full / cancelled；剩余席位；`minEnrollment`；文案：少于 X 人将取消并退回冻结积分 |
| GET | `/api/credits/ledger` | 新类型；`balanceAfter` = 可用 |

确认日结算 **没有** Member 可点的按钮，由托管服务执行。测试可调用内部 `IIntakeSettlementService.SettleIfDueAsync(intakeId)`。

现有种子里已经是 Active 且扣过 `Enrolment` 的报名：**视为历史已 Capture**，不回放 Hold。新报名全部走 Hold。

## 9. 界面

- 报名确认框：写清冻结、最低人数、截止日、不足则全额退回冻结；确认后自行退出为 70% 或不可退。
- 人数不足后 Programs 出现延期班领取入口（7 天）。
- 开班确认、以及开课前 7 天：通知含时间与地点或会议链接。
- 满员 / 已截止：按钮 disabled + 原因，不是 toast 完还能点。
- 首页可用积分为主；On hold 可点进 Programs 过滤 Reserved。
- Trainer Intake 表单：Min enrollment（默认 10）、线下 capacity（>= min）、报名关闭必须早于开课 10 天。
- Trainer / Creator 顶栏增加与 Member 相同的通知铃铛；满员通知点进该 Intake。
- Trainer 学员列表：Reserved vs Active。确认前看到的是占位名单。

## 10. 作业与并发

- `IntakeSettlementHostedService` 每分钟扫 `Published && RegistrationClosesAt <= utcNow && ConfirmedToRunAt == null && Status != Cancelled`。
- Enrol 与结算、取消使用同一 Intake / User 行锁（SQLite 上靠事务 + 条件 `CreditBalance >= cost` 更新）。
- 两人抢最后席位：`UPDATE ... WHERE SeatsTaken < PhysicalCapacity`（线下）；只有一行影响数 = 1 的成功。失败返回 `SESSION_FULL`，不写 Hold。

## 11. 测试要点

- 可用 20、课费 20：Hold 后可用 0、冻结 20；不能再报另一门。
- 人数 9、min 10、过截止：9 笔 Release，Intake Cancelled，可用回到报名前。
- 人数 10：10 笔 Capture；T-7 发出时间地点通知。
- 确认后、距开课 8 天自行 withdraw：退回 70%，Enrollment Refunded。
- 距开课 3 天 withdraw → `WITHDRAW_TOO_LATE`。
- 人数不足后 Trainer 发布替补班，学员 accept-postponement：再次 Hold 成功，可用减少、冻结增加。
- 截止后 Enrol → `REGISTRATION_CLOSED`。
- 线下满员第三十一人失败，余额不变；Trainer 收到恰好一条 `N-session-full`。
- 满员后一人确认前取消：Trainer 收到 `N-session-reopened`；再满员再发一条 full。
- Reserved 达到 min：Trainer 收到一条 `N-min-reached`，之后再有人报不重复发。
- Trainer 无 Member 角色也能 GET `/api/notifications/my`。
- 同一 enrollment 结算跑两次：第二条 Capture 不重复扣 Held。
- 确认前学员取消：席位 −1，第三人可以 Hold。
- PayPal TopUp 只增加可用，不碰冻结。
- Admin 调账不得使可用为负；有冻结时减可用以减完可用为限。
- 旧 Active 种子报名仍能进 Programs，账本仍显示历史 Enrolment。

## 12. 实现边界

- `User`、`Enrollment`、`CourseIntake`、枚举、`EnrollmentService`（拆出 Hold/Settle，不要继续堆 `AppServices.cs`）
- 新 `IntakeSettlementService` + HostedService
- Member 首页 / 详情 / Programs / Payment ledger
- Trainer Intake 表单与取消；Trainer/Creator 通知铃铛
- `NotificationService` 去掉「必须是 Member」限制，补 Trainer 用的 Code → 路径
- 集成测试覆盖第 11 节

`docs/CODE_OVERVIEW.md` 里「容量 0 永远报满」已过时：线上 0 = 不限人数，本规格维持这一条，并加上最低开班人数。
