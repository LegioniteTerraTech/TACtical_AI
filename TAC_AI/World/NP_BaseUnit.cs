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
            if (ManBaseTeams.TryGetBaseTeam(Team, out var ETD))
                ETD.AddBuildBucks(value);
            else
                DebugTAC_AI.Assert("BuildBucks was added but ManBaseTeams didn't have the base team " +
                    Team + "! " + value + " was lost to oblivion!");
        }
        public void SpendBuildBucks(int value)
        {
            if (ManBaseTeams.TryGetBaseTeam(Team, out var ETD))
                ETD.AddBuildBucks(value);
            else
                DebugTAC_AI.Assert("BuildBucks was added but ManBaseTeams didn't have the base team " +
                    Team + "! " + value + " was lost to oblivion!");
        }
        public void SetBuildBucks(int value)
        {
            if (ManBaseTeams.TryGetBaseTeam(Team, out var ETD))
                ETD.SetBuildBucks = value;
        }
        public int BlockCount => tech.m_TechData.m_BlockSpecs.Count;
        public WorldPosition WorldPos => WorldPosition.FromScenePosition(tech.GetBackwardsCompatiblePosition());
        public Tank tank => null;
        public bool valid => this != null && Exists();

        private readonly int RechargeRate;
        private readonly int RechargeRateDay;

        public bool IsHQ => ManBaseTeams.IsTeamHQ(this);

        public readonly int revenue;
        public readonly bool isDefense = false;
        public readonly bool isSiegeBase = false;
        public readonly bool isHarvestBase = false;
        public readonly bool isTechBuilder = false;

        /// <summary>
        /// If this Tech has a terminal, it can build any tech from the population
        /// </summary>
        public readonly bool HasTerminal = false;

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
            base(techIn, team, techIn.m_TechData.GetMainCorporations().FirstOrDefault(), ManEnemyWorld.BaseHealthMulti)
        {
            //tilePos = tilePosition;

            int level = 0;
            int rechargeRate = 0;
            int rechargeRateDay = 0;
            try
            {
                foreach (TankPreset.BlockSpec spec in tech.m_TechData.m_BlockSpecs)
                {
                    TankBlock TB = ManSpawn.inst.GetBlockPrefab(spec.GetBlockType());
                    if ((bool)TB)
                    {
                        var MIP = TB.GetComponent<ModuleItemProducer>();
                        if ((bool)MIP)
                        {
                            revenue += (int)((ManEnemyWorld.GetBiomeAutominerGains(PosScene) * ManEnemyWorld.OperatorTickDelay) /
                                (float)ManEnemyWorld.ProdDelay.GetValue(MIP));
                        }
                        var ME = TB.GetComponent<ModuleEnergy>();
                        if (ME && ME.OutputEnergyType == TechEnergy.EnergyType.Electric)
                        {
                            ModuleEnergy.OutputConditionFlags flags = (ModuleEnergy.OutputConditionFlags)ManEnemyWorld.PowCond.GetValue(ME);
                            if ((flags & ModuleEnergy.OutputConditionFlags.DayTime) != 0)
                                rechargeRateDay += Mathf.CeilToInt((float)ManEnemyWorld.PowDelay.GetValue(ME));
                            else
                                rechargeRate += Mathf.CeilToInt((float)ManEnemyWorld.PowDelay.GetValue(ME));
                        }
                    }
                }
                level++;
                AddBuildBucks(RLoadedBases.GetBuildBucksFromNameExt(tech.m_TechData.Name));
                SpawnBaseTypes SBT = RawTechLoader.GetEnemyBaseTypeFromName(RLoadedBases.EnemyBaseFunder.GetActualName(tech.m_TechData.Name));
                HashSet<BasePurpose> BP = RawTechLoader.GetBaseTemplate(SBT).purposes;
                Faction = tech.m_TechData.GetMainCorp();

                level++;
                if (BP.Contains(BasePurpose.Defense))
                    isDefense = true;
                if (BP.Contains(BasePurpose.TechProduction))
                    isTechBuilder = true;
                if (BP.Contains(BasePurpose.HasReceivers))
                {
                    isHarvestBase = true;
                    revenue += ManEnemyWorld.GetBiomeSurfaceGains(ManWorld.inst.TileManager.CalcTileCentreScene(tilePos)) * ManEnemyWorld.OperatorTickDelay;
                }
                if (BP.Contains(BasePurpose.Headquarters))
                    isSiegeBase = true;
            }
            catch
            {
                DebugTAC_AI.Log(KickStart.ModID + ": EnemyBaseUnit(EBU) Failiure on init at level " + level + "!");
            }
            RechargeRate = rechargeRate;
            RechargeRateDay = rechargeRateDay;
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
