﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using TerraTechETCUtil;
using TAC_AI.Templates;
using TAC_AI.World;
using TAC_AI.AI.Movement;

namespace TAC_AI.AI.Enemy
{

    public static class RLoadedBases
    {
        internal static int MaxSingleBaseType { get { return KickStart.MaxBasesPerTeam / 3; } }
        internal static int MaxDefenses { get { return (int)(KickStart.MaxBasesPerTeam * (float)(2f / 3f)); } }
        internal static int MaxAutominers { get { return KickStart.MaxBasesPerTeam / 2; } }


        public static List<EnemyBaseFunder> EnemyBases = new List<EnemyBaseFunder>();


        public static int TeamActiveMobileTechCount(int Team)
        {
            int activeCount = 0;
            foreach (var item in ManTechs.inst.IterateTechs())
            {
                if (item.Team == Team && !item.IsBase())
                    activeCount++;
            }
            return activeCount;
        }
        public static int TeamActiveAnyBaseCount(int Team)
        {
            int activeCount = 0;
            foreach (var item in ManTechs.inst.IterateTechs())
            {
                if (item.Team == Team && item.IsBase())
                    activeCount++;
            }
            return activeCount;
        }
        public static int TeamActiveMakerBaseCount(int Team)
        {
            return GetTeamBaseFunders(Team).Count;
        }

        public static int TeamGlobalMobileTechCount(int Team)
        {
            return TeamActiveMobileTechCount(Team) + ManEnemyWorld.UnloadedMobileTechCount(Team);
        }
        // Base handling
        /// <summary>
        /// Does NOT count Defenses!!!
        /// </summary>
        /// <param name="Team"></param>
        /// <returns></returns>
        public static int TeamGlobalMakerBaseCount(int Team)
        {
            return TeamActiveMakerBaseCount(Team) + ManEnemyWorld.UnloadedBaseCount(Team);
        }
        public static int TeamGlobalAnyBaseCount(int Team)
        {
            return TeamActiveAnyBaseCount(Team) + ManEnemyWorld.UnloadedBaseCount(Team);
        }

        public static EnemyBaseFunder GetTeamFunder(int Team)
        {
            List<EnemyBaseFunder> baseFunders = GetTeamBaseFunders(Team);
            if (baseFunders.Count == 0)
            {
                //DebugTAC_AI.Log("TACtical_AI: " + Team + " CALLED GetTeamFunds WITH NO BASE!!!");
                return null;
            }
            if (baseFunders.Count > 1)
            {
                //DebugTAC_AI.Log("TACtical_AI: " + Team + " has " + baseFunders.Count + " bases on scene. The richest will be selected.");
                EnemyBaseFunder funder = null;
                int highestFunds = 0;
                foreach (EnemyBaseFunder funds in baseFunders)
                {
                    if (highestFunds < funds.BuildBucks)
                    {
                        highestFunds = funds.BuildBucks;
                        funder = funds;
                    }
                }
                return funder;
            }
            return baseFunders.First();
        }
        public static int GetTeamFunds(int Team)
        {
            EnemyBaseFunder funder = GetTeamFunder(Team);
            if (funder.IsNull())
            {
                return 0;
            }
            return funder.BuildBucks;
        }
        public static List<EnemyBaseFunder> GetTeamBaseFunders(int Team)
        {
            return EnemyBases.FindAll(delegate (EnemyBaseFunder cand) { return cand.Team == Team && cand.Tank.blockman.blockCount > 0; });
        }
        public static int GetAllTeamsEnemyHQCount()
        {
            return EnemyBases.FindAll(delegate (EnemyBaseFunder funds) { return funds.isHQ; }).Count;
        }
        public static int GetCountOfPurpose(BasePurpose BP, List<EnemyBaseFunder> baseFunders)
        {
            return baseFunders.FindAll(delegate (EnemyBaseFunder cand) { return cand.Purposes.Contains(BP); }).Count;
        }
        public static int GetActiveTeamDefenseCount(int Team)
        {
            int Count = 0;
            foreach (Tank tech in Singleton.Manager<ManTechs>.inst.CurrentTechs)
            {
                if (tech.Team == Team)
                {
                    if (tech.name.Contains(RawTechLoader.turretChar))
                    {
                        Count++;
                    }
                }
            }
            return Count;
        }
        public static bool HasTooMuchOfType(int Team, BasePurpose purpose, List<EnemyBaseFunder> baseFunders)
        {
            int Count = 0;
            foreach (EnemyBaseFunder funds in baseFunders)
            {
                if (funds.Purposes.Contains(purpose))
                {
                    Count++;
                }
            }
            if (purpose == BasePurpose.Defense)
            {
                foreach (Tank tech in Singleton.Manager<ManTechs>.inst.CurrentTechs)
                {
                    if (tech.Team == Team)
                    {
                        if (tech.IsAnchored && !tech.GetComponent<EnemyBaseFunder>())
                        {
                            Count++;
                        }
                    }
                }
            }

            bool thisIsTrue;
            if (purpose == BasePurpose.Defense)
            {
                thisIsTrue = Count >= MaxDefenses;
                if (thisIsTrue)
                    DebugTAC_AI.Log("TACtical_AI: HasTooMuchOfType - Team " + Team + " already has too many defenses and cannot make more");
            }
            else if (purpose == BasePurpose.Autominer)
            {
                thisIsTrue = Count >= MaxAutominers;
                if (thisIsTrue)
                    DebugTAC_AI.Log("TACtical_AI: HasTooMuchOfType - Team " + Team + " already has too many autominers and cannot make more");
            }
            else if (purpose == BasePurpose.HasReceivers && FetchNearbyResourceCounts(Team) < AIGlobals.MinResourcesReqToCollect)
            {
                thisIsTrue = false;
                DebugTAC_AI.Log("TACtical_AI: HasTooMuchOfType - Team " + Team + " Does not have enough mineables in range to build Reciever bases.");
            }
            else
            {
                thisIsTrue = Count >= MaxSingleBaseType;
                if (thisIsTrue)
                    DebugTAC_AI.Log("TACtical_AI: HasTooMuchOfType - Team " + Team + " already has too many of type " + purpose.ToString() + " and cannot make more");
            }

            return thisIsTrue;
        }
        public static void RecycleTechToTeam(Tank tank)
        {
            if (!(bool)GetTeamFunder(tank.Team))
            {
                DebugTAC_AI.Log("TACtical_AI: RecycleTechToTeam - Tech " + tank.name + " invoked but no funder is assigned to team");
                return;
            }
            WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(tank.visible);
            int tankCost = RawTechTemplate.GetBBCost(tank);
            string compressed = CompressIfNeeded(tankCost, out int smaller);
            AIGlobals.PopupEnemyInfo(Singleton.Manager<Localisation>.inst.GetMoneyStringWithSymbol(smaller) + compressed, worPos);
            TryAddMoney(tankCost, tank.Team);
            SpecialAISpawner.Eradicate(tank);
        }
        public static string CompressIfNeeded(int input, out int smaller)
        {
            string compressed = "";
            smaller = input;
            if (input > 999999)
            {
                smaller = input / 1000000;
                compressed = "M";
            }
            else if (input > 999)
            {
                smaller = input / 1000;
                compressed = "K";
            }
            return compressed;
        }

        // Bases funds
        public static bool PurchasePossible(int BBCost, int Team)
        {
            EnemyBaseFunder funds = GetTeamFunder(Team);
            if (funds.IsNotNull())
                return funds.PurchasePossible(BBCost);
            return false;
        }
        public static bool PurchasePossible(BlockTypes bloc, int Team)
        {
            int price = Singleton.Manager<RecipeManager>.inst.GetBlockBuyPrice(bloc, true);
            return PurchasePossible(price, Team);
        }
        public static bool BribePossible(Tank tank, int Team)
        {
            int price = (int)(RawTechTemplate.GetBBCost(tank) * AIGlobals.BribeMulti) + AIGlobals.MinimumBBToTryBribe;
            return PurchasePossible(price, Team);
        }
        public static void TryAddMoney(int amount, int Team)
        {
            EnemyBaseFunder funds = GetTeamFunder(Team);
            if (funds.IsNotNull())
            {
                funds.AddBuildBucks(amount);
            }
        }
        public static bool TryMakePurchase(BlockTypes bloc, int Team)
        {
            EnemyBaseFunder funds = GetTeamFunder(Team);
            if (funds.IsNotNull())
            {
                return funds.TryMakePurchase(bloc);
            }
            return false;
        }
        public static bool TryBribeTech(Tank tank, int bribingTeam)
        {
            var funds = GetTeamFunder(bribingTeam);
            if (!funds)
                return false;
            int cost = (int)(RawTechTemplate.GetBBCost(tank) * AIGlobals.BribeMulti);
            if (tank.Team != bribingTeam && funds.PurchasePossible(cost) && RLoadedBases.TeamGlobalMobileTechCount(bribingTeam) < KickStart.EnemyTeamTechLimit)
            {
                funds.SetBuildBucks(funds.BuildBucks - cost);
                try
                {   // Prevent bribing of dead Techs
                    if (tank.rootBlockTrans.GetComponent<TankBlock>())
                    {
                        return !tank.rootBlockTrans.GetComponent<TankBlock>().damage.AboutToDie;
                    }
                    else
                        return false;   // Root block does not exist
                }
                catch { return false; }
            }
            return false;
        }
        public static void TryDeclareBankruptcy(int Team)
        {
            EnemyBaseFunder funds = GetTeamFunder(Team);
            if (funds.IsNotNull())
            {
                funds.FlagBankrupt();
            }
        }


