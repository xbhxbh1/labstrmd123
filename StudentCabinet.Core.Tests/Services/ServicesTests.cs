using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StudentCabinetMVVM.Models;
using StudentCabinetMVVM.Services;
using StudentCabinetMVVM.Tests.TestSupport;

namespace StudentCabinetMVVM.Tests.Services;

public class JwtParserTests
{
    [Fact]
    public void TryParse_ValidToken_ReadsUserRoleAndExpiry()
    {
        var expires = DateTimeOffset.UtcNow.AddHours(1);
        var token = JwtFactory.Create("admin", "Administrator", expires);

        var ok = JwtParser.TryParse(token, out var claims);

        ok.Should().BeTrue();
        claims.UserName.Should().Be("admin");
        claims.Role.Should().Be(UserRole.Administrator);
        claims.ExpiresAt.ToUnixTimeSeconds().Should().Be(expires.ToUnixTimeSeconds());
    }

    [Fact]
    public void TryParse_SeveralRoles_TakesTheHighestOne()
    {
        var token = JwtFactory.Create("u", new[] { "Teacher", "Administrator" }, DateTimeOffset.UtcNow.AddHours(1));

        JwtParser.TryParse(token, out var claims).Should().BeTrue();

        claims.Role.Should().Be(UserRole.Administrator);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("abc.def.ghi")]
    public void TryParse_GarbageOrEmpty_ReturnsFalse(string? token)
    {
        JwtParser.TryParse(token, out _).Should().BeFalse();
    }

    [Fact]
    public void TryParse_UnknownRole_ReturnsFalse()
    {
        var token = JwtFactory.Create("u", "Hacker", DateTimeOffset.UtcNow.AddHours(1));

        JwtParser.TryParse(token, out _).Should().BeFalse();
    }
}

public class CertificatePinValidatorTests
{
    private static X509Certificate2 CreateCertificate(string name)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest($"CN={name}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var created = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        return new X509Certificate2(created.Export(X509ContentType.Cert));
    }

    [Fact]
    public void IsPinned_CertificateWithMatchingFingerprint_IsAccepted()
    {
        using var cert = CreateCertificate("server");
        var pin = CertificatePinValidator.ComputePin(cert);

        CertificatePinValidator.IsPinned(cert, pin).Should().BeTrue();
    }

    [Fact]
    public void IsPinned_FingerprintWithColonsAndLowercase_IsAccepted()
    {
        using var cert = CreateCertificate("server");
        var pin = CertificatePinValidator.ComputePin(cert);
        var formatted = string.Join(":", Enumerable.Range(0, pin.Length / 2).Select(i => pin.Substring(i * 2, 2))).ToLowerInvariant();

        CertificatePinValidator.IsPinned(cert, formatted).Should().BeTrue();
    }

    [Fact]
    public void IsPinned_DifferentCertificate_IsRejected()
    {
        using var real = CreateCertificate("server");
        using var attacker = CreateCertificate("attacker");

        CertificatePinValidator.IsPinned(attacker, CertificatePinValidator.ComputePin(real)).Should().BeFalse();
    }

    [Fact]
    public void IsPinned_NoCertificate_IsRejected()
    {
        CertificatePinValidator.IsPinned(null, "AA").Should().BeFalse();
    }
}

public class LogParserTests
{
    [Fact]
    public void Parse_GroupsStackLinesWithTheirRecord()
    {
        var lines = new[]
        {
            "2026-11-24 22:37:20.344 +02:00 [WRN] My.Class Something odd",
            "2026-11-24 22:37:20.863 +02:00 [ERR]  Необроблений виняток у Task",
            "System.InvalidOperationException: boom",
            "   at My.Class.Method()",
            "2026-11-24 22:38:51.869 +02:00 [INF]  Запуск"
        };

        var entries = LogParser.Parse(lines);

        entries.Should().HaveCount(3);
        entries[0].Level.Should().Be("Warning");
        entries[1].Level.Should().Be("Error");
        entries[1].HasDetails.Should().BeTrue();
        entries[1].Details.Should().Contain("boom").And.Contain("at My.Class.Method");
        entries[2].HasDetails.Should().BeFalse();
    }

