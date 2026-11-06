using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using Mono.Cecil.Cil;

[assembly: InternalsVisibleTo("RepoXR.Tests")]

namespace RepoXR;

/// <summary>
/// The OpenXR 1.10 player assembly shipped in RuntimeDeps calls Unity Analytics at the end of
/// a successful session start. Unity 6 removed <c>UnityEngine.Analytics.AnalyticsResult</c>,
/// so the call throws <see cref="TypeLoadException"/>, the loader tears the new session down,
/// and SteamVR stays in theater mode. The analytics methods are emptied before that assembly
/// is loaded. Harmony cannot do this: opening the broken method body takes the process down.
/// </summary>
static class OpenXrPlayerFix
{
    const string AnalyticsType = "UnityEngine.XR.OpenXR.OpenXRAnalytics";

    internal readonly struct Result
    {
        public string Path { get; }
        public int MethodsNeutralized { get; }
        public string? Error { get; }

        public Result(string path, int methodsNeutralized, string? error)
        {
            Path = path;
            MethodsNeutralized = methodsNeutralized;
            Error = error;
        }
    }

    internal static List<Result> Apply(IEnumerable<string> dllPaths)
    {
        var results = new List<Result>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in dllPaths)
        {
            if (string.IsNullOrWhiteSpace(path))
                continue;

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch (Exception ex)
            {
                results.Add(new Result(path, 0, ex.Message));
                continue;
            }

            if (!seen.Add(fullPath) || !File.Exists(fullPath))
                continue;

            results.Add(ApplyFile(fullPath));
        }

        return results;
    }

    static Result ApplyFile(string path)
    {
        var temp = path + ".tmp";

        try
        {
            using var assembly = AssemblyDefinition.ReadAssembly(path, new ReaderParameters { InMemory = true });
            var neutralized = 0;

            foreach (var type in assembly.MainModule.GetTypes())
            {
                if (type.FullName != AnalyticsType)
                    continue;

                foreach (var method in type.Methods)
                {
                    if (!method.HasBody || !ReferencesAnalytics(method))
                        continue;

                    Neutralize(method);
                    neutralized++;
                }
            }

            if (neutralized == 0)
                return new Result(path, 0, null);

            // Not a .dll name: PreloadRuntimeDependencies loads every *.dll in this folder.
            var backup = path + ".analytics-backup";
            if (!File.Exists(backup))
                File.Copy(path, backup);

            assembly.Write(temp);
            File.Replace(temp, path, null);
            return new Result(path, neutralized, null);
        }
        catch (Exception ex)
        {
            TryDelete(temp);
            return new Result(path, 0, ex.GetType().Name + ": " + ex.Message);
        }
    }

    static bool ReferencesAnalytics(MethodDefinition method)
    {
        foreach (var instruction in method.Body.Instructions)
        {
            switch (instruction.Operand)
            {
                case MethodReference called when called.DeclaringType.FullName == AnalyticsType:
                case MemberReference member when member.FullName.Contains("UnityEngine.Analytics", StringComparison.Ordinal):
                case TypeReference type when type.FullName.Contains("UnityEngine.Analytics", StringComparison.Ordinal):
                    return true;
            }
        }

        return false;
    }

    static void Neutralize(MethodDefinition method)
    {
        method.Body.ExceptionHandlers.Clear();
        method.Body.Variables.Clear();
        method.Body.Instructions.Clear();

        var il = method.Body.GetILProcessor();
        if (method.ReturnType.FullName == "System.Void")
        {
            il.Emit(OpCodes.Ret);
            return;
        }

        if (method.ReturnType.FullName is "System.Boolean" or "System.Int32")
        {
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ret);
            return;
        }

        var local = new VariableDefinition(method.ReturnType);
        method.Body.Variables.Add(local);
        method.Body.InitLocals = true;
        il.Emit(OpCodes.Ldloca, local);
        il.Emit(OpCodes.Initobj, method.ReturnType);
        il.Emit(OpCodes.Ldloc, local);
        il.Emit(OpCodes.Ret);
    }

    static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // The original assembly is still intact.
        }
    }
}
