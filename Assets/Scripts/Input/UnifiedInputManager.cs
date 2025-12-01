using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;
using MixedReality.Toolkit.Input;

namespace UnifiedInput
{
    public enum OperationCode
    {
        Menu=0,
        Escape,
        ZoomIn,
        ZoomOut,
        Left,
        Right,
        Up,
        Down,
        Max
    }

    public struct PickResult
    {
        public bool Hit;
        public bool OnUI;
        /// <summary>
        /// Position of the intersection of the user's gaze and the holograms in the scene.
        /// </summary>
        public Vector3 Position;

        /// <summary>
        /// RaycastHit Normal direction.
        /// </summary>
        public Vector3 Normal;

        /// <summary>
        /// Object currently being focused on.
        /// </summary>
        public GameObject FocusedObject;

        public RaycastResult UIRaycast;
    }

    public class UnifiedInputManager : Singleton<UnifiedInputManager>
    {
        public static bool bGazeMode = false;
        private static bool[] bPressing = new bool[3];
        private static bool[] bDown = new bool[3];
        private static bool[] bUp = new bool[3];

        public static bool OnUI;
        public static GameObject CurObj;
        public static RaycastResult CurUIRaycast;

        private static bool[] bOperations = new bool[(int)OperationCode.Max];

        private GameObject goXRRig;
        private GameObject goXRDeviceSimulator;
        public HandModel LeftHandController = null;
        public HandModel RightHandController = null;
        private void Start()
        {
            goXRDeviceSimulator = GameObject.Find("MRTKInputSimulator");

            goXRRig = GameObject.Find("XR Rig");
            if (goXRRig == null)
                goXRRig = GameObject.Find("MRTK XR Rig");

            if (goXRRig)
            {
                var comhands = goXRRig.GetComponentsInChildren<HandModel>();
                foreach (var comhand in comhands)
                {
                    if (comhand.name.Contains("LeftHand Controller"))
                    {
                        LeftHandController = comhand;
                    }
                    else if (comhand.name.Contains("RightHand Controller"))
                    {
                        RightHandController = comhand;
                    }
                }
            }

            if (KeywordManager.Instance)
            {
                KeywordManager.Instance.AddCommand("Select", null);
                KeywordManager.Instance.AddCommand("Click", null);
                KeywordManager.Instance.AddCommand("Small", null);
                KeywordManager.Instance.AddCommand("Big", null);
                KeywordManager.Instance.AddCommand("Zoom Out", null);
                KeywordManager.Instance.AddCommand("Zoom In", null);
                KeywordManager.Instance.AddCommand("Left", null);
                KeywordManager.Instance.AddCommand("Right", null);
                KeywordManager.Instance.AddCommand("Down", null);
                KeywordManager.Instance.AddCommand("Up", null);
                KeywordManager.Instance.AddCommand("Menu", null);
                KeywordManager.Instance.AddCommand("Escape", null);
                KeywordManager.Instance.AddCommand("Exit", null);
            }

            //#if UNITY_WSA
            //            bGazeMode=true;
            //#else
            //            if (Application.platform == RuntimePlatform.Android)
            //            {
            //                if (SystemInfo.deviceName.ToLower().Contains("oculus"))
            //                    bGazeMode = true;
            //            }

            //#endif
            bGazeMode = true;

            //bGazeMode = false;
            //if (Application.platform == RuntimePlatform.WSAPlayerX64 ||
            //           Application.platform == RuntimePlatform.WSAPlayerX86 ||
            //           Application.platform == RuntimePlatform.WSAPlayerARM)
            //{
            //    bGazeMode = true;
            //}

            RefreshInputModule();
            RefreshGazeMode();

        }



        public void RefreshInputModule()
        {
//            UnifiedInputModule uim = EventSystem.current.GetComponent<UnifiedInputModule>();
//            XRUIInputModule xruiim = EventSystem.current.GetComponent<XRUIInputModule>();
//#if ENABLE_INPUT_SYSTEM
//            if (uim != null) uim.enabled = false;
//            if (xruiim != null) xruiim.enabled = true;
//#else
//            if (uim != null) uim.enabled = true;
//            if (xruiim != null) xruiim.enabled = false;
//#endif        
        }

