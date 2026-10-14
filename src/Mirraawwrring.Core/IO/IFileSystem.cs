namespace Mirraawwrring.Core.IO;

/// <summary>Abstraksi tipis atas file system dan environment supaya <c>ToolLocator</c> bisa diuji tanpa disk.</summary>
public interface IFileSystem
{
    bool FileExists(string path);

    bool DirectoryExists(string path);

    /// <summary>Target akhir jika <paramref name="path"/> adalah symlink/junction; null jika bukan link.</summary>
    string? ResolveLinkTarget(string path);

    string? GetEnvironmentVariable(string name);
}

public sealed class PhysicalFileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public string? ResolveLinkTarget(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (info.LinkTarget is null)
            {
                return null;
            }

            return info.ResolveLinkTarget(returnFinalTarget: true)?.FullName;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public string? GetEnvironmentVariable(string name) => Environment.GetEnvironmentVariable(name);
}
