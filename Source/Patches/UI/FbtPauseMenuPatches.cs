using RepoXR.Player;
using RepoXR.Player.Body;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RepoXR.Patches.UI;

/// <summary>
/// Adds full-body controls to the game's escape menu, which RepoXR already
/// rehosts as the in-game VR pause screen.
/// </summary>
[RepoXRPatch]
internal static class FbtPauseMenuPatches
{
    const string ToggleName = "RepoXR FBT Toggle";
    const string CalibrateName = "RepoXR FBT Calibrate";

    [HarmonyLib.HarmonyPatch(typeof(MenuPageEsc), nameof(MenuPageEsc.Start))]
    [HarmonyLib.HarmonyPostfix]
    private static void AddBodyTrackingButtons(MenuPageEsc __instance)
    {
        if (__instance.transform.Find(ToggleName))
            return;

        var root = __instance.mainMenuRectTransform ? __instance.mainMenuRectTransform : __instance.transform;
        var buttons = root.GetComponentsInChildren<MenuButton>(true);
        if (buttons == null || buttons.Length == 0)
            return;

        MenuButton template = null!;
        var lowestY = float.PositiveInfinity;
        RectTransform lowest = null!;

        foreach (var button in buttons)
        {
            if (!button || button.name is ToggleName or CalibrateName)
                continue;

            template ??= button;
            var rect = button.rectTransform ? button.rectTransform : button.GetComponent<RectTransform>();
            if (!rect)
                continue;

            if (rect.anchoredPosition.y < lowestY)
            {
                lowestY = rect.anchoredPosition.y;
                lowest = rect;
                template = button;
            }
        }

        if (!template || !lowest)
        {
            Logger.LogWarning("Could not add full-body buttons to the pause menu (no MenuButton was found to copy).");
            return;
        }

        var height = Mathf.Max(28f, lowest.sizeDelta.y);
        var parentHasLayout = lowest.parent && lowest.parent.GetComponent<LayoutGroup>();
        var toggle = CloneButton(template, ToggleName, "Body Tracking: Off");
        var calibrate = CloneButton(template, CalibrateName, "Calibrate Body");

        if (!parentHasLayout)
        {
            toggle.rectTransform.anchoredPosition = lowest.anchoredPosition + new Vector2(0f, -height);
            calibrate.rectTransform.anchoredPosition = lowest.anchoredPosition + new Vector2(0f, -height * 2f);
        }

        Wire(toggle, OnToggle);
        Wire(calibrate, OnCalibrate);

        var driver = __instance.gameObject.AddComponent<FbtPauseButtonDriver>();
        driver.Toggle = toggle;
        driver.Calibrate = calibrate;
        driver.LockPositions = !parentHasLayout;
        driver.TogglePosition = toggle.rectTransform.anchoredPosition;
        driver.CalibratePosition = calibrate.rectTransform.anchoredPosition;
    }

    static void OnToggle()
    {
        var enabled = !Plugin.Config.EnableFullBodyTracking.Value;
        Plugin.Config.EnableFullBodyTracking.Value = enabled;
        Logger.LogInfo(!enabled
            ? "Full body tracking off. Tracker poses will not apply. Headset and controllers stay bound."
            : ViveTrackerFeature.Injected
                ? "Full body tracking on. Tracker poses will apply. Headset and controllers stay bound."
                : "Full body tracking on. The tracker profile was not registered this launch, so poses start after the next launch. Headset and controllers stay bound.");
    }

    static void OnCalibrate()
    {
        FullBodyTracking.BeginCalibrationPreview();

        if (MenuPageEsc.instance)
            MenuPageEsc.instance.ButtonEventContinue();
    }

    static MenuButton CloneButton(MenuButton template, string objectName, string label)
    {
        var clone = Object.Instantiate(template, template.transform.parent);
        clone.name = objectName;
        clone.gameObject.SetActive(true);
        SetLabel(clone, label);
        return clone;
    }

    internal static void SetLabel(MenuButton button, string label)
    {
        if (!button)
            return;

        button.buttonTextString = label;
        button.originalText = label;
        if (button.buttonText)
            button.buttonText.text = label;
    }

    static void Wire(MenuButton menuButton, UnityEngine.Events.UnityAction action)
    {
        var button = menuButton.button ? menuButton.button : menuButton.GetComponent<Button>();
        if (!button)
            return;

        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(action);
    }
}

internal class FbtPauseButtonDriver : MonoBehaviour
{
    public MenuButton Toggle;
    public MenuButton Calibrate;
    public bool LockPositions;
    public Vector2 TogglePosition;
    public Vector2 CalibratePosition;

    string shown;

    void LateUpdate()
    {
        if (!Toggle)
            return;

        var label = Plugin.Config.EnableFullBodyTracking.Value ? "Body Tracking: On" : "Body Tracking: Off";
        if (label != shown)
        {
            shown = label;
            FbtPauseMenuPatches.SetLabel(Toggle, label);
        }

        if (!LockPositions)
            return;

        if (Toggle.rectTransform)
            Toggle.rectTransform.anchoredPosition = TogglePosition;

        if (Calibrate && Calibrate.rectTransform)
            Calibrate.rectTransform.anchoredPosition = CalibratePosition;
    }
}
