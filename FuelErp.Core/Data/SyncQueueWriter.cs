using System.Text.Json;

namespace FuelErp.Data;

/// <summary>Формує записи черги офлайн-змін (лабораторна робота 4); сервер вирішує конфлікти за UpdatedAt.</summary>
internal static class SyncQueueWriter
{
    public static SyncQueueItem Create(string entityType, string operation, object payload) => new()
    {
        EntityType = entityType,
        Operation = operation,
        PayloadJson = JsonSerializer.Serialize(payload),
        CreatedAt = DateTime.UtcNow
    };
}