    [Theory]
    [InlineData("DBG", "Debug")]
    [InlineData("INF", "Information")]
    [InlineData("WRN", "Warning")]
    [InlineData("ERR", "Error")]
    [InlineData("FTL", "Fatal")]
    public void ToLevelName_MapsSerilogAbbreviations(string abbreviation, string expected)
    {
        LogParser.ToLevelName(abbreviation).Should().Be(expected);
    }

    [Fact]
    public void Rank_OrdersLevelsFromDebugToFatal()
    {
        LogParser.Rank("Debug").Should().BeLessThan(LogParser.Rank("Warning"));
        LogParser.Rank("Warning").Should().BeLessThan(LogParser.Rank("Fatal"));
    }
}

public class LogFileReaderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"logs_{Guid.NewGuid():N}");

    public LogFileReaderTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task ReadLast_FiltersByLevelAndKeepsOnlyTheLastRecords()
    {
        File.WriteAllLines(Path.Combine(_dir, "app-20261123.log"), new[]
        {
            "2026-11-23 10:00:00.000 +02:00 [INF] A one",
            "2026-11-23 10:00:01.000 +02:00 [ERR] A two"
        });
        File.WriteAllLines(Path.Combine(_dir, "app-20261124.log"), new[]
        {
            "2026-11-24 10:00:00.000 +02:00 [WRN] B one",
            "2026-11-24 10:00:01.000 +02:00 [DBG] B two",
            "2026-11-24 10:00:02.000 +02:00 [ERR] B three"
        });
        var reader = new LogFileReader(_dir);

        var warnings = await reader.ReadLastAsync(10, "Warning");
        var lastOne = await reader.ReadLastAsync(1, "Debug");

        warnings.Select(e => e.Message).Should().Equal("A two", "B one", "B three");
        lastOne.Should().ContainSingle().Which.Message.Should().Be("B three");
    }

    [Fact]
    public async Task ReadLast_MissingDirectory_ReturnsEmptyList()
    {
        var reader = new LogFileReader(Path.Combine(_dir, "nope"));

        (await reader.ReadLastAsync(10, "Debug")).Should().BeEmpty();
    }
}

public class SessionServiceTests
{
    [Fact]
    public void SignIn_SetsRoleAndRaisesEvent()
    {
        var session = new SessionService();
        var raised = 0;
        session.SessionChanged += (_, _) => raised++;

        session.SignIn(UserRole.Teacher, "teacher");

        session.IsAuthenticated.Should().BeTrue();
        session.IsInRole(UserRole.Teacher).Should().BeTrue();
        session.IsInRole(UserRole.Administrator).Should().BeFalse();
        session.UserName.Should().Be("teacher");
        raised.Should().Be(1);
    }

    [Fact]
    public void SignOut_ClearsTheSession()
    {
        var session = new SessionService();
        session.SignIn(UserRole.Administrator, "admin");

        session.SignOut();

        session.IsAuthenticated.Should().BeFalse();
        session.CurrentRole.Should().BeNull();
        session.IsInRole(UserRole.Administrator).Should().BeFalse();
    }
}

public class AuthServiceTests
{
    private readonly Mock<IAuthApi> _api = new();
    private readonly FakeSecureStore _store = new();
    private readonly SessionService _session = new();

    private AuthService CreateSut() => new(_api.Object, _store, _session, NullLogger<AuthService>.Instance);

    [Fact]
    public async Task Login_Success_StoresTokenAndSignsIn()
    {
        var token = JwtFactory.Create("admin", "Administrator", DateTimeOffset.UtcNow.AddHours(1));
        _api.Setup(a => a.AuthenticateAsync("admin", "pw", It.IsAny<CancellationToken>())).ReturnsAsync(token);

        var result = await CreateSut().LoginAsync("admin", "pw");

        result.Success.Should().BeTrue();
        _store.Values["auth_token"].Should().Be(token);
        _session.IsInRole(UserRole.Administrator).Should().BeTrue();
    }

