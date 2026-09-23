# CoLearnX 智能升学推荐 — 设计规格

> **已合并。** 请以 [`2026-09-23-recommendation-and-enrolment-design.md`](./2026-09-23-recommendation-and-enrolment-design.md) 为准。下文仅作归档。

Date: 2026-09-23  
Status: superseded  
Scope: 兴趣词表、注册引导、课程打标、学完评分、规则推荐（无 AI）

对照实现以本文件为准。现有报名、积分、PayPal、证书审核不在本规格内改行为。

## 1. 目标

学员可以从某一专业方向的 Beginner 课学到 Advanced，中间由系统给出下一门课；不够格时补同级课。新用户没有学习记录时，按注册兴趣推该方向的高星初级课。

成功标准：

- 注册后（含 Skip）进入 Member 首页，能看到推荐课，不再只靠 `IsFeatured`。
- Creator 建课必须从预置英文词表选择叶子标签；级别仍用 `CourseLevel`。
- 学完一门课后可打 1–5 星；平均分影响推荐排序和冷启动。
- 完成且考核及格 → 同标签下一级；否则 → 同标签同级其他课。
- 词表随 `SeedData` 一次性写入，Admin 不必逐条创建即可上线。

## 2. 非目标

- 不用向量模型、大模型或协同过滤。
- 不把 Beginner / Intermediate / Advanced 做成标签。
- 不要求证书签发才能升学。
- 不给未登录游客做推荐。
- 不删除或替换现有 `LearningPath`、`Course.Category`；它们不再作为推荐轴。
- 本轮不做 Admin 词表编辑台（词表以种子为准；以后若要加冷门方向再补 CRUD）。

## 3. 已锁定的产品规则

| 项 | 决定 |
|---|---|
| 词表 | 两级英文：大类 → 叶子。Admin 预置，Creator / 学员只能点选。 |
| 级别 | 现有 `CourseLevel`：Beginner → Intermediate → Advanced。 |
| 引擎 | 规则筛选 + 打分排序，无 AI。 |
| 评分 | 复用已有 `ProgramRating`。学完才能评，每个 completed enrollment 一票，可改。 |
| 新用户 | 只推匹配兴趣的 **高星 Beginner**；Skip 则全站高星 Beginner。 |
| 升学轴 | 以**已报名课程的叶子标签**为准，不是注册时勾的全部兴趣。 |
| 升学门槛 | `Enrollment.Status = Completed` 且进度 100%；该开班若有 Assessment，必须全部及格。无考核则完成即可。 |

Hobby vs Professional（引导第 1 步）只写入 `UserPreference.LearningGoals`，不改变推荐公式。职业/爱好在第一版不加权。

## 4. 推荐分两段

```text
注册 → UC-03 兴趣引导（可 Skip）→ 首页冷启动
         └ 大类 + 叶子 → UserInterest（只存叶子）

选课报名后 → 用该课叶子 + CourseLevel 走升学
         └ 够格：同叶子、高一级、未学过
         └ 不够格：同叶子、同级、未学过
         └ 星级只排序，不决定能不能升
```

跨大类不混链：同时勾了 Cybersecurity 和 Sketching 时，首页两边都会出现；一旦在 Cybersecurity 课上升学，不会插入素描课。

## 5. 数据模型

### 5.1 `Interest`（改造现有表）

现有 `Name` + 字符串 `Category` 无法表达父子。改为：

| 字段 | 说明 |
|---|---|
| Id | PK |
| Slug | 稳定键，种子按 slug upsert，如 `it-software`、`cybersecurity` |
| Name | 展示名，英文 |
| ParentId | null = 大类；非空 = 叶子，指向大类 |
| SortOrder | 同级排序 |
| IsActive | 预留停用；种子全部 true |

约束：最多两级；`ParentId` 必须指向 `ParentId == null` 的节点。Slug 全局唯一。课程和学员兴趣都只能绑叶子。

### 5.2 `CourseInterest`

