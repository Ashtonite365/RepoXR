using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace RepoXR.Patches;

/// <summary>
/// 0.4.4 deleted a fixed 10-instruction window after "Mouse X". On 0.5.1 that window
/// runs past the end of the method and fails the whole grabber patch class.
/// Replace only the string load and the call that consumes it.
/// </summary>
internal static class MouseAxisRewrite
{
    internal static int Replace(List<CodeInstruction> instructions, MethodInfo stickX, MethodInfo stickY)
    {
        var replaced = 0;
        for (var i = 0; i < instructions.Count - 1; i++)
        {
            if (instructions[i].opcode != OpCodes.Ldstr || instructions[i].operand is not string axis)
                continue;
            if (axis != "Mouse X" && axis != "Mouse Y")
                continue;

            var call = instructions[i + 1];
            if (call.opcode != OpCodes.Call && call.opcode != OpCodes.Callvirt)
                continue;

            instructions[i].opcode = OpCodes.Call;
            instructions[i].operand = axis == "Mouse X" ? stickX : stickY;
            call.opcode = OpCodes.Nop;
            call.operand = null;
            replaced++;
        }

        return replaced;
    }
}
