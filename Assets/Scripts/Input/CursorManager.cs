// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace UnifiedInput
{

    /// <summary>
    /// CursorManager class takes Cursor GameObjects.
    /// One that is on Holograms and another off Holograms.
    /// 1. Shows the appropriate Cursor when a Hologram is hit.
    /// 2. Places the appropriate Cursor at the hit position.
    /// 3. Matches the Cursor normal to the hit surface.
    /// </summary>
    public partial class CursorManager : Singleton<CursorManager>
    {
        [Tooltip("Drag the Cursor object to show when it hits a hologram.")]
        public GameObject CursorOnHolograms;

        [Tooltip("Drag the Cursor object to show when it does not hit a hologram.")]
        public GameObject CursorOffHolograms;

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

            if (CursorOnHolograms == null || CursorOffHolograms == null)
            {
                return;
            }

            // Hide the Cursors to begin with.
            CursorOnHolograms.SetActive(false);
            CursorOffHolograms.SetActive(false);

            LogTrace.Trace.TraceLn(this.GetType() + " Start success!");
        }

        void LateUpdate()
        {
            if (GazeManager.Instance == null || CursorOnHolograms == null || CursorOffHolograms == null)
            {
                return;
            }

            if (GazeManager.Instance.current.Hit)
            {
                CursorOnHolograms.SetActive(true);
                CursorOffHolograms.SetActive(false);
            }
            else
            {
                CursorOffHolograms.SetActive(true);
                CursorOnHolograms.SetActive(false);
            }

            //// Place the cursor at the calculated position.
            //this.gameObject.transform.position = GazeManager.Instance.current.Position + GazeManager.Instance.current.Normal * DistanceFromCollision;

            //// Orient the cursor to match the surface being gazed at.
            //gameObject.transform.up = GazeManager.Instance.current.Normal;

            Ray gazeRay = Camera.main.ScreenPointToRay(UnifiedInputManager.mousePosition);
            this.gameObject.transform.position = gazeRay.origin + gazeRay.direction * 2.0f;

            // Reorient the cursor to match the hit object normal.
            this.gameObject.transform.up = GazeManager.Instance.current.Normal;
            this.gameObject.transform.rotation *= cursorDefaultRotation;
        }
    }

}