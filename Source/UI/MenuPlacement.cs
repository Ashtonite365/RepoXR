using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace RepoXR.UI;

/// <summary>
/// Presents the canvas that actually contains <see cref="MenuPageMain"/>.
/// HUD Canvas is left alone unless the page is under it.
/// </summary>
internal static class MenuPlacement
{
    internal const float WidthMeters = 1.6f;
    internal const float DistanceMeters = 1.4f;

    static int presentedId;
    static bool loggedWaiting;

    internal static RectTransform Current { get; private set; }

    internal static void Present(MenuPageMain page)
    {
        if (page == null)
            return;

        if (presentedId == page.GetInstanceID() && Current != null)
            return;

        var canvases = page.GetComponentsInParent<Canvas>(true);
        var canvas = canvases != null && canvases.Length > 0 ? canvases[0] : null;
        if (canvas == null ||
            !MenuTarget.Accept(true, canvas.name,
                page.transform == canvas.transform || page.transform.IsChildOf(canvas.transform)))
        {
            if (!loggedWaiting)
            {
                loggedWaiting = true;
                Logger.LogInfo("MenuPageMain has no parent canvas yet. HUD Canvas was not moved.");
            }

            return;
        }

        var rect = canvas.GetComponent<RectTransform>();
        var width = rect.rect.width;
        if (width < 1f)
            width = rect.sizeDelta.x;

        canvas.gameObject.SetActive(true);
        canvas.enabled = true;
        if (!page.gameObject.activeSelf)
            page.gameObject.SetActive(true);

        var camera = Camera.main;
        if (camera == null)
        {
            Logger.LogInfo("Menu page '" + page.name + "' is on '" + canvas.name + "', and Camera Main was not ready.");
            return;
        }

        var scale = MenuTarget.ScaleForWidth(width, WidthMeters);
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = camera;
        canvas.transform.SetParent(camera.transform, false);
        canvas.transform.localPosition = new Vector3(0f, 0f, DistanceMeters);
        canvas.transform.localRotation = Quaternion.identity;
        canvas.transform.localScale = new Vector3(scale, scale, scale);

        var raycaster = canvas.GetComponent<GraphicRaycaster>();
        if (raycaster != null)
            raycaster.enabled = false;
        if (canvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();

        Current = rect;
        presentedId = page.GetInstanceID();

        var center = camera.WorldToViewportPoint(canvas.transform.position);
        var inView = MenuView.CenterInView(center.x, center.y, center.z);
        Logger.LogInfo(
            "Menu page canvas '" + canvas.name +
            "' hud=" + MenuTarget.IsHudCanvas(canvas.name) +
            " active=" + canvas.gameObject.activeInHierarchy +
            " enabled=" + canvas.enabled +
            " mode=" + canvas.renderMode +
            " pos=" + Format(canvas.transform.position) +
            " scale=" + Format(canvas.transform.lossyScale) +
            " page='" + page.name + "' active=" + page.gameObject.activeInHierarchy +
            " viewport=" + Format(center) +
            " canvasCenterInView=" + inView);
    }

    static string Format(Vector3 value) =>
        string.Format(CultureInfo.InvariantCulture, "({0:0.###},{1:0.###},{2:0.###})", value.x, value.y, value.z);
}
