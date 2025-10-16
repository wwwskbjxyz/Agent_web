# SProtectPlatform.Api 部署跨域与 HTTPS 配置

## 允许前端域名跨域访问

SProtectPlatform.Api 通过数据库中的 `AllowedOrigins` 表以及 `appsettings.json` 配置来动态生成允许的跨域来源。当小程序或 Web 前端部署在服务器的 80 端口时，需要把实际的域名加入白名单，否则浏览器会因为端口不同而拒绝请求。

1. **数据库白名单**：在 `AllowedOrigins` 表中插入完整的 Origin，例如 `http://example.com`、`https://example.com`。
2. **应用配置白名单**：`appsettings.json` 中的 `Cors:AdditionalOrigins` 允许添加静态 Origin，发布时可直接把前端部署域名写入。例如：

   ```json
   "Cors": {
     "AdditionalOrigins": [
       "http://example.com",
       "https://example.com"
     ]
   }
   ```

应用会自动合并数据库与配置中的列表。如果请求来源不在名单内，日志会打印 `Rejected CORS request` 方便排查。

> 提示：跨域规则区分端口号，`http://example.com` 与 `http://example.com:8080` 需要分别配置。

## 在 Linux/Windows 上启用 HTTPS

1. **准备证书**：将 `.pfx` 证书放入 `SProtectPlatform.Api` 目录下的 `certificates` 文件夹（可自建），并记录证书密码。
2. **编辑配置**：在 `appsettings.json` 的 `Https` 节点中启用 HTTPS 并指向证书：

   ```json
   "Https": {
     "Enabled": true,
     "HttpPort": 5000,
     "HttpsPort": 5001,
     "CertificatePath": "certificates/platform.pfx",
     "CertificatePassword": "证书密码"
   }
   ```

   - `HttpPort` 为可选的 HTTP 监听端口（默认 5000）。
   - `HttpsPort` 为 HTTPS 监听端口。
   - `CertificatePath` 支持相对路径，会以应用根目录为基准。

3. **开放端口**：确保服务器防火墙放行 `HttpPort` 与 `HttpsPort`。
4. **启动服务**：`dotnet SProtectPlatform.Api.dll` 启动后会同时监听 HTTP 与 HTTPS，浏览器即可通过 `https://域名:HttpsPort` 访问。

如证书路径配置错误，程序启动时会抛出 `HTTPS certificate not found` 提示，便于立即发现问题。