        // Utilities
        public static string GetActualNameDef(string name)
        {
            StringBuilder nameActual = new StringBuilder();
            foreach (char ch in name)
            {
                if (ch == RawTechLoader.turretChar)
                {
                    nameActual.Remove(nameActual.Length - 1, 1);
                    break;
                }
                else
                    nameActual.Append(ch);
            }
            return nameActual.ToString();
        }
        public static int FetchNearbyResourceCounts(int Team)
        {
            var funds = GetTeamFunder(Team);
            if (!(bool)funds)
                return 1;

            Vector3 tankPos = funds.Tank.boundsCentreWorldNoCheck;
            float MaxScanRange = AIGlobals.EnemyBaseMiningMaxRange;
            int InRange = 0;
            int run = AIECore.Minables.Count;
            for (int step = 0; step < run; step++)
            {
                var trans = AIECore.Minables.ElementAt(step);
                if (trans.isActive)
                {
                    if (!trans.GetComponent<ResourceDispenser>().IsDeactivated)
                    {
                        if (!trans.GetComponent<Damageable>().Invulnerable)
                        {
                            float temp = (trans.centrePosition - tankPos).sqrMagnitude;
                            if (MaxScanRange >= temp && temp != 0)
                                InRange++;
                            continue;
                        }
                    }
                }
                AIECore.Minables.Remove(trans);//it's invalid and must be destroyed
                step--;
                run--;
            }
            return InRange;
        }

        // Team requests
        public static void AllTeamTechsBuildRequest(int Team)
        {
            if (!BaseFunderManager.TeamsBuildRequested.Contains(Team))
                BaseFunderManager.TeamsBuildRequested.Add(Team);
        }
        public static void RequestFocusFireNPTs(Tank tank, Visible Target, RequestSeverity priority)
        {
            if (Target.IsNull() || tank.IsNull())
                return;
            if (Target.tank.IsNull())
                return;
            int Team = tank.Team;
            if (tank.IsAnchored)
                AIECore.AIMessage(tank, "Base " + tank.name + " is under attack!  Concentrate all fire on " + Target.tank.name + "!");
            else
                AIECore.AIMessage(tank, tank.name + ": Requesting assistance!  Cover me!");
            if (BaseFunderManager.targetingRequestsNPT.TryGetValue(Team, out var pair))
            {
                if (pair.Key < priority)
                    BaseFunderManager.targetingRequestsNPT[Team] = new KeyValuePair<RequestSeverity, Visible>(priority, Target);
            }
            else
                BaseFunderManager.targetingRequestsNPT.Add(Team, new KeyValuePair<RequestSeverity, Visible>(priority,Target));
        }
        public static void PoolTeamMoney(int Team)
        {
            EnemyBaseFunder funder = GetTeamFunder(Team);
            if (funder.IsNull())
            {
                return;
            }

            List<EnemyBaseFunder> baseFunders = GetTeamBaseFunders(Team);
            int moneyPool = 0;
            foreach (EnemyBaseFunder funds in baseFunders)
            {
                if (funder != funds)
                {
                    moneyPool += funds.BuildBucks;
                    funds.SetBuildBucks(0);
                }
            }
            DebugTAC_AI.Log("TACtical_AI: PoolTeamMoney - Team " + Team + " Pooled a total of " + moneyPool + " Build Bucks this time.");
            funder.AddBuildBucks(moneyPool);
        }
        public static bool EmergencyMoveMoney(EnemyBaseFunder funds)
        {
            if (!(bool)funds)
                return false;
            int Team = funds.Team;
            EnemyBaseFunder funder = GetTeamFunder(Team);
            if (funder.IsNull())
                return false;

            if (funder == funds)
            {   // Get the next in line
                int baseSize = 0;
                EnemyBaseFunder funderChange = funds;
                List<EnemyBaseFunder> baseFunders = GetTeamBaseFunders(Team);
                baseFunders.Remove(funds);
                foreach (EnemyBaseFunder fundC in baseFunders)
                {
                    int blockC = fundC.Tank.blockman.blockCount;
                    if (baseSize < blockC && funderChange != funds)
                    {
                        baseSize = blockC;
                        funderChange = fundC;
                    }
                }
                if (funderChange == funds)
                {
                    if (baseFunders.Count > 0)
                    {
                        funderChange = baseFunders.GetRandomEntry();
                        if (funderChange == funds)
                            return false;
                    }
                    else
                        return false;
                }

                // Transfer the BB
                funderChange.AddBuildBucks(funds.GetBuildBucksFromName());
                funds.SetBuildBucks(0);

                // Change positioning
                EnemyBases.Remove(funderChange);
                EnemyBases.Insert(0, funderChange);
                return true;
            }
            return true;
        }


        public class BaseFunderManager : MonoBehaviour
        {
            public static BaseFunderManager inst;

            public static List<int> TeamsBuildRequested = new List<int>();
            public static Dictionary<int, KeyValuePair<RequestSeverity, Visible>> targetingRequestsNPT = new Dictionary<int, KeyValuePair<RequestSeverity, Visible>>();
            private static readonly Dictionary<int, EnemyBaseFunder> TeamsUpdatedMainBase = new Dictionary<int, EnemyBaseFunder>();
            private float NextDelayedUpdateTime = 0;
            private const float delayedUpdateDelay = 6;

            public static void Initiate()
            {
                if (inst)
                    return;
                inst = new GameObject("BaseFunderManagerMain").AddComponent<BaseFunderManager>();
                DebugTAC_AI.Log("TACtical_AI: Initiated BaseFunderManager");
            }
            public static void DeInit()
            {
                if (!inst)
                    return;
                Destroy(inst.gameObject);
                inst = null;
                DebugTAC_AI.Log("TACtical_AI: DeInit BaseFunderManager");
            }
            public void Update()
            {
                if (ManPauseGame.inst.IsPaused)
                    return;

                if (AIGlobals.TurboAICheat && NextDelayedUpdateTime > Time.time + 1.1f)
                {
                    NextDelayedUpdateTime = Time.time + 1;
                }
                if (NextDelayedUpdateTime <= Time.time)
                {
                    DelayedUpdate();
                    NextDelayedUpdateTime = Time.time + delayedUpdateDelay;
                }
                RunBuildRequests();
                RunFocusFireRequests();
            }
            public void DelayedUpdate()
            {
                ManageBases();
                PeriodicBuildRequest();
            }

