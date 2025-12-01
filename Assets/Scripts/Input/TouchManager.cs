using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnifiedInput
{
    public class TouchManager : Singleton<TouchManager>
    {
        void OnEnable()
        {
            UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.Enable();
            //UnityEngine.InputSystem.EnhancedTouch.TouchSimulation.Enable();
        }

        public enum TouchType
        {
            None = 0,
            Tap = 1,
            Scale = 2,
            Rotation = 4,
            Translate = 8,
        }

        private Dictionary<TouchType, Vector3> gesturequeue = new Dictionary<TouchType, Vector3>();
        public bool GetGesture(TouchType type, ref Vector3 ret)
        {
            if (gesturequeue.ContainsKey(type))
            {
                ret = gesturequeue[type];
                return true;
            }
            else
            {
                return false;
            }
        }

        void OnScale(Vector3 delta)
        {
            gesturequeue[TouchType.Scale] = delta;
        }

        void OnRotate(Vector3 delta)
        {
            gesturequeue[TouchType.Rotation] = delta;
        }

        void OnTranslate(Vector3 delta)
        {
            gesturequeue[TouchType.Translate] = delta;
        }

        public static bool TouchSimulationEnabled
        { 
            get
            {
#if ENABLE_INPUT_SYSTEM
                return UnityEngine.InputSystem.EnhancedTouch.TouchSimulation.instance && 
                    UnityEngine.InputSystem.EnhancedTouch.TouchSimulation.instance.enabled;
#else
                return true;
#endif
            }
        }

        public static bool TouchEnable
        {
            get
            {
                if (UnifiedInputManager.XRSimulatorEnabled)
                    return false;
#if ENABLE_INPUT_SYSTEM
                if (!UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.enabled)
                    return false;
                //UnityEngine.InputSystem.EnhancedTouch.Touch.fingers.Count>0
                return true;
#else
                return true;
#endif
            }
        }



        public static int TouchCount
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return UnityEngine.InputSystem.EnhancedTouch.Touch.activeFingers.Count;
#else
                return Input.touchCount;
#endif
            }
        }

        public static Vector3 screenPosition
        {
            get 
            {
#if ENABLE_INPUT_SYSTEM
                var activefingers = UnityEngine.InputSystem.EnhancedTouch.Touch.activeFingers;
                if (activefingers.Count > 0)
                    return activefingers[0].screenPosition;
                else
                {
                    Debug.LogError("Unexcept");
                    return new Vector3();
                }
#else
                return Input.mousePosition;
#endif
            }
        }

        public static bool isTap
        {
            get
            {
                var activefingers = UnityEngine.InputSystem.EnhancedTouch.Touch.activeFingers;
                for (int i = 0; i < activefingers.Count; i++)
                {
                    var finger = activefingers[i];
                    if (finger.currentTouch.isTap)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        private Vector2 lastpos0;
        private Vector2 lastpos1;
        private int touchid0 = -1;
        private int touchid1 = -1;
        private int lasttouchcount = -1;
        private static Vector2 undefinedPosition = new Vector2(-99999.0f, -99999.0f);

        void LateUpdate()
        {
            gesturequeue.Clear();
#if ENABLE_INPUT_SYSTEM
            if (!TouchEnable)
                return;
            var activeTouchs = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;
            int curtouchcount = activeTouchs.Count;
            if (curtouchcount != lasttouchcount)
            {
                if (curtouchcount == 2)
                {
                    var touch0 = activeTouchs[0];
                    var touch1 = activeTouchs[1];
                    Vector2 curpos0 = touch0.screenPosition;
                    Vector2 curpos1 = touch1.screenPosition;
                    lastpos0 = curpos0;
                    lastpos1 = curpos1;
                    touchid0 = touch0.touchId;
                    touchid1 = touch1.touchId;
                }
                else
                {
                    Vector2 curpos0 = undefinedPosition;
                    Vector2 curpos1 = undefinedPosition;
                    lastpos0 = undefinedPosition;
                    lastpos1 = undefinedPosition;
                    touchid0 = -1;
                    touchid1 = -1;
                }
            }

            if (curtouchcount == 2)
            {
                var touch0 = activeTouchs[0];
                var touch1 = activeTouchs[1];
                if (touch0.phase == UnityEngine.InputSystem.TouchPhase.Began || touch1.phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    Vector2 curpos0 = touch0.screenPosition;
                    Vector2 curpos1 = touch1.screenPosition;
                    lastpos0 = curpos0;
                    lastpos1 = curpos1;
                    touchid0 = touch0.touchId;
                    touchid1 = touch1.touchId;
                }
                else if (touch0.phase == UnityEngine.InputSystem.TouchPhase.Ended || touch1.phase == UnityEngine.InputSystem.TouchPhase.Ended ||
                    touch0.phase == UnityEngine.InputSystem.TouchPhase.Canceled || touch1.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                {
                    Vector2 curpos0 = touch0.screenPosition;
                    Vector2 curpos1 = touch1.screenPosition;
                    lastpos0 = curpos0;
                    lastpos1 = curpos1;
                    touchid0 = -1;
                    touchid1 = -1;
                }
                else if (touch0.phase == UnityEngine.InputSystem.TouchPhase.Moved || touch1.phase == UnityEngine.InputSystem.TouchPhase.Moved &&
                    touch0.touchId==touchid0 && touch1.touchId==touchid1)
                {
                    Vector2 curpos0 = touch0.screenPosition;
                    Vector2 curpos1 = touch1.screenPosition;
                    Vector2 delta0 = curpos0 - lastpos0;
                    Vector2 delta1 = curpos1 - lastpos1;
                    Vector2 curvec = curpos1 - curpos0;
                    Vector2 oldvec = lastpos1 - lastpos0;
                    float curlen = curvec.magnitude;
                    float oldlen = oldvec.magnitude;
                    Vector2 towards0 = delta0.normalized;
                    Vector2 towards1 = delta1.normalized;
                    Vector3 olddir = oldvec.normalized;
                    Vector2 curdir = curvec.normalized;

                    float angle1 = Vector2.Angle(olddir, curdir);
                    float angle2 = Vector2.Angle(towards0, towards1);
                    float angle3 = Vector2.Angle(towards0, olddir);
                    float angle4 = Vector2.Angle(towards1, olddir);
                    float deltalen = curlen - oldlen;

                    if (angle2 > 120.0f)
                    {
                        if (angle3 > 30.0f && angle4 > 30.0f)
                        {
                            //if (angle1 > 2.0f)
                            {
                                float zangle = Vector3.Angle(Vector3.Cross(olddir, curdir), Vector3.forward);
                                float finalangle = zangle < 90.0f ? angle1 : -angle1;
                                OnRotate(new Vector3(finalangle, finalangle, finalangle));
                                Debug.Log("rotation=" + finalangle);
                            }
                        }
                        else if (angle1 < 10.0f)
                        {
                            if (Mathf.Abs(deltalen) > 10.0f)
                            {
                                OnScale(new Vector3(deltalen, deltalen, deltalen));
                                Debug.Log("scale=" + deltalen);
                            }
                        }
                    }
                    else if (angle2 < 10.0f)
                    {
                        if (angle1 < 10.0f)
                        {
                            if (delta0.magnitude + delta1.magnitude > 0.1f)
                            {
                                OnTranslate(delta0);
                                Debug.Log("translate=" + delta0 + "," + delta1);
                            }
                        }
                    }

                    lastpos0 = curpos0;
                    lastpos1 = curpos1;
                }
            }

            lasttouchcount = curtouchcount;
#else            
            int curtouchcount = Input.touchCount;
            if (curtouchcount != lasttouchcount)
            {
                if (curtouchcount == 2)
                {
                    Touch touch0 = Input.GetTouch(0);
                    Touch touch1 = Input.GetTouch(1);
                    Vector2 curpos0 = touch0.position;
                    Vector2 curpos1 = touch1.position;
                    lastpos0 = curpos0;
                    lastpos1 = curpos1;
                }
                else
                {
                    Vector2 curpos0 = Input.mousePosition;
                    Vector2 curpos1 = Input.mousePosition;
                    lastpos0 = Input.mousePosition;
                    lastpos1 = Input.mousePosition;
                }
            }

            if (curtouchcount == 2)
            {
                if (Input.GetTouch(0).phase == TouchPhase.Began || Input.GetTouch(1).phase == TouchPhase.Began)
                {
                    Touch touch0 = Input.GetTouch(0);
                    Touch touch1 = Input.GetTouch(1);
                    Vector2 curpos0 = touch0.position;
                    Vector2 curpos1 = touch1.position;
                    lastpos0 = curpos0;
                    lastpos1 = curpos1;
                    Guesting = true;
                }
                else if (Input.GetTouch(0).phase == TouchPhase.Ended || Input.GetTouch(1).phase == TouchPhase.Ended ||
                    Input.GetTouch(0).phase == TouchPhase.Canceled || Input.GetTouch(1).phase == TouchPhase.Canceled)
                {
                    Touch touch0 = Input.GetTouch(0);
                    Touch touch1 = Input.GetTouch(1);
                    Vector2 curpos0 = touch0.position;
                    Vector2 curpos1 = touch1.position;
                    lastpos0 = curpos0;
                    lastpos1 = curpos1;
                    Guesting = false;
                }
                else if (Input.GetTouch(0).phase == TouchPhase.Moved || Input.GetTouch(1).phase == TouchPhase.Moved)
                {
                    Touch touch0 = Input.GetTouch(0);
                    Touch touch1 = Input.GetTouch(1);
                    Vector2 curpos0 = touch0.position;
                    Vector2 curpos1 = touch1.position;
                    Vector2 delta0 = curpos0 - lastpos0;
                    Vector2 delta1 = curpos1 - lastpos1;
                    Vector2 curvec = curpos1 - curpos0;
                    Vector2 oldvec = lastpos1 - lastpos0;
                    float curlen = curvec.magnitude;
                    float oldlen = oldvec.magnitude;
                    Vector2 towards0 = delta0.normalized;
                    Vector2 towards1 = delta1.normalized;
                    Vector3 olddir = oldvec.normalized;
                    Vector2 curdir = curvec.normalized;

                    float angle1 = Vector2.Angle(olddir, curdir);
                    float angle2 = Vector2.Angle(towards0, towards1);
                    float angle3 = Vector2.Angle(towards0, olddir);
                    float angle4 = Vector2.Angle(towards1, olddir);
                    float deltalen = curlen - oldlen;

                    if (angle2 > 120.0f)
                    {
                        if (angle3 > 30.0f && angle4 > 30.0f)
                        {
                            //if (angle1 > 2.0f)
                            {
                                float zangle = Vector3.Angle(Vector3.Cross(olddir, curdir), Vector3.forward);
                                float finalangle = zangle < 90.0f ? angle1 : -angle1;
                                OnRotate(new Vector3(finalangle, finalangle, finalangle));
                                Debug.Log("rotation=" + finalangle);
                            }
                        }
                        else if (angle1 < 10.0f)
                        {
                            if (Mathf.Abs(deltalen) > 10.0f)
                            {
                                OnScale(new Vector3(deltalen, deltalen, deltalen));
                                Debug.Log("scale=" + deltalen);
                            }
                        }
                    }
                    else if (angle2 < 10.0f)
                    {
                        if (angle1 < 10.0f)
                        {
                            if (delta0.magnitude + delta1.magnitude > 0.1f)
                            {
                                OnTranslate(delta0);
                                Debug.Log("translate=" + delta0 + "," + delta1);
                            }
                        }
                    }

                    lastpos0 = curpos0;
                    lastpos1 = curpos1;
                }
            }

            lasttouchcount = curtouchcount;
#endif

        }







    }  
}