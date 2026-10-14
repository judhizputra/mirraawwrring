namespace Mirraawwrring.Core.Usb;

/// <summary>
/// Tabel USB Vendor ID → merek HP (§5.3). Tambahkan baris baru di <see cref="Vendors"/>; VID ditulis 4 digit heksadesimal huruf besar.
/// Sumber: USB-IF vendor list.
/// </summary>
public static class UsbVendorTable
{
    public static readonly IReadOnlyDictionary<string, string> Vendors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["2717"] = "Xiaomi",
        ["04E8"] = "Samsung",
        ["18D1"] = "Google",
        ["22D9"] = "OPPO/realme/OnePlus",
        ["2D95"] = "vivo",
        ["12D1"] = "Huawei",
        ["22B8"] = "Motorola",
        ["0B05"] = "ASUS",
        ["054C"] = "Sony",
        ["1004"] = "LG",
        ["2E04"] = "Nokia (HMD)",
        ["17EF"] = "Lenovo",
        ["0FCE"] = "Sony Ericsson",
        ["1BBB"] = "TCL/Alcatel",
        ["2A45"] = "Meizu",
        ["0E8D"] = "MediaTek",
        ["1F3A"] = "Allwinner",
        ["19D2"] = "ZTE",
        ["2A70"] = "OnePlus",
        ["29A9"] = "Xiaomi/Redmi",
    };

    /// <summary>Nama merek untuk VID, atau null jika tidak dikenal.</summary>
    public static string? BrandFor(string? vendorId) =>
        vendorId is not null && Vendors.TryGetValue(vendorId, out var brand) ? brand : null;

    public static bool IsKnownPhoneVendor(string? vendorId) => BrandFor(vendorId) is not null;
}
