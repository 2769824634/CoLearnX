# 按课程级别把学分价格限制在价格池区间内

日期：2026-10-28。只写设计，本文件不包含实现。

Beginner、Intermediate、Advanced 都适用。创作者选中哪一级，就只能在那一级的价格池区间里手填报名总学分。

## 背景

价格池已经建好，管理员可以在 Pricing 页查看。创建课程时，创作者仍在「Interests & price」一步手填 `CreditCost`，提示语是 “Whole credits a Member pays to enrol.” 这个数字不读取 `PricePool`。

`CreditCost` 是学员报名支付的总学分，是一个整数。价格池里和这个单位相同的，是课程包单元格的 `MinTotalCredits`–`MaxTotalCredits`。按现有种子数据：

| 级别 | 总学分区间 |
|---|---:|
| Beginner | 10–100 |
| Intermediate | 50–400 |
| Advanced | 150–1000 |

开班单元格存的是每学时学分，而且同一级别有 10 格（档位 A–E × 理论/实践）。课程表单没有领域、课程类型和学时，选不出其中一格。本设计不增加这些字段，也不把每学时价格写进 `CreditCost`。

## 目标

创作者选择 Beginner、Intermediate 或 Advanced 后，仍手填学分价格，但只能填该级别当前课程包价格池那一格的闭区间。保存时服务端用同一条规则拒绝区间外的价格。

三个级别各自用自己的区间，不能共用一个范围。

## 不做什么

- 不增加领域、课程类型、销售方式、学时或分成字段。
- 不把价格池区间复制到 `Course` 上。已保存的 `CreditCost` 保持不变，直到创作者再次保存。
- 不改学员实际支付、报名、开班或管理员审课。
- 不让管理员在界面上编辑价格池单元格。区间来自已有的 `PricePool` 行。
- 不给这三个级别以外的级别加区间。当前种子数据只有这三级；如果以后出现别的级别名称，仍按大于等于 1 的整数保存。

## 用哪一格

对所选级别，查找条件同时成立的那一行：

- `SaleMode` 为 `CoursePack`
- `PriceTierId` 为 `null`
- `CourseType` 为 `Theory`
- `CourseLevel.Name` 与所选级别相同（忽略大小写），名称是 `Beginner`、`Intermediate` 或 `Advanced` 之一

区间是这一行的 `MinTotalCredits` 和 `MaxTotalCredits`，两端都包含。表单和接口都读数据库里的当前值，不把种子表里的数字写死在代码里。

某一级的这一行缺失，或最小值、最大值为空，或最小值大于最大值时，只有这一级不能保存。另外两级仍用自己的行。

## 创作者看到的表单

级别仍在第一步选择，价格仍在「Interests & price」手填。

未选级别：价格框保持现在的行为，`min` 为 1，提示语不变，不能保存。

选中 Beginner、Intermediate 或 Advanced，且该级价格池区间可用：

- 数字框的 `min`、`max` 设为这一级的区间两端，`step` 仍为 1。
- 提示语写明这一级可填的整数区间，例如 Intermediate 显示 “Enter a whole number from 50 to 400 credits.”
- 空值、小数、小于最小值、大于最大值都不能进入保存。错误显示在价格框旁。
- 改选另一个级别时，保留已经填过的数字。数字落在新级别区间外时，留在框里并标出错误，等创作者改到新区间内。
- 三个级别之间切换使用各自的区间，不沿用上一级的 `min` 和 `max`。

选项接口还没返回区间，或当前级别没有可用区间时，不要假装未限制就允许保存。显示加载或错误，保存保持不可用。

## 接口

扩展现有 `GET /api/creator/courses/options`，不新开接口。`CreatorCourseOptionsDto` 增加：

```csharp
IReadOnlyList<CourseLevelPriceBoundDto> LevelPriceBounds
```

```csharp
public record CourseLevelPriceBoundDto(int CourseLevelId, int MinCredits, int MaxCredits);
```

种子数据完整时，列表有三项，分别对应 Beginner、Intermediate、Advanced。某一级没有合格的价格池行时，列表里不放这一级，也不返回没有上下限的项。另外两级照常返回。

创建和更新课程时，`CourseService` 在写入 `CreditCost` 之前检查：

- 所选级别是 Beginner、Intermediate 或 Advanced：必须是整数，且落在该级当前的 `MinTotalCredits`–`MaxTotalCredits` 闭区间内。
- 该级区间不可用：拒绝保存，不写入课程。
- 所选级别不是这三级：沿用现在的校验，`CreditCost` 为大于等于 1 的整数。

失败时沿用课程接口现有的字段错误方式，字段为 `creditCost`：

| 情况 | 状态 | 代码 |
|---|---|---|
| 价格不是该级别区间内的整数 | 400 | `CREDIT_COST_OUT_OF_RANGE` |
| 该级别价格池行不存在或区间无效 | 409 | `PRICE_POOL_UNAVAILABLE` |

错误信息带上当前级别名称，以及这一级的最小值和最大值。区间不可用时说明这一级的价格暂时不能保存。

每次保存都按当时的价格池行检查。之后如果某一行的上下限变了，已经保存的课程价格不动；下次编辑保存时用新区间。

## 测试

后端：

- 选项接口在种子数据下返回三项：Beginner 10–100、Intermediate 50–400、Advanced 150–1000。
- 每一级保存区间两端都成功。Beginner 保存 9 和 101、Intermediate 保存 49 和 401、Advanced 保存 149 和 1001，都返回 400 `CREDIT_COST_OUT_OF_RANGE`，课程价格不变。
- 删掉或清空 Intermediate 的课程包行后，选项接口只返回另外两级；保存 Intermediate 返回 409 `PRICE_POOL_UNAVAILABLE`；Beginner 和 Advanced 仍可按自己的区间保存。
- 已有课程的 `CreditCost` 在未再次保存时保持原值。把一门已保存课程改到另一级时，原价格若落在新区间外，更新被拒绝，原价格和原级别都不变。

前端：

- 选择每一级后，价格提示和输入框的 `min`、`max` 使用选项接口返回的该级区间。
- 区间外的数字不能保存，并显示价格框旁的错误。
- 从 Beginner 改选 Intermediate 或 Advanced 后，提示和输入框改用新级别的区间；原先填的数字若越界，仍留在框里并显示错误。
