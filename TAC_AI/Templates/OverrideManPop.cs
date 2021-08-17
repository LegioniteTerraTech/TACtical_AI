using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using UnityEngine;
using TAC_AI.AI.Enemy;
using TAC_AI.AI;

namespace TAC_AI.Templates
{
    public class OverrideManPop
    {
        private static FieldInfo dayTechs = typeof(ManPop).GetField("m_DayFilter", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo nightTechs = typeof(ManPop).GetField("m_NightFilter", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo nonEXPTechs = typeof(ManPop).GetField("m_NoExpFilter", BindingFlags.NonPublic | BindingFlags.Instance);

        private static FieldInfo BigVal = typeof(TechSpawnFilter).GetField("m_MaxValue", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo BigBloc = typeof(TechSpawnFilter).GetField("m_MaxBlockCount", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo BigRad = typeof(TechSpawnFilter).GetField(" m_MaxRadiusSize", BindingFlags.NonPublic | BindingFlags.Instance);


        internal static TechSpawnFilter DayTechsSav;
        internal static TechSpawnFilter NightTechsSav;
        internal static TechSpawnFilter NonEXPTechsSav;

        private static bool SavedRight = false;
        private static bool QueueOverride = false;
        private static bool ToChangeOverride = false;
        private static bool IsOverridden = false;

        private static void SavePop()
        {
            if (!KickStart.isPopInjectorPresent && !SavedRight)
            {
                ManPop pop = Singleton.Manager<ManPop>.inst;
                DayTechsSav = (TechSpawnFilter)dayTechs.GetValue(pop);
                NightTechsSav = (TechSpawnFilter)nightTechs.GetValue(pop);
                NonEXPTechsSav = (TechSpawnFilter)nonEXPTechs.GetValue(pop);
                SavedRight = true;
            }
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
            if (!(bool)ManPop.inst)
                return;
            SavePop();
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
            ManPop pop = Singleton.Manager<ManPop>.inst;
            try
            {
                TechSpawnFilter DayTechsSavEdit = DayTechsSav;
                BigBloc.SetValue(DayTechsSavEdit, 9001);
                BigRad.SetValue(DayTechsSavEdit, 9001);
                BigVal.SetValue(DayTechsSavEdit, 25000000);
                dayTechs.SetValue(pop, DayTechsSavEdit);
            }
            catch { }

            try
            {
                TechSpawnFilter NightTechsSavEdit = NightTechsSav;
                BigBloc.SetValue(NightTechsSavEdit, 9001);
                BigRad.SetValue(NightTechsSavEdit, 9001);
                BigVal.SetValue(NightTechsSavEdit, 25000000);
                dayTechs.SetValue(pop, NightTechsSavEdit);
            }
            catch { }

            try
            {
                TechSpawnFilter NonEXPTechsSavEdit = NonEXPTechsSav;
                BigBloc.SetValue(NonEXPTechsSavEdit, 9001);
                BigRad.SetValue(NonEXPTechsSavEdit, 9001);
                BigVal.SetValue(NonEXPTechsSavEdit, 25000000);
                dayTechs.SetValue(pop, NonEXPTechsSavEdit);
            }
            catch { }
            IsOverridden = true;
        }

        private static void RecoverPop()
        {
            ManPop pop = Singleton.Manager<ManPop>.inst;
            try
            {
                dayTechs.SetValue(pop, DayTechsSav);
            }
            catch { }
            try
            {
                dayTechs.SetValue(pop, NightTechsSav);
            }
            catch { }
            try
            {
                dayTechs.SetValue(pop, NonEXPTechsSav);
            }
            catch { }
            IsOverridden = false;
        }
    }
}
