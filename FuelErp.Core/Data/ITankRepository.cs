using FuelErp.Models;

namespace FuelErp.Data;

public interface ITankRepository
{
    Task<List<FuelTank>> GetAllAsync();

    Task<FuelTank?> GetByIdAsync(int id);

    Task<int> SaveAsync(FuelTank tank);
}
