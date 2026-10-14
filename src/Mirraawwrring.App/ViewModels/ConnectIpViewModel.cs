using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Mirraawwrring.App.Resources;
using Mirraawwrring.Core.Wireless;

namespace Mirraawwrring.App.ViewModels;

/// <summary>Dialog "Sambung via IP": input IP:port + riwayat koneksi (§6.4).</summary>
public sealed partial class ConnectIpViewModel : ObservableObject
{
    private readonly WirelessService _wireless;
    private readonly ILogger<ConnectIpViewModel> _logger;

    public ConnectIpViewModel(WirelessService wireless, ILogger<ConnectIpViewModel> logger)
    {
        _wireless = wireless;
        _logger = logger;
        History = new ObservableCollection<string>(wireless.ConnectionHistory);
        Address = History.FirstOrDefault() ?? string.Empty;
    }

    public ObservableCollection<string> History { get; }

    public bool HasHistory => History.Count > 0;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    public partial string Address { get; set; }

    [ObservableProperty]
    public partial string? SelectedHistory { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool StatusIsError { get; set; }

    public string? ConnectedAddress { get; private set; }

    public event Action? Completed;

    partial void OnSelectedHistoryChanged(string? value)
    {
        if (value is not null)
        {
            Address = value;
        }
    }

    private bool CanConnect() => !IsBusy && AddressUtil.NormalizeConnectAddress(Address) is not null;

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        IsBusy = true;
        StatusIsError = false;
        StatusText = Strings.Get("ConnectIp_StatusConnecting");
        try
        {
            var result = await _wireless.ConnectAsync(Address);
            if (result.Success)
            {
                ConnectedAddress = result.Address ?? AddressUtil.NormalizeConnectAddress(Address);
                StatusText = Strings.Format("ConnectIp_StatusConnected", ConnectedAddress);
                Completed?.Invoke();
            }
            else
            {
                StatusIsError = true;
                StatusText = result.Message;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Sambung via IP gagal");
            StatusIsError = true;
            StatusText = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Forget(string? address)
    {
        if (address is null)
        {
            return;
        }

        _wireless.ForgetAddress(address);
        History.Remove(address);
        OnPropertyChanged(nameof(HasHistory));
    }
}
