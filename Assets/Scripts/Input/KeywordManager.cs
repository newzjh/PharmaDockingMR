// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_WINRT_8_0 || UNITY_WINRT_8_1 || UNITY_WINRT_10_0
#if UNITY_5_3_OR_NEWER
#define WSA_VR
#endif
#endif

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
#if WSA_VR
using UnityEngine.Windows.Speech;
using HoloToolkit.Unity;
#endif
using LogTrace;

namespace UnifiedInput
{
    /// <summary>
    /// KeywordManager allows you to specify keywords and methods in the Unity
    /// Inspector, instead of registering them explicitly in code.
    /// This also includes a setting to either automatically start the
    /// keyword recognizer or allow your code to start it.
    ///
    /// IMPORTANT: Please make sure to add the microphone capability in your app, in Unity under
    /// Edit -> Project Settings -> Player -> Settings for Windows Store -> Publishing Settings -> Capabilities
    /// or in your Visual Studio Package.appxmanifest capabilities.
    /// </summary>
    public partial class KeywordManager : Singleton<KeywordManager>
    {

        // This enumeration gives the manager two different ways to handle the recognizer. Both will
        // set up the recognizer and add all keywords. The first causes the recognizer to start
        // immediately. The second allows the recognizer to be manually started at a later time.
        public enum RecognizerStartBehavior { AutoStart, ManualStart };

        [Tooltip("An enumeration to set whether the recognizer should start on or off.")]
        public RecognizerStartBehavior RecognizerStart;

        private Dictionary<string, UnityEvent> responses = new Dictionary<string, UnityEvent>();
        private Dictionary<string, int> commanddict = new Dictionary<string, int>();

        public int GetResponse(string command)
        {
            if (commanddict.ContainsKey(command))
            {
                int ret = commanddict[command];
                commanddict[command] = 0;
                return ret;
            }
            else
            {
                return 0;
            }
        }

        public void AddCommand(string command, UnityEvent e)
        {
            responses[command] = e;
            commanddict[command] = 0;
        }

        public void RemoveCommand(string command)
        {
            responses.Remove(command);
            commanddict.Remove(command);
        }

        public void OnCommand(string command)
        {
            if (commanddict.ContainsKey(command))
            {
                commanddict[command]++;
            }


        }

#if WSA_VR

        private KeywordRecognizer keywordRecognizer=null;

        void Start()
        {
            if (responses.Count > 0 && !Application.isEditor)
            {
                keywordRecognizer = new KeywordRecognizer(responses.Keys.ToArray());
                keywordRecognizer.OnPhraseRecognized += KeywordRecognizer_OnPhraseRecognized;

                if (RecognizerStart == RecognizerStartBehavior.AutoStart)
                {
                    keywordRecognizer.Start();
                    Trace.TraceLn("KeywordManager.Start success!");
                }
            }
            else
            {
                //Debug.LogError("Must have at least one keyword specified in the Inspector on " + gameObject.name + ".");
                Trace.TraceLn("KeywordManager::Start fail!");
            }
        }

 
        void OnDestroy()
        {
            if (keywordRecognizer != null)
            {
                StopKeywordRecognizer();
                keywordRecognizer.OnPhraseRecognized -= KeywordRecognizer_OnPhraseRecognized;
                keywordRecognizer.Dispose();
                keywordRecognizer = null;
            }
        }


        private void KeywordRecognizer_OnPhraseRecognized(PhraseRecognizedEventArgs args)
        {
            if (args.text == null)
            {
                Debug.LogError("KeywordRecognizer_OnPhraseRecognized: args.text==null");
                return;
            }


            // Check to make sure the recognized keyword exists in the methods dictionary, then invoke the corresponding method.
            if (responses != null)
            {
                UnityEvent keywordResponse;
                if (responses.TryGetValue(args.text, out keywordResponse))
                {
                    if (keywordResponse != null)
                    {
                        keywordResponse.Invoke();
                    }
                }
            }

            if (commanddict != null)
            {
                string command = args.text;
                OnCommand(command);
            }

            Debug.Log("KeywordRecognizer_OnPhraseRecognized:"+args.text);
        }

        /// <summary>
        /// Make sure the keyword recognizer is off, then start it.
        /// Otherwise, leave it alone because it's already in the desired state.
        /// </summary>
        public void StartKeywordRecognizer()
        {
            if (keywordRecognizer != null && !keywordRecognizer.IsRunning)
            {
                keywordRecognizer.Start();
            }
        }

        /// <summary>
        /// Make sure the keyword recognizer is on, then stop it.
        /// Otherwise, leave it alone because it's already in the desired state.
        /// </summary>
        public void StopKeywordRecognizer()
        {
            if (keywordRecognizer != null && keywordRecognizer.IsRunning)
            {
                keywordRecognizer.Stop();
            }
        }

#endif
    }
}