        private void RefreshGazeMode()
        {
            //goXRDeviceSimulator.SetActive(bGazeMode);
            //LeftHandController.gameObject.SetActive(bGazeMode);
            //RightHandController.gameObject.SetActive(bGazeMode);
            //TrackedPoseDriver tpd = Camera.main.GetComponent<TrackedPoseDriver>();
            //if (tpd)
            //    tpd.enabled = bGazeMode;
        }

        public static bool XRSimulatorEnabled
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                if (!Instance.goXRDeviceSimulator)
                    return false;
                return Instance.goXRDeviceSimulator.activeSelf;
#else
                return false;
#endif
            }
        }


        public static bool MouseEnable
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                if (XRSimulatorEnabled)
                    return false;
                if (TouchManager.TouchSimulationEnabled)
                    return false;
                return Mouse.current != null && Mouse.current.enabled;
#else
                return true;
#endif
            }
        }

        public static bool KeyboardEnable
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                if (XRSimulatorEnabled)
                    return false;
                if (TouchManager.TouchSimulationEnabled)
                    return false;
                return Keyboard.current != null && Keyboard.current.enabled;
#else
                return true;
#endif
            }
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            //if (KeyboardEnable)
            if (Keyboard.current!=null)
            {
                if (Keyboard.current.tabKey.wasPressedThisFrame)
                {
                    bGazeMode = !bGazeMode;
                    RefreshGazeMode();
                }
            }
#else
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                bGazeMode = !bGazeMode;
            }
