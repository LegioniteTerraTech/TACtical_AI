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
    public class NP_BaseUnit : NP_TechUnit
    {
        public int BuildBucks { get { return funds; } set { funds = value; SetBuildBucks(funds); } }
        private int funds = 0;
        private readonly int RechargeRate;
        private readonly int RechargeRateDay;

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
            return teamInst.EBUs.Contains(this);
        }
        public void TryPushMoneyToLoadedInstance()
        {
            if (funds == 0)
                return;
            RLoadedBases.EnemyBaseFunder EBF = RLoadedBases.EnemyBases.Find(delegate (RLoadedBases.EnemyBaseFunder cand) { return cand.Team == teamInst.Team && Name == cand.name; });
            if (EBF)
            {
                DebugTAC_AI.Log("TACtical_AI: EnemyBaseUnloaded - Base " + Name + " pushed funds to loaded tech of ID " + EBF.name);
            }
            else
            {
                DebugTAC_AI.LogError("TACtical_AI: EnemyBaseUnloaded - Base " + Name + " failed to update funds");
            }
        }

        public NP_BaseUnit(ManSaveGame.StoredTech techIn, NP_Presence team) :
            base(techIn, team, ManEnemyWorld.BaseHealthMulti, 0)
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
                        if (ME && ME.OutputEnergyType == EnergyRegulator.EnergyType.Electric)
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
                BuildBucks = RLoadedBases.GetBuildBucksFromNameExt(tech.m_TechData.Name);
                SpawnBaseTypes SBT = RawTechLoader.GetEnemyBaseTypeFromName(RLoadedBases.EnemyBaseFunder.GetActualName(tech.m_TechData.Name));
                HashSet<BasePurpose> BP = RawTechLoader.GetBaseTemplate(SBT).purposes;
                Faction = tech.m_TechData.GetMainCorpExt();

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
                DebugTAC_AI.Log("TACtical_AI: EnemyBaseUnit(EBU) Failiure on init at level " + level + "!");
            }
            RechargeRate = rechargeRate;
            RechargeRateDay = rechargeRateDay;
        }
        public void SetBuildBucks(int newVal)
        {
            StringBuilder nameActual = new StringBuilder();
            char lastIn = 'n';
            bool doingBB = false;
            foreach (char ch in Name)
            {
                if (!doingBB)
                {
                    if (ch == '¥' && lastIn == '¥')
                    {
                        nameActual.Remove(nameActual.Length - 2, 2);
                        doingBB = true;
                    }
                    else
                        nameActual.Append(ch);
                    lastIn = ch;
                }
            }
            if (newVal == -1)
            {
                nameActual.Append(" ¥¥ Inf");
            }
            else
                nameActual.Append(" ¥¥" + newVal);
            Name = nameActual.ToString();
        }
        public static string GetActualName(string name)
        {
            StringBuilder nameActual = new StringBuilder();
            char lastIn = 'n';
            foreach (char ch in name)
            {
                if (ch == '¥' && lastIn == '¥')
                {
                    nameActual.Remove(nameActual.Length - 2, 2);
                    break;
                }
                else
                    nameActual.Append(ch);
                lastIn = ch;
            }
            return nameActual.ToString();
        }

        public long Generate(float deltaTime, bool isDay)
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
        public override bool TakeDamage(int Dealt)
        {
            ManEnemyWorld.GetTeam(tech.m_TeamID).SetDefendMode(tilePos);
            return base.TakeDamage(Dealt);
        }
    }

}