`CourseId + InterestId` 复合主键。`InterestId` 必须是叶子。一门课至少 1 个、最多 4 个叶子（与主流慕课「最多几个 topic」一致）。提交 Admin 发布前必须满足。

### 5.3 `UserInterest`

已有表，补上到 `User` / `Interest` 的外键。只存叶子。引导 Skip 则 0 行。最多 8 个叶子。

### 5.4 `UserPreference`

已有 `LearningGoals`。新增：

- `OnboardingCompletedAt`
- `OnboardingSkippedAt`

二者都空：登录 Member 后强制进入引导（可 Skip）。

`LearningGoals` 取值：`hobby` | `professional` | null。

### 5.5 `ProgramRating`（已有，接上 API）

已按 `EnrollmentId` 一对一存在：`Stars`、`Comment`、`AllowTrainerView`、`CreatedAt`。

本轮新增 `UpdatedAt`。规则：

- 仅 `Enrollment.Status = Completed` 可创建或更新。
- Stars 整数 1–5。
- Comment 可选，最长 500。
- 每人每课一票：若该用户对该 `CourseId` 已有评分（任意 enrollment），则更新已有行，不新增。课程平均分按「每用户一条」计算。

课程列表/详情增加聚合：`averageStars`、`ratingCount`。无人评时 `averageStars = null`，排序当中性。

### 5.6 完成一门课（升学前提）

现状：Completed 多半靠种子，Trainer 评分不会自动结课。本轮补一条明确路径：

Trainer 在学员列表将 enrollment 标为 Complete，当且仅当：

- 进度可视为 100%；且
- 该 Intake 若存在 Assessment，该学员每张试卷都已评分且 `Score >= PassScore`。

无 Assessment 的开班允许直接 Complete。Member 不能自己点完成。

## 6. 推荐算法（纯代码）

课程必须：`Status = Published`，且至少有一个 Published Intake（否则首页能点进去却报不了名）。排除用户已有 Active / Completed 报名的课。Cancelled / Refunded 视为未学，可以再推。

### 6.1 排序分

贝叶斯平均，避免 1 人 5 星霸榜：

```text
m = 5
C = 3.0
R = 该课平均星（无评分则视为 C）
v = 评分数（无评分则为 0）
bayes = (v / (v + m)) * R + (m / (v + m)) * C

jaccard = |A ∩ B| / |A ∪ B|   // 空集时 jaccard = 0
score = 0.55 * (bayes / 5)
      + 0.30 * jaccard
      + 0.15 * hasOpenIntake   // 报名未截止则为 1，否则 0
```

冷启动：A = 用户叶子兴趣，B = 课程叶子。升学：A = 锚点课叶子，B = 候选课叶子。

### 6.2 冷启动（无 Completed 报名）

候选：`CourseLevel.Name = Beginner`。

- 有叶子兴趣：课程至少 overlapping 1 个叶子。
- 无兴趣（Skip）：全部 Beginner。

返回最多 8 门，按 score 降序。`mode = coldStart`。

### 6.3 升学（至少 1 门 Completed）

对**最近完成的那门课**（`CompletedAt` 最新）取叶子集合 T。

`eligibleNext`：该课对应 Intake 满足第 3 节升学门槛。

- 若 eligible：候选为含 T 中至少 1 个叶子、且 `CourseLevel.SortOrder = 当前 + 1`。最多 8 门。`mode = nextLevel`。若已是 Advanced 或没有更高一级课：`mode = mastery`，改推同标签其他 Advanced（未学过），文案说明已到最高级。
- 若 not eligible：候选为含 T 中至少 1 个叶子、同级、排除当前课。`mode = sameLevel`。

首页仍保留 Continue Learning（Active 报名），推荐区在其下。

有 Completed 时不再用注册兴趣做主推荐；兴趣只影响冷启动。账号里改兴趣只影响「还没有任何 Completed」的用户。

## 7. API

沿用现有 Controller 只转发、规则在 Service 的分层。新增 `RecommendationService`、`InterestCatalogService`、`ProgramRatingService`。

