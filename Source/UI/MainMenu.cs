using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using RepoXR.Input;
using RepoXR.Player.Camera;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace RepoXR.UI;

public class MainMenu : MonoBehaviour
{
    private Camera mainCamera;

    private void Awake()
    {
        Actions.Instance["ResetHeight"].performed += OnResetHeight;

        if (RunManager.instance.levelCurrent == RunManager.instance.levelLobbyMenu)
            StartCoroutine(LobbyLinkCopy());
    }

    private void OnDestroy()
    {
        Actions.Instance["ResetHeight"].performed -= OnResetHeight;
    }
    
    private IEnumerator Start()
    {
        yield return null;

        RunStep("menu event system", DisableEventSystem);
        RunStep("menu camera", SetupMainCamera);
        RunStep("controller pointer", SetupControllers);
        StartCoroutine(WaitForMenuPage());

        if (XRRayInteractorManager.Instance == null)
            Logger.LogWarning("Menu controller pointer was not created. The game cursor stays available.");
        else
            Logger.LogInfo("Menu controller pointer is ready.");
    }

    static void RunStep(string name, Action step)
    {
        try
        {
            step();
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Menu setup step '{name}' failed ({ex.GetType().Name}: {ex.Message}).");
        }
    }
    
    private static void DisableEventSystem()
    {
        var eventSystem = GameObject.Find("EventSystem");
        if (eventSystem == null)
            return;

        var input = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (input != null)
            input.enabled = false;

        var xrUi = eventSystem.GetComponent<XRUIInputModule>();
        if (xrUi == null)
            xrUi = eventSystem.AddComponent<XRUIInputModule>();
        xrUi.enabled = true;
    }
    
    private void SetupMainCamera()
    {
        // Camera rendering setup
        mainCamera = CameraUtils.Instance.MainCamera;

        var topCamera = mainCamera.transform.Find("Camera Top").GetComponent<Camera>();
        topCamera.depth = 1;
        topCamera.targetTexture = null;
    }

    IEnumerator WaitForMenuPage()
    {
        var elapsed = 0f;
        while (elapsed < 8f)
        {
            var page = MenuPageMain.instance != null
                ? MenuPageMain.instance
                : FindObjectOfType<MenuPageMain>(true);
            if (page != null)
            {
                if (!page.gameObject.activeSelf)
                    page.gameObject.SetActive(true);
                if (MenuPlacement.Present(page))
                    yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Logger.LogInfo("MenuPageMain was still missing 8 seconds after setup. HUD Canvas was not moved.");
    }

    private void SetupControllers()
    {
        if (mainCamera == null || mainCamera.transform.parent == null)
            throw new InvalidOperationException("the VR camera rig is not ready");

        var rig = mainCamera.transform.parent.gameObject;
        var host = rig.transform.Find("XR Origin")?.gameObject;
        if (host == null)
        {
            host = new GameObject("XR Origin");
            host.SetActive(false);
            host.transform.SetParent(rig.transform, false);
        }
        else if (host.activeSelf)
            host.SetActive(false);

        var floor = host.transform.Find("Camera Floor Offset");
        if (floor == null)
        {
            var floorObject = new GameObject("Camera Floor Offset");
            floorObject.transform.SetParent(host.transform, false);
            floor = floorObject.transform;
        }

        var cameraParent = mainCamera.transform.parent;
        var cameraLocalPosition = mainCamera.transform.localPosition;
        var cameraLocalRotation = mainCamera.transform.localRotation;
        var cameraActive = mainCamera.gameObject.activeSelf;
        var parentBefore = Chain(cameraParent);

        var origin = host.GetComponent<XROrigin>();
        if (origin == null)
            origin = host.AddComponent<XROrigin>();
        // Origin and the floor object have to be set before the component awakens.
        // Assigning Camera makes XR Origin reparent Camera Main under the floor offset.
        // That is the game camera the menu was framed on, so it stays where the game put it.
        origin.Origin = rig;
        origin.CameraFloorOffsetObject = floor.gameObject;
        host.SetActive(true);

        var parentAfter = Chain(mainCamera.transform.parent);
        var cameraMoved = !MenuView.CameraStayed(parentBefore, parentAfter, cameraActive,
            mainCamera.gameObject.activeSelf);
        if (cameraMoved && cameraParent != null)
        {
            mainCamera.transform.SetParent(cameraParent, false);
            mainCamera.transform.localPosition = cameraLocalPosition;
            mainCamera.transform.localRotation = cameraLocalRotation;
            if (cameraActive)
                mainCamera.gameObject.SetActive(true);
            Logger.LogWarning(
                $"XR Origin changed Camera Main ({parentBefore} -> {parentAfter}). Restored the game camera parent so the menu stays on it.");
            parentAfter = Chain(mainCamera.transform.parent);
        }

        LogMenuView(parentAfter);

        if (host.GetComponent<XRRayInteractorManager>() != null)
            return;

        host.AddComponent<XRRayInteractorManager>();
    }

    void LogMenuView(string cameraParents)
    {
        var camera = mainCamera;
        var hud = HUDCanvas.instance != null ? HUDCanvas.instance.GetComponent<Canvas>() : null;
        var page = FindObjectOfType<MenuPageMain>(true);
        if (camera == null || hud == null)
        {
            Logger.LogInfo("Menu view: HUD Canvas or Camera Main was missing. HUD Canvas was not moved. menuPage=" +
                           (page == null ? "missing" : page.name));
            return;
        }

        var center = camera.WorldToViewportPoint(hud.transform.position);
        Logger.LogInfo(
            "Menu view: HUD Canvas left in place active=" + hud.gameObject.activeInHierarchy +
            " enabled=" + hud.enabled +
            " mode=" + hud.renderMode +
            " pos=" + Format(hud.transform.position) +
            " viewport=" + Format(center) +
            " menuPage=" + (page == null ? "missing" : page.name + " active=" + page.gameObject.activeInHierarchy) +
            " cameraParents=[" + cameraParents + "]. The menu canvas is chosen when MenuPageMain starts.");
    }

    static string Chain(Transform node)
    {
        if (node == null)
            return "(none)";

        var parts = new List<string>();
        for (var guard = 0; node != null && guard < 8; guard++)
        {
            parts.Add(node.name + ":" + (node.gameObject.activeInHierarchy ? "shown" : "hidden"));
            node = node.parent;
        }

        return string.Join(" / ", parts);
    }

    static string Format(Vector3 value) =>
        string.Format(CultureInfo.InvariantCulture, "({0:0.###},{1:0.###},{2:0.###})", value.x, value.y, value.z);

    private static IEnumerator LobbyLinkCopy()
    {
        while (true)
        {
            yield return null;
            
            if ((!UnityEngine.Input.GetKey(KeyCode.LeftControl) && !UnityEngine.Input.GetKey(KeyCode.RightControl)) ||
                !UnityEngine.Input.GetKeyDown(KeyCode.C)) 
                continue;
            
            var lobby = SteamManager.instance.currentLobby;
            var link = $"steam://joinlobby/3241660/{lobby.Id}/{lobby.Owner.Id}";

            GUIUtility.systemCopyBuffer = link;
            
            Logger.LogInfo($"Copied lobby link: {link}");
        }
        
        // ReSharper disable once IteratorNeverReturns
    }
    
    private static void OnResetHeight(InputAction.CallbackContext obj)
    {
        VRCameraAim.instance.SetPlayerAim(0, true);
    }
}