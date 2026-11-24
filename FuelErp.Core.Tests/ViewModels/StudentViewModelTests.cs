using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using FuelErp.Data;
using FuelErp.Models;
using FuelErp.Services;
using FuelErp.Tests.TestSupport;
using FuelErp.ViewModels;

namespace FuelErp.Tests.ViewModels;

public class StudentViewModelTests
{
    private readonly Mock<IAppStateService> _state = new();
    private readonly Mock<IStudentRepository> _repo = new();
    private readonly Mock<ISyncService> _sync = new();
    private readonly Mock<ISessionService> _session = new();
    private readonly Mock<IAuthService> _auth = new();
    private readonly Mock<INavigationService> _nav = new();
    private readonly Mock<IDialogService> _dialogs = new();
    private readonly Mock<IMainThreadService> _mainThread = TestHelpers.SynchronousMainThread();
    private readonly Mock<ILogger<StudentViewModel>> _logger = new();

    public StudentViewModelTests()
    {
        _state.SetupProperty(s => s.DraftFullName, string.Empty);
        _state.SetupProperty(s => s.DraftGroup, string.Empty);
        _state.SetupProperty(s => s.DraftAverageScore, 0.0);
        _state.SetupProperty(s => s.PendingDeepLink, null);

        _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Student>());
        _repo.Setup(r => r.SaveAsync(It.IsAny<Student>())).ReturnsAsync(1);
        _repo.Setup(r => r.DeleteAsync(It.IsAny<Student>())).ReturnsAsync(1);

