using StudentCabinetMVVM.Models;

namespace StudentCabinetMVVM.Data;

public interface IInspectionRepository
{
    Task<List<RoomInspection>> GetAllAsync();

    Task<RoomInspection?> GetByIdAsync(int id);

    Task<int> SaveAsync(RoomInspection inspection);
}
