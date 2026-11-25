using FuelErp.Models;

namespace FuelErp.Data;

public interface IVehicleRepository
{
    Task<List<Vehicle>> GetAllAsync();

    Task<Vehicle?> GetByIdAsync(int id);

    /// <exception cref="DuplicateVehicleException">Транспортний засіб із таким номером уже є.</exception>
    Task<int> SaveAsync(Vehicle vehicle);
}
