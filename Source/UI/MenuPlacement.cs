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
        var contentCenter = ContentCenter(rect, page.transform);
        var local = MenuTarget.LocalPositionForContent(contentCenter.x, contentCenter.y, scale, DistanceMeters);
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = camera;
        canvas.transform.SetParent(camera.transform, false);
        canvas.transform.localRotation = Quaternion.identity;
        canvas.transform.localScale = new Vector3(scale, scale, scale);
        canvas.transform.localPosition = new Vector3(local.x, local.y, local.z);
        // One placement in the world. The view rig (Tilt and the rest) keeps moving;
        // leaving the canvas on that rig would drag the menu with the head.
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

        var contentWorld = canvas.transform.TransformPoint(contentCenter);
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
            " contentCenter=" + Format(contentCenter) +
            " localOffset=" + Format(new Vector3(local.x, local.y, local.z)) +
            " page='" + page.name + "' active=" + page.gameObject.activeInHierarchy +
            " contentViewport=" + Format(center) +
            " contentInView=" + inView +
            " cameraPos=" + Format(camera.transform.position) +
            " cameraFwd=" + Format(camera.transform.forward));
        ReleaseDesktopMirror(canvas);
    }

    static Vector3 ContentCenter(RectTransform canvas, Transform page)
    {
        var canvasSize = canvas.rect.size;
        var any = false;
        var total = new Bounds(Vector3.zero, Vector3.zero);
        foreach (var child in page.GetComponentsInChildren<RectTransform>(true))
        {
            if (child == null || child == canvas)
                continue;
            if (child.gameObject == page.gameObject && page.childCount > 0)
                continue;

            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvas, child);
            if (canvasSize.x > 1f && canvasSize.y > 1f &&
                bounds.size.x > canvasSize.x * 0.85f && bounds.size.y > canvasSize.y * 0.85f)
                continue;

            if (!any)
            {
                total = bounds;
                any = true;
            }
            else
                total.Encapsulate(bounds);
        }

        if (!any)
            total = RectTransformUtility.CalculateRelativeRectTransformBounds(canvas, page);

        return total.center;
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
