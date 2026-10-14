using System.Text;

namespace Mirraawwrring.Core.Adb;

/// <summary>
/// Escape argumen untuk shell Android (mksh/toybox). <c>adb shell a b c</c> menggabungkan argumen dengan spasi
/// lalu mengirimnya ke shell di HP, jadi quoting harus gaya POSIX, bukan gaya Windows (§5.8).
/// </summary>
public static class ShellQuoting
{
    private const string SafeChars = "@%+=:,./-_";

    /// <summary>Bungkus dengan tanda kutip tunggal kalau perlu. <c>'</c> di dalam menjadi <c>'\''</c>.</summary>
    public static string Quote(string argument)
    {
        if (argument.Length > 0 && argument.All(c => char.IsAsciiLetterOrDigit(c) || SafeChars.Contains(c)))
        {
            return argument;
        }

        var sb = new StringBuilder(argument.Length + 2);
        sb.Append('\'');
        foreach (var c in argument)
        {
            if (c == '\'')
            {
                sb.Append("'\\''");
            }
            else
            {
                sb.Append(c);
            }
        }

        sb.Append('\'');
        return sb.ToString();
    }

    /// <summary>Gabungkan argumen yang sudah di-escape menjadi satu baris perintah shell.</summary>
    public static string Join(IEnumerable<string> arguments) => string.Join(' ', arguments.Select(Quote));
}
