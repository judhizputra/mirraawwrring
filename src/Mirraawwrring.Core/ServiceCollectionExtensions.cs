using Microsoft.Extensions.DependencyInjection;

namespace Mirraawwrring.Core;

/// <summary>Pendaftaran semua layanan Core ke DI. Dipanggil dari App.xaml.cs.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMirraawwrringCore(this IServiceCollection services)
    {
        // Layanan Core ditambahkan per fase (lihat CLAUDE.md).
        return services;
    }
}
