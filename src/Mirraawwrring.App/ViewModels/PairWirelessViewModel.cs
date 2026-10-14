using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Mirraawwrring.App.Resources;
using Mirraawwrring.Core.Wireless;

namespace Mirraawwrring.App.ViewModels;

/// <summary>Dialog Pair wireless (mockup Pair): pairing → tunggu koneksi otomatis 5 detik → kalau perlu, port koneksi manual.</summary>
public sealed partial class PairWirelessViewModel(WirelessService wireless, ILogger<PairWirelessViewModel> logger) : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrimaryCommand))]
    public partial string PairingAddress { get; set; } = string.Empty;

    /// <summary>6 digit; diisi oleh kotak digit di View.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrimaryCommand))]
    public partial string Code { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrimaryCommand))]
    public partial string ConnectPort { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrimaryCommand))]
    public partial bool IsBusy { get; set; }

    /// <summary>True setelah pairing sukses tetapi belum tersambung: langkah 2 aktif.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PrimaryText))]
    [NotifyPropertyChangedFor(nameof(IsStep1Enabled))]
    [NotifyCanExecuteChangedFor(nameof(PrimaryCommand))]
    public partial bool NeedsConnectPort { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool StatusIsError { get; set; }

    public bool IsStep1Enabled => !NeedsConnectPort;

    public string PrimaryText => NeedsConnectPort ? Strings.Get("Pair_Connect") : Strings.Get("Pair_Primary");

    public string? ConnectedSerial { get; private set; }

    /// <summary>Dipanggil saat HP tersambung; View menutup dialog.</summary>
    public event Action? Completed;

    private bool CanPrimary()
    {
        if (IsBusy)
        {
            return false;
        }

        return NeedsConnectPort
            ? ConnectPort.Trim().Length > 0
            : AddressUtil.TryParseHostPort(PairingAddress, out _, out _) && AddressUtil.NormalizePairingCode(Code) is not null;
    }

    [RelayCommand(CanExecute = nameof(CanPrimary))]
    private async Task PrimaryAsync()
    {
        IsBusy = true;
        StatusIsError = false;
        try
        {
            if (NeedsConnectPort)
            {
                await ConnectManuallyAsync();
            }
            else
            {
                await PairAsync();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Pair wireless gagal");
            SetStatus(ex.Message, error: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task PairAsync()
    {
        SetStatus(Strings.Get("Pair_StatusPairing"), error: false);
        var pairTask = wireless.PairAsync(PairingAddress, Code);
        var outcome = await pairTask;
        if (!outcome.Paired)
        {
            SetStatus(outcome.Message + Environment.NewLine + Strings.Get("Pair_StatusFailedHint"), error: true);
            return;
        }

        if (outcome.ConnectedSerial is not null)
        {
            Finish(outcome.ConnectedSerial);
            return;
        }

        NeedsConnectPort = true;
        SetStatus(Strings.Get("Pair_StatusNeedPort"), error: false);
    }

    private async Task ConnectManuallyAsync()
    {
        var portText = ConnectPort.Trim();
        var address = portText.Contains(':') ? portText : $"{AddressUtil.HostOf(PairingAddress.Trim())}:{portText}";
        if (!AddressUtil.TryParseHostPort(address, out _, out _))
        {
            SetStatus(Strings.Get("Pair_InvalidPort"), error: true);
            return;
        }

        SetStatus(Strings.Get("Pair_StatusConnecting"), error: false);
        var result = await wireless.ConnectAsync(address);
        if (result.Success)
        {
            Finish(result.Address ?? address);
        }
        else
        {
            SetStatus(result.Message, error: true);
        }
    }

    private void Finish(string serial)
    {
        ConnectedSerial = serial;
        SetStatus(Strings.Format("Pair_StatusConnected", serial), error: false);
        Completed?.Invoke();
    }

    private void SetStatus(string text, bool error)
    {
        StatusText = text;
        StatusIsError = error;
    }
}
