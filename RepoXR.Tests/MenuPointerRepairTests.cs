using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using RepoXR.Patches;
using Xunit;

namespace RepoXR.Tests;

public class MenuPointerRepairTests
{
    [Fact]
    public void Tag_replacement_substitutes_each_marker_without_adding_an_underline()
    {
        var text = InputDisplayTags.Replace("Press [jump] and [grab]", new Dictionary<string, string>
        {
            ["[jump]"] = "<sprite=jump>",
            ["[grab]"] = "<sprite=grab>"
        });

        Assert.Equal("Press <sprite=jump> and <sprite=grab>", text);
        Assert.DoesNotContain("_", text);
    }

    [Fact]
    public void Mouse_axis_reads_are_replaced_without_deleting_a_fixed_window()
    {
        var stickX = typeof(MenuPointerRepairTests).GetMethod(nameof(Axis))!;
        var stickY = typeof(MenuPointerRepairTests).GetMethod(nameof(Axis))!;
        var call = typeof(MenuPointerRepairTests).GetMethod(nameof(ReadAxis));
        var instructions = new List<CodeInstruction>
        {
            new(OpCodes.Ldstr, "Mouse X"),
            new(OpCodes.Call, call),
            new(OpCodes.Ldstr, "keep"),
            new(OpCodes.Ldstr, "Mouse Y"),
            new(OpCodes.Call, call)
        };

        var replaced = MouseAxisRewrite.Replace(instructions, stickX, stickY);

        Assert.Equal(2, replaced);
        Assert.Equal(OpCodes.Call, instructions[0].opcode);
        Assert.Equal(stickX, instructions[0].operand);
        Assert.Equal(OpCodes.Nop, instructions[1].opcode);
        Assert.Equal("keep", instructions[2].operand);
        Assert.Equal(stickY, instructions[3].operand);
        Assert.Equal(5, instructions.Count);
    }

    [Fact]
    public void A_short_mouse_axis_span_does_not_throw()
    {
        var instructions = new List<CodeInstruction> { new(OpCodes.Ldstr, "Mouse X"), new(OpCodes.Ret) };

        var axis = typeof(MenuPointerRepairTests).GetMethod(nameof(Axis))!;
        var replaced = MouseAxisRewrite.Replace(instructions, axis, axis);

        Assert.Equal(0, replaced);
        Assert.Equal(OpCodes.Ldstr, instructions[0].opcode);
    }

    [Fact]
    public void EditOrOriginal_keeps_the_method_when_the_edit_throws_argument_exception()
    {
        var original = new[] { new CodeInstruction(OpCodes.Ret) };

        var result = GameSignatures.EditOrOriginal(original, "test edit", _ =>
            throw new System.ArgumentException("offset"));

        Assert.Single(result);
        Assert.Equal(OpCodes.Ret, result.First().opcode);
    }

    [Fact]
    public void Camera_override_stays_false_when_the_menu_camera_has_no_player()
    {
        Assert.False(CameraOverride.IsActive(false, hasAvatar: false, avatarIsLocal: false, hasPositionDriver: false,
            positionOverrideActive: false));
        Assert.True(CameraOverride.IsActive(false, hasAvatar: true, avatarIsLocal: true, hasPositionDriver: true,
            positionOverrideActive: true));
        Assert.True(CameraOverride.IsActive(true, hasAvatar: false, avatarIsLocal: false, hasPositionDriver: false,
            positionOverrideActive: false));
    }

    public static float Axis() => 1f;

    public static float ReadAxis(string name) => name.Length;
}
