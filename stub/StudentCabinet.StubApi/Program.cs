using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

// Тестовий сервер для лабораторних робіт 4-6 (НЕ для реального використання).
//   http://localhost:5099   - синхронізація (лаб. 4) та автентифікація без шифрування
//   https://localhost:5443  - автентифікація через HTTPS (сертифікат закріплюється в застосунку, лаб. 6)
// Параметр --rogue підміняє сертифікат на випадковий: так імітується атака "людина посередині".

const string Issuer = "StudentCabinet.StubApi";
const string Audience = "StudentCabinet.App";
const string PfxPassword = "stubpass";

var rogue = args.Contains("--rogue");

var users = new Dictionary<string, (string Password, string Role)>(StringComparer.OrdinalIgnoreCase)
{
    ["admin"] = ("Admin#2024", "Administrator"),
    ["teacher"] = ("Teacher#2024", "Teacher")
};

var signingKey = new SymmetricSecurityKey(
    Encoding.UTF8.GetBytes("StudentCabinet-stub-signing-key-0123456789-ABCDEF"));

var certificate = rogue
    ? CreateEphemeralCertificate()
    : new X509Certificate2(Path.Combine(AppContext.BaseDirectory, "certs", "stub-localhost.pfx"), PfxPassword);

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5099);
    options.ListenLocalhost(5443, listen => listen.UseHttps(certificate));
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Без цього "role" перейменовується на довгий URI ClaimTypes.Role і RequireRole не спрацьовує.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            NameClaimType = "sub",
            RoleClaimType = "role",
            ClockSkew = TimeSpan.FromSeconds(10)
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/api/auth/login", (LoginRequest request) =>
{
    if (!users.TryGetValue(request.Login ?? string.Empty, out var user) || user.Password != request.Password)
    {
        app.Logger.LogWarning("Невдалий вхід: {Login}", request.Login);
        return Results.Unauthorized();
    }

    var expiresAt = DateTime.UtcNow.AddMinutes(60);
    var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        Issuer = Issuer,
        Audience = Audience,
        Subject = new ClaimsIdentity(new[]
        {
            new Claim("sub", request.Login!.ToLowerInvariant()),
            new Claim("role", user.Role)
        }),
        Expires = expiresAt,
        SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)
    });

    app.Logger.LogInformation("Успішний вхід: {Login} ({Role})", request.Login, user.Role);
    return Results.Ok(new LoginResponse(token, expiresAt));
});

// Серверна перевірка ролі: клієнтській перевірці довіряти не можна.
app.MapGet("/api/users", () => users.Select(u => new UserDto(u.Key, u.Value.Role)))
    .RequireAuthorization(policy => policy.RequireRole("Administrator"));

app.MapPost("/api/sync", (JsonElement body) =>
{
    app.Logger.LogInformation("Синхронізація: отримано {Body}", body.GetRawText());
    return Results.Ok();
});

if (rogue)
    app.Logger.LogWarning("Режим --rogue: сервер використовує сертифікат, якого застосунок не очікує");

app.Run();

static X509Certificate2 CreateEphemeralCertificate()
{
    using var rsa = RSA.Create(2048);
    var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

    var names = new SubjectAlternativeNameBuilder();
    names.AddDnsName("localhost");
    request.CertificateExtensions.Add(names.Build());

    using var created = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
    return new X509Certificate2(created.Export(X509ContentType.Pfx), (string?)null, X509KeyStorageFlags.Exportable);
}

record LoginRequest(string? Login, string? Password);

record LoginResponse(string AccessToken, DateTime ExpiresAt);

record UserDto(string Login, string Role);