            private void ManageBases()
            {
                TeamsUpdatedMainBase.Clear();
                List<Tank> tonks = Singleton.Manager<ManTechs>.inst.CurrentTechs.ToList();
                foreach (Tank tech in tonks)
                {
                    if (!TeamsUpdatedMainBase.ContainsKey(tech.Team))
                    {
                        var funder = GetTeamFunder(tech.Team);
                        if (!(bool)funder)
                            continue;
                        var enemyMind = funder.GetComponent<EnemyMind>();
                        if ((bool)enemyMind)
                        {
                            TeamsUpdatedMainBase.Add(tech.Team, funder);
                        }
                    }
                }
                foreach (var item in TeamsUpdatedMainBase)
                {
                    var enemyMind = item.Value.GetComponent<EnemyMind>();
                    UpdateBaseOperations(enemyMind);
                    if (AIECore.RetreatingTeams.Contains(item.Key))
                    {
                        List<Tank> techs = AIECore.TankAIManager.TeamActiveMobileTechs(item.Key);
                        if (techs.Count == 0)
                            return;
                        float averageTechDMG = 0;
                        foreach (var item2 in techs.Select(x => x.GetHelperInsured()))
                        {
                            averageTechDMG += item2.DamageThreshold;
                        }
                        averageTechDMG /= techs.Count;
                        if (averageTechDMG <= AIGlobals.RetreatBelowTeamDamageThreshold)
                            AIECore.TeamRetreat(item.Key, false, true);
                    }
                }
            }
            private void RunBuildRequests()
            {
                if (TeamsBuildRequested.Count == 0)
                    return;

                foreach (int team in TeamsBuildRequested)
                {
                    DebugTAC_AI.Log("TACtical_AI: Team " + team + " has been issued a team-wide build request!");
                }
                foreach (Tank tech in Singleton.Manager<ManTechs>.inst.CurrentTechs)
                {
                    if (TeamsBuildRequested.Contains(tech.Team))
                    {
                        var helper = tech.GetComponent<AIECore.TankAIHelper>();
                        if (helper)
                        {
                            helper.PendingDamageCheck = true;
                            DebugTAC_AI.Log("TACtical_AI: Tech " + tech.name + " of " + tech.Team + " has acknowleged the request");
                        }
                        else if (tech.IsAnchored)
                        {
                            if (tech.GetComponent<EnemyBaseFunder>())
                                DebugTAC_AI.Log("TACtical_AI: Tech " + tech.name + " is a funder base but contains no DesignMemory?!?");
                        }
                    }
                }
                TeamsBuildRequested.Clear();
            }
            private void RunFocusFireRequests()
            {
                foreach (KeyValuePair<int, KeyValuePair<RequestSeverity, Visible>> request in targetingRequestsNPT)
                {
                    FocusFireRequest(request.Key, request.Value.Value, request.Value.Key);
                }
                targetingRequestsNPT.Clear();
            }
            private static void FocusFireRequest(int requestingTeam, Visible Target, RequestSeverity Priority)
            {
                try
                {
                    switch (Priority)
                    {
                        case RequestSeverity.ThinkMcFly:
                            List<Tank> techs = AIECore.TankAIManager.TeamActiveMobileTechsInCombat(requestingTeam);
                            if (techs.Count == 0)
                                return;

                            float averageTechDMG = 0;
                            foreach (var item in techs.Select(x => x.GetHelperInsured()))
                            {
                                averageTechDMG += item.DamageThreshold;
                            }
                            averageTechDMG /= techs.Count;
                            if (averageTechDMG < AIGlobals.RetreatBelowTeamDamageThreshold)
                            {
                                AIECore.TeamRetreat(requestingTeam, true, true);
                            }
                            else
                            {
                                foreach (Tank tech in AIECore.TankAIManager.GetNonEnemyTanks(requestingTeam))
                                {
                                    var helper = tech.GetComponent<AIECore.TankAIHelper>();
                                    var mind = tech.GetComponent<EnemyMind>();
                                    if ((bool)helper && (bool)mind)
                                    {
                                        if (helper.CanStoreEnergy())
                                            if (helper.GetEnergyPercent() < 0.9f)
                                                continue;
                                        if (helper.DamageThreshold > 15)
                                            continue;
                                        var baseFunds = tech.GetComponent<EnemyBaseFunder>();
                                        if (!mind.StartedAnchored)
                                        {
                                            mind.AIControl.Provoked = AIGlobals.ProvokeTime;
                                            if (!(bool)helper.lastEnemyGet)
                                                mind.GetRevengeOn(Target, true);
                                        }
                                        else if ((bool)baseFunds)
                                        {
                                            if (baseFunds.Purposes.Contains(BasePurpose.TechProduction))
                                            {
                                                if (TeamGlobalMobileTechCount(requestingTeam) < KickStart.EnemyTeamTechLimit && mind.TechMemor.HasFullHealth())
                                                    RBolts.BlowBolts(tech, mind);
                                            }
                                        }
                                    }
                                }
                            }
                            break;
                        case RequestSeverity.Warn:
                            foreach (Tank tech in AIECore.TankAIManager.GetNonEnemyTanks(requestingTeam))
                            {
                                var helper = tech.GetComponent<AIECore.TankAIHelper>();
                                var mind = tech.GetComponent<EnemyMind>();
                                if ((bool)helper && (bool)mind)
                                {
                                    if (helper.CanStoreEnergy())
                                        if (helper.GetEnergyPercent() < 0.9f)
                                            continue;
                                    if (helper.DamageThreshold > 15)
                                        continue;
                                    var baseFunds = tech.GetComponent<EnemyBaseFunder>();
                                    if (!mind.StartedAnchored)
                                    {
                                        mind.AIControl.Provoked = AIGlobals.ProvokeTime;
                                        if (!(bool)helper.lastEnemyGet)
                                            mind.GetRevengeOn(Target, true);
                                    }
                                    else if ((bool)baseFunds)
                                    {
                                        if (baseFunds.Purposes.Contains(BasePurpose.TechProduction))
                                        {
                                            if (TeamGlobalMobileTechCount(requestingTeam) < KickStart.EnemyTeamTechLimit && mind.TechMemor.HasFullHealth())
                                                RBolts.BlowBolts(tech, mind);
                                        }
                                    }
                                }
                            }
                            break;
                        case RequestSeverity.SameTeam:
                            foreach (Tank tech in AIECore.TankAIManager.GetTeamTanks(requestingTeam))
                            {
                                var helper = tech.GetComponent<AIECore.TankAIHelper>();
                                var mind = tech.GetComponent<EnemyMind>();
                                if ((bool)helper && (bool)mind)
                                {
                                    var baseFunds = tech.GetComponent<EnemyBaseFunder>();
                                    if (!mind.StartedAnchored)
                                    {
                                        mind.AIControl.Provoked = AIGlobals.ProvokeTime;
                                        if (!(bool)helper.lastEnemyGet)
                                            mind.GetRevengeOn(Target, true);
                                    }
                                    else if ((bool)baseFunds)
                                    {
                                        if (baseFunds.Purposes.Contains(BasePurpose.TechProduction))
                                        {
                                            if (TeamGlobalMobileTechCount(requestingTeam) < KickStart.EnemyTeamTechLimit && mind.TechMemor.HasFullHealth())
                                                RBolts.BlowBolts(tech, mind);
                                        }
                                    }
                                }
                            }
                            break;
                        case RequestSeverity.AllHandsOnDeck:
                            foreach (Tank tech in AIECore.TankAIManager.GetNonEnemyTanks(requestingTeam))
                            {
                                var helper = tech.GetComponent<AIECore.TankAIHelper>();
                                var mind = tech.GetComponent<EnemyMind>();
                                if ((bool)helper && (bool)mind)
                                {
                                    var baseFunds = tech.GetComponent<EnemyBaseFunder>();
                                    if (!mind.StartedAnchored)
                                    {
                                        mind.AIControl.Provoked = AIGlobals.ProvokeTime;
                                        if (!(bool)helper.lastEnemyGet)
                                            mind.GetRevengeOn(Target, true);
                                    }
                                    else if ((bool)baseFunds)
                                    {
                                        if (baseFunds.Purposes.Contains(BasePurpose.TechProduction))
                                        {
                                            if (TeamGlobalMobileTechCount(requestingTeam) < KickStart.EnemyTeamTechLimit && mind.TechMemor.HasFullHealth())
                                                RBolts.BlowBolts(tech, mind);
                                        }
                                    }
                                }
                            }
                            var reqTeam = ManEnemyWorld.GetTeam(requestingTeam);
                            if (reqTeam != null)
                            {
                                reqTeam.SetAttackMode(WorldPosition.FromScenePosition(Target.centrePosition).TileCoord);
                            }
                            break;
                        default:
                            break;
                    }
                   
                }
                catch { }
            }
            private void PeriodicBuildRequest()
            {
                if (EnemyBases.Count == 0)
                    return;
                TeamsUpdatedMainBase.Clear();

                foreach (EnemyBaseFunder funds in EnemyBases)
                {
                    if (!TeamsUpdatedMainBase.ContainsKey(funds.Team))
                    {
                        if (funds.Bankrupt)
                            TeamsUpdatedMainBase.Add(funds.Team, funds);
                    }
                }
                foreach (Tank tech in Singleton.Manager<ManTechs>.inst.CurrentTechs)
                {
                    if (TeamsUpdatedMainBase.ContainsKey(tech.Team))
                    {
                        var helper = tech.GetComponent<AIECore.TankAIHelper>();
                        if (helper)
                        {
                            helper.PendingDamageCheck = true;
                        }
                        else if (tech.IsAnchored)
                        {
                            if (tech.GetComponent<EnemyBaseFunder>())
                                DebugTAC_AI.Log("TACtical_AI: Tech " + tech.name + " is a funder base but contains no DesignMemory?!?");
                        }
                        //DebugTAC_AI.Log("TACtical_AI: Team " + Team + " has been issued a team-wide build request!");
                    }
                }
                //DebugTAC_AI.Log("TACtical_AI: BaseFunderManager - Sent worldwide build request");
            }
        }
        public class EnemyBaseFunder : MonoBehaviour
        {
            public Tank Tank;
            public HashSet<BasePurpose> Purposes = new HashSet<BasePurpose>();
            public int Team { get { return Tank.Team; } }
            public int BuildBucks { get { return buildBucks; } }
            private int buildBucks = 5000;
            public bool isHQ = false;

            /// <summary>
            /// If this Tech has a terminal, it can build any tech from the population
            /// </summary>
            public bool HasTerminal = false;
            public bool Bankrupt = false;

            public void Initiate(Tank tank)
            {
                Tank = tank;
                tank.TankRecycledEvent.Subscribe(OnRecycle);
                if (buildBucks == 5000)
                    buildBucks = GetBuildBucksFromName();
                EnemyBases.Add(this);
                PoolTeamMoney(tank.Team);
                DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + " Initiated EnemyBaseFunder");
            }
            public void SetupPurposes(RawTechTemplate type)
            {
                Purposes.Clear();
                Purposes = type.purposes;
            }
            public void OnRecycle(Tank tank)
            {
                // Make sure the money is safe

                //DebugTAC_AI.Log("TACtical_AI: Base " + tank.name + " scrambling money to next possible base"
                //    + " worked? " + EmergencyMoveMoney(this));
#if !STEAM
                AnimeAICompat.RespondToLoss(tank, ALossReact.Base);
#endif

                //DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + " Recycled EnemyBaseFunder");
                tank.TankRecycledEvent.Unsubscribe(OnRecycle);
                EnemyBases.Remove(this);
                Destroy(this);
            }
            public void AddBuildBucks(int toAdd)
            {
                SetBuildBucks(buildBucks + toAdd);
            }

