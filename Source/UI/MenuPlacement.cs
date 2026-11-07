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
            return;
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
        displayObject.transform.localScale = Vector3.one * (0.001f * MenuTarget.MenuScale);
        var image = displayObject.AddComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = false;

        // 1.3.17 capture is unchanged. The plane is placed once from the front panel:
        // 3 m in front of the nose, 1.8 m above that panel, facing the camera position
        // sampled here. It is not parented to the camera and this method does not run again.
        var cameraStart = camera.transform.position;
        var position = cameraStart + camera.transform.forward * DistanceMeters;
        var rotation = camera.transform.rotation;
        var poseDetail = "moved=False";
        var animated = UnityEngine.Object.FindObjectOfType<TruckMenuAnimated>();
        if (animated != null && animated.frontPanelTransform != null)
        {
            var panel = animated.frontPanelTransform;
            var fwd = panel.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f)
            {
                fwd = animated.transform.forward;
                fwd.y = 0f;
            }

            if (fwd.sqrMagnitude > 0.0001f)
            {
                fwd.Normalize();
                var pose = MenuTarget.MenuPose(panel.position.x, panel.position.y, panel.position.z, fwd.x, fwd.z,
                    cameraStart.x, cameraStart.z);
                position = new Vector3(pose.x, pose.y, pose.z);
                var face = new Vector3(pose.faceX, 0f, pose.faceZ);
                rotation = Quaternion.LookRotation(-face.normalized, Vector3.up);
                var sized = MenuTarget.PhysicalSize(WidthMeters, HeightMeters, MenuTarget.MenuScale);
                poseDetail = "moved=True anchor='frontPanel' panelPos=" + Format(panel.position) +
                             " rootPos=" + Format(animated.transform.position) +
                             " truckFwd=" + Format(fwd) +
                             " face=" + Format(face.normalized) +
                             " cameraStart=" + Format(cameraStart) +
                             " forward=" + MenuTarget.MenuForwardMeters.ToString("0.###", CultureInfo.InvariantCulture) +
                             " raise=" + MenuTarget.MenuRaiseMeters.ToString("0.###", CultureInfo.InvariantCulture) +
                             " scale=" + MenuTarget.MenuScale.ToString("0.###", CultureInfo.InvariantCulture) +
                             " planeSize=(" + sized.width.ToString("0.###", CultureInfo.InvariantCulture) + "," +
                             sized.height.ToString("0.###", CultureInfo.InvariantCulture) + ")";
            }
        }

        display.SetPositionAndRotation(position, rotation);
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
            " " + poseDetail +
            " headLocked=False planePos=" + Format(display.position) +
            " planeForward=" + Format(display.forward) +
            " planeRot=" + Format(display.eulerAngles) +
            " planeViewport=" + Format(view) +
            " planeInView=" + inView +
            " cameraPos=" + Format(camera.transform.position) +
            " cameraFwd=" + Format(camera.transform.forward));
        ReleaseDesktopMirror(canvas);
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
