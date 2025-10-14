# SProtect 多架构系统

本仓库包含三大模块：

1. **SProtectPlatform.Api** — .NET 8 编写的主控后端，使用 MySqlConnector 访问 MySQL，负责作者/代理注册登录、软件码绑定、动态 CORS 与请求转发。
2. **SProtectAgentWeb.Api** — 现有的作者侧后端（SQLite 查询接口），保持原有逻辑，仅新增 Host/Port/DatabasePath/SoftwareType 等配置键支持。
3. **UniApp 前端** — 位于 `SProtectAgentWeb.Api/wwwroot/uniapp`，提供统一入口，包含作者注册/登录、代理注册/登录、绑定软件码与软件列表选择等页面，并可通过主控后端转发访问作者接口。

## 目录结构

```
SProtectAgentWeb.Api/       作者端后端（.NET 8）
SProtectPlatform.Api/       主控后端（.NET 8）
SProtectAgentWeb.Api.sln    既有作者端解决方案
SProtectAgentWeb.Api/wwwroot/uniapp/             UniApp 前端工程
```

## 1. 主控后端 `SProtectPlatform.Api`

### 运行前准备

1. 创建 MySQL 数据库（默认名称 `sprotect_platform`）。
2. 在 `SProtectPlatform.Api/appsettings.json` 中配置 `MySql:ConnectionString`、`Jwt:SigningKey` 与 `Encryption:CredentialKey`。
3. 确保 `Encryption:CredentialKey` 至少 32 个字符；用于 AES 加密代理绑定的作者账号密码。
4. 运行项目（示例）：
   ```bash
   cd SProtectPlatform.Api
   dotnet restore
   dotnet run
   ```

### 功能概览

- **作者管理**：`/api/authors/register`、`/api/authors/login`
- **代理管理**：`/api/agents/register`、`/api/agents/login`、`/api/agents/me`
- **绑定管理**：`/api/bindings`（增删查），支持 `PUT /api/bindings/{id}` 更新绑定凭证，自动加密保存作者账号密码
- **作者控制台**：`/api/authors/me`（查询与更新作者接口信息）、`/api/authors/me/regenerate-code`（生成新软件码）、`DELETE /api/authors/me`（注销账号），需作者登录后携带 JWT 访问
- **动态 CORS**：读取 `AllowedOrigins` 表并默认放行 `localhost` / `127.0.0.1`（含 8080 端口与 HTTPS），确保前端开发端口 8080 可直接访问
- **请求转发**：`/api/proxy/{softwareCode}/...` 将代理请求转发至作者端，同时携带绑定凭据，支持透传作者登录产生的 Token
- **微信订阅消息**：配置 `WeChat` 段的 AppId/AppSecret 与模板 ID 后，代理或作者可在 `/api/wechat` 系列接口绑定小程序账号、触发即时沟通、黑名单预警、结算通知等订阅消息
- **数据库初始化**：启动时自动创建 `Authors`、`Agents`、`Bindings`、`AllowedOrigins` 四张表

### 表结构（简化）

| 表名          | 主要字段 | 描述 |
| ------------- | -------- | ---- |
| `Authors`     | `Email`, `ApiAddress`, `ApiPort`, `SoftwareType`, `SoftwareCode` | 作者接口信息与唯一软件码 |
| `Agents`      | `Email`, `DisplayName`, `PasswordHash` | 代理账号 |
| `Bindings`    | `AgentId`, `AuthorId`, `AuthorAccount`, `EncryptedAuthorPassword` | 代理-作者绑定关系 |
| `AllowedOrigins` | `Origin` | 动态 CORS 白名单 |
| `WeChatBindings` | `UserType`, `UserId`, `OpenId` | 记录作者/代理与微信 openid 的绑定关系 |
| `WeChatAccessTokens` | `AppId`, `AccessToken`, `ExpiresAtUtc` | 缓存微信全局 access_token |

> **提示**：`WeChat:Previews` 中的键名需要与微信订阅消息模板字段一一对应（例如 `phrase2`、`time3`、`character_string1` 等）。
> 如果模板字段有调整，可在配置文件中直接修改对应键值，前端的预览/测试推送会自动读取更新后的内容。
| `WeChatMessageLogs` | `TemplateKey`, `UserType`, `UserId`, `Success` | 订阅消息发送日志 |

