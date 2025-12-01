// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_WINRT_8_0 || UNITY_WINRT_8_1 || UNITY_WINRT_10_0
#if UNITY_5_3_OR_NEWER
#define WSA_VR
#endif
#endif

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if WSA_VR
using UnityEngine.VR.WSA.Input;
using HoloToolkit.Unity;
#endif

namespace UnifiedInput
{
    public enum GestureType
    {
        None = 0,
        Tap = 1,
        Hold = 2,
        Navigation=4,
        Manipulation = 8,
    }

    /// <summary>
    /// GestureManager creates a gesture recognizer and signs up for a tap gesture.
    /// When a tap gesture is detected, GestureManager uses GazeManager to find the game object.
    /// GestureManager then sends a message to that game object.
    /// </summary>
    public partial class GestureManager : Singleton<GestureManager>
    {
        private Dictionary<GestureType, Vector3> gesturequeue = new Dictionary<GestureType, Vector3>();

        /// <summary>
        /// Key to press in the editor to select the currently gazed hologram
        /// </summary>
        public KeyCode EditorSelectKey = KeyCode.BackQuote;





        public bool IsNavigating;
        public Vector3 NavigationPosition;
        public bool IsManipulating;
        public Vector3 ManipulationPosition;

        private void OnTap(int tapCount)
        {
            //VRMessager.ProcessEventOnObj(GazeManager.Instance.current, "OnPointerClick");
            gesturequeue[GestureType.Tap] = Vector3.zero;
        }

        private void OnManipulation(Vector3 abspos, Vector3 delta)
        {
            gesturequeue[GestureType.Manipulation] = delta;
        }

        private void OnNavigation(Vector3 delta)
        {
            gesturequeue[GestureType.Navigation] = delta;
        }