| 方法 | 路径 | 谁 | 作用 |
|---|---|---|---|
| GET | `/api/interests` | 登录用户 | 返回树：大类 + 叶子，仅 `IsActive` |
| PUT | `/api/users/me/interests` | Member | body: `{ learningGoal, interestIds, skipped }` |
| GET | `/api/users/me/interests` | Member | 当前选择 + onboarding 状态 |
| GET | `/api/recommendations` | Member | `{ mode, items[], anchorCourseId? }` |
| POST | `/api/enrollments/{id}/rating` | Member | `{ stars, comment?, allowTrainerView? }` 创建或覆盖 |
| GET | `/api/courses` / detail | 已有 | 增加 `interestIds`, `interestNames`, `averageStars`, `ratingCount` |
| POST/PUT | `/api/creator/courses` | Creator | 增加 `interestIds`（叶子，1–4 个） |
| POST | `/api/trainer/enrollments/{id}/complete` | Trainer | 结课，校验考核 |

错误码：

- `INTEREST_NOT_LEAF` / `INTEREST_NOT_FOUND`
- `INTEREST_LIMIT`（>8）
- `COURSE_INTERESTS_REQUIRED`（提交发布时 0 个叶子）
- `RATING_NOT_COMPLETED`
- `ENROLLMENT_NOT_COMPLETABLE`（考核未过）

## 8. 界面

### 8.1 注册引导（原 UC-03，3 步，可 Skip）

注册成功且邮箱流程结束后，Member 首次进入 `/member/onboarding`：

1. What brings you to CoLearnX? — Hobby / Professional Skill。
2. Choose your interests — 大类多选。
3. Refine your direction — 只展开已选大类的叶子；可搜索叶子英文名。

Skip 任意一步视为跳过整段，写 `OnboardingSkippedAt`。Continue 到第 3 步提交：至少 1 个叶子。之后可在 Account 编辑兴趣。

### 8.2 Member 首页

替换 Featured 主区块为 **Recommended for you**，卡片显示级别、叶子名、平均星（无人评显示 New）。Continue Learning 不动。

### 8.3 课程详情 / 目录

展示叶子芯片、平均星和人数。学完且未评：主按钮旁 **Rate this program**（原 MBR-07 弹窗：5 星 + 评论）。

### 8.4 Creator 课程表单

在 Course level、Learning path 旁增加 Interest tags（按大类分组的多选，最多 4 个叶子）。提交发布时校验。

### 8.5 Trainer 学员页

每名 Active 学员增加 Mark complete（不满足时按钮 disabled + 短原因）。

## 9. 预置标签库

来源：Udemy 市场 13 个顶级类、Coursera 把 Data / Language 提成学科、CoLearnX 原线框（Programming / Design / Art / Business / Music / Language）、以及用户举例的 IT → Cybersecurity、Art → Sketching。

原则：只做两级，不做 Udemy 那种上千 topic；叶子是「一条升学链」的粒度（Cybersecurity，而不是某一个工具名）。

种子按 slug upsert：已存在则更新 Name/Parent/SortOrder，不删已被课程或用户引用的行。

### 9.1 大类（15）

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

Lifestyle / Food / Travel 不单独做大类，避免和职业培训主路径抢首页；美术相关放在 Art，手作/家居不进第一版词表。

### 9.2 叶子

**Development**

| Slug | Name |
|---|---|
| `web-development` | Web Development |
| `frontend-development` | Frontend Development |
| `backend-development` | Backend Development |
| `mobile-development` | Mobile Development |
| `game-development` | Game Development |
| `programming-languages` | Programming Languages |
| `software-testing` | Software Testing |
| `software-engineering` | Software Engineering |
| `devops` | DevOps |
| `no-code-development` | No-Code Development |

**IT & Software**