#endif

            FetchInputSystem();
            GazeManager.Instance.UpdateEx();
    
        }

        private static Vector3 _curMousePosition = new Vector3();
        private static Vector3 _lastMousePosition = new Vector3();
        private static Vector3 _downMousePosition = new Vector3();
        private static Vector3 _upMousePosition = new Vector3();
        //
        // 摘要:
        //     The current mouse position in pixel coordinates. (Read Only)
        public static Vector3 mousePosition 
        {
            get
            {
                if (bGazeMode)
                {
                    return GazeManager.GazeScreenPoint;
                }
                else
                {
#if ENABLE_INPUT_SYSTEM
                    return _curMousePosition;
#else
                    return Input.mousePosition;
#endif
                }
            }
        }

        public static bool isMouseInScreen
        {
            get
            {
                Vector2 pt = mousePosition;
                return pt.x >= 0 && pt.x <= Screen.width && pt.y >= 0 && pt.y <= Screen.height;
            }
        }

        private static KeyControl findKeyControl(KeyCode key)
        {
            return null;
        }

        public static bool GetKeyDown(KeyCode key)
        {
            bool ret = false;
#if ENABLE_INPUT_SYSTEM
            if (KeyboardEnable)
            {
                KeyControl keycontrol = Keyboard.current.TryGetChildControl(key.ToString()) as KeyControl;
                if (keycontrol == null)
                    keycontrol = findKeyControl(key);
                if (keycontrol != null)
                    ret = keycontrol.wasPressedThisFrame;
            }
#else
            ret = Input.GetKeyDown(key);
#endif
            return ret;
        }

        public static bool GetKeyUp(KeyCode key)
        {
            bool ret = false;
#if ENABLE_INPUT_SYSTEM
            if (KeyboardEnable)
            {
                KeyControl keycontrol = Keyboard.current.TryGetChildControl(key.ToString()) as KeyControl;
                if (keycontrol == null)
                    keycontrol = findKeyControl(key);
                if (keycontrol != null)
                    ret = keycontrol.wasReleasedThisFrame;
            }
#else
            ret = Input.GetKeyUp(key);
#endif
            return ret;
        }

        public static bool GetKey(KeyCode key)
        {
            bool ret = false;
#if ENABLE_INPUT_SYSTEM
            if (KeyboardEnable)
            {
                KeyControl keycontrol = Keyboard.current.TryGetChildControl(key.ToString()) as KeyControl;
                if (keycontrol == null)
                    keycontrol = findKeyControl(key);
                if (keycontrol != null)
                    ret = keycontrol.isPressed;
            }
#else
            ret = Input.GetKey(key);
#endif
            return ret;
        }

        public static bool GetButton(string buttonName)
        {
            bool ret = false;
#if ENABLE_INPUT_SYSTEM
            if (MouseEnable)
            {
                ButtonControl button = Mouse.current.TryGetChildControl(buttonName) as ButtonControl;
                if (button != null)
                    ret = button.isPressed;
            }
#else
            ret= Input.GetButton(buttonName);
#endif
            return ret;
        }

        public static bool GetButtonDown(string buttonName)
        {
            bool ret = false;
#if ENABLE_INPUT_SYSTEM
            if (MouseEnable)
            {
                ButtonControl button = Mouse.current.TryGetChildControl(buttonName) as ButtonControl;
                if (button != null)
                    ret = button.wasPressedThisFrame;
            }
#else
            ret = Input.GetButtonDown(buttonName);
#endif
            if (!ret)
            {
                if (buttonName == "Horizontal")
                    ret = bOperations[(int)OperationCode.Left] || bOperations[(int)OperationCode.Right];
                else if (buttonName == "Vertical")
                    ret = bOperations[(int)OperationCode.Down] || bOperations[(int)OperationCode.Up];
                else if (buttonName == "Cancel")
                    ret = bOperations[(int)OperationCode.Escape];
            }
            return ret;
        }

        public static bool GetButtonUp(string buttonName)
        {
            bool ret = false;
#if ENABLE_INPUT_SYSTEM
            if (MouseEnable)
            {
                ButtonControl button = Mouse.current.TryGetChildControl(buttonName) as ButtonControl;
                if (button != null)
                    ret = button.wasReleasedThisFrame;
            }
#else
            ret = Input.GetButtonUp(buttonName);
#endif
            if (!ret)
            {
                if (buttonName == "Horizontal")
                    ret = bOperations[(int)OperationCode.Left] || bOperations[(int)OperationCode.Right];
                else if (buttonName == "Vertical")
                    ret = bOperations[(int)OperationCode.Down] || bOperations[(int)OperationCode.Up];
                else if (buttonName == "Cancel")
                    ret = bOperations[(int)OperationCode.Escape];
            }
            return ret;
        }

        public static float GetAxisRaw(string axisName)
        {
            float ret = 0.0f;
#if ENABLE_INPUT_SYSTEM
            if (MouseEnable)
            {
                Vector2 vec1 = Mouse.current.delta.ReadValue();
                Vector2 vec2 = Mouse.current.scroll.ReadValue();
                if (axisName == "Mouse X")
                    ret = vec1.x;
                else if (axisName == "Mouse Y")
                    ret = vec1.y;
                else if (axisName == "Mouse ScrollWheel")
                    ret = vec2.y;
            }
#else
            ret = Input.GetAxisRaw(axisName);
#endif

            if (axisName == "Horizontal")
            {
                if (bOperations[(int)OperationCode.Left])
                    ret = -1f;
                if (bOperations[(int)OperationCode.Right])
                    ret = 1f;
            }
            else if (axisName == "Vertical")
            {
                if (bOperations[(int)OperationCode.Down])
                    ret = -1f;
                if (bOperations[(int)OperationCode.Up])
                    ret = 1f;
            }

            return ret;
        }

        public static float GetAxis(string axisName)
        {
            float ret = 0.0f;
#if ENABLE_INPUT_SYSTEM
            if (MouseEnable)
            {
                Vector2 vec1 = Mouse.current.delta.ReadValue() * 0.05f;
                Vector2 vec2 = Mouse.current.scroll.ReadValue() * 0.005f;
                if (axisName == "Mouse X")
                    ret = vec1.x;
                else if (axisName == "Mouse Y")
                    ret = vec1.y;
                else if (axisName == "Mouse ScrollWheel")
                    ret = vec2.y;
            }
#else
            ret = Input.GetAxis(axisName);
#endif

            if (axisName == "Horizontal")
            {
                if (bOperations[(int)OperationCode.Left])
                    ret = -1f;
                if (bOperations[(int)OperationCode.Right])
                    ret = 1f;
            }
            else if (axisName == "Vertical")
            {
                if (bOperations[(int)OperationCode.Down])
                    ret = -1f;
                if (bOperations[(int)OperationCode.Up])
                    ret = 1f;
            }
            else if (axisName == "Mouse ScrollWheel")
            {
                if (bOperations[(int)OperationCode.ZoomIn])
                    ret = -0.25f;
                if (bOperations[(int)OperationCode.ZoomOut])
                    ret = 0.25f;
            }

            return ret;
        }


        private static Vector3 lastmousepos=Vector3.zero;

        private void FetchInputSystem()
        {
            _lastMousePosition = mousePosition;

#if ENABLE_INPUT_SYSTEM
            if (MouseEnable) //up by mouse first
                _curMousePosition = Mouse.current.position.ReadValue();
            else if (TouchManager.TouchEnable && TouchManager.TouchCount > 0) //then up by touch
                _curMousePosition = TouchManager.screenPosition;
#endif

            for (int k = 0; k < 3; k++)
            {
                bDown[k] = false;
                bUp[k] = false;
                bPressing[k] = false;
            }

            if (bGazeMode)
            {
                if (LeftHandController && LeftHandController.isActiveAndEnabled)
                {
                    if (UnityEngine.InputSystem.XR.XRController.leftHand!=null && UnityEngine.InputSystem.XR.XRController.leftHand.IsPressed())
                    {
                        bDown[0] = bUp[0] = true;
                        bDown[1] = bUp[1] = true;
                    }
                    var refobj = LeftHandController.SelectInput.GetObjectReference() as PinchInputReader;
                    if (refobj && refobj.SelectAction.action.triggered)
                        bDown[0] = bUp[0] = true;
                    if (refobj && refobj.SelectAction.action.triggered)
                        bDown[1] = bUp[1] = true;
                }
                if (RightHandController && RightHandController.isActiveAndEnabled)
                {
                    if (UnityEngine.InputSystem.XR.XRController.rightHand !=null && UnityEngine.InputSystem.XR.XRController.rightHand.IsPressed())
                    {
                        bDown[0] = bUp[0] = true;
                        bDown[1] = bUp[1] = true;
                    }
                    var refobj = RightHandController.SelectInput.GetObjectReference() as PinchInputReader;
                    if (refobj && refobj.SelectAction.action.triggered)
                        bDown[0] = bUp[0] = true;
                    if (refobj && refobj.SelectAction.action.triggered)
                        bDown[1] = bUp[1] = true;
                }
                if (Input.GetKeyDown(KeyCode.JoystickButton1) || Input.GetKeyDown(KeyCode.JoystickButton2))
                {
                    bDown[0] = bUp[0] = true;
                    bDown[1] = bUp[1] = true;
                }
            }
            else
            {
#if ENABLE_INPUT_SYSTEM
                
                if (MouseEnable)
                {
                    bDown[0] = Mouse.current.leftButton.wasPressedThisFrame;
                    bUp[0] = Mouse.current.leftButton.wasReleasedThisFrame;
                    bPressing[0] = Mouse.current.leftButton.isPressed;
                    bDown[1] = Mouse.current.rightButton.wasPressedThisFrame;
                    bUp[1] = Mouse.current.rightButton.wasReleasedThisFrame;
                    bPressing[1] = Mouse.current.rightButton.isPressed;
                    bDown[2] = Mouse.current.middleButton.wasPressedThisFrame;
                    bUp[2] = Mouse.current.middleButton.wasReleasedThisFrame;
                    bPressing[2] = Mouse.current.middleButton.isPressed;
                }
                else if (TouchManager.TouchEnable && TouchManager.isTap)
                {
                    bDown[0] = true;
                    bUp[0] = true;
                    bPressing[0] = true;
                }
#else
                if (Input.touchCount <= 1)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        bDown[k] = Input.GetMouseButtonDown(k);
                        bUp[k] = Input.GetMouseButtonUp(k);
                        bPressing[k] = Input.GetMouseButton(k);
                    }                
                }