    [Fact]
    public async Task Login_WrongPassword_FailsWithoutStoringAnything()
    {
        _api.Setup(a => a.AuthenticateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await CreateSut().LoginAsync("admin", "bad");

        result.Success.Should().BeFalse();
        _store.Values.Should().BeEmpty();
        _session.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task Login_ServerUnavailable_ReportsUnavailableServer()
    {
        _api.Setup(a => a.AuthenticateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("no connection"));

        var result = await CreateSut().LoginAsync("admin", "pw");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("недоступний");
    }

    [Fact]
    public async Task Login_CertificateMismatch_ReportsPossibleMitm()
    {
        _api.Setup(a => a.AuthenticateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException(HttpRequestError.SecureConnectionError, "tls"));

        var result = await CreateSut().LoginAsync("admin", "pw");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("сертифікат");
    }

    [Fact]
    public async Task LoginWithStoredToken_ValidToken_SignsInWithoutPassword()
    {
        _store.Values["auth_token"] = JwtFactory.Create("teacher", "Teacher", DateTimeOffset.UtcNow.AddMinutes(30));

        var result = await CreateSut().LoginWithStoredTokenAsync();

        result.Success.Should().BeTrue();
        _session.IsInRole(UserRole.Teacher).Should().BeTrue();
    }

    [Fact]
    public async Task LoginWithStoredToken_ExpiredToken_IsRemovedAndRefused()
    {
        _store.Values["auth_token"] = JwtFactory.Create("teacher", "Teacher", DateTimeOffset.UtcNow.AddMinutes(-5));

        var result = await CreateSut().LoginWithStoredTokenAsync();

        result.Success.Should().BeFalse();
        _store.Values.Should().NotContainKey("auth_token");
        _session.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task Logout_RemovesTokenAndClearsSession()
    {
        _store.Values["auth_token"] = JwtFactory.Create("admin", "Administrator", DateTimeOffset.UtcNow.AddHours(1));
        var sut = CreateSut();
        await sut.LoginWithStoredTokenAsync();

        sut.Logout();

        _store.Values.Should().NotContainKey("auth_token");
        _session.IsAuthenticated.Should().BeFalse();
        (await sut.HasStoredTokenAsync()).Should().BeFalse();
    }
}

public class AesEncryptionServiceTests
{
    [Fact]
    public async Task EncryptThenDecrypt_ReturnsOriginalText()
    {
        var service = new AesEncryptionService(new FakeSecureStore());

        var cipher = await service.EncryptAsync("Іваненко Іван Іванович");
        var plain = await service.DecryptAsync(cipher);

        cipher.Should().NotContain("Іваненко");
        plain.Should().Be("Іваненко Іван Іванович");
    }

    [Fact]
    public async Task Encrypt_SameTextTwice_GivesDifferentCipherBecauseOfRandomIv()
    {
        var service = new AesEncryptionService(new FakeSecureStore());

        var first = await service.EncryptAsync("текст");
        var second = await service.EncryptAsync("текст");

        first.Should().NotBe(second);
    }

    [Fact]
    public async Task Key_IsGeneratedOnceAndReusedFromTheSecureStore()
    {
        var store = new FakeSecureStore();
        var cipher = await new AesEncryptionService(store).EncryptAsync("секрет");

        var plain = await new AesEncryptionService(store).DecryptAsync(cipher);

        store.Values.Should().ContainKey("db_encryption_key");
        plain.Should().Be("секрет");
    }

    [Fact]
    public async Task Decrypt_EmptyText_ReturnsEmptyString()
    {
        var service = new AesEncryptionService(new FakeSecureStore());

        (await service.DecryptAsync(string.Empty)).Should().BeEmpty();
    }
}

public class AppStateServiceTests
{
    [Fact]
    public void SaveThenRestore_BringsBackDraftAndStudents()
    {
        var settings = new FakeSettings();
        var first = new AppStateService(settings)
        {
            DraftFullName = "Чернетка",
            DraftGroup = "КН-31",
            DraftAverageScore = 4.2,
            Students = new List<Student> { new() { Id = 1, FullName = "Іваненко" } }
        };
        first.Save();

        var second = new AppStateService(settings);
        var restored = 0;
        second.StateRestored += (_, _) => restored++;
        second.Restore();

        second.DraftFullName.Should().Be("Чернетка");
        second.DraftGroup.Should().Be("КН-31");
        second.DraftAverageScore.Should().Be(4.2);
        second.Students.Should().ContainSingle().Which.FullName.Should().Be("Іваненко");
        restored.Should().Be(1);
    }

    [Fact]
    public void PendingDeepLink_RaisesEventOnlyWhenSet()
    {
        var state = new AppStateService(new FakeSettings());
        var raised = 0;
        state.DeepLinkReceived += (_, _) => raised++;

        state.PendingDeepLink = "studentcabinet://student/Test";
        state.PendingDeepLink = null;

        raised.Should().Be(1);
    }
}
