namespace StudentCabinetMVVM.Services;

public class MauiNetworkStatusService : INetworkStatusService
{
    private readonly IConnectivity _connectivity;

    public MauiNetworkStatusService(IConnectivity connectivity)
    {
        _connectivity = connectivity;
        _connectivity.ConnectivityChanged += (_, e) =>
            OnlineChanged?.Invoke(this, e.NetworkAccess == NetworkAccess.Internet);
    }

    public bool IsOnline => _connectivity.NetworkAccess == NetworkAccess.Internet;

    public event EventHandler<bool>? OnlineChanged;
}
