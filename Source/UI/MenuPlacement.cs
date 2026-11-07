using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

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
    internal static RectTransform Display { get; private set; }

    internal static bool Present(MenuPageMain page)
    {
        if (page == null)
            return false;

        if (presentedId == page.GetInstanceID() && Current != null && Display != null)
            return true;

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

            return false;
        }

        var rect = canvas.GetComponent<RectTransform>();
        var size = rect.rect.size;
        if (size.x < 1f || size.y < 1f)
            size = rect.sizeDelta;
        if (size.x < 1f)
            size.x = 712f;
        if (size.y < 1f)
            size.y = 400f;

        canvas.gameObject.SetActive(true);
        canvas.enabled = true;
        if (!page.gameObject.activeSelf)
            page.gameObject.SetActive(true);

        var camera = Camera.main;
        if (camera == null)
        {
            Logger.LogInfo("Menu page '" + page.name + "' is on '" + canvas.name + "', and Camera Main was not ready.");
            return false;
        }

        // Leave the game's anchors, pivot, and children alone. Draw that canvas into a
        // texture, then show the texture on a fixed plane. 1.3.16 rewrote the stretch
        // rect and the buttons stopped drawing even though a point still projected to centre.
        var capture = new GameObject("RepoFBT Menu Capture");
        var uiCamera = capture.AddComponent<Camera>();
        uiCamera.enabled = true;
        uiCamera.clearFlags = CameraClearFlags.SolidColor;
        uiCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        uiCamera.orthographic = true;
        uiCamera.orthographicSize = 1f;
        uiCamera.nearClipPlane = 0.01f;
        uiCamera.farClipPlane = 5f;
        uiCamera.transform.position = new Vector3(0f, -4000f, 0f);
        uiCamera.cullingMask = 1 << canvas.gameObject.layer;
        uiCamera.stereoTargetEye = StereoTargetEyeMask.None;
        uiCamera.depth = -50;
        uiCamera.allowHDR = false;
        uiCamera.allowMSAA = false;

        var texture = new RenderTexture(Mathf.Max(2, Mathf.RoundToInt(size.x)),
            Mathf.Max(2, Mathf.RoundToInt(size.y)), 0, RenderTextureFormat.ARGB32)
        {
            name = "RepoFBT Menu"
        };
        texture.Create();
        uiCamera.targetTexture = texture;
        uiCamera.aspect = size.x / size.y;

        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = uiCamera;
        canvas.planeDistance = 1f;

        var displayObject = new GameObject("RepoFBT Menu Plane");
        var displayCanvas = displayObject.AddComponent<Canvas>();
        displayCanvas.renderMode = RenderMode.WorldSpace;
        displayObject.layer = 0;
        var display = displayObject.GetComponent<RectTransform>();
        display.anchorMin = display.anchorMax = display.pivot = new Vector2(0.5f, 0.5f);
        display.sizeDelta = new Vector2(WidthMeters * 1000f, HeightMeters * 1000f);
        displayObject.transform.localScale = Vector3.one * 0.001f;
        var image = displayObject.AddComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = false;

        if (!TrySceneAnchor(camera, out var anchorName, out var truckPosition, out var standPosition, out var left,
                out var place, out var alongLeft))
        {
            Logger.LogInfo("Menu plane was not placed. No truck anchor was found in the main menu scene.");
            UnityEngine.Object.Destroy(displayObject);
            UnityEngine.Object.Destroy(capture);
            return false;
        }

        var toPlane = place - standPosition;
        toPlane.y = 0f;
        if (toPlane.sqrMagnitude < 0.01f)
            toPlane = -left;
        display.SetPositionAndRotation(place, Quaternion.LookRotation(toPlane.normalized, Vector3.up));
        display.SetParent(null, true);

        Current = rect;
        Display = display;
        presentedId = page.GetInstanceID();

        var view = camera.WorldToViewportPoint(display.position);
        var inView = MenuView.CenterInView(view.x, view.y, view.z);
        var pageRect = page.GetComponent<RectTransform>();
        Logger.LogInfo(
            "Menu page canvas '" + canvas.name +
            "' hud=" + MenuTarget.IsHudCanvas(canvas.name) +
            " leftInPlace=True mode=" + canvas.renderMode +
            " sourceSize=" + Format(size) +
            " page='" + page.name + "' active=" + page.gameObject.activeInHierarchy +
            " pagePivot=" + (pageRect != null ? Format(pageRect.pivot) : "(none)") +
            " pageAnchorMin=" + (pageRect != null ? Format(pageRect.anchorMin) : "(none)") +
            " pageAnchorMax=" + (pageRect != null ? Format(pageRect.anchorMax) : "(none)") +
            " anchor='" + anchorName +
            "' truckPos=" + Format(truckPosition) +
            " standPos=" + Format(standPosition) +
            " left=" + Format(left) +
            " alongLeft=" + alongLeft.ToString("0.###", CultureInfo.InvariantCulture) +
            " leftOfTruck=" + MenuTarget.IsLeftOfTruck(alongLeft) +
            " headLocked=False planePos=" + Format(display.position) +
            " planeViewport=" + Format(view) +
            " planeInView=" + inView +
            " cameraPos=" + Format(camera.transform.position) +
            " cameraFwd=" + Format(camera.transform.forward));
        ReleaseDesktopMirror(canvas);
        return true;
    }

    internal static bool TryLocal(Transform ray, out Vector2 canvasLocal, out Vector3 world)
    {
        canvasLocal = default;
        world = default;
        if (ray == null || Display == null || Current == null)
            return false;
        if (!XRRayInteractorManager.MenuPlaneHit(ray, Display, out world, out var planeLocal))
            return false;

        var plane = Display.rect;
        var source = Current.rect;
        if (plane.width < 0.001f || plane.height < 0.001f)
            return false;

        var u = (planeLocal.x - plane.xMin) / plane.width;
        var v = (planeLocal.y - plane.yMin) / plane.height;
        var mapped = MenuTarget.LocalFromUv(u, v, source.xMin, source.yMin, source.xMax, source.yMax);
        canvasLocal = new Vector2(mapped.x, mapped.y);
        return u >= 0f && u <= 1f && v >= 0f && v <= 1f;
    }

    static bool TrySceneAnchor(Camera camera, out string anchorName, out Vector3 truckPosition,
        out Vector3 standPosition, out Vector3 left, out Vector3 place, out float alongLeft)
    {
        anchorName = "";
        truckPosition = standPosition = left = place = default;
        alongLeft = 0f;
        var truck = FindTruck();
        if (truck == null)
            return false;

        anchorName = truck.name;
        truckPosition = truck.position;
        standPosition = SceneStand(camera.transform);
        var flatLeft = MenuTarget.PlayerLeft(standPosition.x, standPosition.z, truckPosition.x, truckPosition.z);
        left = new Vector3(flatLeft.x, flatLeft.y, flatLeft.z);
        var gap = TruckRadius(truck) + WidthMeters * 0.5f + 1.2f;
        var spot = MenuTarget.LeftOfTruck(truckPosition.x, truckPosition.z, left.x, left.z, gap, standPosition.y);
        place = new Vector3(spot.x, spot.y, spot.z);
        alongLeft = Vector3.Dot(place - truckPosition, left);
        return true;
    }

    static Transform? FindTruck()
    {
        var animated = UnityEngine.Object.FindObjectOfType<TruckMenuAnimated>();
        if (animated != null)
            return animated.transform;

        Transform? best = null;
        var bestSize = 0f;
        foreach (var renderer in UnityEngine.Object.FindObjectsOfType<Renderer>())
        {
            if (renderer == null || renderer.name.IndexOf("truck", StringComparison.OrdinalIgnoreCase) < 0 &&
                (renderer.transform.parent == null ||
                 renderer.transform.parent.name.IndexOf("truck", StringComparison.OrdinalIgnoreCase) < 0))
                continue;

            var size = renderer.bounds.size.sqrMagnitude;
            if (size <= bestSize)
                continue;

            bestSize = size;
            best = renderer.transform.parent != null &&
                   renderer.transform.parent.name.IndexOf("truck", StringComparison.OrdinalIgnoreCase) >= 0
                ? renderer.transform.parent
                : renderer.transform;
        }

        return best;
    }

    static float TruckRadius(Transform truck)
    {
        var radius = 2f;
        foreach (var renderer in truck.GetComponentsInChildren<Renderer>())
        {
            if (renderer == null)
                continue;
            var extent = renderer.bounds.extents.magnitude;
            if (extent > radius)
                radius = extent;
        }

        return radius;
    }

    static Vector3 SceneStand(Transform camera)
    {
        var stand = camera;
        var cursor = camera;
        while (cursor.parent != null && Vector3.Distance(cursor.parent.position, camera.position) < 8f)
        {
            cursor = cursor.parent;
            stand = cursor;
        }

        return stand.position;
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

        foreach (var other in UnityEngine.Object.FindObjectsOfType<Canvas>())
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
