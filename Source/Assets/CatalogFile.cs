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

        if (string.Equals(name, loadedName ?? "", StringComparison.OrdinalIgnoreCase))
            return true;

        // Addressables writes the group name into the bundle, then appends _<32 hex> on disk.
        // A second file for that same group must be reused, not loaded again.
        if (string.IsNullOrEmpty(loadedName) ||
            !name.StartsWith(loadedName + "_", StringComparison.OrdinalIgnoreCase))
            return false;

        var suffix = name[(loadedName.Length + 1)..];
        if (suffix.Length != 32)
            return false;

        foreach (var character in suffix)
        {
            if (!Uri.IsHexDigit(character))
                return false;
        }

        return true;
    }
}