        public bool GetGesture(GestureType type, ref Vector3 ret)
        {
            if (gesturequeue.ContainsKey(type))
            {
                ret = gesturequeue[type];
                gesturequeue.Remove(type);
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Revert back to the default GestureRecognizer.
        /// </summary>
        public void Reset(bool bNavigation)
        {
#if WSA_VR
            if (bNavigation)
                Transition(NavigationRecognizer);
            else
                Transition(ManipulationRecognizer);
#endif
        }

#if WSA_VR
        // Tap and Navigation gesture recognizer.
        private GestureRecognizer NavigationRecognizer;

        // Manipulation gesture recognizer.
        private GestureRecognizer ManipulationRecognizer;

        // Currently active gesture recognizer.
        private GestureRecognizer ActiveRecognizer;


        void Start()
        {

            // 2.b: Instantiate the NavigationRecognizer.
            NavigationRecognizer = new GestureRecognizer();

            // 2.b: Add Tap and NavigationX GestureSettings to the NavigationRecognizer's RecognizableGestures.
            NavigationRecognizer.SetRecognizableGestures(
                GestureSettings.Tap |
                GestureSettings.NavigationX |
                GestureSettings.NavigationY |
                GestureSettings.NavigationZ);

            // 2.b: Register for the TappedEvent with the NavigationRecognizer_TappedEvent function.
            NavigationRecognizer.TappedEvent += CommonRecognizer_TappedEvent;
            // 2.b: Register for the NavigationStartedEvent with the NavigationRecognizer_NavigationStartedEvent function.
            NavigationRecognizer.NavigationStartedEvent += NavigationRecognizer_NavigationStartedEvent;
            // 2.b: Register for the NavigationUpdatedEvent with the NavigationRecognizer_NavigationUpdatedEvent function.
            NavigationRecognizer.NavigationUpdatedEvent += NavigationRecognizer_NavigationUpdatedEvent;
            // 2.b: Register for the NavigationCompletedEvent with the NavigationRecognizer_NavigationCompletedEvent function. 
            NavigationRecognizer.NavigationCompletedEvent += NavigationRecognizer_NavigationCompletedEvent;
            // 2.b: Register for the NavigationCanceledEvent with the NavigationRecognizer_NavigationCanceledEvent function. 
            NavigationRecognizer.NavigationCanceledEvent += NavigationRecognizer_NavigationCanceledEvent;

            // Instantiate the ManipulationRecognizer.
            ManipulationRecognizer = new GestureRecognizer();

            // Add the ManipulationTranslate GestureSetting to the ManipulationRecognizer's RecognizableGestures.
            ManipulationRecognizer.SetRecognizableGestures(
                GestureSettings.Tap | GestureSettings.ManipulationTranslate);

            // Register for the Manipulation events on the ManipulationRecognizer.

            ManipulationRecognizer.TappedEvent += CommonRecognizer_TappedEvent;
            ManipulationRecognizer.ManipulationStartedEvent += ManipulationRecognizer_ManipulationStartedEvent;
            ManipulationRecognizer.ManipulationUpdatedEvent += ManipulationRecognizer_ManipulationUpdatedEvent;
            ManipulationRecognizer.ManipulationCompletedEvent += ManipulationRecognizer_ManipulationCompletedEvent;
            ManipulationRecognizer.ManipulationCanceledEvent += ManipulationRecognizer_ManipulationCanceledEvent;

            Reset(false);

            LogTrace.Trace.TraceLn(this.GetType() + " Start success!");
        }

        void OnDestroy()
        {
            // 2.b: Unregister the Tapped and Navigation events on the NavigationRecognizer.
            NavigationRecognizer.TappedEvent -= CommonRecognizer_TappedEvent;
            NavigationRecognizer.NavigationStartedEvent -= NavigationRecognizer_NavigationStartedEvent;
            NavigationRecognizer.NavigationUpdatedEvent -= NavigationRecognizer_NavigationUpdatedEvent;
            NavigationRecognizer.NavigationCompletedEvent -= NavigationRecognizer_NavigationCompletedEvent;
            NavigationRecognizer.NavigationCanceledEvent -= NavigationRecognizer_NavigationCanceledEvent;

            // Unregister the Manipulation events on the ManipulationRecognizer.
            ManipulationRecognizer.TappedEvent -= CommonRecognizer_TappedEvent;
            ManipulationRecognizer.ManipulationStartedEvent -= ManipulationRecognizer_ManipulationStartedEvent;
            ManipulationRecognizer.ManipulationUpdatedEvent -= ManipulationRecognizer_ManipulationUpdatedEvent;
            ManipulationRecognizer.ManipulationCompletedEvent -= ManipulationRecognizer_ManipulationCompletedEvent;
            ManipulationRecognizer.ManipulationCanceledEvent -= ManipulationRecognizer_ManipulationCanceledEvent;
        }



        /// <summary>
        /// Transition to a new GestureRecognizer.
        /// </summary>
        /// <param name="newRecognizer">The GestureRecognizer to transition to.</param>
        private void Transition(GestureRecognizer newRecognizer)
        {
            if (newRecognizer == null)
            {
                return;
            }

            if (ActiveRecognizer != null)
            {
                if (ActiveRecognizer == newRecognizer)
                {
                    return;
                }

                ActiveRecognizer.CancelGestures();
                ActiveRecognizer.StopCapturingGestures();
            }

            newRecognizer.StartCapturingGestures();
            ActiveRecognizer = newRecognizer;
        }

        private void NavigationRecognizer_NavigationStartedEvent(InteractionSourceKind source, Vector3 relativePosition, Ray ray)
        {
            IsNavigating = true;
            NavigationPosition = relativePosition;
        }

        private void NavigationRecognizer_NavigationUpdatedEvent(InteractionSourceKind source, Vector3 relativePosition, Ray ray)
        {
            IsNavigating = true;
            NavigationPosition = relativePosition;
            OnNavigation(relativePosition);
        }

        private void NavigationRecognizer_NavigationCompletedEvent(InteractionSourceKind source, Vector3 relativePosition, Ray ray)
        {
            IsNavigating = false;
        }

        private void NavigationRecognizer_NavigationCanceledEvent(InteractionSourceKind source, Vector3 relativePosition, Ray ray)
        {
            IsNavigating = false;
        }

        private void ManipulationRecognizer_ManipulationStartedEvent(InteractionSourceKind source, Vector3 position, Ray ray)
        {
            IsManipulating = true;
            ManipulationPosition = position;
        }

        private void ManipulationRecognizer_ManipulationUpdatedEvent(InteractionSourceKind source, Vector3 position, Ray ray)
        {
            Vector3 deltapos = position - ManipulationPosition;
            IsManipulating = true;
            ManipulationPosition = position;
            OnManipulation(position, deltapos);
        }

        private void ManipulationRecognizer_ManipulationCompletedEvent(InteractionSourceKind source, Vector3 position, Ray ray)
        {
            IsManipulating = false;
        }

        private void ManipulationRecognizer_ManipulationCanceledEvent(InteractionSourceKind source, Vector3 position, Ray ray)
        {
            IsManipulating = false;
        }

        private void CommonRecognizer_TappedEvent(InteractionSourceKind source, int tapCount, Ray ray)
        {
            OnTap(tapCount);
        }
#endif

        void LateUpdate()
        {
            
            if (UnifiedInputManager.GetKeyDown(EditorSelectKey))
            {
                OnTap(1);
            }
        }





    }
}