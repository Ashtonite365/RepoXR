using System.Reflection.Emit;
using HarmonyLib;
using Xunit;

namespace RepoXR.Tests;

public class ClimbInsertTests
{
    [Fact]
    public void Angle_insert_puts_the_hand_call_immediately_before_angle()
    {
        var angle = typeof(ClimbInsertHost).GetMethod(nameof(ClimbInsertHost.Angle))!;
        var replace = typeof(ClimbInsertHost).GetMethod(nameof(ClimbInsertHost.Replace))!;
        var matcher = new CodeMatcher(new[]
        {
            new CodeInstruction(OpCodes.Ldarg_1),
            new CodeInstruction(OpCodes.Ldarg_2),
            new CodeInstruction(OpCodes.Call, angle),
            new CodeInstruction(OpCodes.Ret)
        });

        matcher.MatchForward(false, new CodeMatch(OpCodes.Call, angle));
        Assert.False(matcher.IsInvalid);

        matcher.Insert(
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Call, replace));

        var opcodes = matcher.InstructionEnumeration().Select(ins => ins.opcode).ToArray();
        Assert.Equal(
            [OpCodes.Ldarg_1, OpCodes.Ldarg_2, OpCodes.Ldarg_0, OpCodes.Call, OpCodes.Call, OpCodes.Ret],
            opcodes);

        var calls = matcher.InstructionEnumeration().Select(ins => ins.operand).OfType<System.Reflection.MethodInfo>()
            .Select(method => method.Name).ToArray();
        Assert.Equal([nameof(ClimbInsertHost.Replace), nameof(ClimbInsertHost.Angle)], calls);
    }
}

public class ClimbInsertHost
{
    public static int Angle(int left, int right) => left + right;

    public static int Replace(int top, ClimbInsertHost self)
    {
        _ = self;
        _ = top;
        return 100;
    }
}