### 请求转发说明

- 前端访问业务接口时调用：`/api/proxy/{softwareCode}/api/...`
- 主控后端验证代理 JWT，读取绑定凭据，必要时附带 `X-SProtect-Author-Account`、`X-SProtect-Author-Password` 以及 `X-SProtect-Remote-Token`
- 若前端携带 `X-SProtect-Remote-Token`，主控后端会将其设置为转发请求的 `Authorization` 头，确保作者端权限验证通过

## 2. 作者端 `SProtectAgentWeb.Api`

原工程保持不变，仅在配置文件与绑定对象中新增英文键支持：

```jsonc
"服务器设置": {
  "服务器地址": "0.0.0.0",
  "Host": "0.0.0.0",
  "端口": 8080,
  "Port": 8080,
  "数据库路径": "idc",
  "DatabasePath": "idc",
  "SoftwareType": "SP",
  "软件类型": "SP"
}
```

后端仍通过 `appsettings.json` 配置 SQLite 路径与监听端口，可作为作者独立部署的接口服务。

## 3. 前端 `SProtectAgentWeb.Api/wwwroot/uniapp`

### 新增/改动页面

- `/pages/login/index` — 登录入口，提供“代理入口”“作者入口”按钮（接口地址现改由源码配置，不再在页面展示）
- `/pages/login/agent` — 默认展示登录表单，支持注册时绑定首个软件码；登录成功后跳转至 `/pages/index/index`
- `/pages/login/author` — 默认展示登录表单；注册成功后提示新软件码并可直接登录主控作者控制台
- `/pages/author/dashboard` — 作者控制台，可更新接口地址/端口/类型、重新生成软件码、注销账号
- `/pages/agent/bind` — 代理软件码管理，新增“编辑凭证”对话框用于修改绑定的作者账号与密码
- `/pages/home` — 精简为绑定管理页（软件码列表与自动登录逻辑保留）
- `/pages/index/index` — 代理主控制台，展示快捷按钮、销量统计、趋势图等原有首页内容

### 运行方式

前端为标准 UniApp/Vue 项目，可在 `SProtectAgentWeb.Api/wwwroot/uniapp` 内使用 HBuilderX 或 `npm`/`pnpm` 构建。开发时默认 API 地址为 `http://127.0.0.1:5050`，可在登录入口页面调整。

### 与主控后端交互流程

1. 代理在主控后端注册/登录 → 获取平台 Token
2. 绑定作者软件码（保存作者端账号/密码）
3. 前端选择某个软件码，主控自动通过 `/api/proxy/{code}` 转发请求至作者端
4. 自动为选择的软件码调用作者端登录（使用绑定凭据），并缓存作者返回的 JWT 供后续接口使用

## 4. 跨域与安全

- 动态 CORS：在 `AllowedOrigins` 表中维护白名单，可按需新增生产域名
- 主控与作者凭据：作者端账号密码在数据库中使用 AES 加密存储
- 代理 Token：使用 JWT（HMAC-SHA256），可通过 `Jwt` 配置调整 Issuer/Audience/有效期

## 5. 开发建议

- 主控后端使用 `MySqlConnector` 官方包连接 MySQL
- 前端默认在 8080 端口开发，结合动态 CORS 可直接访问主控后端
- 当作者端地址、端口、类型变更时，在主控后端更新信息即可，无需重新编译前端
- 如需新增软件类型（QP/API 等），仅需在作者注册处扩展下拉选项及业务映射

## 6. 常见问题

- **为何需要同时保存作者账号密码？** 用于主控自动登录作者端并在转发请求时带上凭据，代理无需重复输入。
- **如何新增允许访问的域名？** 向 `AllowedOrigins` 表插入记录即可，无需重启服务。
- **前端报“未选择软件码”**：请先在绑定页面绑定并选择软件码，或在 `/pages/home` 切换。
- **绑定索引无法自动升级**：若启动日志提示 `UX_Bindings` 无法删除，可参考 `docs/upgrade-binding-index.sql` 中的手动脚本，在数据库中执行后重新启动主控服务。

如需进一步扩展（短信验证码、邮箱找回密码等），可在主控后端增加相应模块并复用现有认证/加密基础设施。
