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
    static bool loggedMiss;

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

        if (!TrySceneAnchor(camera, out var detail, out var place, out var rotation))
        {
            if (!loggedMiss)
            {
                loggedMiss = true;
                Logger.LogInfo("Menu plane was not placed. " + detail);
            }

            UnityEngine.Object.Destroy(displayObject);
            UnityEngine.Object.Destroy(capture);
            return false;
        }

        display.SetPositionAndRotation(place, rotation);
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
            " " + detail +
            " headLocked=False planePos=" + Format(display.position) +
            " planeForward=" + Format(display.forward) +
            " planeRot=" + Format(display.eulerAngles) +
            " planeViewport=" + Format(view) +
            " planeInView=" + inView +
            " viewportIsNotRenderProof=True" +
            " captureTex=" + texture.width + "x" + texture.height +
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

    static bool TrySceneAnchor(Camera camera, out string detail, out Vector3 place, out Quaternion rotation)
    {
        detail = "";
        place = default;
        rotation = Quaternion.identity;
        var origin = camera.transform.position;
        var forward = camera.transform.forward;
        var seen = new System.Collections.Generic.List<MeshNote>();
        foreach (var renderer in UnityEngine.Object.FindObjectsOfType<Renderer>())
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;
            if (renderer is ParticleSystemRenderer)
                continue;
            if (renderer.name.StartsWith("RepoFBT", StringComparison.Ordinal))
                continue;

            var bounds = renderer.bounds;
            var largest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (largest < 1.2f || largest > 20f)
                continue;

            var center = bounds.center;
            var offset = MenuTarget.ViewOffset(origin.x, origin.z, forward.x, forward.z, center.x, center.z);
            var view = camera.WorldToViewportPoint(center);
            var inFrame = view.z > 0.4f && view.z < 22f && view.x > -0.25f && view.x < 1.25f && view.y > -0.35f &&
                          view.y < 1.35f;
            var pass = inFrame && MenuTarget.InViewCone(offset.ahead, offset.lateral);
            var parentName = renderer.transform.parent != null ? renderer.transform.parent.name : "";
            seen.Add(new MeshNote
            {
                Name = renderer.name,
                Parent = parentName,
                Center = center,
                Size = bounds.size,
                Ahead = offset.ahead,
                Lateral = offset.lateral,
                Viewport = view,
                Pass = pass,
                Transform = renderer.transform,
                Bounds = bounds
            });
        }

        MeshNote? chosen = null;
        MeshNote? named = null;
        MeshNote? rejected = null;
        var largestVolume = -1f;
        var namedVolume = -1f;
        var rejectedVolume = -1f;
        foreach (var note in seen)
        {
            var volume = note.Size.x * note.Size.y * note.Size.z;
            if (!note.Pass)
            {
                if (volume <= rejectedVolume)
                    continue;
                rejectedVolume = volume;
                rejected = note;
                continue;
            }

            if (volume > largestVolume)
            {
                largestVolume = volume;
                chosen = note;
            }

            var truckNamed = note.Name.IndexOf("truck", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             note.Parent.IndexOf("truck", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!truckNamed || volume <= namedVolume)
                continue;
            namedVolume = volume;
            named = note;
        }

        if (named != null && namedVolume >= largestVolume * 0.5f)
            chosen = named;

        var animated = UnityEngine.Object.FindObjectOfType<TruckMenuAnimated>();
        var controllerText = DescribeController(camera, animated);
        if (chosen == null)
        {
            detail = "No renderer bounds were in the camera view. " + controllerText + " " + Describe(seen);
            return false;
        }

        var body = chosen.Bounds;
        foreach (var note in seen)
        {
            if (!note.Pass || note == chosen)
                continue;
            if ((note.Center - chosen.Center).sqrMagnitude > 9f)
                continue;
            body.Encapsulate(note.Bounds);
        }

        var toCamera = origin - body.center;
        toCamera.y = 0f;
        var meshForward = chosen.Transform.forward;
        meshForward.y = 0f;
        var forwardSource = "mesh";
        if (meshForward.sqrMagnitude < 0.0001f)
        {
            meshForward = toCamera;
            forwardSource = "toward camera";
        }
        else if (Vector3.Dot(meshForward, toCamera) < 0f)
        {
            meshForward = -meshForward;
            forwardSource = "mesh flipped toward camera";
        }

        meshForward.Normalize();
        var meshRight = Vector3.Cross(Vector3.up, meshForward);
        var pose = MenuTarget.SketchPose(body.center.x, body.center.z, meshForward.x, meshForward.z, meshRight.x,
            meshRight.z, ExtentAlong(body, meshForward), ExtentAlong(body, meshRight), origin.y);
        place = new Vector3(pose.x, pose.y, pose.z);
        var face = new Vector3(pose.faceX, 0f, pose.faceZ);
        if (face.sqrMagnitude < 0.0001f)
            face = -meshRight;
        rotation = Quaternion.LookRotation(-face.normalized, Vector3.up);
        var centerView = camera.WorldToViewportPoint(body.center);
        detail = "anchor='" + chosen.Name +
                 "' parent='" + chosen.Parent +
                 "' truckPos=" + Format(body.center) +
                 " bounds=" + Format(body.size) +
                 " centerViewport=" + Format(centerView) +
                 " ahead=" + chosen.Ahead.ToString("0.###", CultureInfo.InvariantCulture) +
                 " lateral=" + chosen.Lateral.ToString("0.###", CultureInfo.InvariantCulture) +
                 " forwardSource=" + forwardSource +
                 " truckFwd=" + Format(meshForward) +
                 " face=" + Format(face.normalized) +
                 " raise=" + MenuTarget.SketchRaiseMeters.ToString("0.###", CultureInfo.InvariantCulture) +
                 " " + controllerText +
                 (rejected == null
                     ? ""
                     : " rejected='" + rejected.Name +
                       "' rejectedCenter=" + Format(rejected.Center) +
                       " rejectedAhead=" + rejected.Ahead.ToString("0.###", CultureInfo.InvariantCulture) +
                       " rejectedLateral=" + rejected.Lateral.ToString("0.###", CultureInfo.InvariantCulture) +
                       " rejectedViewport=" + Format(rejected.Viewport));
        return true;
    }

    static string DescribeController(Camera camera, TruckMenuAnimated? animated)
    {
        if (animated == null)
            return "controller=missing";

        var position = animated.transform.position;
        var forward = camera.transform.forward;
        var offset = MenuTarget.ViewOffset(camera.transform.position.x, camera.transform.position.z, forward.x,
            forward.z, position.x, position.z);
        var view = camera.WorldToViewportPoint(position);
        return "controller='" + animated.name +
               "' controllerPos=" + Format(position) +
               " controllerAhead=" + offset.ahead.ToString("0.###", CultureInfo.InvariantCulture) +
               " controllerLateral=" + offset.lateral.ToString("0.###", CultureInfo.InvariantCulture) +
               " controllerViewport=" + Format(view) +
               " controllerInView=" + MenuTarget.InViewCone(offset.ahead, offset.lateral);
    }

    static string Describe(System.Collections.Generic.List<MeshNote> seen)
    {
        seen.Sort((a, b) => (b.Size.x * b.Size.y * b.Size.z).CompareTo(a.Size.x * a.Size.y * a.Size.z));
        var text = "candidates=" + seen.Count;
        var shown = 0;
        foreach (var note in seen)
        {
            if (shown >= 4)
                break;
            shown++;
            text += " [" + note.Name + " pass=" + note.Pass + " center=" + Format(note.Center) + " ahead=" +
                    note.Ahead.ToString("0.###", CultureInfo.InvariantCulture) + " lateral=" +
                    note.Lateral.ToString("0.###", CultureInfo.InvariantCulture) + "]";
        }

        return text;
    }

    static float ExtentAlong(Bounds bounds, Vector3 direction)
    {
        return bounds.extents.x * Mathf.Abs(direction.x)
               + bounds.extents.y * Mathf.Abs(direction.y)
               + bounds.extents.z * Mathf.Abs(direction.z);
    }

    sealed class MeshNote
    {
        internal string Name = "";
        internal string Parent = "";
        internal Vector3 Center;
        internal Vector3 Size;
        internal float Ahead;
        internal float Lateral;
        internal Vector3 Viewport;
        internal bool Pass;
        internal Transform Transform = null!;
        internal Bounds Bounds;
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