            public void FlagBankrupt()
            {
                Bankrupt = true;
            }
            public void SetBuildBucks(int newVal, bool noNameChange = false)
            {
                if (!noNameChange)
                {
                    StringBuilder nameActual = new StringBuilder();
                    char lastIn = 'n';
                    bool doingBB = false;
                    foreach (char ch in name)
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
                    try
                    {
                        Tank.SetName(nameActual.ToString());
                        TankDescriptionOverlay overlay = (TankDescriptionOverlay)GUIAIManager.bubble.GetValue(Tank);
                        overlay.Update();
                    }
                    catch (Exception e) { DebugTAC_AI.LogError("TACtical_AI: SetBuildBucks - Unhandled error: " + e); }
                }
                buildBucks = newVal;
            }
            public int GetBuildBucksFromName(string name = "")
            {
                if (name == "")
                    name = Tank.name;
                StringBuilder funds = new StringBuilder();
                char lastIn = 'n';
                bool doingBB = false;
                foreach (char ch in name)
                {
                    if (!doingBB)
                    {
                        if (ch == '¥' && lastIn == '¥')
                        {
                            doingBB = true;
                        }
                        lastIn = ch;
                    }
                    else    // Get the base's "saved" funds
                    {
                        funds.Append(ch);
                    }
                }
                if (!doingBB)
                    return 0;
                string Funds = funds.ToString();
                if (Funds == " Inf")
                {
                    return -1;
                }
                bool worked = int.TryParse(Funds, out int Output);
                if (!worked)
                {
                    DebugTAC_AI.Log("TACtical_AI: BuildBucks corrupted for tech " + name + ", returning 0");
                    return 0;
                }
                return Output;
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


            // EnemyPurchase
            public bool PurchasePossible(int BBCost)
            {
                if (BBCost <= buildBucks)
                    return true;
                return false;
            }
            public bool TryMakePurchase(BlockTypes bloc)
            {
                int cost = Singleton.Manager<RecipeManager>.inst.GetBlockBuyPrice(bloc, true);
                if (PurchasePossible(cost))
                {
                    Bankrupt = false;
                    SetBuildBucks(buildBucks - cost);
                    return true;
                }
                else
                    FlagBankrupt();
                return false;
            }
            public bool TryMakePurchase(int Pay, int Team)
            {
                if (Pay <= GetTeamFunds(Team))
                {
                    var funds = GetTeamFunder(Team);
                    funds.SetBuildBucks(funds.BuildBucks - Pay);
                    return true;
                }
                return false;
            }

        }
        public static int GetBuildBucksFromNameExt(string name)
        {
            StringBuilder funds = new StringBuilder();
            char lastIn = 'n';
            bool doingBB = false;
            foreach (char ch in name)
            {
                if (!doingBB)
                {
                    if (ch == '¥' && lastIn == '¥')
                    {
                        doingBB = true;
                    }
                    lastIn = ch;
                }
                else    // Get the base's "saved" funds
                {
                    funds.Append(ch);
                }
            }
            if (!doingBB)
                return 0;
            string Funds = funds.ToString();
            if (Funds == " Inf")
            {
                return -1;
            }
            bool worked = int.TryParse(Funds, out int Output);
            if (!worked)
            {
                DebugTAC_AI.Log("TACtical_AI: BuildBucks corrupted for tech " + name + ", returning 0");
                return 0;
            }
            return Output;
        }

        // MAIN enemy bootup base handler
        public static bool SetupBaseAI(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind mind)
        {   // iterate if there's key characters in the name of the Tech
            string name = tank.name;
            bool DidFire = false;

            // Enemy base tech purchese spawn


            if (name == "TEST_BASE")
            {   //It's a base spawned by this mod
                thisInst.TryReallyAnchor();
                mind.StartedAnchored = true;
                DidFire = true;
            }
            BookmarkBuilder builder = tank.GetComponent<BookmarkBuilder>();
            if (builder)
            {
                mind.AllowInvBlocks = true;
                mind.AllowRepairsOnFly = true;
                mind.CommanderAttack = EAttackMode.Chase;
                mind.CommanderSmarts = EnemySmarts.IntAIligent;
                mind.CommanderBolts = EnemyBolts.MissionTrigger;
                thisInst.InsureTechMemor("SetupBaseAI - BookmarkBuilder", false);
                mind.TechMemor.SetupForNewTechConstruction(thisInst, builder.blueprint);
                tank.MainCorps = new List<FactionSubTypes> { KickStart.CorpExtToCorp(builder.faction) };
                if (builder.faction != FactionTypesExt.NULL)
                {
                    tank.MainCorps = new List<FactionSubTypes> { KickStart.CorpExtToCorp(builder.faction) };
                    mind.MainFaction = builder.faction;
                    //DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + " set faction " + tank.GetMainCorp().ToString());
                }
                if (builder.instant)
                {
                    AIERepair.Turboconstruct(tank, mind.TechMemor, true);
                    RCore.BlockSetEnemyHandling(tank, mind, true);
                    RCore.RandomSetMindAttack(mind, tank);
                }

                if (builder.unprovoked)
                {
                    mind.CommanderAlignment = EnemyStanding.SubNeutral;
                }
                mind.TechMemor.MakeMinersMineUnlimited();
                DidFire = true;
                //DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + " is ready to roll!  " + mind.EvilCommander.ToString() + " based " + mind.CommanderAlignment.ToString() + " with attitude " + mind.CommanderAttack.ToString() + " | Mind " + mind.CommanderMind.ToString() + " | Smarts " + mind.CommanderSmarts.ToString() + " inbound!");
            }

            if (name.Contains(" ¥¥"))
            {   // Main base
                if (name.Contains("#"))
                {
                    //It's not a base
                    if (tank.IsAnchored)
                    {   // It's a fragment of the base - prevent unwanted mess from getting in the way
                        if (ManNetwork.IsHost)
                            RecycleTechToTeam(tank);
                        return true;
                    }

                    StringBuilder nameNew = new StringBuilder();
                    char lastIn = 'n';
                    foreach (char ch in name)
                    {
                        if (ch == '¥' && lastIn == '¥')
                        {
                            nameNew.Remove(nameNew.Length - 2, 2);
                            break;
                        }
                        else
                            nameNew.Append(ch);
                        lastIn = ch;
                    }
                    nameNew.Append(" Minion");
                    tank.SetName(nameNew.ToString());
                    // it's a minion of the base
                    if (mind.CommanderAttack == EAttackMode.Safety)
                        mind.CommanderAttack = EAttackMode.Chase;

                    // Charge the new Tech and send it on it's way!
                    RawTechLoader.ChargeAndClean(tank);
                    tank.visible.Teleport(tank.boundsCentreWorldNoCheck + (tank.rootBlockTrans.forward * tank.blockBounds.size.magnitude), tank.rootBlockTrans.rotation, true, false);
                    if (Singleton.Manager<ManVisible>.inst.AllTrackedVisibles.ToList().Exists(delegate (TrackedVisible cand) { return cand.visible == tank.visible; }))
                    {
                        DebugTAC_AI.Assert(true, "TACtical_AI: ASSERT - " + tank.name + " was not properly inserted into the TrackedVisibles list and will not function properly!");
                        RawTechLoader.TrackTank(tank);
                    }
                }
                else
                {
                    thisInst.InsureTechMemor("SetupBaseAI - Base", false);

                    var funds = tank.gameObject.GetComponent<EnemyBaseFunder>(); 
                    if (funds.IsNull())
                    {
                        funds = tank.gameObject.AddComponent<EnemyBaseFunder>();
                        funds.Initiate(tank);
                    }
                    funds.SetBuildBucks(funds.GetBuildBucksFromName(name), true);

                    try
                    {
                        string baseName = EnemyBaseFunder.GetActualName(name);
                        SpawnBaseTypes type = RawTechLoader.GetEnemyBaseTypeFromName(baseName);
                        bool activated = false;
                        RawTechTemplate BT;
                        if (type == SpawnBaseTypes.NotAvail)
                        {
                            BT = RawTechLoader.GetExtEnemyBaseFromName(baseName);
                            if (BT != null)
                            {
                                SetupBaseType(BT, mind);
                                funds.SetupPurposes(BT);
                                DebugTAC_AI.Log("TACtical_AI: Registered EXTERNAL base " + baseName);
                                mind.TechMemor.SetupForNewTechConstruction(thisInst, BT.savedTech);
                                tank.MainCorps.Add(KickStart.CorpExtToCorp(BT.faction));
                                activated = true;
                            }
                        }
                        if (!activated)
                        {
                            BT = RawTechLoader.GetBaseTemplate(type);
                            SetupBaseType(BT, mind);
                            funds.SetupPurposes(BT);
                            DebugTAC_AI.Log("TACtical_AI: Registered base " + baseName + " | type " + type.ToString());
                            mind.TechMemor.SetupForNewTechConstruction(thisInst, BT.savedTech);
                            tank.MainCorps.Add(RawTechLoader.GetMainCorp(type));
                        }
                        mind.TechMemor.MakeMinersMineUnlimited();
                    }
                    catch { }
                    if (!tank.IsAnchored)
                        thisInst.TryReallyAnchor();
                    mind.StartedAnchored = true;
                    AllTeamTechsBuildRequest(tank.Team);
                    DidFire = true;
                }
            }
            else if (name.Contains(RawTechLoader.turretChar))
            {   // Defense
                if (name.Contains("#"))
                {
                    if (tank.IsAnchored)
                    {   // It's a fragment of the base - prevent unwanted mess from getting in the way
                        if (ManNetwork.IsHost)
                            RecycleTechToTeam(tank);
                        return true;
                    }
                    //It's not a base
                    StringBuilder nameNew = new StringBuilder();
                    nameNew.Append(GetActualNameDef(name));
                    nameNew.Append(" Minion");
                    tank.SetName(nameNew.ToString());
                    // it's a minion of the base
                }
                else
                {
                    thisInst.InsureTechMemor("SetupBaseAI - Defense", false);
                    try
                    {
                        string defName = GetActualNameDef(name);
                        SpawnBaseTypes type = RawTechLoader.GetEnemyBaseTypeFromName(defName);
                        bool activated = false;
                        RawTechTemplate BT;
                        if (type == SpawnBaseTypes.NotAvail)
                        {
                            BT = RawTechLoader.GetExtEnemyBaseFromName(defName);
                            if (BT != null)
                            {
                                SetupBaseType(BT, mind);
                                //DebugTAC_AI.Log("TACtical_AI: Registered EXTERNAL base defense " + defName);
                                mind.TechMemor.SetupForNewTechConstruction(thisInst, BT.savedTech);
                                tank.MainCorps.Add(KickStart.CorpExtToCorp(BT.faction));
                                activated = true;
                            }
                        }
                        if (!activated)
                        {
                            BT = RawTechLoader.GetBaseTemplate(type);
                            SetupBaseType(BT, mind);
                            mind.TechMemor.SetupForNewTechConstruction(thisInst, BT.savedTech);
                            tank.MainCorps.Add(RawTechLoader.GetMainCorp(type));
                        }
                    }
                    catch { }
                    if (!tank.IsAnchored)
                        thisInst.TryReallyAnchor();
                    mind.StartedAnchored = true;
                    DidFire = true;
                }
            }

            return DidFire;
        }

