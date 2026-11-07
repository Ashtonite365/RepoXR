using System;
using RepoXR.Assets;
using RepoXR.Input;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

namespace RepoXR.UI;

public class XRRayInteractorManager : MonoBehaviour
{
    public static XRRayInteractorManager? Instance { get; private set; }

    private XRRayInteractor leftInteractor;
    private XRRayInteractor rightInteractor;
    private ActionBasedController leftActions;
    private ActionBasedController rightActions;
    private LineRenderer leftRenderer;
    private LineRenderer rightRenderer;

    private ActiveController activeController;
    
    private void Awake()
    {
        Instance = this;

        try
        {
            (leftActions, leftInteractor, leftRenderer) = CreateInteractorController("Left");
            (rightActions, rightInteractor, rightRenderer) = CreateInteractorController("Right");

            leftActions.uiPressAction.action.performed += LeftControllerPressed;
            rightActions.uiPressAction.action.performed += RightControllerPressed;

            UpdateActiveController(ActiveController.Right);
        }
        catch (Exception ex)
        {
            Instance = null;
            Logger.LogWarning($"Controller pointer was not bound ({ex.GetType().Name}: {ex.Message}).");
            enabled = false;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (leftActions != null && leftActions.uiPressAction.action != null)
            leftActions.uiPressAction.action.performed -= LeftControllerPressed;
        if (rightActions != null && rightActions.uiPressAction.action != null)
            rightActions.uiPressAction.action.performed -= RightControllerPressed;
    }

    void LateUpdate()
    {
        var tracking = TrackingInput.Instance;
        if (tracking == null)
            return;

        var rig = transform;
        PoseFromTracking(leftInteractor, tracking.LeftHandTransform, rig);
        PoseFromTracking(rightInteractor, tracking.RightHandTransform, rig);
    }

    static void PoseFromTracking(XRRayInteractor interactor, Transform tracked, Transform rig)
    {
        if (interactor == null || tracked == null || rig == null)
            return;

        if (tracked.localPosition.sqrMagnitude < 0.0001f && tracked.localRotation == Quaternion.identity)
        {
            var camera = rig.GetComponent<Unity.XR.CoreUtils.XROrigin>()?.Camera;
            if (camera != null)
                interactor.transform.SetPositionAndRotation(camera.transform.position,
                    camera.transform.rotation * Quaternion.Euler(15f, 0f, 0f));
            return;
        }

        interactor.transform.SetPositionAndRotation(
            rig.TransformPoint(tracked.localPosition),
            rig.rotation * tracked.localRotation);
    }

    public void SetVisible(bool visible)
    {
        leftInteractor.GetComponent<XRInteractorLineVisual>().enabled = visible;
        rightInteractor.GetComponent<XRInteractorLineVisual>().enabled = visible;
    }

    public void SetLineSortingOrder(int sortingOrder)
    {
        leftRenderer.sortingOrder = sortingOrder;
        rightRenderer.sortingOrder = sortingOrder;
    }
    
    private void LeftControllerPressed(InputAction.CallbackContext obj)
    {
        UpdateActiveController(ActiveController.Left);
    }

    private void RightControllerPressed(InputAction.CallbackContext obj)
    {
        UpdateActiveController(ActiveController.Right);
    }

    public Vector2 GetUIHitPosition(RectTransform? rect)
    {
        var (interactor, _) = GetActiveInteractor();
        if (!interactor.TryGetCurrentUIRaycastResult(out var result))
            return Vector2.one * -1000;

        var canvas = rect is null
            ? result.gameObject.GetComponentInParent<Canvas>().transform
            : rect.GetComponentInParent<Canvas>().transform;

        var local = canvas.InverseTransformPoint(result.worldPosition);
        return new Vector2(local.x, local.y);
    }

    public RectTransform GetUIHitRectTransform()
    {
        var (interactor, _) = GetActiveInteractor();
        return !interactor.TryGetCurrentUIRaycastResult(out var result)
            ? HUDCanvas.instance.GetComponent<RectTransform>()
            : result.gameObject.GetComponent<RectTransform>();
    }

    public bool GetTriggerDown()
    {
        var (_, actions) = GetActiveInteractor();
        return actions.uiPressAction.action.WasPressedThisFrame();
    }

    public bool GetTriggerButton()
    {
        var (_, actions) = GetActiveInteractor();
        return actions.uiPressAction.action.IsPressed();
    }

    public float GetUIScrollX()
    {
        var (_, actions) = GetActiveInteractor();
        return actions.uiScrollAction.action.ReadValue<Vector2>().x;
    }

    public float GetUIScrollY()
    {
        var (_, actions) = GetActiveInteractor();
        return actions.uiScrollAction.action.ReadValue<Vector2>().y;
    }

    private void UpdateActiveController(ActiveController newValue)
    {
        activeController = newValue;
        
        var oldInteractor = newValue == ActiveController.Left ? rightInteractor : leftInteractor;
        var newInteractor = newValue == ActiveController.Left ? leftInteractor : rightInteractor;

        var oldVisual = oldInteractor.GetComponent<XRInteractorLineVisual>();
        var newVisual = newInteractor.GetComponent<XRInteractorLineVisual>();

        oldVisual.lineLength = 1;
        newVisual.lineLength = 20;

        oldVisual.invalidColorGradient = oldVisual.validColorGradient = new Gradient
        {
            mode = GradientMode.Blend,
            alphaKeys =
            [
                new GradientAlphaKey(0.05f, 0),
                new GradientAlphaKey(0.05f, 0.8f),
                new GradientAlphaKey(0f, 1),
            ],
            colorKeys =
            [
                new GradientColorKey(Color.white, 0),
                new GradientColorKey(Color.white, 1)
            ]
        };
        
        newVisual.invalidColorGradient = new Gradient
        {
            mode = GradientMode.Blend,
            alphaKeys =
            [
                new GradientAlphaKey(0.9f, 0),
                new GradientAlphaKey(0.9f, 1),
            ],
            colorKeys =
            [
                new GradientColorKey(Color.white, 0),
                new GradientColorKey(Color.white, 1)
            ]
        };

        newVisual.validColorGradient = new Gradient
        {
            mode = GradientMode.Blend,
            alphaKeys =
            [
                new GradientAlphaKey(1, 0),
                new GradientAlphaKey(1, 1),
            ],
            colorKeys =
            [
                new GradientColorKey(Color.white, 0),
                new GradientColorKey(Color.white, 1)
            ]
        };
    }

    public (XRRayInteractor, ActionBasedController) GetActiveInteractor()
    {
        return activeController switch
        {
            ActiveController.Left => (leftInteractor, leftActions),
            ActiveController.Right => (rightInteractor, rightActions),
            _ => throw new ArgumentOutOfRangeException()
        };
    }
    
    private (ActionBasedController, XRRayInteractor, LineRenderer) CreateInteractorController(string hand)
    {
        var go = new GameObject($"{hand} Controller")
        {
            transform =
            {
                parent = transform
            },
            layer = 5
        };

        var controller = go.AddComponent<ActionBasedController>();
        var interactor = go.AddComponent<XRRayInteractor>();
        var visual = go.AddComponent<XRInteractorLineVisual>();
        var renderer = go.GetComponent<LineRenderer>();
        
        interactor.raycastMask = 1 << 5;

        visual.lineBendRatio = 1;
        visual.invalidColorGradient = new Gradient
        {
            mode = GradientMode.Blend,
            alphaKeys =
            [
                new GradientAlphaKey(0.1f, 0),
                new GradientAlphaKey(0.1f, 1),
            ],
            colorKeys =
            [
                new GradientColorKey(Color.white, 0),
                new GradientColorKey(Color.white, 1)
            ]
        };
        visual.enabled = true;
        visual.lineLength = 20;
        renderer.sortingOrder = 4;

        renderer.material = AssetCollection.DefaultLine;
        if (renderer.material == null)
            throw new InvalidOperationException("the controller line material is missing");

        AddActionBasedControllerBinds(controller, hand);
        
        return (controller, interactor, renderer);
    }

    private static void AddActionBasedControllerBinds(ActionBasedController controller, string hand)
    {
        controller.enableInputTracking = true;
        controller.positionAction =
            new InputActionProperty(hand == "Left"
                ? Actions.Instance.LeftHandPosition
                : Actions.Instance.RightHandPosition);
        controller.rotationAction =
            new InputActionProperty(hand == "Left"
                ? Actions.Instance.LeftHandRotation
                : Actions.Instance.RightHandRotation);
        controller.trackingStateAction = new InputActionProperty(hand == "Left"
            ? Actions.Instance.LeftHandTrackingState
            : Actions.Instance.RightHandTrackingState);

        controller.enableInputActions = true;
        controller.selectAction = new InputActionProperty(AssetCollection.DefaultXRActions.FindAction($"{hand}/Select"));
        controller.selectActionValue =
            new InputActionProperty(AssetCollection.DefaultXRActions.FindAction($"{hand}/Select Value"));
        controller.activateAction =
            new InputActionProperty(AssetCollection.DefaultXRActions.FindAction($"{hand}/Activate"));
        controller.activateActionValue =
            new InputActionProperty(AssetCollection.DefaultXRActions.FindAction($"{hand}/Activate Value"));
        controller.uiPressAction = new InputActionProperty(AssetCollection.DefaultXRActions.FindAction($"{hand}/UI Press"));
        controller.uiPressActionValue =
            new InputActionProperty(AssetCollection.DefaultXRActions.FindAction($"{hand}/UI Press Value"));
        controller.uiScrollAction =
            new InputActionProperty(AssetCollection.DefaultXRActions.FindAction($"{hand}/UI Scroll"));
        controller.rotateAnchorAction =
            new InputActionProperty(AssetCollection.DefaultXRActions.FindAction($"{hand}/Rotate Anchor"));
        controller.translateAnchorAction =
            new InputActionProperty(AssetCollection.DefaultXRActions.FindAction($"{hand}/Translate Anchor"));
        controller.scaleToggleAction =
            new InputActionProperty(AssetCollection.DefaultXRActions.FindAction($"{hand}/Scale Toggle"));
        controller.scaleDeltaAction =
            new InputActionProperty(AssetCollection.DefaultXRActions.FindAction($"{hand}/Scale Delta"));
    }

    private enum ActiveController
    {
        Left,
        Right
    }
}