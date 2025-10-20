using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Microsoft.IdentityModel.Tokens;
using SProtectAgentWeb.Api.Configuration;
using SProtectAgentWeb.Api.Database;
using SProtectAgentWeb.Api.Services;
using SProtectAgentWeb.Api.Sessions;
using SProtectAgentWeb.Api.Utilities;

var builder = WebApplication.CreateBuilder(args);

// Load configuration from appsettings.json
builder.Configuration
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

var appConfig = new AppConfig();
builder.Configuration.Bind(appConfig);

builder.Services.Configure<AppConfig>(builder.Configuration);
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<AppConfig>>().Value);

if (!string.IsNullOrWhiteSpace(appConfig.Server.Host) && appConfig.Server.Port > 0)
{
    builder.WebHost.UseUrls($"http://{appConfig.Server.Host}:{appConfig.Server.Port}");
}

if (string.IsNullOrWhiteSpace(appConfig.Jwt.Secret) || appConfig.Jwt.Secret.Length < 32)
{
    throw new InvalidOperationException("JWT密钥未配置或长度不足，请在配置文件中提供至少32位的安全密钥。");
}

// Core services
builder.Services.AddSingleton<DatabaseManager>();
builder.Services.AddSingleton<PermissionHelper>();
builder.Services.AddSingleton<AdminPermissionHelper>();
builder.Services.AddSingleton<UsageDistributionCacheRepository>();
builder.Services.AddSingleton<IpLocationCacheRepository>();
builder.Services.AddSingleton<LanzouLinkService>();
builder.Services.AddSingleton<CardVerificationRepository>();
builder.Services.AddSingleton<SettlementRateService>();
builder.Services.AddSingleton<SettlementLifecycleService>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<ClientIpResolver>();
builder.Services.AddSingleton<TokenSessionStore>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton<HeartbeatRegistry>();
builder.Services.AddSingleton<ChatService>();


// Domain services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<SoftwareService>();
builder.Services.AddScoped<AgentService>();
builder.Services.AddScoped<CardService>();
builder.Services.AddScoped<CardTypeService>();
builder.Services.AddScoped<SystemInfoService>();
builder.Services.AddScoped<BlacklistService>();
builder.Services.AddScoped<CardVerificationService>();
builder.Services.AddScoped<LinkAuditService>();
builder.Services.AddHostedService<UsageDistributionBackgroundService>();
builder.Services.AddHostedService<ChatCleanupBackgroundService>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<SessionManager>();

builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "sessionid";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.IdleTimeout = TimeSpan.FromHours(3);
    options.Cookie.IsEssential = true;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy
                .AllowAnyOrigin()      // 允许任何来源（包含任何域名和端口）
                .AllowAnyHeader()      // 允许任何请求头
                .AllowAnyMethod();     // 允许任何 HTTP 方法
        });
});


var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(appConfig.Jwt.Secret));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateIssuer = !string.IsNullOrWhiteSpace(appConfig.Jwt.Issuer),
            ValidIssuer = string.IsNullOrWhiteSpace(appConfig.Jwt.Issuer) ? null : appConfig.Jwt.Issuer,
            ValidateAudience = !string.IsNullOrWhiteSpace(appConfig.Jwt.Audience),
            ValidAudience = string.IsNullOrWhiteSpace(appConfig.Jwt.Audience) ? null : appConfig.Jwt.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(Math.Clamp(appConfig.Jwt.ClockSkewSeconds, 0, 300))
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var tokenId = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Jti)
                               ?? context.Principal?.FindFirstValue(ClaimTypes.Sid);

                if (string.IsNullOrWhiteSpace(tokenId))
                {
                    context.Fail("Token 缺少唯一标识");
                    return Task.CompletedTask;
                }

                var store = context.HttpContext.RequestServices.GetRequiredService<TokenSessionStore>();
                if (!store.Exists(tokenId))
                {
                    context.Fail("Token 已失效");
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddControllers(options =>
    {
        options.Filters.Add(new ProducesAttribute("application/json"));
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.WriteIndented = false;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SProtectAgentWeb API",
        Version = "v1",
        Description = "ASP.NET Core implementation of the original Go backend",
    });

    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename), includeControllerXmlComments: true);
});

var app = builder.Build();


app.UseCors("AllowLocalhost");

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SProtectAgentWeb API v1");
    c.RoutePrefix = "swagger";
});

app.UseSession();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(ApiResponse.Success("SProtectAgentWeb backend running")));

app.Run();

