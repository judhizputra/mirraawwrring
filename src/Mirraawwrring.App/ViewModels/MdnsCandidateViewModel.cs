using CommunityToolkit.Mvvm.ComponentModel;
using Mirraawwrring.App.Resources;
using Mirraawwrring.Core.Adb;

namespace Mirraawwrring.App.ViewModels;

/// <summary>HP yang terlihat lewat mDNS (Wireless debugging aktif) tetapi belum tersambung ke adb.</summary>
public sealed partial class MdnsCandidateViewModel(MdnsService service) : ObservableObject
{
    public MdnsService Service { get; } = service;

    public string Name => Service.Name;

    public string Address => Service.Address;

    public string Title => Strings.Get("Mdns_Title");

    public string Subtitle => Strings.Format("Mdns_Subtitle", Address);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ButtonText))]
    public partial bool IsConnecting { get; set; }

    public string ButtonText => IsConnecting ? Strings.Get("Mdns_Connecting") : Strings.Get("Mdns_Connect");
}
