namespace StudentCabinetMVVM.Services;

public class MauiMainThreadService : IMainThreadService
{
    public void BeginInvokeOnMainThread(Action action) => MainThread.BeginInvokeOnMainThread(action);
}
