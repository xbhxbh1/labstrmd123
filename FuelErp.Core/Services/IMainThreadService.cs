namespace FuelErp.Services;

/// <summary>Виконання дій у головному потоці (події сенсорів і мережі приходять з інших потоків).</summary>
public interface IMainThreadService
{
    void BeginInvokeOnMainThread(Action action);
}
