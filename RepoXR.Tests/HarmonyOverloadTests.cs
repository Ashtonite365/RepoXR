using System.Reflection;
using HarmonyLib;
using Mono.Cecil;
using Xunit;

namespace RepoXR.Tests;

public class HarmonyOverloadTests
{
    class ShakeHost
    {
        public void Shake(float strengthAdd, float time)
        {
        }

        public void Shake(float strengthAdd, float time, float rumbleMultiplier)
        {
        }
    }

    [Fact]
    public void Unqualified_Shake_is_ambiguous_and_explicit_overloads_are_not()
    {
        Assert.Throws<AmbiguousMatchException>(() => AccessTools.DeclaredMethod(typeof(ShakeHost), "Shake"));

        var two = AccessTools.DeclaredMethod(typeof(ShakeHost), "Shake", [typeof(float), typeof(float)]);
        var three = AccessTools.DeclaredMethod(typeof(ShakeHost), "Shake",
            [typeof(float), typeof(float), typeof(float)]);

        Assert.Equal(2, two.GetParameters().Length);
        Assert.Equal(3, three.GetParameters().Length);
    }

    [Fact]
    public void Camera_shake_menu_patches_name_both_0_5_overloads()
    {
        var assembly = AssemblyDefinition.ReadAssembly(typeof(OpenXrPlayerFix).Assembly.Location);
        var type = assembly.MainModule.Types.Single(candidate => candidate.Name == "CameraEffectsPatches");
        var arities = type.Methods
            .SelectMany(method => method.CustomAttributes)
            .Where(attribute => attribute.AttributeType.Name == "HarmonyPatch")
            .Select(ArgumentTypes)
            .Where(types => types.Count > 0)
            .ToList();

        Assert.Contains(arities, types => types.SequenceEqual(["Single", "Single"]));
        Assert.Contains(arities, types => types.SequenceEqual(["Single", "Single", "Single"]));
    }

    static List<string> ArgumentTypes(CustomAttribute attribute)
    {
        var names = new List<string>();
        foreach (var argument in attribute.ConstructorArguments)
        {
            if (argument.Value is TypeReference type)
                names.Add(type.Name);
            else if (argument.Value is CustomAttributeArgument[] array)
                names.AddRange(array.Select(item => ((TypeReference)item.Value).Name));
        }

        return names.Where(name => name is "Single" or "Int32" or "String").ToList();
    }
}