        _sync.Setup(s => s.GetPendingCountAsync()).ReturnsAsync(0);
        _sync.Setup(s => s.SyncAsync()).ReturnsAsync(0);
    }

    private StudentViewModel CreateSut() =>
        new(_state.Object, _repo.Object, _sync.Object, _session.Object, _auth.Object,
            _nav.Object, _dialogs.Object, _mainThread.Object, _logger.Object);

    private void SignInAs(UserRole? role)
    {
        _session.Setup(s => s.CurrentRole).Returns(role);
        _session.Setup(s => s.UserName).Returns(role is null ? null : "user");
        _session.Setup(s => s.IsInRole(It.IsAny<UserRole>())).Returns((UserRole r) => r == role);
    }

    [Fact]
    public async Task Load_FillsStudentsFromRepository()
    {
        _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Student>
        {
            new() { Id = 1, FullName = "Іваненко Іван" },
            new() { Id = 2, FullName = "Петренко Петро" }
        });
        var vm = CreateSut();

        await vm.LoadCommand.ExecuteAsync(null);

        vm.Students.Should().HaveCount(2);
        vm.Students.Select(s => s.FullName).Should().Contain("Петренко Петро");
    }

    [Fact]
    public async Task Load_CalledTwice_ReadsRepositoryOnlyOnce()
    {
        var vm = CreateSut();

        await vm.LoadCommand.ExecuteAsync(null);
        await vm.LoadCommand.ExecuteAsync(null);

        _repo.Verify(r => r.GetAllAsync(), Times.Once);
    }

    [Fact]
    public async Task Load_WhenRepositoryThrows_LogsErrorAndShowsAlert()
    {
        _repo.Setup(r => r.GetAllAsync()).ThrowsAsync(new InvalidOperationException("db is broken"));
        var vm = CreateSut();

        await vm.LoadCommand.ExecuteAsync(null);

        _logger.VerifyLogged(LogLevel.Error, Times.Once());
        _dialogs.Verify(d => d.AlertAsync("Помилка", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        vm.Students.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("Іваненко Іван", true)]
    public void AddStudentCommand_CanExecute_DependsOnFullName(string fullName, bool expected)
    {
        var vm = CreateSut();

        vm.FullName = fullName;

        vm.AddStudentCommand.CanExecute(null).Should().Be(expected);
    }

    [Fact]
    public async Task AddStudent_SavesToRepository_AddsToListAndClearsForm()
    {
        var vm = CreateSut();
        vm.FullName = "Іваненко Іван";
        vm.Group = "КН-31";
        vm.AverageScore = 4.5;

        await vm.AddStudentCommand.ExecuteAsync(null);

        _repo.Verify(r => r.SaveAsync(It.Is<Student>(s =>
            s.FullName == "Іваненко Іван" && s.Group == "КН-31" && s.AverageScore == 4.5)), Times.Once);
        vm.Students.Should().ContainSingle();
        vm.FullName.Should().BeEmpty();
        vm.Group.Should().BeEmpty();
    }

    [Fact]
    public async Task AddStudent_WithoutGroup_LogsWarning()
    {
        var vm = CreateSut();
        vm.FullName = "Іваненко Іван";

        await vm.AddStudentCommand.ExecuteAsync(null);

        _logger.VerifyLogged(LogLevel.Warning, Times.Once());
    }

    [Fact]
    public async Task AddStudent_WhenRepositoryThrows_LogsErrorAndKeepsFormData()
    {
        _repo.Setup(r => r.SaveAsync(It.IsAny<Student>())).ThrowsAsync(new InvalidOperationException());
        var vm = CreateSut();
        vm.FullName = "Іваненко Іван";
        vm.Group = "КН-31";

        await vm.AddStudentCommand.ExecuteAsync(null);

        _logger.VerifyLogged(LogLevel.Error, Times.Once());
        vm.Students.Should().BeEmpty();
        vm.FullName.Should().Be("Іваненко Іван");
    }

    [Fact]
    public async Task Delete_AsTeacher_IsDeniedAndRepositoryNotCalled()
    {
        SignInAs(UserRole.StationOperator);
        var vm = CreateSut();

        await vm.DeleteStudentCommand.ExecuteAsync(new Student { Id = 1 });

        _repo.Verify(r => r.DeleteAsync(It.IsAny<Student>()), Times.Never);
        _dialogs.Verify(d => d.AlertAsync("Доступ заборонено", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _logger.VerifyLogged(LogLevel.Warning, Times.Once());
    }

    [Fact]
    public async Task Delete_AsAdministrator_RemovesStudentFromRepositoryAndList()
    {
        SignInAs(UserRole.Accountant);
        var student = new Student { Id = 7, FullName = "Іваненко Іван" };
        _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Student> { student });
        var vm = CreateSut();
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.DeleteStudentCommand.ExecuteAsync(student);

        _repo.Verify(r => r.DeleteAsync(student), Times.Once);
        vm.Students.Should().BeEmpty();
        _dialogs.Verify(d => d.AlertAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task OpenDetails_NavigatesToDetailPageWithSelectedStudent()
    {
        var student = new Student { Id = 3, FullName = "Іваненко Іван" };
        IDictionary<string, object>? passed = null;
        _nav.Setup(n => n.GoToAsync("studentdetail", It.IsAny<IDictionary<string, object>>()))
            .Callback<string, IDictionary<string, object>?>((_, p) => passed = p)
            .Returns(Task.CompletedTask);
        var vm = CreateSut();

        await vm.OpenDetailsCommand.ExecuteAsync(student);

        passed.Should().NotBeNull();
        passed!["SelectedStudent"].Should().BeSameAs(student);
    }

    [Theory]
    [InlineData(UserRole.Accountant, true)]
    [InlineData(UserRole.StationOperator, false)]
    public void IsAdminPanelVisible_DependsOnRole(UserRole role, bool expected)
    {
        SignInAs(role);
        var vm = CreateSut();

        vm.IsAdminPanelVisible.Should().Be(expected);
    }

    [Fact]
    public async Task OpenUsers_AsTeacher_IsDeniedWithoutNavigation()
    {
        SignInAs(UserRole.StationOperator);
        var vm = CreateSut();

        await vm.OpenUsersCommand.ExecuteAsync(null);

        _nav.Verify(n => n.GoToAsync("users", It.IsAny<IDictionary<string, object>?>()), Times.Never);
        _dialogs.Verify(d => d.AlertAsync("Доступ заборонено", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task OpenUsers_AsAdministrator_NavigatesToUsersPage()
    {
        SignInAs(UserRole.Accountant);
        var vm = CreateSut();

        await vm.OpenUsersCommand.ExecuteAsync(null);

        _nav.Verify(n => n.GoToAsync("users", It.IsAny<IDictionary<string, object>?>()), Times.Once);
    }

    [Fact]
    public async Task Logout_SignsOutAndReturnsToLoginPage()
    {
        var vm = CreateSut();

        await vm.LogoutCommand.ExecuteAsync(null);

        _auth.Verify(a => a.Logout(), Times.Once);
        _nav.Verify(n => n.GoToAsync("//LoginPage", It.IsAny<IDictionary<string, object>?>()), Times.Once);
    }

    [Fact]
    public async Task ApplyUpdatedStudent_SavesAndReplacesListItem()
    {
        var student = new Student { Id = 5, FullName = "Іваненко Іван", AverageScore = 3.0 };
        _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Student> { student });
        var vm = CreateSut();
        await vm.LoadCommand.ExecuteAsync(null);
        student.AverageScore = 4.8;

        await vm.ApplyUpdatedStudentAsync(student);

        _repo.Verify(r => r.SaveAsync(student), Times.Once);
        vm.Students.Should().ContainSingle().Which.AverageScore.Should().Be(4.8);
    }

    [Fact]
    public async Task ApplyUpdatedStudent_SameChangeTwice_IsSavedOnce()
    {
        var student = new Student { Id = 5, AverageScore = 4.0 };
        var vm = CreateSut();

        await vm.ApplyUpdatedStudentAsync(student);
        await vm.ApplyUpdatedStudentAsync(student);

        _repo.Verify(r => r.SaveAsync(student), Times.Once);
    }

    [Fact]
    public void Draft_IsWrittenToAppState()
    {
        var vm = CreateSut();

        vm.FullName = "Чернетка";

        _state.Object.DraftFullName.Should().Be("Чернетка");
    }

    [Fact]
    public void Greeting_ContainsNameAndGroup()
    {
        var vm = CreateSut();
        vm.FullName = "Іваненко Іван";
        vm.Group = "КН-31";

        vm.Greeting.Should().Be("Студент: Іваненко Іван, група КН-31");
    }
}
