using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using SProtectPlatform.Api.Controllers;
using SProtectPlatform.Api.Data;
using SProtectPlatform.Api.Options;
using SProtectPlatform.Api.Services;
using SProtectPlatform.Api.Utilities;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MySqlOptions>(builder.Configuration.GetSection("MySql"));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<EncryptionOptions>(builder.Configuration.GetSection("Encryption"));
builder.Services.Configure<ForwardingOptions>(builder.Configuration.GetSection("Forwarding"));
builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection("Cors"));
builder.Services.Configure<HttpsOptions>(builder.Configuration.GetSection("Https"));
builder.Services.Configure<WeChatOptions>(builder.Configuration.GetSection("WeChat"));

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();
builder.Services.AddCors();

builder.Services.AddSingleton<IMySqlConnectionFactory, MySqlConnectionFactory>();
builder.Services.AddHostedService<DatabaseInitializer>();

builder.Services.AddSingleton<IPasswordService, PasswordService>();
builder.Services.AddSingleton<ICredentialProtector, CredentialProtector>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthorService, AuthorService>();
builder.Services.AddScoped<IAgentService, AgentService>();
builder.Services.AddScoped<IBindingService, BindingService>();
builder.Services.AddScoped<ICardVerificationForwarder, CardVerificationForwarder>();
builder.Services.AddSingleton<ICorsOriginService, CorsOriginService>();
builder.Services.AddSingleton<IWeChatAccessTokenProvider, WeChatAccessTokenProvider>();
builder.Services.AddScoped<IWeChatBindingService, WeChatBindingService>();
builder.Services.AddScoped<IWeChatMiniProgramService, WeChatMiniProgramService>();
builder.Services.AddScoped<IWeChatTemplateDataFactory, WeChatTemplateDataFactory>();
builder.Services.AddScoped<IWeChatMessageService, WeChatMessageService>();


builder.Services.AddHttpClient(nameof(ProxyController));
builder.Services.AddHttpClient(nameof(CardVerificationForwarder));
builder.Services.AddHttpClient(nameof(IWeChatAccessTokenProvider));
builder.Services.AddHttpClient(nameof(IWeChatMiniProgramService));
builder.Services.AddHttpClient(nameof(IWeChatMessageService), client =>
{
    client.DefaultRequestHeaders.ExpectContinue = false;
    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    client.DefaultRequestHeaders.UserAgent.Clear();
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) SProtectPlatform/1.0");
});
builder.Services.AddHttpClient(nameof(WeChatTemplateDataFactory));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
var signingKey = Encoding.UTF8.GetBytes(jwtOptions.SigningKey ?? string.Empty);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = !string.IsNullOrWhiteSpace(jwtOptions.Issuer),
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = !string.IsNullOrWhiteSpace(jwtOptions.Audience),
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(signingKey),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });

builder.Services.AddAuthorization();

builder.WebHost.ConfigureKestrel((context, options) =>
{
    var httpsOptions = context.Configuration.GetSection("Https").Get<HttpsOptions>();
    if (httpsOptions?.Enabled == true)
    {
        var httpPort = httpsOptions.HttpPort ?? 5000;
        var httpsPort = httpsOptions.HttpsPort ?? 5001;

        if (string.IsNullOrWhiteSpace(httpsOptions.CertificatePath))
        {
            throw new InvalidOperationException("HTTPS is enabled but no certificate path was provided.");
        }

        var certificatePath = httpsOptions.CertificatePath;
        if (!Path.IsPathRooted(certificatePath))
        {
            certificatePath = Path.Combine(context.HostingEnvironment.ContentRootPath, certificatePath);
        }

        if (!File.Exists(certificatePath))
        {
            throw new InvalidOperationException($"HTTPS certificate not found at '{certificatePath}'.");
        }

        options.ListenAnyIP(httpPort);
        options.ListenAnyIP(httpsPort, listenOptions =>
        {
            listenOptions.UseHttps(certificatePath, httpsOptions.CertificatePassword);
        });
    }
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()   // ✅ 不限制来源
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});


var app = builder.Build();
app.UseDefaultFiles();   // 支持默认加载 index.html
app.UseStaticFiles();    // 启用 wwwroot 静态资源服务
app.MapGet("/", context =>
{
    context.Response.Redirect("index.html", permanent: false);
    return Task.CompletedTask;
});

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
