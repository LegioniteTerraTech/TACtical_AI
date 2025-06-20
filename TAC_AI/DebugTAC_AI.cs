using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TerraTechETCUtil;
using UnityEngine;

namespace TAC_AI
{
    internal static class DebugTAC_AI
    {
        internal static bool NotErrored = true;

        internal static bool ShouldLog = true;
        internal static bool DoLogInfos = false;
        internal static bool DoLogAISetup = false;
        internal static bool DoLogPathing = false;
        internal static bool DoLogSpawning = false;
        internal static bool DoLogLoading = false;
        internal static bool DoLogTeams = true;
        private static bool DoLogNet = false;
#if DEBUG
        private static bool LogDev = true;
#else
        private static bool LogDev = false;
#endif

        private static Tank CalcTarget = null;
        private static System.Diagnostics.Stopwatch AILoadTimer = new System.Diagnostics.Stopwatch();
        internal static void BeginAICalculationTimer(Tank target)
        {
            CalcTarget = target;
            AILoadTimer.Restart();
            AILoadTimer.Start();
        }
        internal static void FinishAICalculationTimer(Tank target)
        {
            if (CalcTarget != target)
                return;
            AILoadTimer.Stop();
            Log("Calculations for AI " + target.name + " finished in " + 
                AILoadTimer.ElapsedMilliseconds.ToString() + " miliseconds");
            CalcTarget = null;
        }

        private static System.Diagnostics.Stopwatch AIWorldTimer = new System.Diagnostics.Stopwatch();
        internal static void BeginAIWorldTimer()
        {
            AILoadTimer.Restart();
            AILoadTimer.Start();
        }
        internal static long FinishAIWorldTimer()
        {
            AILoadTimer.Stop();
            return AILoadTimer.ElapsedMilliseconds;
        }

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
        internal static void LogTeams(string message)
        {
            if (!ShouldLog || !DoLogTeams)
                return;
            UnityEngine.Debug.Log(message);
        }
        internal static void LogWarnPlayerOnce(string message, Exception e)
        {
            if (!ShouldLog)
                return;
            if (NotErrored)
            {
                ManUI.inst.ShowErrorPopup("ERROR with Advanced AI\n" + message + "\nContinue with caution");
                NotErrored = false;
            }
            UnityEngine.Debug.Log(KickStart.ModID + ": "  + message + e);
        }
        internal static void LogLoad(string message)
        {
            if (!ShouldLog || !DoLogLoading)
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
            if (NoLogSpawning)
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
            UnityEngine.Debug.Log(message);
        }
        internal static void LogDevOnlyAssert(string message)
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
            Debug.Log("Advanced AI: Error happened "+ Warning + " - " + StackTraceUtility.ExtractStackTrace());
            seriousError = true;
        }
        internal static void Warning(string Warning)
        {
            Debug.Log("Advanced AI: Warning happened " + Warning + " - " + StackTraceUtility.ExtractStackTrace());
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
                    Debug.Log("Advanced AI: Error happened " + StackTraceUtility.ExtractStackTrace());
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
            Assert(StackTraceUtility.ExtractStackTrace());
            UnityEngine.Debug.Log(KickStart.ModID + ": ENCOUNTERED CRITICAL ERROR");
            UnityEngine.Debug.Log(KickStart.ModID + ": MAY NOT WORK PROPERLY AFTER THIS ERROR, PLEASE REPORT!");
        }
        internal static void FatalError(string e)
        {
            ManUI.inst.ShowErrorPopup(KickStart.ModID + ": ENCOUNTERED CRITICAL ERROR: " + e);
            Assert(e);
            UnityEngine.Debug.Log(KickStart.ModID + ": ENCOUNTERED CRITICAL ERROR");
            UnityEngine.Debug.Log(KickStart.ModID + ": MAY NOT WORK PROPERLY AFTER THIS ERROR, PLEASE REPORT!");
        }
        private static int count =-1;
        internal static void EndlessLoopBreaker()
        {
            if (count == -1)
            {
                InvokeHelper.InvokeSingleRepeat(() => { count = 0; }, 0.001f);
                count = 0;
            }
            if (count > 30)
            {
                throw new InvalidOperationException("Endless loop!");
            }
        }
    }
}
