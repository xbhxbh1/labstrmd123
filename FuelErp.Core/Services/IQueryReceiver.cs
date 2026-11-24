namespace FuelErp.Services;

/// <summary>
/// ViewModel, що приймає параметри навігації. Shell передає їх у IQueryAttributable сторінки,
/// а та пересилає сюди; так ядро не залежить від MAUI.
/// </summary>
public interface IQueryReceiver
{
    void ApplyQuery(IDictionary<string, object> query);
}
