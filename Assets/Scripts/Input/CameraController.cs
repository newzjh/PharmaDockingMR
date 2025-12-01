using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace UnifiedInput
{
	public class CameraController : MonoBehaviour
	{
        Camera cam;
        private int nControlMode = -1;
        Transform tLeftJoy;
        Transform tRightJoy;

        public void Awake()
        {
            cam = Camera.main;

#if UNITY_WSA
#else
            cam.transform.position = new Vector3(0.3f, 0.0f, 0.0f);
#endif
            tLeftJoy = transform.Find("LeftJoystick");
            tRightJoy = transform.Find("RightJoystick");
        }



        int lastsw = -1;
        int lastsh = -1;


        public void Update()
        {
            int sw = Screen.width;
            int sh = Screen.height;
            if (sw != lastsw || sh != lastsh)
            {
                RefreshResoultion();
                lastsw = sw;
                lastsh = sh;
            }

            int nNewMode = UnifiedInputManager.bGazeMode ? 1 : 0;
            if (nControlMode != nNewMode)
            {
                nControlMode = nNewMode;
                RefreshControlMode();
            }

            if (nControlMode == 0)
            {
                if (UnifiedInputManager.isMouseInScreen)
                {
                    float dx = UnifiedInputManager.GetAxis("Mouse X");
                    float dy = UnifiedInputManager.GetAxis("Mouse Y");
                    float dz = UnifiedInputManager.GetAxis("Mouse ScrollWheel");
                    if (Mathf.Abs(dx) > 0.001f || Mathf.Abs(dy) > 0.001f || Mathf.Abs(dz) > 0.001f)
                    {
                        bool b0 = UnifiedInputManager.GetMouseButton(0);
                        bool b1 = UnifiedInputManager.GetMouseButton(1);
                        bool b2 = UnifiedInputManager.GetMouseButton(2);
                        if (b0 && !b1 && !b2)
                        {
                            Vector3 langle = cam.transform.localEulerAngles;
                            langle.y += dx;
                            langle.x += dy * -1;
                            cam.transform.localEulerAngles = langle;
                            cam.transform.Translate(new Vector3(0, 0, dz * 0.2f), Space.Self);
                        }
                        else if (!b0 && b1 && !b2)
                        {
                            cam.transform.Translate(new Vector3(dx * 0.08f, dy * 0.08f, dz * 0.2f), Space.Self);
                        }
                    }
                }
            }
        }

        private void RefreshResoultion()
        {
            //EasyJoystick[] joys = this.GetComponentsInChildren<EasyJoystick>();
            //if (joys == null)
            //    return;

            //foreach (EasyJoystick joy in joys)
            //{
            //    joy.JoyAnchor = joy.JoyAnchor;
            //}
        }

        private void RefreshControlMode()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform t = transform.GetChild(i);
                t.gameObject.SetActive(nControlMode!=1);
            }

            if (nControlMode == 1)
            {
                if (Application.platform==RuntimePlatform.WSAPlayerX86 || Application.platform == RuntimePlatform.WSAPlayerX64 || Application.platform == RuntimePlatform.WSAPlayerARM)
                    cam.fieldOfView = 34.11f;
                else
                    cam.fieldOfView = 60.0f;
            }
            else
            {
                cam.fieldOfView = 60.0f;
            }
        }
	}
}