        public static void SetupBaseType(RawTechTemplate BT, EnemyMind mind)
        {  
            if (BT.purposes.Contains(BasePurpose.Headquarters))
            {
                mind.StartedAnchored = true;
                mind.AllowInvBlocks = true;
                mind.AllowRepairsOnFly = true;
                mind.InvertBullyPriority = true;
                mind.EvilCommander = EnemyHandling.Stationary;
                mind.CommanderAttack = EAttackMode.Strong;
                mind.CommanderMind = EnemyAttitude.Homing;
                mind.CommanderSmarts = EnemySmarts.IntAIligent;
                mind.CommanderBolts = EnemyBolts.AtFull;
            }
            else if (BT.purposes.Contains(BasePurpose.Harvesting))
            {
                mind.StartedAnchored = true;
                mind.AllowInvBlocks = true;
                mind.AllowRepairsOnFly = true;
                mind.EvilCommander = EnemyHandling.Stationary;
                mind.CommanderMind = EnemyAttitude.Default;
                mind.CommanderSmarts = EnemySmarts.IntAIligent;
                mind.CommanderAttack = EAttackMode.Chase;
                mind.CommanderBolts = EnemyBolts.AtFull;
            }
            else if (BT.purposes.Contains(BasePurpose.TechProduction))
            {
                mind.StartedAnchored = true;
                mind.AllowInvBlocks = true;
                mind.AllowRepairsOnFly = true;
                mind.EvilCommander = EnemyHandling.Stationary;
                mind.CommanderMind = EnemyAttitude.Default;
                mind.CommanderSmarts = EnemySmarts.IntAIligent;
                mind.CommanderAttack = EAttackMode.Chase;
                mind.CommanderBolts = EnemyBolts.AtFullOnAggro;
            }
            else
            {
                mind.StartedAnchored = true;
                mind.AllowInvBlocks = true;
                mind.AllowRepairsOnFly = true;
                mind.EvilCommander = EnemyHandling.Stationary;
                mind.CommanderMind = EnemyAttitude.Default;
                mind.CommanderSmarts = EnemySmarts.IntAIligent;
                mind.CommanderAttack = EAttackMode.Chase;
                mind.CommanderBolts = EnemyBolts.AtFull;
            }
        }


        // Base Operations
        public static void UpdateBaseOperations(EnemyMind mind)
        {
            try
            {
                var funder = mind.GetComponent<EnemyBaseFunder>();
                if (funder && (bool)mind.TechMemor)
                {
                    if (!KickStart.AllowEnemiesToStartBases && !mind.Tank.FirstUpdateAfterSpawn)
                    {
                        SpecialAISpawner.Eradicate(mind.Tank);
                        return;
                    }
                    if (ManNetwork.IsNetworked)
                    {   // Because Autominers are disabled
                        int addBucks = 0;
                        foreach (var item in GetTeamBaseFunders(mind.Tank.Team))
                        {
                            addBucks += AIGlobals.MPEachBaseProfits * (40 / KickStart.AIClockPeriod);
                        }
                        if (addBucks > 0)
                            funder.AddBuildBucks(addBucks);
                    }

                    // Bribe
                    if ((bool)mind.AIControl.lastEnemyGet)
                    {
                        Tank lastTankGrab = mind.AIControl.lastEnemyGet.tank;
                        if (lastTankGrab.IsPopulation)
                        {
                            int team = mind.Tank.Team;
                            if (TryBribeTech(lastTankGrab, team))
                            {
                                try
                                {
                                    if (KickStart.DisplayEnemyEvents)
                                    {
                                        WorldPosition pos2 = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(lastTankGrab.visible);
                                        if (AIGlobals.IsFriendlyBaseTeam(team))
                                        AIGlobals.PopupAllyInfo("Bribed!", pos2);
                                        else if (AIGlobals.IsNonAggressiveTeam(team))
                                            AIGlobals.PopupNeutralInfo("Bribed!", pos2);
                                        else
                                            AIGlobals.PopupEnemyInfo("Bribed!", pos2);

                                        try
                                        {
                                            Singleton.Manager<UIMPChat>.inst.AddMissionMessage("Tech " + lastTankGrab.name + " was bribed by " + mind.Tank.name + "!");
                                        }
                                        catch { }
                                    }
                                    DebugTAC_AI.Log("TACtical_AI: Tech " + lastTankGrab.name + " was purchased by " + mind.Tank.name + ".");
                                }
                                catch { }
                                lastTankGrab.SetTeam(mind.Tank.Team);
                            }
                        }
                    }
                    if (!mind.AIControl.PendingDamageCheck && UnityEngine.Random.Range(1, 100) <= AIGlobals.BaseExpandChance + (GetTeamFunds(mind.Tank.Team) / 10000))
                        ImTakingThatExpansion(mind, mind.GetComponent<EnemyBaseFunder>());
                    //if (UnityEngine.Random.Range(1, 100) < 7)
                    //{
                    //    RBases.AllTeamTechsBuildRequest(mind.Tank.Team);
                    //}
                }
            }
            catch { }
        }
        public static void ImTakingThatExpansion(EnemyMind mind, EnemyBaseFunder funds)
        {   // Expand the base!
            if (!KickStart.AllowEnemyBaseExpand && !mind.Tank.FirstUpdateAfterSpawn)
            {
                RemoveAllBases(mind, funds);
                return;
            }
            try
            {
                //DebugTAC_AI.Log("TACtical_AI: ImTakingThatExpansion - Call for " + mind.name);
                if (AIGlobals.IsAttract)
                    return; // no branching

                if (AIGlobals.TurboAICheat && funds.BuildBucks < AIGlobals.MinimumBBToTryExpand * 25)
                    funds.AddBuildBucks(AIGlobals.MinimumBBToTryExpand);

                if (funds.BuildBucks < AIGlobals.MinimumBBToTryExpand)
                    return; // Reduce expansion lag

                Tank tech = mind.AIControl.tank;


                FactionLevel lvl = RawTechLoader.TryGetPlayerLicenceLevel();
                int grade = 99;
                try
                {
                    if (!SpecialAISpawner.CreativeMode)
                        grade = Singleton.Manager<ManLicenses>.inst.GetCurrentLevel(KickStart.CorpExtToCorp(mind.MainFaction));
                }
                catch { }

                int Cost = GetTeamFunds(tech.Team);
                if (TeamGlobalMakerBaseCount(tech.Team) >= KickStart.MaxBasesPerTeam)
                {
                    TryFreeUpBaseSlots(mind, lvl, funds);
                    if (TeamGlobalMobileTechCount(tech.Team) < KickStart.EnemyTeamTechLimit)
                    {
                        BaseConstructTech(mind, tech, lvl, funds, grade, Cost);
                    }
                    else if (AIGlobals.NoBuildWhileInCombat)
                        BaseUpgradeTechs(mind, tech, lvl, funds, GetTeamBaseFunders(tech.Team), grade, Cost);
                    return;
                }

                Visible lastEnemySet = mind.AIControl.lastEnemyGet;
                if (!lastEnemySet)
                {
                    ExpandBasePeaceful(mind, lvl, funds, grade, Cost);
                    return;
                }

                if (AIEBases.FindNewExpansionBase(tech, lastEnemySet.tank.boundsCentreWorld, out Vector3 pos))
                {   // Try spawning defense
                    List<EnemyBaseFunder> funders = GetTeamBaseFunders(tech.Team);
                    BaseTerrain Terra = RawTechLoader.GetTerrain(pos);
                    BasePurpose reason = PriorityDefense(mind, lvl, funds, funders);

                    DebugTAC_AI.Log("TACtical_AI: ImTakingThatExpansion - Team " + tech.Team + ": That expansion is mine!  Type: " + reason + ", Faction: " + mind.MainFaction);
                    if (RawTechLoader.ShouldUseCustomTechs(out List<int> valid, mind.MainFaction, lvl, reason, Terra, false, grade, maxPrice: Cost))
                    {
                        int spawnIndex = valid.GetRandomEntry();
                        if (spawnIndex == -1)
                        {
                            DebugTAC_AI.Log("TACtical_AI: ShouldUseCustomTechs(ImTakingThatExpansion) - Critical error on call - Expected a Custom Local Tech to exist but found none!");
                        }
                        else
                        {
                            RawTechTemplate BTemp = TempManager.ExternalEnemyTechsAll[spawnIndex];
                            RawTechLoader.SpawnBaseExpansion(tech, pos, tech.Team, BTemp);
                            return;
                        }
                    }
                    SpawnBaseTypes type = RawTechLoader.GetEnemyBaseType(mind.MainFaction, lvl, reason, Terra, maxGrade: grade, maxPrice: Cost);
                    if (RawTechLoader.IsFallback(type))
                        return;

                    if (RawTechLoader.SpawnBaseExpansion(tech, pos, tech.Team, type))
                    {
                        DebugTAC_AI.Log("TACtical_AI: ImTakingThatExpansion - Expanded");
                    }
                    else
                        DebugTAC_AI.Log("TACtical_AI: SpawnBaseExpansion - Team " + tech.Team + ": Failiure on expansion");
                }
                else
                {   // Get new base location to expand
                    if (EmergencyMoveMoney(funds))
                    {
                        if (TeamGlobalMobileTechCount(tech.Team) < KickStart.EnemyTeamTechLimit)
                        {
                            BaseConstructTech(mind, tech, lvl, funds, grade, Cost);
                        }
                        else
                            BaseUpgradeTechs(mind, tech, lvl, funds, GetTeamBaseFunders(tech.Team), grade, Cost);
                    }
                }
            }
            catch
            {
                DebugTAC_AI.Log("TACtical_AI: ImTakingThatExpansion - game is being stubborn");
            }
        }
        public static void ExpandBasePeaceful(EnemyMind mind, FactionLevel lvl, EnemyBaseFunder funds, int grade, int Cost)
        {   // Expand the base!
            try
            {
                Tank tech = mind.Tank;
                BaseTerrain Terra;
                BasePurpose reason;

                List<EnemyBaseFunder> funders = GetTeamBaseFunders(tech.Team);

                if (KickStart.AllowEnemiesToMine && InsureHarvester(mind, lvl, funds, funders))
                {
                    DebugTAC_AI.Log("TACtical_AI: ImTakingThatExpansion - Building harvester");
                }
                if (AIEBases.TryFindExpansionLocationGrid(tech.boundsCentreWorldNoCheck, tech.boundsCentreWorldNoCheck, out Vector3 pos2))
                {   // Try spawning base extensions
                    Terra = RawTechLoader.GetTerrain(pos2);
                    reason = PickBuildBasedOnPriorities(mind, lvl, funds, funders);
                    DebugTAC_AI.Log("TACtical_AI: ImTakingThatExpansion - Team " + tech.Team + ": That expansion is mine!  Type: " + reason + ", Faction: " + mind.MainFaction);
                    if (RawTechLoader.ShouldUseCustomTechs(out List<int> valid, mind.MainFaction, lvl, reason, Terra, false, grade, maxPrice: Cost))
                    {
                        int spawnIndex = valid.GetRandomEntry();
                        if (spawnIndex == -1)
                        {
                            DebugTAC_AI.Log("TACtical_AI: ShouldUseCustomTechs(ImTakingThatExpansion) - Critical error on call - Expected a Custom Local Tech to exist but found none!");
                        }
                        else
                        {
                            RawTechTemplate BTemp = TempManager.ExternalEnemyTechsAll[spawnIndex];
                            RawTechLoader.SpawnBaseExpansion(tech, pos2, tech.Team, BTemp);
                            return;
                        }
                    }
                    DebugTAC_AI.Log("TACtical_AI: ImTakingThatExpansion - was given " + Terra + " | " + grade + " | " + Cost);
                    SpawnBaseTypes type = RawTechLoader.GetEnemyBaseType(mind.MainFaction, lvl, reason, Terra, maxGrade: grade, maxPrice: Cost);
                    if (RawTechLoader.IsFallback(type))
                        return;
                    if (RawTechLoader.SpawnBaseExpansion(tech, pos2, tech.Team, type))
                    {
                        DebugTAC_AI.Log("TACtical_AI: ImTakingThatExpansion - Expanded");
                    }
                    else
                        DebugTAC_AI.Log("TACtical_AI: SpawnBaseExpansion - Team " + tech.Team + ": Failiure on expansion");
                }
                else
                {   // Get new base location to expand
                    if (EmergencyMoveMoney(funds))
                    {
                        if (TeamGlobalMobileTechCount(tech.Team) < KickStart.EnemyTeamTechLimit)
                        {
                            BaseConstructTech(mind, tech, lvl, funds, grade, Cost);
                        }
                        else
                            BaseUpgradeTechs(mind, tech, lvl, funds, funders, grade, Cost);
                    }
                }
            }
            catch (Exception e)
            {
                DebugTAC_AI.Log("TACtical_AI: ExpandBasePeaceful - game is being stubborn " + e);
            }
        }

