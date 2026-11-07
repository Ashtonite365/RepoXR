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
    internal const float HeightMeters = 0.9f;
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
        // 1.3.15: pivot (0,0) and anchors (0,0)-(1,1). Detaching that stretch rect
        // collapses it, so the buttons vanish while a stored point still projects to centre.
        var size = FreezeSize(rect);

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

        var scale = MenuTarget.ScaleToFit(size.x, size.y, WidthMeters, HeightMeters);
        var local = MenuTarget.LocalPositionForContent(0f, 0f, scale, DistanceMeters);
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = camera;
        canvas.transform.SetParent(camera.transform, false);
        canvas.transform.localRotation = Quaternion.identity;
        canvas.transform.localScale = new Vector3(scale, scale, scale);
        canvas.transform.localPosition = new Vector3(local.x, local.y, local.z);
        var heldRotation = canvas.transform.rotation;
        var heldPosition = canvas.transform.position;
        canvas.transform.SetParent(null, true);
        canvas.transform.SetPositionAndRotation(heldPosition, heldRotation);

        var raycaster = canvas.GetComponent<GraphicRaycaster>();
        if (raycaster != null)
            raycaster.enabled = false;
        if (canvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();

        Current = rect;
        presentedId = page.GetInstanceID();

        var contentWorld = canvas.transform.position;
        var center = camera.WorldToViewportPoint(contentWorld);
        var inView = MenuView.CenterInView(center.x, center.y, center.z);
        var pageRect = page.GetComponent<RectTransform>();
        Logger.LogInfo(
            "Menu page canvas '" + canvas.name +
            "' hud=" + MenuTarget.IsHudCanvas(canvas.name) +
            " active=" + canvas.gameObject.activeInHierarchy +
            " enabled=" + canvas.enabled +
            " mode=" + canvas.renderMode +
            " pos=" + Format(canvas.transform.position) +
            " scale=" + Format(canvas.transform.lossyScale) +
            " pivot=" + (pageRect != null ? Format(pageRect.pivot) : "(none)") +
            " anchorMin=" + (pageRect != null ? Format(pageRect.anchorMin) : "(none)") +
            " anchorMax=" + (pageRect != null ? Format(pageRect.anchorMax) : "(none)") +
            " frozenSize=" + Format(size) +
            " contentCenter=" + Format(Vector3.zero) +
            " localOffset=" + Format(new Vector3(local.x, local.y, local.z)) +
            " page='" + page.name + "' active=" + page.gameObject.activeInHierarchy +
            " contentViewport=" + Format(center) +
            " contentInView=" + inView +
            " cameraPos=" + Format(camera.transform.position) +
            " cameraFwd=" + Format(camera.transform.forward));
        ReleaseDesktopMirror(canvas);
    }

    static Vector2 FreezeSize(RectTransform rect)
    {
        var size = rect.rect.size;
        if (size.x < 1f || size.y < 1f)
            size = rect.sizeDelta;

        var worldCenter = rect.TransformPoint(rect.rect.center);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.position = worldCenter;
        return size;
    }

    static void ReleaseDesktopMirror(Canvas menuCanvas)
    {
        var named = GameObject.Find("UI/UI/Canvas");
        if (named != null)
        {
            var namedCanvas = named.GetComponent<Canvas>();
            if (namedCanvas != null && namedCanvas != menuCanvas && namedCanvas.enabled)
            {
                namedCanvas.enabled = false;
                Logger.LogInfo("Desktop mirror: disabled screen canvas 'UI/UI/Canvas'.");
            }
        }

        foreach (var other in Object.FindObjectsOfType<Canvas>())
        {
            if (other == null || other == menuCanvas || !other.enabled)
                continue;
            if (other.renderMode != RenderMode.ScreenSpaceOverlay)
                continue;

            Logger.LogInfo(
                "Desktop mirror: disabled screen overlay '" + other.name + "' sorting=" + other.sortingOrder +
                ". It draws on the game window after the headset image.");
            other.enabled = false;
        }
    }

    static string Format(Vector3 value) =>
        string.Format(CultureInfo.InvariantCulture, "({0:0.###},{1:0.###},{2:0.###})", value.x, value.y, value.z);

    static string Format(Vector2 value) =>
        string.Format(CultureInfo.InvariantCulture, "({0:0.###},{1:0.###})", value.x, value.y);
}
