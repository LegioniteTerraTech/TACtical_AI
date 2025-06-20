using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TerraTechETCUtil;
using TAC_AI.AI.Enemy;
using TAC_AI.Templates;

namespace TAC_AI.World
{
    public class NP_BaseUnit : NP_TechUnit, TeamBasePointer
    {
        public int BuildBucks
        {
            get => ManBaseTeams.GetTeamMoney(Team);
        }
        public void AddBuildBucks(int value)
        {
            if (ManBaseTeams.TryGetBaseTeamDynamicOnly(Team, out var ETD))
                ETD.AddBuildBucks(value);
            else
                DebugTAC_AI.Assert("AddBuildBucks was called but ManBaseTeams didn't have the base team " +
                    Team + "!  [" + value + "] was lost to oblivion!");
        }
        public void SpendBuildBucks(int value)
        {
            if (ManBaseTeams.TryGetBaseTeamDynamicOnly(Team, out var ETD))
                ETD.SpendBuildBucks(value);
            else
                DebugTAC_AI.Assert("SpendBuildBucks was called but ManBaseTeams didn't have the base team " +
                    Team + "!  gues its free then");
        }
        public void SetBuildBucks(int value)
        {
            if (ManBaseTeams.TryGetBaseTeamDynamicOnly(Team, out var ETD))
                ETD.SetBuildBucks = value;
            else
                DebugTAC_AI.Assert("SetBuildBucks was called but ManBaseTeams didn't have the base team " +
                    Team + "!  Nothing happens!");
        }
        public int BlockCount => tech.m_TechData.m_BlockSpecs.Count;
        public Tank tank => null;
        public bool valid => this != null && Exists();

        internal int RechargeRate;
        internal int RechargeRateDay;

        public bool IsHQ => ManBaseTeams.IsTeamHQ(this);

        public int revenue;
        public bool isDefense = false;
        public bool isSiegeBase = false;
        public bool isTechBuilder = false;

        /// <summary>
        /// If this Tech has a terminal, it can build any tech from the population
        /// </summary>
        public bool HasTerminal = false;

        public override bool Exists()
        {
            return teamInst.IsValidAndRegistered() && teamInst.EBUs.Contains(this);
        }
        public override float GetSpeed()
        {
            return 0;
        }
        public override float GetEvasion()
        {
            return ManEnemyWorld.BaseEvasion;
        }

        internal override void MovementSceneDelta(float timeDelta)
        { 
        }


        internal NP_BaseUnit(ManSaveGame.StoredTech techIn, NP_Presence_Automatic team) :
            base(techIn, team, techIn.m_TechData.GetMainCorporations().FirstOrDefault())
        {
            //tilePos = tilePosition;

            ManEnemyWorld.GetStatsAsync(this);
        }

        internal long Generate(float deltaTime, bool isDay)
        {
            if (isDay)
                Shield += Mathf.RoundToInt(deltaTime * (RechargeRateDay + RechargeRate));
            else
                Shield += Mathf.RoundToInt(deltaTime * RechargeRateDay);
            if (Shield > MaxShield)
            {
                long excess = Shield - MaxShield;
                Shield = MaxShield;
                return excess;
            }
            return 0;
        }

        /// <summary>
        /// Deal damage to this Tech
        /// </summary>
        /// <param name="dealt"></param>
        /// <returns>True if tech destroyed</returns>
        public override bool RecieveDamage(int Dealt)
        {
            ManEnemyWorld.GetTeam(tech.m_TeamID).SetDefendMode(tilePos);
            if (MaxShield > 0)
            {
                Shield -= Dealt;
                if (Shield <= 0)
                {
                    Health += Shield;
                    Shield = 0;
                }
                NP_Presence_Automatic.ReportCombat("Base " + Name + " has received " + Dealt + " damage | Health " + Health
                    + " | Shield " + Shield);
            }
            else
            {
                Health -= Dealt;
                NP_Presence_Automatic.ReportCombat("Base " + Name + " has received " + Dealt + " damage | Health " + Health);
            }
            return Health < 0;
        }
    }

}
