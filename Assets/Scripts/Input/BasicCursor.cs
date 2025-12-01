// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;
using LogTrace;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace UnifiedInput
{
    /// <summary>
    /// 1. Decides when to show the cursor.
    /// 2. Positions the cursor at the gazed location.
    /// 3. Rotates the cursor to match hologram normals.
    /// </summary>
    public class BasicCursor : MonoBehaviour
    {
        [Tooltip("Distance, in meters, to offset the cursor from the collision point.")]
        public float DistanceFromCollision = 0.01f;

        private Quaternion cursorDefaultRotation;



        void Start()
        {
            int thisLayerMask = 1 << this.gameObject.layer;
            if ((GazeManager.Instance.RaycastLayerMask & thisLayerMask) != 0)
            {
                Debug.LogError("The cursor has a layer that is checked in the GazeManager's Raycast Layer Mask.  Change the cursor layer (e.g.: to Ignore Raycast) or uncheck the layer in GazeManager: " +
                    LayerMask.LayerToName(this.gameObject.layer));
            }

            // Hide the Cursor to begin with.
            //meshRenderer.enabled = false;

            // Cache the cursor default rotation so the cursor can be rotated with respect to the original orientation.
            cursorDefaultRotation = this.gameObject.transform.rotation;

        }

        void LateUpdate()
        {
            if (GazeManager.Instance == null)
            {
                return;
            }

            // Show or hide the Cursor based on if the user's gaze hit a hologram.
            //meshRenderer.enabled = GazeManager.Instance.Hit;

            // Place the cursor at the calculated position.
            //this.gameObject.transform.position = GazeManager.Instance.Position + GazeManager.Instance.Normal * DistanceFromCollision;

            //// Reorient the cursor to match the hit object normal.
            //this.gameObject.transform.up = GazeManager.Instance.Normal;
            //this.gameObject.transform.rotation *= cursorDefaultRotation;


            //Ray gazeRay = Camera.main.ScreenPointToRay(UnifiedInputManager.mousePosition);
            //this.gameObject.transform.position = gazeRay.origin + gazeRay.direction * 2.0f;
            Vector3 worldpos = GazeManager.GazeWorldPosition;
            Vector3 vec = (worldpos - Camera.main.transform.position);
            Vector3 dir = vec.normalized;
            //float len = 3.0f;
            //if (len > vec.magnitude-1.0f)
            //    len = 0.5f;
            //this.gameObject.transform.position = worldpos;// - dir * len;

            //// Reorient the cursor to match the hit object normal.
            //this.gameObject.transform.up = GazeManager.Instance.current.Normal;
            //this.gameObject.transform.rotation *= cursorDefaultRotation;


            float len = 0.0001f;
            if (len > vec.magnitude)
                len = 0.0f;
            this.gameObject.transform.position = worldpos - dir * len;

            // Reorient the cursor to match the hit object normal.
            this.gameObject.transform.up = Camera.main.transform.forward;
            this.gameObject.transform.rotation *= cursorDefaultRotation;
        }

        void Update()
        {
            var inputmodule = EventSystem.current.GetComponent<XRUIInputModule>();

            for (int i = 0; i < 3; i++)
            {
                //EventSystem.current.GetComponent<XRUIInputModule>().submitButton!=string.Empty
                if (UnifiedInput.UnifiedInputManager.GetMouseButtonDown(i))
                {
                    if (i < this.transform.childCount)
                    {
                        ParticleSystem ps = this.transform.GetChild(i).GetComponent<ParticleSystem>();
                        if (ps != null)
                            ps.Play();
                    }
                }
            }
        }
    }
}