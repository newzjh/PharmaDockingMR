
using System.Collections;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading;

namespace LogTrace
{
	
	// 输出到Html文件
	public class ScreenTraceListener : MonoBehaviour
	{
        public class STraceMsg
        {
            public DateTime time;
            public LogType logtype;
            public string content;
            public string stack;
            public bool expand = false;
        }

        List<STraceMsg> mLines = new List<STraceMsg>();

        void Awake()
        {
            Application.logMessageReceived += onTrace;
            Trace.logMessageReceived += onTrace;
		}

        void OnDestroy()
        {
            Trace.logMessageReceived -= onTrace;
            Application.logMessageReceived -= onTrace;
        }

        void Start()
        {
            Trace.TraceLn("ScreenTraceListener::Start success!");
        }

        public void onTrace(string content, string stack, LogType logtype)
		{
            if (!Application.isPlaying) return;

			//string sline = time.ToString ("yyyy-MM-dd HH:mm:ss") + " :" + msg ;
			//string sline = time.ToString() + " :" + msg ;
     
            lock (mLines)
            {
                if (mLines.Count > 50)
                {
                    mLines.RemoveAt(0);
                }
                STraceMsg msg = new STraceMsg();
                msg.content = content;
                msg.stack = stack;
                msg.logtype = logtype;
                msg.time = DateTime.Now;
                mLines.Add(msg);
            }
		}


		private Vector2 scrollPosition  = Vector2.zero;
		public Vector2 pos = new Vector2 (30, 30);
		public bool bShow=false;

        private string DateTimeToString(DateTime datetime)
        {
            string ret = "";
            ret += datetime.Year + "/" + datetime.Month + "/" + datetime.Day + " ";
            ret += datetime.Hour + ":" + datetime.Minute + ":" + datetime.Second + " ";
            ret += datetime.Millisecond;
            return ret;
        }

		void OnGUI()
		{
            if (Debug.isDebugBuild == false)
            {
                return;
            }

			GUI.color = Color.white;
            GUI.skin.button.fontSize = 24;
            GUI.skin.toggle.fontSize = 24;
            GUI.skin.label.fontSize = 24;

			if (GUI.Button (new Rect (pos.x, pos.y, Screen.width / 2 - 20, 30), "Trace")) {
				bShow=!bShow;
			}

			if (bShow)
			{

				scrollPosition = GUI.BeginScrollView (new Rect (pos.x,pos.y+20,Screen.width/2-20,Screen.height-pos.y-40),
				                                      scrollPosition, new Rect (0, 0, 1200, 3200));

                OnGUIEx();

				// End the scroll view that we began above.
				GUI.EndScrollView ();
			}

		}

        public void OnGUIEx()
        {

            GUILayout.BeginVertical("Box");

            STraceMsg[] lines;
            lock (mLines)
            {
                lines = mLines.ToArray();
            }
            if (lines != null)
            {
                int imax = lines.Length;
                for (int i = 0; i < imax; i++)
                {
                    STraceMsg msg = lines[i];
                    if (msg.logtype == LogType.Error || msg.logtype == LogType.Exception)
                        GUI.color = new Color(1.0f, 0.5f, 0.5f);
                    else if (msg.logtype == LogType.Warning)
                        GUI.color = new Color(1.0f, 0.8f, 0.4f);
                    else
                        GUI.color = new Color(0.5f,0.5f,1.0f);
                    string msgmain = "(" + DateTimeToString(msg.time) + "): " + msg.content;
                    msg.expand = GUILayout.Toggle(msg.expand, msgmain, GUILayout.Width(1160));
                    if (msg.expand)
                        GUILayout.Label(msg.stack, GUILayout.Width(1160));
                }
            }

            GUILayout.EndVertical();
        }
	}
}
