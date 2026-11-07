using System;

namespace RepoXR.Assets;

internal static class CatalogFile
{
    internal static bool IsJson(byte[] bytes)
    {
        if (bytes == null)
            return false;

        foreach (var value in bytes)
        {
            if (value is (byte)' ' or (byte)'\r' or (byte)'\n' or (byte)'\t' or 0xEF or 0xBB or 0xBF)
                continue;

            return value == (byte)'{';
        }

        return false;
    }

    internal static bool SameLoadedBundle(string path, string loadedName)
    {
        var name = path ?? "";
        var slash = Math.Max(name.LastIndexOf('/'), name.LastIndexOf('\\'));
        if (slash >= 0)
            name = name[(slash + 1)..];

        const string extension = ".bundle";
        if (name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            name = name[..^extension.Length];

        return string.Equals(name, loadedName ?? "", StringComparison.OrdinalIgnoreCase);
    }
}