        public static void BaseConstructTech(EnemyMind mind, Tank tech, FactionLevel lvl, EnemyBaseFunder funds, int grade, int Cost)
        {   // Expand the base!
            try
            {
                if (AIEBases.FindNewExpansionBase(tech, mind.AIControl.lastDestinationCore, out Vector3 pos))
                {
                    BaseTerrain terra;
                    if (AIEPathing.AboveTheSea(pos))
                        terra = BaseTerrain.Sea;
                    else
                        terra = BaseTerrain.AnyNonSea;

                    if (RawTechLoader.ShouldUseCustomTechs(out List<int> valid, mind.MainFaction, lvl, BasePurpose.NotStationary, terra, false, grade, maxPrice: Cost))
                    {
                        int spawnIndex = valid.GetRandomEntry();
                        if (spawnIndex == -1)
                        {
                            DebugTAC_AI.Log("TACtical_AI: ShouldUseCustomTechs(ImTakingThatExpansion -EnemyBaseWorld) - Critical error on call - Expected a Custom Local Tech to exist but found none!");
                        }
                        else
                        {
                            RawTechTemplate BTemp = TempManager.ExternalEnemyTechsAll[spawnIndex];
                            RawTechLoader.SpawnTechFragment(pos, funds.Team, BTemp);
                            //DebugTAC_AI.Log("TACtical_AI: BaseConstructTech - Team " + EP.Team + ": Built new mobile tech " + BTemp.techName);
                            return;
                        }
                    }
                    SpawnBaseTypes type = RawTechLoader.GetEnemyBaseType(mind.MainFaction, lvl, BasePurpose.NotStationary, terra, maxGrade: grade, maxPrice: Cost);
                    if (RawTechLoader.IsFallback(type))
                        return;
                    RawTechLoader.SpawnTechFragment(pos, funds.Team, RawTechLoader.GetBaseTemplate(type));
                }
            }
            catch (Exception e)
            {
                DebugTAC_AI.Log("TACtical_AI: BaseConstructTech - game is being stubborn " + e);
            }
        }

