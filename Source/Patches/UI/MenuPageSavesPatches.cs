using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RepoXR.UI.Menu;

namespace RepoXR.Patches.UI;

/// <summary>
/// Close the VR keyboard when a save rename finishes. 0.4.4 used MenuPageSavesRename.
/// 0.5.1 folded that page into MenuPageSaves, so leaving the page closes the keyboard.
/// </summary>
[RepoXRPatch]
internal static class MenuPageSavesPatches
{
    [HarmonyPrepare]
    static bool Prepare()
    {
        foreach (var _ in TargetMethods())
            return true;

        return false;
    }

    [HarmonyTargetMethods]
    static IEnumerable<MethodBase> TargetMethods()
    {
        var rename = GameType("MenuPageSavesRename");
        if (rename != null)
        {
            foreach (var name in new[] { "ButtonConfirm", "ExitPage" })
            {
                var method = Method(rename, name);
                if (method != null)
                    yield return method;
            }
        }

        var leave = Method(typeof(MenuPageSaves), "OnGoBack");
        if (leave != null)
            yield return leave;
    }

    internal static Type GameType(string name)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type;
            try
            {
                type = assembly.GetType(name, false);
            }
            catch (ReflectionTypeLoadException)
            {
                continue;
            }

            if (type != null)
                return type;
        }

        return null;
    }

    static MethodInfo Method(Type type, string name) =>
        type?.GetMethod(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

    [HarmonyPostfix]
    static void CloseKeyboard()
    {
        if (InputKeyboard.instance is { } instance)
            instance.Close();
    }
}
