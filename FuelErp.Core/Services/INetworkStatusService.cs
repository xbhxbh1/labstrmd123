namespace FuelErp.Services;

public interface INetworkStatusService
{
    bool IsOnline { get; }

    /// <summary>Подія приходить не з потоку інтерфейсу; аргумент - чи є доступ до інтернету.</summary>
    event EventHandler<bool>? OnlineChanged;
}