        private static List<RawTechTemplate> BTs = new List<RawTechTemplate>();
        public static void BaseUpgradeTechs(EnemyMind mind, Tank tech, FactionLevel lvl, EnemyBaseFunder funds, List<EnemyBaseFunder> funders, int grade, int Cost)
        {   // Upgrade the Techs!
            try
            {
                List<Tank> mobileTechs = AIECore.TankAIManager.TeamActiveMobileTechs(tech.Team);
                Tank toUpgrade = null;
                bool shouldChangeHarvesters = GetCountOfPurpose(BasePurpose.HasReceivers, funders) == 0;
                foreach (var item in mobileTechs)
                {
                    var helper = item.GetComponent<AIECore.TankAIHelper>();
                    if (!helper.PendingDamageCheck && !helper.lastEnemyGet && (shouldChangeHarvesters || !RCore.IsHarvester(item.blockman)))
                    {
                        toUpgrade = item;
                        break;
                    }
                }
                if (toUpgrade != null)
                {
                    if (AIEBases.FindNewExpansionBase(tech, mind.AIControl.lastDestinationCore, out Vector3 pos))
                    {
                        BaseTerrain terra;
                        if (AIEPathing.AboveTheSea(pos))
                            terra = BaseTerrain.Sea;
                        else
                            terra = BaseTerrain.AnyNonSea;

                        Vector3 posTech = toUpgrade.WorldCenterOfMass;
                        RawTechTemplate BT;
                        BTs.Clear();
                        if (RawTechLoader.ShouldUseCustomTechs(out List<int> valid, mind.MainFaction, lvl, BasePurpose.NotStationary, terra, false, grade, maxPrice: Cost))
                        {
                            if (valid.Count == 0)
                            {
                                DebugTAC_AI.Log("TACtical_AI: ShouldUseCustomTechs(ImTakingThatExpansion -EnemyBaseWorld) - Critical error on call - Expected a Custom Local Tech to exist but found none!");
                            }
                            else
                            {
                                foreach (var item in valid)
                                {
                                    BTs.Add(TempManager.ExternalEnemyTechsAll[item]);
                                }
                                if (RawTechLoader.FindNextBest(out BT, BTs, RawTechTemplate.GetBBCost(toUpgrade)))
                                {
                                    RecycleTechToTeam(toUpgrade);
                                    RawTechLoader.SpawnTechFragment(posTech, funds.Team, BT);
                                    //DebugTAC_AI.Log("TACtical_AI: BaseConstructTech - Team " + EP.Team + ": Built new mobile tech " + BTemp.techName);
                                    return;
                                }
                            }
                        }
                        List<SpawnBaseTypes> types = RawTechLoader.GetEnemyBaseTypes(mind.MainFaction, lvl, BasePurpose.NotStationary, terra, maxGrade: grade, maxPrice: Cost);

                        foreach (var item in types)
                        {
                            if (!RawTechLoader.IsFallback(item))
                                BTs.Add(RawTechLoader.GetBaseTemplate(item));
                        }
                        if (BTs.Count == 0)
                            return;
                        if (RawTechLoader.FindNextBest(out BT, BTs, RawTechTemplate.GetBBCost(toUpgrade)))
                        {
                            RecycleTechToTeam(toUpgrade);
                            RawTechLoader.SpawnTechFragment(posTech, funds.Team, BT);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                DebugTAC_AI.Log("TACtical_AI: BaseConstructTech - game is being stubborn " + e);
            }
        }


        public static void ExpandBaseLegacy(EnemyMind mind, FactionLevel lvl, EnemyBaseFunder funds, int grade, int Cost)
        {   // Expand the base!
            try
            {
                Tank tech = mind.Tank;
                BaseTerrain Terra;
                BasePurpose reason;

                List<EnemyBaseFunder> funders = GetTeamBaseFunders(tech.Team);

                if (AIEBases.TryFindExpansionLocationCorner(tech, tech.boundsCentreWorldNoCheck, out Vector3 pos))
                {   // Try spawning defense
                    Terra = RawTechLoader.GetTerrain(pos);
                    reason = PickBuildBasedOnPriorities(mind, lvl, funds, funders);
                    DebugTAC_AI.Log("TACtical_AI: ImTakingThatExpansion - Team " + tech.Team + ": That expansion is mine!  Type: " + reason + ", Faction: " + mind.MainFaction);
                    if (RawTechLoader.ShouldUseCustomTechs(out List<int> valid, mind.MainFaction, lvl, reason, Terra, false, grade, maxPrice: Cost))
                    {
                        int spawnIndex = valid.GetRandomEntry();
                        if (spawnIndex == -1)
                        {
                            DebugTAC_AI.Log("TACtical_AI: ShouldUseCustomTechs(ImTakingThatExpansion) - Critical error on call - Expected a Custom Local Tech to exist but found none!");
                        }
                        else
                        {
                            RawTechTemplate BTemp = TempManager.ExternalEnemyTechsAll[spawnIndex];
                            RawTechLoader.SpawnBaseExpansion(tech, pos, tech.Team, BTemp);
                            return;
                        }
                    }
                    SpawnBaseTypes type = RawTechLoader.GetEnemyBaseType(mind.MainFaction, lvl, reason, Terra, maxGrade: grade, maxPrice: Cost);
                    if (RawTechLoader.IsFallback(type))
                        return;
                    if (RawTechLoader.SpawnBaseExpansion(tech, pos, tech.Team, type))
                    {
                        DebugTAC_AI.Log("TACtical_AI: ImTakingThatExpansion - Expanded");
                    }
                    else
                        DebugTAC_AI.Log("TACtical_AI: SpawnBaseExpansion - Team " + tech.Team + ": Failiure on expansion");
                }
                else if (AIEBases.TryFindExpansionLocationDirect(tech, tech.boundsCentreWorldNoCheck, out Vector3 pos2))
                {   // Try spawning base extensions
                    Terra = RawTechLoader.GetTerrain(pos2);
                    reason = PickBuildNonDefense(mind);
                    DebugTAC_AI.Log("TACtical_AI: ImTakingThatExpansion(2) - Team " + tech.Team + ": That expansion is mine!  Type: " + reason + ", Faction: " + mind.MainFaction);
                    if (RawTechLoader.ShouldUseCustomTechs(out List<int> valid, mind.MainFaction, lvl, reason, Terra, false, grade, maxPrice: Cost))
                    {
                        int spawnIndex = valid.GetRandomEntry();
                        if (spawnIndex == -1)
                        {
                            DebugTAC_AI.Log("TACtical_AI: ShouldUseCustomTechs(ImTakingThatExpansion) - Critical error on call - Expected a Custom Local Tech to exist but found none!");
                        }
                        else
                        {
                            RawTechTemplate BTemp = TempManager.ExternalEnemyTechsAll[spawnIndex];
                            RawTechLoader.SpawnBaseExpansion(tech, pos2, tech.Team, BTemp);
                            return;
                        }
                    }
                    DebugTAC_AI.Log("TACtical_AI: ImTakingThatExpansion - was given " + Terra + " | " + grade + " | " + Cost);
                    SpawnBaseTypes type = RawTechLoader.GetEnemyBaseType(mind.MainFaction, lvl, reason, Terra, maxGrade: grade, maxPrice: Cost);
                    if (RawTechLoader.IsFallback(type))
                        return;
                    if (RawTechLoader.SpawnBaseExpansion(tech, pos2, tech.Team, type))
                    {
                        DebugTAC_AI.Log("TACtical_AI: ImTakingThatExpansion - Expanded");
                    }
                    else
                        DebugTAC_AI.Log("TACtical_AI: SpawnBaseExpansion - Team " + tech.Team + ": Failiure on expansion");
                }
                else
                {   // Get new base location to expand
                    EmergencyMoveMoney(funds);
                }
            }
            catch (Exception e)
            {
                DebugTAC_AI.Log("TACtical_AI: ExpandBasePeaceful - game is being stubborn " + e);
            }
        }


        public static bool InsureHarvester(EnemyMind mind, FactionLevel lvl, EnemyBaseFunder funds, List<EnemyBaseFunder> funders)
        {   // Make sure at least one harvester is on scene
            try
            {
                int harvestBases = GetCountOfPurpose(BasePurpose.HasReceivers, funders);
                if (harvestBases > 0)
                {
                    foreach (var item in ManTechs.inst.IterateTechs())
                    {
                        var mindItem = item.GetComponent<EnemyMind>();
                        if (mindItem && !item.GetComponent<EnemyBaseFunder>())
                        {
                            if (mindItem.CommanderMind == EnemyAttitude.Miner)
                            {
                                harvestBases--;
                                if (harvestBases == 0)
                                    return false;
                            }
                        }
                    }
                    if (AIEBases.TryFindExpansionLocationGrid(mind.Tank.boundsCentreWorld, mind.Tank.trans.forward * 128, out Vector3 pos))
                    {
                        SpawnBaseTypes SBT = RawTechLoader.GetEnemyBaseType(mind.MainFaction, lvl, new HashSet<BasePurpose> { BasePurpose.Harvesting, BasePurpose.NotStationary }, BaseTerrain.AnyNonSea, maxPrice: funds.BuildBucks);
                        if (RawTechLoader.IsFallback(SBT))
                        {
                            DebugTAC_AI.Log("TACtical_AI: InsureHarvester - There are no harvesters for FactionTypesExt " + mind.MainFaction + ", trying fallbacks");

                            FactionTypesExt FTE = KickStart.CorpExtToVanilla(mind.MainFaction);
                            SBT = RawTechLoader.GetEnemyBaseType(FTE, lvl, new HashSet<BasePurpose> { BasePurpose.Harvesting, BasePurpose.NotStationary }, BaseTerrain.AnyNonSea, maxPrice: funds.BuildBucks);
                            if (RawTechLoader.IsFallback(SBT))
                            {
                                DebugTAC_AI.Log("TACtical_AI: InsureHarvester - There are no harvesters for Vanilla Faction " + FTE + ", using GSO");

                                SBT = RawTechLoader.GetEnemyBaseType(FactionTypesExt.GSO, lvl, new HashSet<BasePurpose> { BasePurpose.Harvesting, BasePurpose.NotStationary }, BaseTerrain.AnyNonSea, maxPrice: funds.BuildBucks);
                            }
                        }
                        RawTechLoader.SpawnTechFragment(pos, funds.Team, RawTechLoader.GetBaseTemplate(SBT));
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                DebugTAC_AI.LogError("TACtical_AI: InsureHarvester - Error " + e);
            }
            return false;
        }
        public static void TryFreeUpBaseSlots(EnemyMind mind, FactionLevel lvl, EnemyBaseFunder funds)
        {   // Remove uneeeded garbage
            try
            {
                Tank tech = mind.AIControl.tank;
                int TeamBaseCount = TeamGlobalMakerBaseCount(tech.Team);
                bool RemoveReceivers = FetchNearbyResourceCounts(tech.Team) == 0;
                bool RemoveSpenders = GetTeamFunds(tech.Team) < CheapestAutominerPrice(mind, lvl) / 2;
                bool ForceRemove = TeamBaseCount > KickStart.MaxBasesPerTeam;

                int attempts = 1;
                int step = 0;

                if (ForceRemove)
                {
                    attempts = KickStart.MaxBasesPerTeam - TeamBaseCount;
                }

                List<EnemyBaseFunder> basesSorted = EnemyBases;
                // Remove the lower-end first
                foreach (EnemyBaseFunder fund in basesSorted.OrderBy((F) => F.Tank.blockman.blockCount))
                {
                    if (fund.Team == tech.Team && fund != funds)
                    {
                        if (ForceRemove)
                        {
                            RecycleTechToTeam(fund.Tank);
                            if (step >= attempts)
                                return;
                        }
                        if (RemoveReceivers && fund.Purposes.Contains(BasePurpose.HasReceivers) && !fund.Purposes.Contains(BasePurpose.Autominer))
                        {
                            RecycleTechToTeam(fund.Tank);
                            if (step >= attempts)
                                return;
                        }
                        if (RemoveSpenders && !fund.GetComponent<AIECore.TankAIHelper>().PendingDamageCheck
                            && fund.Purposes.Contains(BasePurpose.TechProduction) && !fund.Purposes.Contains(BasePurpose.Harvesting))
                        {
                            RecycleTechToTeam(fund.Tank);
                            if (step >= attempts)
                                return;
                        }
                        step++;
                    }
                }
                //if (removeAll) // Final coffin nail
                //    SpecialAISpawner.Eradicate(mind.Tank);
            }
            catch (Exception e)
            {
                DebugTAC_AI.Log("TACtical_AI: TryFreeUpBaseSlots - Error " + e);
            }
        }

        public static void RemoveAllBases(EnemyMind mind, EnemyBaseFunder funds)
        {   // Remove uneeeded garbage
            try
            {
                Tank tech = mind.AIControl.tank;
                foreach (EnemyBaseFunder fund in EnemyBases)
                {
                    if (fund.Team == tech.Team && fund != funds)
                    {
                        RecycleTechToTeam(fund.Tank);
                    }
                }
            }
            catch (Exception e)
            {
                DebugTAC_AI.Log("TACtical_AI: RemoveAllBases - Error " + e);
            }
        }

        // AI Base building
        public static BasePurpose PickBuildBasedOnPriorities(EnemyMind mind, FactionLevel lvl, EnemyBaseFunder funds, List<EnemyBaseFunder> funders)
        {   // Expand the base!
            /*
             * BUILD ORDER:
             *  Main base (duh)
             *  Autominer -- If resources nearby -> HasReceivers
             *  Defenses
             *  Factory
             *  Defenses
             *  Autominer
             *  Defenses
             *  Factory
             *  
             */
            if (GetCountOfPurpose(BasePurpose.Harvesting, funders) == 0)
                return PickHarvestBase(mind, funds, funders);

            // Fallback
            return PickBuildBasedOnPrioritiesLegacy(mind, lvl, funds);
        }
        public static BasePurpose PriorityDefense(EnemyMind mind, FactionLevel lvl, EnemyBaseFunder funds, List<EnemyBaseFunder> funders)
        {   // Expand the base!
            int team = funds.Team;
            if (GetCountOfPurpose(BasePurpose.Harvesting, funders) == 0)
                return PickHarvestBase(mind, funds, funders);
            if (GetActiveTeamDefenseCount(team) >= MaxDefenses)
                return PickBuildBasedOnPriorities(mind, lvl, funds, funders);
            return BasePurpose.TechProduction;
        }
        public static BasePurpose PickHarvestBase(EnemyMind mind, EnemyBaseFunder funds, List<EnemyBaseFunder> funders)
        {   // Expand the base!
            int team = funds.Team;
            if (FetchNearbyResourceCounts(team) > 6 && GetCountOfPurpose(BasePurpose.HasReceivers, funders) == 0)
                return BasePurpose.HasReceivers;
            if (!ManNetwork.IsNetworked)
                return BasePurpose.Autominer;
            else 
                return BasePurpose.TechProduction;
        }


        public static BasePurpose PickBuildBasedOnPrioritiesLegacy(EnemyMind mind, FactionLevel lvl, EnemyBaseFunder funds)
        {   // Expand the base!
            int team = mind.Tank.Team;
            List<EnemyBaseFunder> funders = GetTeamBaseFunders(team);
            if (GetTeamFunds(team) <= CheapestAutominerPrice(mind, lvl) && !HasTooMuchOfType(team, BasePurpose.Autominer, funders))
            {   // YOU MUST CONSTRUCT ADDITIONAL PYLONS
                return BasePurpose.Autominer;
            }
            else if (mind.AIControl.lastEnemyGet)
            {
                switch (UnityEngine.Random.Range(1, 7))
                {
                    case 1:
                        if (GetActiveTeamDefenseCount(team) > MaxDefenses)
                            return BasePurpose.TechProduction;
                        return BasePurpose.Defense;
                    case 2:
                        if (HasTooMuchOfType(team, BasePurpose.Harvesting, funders))
                            return BasePurpose.TechProduction;
                        return BasePurpose.Harvesting;
                    case 3:
                        if (HasTooMuchOfType(team, BasePurpose.HasReceivers, funders))
                            return BasePurpose.TechProduction;
                        return BasePurpose.HasReceivers;
                    case 4:
                        return BasePurpose.TechProduction;
                    case 5:
                        if (HasTooMuchOfType(team, BasePurpose.Autominer, funders))
                            return BasePurpose.TechProduction;
                        return BasePurpose.Autominer;
                    default:
                        if (HasTooMuchOfType(team, BasePurpose.Defense, funders))
                            return BasePurpose.TechProduction;
                        return BasePurpose.Defense;
                }
            }
            else
            {
                switch (UnityEngine.Random.Range(0, 5))
                {
                    case 1:
                        if (GetActiveTeamDefenseCount(team) > MaxDefenses)
                            return BasePurpose.TechProduction;
                        return BasePurpose.Defense;
                    case 2:
                        if (HasTooMuchOfType(team, BasePurpose.Harvesting, funders))
                            return BasePurpose.TechProduction;
                        return BasePurpose.Harvesting;
                    case 3:
                        if (HasTooMuchOfType(team, BasePurpose.HasReceivers, funders))
                            return BasePurpose.TechProduction;
                        return BasePurpose.HasReceivers;
                    case 4:
                        return BasePurpose.TechProduction;
                    case 5:
                        if (HasTooMuchOfType(team, BasePurpose.Autominer, funders))
                            return BasePurpose.TechProduction;
                        return BasePurpose.Autominer;
                    default:
                        if (HasTooMuchOfType(team, BasePurpose.Harvesting, funders))
                            return BasePurpose.TechProduction;
                        return BasePurpose.AnyNonHQ;
                }
            }
        }
        public static BasePurpose PickBuildNonDefense(EnemyMind mind)
        {   // Expand the base!
            int team = mind.Tank.Team;
            List<EnemyBaseFunder> funders = GetTeamBaseFunders(team);
            switch (UnityEngine.Random.Range(0, 5))
            {
                case 2:
                    if (HasTooMuchOfType(team, BasePurpose.Harvesting, funders))
                        return BasePurpose.TechProduction;
                    return BasePurpose.Harvesting;
                case 3:
                    if (HasTooMuchOfType(team, BasePurpose.HasReceivers, funders))
                        return BasePurpose.TechProduction;
                    return BasePurpose.HasReceivers;
                case 4:
                case 5:
                    return BasePurpose.TechProduction;
                default:
                    if (HasTooMuchOfType(team, BasePurpose.Autominer, funders))
                        return BasePurpose.TechProduction;
                    return BasePurpose.Autominer;
            }
        }




        // Utilities
        public static ChunkTypes[] TryGetBiomeResource(Vector3 scenePos)
        {   // make autominers mine deep based on biome
            switch (ManWorld.inst.GetBiomeWeightsAtScenePosition(scenePos).Biome(0).BiomeType)
            {
                case BiomeTypes.Grassland:
                    return new ChunkTypes[1] { ChunkTypes.EruditeShard };
                case BiomeTypes.Desert:
                    return new ChunkTypes[4] { ChunkTypes.OleiteJelly, ChunkTypes.OleiteJelly, ChunkTypes.OleiteJelly, ChunkTypes.IgniteShard };
                case BiomeTypes.Mountains:
                    return new ChunkTypes[1] { ChunkTypes.RoditeOre, };
                case BiomeTypes.SaltFlats:
                case BiomeTypes.Ice:
                    return new ChunkTypes[4] { ChunkTypes.CarbiteOre, ChunkTypes.CarbiteOre, ChunkTypes.CarbiteOre, ChunkTypes.CelestiteShard };

                case BiomeTypes.Pillars:
                    return new ChunkTypes[1] { ChunkTypes.RubberJelly };

                default:
                    return new ChunkTypes[2] { ChunkTypes.PlumbiteOre, ChunkTypes.TitaniteOre };
            }
        }

        private static int CheapestAutominerPrice(EnemyMind mind, FactionLevel lvl)
        {
            List<SpawnBaseTypes> types = RawTechLoader.GetEnemyBaseTypes(mind.MainFaction, lvl, BasePurpose.Autominer, BaseTerrain.Land);
            int lowest = 150000;
            RawTechTemplate BT;
            foreach (SpawnBaseTypes type in types)
            {
                BT = RawTechLoader.GetBaseTemplate(type);
                int tryThis = BT.baseCost;
                if (tryThis < lowest)
                {
                    lowest = tryThis;
                }
            }
            return lowest;
        }
    }
}
