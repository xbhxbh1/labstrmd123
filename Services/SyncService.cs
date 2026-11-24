using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Networking;
using StudentCabinetMVVM.Data;

namespace StudentCabinetMVVM.Services;

public class SyncService : ISyncService
{
    private readonly AppDatabase _database;
    private readonly ILogger<SyncService> _logger;
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private readonly HttpClient _http = new()
    {
        BaseAddress = new Uri("http://localhost:5099/"),
        Timeout = TimeSpan.FromSeconds(5)
    };

    public event EventHandler? QueueChanged;

    public SyncService(AppDatabase database, ILogger<SyncService> logger)
    {
        _database = database;
        _logger = logger;
        Connectivity.Current.ConnectivityChanged += async (_, _) => await SyncAsync();
    }

    public async Task<int> GetPendingCountAsync()
    {
        var db = await _database.GetConnectionAsync();
        return await db.Table<SyncQueueItem>().CountAsync();
    }

    public async Task<int> SyncAsync()
    {
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            _logger.LogWarning("Немає мережі: зміни залишаються в черзі синхронізації");
            return 0;
        }

        if (!await _syncLock.WaitAsync(0))
            return 0;

        var sent = 0;
        try
        {
            var db = await _database.GetConnectionAsync();
            var items = await db.Table<SyncQueueItem>().OrderBy(i => i.Id).ToListAsync();

            foreach (var item in items)
            {
                var response = await _http.PostAsJsonAsync("api/sync", item);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Сервер відхилив запис черги #{QueueId}: HTTP {Status}",
                        item.Id, (int)response.StatusCode);
                    break;
                }

                await db.DeleteAsync(item);
                sent++;
                _logger.LogInformation("Відправлено на сервер: {Operation} (запис черги #{QueueId})",
                    item.Operation, item.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Сервер синхронізації недоступний: {Reason}", ex.Message);
        }
        finally
        {
            _syncLock.Release();
            QueueChanged?.Invoke(this, EventArgs.Empty);
        }

        return sent;
    }
}
