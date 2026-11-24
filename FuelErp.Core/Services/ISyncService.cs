namespace FuelErp.Services;

public interface ISyncService
{
    event EventHandler? QueueChanged;

    Task<int> GetPendingCountAsync();
    Task<int> SyncAsync();
}