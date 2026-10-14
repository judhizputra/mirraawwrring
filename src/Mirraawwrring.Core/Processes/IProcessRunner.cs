namespace Mirraawwrring.Core.Processes;

/// <summary>
/// Satu-satunya pintu untuk menjalankan proses eksternal (prinsip §4.1).
/// Di test diganti fake; semua perintah dan output-nya tercatat di log (prinsip §4.5).
/// </summary>
public interface IProcessRunner
{
    /// <summary>Jalankan sampai selesai, dengan timeout dan pembatalan. Tidak melempar exception untuk exit code bukan nol.</summary>
    Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default);

    /// <summary>Mulai proses yang berjalan lama (scrcpy). Output diteruskan baris per baris ke <paramref name="onOutput"/>.</summary>
    IRunningProcess Start(ProcessRequest request, Action<ProcessOutputLine>? onOutput = null);
}

/// <summary>Proses yang masih berjalan; dibuat oleh <see cref="IProcessRunner.Start"/>.</summary>
public interface IRunningProcess : IDisposable
{
    int ProcessId { get; }

    bool HasExited { get; }

    /// <summary>Selesai ketika proses keluar; hasilnya exit code.</summary>
    Task<int> WaitForExitAsync(CancellationToken cancellationToken = default);

    /// <summary>Hentikan proses beserta anak-anaknya.</summary>
    void Kill();
}
