using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Mirraawwrring.App.Resources;
using Mirraawwrring.Core.Wireless;
using QRCoder;

namespace Mirraawwrring.App.ViewModels;

/// <summary>
/// Dialog Pair wireless (mockup Pair): pairing dengan kode 6 digit → tunggu koneksi otomatis 5 detik → kalau perlu, port koneksi manual.
/// Mode QR (Fase 8): tampilkan QR, tunggu HP mengumumkan layanan pairing lewat mDNS, lalu pair dengan password dari QR.
/// </summary>
public sealed partial class PairWirelessViewModel(WirelessService wireless, ILogger<PairWirelessViewModel> logger) : ObservableObject
{
    public static readonly TimeSpan QrTimeout = TimeSpan.FromMinutes(5);

    private CancellationTokenSource? _qrCts;

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

    // ---------- Mode QR ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QrModeText))]
    [NotifyPropertyChangedFor(nameof(IsCodeMode))]
    [NotifyCanExecuteChangedFor(nameof(PrimaryCommand))]
    public partial bool IsQrMode { get; set; }

    public bool IsCodeMode => !IsQrMode;

    public string QrModeText => IsQrMode ? Strings.Get("Pair_CodeMode") : Strings.Get("Pair_Qr");

    [ObservableProperty]
    public partial ImageSource? QrImage { get; set; }

    [ObservableProperty]
    public partial string QrStatusText { get; set; } = string.Empty;

    /// <summary>Nama layanan sesi QR aktif (untuk log dan uji).</summary>
    public string? QrServiceName { get; private set; }

    [RelayCommand]
    private void ToggleQrMode() => IsQrMode = !IsQrMode;

    partial void OnIsQrModeChanged(bool value)
    {
        if (value)
        {
            StartQr();
        }
        else
        {
            StopQr();
        }
    }

    private void StartQr()
    {
        StopQr();
        var session = QrPairing.Create();
        QrServiceName = session.ServiceName;
        QrImage = RenderQr(session.QrContent);
        QrStatusText = Strings.Get("Pair_QrWaiting");
        SetStatus(string.Empty, error: false);
        var cts = _qrCts = new CancellationTokenSource();
        logger.LogInformation("QR pairing: menunggu layanan {Name}", session.ServiceName);
        _ = RunQrAsync(session, cts.Token);
    }

    private void StopQr()
    {
        _qrCts?.Cancel();
        _qrCts = null;
        QrServiceName = null;
    }

    /// <summary>Dipanggil View saat dialog ditutup supaya polling mDNS berhenti.</summary>
    public void Cancel() => StopQr();

    private async Task RunQrAsync(QrPairingSession session, CancellationToken ct)
    {
        try
        {
            var service = await wireless.WaitForPairingServiceAsync(session.ServiceName, QrTimeout, ct);
            if (ct.IsCancellationRequested)
            {
                return;
            }

            if (service is null)
            {
                QrStatusText = Strings.Get("Pair_QrTimeout");
                return;
            }

            QrStatusText = Strings.Format("Pair_QrFound", service.Address);
            IsBusy = true;
            PairingAddress = service.Address;
            var outcome = await wireless.PairWithPasswordAsync(service.Address, session.Password, ct);
            if (!outcome.Paired)
            {
                QrStatusText = Strings.Get("Pair_QrTimeout");
                SetStatus(outcome.Message + Environment.NewLine + Strings.Get("Pair_StatusFailedHint"), error: true);
                return;
            }

            if (outcome.ConnectedSerial is not null)
            {
                Finish(outcome.ConnectedSerial);
                return;
            }

            NeedsConnectPort = true;
            QrStatusText = string.Empty;
            SetStatus(Strings.Get("Pair_StatusNeedPort"), error: false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "QR pairing gagal");
            SetStatus(ex.Message, error: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static BitmapImage RenderQr(string content)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
        using var png = new PngByteQRCode(data);
        var bytes = png.GetGraphic(8);
        var image = new BitmapImage();
        using var stream = new MemoryStream(bytes);
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    // ---------- Mode kode 6 digit ----------

    private bool CanPrimary()
    {
        if (IsBusy)
        {
            return false;
        }

        if (NeedsConnectPort)
        {
            return ConnectPort.Trim().Length > 0;
        }

        return !IsQrMode && AddressUtil.TryParseHostPort(PairingAddress, out _, out _) && AddressUtil.NormalizePairingCode(Code) is not null;
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
        var outcome = await wireless.PairAsync(PairingAddress, Code);
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
        StopQr();
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
