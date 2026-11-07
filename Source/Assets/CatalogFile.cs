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
        var name = Stem(path);
        var loaded = Stem(loadedName);
        if (string.Equals(name, loaded, StringComparison.OrdinalIgnoreCase))
            return true;

        // Addressables keeps one group name and writes a different _<32 hex> on each build.
        // defaultlocalgroup_assets_all_<hashA> and defaultlocalgroup_assets_all_<hashB> are the same files.
        var nameGroup = GroupOf(name);
        var loadedGroup = GroupOf(loaded);
        return nameGroup.Length > 0
               && string.Equals(nameGroup, loadedGroup, StringComparison.OrdinalIgnoreCase)
               && (HasHash(name) || HasHash(loaded));
    }

    static string Stem(string value)
    {
        var name = value ?? "";
        var slash = Math.Max(name.LastIndexOf('/'), name.LastIndexOf('\\'));
        if (slash >= 0)
            name = name[(slash + 1)..];

        const string extension = ".bundle";
        if (name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            name = name[..^extension.Length];

        return name;
    }

    internal static string GroupOf(string name)
    {
        if (string.IsNullOrEmpty(name) || !HasHash(name))
            return name ?? "";

        return name[..^33];
    }

    static bool HasHash(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Length < 33 || name[^33] != '_')
            return false;

        foreach (var character in name[^32..])
        {
            if (!Uri.IsHexDigit(character))
                return false;
        }

        return true;
    }
}
