# 开发日志：Azure、PayPal 线上联调与冒烟（2026-09-10）

在 Visual Studio 里对 HTTPS 配置文件开 **Dev Tunnel**，所有人用**同一条**隧道 URL（Vite 端口 **55128**，不是 7238）。SQLite 在主机上，Azure 在云端；换一台电脑单独 F5 看不到同一份数据。改完 user-secrets 后必须重启 F5。

在 `CoLearnX.Server` 目录执行下面的 `dotnet user-secrets`。不要把连接串或 PayPal Secret 提交进 Git。

## 1. 配置 Azure Blob

1. Azure 门户新建 Storage Account，复制 **Access key** 里的 Connection string。
2. 容器名用 `materials`（代码在首次上传时会 `CreateIfNotExists`）。
3. 写入本地密钥：

```powershell
cd CoLearnX.Server
dotnet user-secrets set "Storage:ConnectionString" "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;EndpointSuffix=core.windows.net"
dotnet user-secrets set "Storage:Container" "materials"
```

4. 重启后控制台应出现 `File storage: Azure container=materials cloudLinks=True`。`cloudLinks=False` 时仍能上传/下载，但不能复制 SAS 链接。
5. 不配连接串时文件只在主机 `App_Data/uploads`，隧道外的机器读不到物理文件。

## 2. 重新接上 PayPal（隧道 / 线上）

PayPal 只认当前浏览器的 origin。隧道演示请用 `https://xxxx.asse.devtunnels.ms/member/payment`，不要用 `https://localhost:7238`。

**Sandbox（课程演示）：**

1. [PayPal Developer](https://developer.paypal.com/dashboard/) 建 Sandbox App，记下 Client ID / Secret。
2. Sandbox Business 账户要能收 **AUD**（`TRANSACTION_REFUSED` 多半是币种未开）。
3. 写入密钥：

```powershell
cd CoLearnX.Server
dotnet user-secrets set "PayPal:ClientId" "<sandbox-client-id>"
dotnet user-secrets set "PayPal:ClientSecret" "<sandbox-secret>"
dotnet user-secrets set "PayPal:Mode" "Sandbox"
dotnet user-secrets set "PayPal:BaseUrl" "https://api-m.sandbox.paypal.com"
dotnet user-secrets set "PayPal:Currency" "AUD"
```

4. 前端 `vite.config.js` 已 `allowedHosts: true`。用隧道打开站点 → Member 登录 → Credit Wallet → 点套餐上的 PayPal 按钮。
5. 返回 URL 白名单只有 `localhost` / `127.0.0.1` 和 `*.devtunnels.ms`，路径必须是 `/member/payment`。自定义生产域名需要改 `PayPalReturnUrls` 后再发版。

**Live（真收款）：** 换 Live App 的 Client ID / Secret，并把 `PayPal:Mode` 设为 `Live`、`PayPal:BaseUrl` 设为 `https://api-m.paypal.com`。`Mode` 和 `BaseUrl` 必须一起改。

未配置时支付页会提示 sandbox 未配置，且不会出现可用按钮。

## 3. 粗测功能链路（含资料出现在别人页面）

两个浏览器（或无痕）打开**同一条**隧道 URL。没有 WebSocket：对方要点 **Refresh** 或重新进入该页。

| 步骤 | 谁 | 做什么 | 别人应看到什么 |
|---|---|---|---|
| 1 | Creator | Create Course，附带 PDF，Save and submit | Admin `/admin/approvals`（课程审核）刷新后出现 Pending |
| 2 | Admin | 发布课程；Later Phase 材料队列批准该版本 | Creator 资料状态变 Approved；Trainer 该课可选材料出现新文件 |
| 3 | Trainer | 对 Published 课建 Intake + 有座位的 Session，提交 | Creator Intake applications 刷新后可确认 |
| 4 | Creator | 确认 Intake | Member 目录能搜到并可报名（有余座） |
| 5 | Member | `/member/payment` PayPal 充值，再报名 | 余额增加；Trainer Learner List 刷新后出现该学员 |
| 6 | Trainer | Intake 资源里 Attach 刚批准的材料 | 已报名 Member 打开该课 Learning Hub / 资料入口并刷新，应能打开同一文件 |

上传后立刻：Admin 材料队列刷新即可看到 Pending 记录；文件在 Azure 时，下载走同一 Blob，不必等对方本机有副本。未批准前 Member 不能下载。

**失败时先看：** F5 是否已重启、启动日志是 Azure 还是 local、PayPal 是否用了 55128 隧道 URL、在线 Session 容量是否为 0。
