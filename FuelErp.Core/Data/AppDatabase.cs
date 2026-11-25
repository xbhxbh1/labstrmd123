using SQLite;

namespace FuelErp.Data;

public class AppDatabase
{
    /// <summary>
    /// Поточна версія схеми (PRAGMA user_version).
    /// v1 - початкові таблиці; v2 - у таблицю Refuelings додано стовпець Notes (демонстраційна міграція).
    /// </summary>
    public const int SchemaVersion = 2;

    private readonly string _path;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private SQLiteAsyncConnection? _connection;

    public AppDatabase(string path)
    {
        _path = path;
    }

    public async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        if (_connection is not null)
            return _connection;

        await _lock.WaitAsync();
        try
        {
            if (_connection is null)
            {
                var connection = new SQLiteAsyncConnection(_path);
                await PrepareSchemaAsync(connection);
                _connection = connection;
            }

            return _connection;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<int> GetSchemaVersionAsync()
    {
        var db = await GetConnectionAsync();
        return await db.ExecuteScalarAsync<int>("PRAGMA user_version");
    }

    /// <summary>Закриває з'єднання (потрібно інтеграційним тестам перед видаленням тимчасового файла).</summary>
    public async Task CloseAsync()
    {
        if (_connection is null)
            return;

        await _connection.CloseAsync();
        _connection = null;
    }

    private static async Task PrepareSchemaAsync(SQLiteAsyncConnection connection)
    {
        var version = await connection.ExecuteScalarAsync<int>("PRAGMA user_version");

        // База старішої версії спочатку мігрується, потім створюються відсутні таблиці
        if (version > 0 && version < SchemaVersion)
            await MigrateAsync(connection, version);

        await CreateTablesAsync(connection);

        if (version != SchemaVersion)
            await connection.ExecuteAsync($"PRAGMA user_version = {SchemaVersion}");
    }

    private static async Task CreateTablesAsync(SQLiteAsyncConnection connection)
    {
        await connection.CreateTableAsync<VehicleRecord>();
        await connection.CreateTableAsync<FuelTankRecord>();
        await connection.CreateTableAsync<RefuelingRecord>();
        await connection.CreateTableAsync<SyncQueueItem>();
        await CreateLegacyTablesAsync(connection);
    }

    private static async Task MigrateAsync(SQLiteAsyncConnection connection, int fromVersion)
    {
        if (fromVersion < 2)
        {
            // v1 -> v2: у заправку додано примітки; наявні записи отримують порожній текст
            var columns = await connection.GetTableInfoAsync("Refuelings");
            var hasNotes = columns.Any(c => string.Equals(c.Name, "Notes", StringComparison.OrdinalIgnoreCase));
            if (!hasNotes)
                await connection.ExecuteAsync("ALTER TABLE Refuelings ADD COLUMN Notes TEXT NOT NULL DEFAULT ''");
        }
    }

    // Таблиці попередніх лабораторних робіт існують лише до завершення переходу на модуль обліку палива
    private static async Task CreateLegacyTablesAsync(SQLiteAsyncConnection connection)
    {
        await connection.CreateTableAsync<StudentRecord>();
        await connection.CreateTableAsync<RoomInspectionRecord>();
    }
}
