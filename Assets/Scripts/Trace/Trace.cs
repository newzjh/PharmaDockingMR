
using System.Collections;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace LogTrace
{

	public class Trace
    {

        public static event Application.LogCallback logMessageReceived;

        // 打印一个信息
        private static void TraceByLevel(string szContent, string szStack, LogType logtype)
        {
            if (logMessageReceived != null)
            {
                logMessageReceived(szContent, szStack, logtype);
            }
        }


        public static void TraceLn(string szContent)
        {
            TraceByLevel(szContent, System.Environment.StackTrace, LogType.Log);
        }

        public static void WarningLn(string szContent)
        {
            TraceByLevel(szContent, System.Environment.StackTrace, LogType.Warning);
        }

        public static void ErrorLn(string szContent)
        {
            TraceByLevel(szContent, System.Environment.StackTrace, LogType.Error);
        }

		
 
    }



}