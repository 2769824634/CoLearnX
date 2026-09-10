# 课程资料、Azure 存储与 PayPal 充值

这份说明覆盖 `feat/materials-paypal-azure` 相对 `master` 新增的能力。B/D Trainer–Admin 流程仍以仓库根目录 `README.md` 为准；Azure / PayPal 密钥与线上冒烟步骤见 [`DEVLOG_20260910_AZURE_PAYPAL.md`](DEVLOG_20260910_AZURE_PAYPAL.md)。

| 项目 | 内容 |
|---|---|
| 范围 | Creator 课程资料、Azure Blob、PayPal 积分充值、下载权限 |
| 密钥 | 只写在 `dotnet user-secrets` 或环境变量，不进 Git |
| 存储回退 | 未配置 Azure 时写入 `CoLearnX.Server/App_Data/uploads`（已 gitignore） |

## 1. 课程资料绑定到 Course

资料必须属于一门 Course，不能再作为孤立文件上传。

- **Create Course**（`/creator/courses/new`）：保存草稿时可同时选 PDF / PPTX / DOCX / PNG / JPG（单文件 ≤ 20 MB）；课程尚未落库时先排队，保存成功后再上传。
- **Upload Material**（`/creator/upload`）：必须先选 Course，列表只显示该课资料。
- 后端 `POST /api/materials` 的 `courseId` 必填；写入 `LearningMaterial`、`CourseMaterialVersion` 和 `CourseMaterials`。Creator 只能给自己的课上传。
- `GET /api/materials?courseId=` 按课过滤。
- Course 处于 Pending / Published 时表单锁定，不能再从创建页追加文件。

状态：上传后为 `PendingApproval`。Admin 在 Later Phase 材料队列批准后，Trainer 才能绑到 Intake。

## 2. 四角色链路

```text
Creator  创建 Course → 上传资料 → Submit
Admin    /admin/login 发布课程，并批准材料版本
Trainer  对 Published 课程建 Intake / Session → 提交
Creator  确认 Intake
Member   目录搜索 → 用积分报名（Session 必须有余座）
Trainer  把已批准资料绑到 Intake，学员刷新后可见
```

注意：

- Admin 入口只在地址栏输入 `/admin/login`，普通登录页没有链接。
- 在线 Session 若 `PhysicalCapacity = 0`，报名会判满员。演示报名请用有座位的线下 Session，或把在线 Session 容量设成大于 0。
- 未批准的文件只有上传者可下载；Member / 其他角色看不到云链接，也不能把 pending 文件当教材使用。

## 3. Azure Blob

`Storage:ConnectionString` 非空时使用 Azure Blob（默认容器 `materials`），启动日志会出现 `File storage: Azure`。空字符串则用本机磁盘。

配置了 Azure 后，同一进程上的所有浏览器会话读写同一容器；Dev Tunnel 下其他人刷新对应页面即可看到新记录。复制云链接需要账户密钥能签发 SAS。

## 4. PayPal 积分

- 套餐为固定 AUD 档位；Member `/member/payment` 使用 PayPal Buttons。
- 创建订单会带 `returnUrl`（当前页 origin + `/member/payment`）。服务端只接受 `localhost` / `127.0.0.1` 或 `*.devtunnels.ms`，且路径必须以 `/member/payment` 开头。
- 从 PayPal 跳回时用 `?token=` / `orderID` 自动 capture；`ORDER_ALREADY_CAPTURED` 视为成功。
- 已配置 PayPal 时禁用模拟 `POST /api/credits/topup`。
- Sandbox / Live 的 ClientId、Secret、`Mode`、`BaseUrl` 用 user-secrets，不要写进 `appsettings.json`。

## 5. 演示账号

| 角色 | 入口 | 邮箱 | 密码 |
|---|---|---|---|
| Member | `/login` | `huang.yousheng@colearnx.com` | `Password123!` |
| Trainer | `/login` | `gu.yincheng@colearnx.com` | `Password123!` |
| Creator | `/login` | `zou.ruiqi@colearnx.com` | `Password123!` |
| Admin | 手动打开 `/admin/login` | `zhu.zirui@colearnx.com` | `Password123!` |

旧 SQLite 若报 Course Core 需要全新库：停掉 F5，删除 `CoLearnX.Server/colearnx-later-v1.db` 及其 `-wal` / `-shm`，再启动。
