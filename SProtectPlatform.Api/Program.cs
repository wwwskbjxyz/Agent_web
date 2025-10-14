using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors.Infrastructure;
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

builder.Services.AddSingleton<ICorsPolicyProvider, DatabaseCorsPolicyProvider>();
builder.Services.AddSingleton<ICorsService, CorsService>();

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

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
