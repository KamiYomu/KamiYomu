using System.IO.Compression;

namespace KamiYomu.Web.Infrastructure.Storage;

public static class NugetHelper
{
    public static bool IsNugetPackage(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        try
        {
            using ZipArchive archive = ZipFile.OpenRead(filePath);
            return archive.Entries.Any(entry => entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }


}
