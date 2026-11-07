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

    [Fact]
    public void A_json_catalog_is_not_a_binary_catalog()
    {
        var json = System.Text.Encoding.UTF8.GetBytes("\n{ \"m_LocatorId\": \"catalog\" }");
        var binary = new byte[] { 0x00, 0x01, 0x02, 0x03 };

        Assert.True(RepoXR.Assets.CatalogFile.IsJson(json));
        Assert.False(RepoXR.Assets.CatalogFile.IsJson(binary));
        Assert.True(RepoXR.Assets.CatalogFile.SameLoadedBundle(
            @"C:\RepoXR\Addressables\defaultlocalgroup_assets_all_644635e9bfc6a9f81b1adcaf94351997.bundle",
            "defaultlocalgroup_assets_all_644635e9bfc6a9f81b1adcaf94351997"));
        Assert.True(RepoXR.Assets.CatalogFile.SameLoadedBundle(
            @"C:\RepoXR\Addressables\defaultlocalgroup_assets_all_644635e9bfc6a9f81b1adcaf94351997.bundle",
            "defaultlocalgroup_assets_all"));
        Assert.False(RepoXR.Assets.CatalogFile.SameLoadedBundle(
            @"C:\RepoXR\Addressables\localization-assets-shared_644635e9bfc6a9f81b1adcaf94351997.bundle",
            "defaultlocalgroup_assets_all"));
        Assert.True(RepoXR.Assets.CatalogFile.SameLoadedBundle(
            @"C:\RepoXR\Addressables\defaultlocalgroup_assets_all_644635e9bfc6a9f81b1adcaf94351997.bundle",
            @"C:\RepoXR\Addressables\defaultlocalgroup_assets_all_3495734bfc6a9f81b1adcaf943519970.bundle"));
        Assert.False(RepoXR.Assets.CatalogFile.SameLoadedBundle(
            @"C:\RepoXR\Addressables\defaultlocalgroup_assets_all_644635e9bfc6a9f81b1adcaf94351997.bundle",
            "repoxrassets"));
    }

    [Fact]
    public void The_menu_camera_stays_put_when_its_parent_and_active_state_match()
    {
        Assert.True(RepoXR.UI.MenuView.CameraStayed("Rig:shown", "Rig:shown", true, true));
        Assert.False(RepoXR.UI.MenuView.CameraStayed(
            "Rig:shown", "Camera Floor Offset:shown / XR Origin:shown / Rig:shown", true, true));
        Assert.False(RepoXR.UI.MenuView.CameraStayed("Rig:shown", "Rig:shown", true, false));
        Assert.True(RepoXR.UI.MenuView.CenterInView(0.5f, 0.4f, 2f));
        Assert.False(RepoXR.UI.MenuView.CenterInView(0.5f, 0.4f, -1f));
        Assert.False(RepoXR.UI.MenuView.CenterInView(1.4f, 0.4f, 2f));
    }

    [Fact]
    public void The_main_menu_waits_for_its_page_and_does_not_claim_the_hud()
    {
        Assert.False(RepoXR.UI.MenuTarget.PageReady(false));
        Assert.False(RepoXR.UI.MenuTarget.Accept(false, "HUD Canvas", true));
        Assert.False(RepoXR.UI.MenuTarget.Accept(true, "HUD Canvas", false));
        Assert.True(RepoXR.UI.MenuTarget.Accept(true, "HUD Canvas", true));
        Assert.True(RepoXR.UI.MenuTarget.Accept(true, "Canvas", true));
        Assert.True(RepoXR.UI.MenuTarget.IsHudCanvas("HUD Canvas"));
        Assert.False(RepoXR.UI.MenuTarget.IsHudCanvas("Canvas"));
        Assert.Equal(1.6f / 1920f, RepoXR.UI.MenuTarget.ScaleForWidth(1920f, 1.6f), 5);
        var shifted = RepoXR.UI.MenuTarget.LocalPositionForContent(400f, 300f, 0.002f, 1.4f);
        Assert.Equal(-0.8f, shifted.x, 4);
        Assert.Equal(-0.6f, shifted.y, 4);
        Assert.Equal(1.4f, shifted.z, 4);
        Assert.Equal(1.6f / 2000f, RepoXR.UI.MenuTarget.ScaleToFit(2000f, 800f, 1.6f, 0.9f), 5);
        Assert.True(RepoXR.UI.MenuTarget.RayPlane(0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 1.4f, 0f, 0f, -1f, 30f, out var distance));
        Assert.Equal(1.4f, distance, 3);
        Assert.False(RepoXR.UI.MenuTarget.RayPlane(0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 1.4f, 0f, 0f, 1f, 30f, out _));
        Assert.False(RepoXR.UI.MenuTarget.CoversDesktopMirror(isPresentedMenu: true, screenOverlay: true));
        Assert.True(RepoXR.UI.MenuTarget.CoversDesktopMirror(isPresentedMenu: false, screenOverlay: true));
        var uv = RepoXR.UI.MenuTarget.UvFromLocal(0f, 0f, 1.6f, 0.9f);
        Assert.Equal(0.5f, uv.u, 3);
        Assert.Equal(0.5f, uv.v, 3);
        var local = RepoXR.UI.MenuTarget.LocalFromUv(0f, 0f, 0f, 0f, 712f, 400f);
        Assert.Equal(0f, local.x, 3);
        Assert.Equal(0f, local.y, 3);
        var mid = RepoXR.UI.MenuTarget.LocalFromUv(0.5f, 0.5f, 0f, 0f, 712f, 400f);
        Assert.Equal(356f, mid.x, 3);
        Assert.Equal(200f, mid.y, 3);
        var left = RepoXR.UI.MenuTarget.CameraViewLeft(1f, 0f);
        Assert.Equal(-1f, left.x, 3);
        Assert.Equal(0f, left.z, 3);
        var spot = RepoXR.UI.MenuTarget.LeftOfTruck(2f, 5f, left.x, left.z, 4f, 1.8f);
        Assert.Equal(-2f, spot.x, 3);
        Assert.Equal(1.8f, spot.y, 3);
        Assert.Equal(5f, spot.z, 3);
        Assert.True(RepoXR.UI.MenuTarget.IsLeftOfTruck(4f));
        Assert.False(RepoXR.UI.MenuTarget.IsLeftOfTruck(0f));
        var behind = RepoXR.UI.MenuTarget.Ahead(0f, 2f, 0f, 1f, -20f, -8f);
        Assert.True(behind < 0f);
        Assert.False(RepoXR.UI.MenuTarget.InFrontOfCamera(behind, 40f));
        var ahead = RepoXR.UI.MenuTarget.Ahead(0f, 2f, 0f, 1f, 1f, 8f);
        Assert.True(RepoXR.UI.MenuTarget.InFrontOfCamera(ahead, 40f));
    }

    public static float Axis() => 1f;

    public static float ReadAxis(string name) => name.Length;
}
