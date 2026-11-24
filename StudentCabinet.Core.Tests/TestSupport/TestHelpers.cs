using Microsoft.Extensions.Logging;
using Moq;
using StudentCabinetMVVM.Services;

namespace StudentCabinetMVVM.Tests.TestSupport;

public static class TestHelpers
{
    /// <summary>Перевіряє, що в mock-логер записано повідомлення вказаного рівня задану кількість разів.</summary>
    public static void VerifyLogged<T>(this Mock<ILogger<T>> logger, LogLevel level, Times times) =>
        logger.Verify(l => l.Log(
            level,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), times);

    /// <summary>Головний потік у тестах: дія виконується відразу.</summary>
    public static Mock<IMainThreadService> SynchronousMainThread()
    {
        var mock = new Mock<IMainThreadService>();
        mock.Setup(m => m.BeginInvokeOnMainThread(It.IsAny<Action>())).Callback<Action>(action => action());
        return mock;
    }
}
