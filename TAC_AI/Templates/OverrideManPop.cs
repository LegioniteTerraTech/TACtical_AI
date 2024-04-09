using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using UnityEngine;

namespace TAC_AI.Templates
{
    public class TempFilterStore
    {
        internal int Bloc;
        internal float Val;
        internal float Rad;
    }
    internal class OverrideManPop : MonoBehaviour
    {
        private static FieldInfo dayTechs = typeof(ManPop).GetField("m_DayFilter", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo nightTechs = typeof(ManPop).GetField("m_NightFilter", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo nonEXPTechs = typeof(ManPop).GetField("m_NoExpFilter", BindingFlags.NonPublic | BindingFlags.Instance);

        private static FieldInfo BigVal = typeof(TechSpawnFilter).GetField("m_MaxValue", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo BigBloc = typeof(TechSpawnFilter).GetField("m_MaxBlockCount", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo BigRad = typeof(TechSpawnFilter).GetField(" m_MaxRadiusSize", BindingFlags.NonPublic | BindingFlags.Instance);


        internal static TempFilterStore DayTechsSav;
        internal static TempFilterStore NightTechsSav;
        internal static TempFilterStore NonEXPTechsSav;
        internal static TempFilterStore UnRestrainedSav;

        private static bool SavedRight = false;
        private static bool QueueOverride = false;
        private static bool ToChangeOverride = false;
        private static bool IsOverridden = false;

        private static bool SavePop()
        {
            try
            {
                if (!KickStart.isPopInjectorPresent && !SavedRight)
                {
                    ManPop pop = Singleton.Manager<ManPop>.inst;
                    DayTechsSav = GetFilter(dayTechs, pop);
                    NightTechsSav = GetFilter(nightTechs, pop);
                    NonEXPTechsSav = GetFilter(nonEXPTechs, pop);
                    UnRestrainedSav = new TempFilterStore
                    {
                        Bloc = 109001,
                        Rad = 109001,
                        Val = 250000000
                    };
                    SavedRight = true;
                    DebugTAC_AI.Log(KickStart.ModID + ": Fetched pop");
                    return true;
                }
            }
            catch
            {
                DebugTAC_AI.Log(KickStart.ModID + ": Could not fetch pop, will try again later");
                return false;
            }
            DebugTAC_AI.Log(KickStart.ModID + ": Pop already fetched");
            return true;
        }
        public static void ChangeToRagnarokPop(bool isTrue)
        {
            if (!KickStart.isPopInjectorPresent)
            {
                if (isTrue && !IsOverridden)
                {
                    ToChangeOverride = true;
                    QueueOverride = true;
                }
                else if (IsOverridden)
                {
                    ToChangeOverride = false;
                    QueueOverride = true;
                }
            }
        }
        public static void QueuedChangeToRagnarokPop()
        {
            if (!(bool)ManPop.inst || !SavePop())
                return;
            if (!KickStart.isPopInjectorPresent && QueueOverride)
            {
                if (ToChangeOverride && !IsOverridden)
                {
                    OverridePop();
                }
                else if (IsOverridden)
                {
                    RecoverPop();
                }
            }
        }


        private static void OverridePop()
        {
            DebugTAC_AI.Log(KickStart.ModID + ": RAGNAROK ENABLED");
            ManPop pop = Singleton.Manager<ManPop>.inst;
            try
            {
                SetFilter(dayTechs, pop, UnRestrainedSav);
            }
            catch (Exception e)
            {
                DebugTAC_AI.Log(KickStart.ModID + ": Failed to change dayTechs " + e);
            }

            try
            {
                SetFilter(nightTechs, pop, UnRestrainedSav);
            }
            catch
            {
                DebugTAC_AI.Log(KickStart.ModID + ": Failed to change nightTechs");
            }

            try
            {
                SetFilter(nonEXPTechs, pop, UnRestrainedSav);
            }
            catch
            {
                DebugTAC_AI.Log(KickStart.ModID + ": Failed to change nonEXPTechs");
            }
            IsOverridden = true;
        }

        private static void RecoverPop()
        {
            DebugTAC_AI.Log(KickStart.ModID + ": RAGNAROK DISABLED");
            ManPop pop = Singleton.Manager<ManPop>.inst;
            try
            {
                SetFilter(dayTechs, pop, DayTechsSav);
            }
            catch
            {
                DebugTAC_AI.Log(KickStart.ModID + ": Failed to change dayTechs");
            }
            try
            {
                SetFilter(nightTechs, pop, NightTechsSav);
            }
            catch
            {
                DebugTAC_AI.Log(KickStart.ModID + ": Failed to change nightTechs");
            }
            try
            {
                SetFilter(nonEXPTechs, pop, NonEXPTechsSav);
            }
            catch
            {
                DebugTAC_AI.Log(KickStart.ModID + ": Failed to change nonEXPTechs");
            }
            IsOverridden = false;
        }


        private static TempFilterStore GetFilter(FieldInfo toApplyTo, ManPop inst)
        {
            TempFilterStore TFS = new TempFilterStore();
            try
            {
                TechSpawnFilter TSF = (TechSpawnFilter)toApplyTo.GetValue(inst);
                try
                {
                    TFS.Bloc = (int)BigBloc.GetValue(TFS);
                }
                catch
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Failed to get Bloc");
                }
                try
                {
                    TFS.Rad = (float)BigRad.GetValue(TFS);
                }
                catch
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Failed to get Rad");
                }
                try
                {
                    TFS.Val = (float)BigVal.GetValue(TFS);
                }
                catch
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Failed to get Val");
                }
            }
            catch
            {
                DebugTAC_AI.Log(KickStart.ModID + ": Failed to get TechSpawnFilter");
            }
            return TFS;
        }
        private static void SetFilter(FieldInfo toApplyTo, ManPop inst, TempFilterStore TFS)
        {
            try
            {
                TechSpawnFilter TSF = (TechSpawnFilter)toApplyTo.GetValue(inst);
                try
                {
                    BigBloc.SetValue(TSF, TFS.Bloc);
                }
                catch
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Failed to change Bloc");
                }
                try
                {
                    BigRad.SetValue(TSF, TFS.Rad);
                }
                catch
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Failed to change Rad");
                }
                try
                {
                    BigVal.SetValue(TSF, TFS.Val);
                }
                catch
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Failed to change Val");
                }
            }
            catch
            {
                DebugTAC_AI.Log(KickStart.ModID + ": Failed to change TechSpawnFilter");
            }
        }
    }
}
