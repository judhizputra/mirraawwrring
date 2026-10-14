using System.Text.RegularExpressions;

namespace Mirraawwrring.Core.Adb;

/// <summary>
/// Parser murni untuk output <c>adb devices -l</c>:
/// <code>
/// List of devices attached
/// 192.168.1.20:41235     device product:example model:2407FPN8EG device:example transport_id:3
/// R58N12ABCDE            unauthorized usb:1-1 transport_id:2
/// </code>
/// Baris <c>* daemon …</c> dan header diabaikan.
/// </summary>
public static partial class AdbDevicesParser
{
    [GeneratedRegex(@"^(?<serial>\S+)\s+(?<state>no permissions|\S+)(?<rest>.*)$")]
    private static partial Regex LineRegex();

    [GeneratedRegex(@"(?<key>\w+):(?<value>\S+)")]
    private static partial Regex PairRegex();

    public static IReadOnlyList<Device> Parse(string output)
    {
        var devices = new List<Device>();
        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r').Trim();
            if (line.Length == 0 || line.StartsWith('*') || line.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var match = LineRegex().Match(line);
            if (!match.Success)
            {
                continue;
            }

            var serial = match.Groups["serial"].Value;
            var rawState = match.Groups["state"].Value;
            var state = ParseState(rawState);
            if (state == DeviceState.Unknown && !IsPlausibleState(rawState))
            {
                // Bukan baris device (mis. pesan error adb).
                continue;
            }

            string? model = null, product = null, codename = null, usb = null;
            int? transportId = null;
            foreach (Match pair in PairRegex().Matches(match.Groups["rest"].Value))
            {
                var value = pair.Groups["value"].Value;
                switch (pair.Groups["key"].Value)
                {
                    case "model":
                        model = value.Replace('_', ' ');
                        break;
                    case "product":
                        product = value;
                        break;
                    case "device":
                        codename = value;
                        break;
                    case "usb":
                        usb = value;
                        break;
                    case "transport_id":
                        transportId = int.TryParse(value, out var id) ? id : null;
                        break;
                }
            }

            devices.Add(new Device
            {
                Serial = serial,
                State = state,
                RawState = rawState,
                Transport = Device.TransportFromSerial(serial),
                Model = model,
                Product = product,
                DeviceCodename = codename,
                UsbPath = usb,
                TransportId = transportId,
            });
        }

        return devices;
    }

    public static DeviceState ParseState(string raw) => raw switch
    {
        "device" => DeviceState.Device,
        "unauthorized" => DeviceState.Unauthorized,
        "offline" => DeviceState.Offline,
        "no permissions" => DeviceState.NoPermissions,
        "recovery" => DeviceState.Recovery,
        "sideload" => DeviceState.Sideload,
        _ => DeviceState.Unknown,
    };

    /// <summary>State lain yang memang dikeluarkan adb tetapi tidak kita bedakan.</summary>
    private static bool IsPlausibleState(string raw) => raw is "connecting" or "authorizing" or "bootloader" or "host" or "rescue" or "detached";
}
