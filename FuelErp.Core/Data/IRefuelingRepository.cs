using FuelErp.Models;

namespace FuelErp.Data;

public interface IRefuelingRepository
{
    /// <summary>Усі заправки, найновіші першими, із номером транспортного засобу та назвою резервуара.</summary>
    Task<List<Refueling>> GetAllAsync();

    Task<Refueling?> GetByIdAsync(int id);

    /// <summary>
    /// Додає або змінює заправку й у тій самій транзакції змінює залишок у резервуарі.
    /// </summary>
    /// <exception cref="InsufficientFuelException">У резервуарі недостатньо палива.</exception>
    Task<int> SaveAsync(Refueling refueling);

    /// <summary>Видаляє заправку й повертає пальне в резервуар.</summary>
    Task<int> DeleteAsync(Refueling refueling);
}