| Slug | Name |
|---|---|
| `cybersecurity` | Cybersecurity |
| `ethical-hacking` | Ethical Hacking |
| `network-security` | Network & Security |
| `cloud-computing` | Cloud Computing |
| `operating-systems` | Operating Systems & Servers |
| `it-certifications` | IT Certifications |
| `hardware` | Hardware |
| `it-operations` | IT Operations |

**Data Science & AI**

| Slug | Name |
|---|---|
| `data-science` | Data Science |
| `machine-learning` | Machine Learning |
| `generative-ai` | Generative AI |
| `data-engineering` | Data Engineering |
| `data-analysis` | Data Analysis |
| `business-intelligence` | Business Intelligence |

**Design**

| Slug | Name |
|---|---|
| `user-experience-design` | User Experience Design |
| `ui-visual-design` | UI Visual Design |
| `ux-research` | UX Research |
| `web-design` | Web Design |
| `graphic-design` | Graphic Design & Illustration |
| `design-systems` | Design Systems |
| `3d-animation` | 3D & Animation |
| `game-design` | Game Design |
| `fashion-design` | Fashion Design |
| `interior-design` | Interior Design |

**Art**

| Slug | Name |
|---|---|
| `sketching` | Sketching |
| `drawing` | Drawing |
| `painting` | Painting |
| `digital-painting` | Digital Painting |
| `illustration` | Illustration |
| `calligraphy` | Calligraphy |
| `comics` | Comics & Sequential Art |

**Business**

| Slug | Name |
|---|---|
| `entrepreneurship` | Entrepreneurship |
| `management` | Management |
| `project-management` | Project Management |
| `product-management` | Product Management |
| `sales` | Sales |
| `communication` | Communication |
| `human-resources` | Human Resources |
| `operations` | Operations |
| `business-strategy` | Business Strategy |
| `e-commerce` | E-Commerce |

**Finance & Accounting**

| Slug | Name |
|---|---|
| `accounting` | Accounting & Bookkeeping |
| `finance` | Finance |
| `investing` | Investing & Trading |
| `financial-modeling` | Financial Modeling & Analysis |
| `cryptocurrency` | Cryptocurrency & Blockchain |
| `taxes` | Taxes |

**Marketing**

| Slug | Name |
|---|---|
| `digital-marketing` | Digital Marketing |
| `seo` | Search Engine Optimization |
| `social-media-marketing` | Social Media Marketing |
| `content-marketing` | Content Marketing |
| `branding` | Branding |
| `paid-advertising` | Paid Advertising |
| `marketing-analytics` | Marketing Analytics |

**Office Productivity**

| Slug | Name |
|---|---|
| `microsoft-office` | Microsoft Office |
| `google-workspace` | Google Workspace |
| `apple-productivity` | Apple Productivity |
| `sap` | SAP |
| `data-entry-automation` | Automation & Productivity Tools |

**Personal Development**

| Slug | Name |
|---|---|
| `career-development` | Career Development |
| `leadership` | Leadership |
| `personal-productivity` | Personal Productivity |
| `public-speaking` | Public Speaking |
| `confidence` | Self-Esteem & Confidence |
| `stress-management` | Stress Management |

**Photography & Video**

| Slug | Name |
|---|---|
| `digital-photography` | Digital Photography |
| `video-production` | Video Production |
| `photo-editing` | Photo Editing |
| `cinematography` | Cinematography |

**Health & Fitness**

| Slug | Name |
|---|---|
| `fitness` | Fitness |
| `yoga` | Yoga |
| `nutrition` | Nutrition & Diet |
| `mental-health` | Mental Health |
| `sports` | Sports |
| `meditation` | Meditation |

**Music**

| Slug | Name |
|---|---|
| `music-production` | Music Production |
| `instruments` | Instruments |
| `music-fundamentals` | Music Fundamentals |
| `vocals` | Vocals |
| `music-software` | Music Software |

**Language Learning**

| Slug | Name |
|---|---|
| `english` | English |
| `academic-english` | Academic English & IELTS |
| `chinese` | Chinese |
| `japanese` | Japanese |
| `korean` | Korean |
| `spanish` | Spanish |
| `french` | French |
| `german` | German |

