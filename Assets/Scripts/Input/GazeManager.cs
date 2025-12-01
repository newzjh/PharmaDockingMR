// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.


using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.XR;

using UnityEngine.XR.Interaction.Toolkit.UI;
using MixedReality.Toolkit.Input;

namespace UnifiedInput
{
    /// <summary>
    /// GazeManager determines the location of the user's gaze, hit position and normals.
    /// </summary>
    public partial class GazeManager : Singleton<GazeManager>
    {

        [Tooltip("Maximum gaze distance, in meters, for calculating a hit.")]
        public float MaxGazeDistance = 15.0f;

        [Tooltip("Select the layers raycast should target.")]
        public LayerMask RaycastLayerMask = Physics.DefaultRaycastLayers;


        public PickResult current;
        public PickResult last;

        [Tooltip("Checking enables SetFocusPointForFrame to set the stabilization plane.")]
        public bool SetStabilizationPlane = true;
        [Tooltip("Lerp speed when moving focus point closer.")]
        public float LerpStabilizationPlanePowerCloser = 4.0f;
        [Tooltip("Lerp speed when moving focus point farther away.")]
        public float LerpStabilizationPlanePowerFarther = 7.0f;

        private float lastHitDistance = 15.0f;
        private static Vector3 _GazeWorldPosition = new Vector3();
        
        public MRTKRayInteractor LeftRayInteractor = null;
        public MRTKRayInteractor RightRayInteractor = null;

        public void Start()
        {
            GameObject goXRRig = GameObject.Find("XR Rig");
            if (goXRRig==null)
                goXRRig = GameObject.Find("MRTK XR Rig");
            if (goXRRig)
            {
                var comhands= goXRRig.GetComponentsInChildren<HandModel>(true);
                foreach(var comhand in comhands)
                {
                    if (comhand.name.Contains("LeftHand Controller"))
                    {
                        LeftRayInteractor = comhand.GetComponentInChildren<MRTKRayInteractor>(true);
                    }
                    else if (comhand.name.Contains("RightHand Controller"))
                    {
                        RightRayInteractor = comhand.GetComponentInChildren<MRTKRayInteractor>(true);
                    }
                }
            }

            RaycastLayerMask = 1 << LayerMask.NameToLayer("Default");
            LogTrace.Trace.TraceLn(this.GetType() + " Start success!");
            string cameramsg = "campos="+Camera.main.transform.position.ToString()+",camfov="+Camera.main.fieldOfView.ToString();
            LogTrace.Trace.TraceLn(cameramsg);
        }

        public static Vector3 GazeWorldPosition
        {
            get
            {
                return _GazeWorldPosition;
            }
        }

        public static Vector2 GazeScreenPoint
        {
            get
            {
                Vector2 ret = new Vector2(Screen.width*0.5f, Screen.height*0.5f);
                //if (Instance != null)
                //{
                //    if (Instance.LeftRayInteractor || Instance.RightRayInteractor)
                //    {
                //        ret = Camera.main.WorldToScreenPoint(_GazeWorldPosition);
                //    }
                //}
                return ret;
            }
        }

        private static List<RaycastResult> templist = new List<RaycastResult>();
        private static int UILayer = -1;

        //方法二 通过UI事件发射射线  
        //是 2D UI 的位置，非 3D 位置  
        public static bool RaycaseUIObject(Vector2 screenPosition,ref List<RaycastResult> results)
        {
            //实例化点击事件  
            PointerEventData eventDataCurrentPosition = new PointerEventData(UnityEngine.EventSystems.EventSystem.current);
            //将点击位置的屏幕坐标赋值给点击事件  
            eventDataCurrentPosition.position = screenPosition;

            //向点击处发射射线  
            if (EventSystem.current)
                EventSystem.current.RaycastAll(eventDataCurrentPosition, templist);

            if (UILayer == -1)
                UILayer = LayerMask.NameToLayer("UI");

            results.Clear();
            foreach(var ret in templist)
            {
                if (ret.gameObject.GetComponent<Canvas>()==null && ret.gameObject.layer == UILayer)
                    results.Add(ret);
            }

            return results.Count > 0;
        }

