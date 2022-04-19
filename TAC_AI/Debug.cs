using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TAC_AI
{
    internal static class Debug
    {
        internal static bool LogAll = false;
        internal static bool ShouldLog = true;
        private static bool LogDev = false;

        internal static void Info(string message)
        {
            if (!ShouldLog || !LogAll)
                return;
            UnityEngine.Debug.Log(message);
        }
        internal static void Log(string message)
        {
            if (!ShouldLog)
                return;
            UnityEngine.Debug.Log(message);
        }
        internal static void Log(Exception e)
        {
            if (!ShouldLog)
                return;
            UnityEngine.Debug.Log(e);
        }
        internal static void Assert(bool shouldAssert, string message)
        {
            if (!ShouldLog || !shouldAssert)
                return;
            UnityEngine.Debug.Log(message + "\n" + StackTraceUtility.ExtractStackTrace().ToString());
        }
        internal static void LogError(string message)
        {
            if (!ShouldLog)
                return;
            UnityEngine.Debug.Log(message + "\n" + StackTraceUtility.ExtractStackTrace().ToString());
        }
        internal static void LogDevOnly(string message)
        {
            if (!LogDev)
                return;
            UnityEngine.Debug.Log(message + "\n" + StackTraceUtility.ExtractStackTrace().ToString());
        }
        internal static void FatalError(Exception e)
        {
            ManUI.inst.ShowErrorPopup("TACtical_AI: ENCOUNTERED CRITICAL ERROR: " + e);
            UnityEngine.Debug.Log("TACtical_AI: ENCOUNTERED CRITICAL ERROR");
            UnityEngine.Debug.Log("TACtical_AI: MAY NOT WORK PROPERLY AFTER THIS ERROR, PLEASE REPORT!");
        }
    }
}