**Teaching & Academics**

| Slug | Name |
|---|---|
| `teacher-training` | Teacher Training |
| `math` | Math |
| `science` | Science |
| `humanities` | Humanities |
| `social-science` | Social Science |
| `test-prep` | Test Prep |
| `engineering` | Engineering |

合计：15 大类、约 105 个叶子。足够覆盖主流职业培训目录；工具级名词（React、Photoshop）不进词表，避免和具体课名重复。Frontend React 课打 `frontend-development` + `web-development` 即可。

### 9.3 现有种子课如何打标

| Code | Title | Level | 叶子 slug |
|---|---|---|---|
| INFT 2051 | UI/UX Design Fundamentals | Beginner | `user-experience-design`, `ui-visual-design` |
| INFT 2002 | Frontend React Bootcamp | Beginner | `frontend-development`, `web-development` |
| INFT 3030 | Cybersecurity Essentials | Intermediate | `cybersecurity`, `network-security` |
| INFT 4010 | UX Research Methods | Intermediate | `ux-research`, `user-experience-design` |
| INFT 4025 | Responsible AI for Learning Design | Advanced | `generative-ai`, `user-experience-design` |

演示学员 Huang：引导结果视为 Professional + `user-experience-design` + `cybersecurity`（与现有 LearningGoals 文案一致）。其已完成 INFT 2002，首页走升学：Frontend / Web 的 Intermediate，而不是冷启动。

为方便演示「新用户高星初级」：给 INFT 2051、INFT 2002 各写 2–3 条种子评分（其他演示账号或仅结构允许的 fixture），平均分明显高于无评分课。

## 10. 测试要点

- 种子后 `GET /api/interests` 含 15 个大类，且 `cybersecurity` 的 parent 为 `it-software`，`sketching` 的 parent 为 `art`。
- 绑大类 id 到课程或 UserInterest → 400 `INTEREST_NOT_LEAF`。
- Skip 引导 → 推荐全是 Beginner。
- 只选 `cybersecurity` → 冷启动不含 Sketching 课。
- Completed 且考核全过 → 出现下一级；未过 → 同级其他课。
- Advanced 完成且同标签无更高 → `mode = mastery`。
- 1 条 5 星 vs 20 条 4.5 星：贝叶斯下后者 score 更高。
- 同一用户对同一课第二次评分：仍 1 票，平均分按覆盖后的星计算。
- 未 Complete 评分 → 400。
- Creator 0 个叶子提交发布 → 400。
- Trainer 考核未过就 Complete → 400。

前端：引导三步、Skip、首页推荐区、评分弹窗、Creator 多选叶子。

## 11. 实现时改哪些地方（边界，不是计划任务）

- 实体 / `CoLearnXDbContext` / `SeedData`（词表 + 课打标 + 演示评分 + 学员兴趣）
- 新 Service：兴趣树、推荐、评分、结课校验
- `ApiControllers`、Creator 课程 DTO、Trainer later-phase
- 前端：`RegisterPage` 之后的 onboarding 路由、`MemberHomePage`、`MemberCourseDetailPage`、Account 改兴趣、`CreatorCourseFormPage`、`TrainerLearnersPage`
- 集成测试覆盖第 10 节

`AppServices.cs` 已偏大：推荐和评分不要再塞进去。

## 12. 与原文档的关系

- UC-01 → UC-03 → UC-06：`docs/diagrams/member_use_case.mmd`
- 三步线框：`docs/wireframes/drawio/CoLearnX-Member-Wireframe.drawio`（03/04/05）
- 评分弹窗：同文件 `21-Modal-Rating`（MBR-07）
- 表：`Interest`、`UserInterest`、`UserPreference`、`ProgramRating` 已在代码中，此前未接业务

实现完成后更新 `docs/FEATURES.md` 与 `docs/CODE_OVERVIEW.md`。