        public static bool RaycaseUIObject(Canvas canvas, Vector2 screenPosition, ref List<RaycastResult> results)
        {
            //实例化点击事件  
            PointerEventData eventDataCurrentPosition = new PointerEventData(EventSystem.current);
            //将点击位置的屏幕坐标赋值给点击事件  
            eventDataCurrentPosition.position = screenPosition;
            //获取画布上的 GraphicRaycaster 组件  
            GraphicRaycaster uiRaycaster = canvas.gameObject.GetComponent<GraphicRaycaster>();

            // GraphicRaycaster 发射射线  
            uiRaycaster.Raycast(eventDataCurrentPosition, results);

            return results.Count > 0;
        }

        public void UpdateEx()
        {
            UpdateRaycast();
            UpdateStabilizationPlane();
        }

        public PickResult Pick(float maxdistance, int layer)
        {
            PickResult ret;
            ret.Hit = false;
            ret.OnUI = false;
            ret.Position = default;
            ret.Normal = default;
            ret.FocusedObject = null;
            ret.UIRaycast = new RaycastResult();

            Ray ray = default;
            Vector2 screenpt = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            if (LeftRayInteractor && LeftRayInteractor.isActiveAndEnabled && LeftRayInteractor.isHoverActive && UnifiedInputManager.bGazeMode)
            {
                Vector3[] linePoints = new Vector3[64];
                int numPoints = 0;

                if (LeftRayInteractor.GetLinePoints(ref linePoints, out numPoints))
                {
                    if (numPoints >= 2)
                    {
                        Vector3 start = linePoints[0];
                        Vector3 end = linePoints[numPoints - 1];
                        Vector3 dir = (end - start).normalized;
                        ray = new Ray(start, dir);
                        Transform camtran = Camera.main.transform;
                        Plane p = new Plane(camtran.forward, camtran.position + camtran.forward * 2.0f);
                        if (p.Raycast(ray, out var d))
                            end = start + dir * d;
                        screenpt = Camera.main.WorldToScreenPoint(end);
                    }
                    else
                    {
                        Vector3 start = LeftRayInteractor.transform.position;
                        Vector3 dir = LeftRayInteractor.transform.forward;
                        Vector3 end = start + dir * LeftRayInteractor.endPointDistance;
                        ray = new Ray(start, dir);
                        Transform camtran = Camera.main.transform;
                        Plane p = new Plane(camtran.forward, camtran.position + camtran.forward * 2.0f);
                        if (p.Raycast(ray, out var d))
                            end = start + dir * d;
                        screenpt = Camera.main.WorldToScreenPoint(end);
                    }
                }
                else
                {
                    Vector3 start = LeftRayInteractor.transform.position;
                    Vector3 dir = LeftRayInteractor.transform.forward;
                    Vector3 end = start + dir * LeftRayInteractor.endPointDistance;
                    ray = new Ray(start, dir);
                    Transform camtran = Camera.main.transform;
                    Plane p = new Plane(camtran.forward, camtran.position + camtran.forward * 2.0f);
                    if (p.Raycast(ray, out var d))
                        end = start + dir * d;
                    screenpt = Camera.main.WorldToScreenPoint(end);
                }
            }
            else if (RightRayInteractor && RightRayInteractor.isActiveAndEnabled && RightRayInteractor.isHoverActive && UnifiedInputManager.bGazeMode)
            {
                Vector3[] linePoints = new Vector3[64];
                int numPoints = 0;

                if (RightRayInteractor.GetLinePoints(ref linePoints, out numPoints))
                {
                    if (numPoints >= 2)
                    {
                        Vector3 start = linePoints[0];
                        Vector3 end = linePoints[numPoints - 1];
                        Vector3 dir = (end - start).normalized;
                        ray = new Ray(start, dir);
                        Transform camtran = Camera.main.transform;
                        Plane p = new Plane(camtran.forward, camtran.position + camtran.forward * 2.0f);
                        if (p.Raycast(ray, out var d))
                            end = start + dir * d;
                        screenpt = Camera.main.WorldToScreenPoint(end);
                    }
                    else
                    {
                        Vector3 start = RightRayInteractor.transform.position;
                        Vector3 dir = RightRayInteractor.transform.forward;
                        Vector3 end = start + dir * RightRayInteractor.endPointDistance;
                        ray = new Ray(start, dir);
                        Transform camtran = Camera.main.transform;
                        Plane p = new Plane(camtran.forward, camtran.position + camtran.forward * 2.0f);
                        if (p.Raycast(ray, out var d))
                            end = start + dir * d;
                        screenpt = Camera.main.WorldToScreenPoint(end);
                    }
                }
                else
                {
                    Vector3 start = RightRayInteractor.transform.position;
                    Vector3 dir = RightRayInteractor.transform.forward;
                    Vector3 end = start + dir * RightRayInteractor.endPointDistance;
                    ray = new Ray(start, dir);
                    Transform camtran = Camera.main.transform;
                    Plane p = new Plane(camtran.forward, camtran.position + camtran.forward * 2.0f);
                    if (p.Raycast(ray, out var d))
                        end = start + dir * d;
                    screenpt = Camera.main.WorldToScreenPoint(end);
                }
            }
            else
            {
                screenpt = UnifiedInputManager.mousePosition;
            }

            ray = Camera.main.ScreenPointToRay(screenpt);

            List<RaycastResult> uihitresults = new List<RaycastResult>();
            if (RaycaseUIObject(screenpt, ref uihitresults))
            {
                ret.Position = uihitresults[0].worldPosition;
                ret.Normal = uihitresults[0].worldNormal;  //The normal of the surface the ray hit.
                ret.FocusedObject = uihitresults[0].gameObject;
                ret.Hit = true;
                ret.OnUI = true;
                ret.UIRaycast = uihitresults[0];
            }
            else if (ret.Hit) //already have result
            {
                
            }
            else
            {
                // Get the raycast hit information from Unity's physics system.
                RaycastHit hitInfo;
                if (Physics.Raycast(ray, out hitInfo, maxdistance, layer))
                {
                    // If the raycast hits a hologram, set the position and normal to match the intersection point.
                    ret.Position = hitInfo.point;
                    ret.Normal = hitInfo.normal;
                    lastHitDistance = hitInfo.distance;
                    ret.FocusedObject = hitInfo.collider.gameObject;
                    ret.Hit = true;
                }
                else
                {
                    //if (UnifiedInputManager.bGazeMode)
                    //{
                    //    // If the raycast does not hit a hologram, default the position to last hit distance in front of the user,
                    //    // and the normal to face the user.
                    //    Vector3 gazeOrigin = Camera.main.transform.position;
                    //    Vector3 gazeDirection = Camera.main.transform.forward;
                    //    ret.Position = gazeOrigin + (gazeDirection * lastHitDistance);
                    //    ret.Normal = -gazeDirection;   //The normal of the surface the ray hit.
                    //    ret.FocusedObject = null;
                    //}
                    //else
                    {
                        // If the raycast does not hit a hologram, default the position to last hit distance in front of the user,
                        // and the normal to face the user.
                        Vector3 gazeOrigin = ray.origin;
                        Vector3 gazeDirection = ray.direction;
                        ret.Position = gazeOrigin + (gazeDirection * lastHitDistance);
                        ret.Normal = -gazeDirection;   //The normal of the surface the ray hit.
                        ret.FocusedObject = null;
                    }
                }
            }
            return ret;
        }


