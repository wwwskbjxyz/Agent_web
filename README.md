# SProtect SaaS 系统使用指南

本仓库提供一套由 **SProtectPlatform.Api** 与 **SProtectAgentWeb.Api** 组成的 SaaS 平台：

| 模块 | 说明 | 默认端口 | 主要依赖 |
| --- | --- | --- | --- |
| `SProtectPlatform.Api` | 平台级后端，负责作者/代理账号体系、绑定关系、请求转发与微信公众号消息 | `5000`（可配置） | .NET 8、MySQL | 
| `SProtectPlatform.Api/wwwroot` | 平台前端构建产物，由 `SProtectPlatform.Api` 托管静态资源与运行时配置 | 随后端 | 浏览器即可访问 |
| `SProtectAgentWeb.Api` | 兼容原有作者端（客户端）接口，读取作者侧 SQLite 数据，供平台反向代理调用 | `8080`（可配置） | .NET 8、SQLite 原始数据、可选 MySQL |

## 1. 环境准备

在部署任一模块前，请先安装/准备：

- [.NET 8 SDK](https://dotnet.microsoft.com/) 与运行时（两个 API 均基于 ASP.NET Core）。
- MySQL 8.x（或兼容版本），用于 `SProtectPlatform.Api` 持久化平台数据。
- 已有的作者端 SQLite 数据目录（即原始 `idc` 文件夹等），供 `SProtectAgentWeb.Api` 读取。
- 可选：Node.js 18+（仅当你希望自行重新构建前端时需要）。

> Windows、Linux 均可部署。若运行在 Linux，请确保为 `SProtectAgentWeb.Api` 配置好 `NativeBinaries/sp_sqlite_bridge.dll` 对应的平台版本（仓库已包含 Windows x64 版本，如需 Linux 版需替换为对应动态库）。

## 2. 快速上手

### 2.1 配置并启动平台后端 `SProtectPlatform.Api`

1. 创建数据库：
   ```sql
   CREATE DATABASE `sprotect_platform` DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
   ```
2. 打开 `SProtectPlatform.Api/appsettings.json`，至少修改以下字段：
   - `MySql:ConnectionString`：填写数据库地址、账号密码等。
   - `Jwt:SigningKey`：填写 32 位以上随机密钥。
   - `Encryption:CredentialKey`：同样为 32 位以上随机密钥，用于加密作者凭据。
   - 如需前端跨域访问，追加 `Cors:AdditionalOrigins` 中的域名。
3. 在命令行进入项目目录并启动：
   ```bash
   cd SProtectPlatform.Api
   dotnet restore
   dotnet run
   ```
   启动时会自动执行数据库初始化/升级，创建作者、代理、绑定、微信等表结构，无需额外迁移脚本。

### 2.2 配置运行平台前端

- 平台前端已打包在 `SProtectPlatform.Api/wwwroot`，默认随后端提供静态页面，无需额外构建。
- 若要改动 API 地址等运行时配置，编辑 `wwwroot/config/runtime-config.js` 中的 `apiBaseUrl`（键名为 `__SPROTECT_RUNTIME_CONFIG__`）。
- 访问 `http://<platform-host>:<port>/` 即可打开平台 UI。

### 2.3 配置作者端后端 `SProtectAgentWeb.Api`

1. 将原作者端 SQLite 数据目录放在合适位置，并在 `appsettings.json` 的 `服务器设置` 节中设置：
   - `数据库路径`/`DatabasePath`：指向 SQLite 根目录（例如 `idc`）。
   - `服务器地址`/`Host`、`端口`/`Port`：指定监听地址与端口。
   - `软件类型`/`SoftwareType`：标识当前作者端的软件类型，供平台区分。
2. 在 `认证设置` 节配置 JWT：
   - `JWT密钥`：不少于 32 位的随机字符串。
   - 可按需调整签发者、受众、过期时间等。
3. （可选）如需启用蓝奏云相关统计或其他扩展，可在 `数据库` 节配置 MySQL 连接。
4. 启动：
   ```bash
   cd SProtectAgentWeb.Api
   dotnet restore
   dotnet run
   ```
   若使用 systemd/Windows 服务部署，可改为 `dotnet publish -c Release` 后部署可执行文件。

### 2.4 关联平台与作者端

1. 在平台前端/Swagger 中注册作者账号，并填写作者端 `API 地址`、`端口` 与 `软件类型`。
2. 代理登录平台后，绑定对应的软件码及作者账号密码。平台会使用 `Encryption:CredentialKey` 加密后存储。
3. 平台代理入口调用 `/api/proxy/{软件码}/...` 时，会自动转发请求到对应作者端，并附带代理绑定凭据。

## 3. `SProtectPlatform.Api` 配置详解

`appsettings.json` 中的主要配置项如下（均可通过环境变量覆盖，例如 `MySql__ConnectionString`）：

| 配置节 | 关键字段 | 说明 |
| --- | --- | --- |
| `MySql` | `ConnectionString` | MySQL 连接字符串。启动时会自动建表/补充索引。|
| `Jwt` | `Issuer`、`Audience`、`SigningKey`、`AccessTokenMinutes` | 平台 JWT 设置，`SigningKey` 必须 ≥32 字符。|
| `Encryption` | `CredentialKey` | AES 加密密钥（≥32 字符）。|
| `Cors` | `AdditionalOrigins` | 除内置 `localhost/127.0.0.1` 外的前端域名白名单。|
| `Forwarding` | `RequestTimeoutSeconds` | 平台代理作者端接口时的超时秒数。|
| `WeChat` | `AppId`、`AppSecret`、`Templates`、`Previews`、`TokenSafetyMarginSeconds` | 小程序订阅消息所需的凭据及模板预览文案。|
| `Https` | `Enabled`、`HttpPort`、`HttpsPort`、`CertificatePath`、`CertificatePassword` | 启用 HTTPS 并指定证书路径/口令。|

> 若启用 HTTPS，请确保 `CertificatePath` 为绝对路径或相对 `ContentRoot` 的路径，且证书存在。Kestrel 将在配置无误时同时监听 HTTP 与 HTTPS 端口。

### 自动化维护

- `DatabaseInitializer` 启动时会检查/创建 `Authors`、`Agents`、`AuthorSoftwares`、`Bindings`、`AllowedOrigins`、`WeChat*` 等表，并补齐缺失字段、索引与默认值，适合持续迭代升级。
- 平台使用内置 JWT 与 AES 服务管理账号密码，相关服务定义在 `Services` 目录中，可根据需要扩展。
- 平台默认允许所有来源访问（`AllowAll` CORS 策略），如需收紧可在 `Program.cs` 中调整策略或结合数据库白名单实现精细控制。

## 4. `SProtectAgentWeb.Api` 配置详解

`SProtectAgentWeb.Api/appsettings.json` 支持中英文混合键名，重要配置如下：

| 配置节 | 关键字段 | 说明 |
| --- | --- | --- |
| `服务器设置` / `Server` | `Host`、`Port`、`DatabasePath`、`SoftwareType`、`权限` | 监听地址/端口、SQLite 根目录、软件类型以及超级用户账号列表（逗号分隔）。|
| `认证设置` / `Jwt` | `JWT密钥`、`JWT签发者`、`JWT受众`、`JWT访问令牌过期分钟数`、`JWT容许时间偏差秒数` | 用于生成作者端/代理端登录 Token。|
| `数据库` | `主机`、`端口`、`数据库`、`用户名`、`密码` | 可选的 MySQL 连接，供扩展功能使用（例如蓝奏云链接审计）。|
| `Heartbeat` | `Secret`、`ExpirationSeconds` | 心跳接口校验密钥及过期策略。|
| `聊天设置` | `保留小时数`、`允许图片消息`、`允许表情包`、`最大图片大小KB` | 内置客服聊天模块的行为配置。|

### 数据文件与原生依赖

- `Server.DatabasePath` 需指向包含作者端 SQLite 数据库文件（如 `data.db`、`agent.db` 等）的目录。程序会自动组合路径读取，不需要包含文件名。
- 请确保 `NativeBinaries` 目录与可执行文件位于同一层级，以便加载 `sp_sqlite_bridge` 原生库与作者端原有的 SQLite 函数。

### 运行与发布

- 开发调试：`dotnet run` 会读取当前目录下的配置文件；调试前请确认已生成 `appsettings.Development.json` 或修改默认配置。
- 生产部署：
  ```bash
  dotnet publish -c Release -o publish
  cd publish
  ./SProtectAgentWeb.Api
  ```
  可结合反向代理（Nginx/IIS）或 systemd/Windows 服务运行。

## 5. 与平台前端的协同

- 平台前端通过 `runtime-config.js` 中的 `apiBaseUrl` 连接 `SProtectPlatform.Api`，请根据实际部署地址修改。
- 静态资源（`wwwroot/assets`）为打包后的前端文件。若需要重新构建，可在单独的前端源码仓库进行构建，再覆盖到 `wwwroot`。
- `Program.cs` 已启用默认文件和静态文件服务，因此发布后端即可同时提供前端页面。

## 6. 常见问题

1. **数据库连接失败**：请确认 MySQL 账号具备建表权限，并检查 `ConnectionString` 与数据库字符集设置（推荐 `utf8mb4`）。
2. **JWT 校验报错**：两端都需要至少 32 字符的密钥，且平台/作者端的签发者、受众需与配置一致。
3. **代理无法访问作者端接口**：确认平台中绑定的软件码指向了正确的作者端地址/端口，并确保作者端对外网络可达。
4. **静态前端访问空白页**：通常是 `runtime-config.js` 中 API 地址错误或浏览器跨域限制。请检查平台日志的 CORS 配置与网络请求。
5. **加载 SQLite 插件失败**：检查 `sp_sqlite_bridge.dll` 是否匹配当前系统架构；在 Linux 下需将其替换为 `.so` 文件并更新 `SqliteBridge` 加载逻辑。

如需二次开发，可在各 API 的 `Controllers` 与 `Services` 目录中扩展业务逻辑，保持配置字段与数据库结构同步即可。
