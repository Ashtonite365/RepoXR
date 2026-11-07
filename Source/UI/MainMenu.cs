using System;
using System.Collections;
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
    private Canvas mainCanvas;

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
        RunStep("menu canvas", SetupMainCanvas);
        RunStep("controller pointer", SetupControllers);

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

    private void SetupMainCanvas()
    {
        mainCanvas = HUDCanvas.instance.GetComponent<Canvas>();
        mainCanvas.renderMode = RenderMode.WorldSpace;
        mainCanvas.transform.position = new Vector3(-45, -0.75f, 6);
        mainCanvas.transform.eulerAngles = new Vector3(0, 50, 0);
        mainCanvas.transform.localScale = Vector3.one * 0.03f;
        mainCanvas.transform.Find("HUD").gameObject.AddComponent<RectMask2D>();
        
        Destroy(mainCanvas.GetComponent<GraphicRaycaster>());
        mainCanvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
        
        // Remove game HUD elements
        mainCanvas.transform.Find("HUD/Game Hud").gameObject.SetActive(false);
        mainCanvas.transform.Find("HUD/Chat Local").gameObject.SetActive(false);
        
        // Set up chat
        if (RunManager.instance.levelCurrent == RunManager.instance.levelLobbyMenu)
        {
            var chat = mainCanvas.transform.Find("HUD/Chat").GetComponent<RectTransform>();
            chat.SetParent(mainCanvas.transform.Find("HUD"), false);
        }
        else
            mainCanvas.transform.Find("HUD/Chat").gameObject.SetActive(false);

        // Move top menu selection outline
        var selection = FindObjectOfType<MenuSelectionBoxTop>(true);
        if (selection != null)
        {
            var moved = selection.transform.parent.parent;
            moved.SetParent(moved.parent.parent, true);
        }
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

        var origin = host.GetComponent<XROrigin>();
        if (origin == null)
            origin = host.AddComponent<XROrigin>();
        origin.Origin = rig;
        origin.CameraFloorOffsetObject = floor.gameObject;
        host.SetActive(true);
        origin.Camera = mainCamera;

        if (mainCanvas != null)
            mainCanvas.worldCamera = mainCamera;

        if (host.GetComponent<XRRayInteractorManager>() != null)
            return;

        host.AddComponent<XRRayInteractorManager>();
    }

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