using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TAC_AI
{
    internal static class DebugTAC_AI
    {
        internal static bool ShouldLog = true;
        internal static bool DoLogInfos = false;
        internal static bool DoLogAISetup = true;
        internal static bool DoLogPathing = false;
        internal static bool DoLogSpawning = false;
        private static bool DoLogNet = true;
#if DEBUG
        private static bool LogDev = true;
#else
        private static bool LogDev = false;
#endif



        internal static void Info(string message)
        {
            if (!ShouldLog || !DoLogInfos)
                return;
            UnityEngine.Debug.Log(message);
        }
        internal static void Log(string message)
        {
            if (!ShouldLog)
                return;
            UnityEngine.Debug.Log(message);
        }
        internal static bool NoInfoAISetup => !ShouldLog || !DoLogAISetup;
        internal static void LogAISetup(string message)
        {
            if (NoInfoAISetup)
                return;
            UnityEngine.Debug.Log(message);
        }
        internal static bool NoLogPathing => !ShouldLog || !DoLogPathing;
        internal static void LogPathing(string message)
        {
            if (NoLogPathing)
                return;
            UnityEngine.Debug.Log(message);
        }
        internal static bool NoLogSpawning => !ShouldLog || !DoLogSpawning;
        internal static void LogSpawn(string message)
        {
            if (!NoLogSpawning)
                return;
            UnityEngine.Debug.Log(message);
        }
        internal static void Log(Exception e)
        {
            if (!ShouldLog)
                return;
            UnityEngine.Debug.Log(e);
        }

        internal static void LogNet(string message)
        {
            if (!DoLogNet)
                return;
            UnityEngine.Debug.Log(message);
        }

        internal static void Assert(string message)
        {
            if (!ShouldLog)
                return;
            UnityEngine.Debug.Log(message + "\n" + StackTraceUtility.ExtractStackTrace().ToString());
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
        internal static void Exception(string message)
        {
            throw new Exception(KickStart.ModID + ": Exception - ", new Exception(message));
        }
        private static List<string> warning = new List<string>();
        private static bool postStartup = false;
        private static bool seriousError = false;
        internal static void ErrorReport(string Warning)
        {
            warning.Add(Warning);
            seriousError = true;
        }
        internal static void Warning(string Warning)
        {
            warning.Add(Warning);
        }
        internal static void DoShowWarnings()
        {
            if (warning.Any())
            {
                foreach (var item in warning)
                {
                    ManUI.inst.ShowErrorPopup("Adv.AI: " + item);
                }
                warning.Clear();
                if (!postStartup && seriousError)
                {
                    if (TerraTechETCUtil.MassPatcher.CheckIfUnstable())
                        ManUI.inst.ShowErrorPopup("Advanced AI: Error happened on Unstable Branch.  If the issue persists, switch back to Stable Branch.");
                    else
                        ManUI.inst.ShowErrorPopup("Advanced AI: Error happened during startup!  Advanced AI might not work correctly.");
                }
                seriousError = false;
            }
            postStartup = true;
        }
        internal static void FatalError()
        {
            ManUI.inst.ShowErrorPopup(KickStart.ModID + ": ENCOUNTERED CRITICAL ERROR");
            UnityEngine.Debug.Log(KickStart.ModID + ": ENCOUNTERED CRITICAL ERROR");
            UnityEngine.Debug.Log(KickStart.ModID + ": MAY NOT WORK PROPERLY AFTER THIS ERROR, PLEASE REPORT!");
        }
        internal static void FatalError(string e)
        {
            ManUI.inst.ShowErrorPopup(KickStart.ModID + ": ENCOUNTERED CRITICAL ERROR: " + e);
            UnityEngine.Debug.Log(KickStart.ModID + ": ENCOUNTERED CRITICAL ERROR");
            UnityEngine.Debug.Log(KickStart.ModID + ": MAY NOT WORK PROPERLY AFTER THIS ERROR, PLEASE REPORT!");
        }
    }
}
