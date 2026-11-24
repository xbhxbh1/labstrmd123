using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using FuelErp.Models;
using FuelErp.Services;
using FuelErp.ViewModels;

namespace FuelErp.Tests.ViewModels;

public class UsersViewModelTests
{
    private readonly Mock<IAuthApi> _api = new();
    private readonly Mock<IAuthService> _auth = new();

    private UsersViewModel CreateSut() =>
        new(_api.Object, _auth.Object, new Mock<INavigationService>().Object, NullLogger<UsersViewModel>.Instance);

    [Fact]
    public async Task Load_WithoutToken_AsksToSignInAgain()
    {
        _auth.Setup(a => a.GetTokenAsync()).ReturnsAsync((string?)null);
        var vm = CreateSut();

        await vm.LoadCommand.ExecuteAsync(null);

        vm.Status.Should().Contain("Токен");
        _api.Verify(a => a.GetUsersAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Load_WithToken_ShowsUsersFromServer()
    {
        _auth.Setup(a => a.GetTokenAsync()).ReturnsAsync("token");
        _api.Setup(a => a.GetUsersAsync("token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserInfo> { new("admin", "Administrator"), new("teacher", "Teacher") });
        var vm = CreateSut();

        await vm.LoadCommand.ExecuteAsync(null);

        vm.Users.Should().HaveCount(2);
        vm.Status.Should().Contain("2");
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "403")]
    [InlineData(HttpStatusCode.Unauthorized, "401")]
    public async Task Load_ServerRefusal_IsExplainedToTheUser(HttpStatusCode code, string expectedFragment)
    {
        _auth.Setup(a => a.GetTokenAsync()).ReturnsAsync("token");
        _api.Setup(a => a.GetUsersAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("refused", null, code));
        var vm = CreateSut();

        await vm.LoadCommand.ExecuteAsync(null);

        vm.Status.Should().Contain(expectedFragment);
        vm.Users.Should().BeEmpty();
    }
}

public class LogsViewModelTests
{
    private readonly Mock<ILogFileReader> _reader = new();
    private readonly Mock<ICrashReporter> _crash = new();
    private readonly Mock<IDialogService> _dialogs = new();

    private LogsViewModel CreateSut() =>
        new(_reader.Object, _crash.Object, _dialogs.Object, NullLogger<LogsViewModel>.Instance);

    [Fact]
    public async Task Refresh_ShowsNewestFirstAndUsesSelectedLevelAndCount()
    {
        _reader.Setup(r => r.ReadLastAsync(20, "Warning")).ReturnsAsync(new List<LogEntry>
        {
            new() { Level = "Warning", Message = "old" },
            new() { Level = "Error", Message = "new" }
        });
        var vm = CreateSut();
        vm.SelectedCount = 20;

        vm.SelectedLevel = "Warning";
        await vm.RefreshCommand.ExecuteAsync(null);

        vm.Entries.Select(e => e.Message).Should().Equal("new", "old");
        vm.Status.Should().Contain("Warning");
    }

    [Fact]
    public async Task Refresh_WhenReaderFails_ShowsStatusInsteadOfCrashing()
    {
        _reader.Setup(r => r.ReadLastAsync(It.IsAny<int>(), It.IsAny<string>())).ThrowsAsync(new IOException());
        var vm = CreateSut();

        await vm.RefreshCommand.ExecuteAsync(null);

        vm.Status.Should().Contain("Не вдалося");
    }

    [Fact]
    public async Task SendTestReport_WithoutDsn_ExplainsThatServiceIsDisabled()
    {
        _crash.Setup(c => c.SendTestReport()).Returns((string?)null);
        var vm = CreateSut();

        await vm.SendTestReportCommand.ExecuteAsync(null);

        vm.Status.Should().Contain("вимкнено");
        _crash.Verify(c => c.FlushAsync(), Times.Never);
    }

    [Fact]
    public async Task SendTestReport_WithDsn_FlushesAndShowsEventId()
    {
        _crash.Setup(c => c.SendTestReport()).Returns("abc123");
        var vm = CreateSut();

        await vm.SendTestReportCommand.ExecuteAsync(null);

        _crash.Verify(c => c.FlushAsync(), Times.Once);
        vm.Status.Should().Contain("abc123");
    }

    [Fact]
    public void SaveDsn_PassesTheValueToTheReporter()
    {
        var vm = CreateSut();
        vm.Dsn = "https://key@example.ingest.sentry.io/1";

        vm.SaveDsnCommand.Execute(null);

        _crash.Verify(c => c.SaveDsn("https://key@example.ingest.sentry.io/1"), Times.Once);
    }

    [Fact]
    public async Task SimulateCrash_WhenUserDeclines_DoesNothing()
    {
        _dialogs.Setup(d => d.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);
        var vm = CreateSut();

        await vm.SimulateCrashCommand.ExecuteAsync(null);

        _dialogs.Verify(d => d.ConfirmAsync("Штучний краш", It.IsAny<string>(), "Так", "Ні"), Times.Once);
    }
}
