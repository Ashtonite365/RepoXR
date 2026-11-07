using System;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;
using CecilMethodAttributes = Mono.Cecil.MethodAttributes;
using CecilOpCodes = Mono.Cecil.Cil.OpCodes;
using CecilTypeAttributes = Mono.Cecil.TypeAttributes;

namespace RepoXR.Tests;

public class OpenXrPlayerFixTests
{
    [Fact]
    public void Neutralized_loader_finishes_when_AnalyticsResult_is_missing()
    {
        var dir = Path.Combine(Path.GetTempPath(), "repoxr-oxr-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        try
        {
            WriteAnalyticsModule(Path.Combine(dir, "UnityEngine.UnityAnalyticsModule.dll"));
            var fakePath = Path.Combine(dir, "Unity.XR.OpenXR.dll");
            WriteOpenXrStub(fakePath);

            var first = Assert.Single(OpenXrPlayerFix.Apply(new[] { fakePath }));
            Assert.Null(first.Error);
            Assert.True(first.MethodsNeutralized >= 2);

            var second = Assert.Single(OpenXrPlayerFix.Apply(new[] { fakePath }));
            Assert.Null(second.Error);
            Assert.Equal(0, second.MethodsNeutralized);

            using var rewritten = AssemblyDefinition.ReadAssembly(fakePath);
            var send = rewritten.MainModule.Types
                .Single(type => type.Name == "OpenXRAnalytics")
                .Methods.Single(method => method.Name == "SendInitializeEvent");
            var only = Assert.Single(send.Body.Instructions);
            Assert.Equal(OpCodes.Ret, only.OpCode);

            var context = new System.Runtime.Loader.AssemblyLoadContext(
                "repoxr-oxr-" + Guid.NewGuid().ToString("N"), isCollectible: true);
            context.Resolving += (_, name) =>
            {
                var candidate = Path.Combine(dir, name.Name + ".dll");
                return File.Exists(candidate) ? context.LoadFromAssemblyPath(candidate) : null;
            };

            var assembly = context.LoadFromAssemblyPath(fakePath);
            var loader = assembly.GetType("UnityEngine.XR.OpenXR.OpenXRLoaderBase", throwOnError: true)!;
            var initialize = loader.GetMethod("InitializeInternal")!;
            var sendMethod = assembly.GetType("UnityEngine.XR.OpenXR.OpenXRAnalytics", throwOnError: true)!
                .GetMethod("SendInitializeEvent")!;

            sendMethod.Invoke(null, new object?[] { true });
            var instance = Activator.CreateInstance(loader);
            Assert.True((bool)initialize.Invoke(instance, null)!);
            context.Unload();
        }
        finally
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A collectible load context can keep the temp DLL mapped until the
                // finalizer runs. The production patcher reads the assembly in memory
                // and does not load it into an AssemblyLoadContext.
            }
        }
    }

    static void WriteAnalyticsModule(string path)
    {
        var assembly = AssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition("UnityEngine.UnityAnalyticsModule", new System.Version(0, 0, 0, 0)),
            "UnityEngine.UnityAnalyticsModule",
            ModuleKind.Dll);
        assembly.MainModule.Types.Add(new TypeDefinition(
            "UnityEngine.Analytics",
            "Analytics",
            CecilTypeAttributes.Public | CecilTypeAttributes.Abstract | CecilTypeAttributes.Sealed,
            assembly.MainModule.TypeSystem.Object));
        assembly.Write(path);
    }

    static void WriteOpenXrStub(string path)
    {
        var assembly = AssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition("Unity.XR.OpenXR", new System.Version(0, 0, 0, 0)),
            "Unity.XR.OpenXR",
            ModuleKind.Dll);
        var module = assembly.MainModule;
        var analyticsAssembly =
            new AssemblyNameReference("UnityEngine.UnityAnalyticsModule", new System.Version(0, 0, 0, 0));
        module.AssemblyReferences.Add(analyticsAssembly);
        var analyticsResult = new TypeReference("UnityEngine.Analytics", "AnalyticsResult", module, analyticsAssembly);
        var analyticsType = new TypeReference("UnityEngine.Analytics", "Analytics", module, analyticsAssembly);
        var register = new MethodReference("RegisterEvent", analyticsResult, analyticsType);

        var analytics = new TypeDefinition(
            "UnityEngine.XR.OpenXR",
            "OpenXRAnalytics",
            CecilTypeAttributes.NotPublic | CecilTypeAttributes.Abstract | CecilTypeAttributes.Sealed,
            module.TypeSystem.Object);
        var initializeAnalytics = new MethodDefinition(
            "Initialize",
            CecilMethodAttributes.Private | CecilMethodAttributes.Static,
            module.TypeSystem.Boolean);
        var initializeIl = initializeAnalytics.Body.GetILProcessor();
        initializeIl.Emit(CecilOpCodes.Call, register);
        initializeIl.Emit(CecilOpCodes.Pop);
        initializeIl.Emit(CecilOpCodes.Ldc_I4_1);
        initializeIl.Emit(CecilOpCodes.Ret);
        analytics.Methods.Add(initializeAnalytics);

        var send = new MethodDefinition(
            "SendInitializeEvent",
            CecilMethodAttributes.Public | CecilMethodAttributes.Static,
            module.TypeSystem.Void);
        send.Parameters.Add(new ParameterDefinition(module.TypeSystem.Boolean));
        var sendIl = send.Body.GetILProcessor();
        sendIl.Emit(CecilOpCodes.Call, initializeAnalytics);
        sendIl.Emit(CecilOpCodes.Pop);
        sendIl.Emit(CecilOpCodes.Ret);
        analytics.Methods.Add(send);

        var loader = new TypeDefinition(
            "UnityEngine.XR.OpenXR",
            "OpenXRLoaderBase",
            CecilTypeAttributes.Public,
            module.TypeSystem.Object);
        var ctor = new MethodDefinition(
            ".ctor",
            CecilMethodAttributes.Public | CecilMethodAttributes.SpecialName | CecilMethodAttributes.RTSpecialName,
            module.TypeSystem.Void);
        var ctorIl = ctor.Body.GetILProcessor();
        ctorIl.Emit(CecilOpCodes.Ldarg_0);
        ctorIl.Emit(CecilOpCodes.Call, new MethodReference(".ctor", module.TypeSystem.Void, module.TypeSystem.Object)
        {
            HasThis = true
        });
        ctorIl.Emit(CecilOpCodes.Ret);
        loader.Methods.Add(ctor);

        var initialize = new MethodDefinition(
            "InitializeInternal",
            CecilMethodAttributes.Public,
            module.TypeSystem.Boolean);
        var body = initialize.Body.GetILProcessor();
        body.Emit(CecilOpCodes.Ldc_I4_1);
        body.Emit(CecilOpCodes.Call, send);
        body.Emit(CecilOpCodes.Ldc_I4_1);
        body.Emit(CecilOpCodes.Ret);
        loader.Methods.Add(initialize);

        module.Types.Add(analytics);
        module.Types.Add(loader);
        assembly.Write(path);
    }
}
