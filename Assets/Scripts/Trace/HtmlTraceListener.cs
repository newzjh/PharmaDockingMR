
using System.Collections;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading;
#if NETFX_CORE
using System.Threading.Tasks;
using Windows.System.Threading;
#endif
using System.IO;

namespace LogTrace
{

    // 输出到Html文件
    public class HtmlTraceListener : MonoBehaviour
    {
        public class STraceMsg
        {
            public DateTime time;
            public LogType logtype;
            public string content;
            public string stack;
        }

#if NETFX_CORE 
        private Task task = null;
#else
        private Thread task = null;
#endif
        public bool m_bThreadExit = false;

        Queue<STraceMsg> m_msgInList = new Queue<STraceMsg>();

        private FileStream m_fs = null;

        string[] m_color;

 
        public void Awake()
        {
            Application.logMessageReceived += onTrace;
            Trace.logMessageReceived += onTrace;

            m_color = new string[(int)LogType.Exception + 1]
            { "<font color=\"#FF0000\">",//Error
                "<font color=\"#000000\">", // Assert
				"<font color=\"#FF00FF\">",	// Warning
				"<font color=\"#0000FF\">",	// Trace
				"<font color=\"#FF0000\">",};	// Exception 

            string filepath = Application.persistentDataPath + "/log.html";
            File.Delete(filepath);
            m_fs = new FileStream(filepath, FileMode.OpenOrCreate);
            WriteLine("<html>\n<head>\n<meta http-equiv=\"content-type\" content=\"text/html; charset=UTF-8\">\n" +
                             "<title>Html Output</title>\n</head>\n<body>\n<font face=\"Fixedsys\" size=\"2\" color=\"#0000FF\">\n");

#if NETFX_CORE 
            task = new Task(run);
            task.Start();
#else
            task = new Thread(new ThreadStart(run));
            task.Start();
#endif

            Trace.TraceLn("HtmlTraceListener::Start success!");
            Trace.TraceLn("LogPath=" + filepath);
        }

        public void OnDestroy()
        {
            Trace.logMessageReceived -= onTrace;
            Application.logMessageReceived -= onTrace;
            Close();
        }


        private void WriteLine(string line)
        {
            if (m_fs == null) return;
            byte[] data = System.Text.Encoding.UTF8.GetBytes(line);
            m_fs.Write(data, 0, data.Length);
            m_fs.Flush();
        }

        private void Stop()
        {
            m_bThreadExit = true;
        }

        // 关闭
        public void Close()
        {
            Stop();
#if NETFX_CORE 
            task.Wait();
#else
            task.Join();
#endif
            task = null;

            WriteLine("</font>\n</body>\n</html>");

            if (m_fs != null)
            {
                m_fs.Dispose();
            }
            m_fs = null;
        }

        private bool m_enable = true;
        public void OnEnable()
        {
            m_enable = true;
        }
        public void OnDisable()
        {
            m_enable = false;
        }


        public void run()
        {
            while (!m_bThreadExit)
            {
                if (m_enable)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        STraceMsg msg = null;
                        lock (m_msgInList)
                        {
                            if (m_msgInList.Count > 0)
                            {
                                msg = m_msgInList.Dequeue();
                            }
                        }
                        if (msg != null)
                        {
                            HandleMsg(msg.content, msg.stack, msg.logtype, msg.time);
                        }
                    }
                }

#if NETFX_CORE 
                Task.Delay(20).Wait();
#else
                Thread.Sleep(20);
#endif
            }

        }

        private string DateTimeToString(DateTime datetime)
        {
            string ret = "";
            ret += datetime.Year + "/" + datetime.Month + "/" + datetime.Day+" ";
            ret += datetime.Hour + ":" + datetime.Minute + ":" + datetime.Second + " ";
            ret += datetime.Millisecond;
            return ret;
        }

        public void HandleMsg(string szMsg, string szStack, LogType logtype, DateTime time)
        {
            string htmlBuff;
            htmlBuff = m_color[(int)logtype] + DateTimeToString(time) + " :" + szMsg + "</font><br>\n";
            WriteLine(htmlBuff);
            if (logtype == LogType.Error || logtype == LogType.Exception)
            {
                htmlBuff = "&nbsp;&nbsp;&nbsp;<details><summary>stacktrace</summary>" + m_color[(int)logtype] +
                    szStack.Replace("at", "<br>at") + "</details></font><br>\n";
                WriteLine(htmlBuff);
            }
        }

        private void onTrace(string szContent, string szStack, LogType logtype)
        {
            STraceMsg pMsg = new STraceMsg();
            pMsg.time = DateTime.Now;
            pMsg.logtype = logtype;
            pMsg.content = szContent;
            pMsg.stack = szStack;
            lock (m_msgInList)
            {
                m_msgInList.Enqueue(pMsg);
            }
        }

    }
}