        /// <summary>
        /// Calculates the Raycast hit position and normal.
        /// </summary>
        private void UpdateRaycast()
        {
            last = current;

            current = Pick(MaxGazeDistance, RaycastLayerMask);
            _GazeWorldPosition = current.Position;

            // Check if the currently hit object has changed
            if (last.FocusedObject != current.FocusedObject)
            {
                if (last.FocusedObject != null)
                {
                    last.FocusedObject.SendMessage("OnGazeLeave", SendMessageOptions.DontRequireReceiver);
                }
                if (current.FocusedObject != null)
                {
                    current.FocusedObject.SendMessage("OnGazeEnter", SendMessageOptions.DontRequireReceiver);
                }

                //if (last.OnUI)
                //{
                //    GameObject go = VRMessager.GetSelectableUIObj(last.FocusedObject);
                //    VRMessager.OnLeaveUIObj(go);
                //}

                //if (current.OnUI)
                //{
                //    GameObject go = VRMessager.GetSelectableUIObj(current.FocusedObject);
                //    VRMessager.OnEnterUIObj(go);
                //}
            }
        }

        /// <summary>
        /// Adds the stabilization plane modifier if it's enabled and if it doesn't exist yet.
        /// </summary>
        private void UpdateStabilizationPlane()
        {
            // We want to use the stabilization logic.
            if (SetStabilizationPlane)
            {
                // Check if it exists in the scene.
                if (StabilizationPlaneModifier.Instance == null)
                {
                    // If not, add it to us.
                    gameObject.AddComponent<StabilizationPlaneModifier>();
                }
            }

            if (StabilizationPlaneModifier.Instance)
            {
                StabilizationPlaneModifier.Instance.SetStabilizationPlane = SetStabilizationPlane;
            }
        }
    }
}