#endif
            }

            Vector3 gestureret = Vector3.zero;
            if (GestureManager.Instance.GetGesture(GestureType.Tap, ref gestureret)||
                KeywordManager.Instance.GetResponse("Select") > 0 ||
                KeywordManager.Instance.GetResponse("Click") > 0)
            {
                bDown[0] = true;
                bUp[0] = true;
                bPressing[0] = true;
            }

            for (int k = 0; k < (int)OperationCode.Max; k++)
                bOperations[k] = false;

            if (KeywordManager.Instance.GetResponse("Zoom In") > 0 || KeywordManager.Instance.GetResponse("Small")>0 ||
                (GetKeyDown(KeyCode.Minus)) || (GetKeyDown(KeyCode.KeypadMinus)))
            {
                bOperations[(int)OperationCode.ZoomIn] = true;
            }
            if (KeywordManager.Instance.GetResponse("Zoom Out") > 0 || KeywordManager.Instance.GetResponse("Big") > 0 ||
                (GetKeyDown(KeyCode.Plus)) || (GetKeyDown(KeyCode.Equals)) || (GetKeyDown(KeyCode.KeypadPlus)))
            {
                bOperations[(int)OperationCode.ZoomOut] = true;
            }
            if (KeywordManager.Instance.GetResponse("Menu") > 0 ||
                (GetKeyDown(KeyCode.Menu)))
            {
                bOperations[(int)OperationCode.Menu] = true;
            }
            if (KeywordManager.Instance.GetResponse("Escape") > 0 || KeywordManager.Instance.GetResponse("Exit") > 0||
                 (GetKeyDown(KeyCode.Home)) || (GetKeyDown(KeyCode.Escape)))
            {
                bOperations[(int)OperationCode.Escape] = true;
            }
            if (KeywordManager.Instance.GetResponse("Left") > 0 )
            {
                bOperations[(int)OperationCode.Left] = true;
            }
            if (KeywordManager.Instance.GetResponse("Right") > 0 )
            {
                bOperations[(int)OperationCode.Right] = true;
            }
            if (KeywordManager.Instance.GetResponse("Up") > 0 )
            {
                bOperations[(int)OperationCode.Up] = true;
            }
            if (KeywordManager.Instance.GetResponse("Down") > 0 )
            {
                bOperations[(int)OperationCode.Down] = true;
            }

            if (bDown[0])
                _downMousePosition = mousePosition;
            if (bUp[0])
                _upMousePosition = mousePosition;

        }

        private void LateUpdate()
        {
            CurObj = null;
            OnUI = false;
            CurUIRaycast = new RaycastResult();
            if (GazeManager.Instance && GazeManager.Instance.current.FocusedObject)
            {
                CurObj = GazeManager.Instance.current.FocusedObject;
                OnUI = GazeManager.Instance.current.OnUI;
                CurUIRaycast = GazeManager.Instance.current.UIRaycast;
            }

            if (bGazeMode)
            {
                if (bUp[0] && _upMousePosition == _downMousePosition && CurUIRaycast.gameObject)
                {
                    var button = CurUIRaycast.gameObject.GetComponentInParent<UnityEngine.UI.Button>();
                    PointerEventData ped = new PointerEventData(EventSystem.current);
                    ped.position = mousePosition;
                    ped.button = PointerEventData.InputButton.Left;
                    ped.clickCount = 1;
                    if (button)
                        button.OnPointerClick(ped);
                }
            }
        }

        public static bool GetMouseButton(int button)
        {
            if (button >= 0 && button <= 2)
                return bPressing[button];
            else
                return false;
        }

        public static bool GetMouseButtonDown(int button)
        {
            if (button >= 0 && button <= 2)
                return bDown[button];
            else
                return false;
        }

        public static bool GetMouseButtonUp(int button)
        {
            if (button >= 0 && button <= 2)
                return bUp[button];
            else
                return false;
        }

        public static bool GetOperation(OperationCode code)
        {
            return bOperations[(int)code];
        }

        public static void ResetGestureMode(bool b)
        {
            GestureManager.Instance.Reset(b);
        }

        public static Vector3 GetManipulation(Vector3 refpos)
        {
            Vector3 ret = Vector3.zero;
            Vector3 gesturedelta = Vector3.zero;
            Camera cam = Camera.main;
            Vector2 curmousepos = mousePosition;

            if (GestureManager.Instance.GetGesture(GestureType.Manipulation, ref gesturedelta))
            {
                ret = gesturedelta * 10.0f;
            }
            else if (TouchManager.Instance.GetGesture(TouchManager.TouchType.Translate, ref gesturedelta))
            {
                Vector3 targetscreenorg = cam.WorldToScreenPoint(refpos);
                Ray ray = cam.ScreenPointToRay(targetscreenorg);
                Vector3 targetworldorg = ray.origin;
                float distance = (targetworldorg - refpos).magnitude;
                Vector3 curworldorg = cam.ScreenToWorldPoint(new Vector3(curmousepos.x, curmousepos.y, targetscreenorg.z));
                Vector3 lastworldorg = cam.ScreenToWorldPoint(new Vector3(curmousepos.x - gesturedelta.x, curmousepos.y - gesturedelta.y, targetscreenorg.z));
                Vector3 curworldfinal = curworldorg + ray.direction * distance;
                Vector3 lastworldfinal = lastworldorg + ray.direction * distance;
                Vector3 inputdelta = curworldfinal - lastworldfinal;
                ret = inputdelta * 1.5f;
            }
#if ENABLE_INPUT_SYSTEM
            else if (Instance.RightHandController && Instance.RightHandController.isActiveAndEnabled)
            {
                Vector2 axis = Instance.RightHandController.SelectInput.inputActionValue.ReadValue<Vector2>();
                ret = axis * 0.01f;
            }
            else if (MouseEnable)
            { 
                if (GetMouseButton(2) && isMouseInScreen)
                {
                    if (GetMouseButtonDown(2))
                        _lastMousePosition = curmousepos;
                    Vector3 targetscreenorg = cam.WorldToScreenPoint(refpos);
                    Ray ray = cam.ScreenPointToRay(targetscreenorg);
                    Vector3 targetworldorg = ray.origin;
                    float distance = (targetworldorg - refpos).magnitude;
                    Vector3 curworldorg = cam.ScreenToWorldPoint(new Vector3(curmousepos.x, curmousepos.y, targetscreenorg.z));
                    Vector3 lastworldorg = cam.ScreenToWorldPoint(new Vector3(lastmousepos.x, lastmousepos.y, targetscreenorg.z));
                    Vector3 curworldfinal = curworldorg + ray.direction * distance;
                    Vector3 lastworldfinal = lastworldorg + ray.direction * distance;
                    Vector3 inputdelta = curworldfinal - lastworldfinal;
                    ret = inputdelta * 1.0f;
                }
                else
                {
                    float mousex = GetAxis("Horizontal");
                    float mousey = GetAxis("Vertical");
                    float mousez = GetAxis("Depth");
                    Transform tcam = Camera.main.transform;
                    mousex *= 10.0f;
                    mousey *= 10.0f;
                    mousez *= 10.0f;
                    ret = mousex * tcam.right + mousey * tcam.up + mousez * tcam.forward;
                    ret *= Time.deltaTime;

                }
            }

#else
            else if (Input.touchCount == 0)
            {
                if (Input.GetMouseButton(2))
                {
                    if (Input.GetMouseButtonDown(2))
                        lastmousepos = curmousepos;
                    Vector3 targetscreenorg = cam.WorldToScreenPoint(refpos);
                    Ray ray = cam.ScreenPointToRay(targetscreenorg);
                    Vector3 targetworldorg = ray.origin;
                    float distance = (targetworldorg - refpos).magnitude;
                    Vector3 curworldorg = cam.ScreenToWorldPoint(new Vector3(curmousepos.x, curmousepos.y, targetscreenorg.z));
                    Vector3 lastworldorg = cam.ScreenToWorldPoint(new Vector3(lastmousepos.x, lastmousepos.y, targetscreenorg.z));
                    Vector3 curworldfinal = curworldorg + ray.direction * distance;
                    Vector3 lastworldfinal = lastworldorg + ray.direction * distance;
                    Vector3 inputdelta = curworldfinal - lastworldfinal;
                    ret = inputdelta * 1.0f;
                }
                else
                { 
                    float mousex = GetAxis("Horizontal");
                    float mousey = GetAxis("Vertical");
                    float mousez = GetAxis("Depth");
                    Transform tcam = Camera.main.transform;
                    mousex *= 10.0f;
                    mousey *= 10.0f;
                    mousez *= 10.0f;
                    ret = mousex*tcam.right + mousey*tcam.up + mousez*tcam.forward;
                    ret *= Time.deltaTime;

                }
            }
#endif
            return ret;
        }

        public static Vector3 GetNavigation()
        {
            Vector3 ret=Vector3.zero;
            Vector3 gesturedelta = Vector3.zero;
            if (GestureManager.Instance.GetGesture(GestureType.Navigation, ref gesturedelta))
            {
                ret = -gesturedelta;
            }
            else if (TouchManager.Instance.GetGesture(TouchManager.TouchType.Translate, ref gesturedelta))
            {
                ret = gesturedelta * 0.25f;
            }
#if ENABLE_INPUT_SYSTEM
            else if (Instance.RightHandController && Instance.RightHandController.isActiveAndEnabled)
            {
                Vector2 axis = Instance.RightHandController.SelectInput.inputActionValue.ReadValue<Vector2>();
                ret = axis * 0.2f;
            }
            else if (MouseEnable)
            {
                if (GetMouseButton(2) && isMouseInScreen)
                {
                    float mousex = GetAxis("Mouse X");
                    float mousey = GetAxis("Mouse Y");
                    float mousez = GetAxis("Mouse ScrollWheel");
                    ret = new Vector3(mousex, mousey, mousez);
                    ret *= Time.deltaTime * 100.0f;
                }
                else
                {
                    float mousex = GetAxis("Horizontal");
                    float mousey = GetAxis("Vertical");
                    float mousez = GetAxis("Depth");
                    ret = new Vector3(mousex, mousey, mousez);
                    ret *= Time.deltaTime * 100.0f;
                }
            }
#else
            else if (Input.touchCount == 0)
            {
                if (Input.GetMouseButton(2))
                {
                    float mousex = GetAxis("Mouse X");
                    float mousey = GetAxis("Mouse Y");
                    float mousez = GetAxis("Mouse ScrollWheel");
                    ret = new Vector3(mousex, mousey, mousez);
                    ret *= Time.deltaTime * 100.0f;
                }
                else
                {
                    float mousex = GetAxis("Horizontal");
                    float mousey = GetAxis("Vertical");
                    float mousez = GetAxis("Depth");
                    ret = new Vector3(mousex, mousey, mousez);
                    ret *= Time.deltaTime * 100.0f;
                }
            }
#endif
            return ret;
        }

        public static PickResult Pick(int layerselection)
        {
            float maxdistance = 15.0f;
            if (GazeManager.Instance)
            {
                PickResult ret = GazeManager.Instance.Pick(maxdistance, layerselection);
                return ret;
            }

            PickResult failr=new PickResult();
            failr.Hit = false;
            failr.OnUI = false;
            return failr;
        }
    }
}
