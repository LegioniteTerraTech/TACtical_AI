using System;
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


        public static List<EnemyBaseFunder> AllEnemyBases = new List<EnemyBaseFunder>();


        private static StringBuilder SB = new StringBuilder();

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

        public static TeamBasePointer GetTeamHQ(int Team)
        {
            if (ManBaseTeams.TryGetBaseTeam(Team, out var ETD))
            {
                if (ETD.HQ == null)
                    ETD.SetHQToStrongestOrRandomBase();
                return ETD.HQ;
            }
            return null;
        }
        public static Func<int,int> GetTeamFunds => ManBaseTeams.GetTeamMoney;
        public static List<EnemyBaseFunder> GetTeamBaseFunders(int Team)
        {
            return AllEnemyBases.FindAll(delegate (EnemyBaseFunder cand) { return cand.Team == Team && cand._tank.blockman.blockCount > 0; });
        }
        public static void CollectTeamBaseFunders(int Team, List<TeamBasePointer> collection)
        {
            foreach (var item in AllEnemyBases.FindAll(delegate (EnemyBaseFunder cand) { return cand.Team == Team && cand._tank.blockman.blockCount > 0; }))
            {
                collection.Add(item);
            }
        }
        public static int GetAllTeamsEnemyHQCount()
        {
            return AllEnemyBases.FindAll(delegate (EnemyBaseFunder funds) { return funds.IsHQ; }).Count;
        }
        public static int GetCountOfPurpose(BasePurpose BP, List<EnemyBaseFunder> baseFunders)
        {
            return baseFunders.FindAll(delegate (EnemyBaseFunder cand) { return cand.purposes.Contains(BP); }).Count;
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
                if (funds.purposes.Contains(purpose))
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
            if (!ManBaseTeams.TryGetBaseTeam(tank.Team, out var ETD))
            {
                DebugTAC_AI.Log("TACtical_AI: RecycleTechToTeam - Tech " + tank.name + " invoked but no TeamBase is assigned to team");
                return;
            }
            WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(tank.visible);
            int tankCost = RawTechTemplate.GetBBCost(tank);
            string compressed = CompressIfNeeded(tankCost, out int smaller);
            AIGlobals.PopupEnemyInfo(Singleton.Manager<Localisation>.inst.GetMoneyStringWithSymbol(smaller) + compressed, worPos);
            ETD.AddBuildBucks(tankCost);
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
            if (ManBaseTeams.TryGetBaseTeam(Team, out var ETD))
                return ETD.PurchasePossible(BBCost);
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
            if (ManBaseTeams.TryGetBaseTeam(Team, out var ETD))
            {
                ETD.AddBuildBucks(amount);
            }
        }
        public static bool TryMakePurchase(BlockTypes bloc, int Team)
        {
            if (ManBaseTeams.TryGetBaseTeam(Team, out var ETD))
            {
                return ETD.TryMakePurchase(bloc);
            }
            return false;
        }
        public static bool TryBribeTech(Tank tank, int bribingTeam)
        {
            if (tank.Team != bribingTeam && TeamGlobalMobileTechCount(bribingTeam) < KickStart.EnemyTeamTechLimit &&
                 ManBaseTeams.TryGetBaseTeam(bribingTeam, out var ETD))
            {
                int cost = (int)(RawTechTemplate.GetBBCost(tank) * AIGlobals.BribeMulti);
                if (ETD.TryMakePurchase(cost))
                {
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
            }
            return false;
        }
        public static void TryDeclareBankruptcy(int Team)
        {
            if (ManBaseTeams.TryGetBaseTeam(Team, out var ETD))
            {
                ETD.FlagBankrupt();
            }
        }


        // Utilities
        public static string GetActualNameDef(string name)
        {
            foreach (char ch in name)
            {
                if (ch == RawTechLoader.turretChar)
                {
                    SB.Remove(SB.Length - 1, 1);
                    break;
                }
                else
                    SB.Append(ch);
            }
            var anon = SB.ToString();
            SB.Clear();
            return anon;
        }
        public static int FetchNearbyResourceCounts(int Team)
        {
            var funds = GetTeamHQ(Team);
            if (funds == null)
                return 1;

            Vector3 tankPos = funds.WorldPos.ScenePosition;
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
        public static void RequestFocusFireNPTs(EnemyMind mind, Visible Target, RequestSeverity priority)
        {
            var tank = mind.Tank;
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
                if (pair.severity < priority)
                    BaseFunderManager.targetingRequestsNPT[Team] = new TargetingRequest(priority, Target, mind.CanCallRetreat);
            }
            else
                BaseFunderManager.targetingRequestsNPT.Add(Team, new TargetingRequest(priority, Target, mind.CanCallRetreat));
        }


        /// <summary>
        /// This is VERY lazy.  There's only a small chance it will actually transfer the funds to the real strongest base.
        /// <para>This is normally handled automatically by the manager, but you can call this if you want to move the cash NOW</para>
        /// </summary>
        /// <param name="funds">The EnemyBaseFunder that contains the money to move</param>
        /// <returns>True if it actually moved the money</returns>
        public static bool SetHQToStrongestOrRandomBase(this EnemyBaseFunder funds)
        {
            if (!(bool)funds)
                return false;
            if (ManBaseTeams.TryGetBaseTeam(funds.Team, out var ETD))
            {
                ETD.SetHQToStrongestOrRandomBase();
                return true;
            }
            return false;
        }

        internal struct TargetingRequest
        {
            public readonly RequestSeverity severity;
            public readonly Visible target;
            public readonly bool canCallRetreat;

            public TargetingRequest(RequestSeverity Severity, Visible Target, bool CanCallRetreat)
            {
                severity = Severity;
                target = Target;
                canCallRetreat = CanCallRetreat;
            }
        }

        internal class BaseFunderManager : MonoBehaviour
        {
            internal static BaseFunderManager inst;

            internal static List<int> TeamsBuildRequested = new List<int>();
            internal static Dictionary<int, TargetingRequest> targetingRequestsNPT = new Dictionary<int, TargetingRequest>();
            private static readonly Dictionary<int, EnemyBaseFunder> TeamsUpdatedMainBase = new Dictionary<int, EnemyBaseFunder>();
            private float NextDelayedUpdateTime = 0;
            private const float delayedUpdateDelay = 6;
            internal static void Initiate()
            {
                if (inst)
                    return;
                inst = new GameObject("BaseFunderManagerMain").AddComponent<BaseFunderManager>();
                ManPauseGame.inst.PauseEvent.Subscribe(inst.OnPaused);
                DebugTAC_AI.Log("TACtical_AI: Initiated BaseFunderManager");
            }
            internal static void DeInit()
            {
                if (!inst)
                    return;
                ManPauseGame.inst.PauseEvent.Unsubscribe(inst.OnPaused);
                Destroy(inst.gameObject);
                inst = null;
                DebugTAC_AI.Log("TACtical_AI: DeInit BaseFunderManager");
            }
            public void OnPaused(bool state)
            {
                enabled = !state;
            }
            private void Update()
            {
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
            private void DelayedUpdate()
            {
                ManBaseTeams.UpdateTeams();
                PeriodicBuildRequest();
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
                        var helper = tech.GetComponent<TankAIHelper>();
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
            private static void ProcessFocusFireRequest(int requestingTeam, TargetingRequest request)
            {
                try
                {
                    switch (request.severity)
                    {
                        case RequestSeverity.ThinkMcFly:
                            float averageTechDMG = 0;
                            int count = 0;
                            foreach (var item2 in TankAIManager.TeamActiveMobileTechs(requestingTeam))
                            {
                                averageTechDMG += item2.GetHelperInsured().DamageThreshold;
                                count++;
                            }
                            if (averageTechDMG == 0)
                                return;
                            averageTechDMG /= count;
                            if (averageTechDMG < AIGlobals.RetreatBelowTeamDamageThreshold)
                            {
                                AIECore.TeamRetreat(requestingTeam, true, true);
                            }
                            else
                            {
                                foreach (Tank tech in TankAIManager.GetNonEnemyTanks(requestingTeam))
                                {
                                    var helper = tech.GetComponent<TankAIHelper>();
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
                                                mind.GetRevengeOn(request.target, true);
                                        }
                                        else if ((bool)baseFunds)
                                        {
                                            if (baseFunds.purposes.Contains(BasePurpose.TechProduction))
                                            {
                                                if (TeamGlobalMobileTechCount(requestingTeam) < KickStart.EnemyTeamTechLimit && mind.TechMemor.HasFullHealth())
                                                    mind.BlowBolts();
                                            }
                                        }
                                    }
                                }
                            }
                            break;
                        case RequestSeverity.Warn:
                            foreach (Tank tech in TankAIManager.GetNonEnemyTanks(requestingTeam))
                            {
                                var helper = tech.GetComponent<TankAIHelper>();
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
                                            mind.GetRevengeOn(request.target, true);
                                    }
                                    else if ((bool)baseFunds)
                                    {
                                        if (baseFunds.purposes.Contains(BasePurpose.TechProduction))
                                        {
                                            if (TeamGlobalMobileTechCount(requestingTeam) < KickStart.EnemyTeamTechLimit && mind.TechMemor.HasFullHealth())
                                                mind.BlowBolts();
                                        }
                                    }
                                }
                            }
                            break;
                        case RequestSeverity.SameTeam:
                            foreach (Tank tech in TankAIManager.GetTeamTanks(requestingTeam))
                            {
                                var helper = tech.GetComponent<TankAIHelper>();
                                var mind = tech.GetComponent<EnemyMind>();
                                if ((bool)helper && (bool)mind)
                                {
                                    var baseFunds = tech.GetComponent<EnemyBaseFunder>();
                                    if (!mind.StartedAnchored)
                                    {
                                        mind.AIControl.Provoked = AIGlobals.ProvokeTime;
                                        if (!(bool)helper.lastEnemyGet)
                                            mind.GetRevengeOn(request.target, true);
                                    }
                                    else if ((bool)baseFunds)
                                    {
                                        if (baseFunds.purposes.Contains(BasePurpose.TechProduction))
                                        {
                                            if (TeamGlobalMobileTechCount(requestingTeam) < KickStart.EnemyTeamTechLimit && mind.TechMemor.HasFullHealth())
                                                mind.BlowBolts();
                                        }
                                    }
                                }
                            }
                            break;
                        case RequestSeverity.AllHandsOnDeck:
                            foreach (Tank tech in TankAIManager.GetNonEnemyTanks(requestingTeam))
                            {
                                var helper = tech.GetComponent<TankAIHelper>();
                                var mind = tech.GetComponent<EnemyMind>();
                                if ((bool)helper && (bool)mind)
                                {
                                    var baseFunds = tech.GetComponent<EnemyBaseFunder>();
                                    if (!mind.StartedAnchored)
                                    {
                                        mind.AIControl.Provoked = AIGlobals.ProvokeTime;
                                        if (!(bool)helper.lastEnemyGet)
                                            mind.GetRevengeOn(request.target, true);
                                    }
                                    else if ((bool)baseFunds)
                                    {
                                        if (baseFunds.purposes.Contains(BasePurpose.TechProduction))
                                        {
                                            if (TeamGlobalMobileTechCount(requestingTeam) < KickStart.EnemyTeamTechLimit && mind.TechMemor.HasFullHealth())
                                                mind.BlowBolts();
                                        }
                                    }
                                }
                            }
                            var reqTeam = ManEnemyWorld.GetTeam(requestingTeam);
                            if (reqTeam != null)
                            {
                                reqTeam.SetAttackMode(WorldPosition.FromScenePosition(request.target.centrePosition).TileCoord);
                            }
                            break;
                        default:
                            break;
                    }

                }
                catch { }
            }
            private void RunFocusFireRequests()
            {
                foreach (KeyValuePair<int, TargetingRequest> request in targetingRequestsNPT)
                {
                    ProcessFocusFireRequest(request.Key, request.Value);
                }
                targetingRequestsNPT.Clear();
            }
        
            private void PeriodicBuildRequest()
            {
                if (AllEnemyBases.Count == 0)
                    return;
                TeamsUpdatedMainBase.Clear();

                foreach (EnemyBaseFunder funds in AllEnemyBases)
                {
                    if (!TeamsUpdatedMainBase.ContainsKey(funds.Team))
                    {
                        if (funds.bankrupt)
                            TeamsUpdatedMainBase.Add(funds.Team, funds);
                    }
                }
                foreach (Tank tech in Singleton.Manager<ManTechs>.inst.CurrentTechs)
                {
                    if (TeamsUpdatedMainBase.ContainsKey(tech.Team))
                    {
                        var helper = tech.GetComponent<TankAIHelper>();
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
        public static EnemyBaseFunder TryGetFunder(this TankAIHelper tank)
        {
            return tank.GetComponent<EnemyBaseFunder>();
        }
        public static EnemyBaseFunder TryGetFunder(this Tank tank)
        {
            return tank.GetComponent<EnemyBaseFunder>();
        }
        public class EnemyBaseFunder : MonoBehaviour, TeamBasePointer
        {
            internal Tank _tank;
            public Tank tank => _tank;
            internal HashSet<BasePurpose> purposes = new HashSet<BasePurpose>();
            public HashSet<BasePurpose> Purposes => purposes;
            public int Team => _tank.Team;
            public WorldPosition WorldPos => WorldPosition.FromScenePosition(_tank.boundsCentreWorldNoCheck);
            public int BuildBucks
            {
                get => ManBaseTeams.GetTeamMoney(Team);
                set
                {
                    if (ManBaseTeams.TryGetBaseTeam(Team, out var ETD))
                        ETD.SetBuildBucks = value;
                    else
                        DebugTAC_AI.Assert("BuildBucks was added but ManBaseTeams didn't have the base team " +
                            Team + "! " +  value + " was lost to oblivion!");
                }
            }
            public int BlockCount => _tank.blockman.blockCount;
            public bool valid => this && _tank;


            /// <summary> This tech will occationally gather funds.  Use this to get the team Tech with all the Build Bucks. </summary>
            public bool IsHQ => ManBaseTeams.IsTeamHQ(this);
            public bool hasTerminal = false;
            /// <summary> If this Tech has a terminal, it can build any tech from the population </summary>
            public bool HasTerminal => hasTerminal;
            public bool bankrupt = false;
            /// <summary> This base has not enough Build Bucks </summary>
            public bool Bankrupt => bankrupt;

            public void Initiate(Tank tank)
            {
                this._tank = tank;
                tank.TankRecycledEvent.Subscribe(OnRecycle);
                UpdateToNewer();

                AllEnemyBases.Add(this);
                DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + " Initiated EnemyBaseFunder");
            }
            public void SetupPurposes(RawTechTemplate type)
            {
                purposes.Clear();
                purposes = type.purposes;
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
                AllEnemyBases.Remove(this);
                Destroy(this);
            }
            public void AddBuildBucks(int toAdd)
            {
                if (ManBaseTeams.TryGetBaseTeam(Team, out var ETD))
                    ETD.AddBuildBucks(toAdd);
            }
            public void SetBuildBucks(int toSet)
            {
                if (ManBaseTeams.TryGetBaseTeam(Team, out var ETD))
                    ETD.SetBuildBucks = toSet;
            }

            public int GetBuildBucksFromName(string name = "")
            {
                if (name == "")
                    name = _tank.name;
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
                        SB.Append(ch);
                    }
                }
                if (!doingBB)
                    return 0;
                string Funds = SB.ToString();
                SB.Clear();
                if (Funds == " Inf")
                {
                    return -1;
                }
                bool worked = int.TryParse(Funds, out int Output);
                if (!worked)
                {
                    //DebugTAC_AI.Log("TACtical_AI: BuildBucks corrupted for tech " + name + ", returning 0");
                    return 0;
                }
                return Output;
            }
            public int UpdateToNewer(string name = "")
            {
                if (name == "")
                    name = _tank.name;
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
                        SB.Append(ch);
                    }
                }
                if (!doingBB)
                    return 0;
                string Funds = SB.ToString();
                SB.Clear();
                if (Funds == " Inf")
                {
                    if (ManBaseTeams.InsureBaseTeam(Team, out var ETD))
                    {
                        ETD.SetBuildBucks = -1;
                        _tank.SetName(GetActualName(_tank.name));
                    }
                    return -1;
                }
                bool worked = int.TryParse(Funds, out int Output);
                if (!worked)
                {
                    //DebugTAC_AI.Log("TACtical_AI: BuildBucks corrupted for tech " + name + ", returning 0");
                    return 0;
                }
                else
                {
                    if (ManBaseTeams.InsureBaseTeam(Team, out var ETD))
                    {
                        ETD.AddBuildBucks(Output);
                        _tank.SetName(GetActualName(_tank.name));
                    }
                }
                return Output;
            }
            public static string GetActualName(string name)
            {
                try
                {
                    char lastIn = 'n';
                    foreach (char ch in name)
                    {
                        if (ch == '¥' && lastIn == '¥')
                        {
                            SB.Remove(SB.Length - 2, 2);
                            break;
                        }
                        else
                            SB.Append(ch);
                        lastIn = ch;
                    }
                    return SB.ToString();
                }
                catch (Exception e)
                {
                    throw new Exception("RLoadedBases.GetActualName FAILED", e);
                }
                finally
                {
                    SB.Clear();
                }
            }


            // EnemyPurchase
            public bool PurchasePossible(int BBCost)
            {
                if (ManBaseTeams.TryGetBaseTeam(Team, out var ETD))
                {
                    if (BBCost <= ETD.BuildBucks)
                        return true;
                }
                return false;
            }
            public bool TryMakePurchase(BlockTypes bloc)
            {
                return TryMakePurchase(Singleton.Manager<RecipeManager>.inst.GetBlockBuyPrice(bloc, true));
            }
            public bool TryMakePurchase(int Pay)
            {
                if (ManBaseTeams.TryGetBaseTeam(Team, out var ETD))
                {
                    if (Pay <= ETD.BuildBucks)
                    {
                        ETD.SpendBuildBucks(Pay);
                        return true;
                    }
                }
                return false;
            }

        }
        public static int GetBuildBucksFromNameExt(string name)
        {
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
                    SB.Append(ch);
                }
            }
            if (!doingBB)
                return 0;
            string Funds = SB.ToString();
            SB.Clear();
            if (Funds == " Inf")
            {
                return -1;
            }
            bool worked = int.TryParse(Funds, out int Output);
            if (!worked)
            {
                //DebugTAC_AI.Log("TACtical_AI: BuildBucks corrupted for tech " + name + ", returning 0");
                return 0;
            }
            return Output;
        }


        private static HashSet<string> cachedTechNames = new HashSet<string>();
        private static List<Tank> cachedTechs = new List<Tank>();
        private static void BaseSplitPriorityHandler(Tank speculativePart)
        {
            char lastIn = 'n';
            foreach (char ch in speculativePart.name)
            {
                if (ch == '¥' && lastIn == '¥')
                {
                    SB.Remove(SB.Length - 2, 2);
                    break;
                }
                else
                    SB.Append(ch);
                lastIn = ch;
            }
            cachedTechNames.Add(SB.ToString());
            SB.Clear();
            InvokeHelper.InvokeSingle(BaseSplitPriorityCheck, 0.125f);
        }
        private static void BaseSplitPriorityCheck()
        {
            try
            {
                foreach (var name in cachedTechNames)
                {
                    try
                    {
                        int bigBlockCount = 0;
                        Tank largest = null;
                        foreach (var item in ManTechs.inst.IterateTechsWhere(x => x.name.Contains(name)))
                        {
                            if (bigBlockCount < item.blockman.blockCount)
                            {
                                bigBlockCount = item.blockman.blockCount;
                                largest = item;
                            }
                            cachedTechs.Add(item);
                        }
                        if (largest)
                        {
                            foreach (var tank in cachedTechs)
                            {
                                if (largest == tank)
                                {
                                    tank.SetName(name);
                                }
                                else
                                {
                                    //It's not a base
                                    if (tank.IsAnchored)
                                    {   // It's a fragment of the base - prevent unwanted mess from getting in the way
                                        RecycleTechToTeam(tank);
                                        continue;
                                    }

                                    char lastIn = 'n';
                                    foreach (char ch in name)
                                    {
                                        if (ch == '¥' && lastIn == '¥')
                                        {
                                            SB.Remove(SB.Length - 2, 2);
                                            break;
                                        }
                                        else
                                            SB.Append(ch);
                                        lastIn = ch;
                                    }
                                    SB.Append(" Minion");
                                    tank.SetName(SB.ToString());
                                    SB.Clear();
                                    var help = tank.GetHelperInsured();
                                    if (help)
                                    {
                                        help.ResetAll(tank);
                                        help.RandomizeBrain(tank);
                                    }

                                    var mind = tank.GetComponent<EnemyMind>();
                                    if (mind)
                                    {
                                        // it's a minion of the base
                                        if (mind.CommanderAttack == EAttackMode.Safety)
                                            mind.CommanderAttack = EAttackMode.Chase;
                                    }

                                    // Charge the new Tech and send it on it's way!
                                    RawTechLoader.ChargeAndClean(tank);
                                    tank.visible.Teleport(tank.boundsCentreWorldNoCheck + (tank.rootBlockTrans.forward * tank.blockBounds.size.magnitude), tank.rootBlockTrans.rotation, true, false);
                                    if (!Singleton.Manager<ManVisible>.inst.AllTrackedVisibles.Any(delegate (TrackedVisible cand) { return cand.visible == tank.visible; }))
                                    {
                                        DebugTAC_AI.Assert(true, "TACtical_AI: ASSERT - " + tank.name + " was not properly inserted into the TrackedVisibles list and will not function properly!");
                                        RawTechLoader.TrackTank(tank);
                                    }
                                }
                            }
                        }
                    }
                    finally
                    {
                        cachedTechs.Clear();
                    }
                }
            }
            finally
            {
                cachedTechNames.Clear();
            }
        }


        // MAIN enemy bootup base handler
        public static bool SetupBaseAI(TankAIHelper thisInst, Tank tank, EnemyMind mind)
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
                tank.MainCorps = new List<FactionSubTypes> { builder.faction };
                if (builder.faction != FactionSubTypes.NULL)
                {
                    tank.MainCorps = new List<FactionSubTypes> { builder.faction };
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

            if (name.Contains(RawTechLoader.baseChar))
            {   // Main base
                if (name.Contains('#'))
                {
                    if (ManNetwork.IsHost)
                        BaseSplitPriorityHandler(tank);
                }
                else
                {
                    thisInst.InsureTechMemor("SetupBaseAI - Base", false);
                    DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + " Setup for BASE");

                    var funds = tank.gameObject.GetComponent<EnemyBaseFunder>(); 
                    if (funds.IsNull())
                    {
                        funds = tank.gameObject.AddComponent<EnemyBaseFunder>();
                        funds.Initiate(tank);
                    }

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
                                tank.MainCorps.Add(RawTechUtil.CorpExtToCorp(BT.faction));
                                activated = true;
                            }
                        }
                        if (!activated)
                        {
                            BT = RawTechLoader.GetBaseTemplate(type);
                            if (BT != null)
                            {
                                SetupBaseType(BT, mind);
                                funds.SetupPurposes(BT);
                                DebugTAC_AI.Log("TACtical_AI: Registered base " + baseName + " | type " + type.ToString());
                                mind.TechMemor.SetupForNewTechConstruction(thisInst, BT.savedTech);
                                tank.MainCorps.Add(RawTechLoader.GetMainCorp(type));
                            }
                            else
                                DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + " Setup for Base FAILED - Could not find base type for \"" +
                                    baseName + "\" fetched type was " + type.ToString());
                        }
                        mind.TechMemor.MakeMinersMineUnlimited();
                    }
                    catch (Exception e)
                    {
                        DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + " Setup for BASE FAILED - " + e);
                    }
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
                    if (ManNetwork.IsHost)
                        BaseSplitPriorityHandler(tank);
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
                                tank.MainCorps.Add(RawTechUtil.CorpExtToCorp(BT.faction));
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
                    {   // Because Autominers are disabled(???)
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
            catch (Exception e) 
            {
                throw new Exception("UpdateBaseOperations FAILED ~ ", e);
            }
        }
        public static void ImTakingThatExpansion(EnemyMind mind, EnemyBaseFunder funds)
        {   // Expand the base!
            WorldPosition pos2 = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(mind.AIControl.tank.visible);

            if (!KickStart.AllowEnemyBaseExpand && !mind.Tank.FirstUpdateAfterSpawn)
            {
                if (DebugRawTechSpawner.ShowDebugFeedBack)
                    AIGlobals.PopupEnemyInfo("Cleanup", pos2);
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
                        grade = Singleton.Manager<ManLicenses>.inst.GetCurrentLevel(mind.MainFaction);
                }
                catch { }
                int Cost = GetTeamFunds(tech.Team);
                if (TeamGlobalMakerBaseCount(tech.Team) >= KickStart.MaxBasesPerTeam)
                {
                    TryFreeUpBaseSlots(mind, lvl, funds);
                    if (TeamGlobalMobileTechCount(tech.Team) < KickStart.EnemyTeamTechLimit)
                    {
                        if (DebugRawTechSpawner.ShowDebugFeedBack)
                            AIGlobals.PopupEnemyInfo("Build Unit", pos2);
                        BaseConstructTech(mind, tech, lvl, funds, grade, Cost);
                    }
                    else if (AIGlobals.NoBuildWhileInCombat)
                    {
                        if (DebugRawTechSpawner.ShowDebugFeedBack)
                            AIGlobals.PopupEnemyInfo("Upgrades", pos2);
                        BaseUpgradeTechs(mind, tech, lvl, funds, GetTeamBaseFunders(tech.Team), grade, Cost);
                    }
                    else if (DebugRawTechSpawner.ShowDebugFeedBack)
                        AIGlobals.PopupEnemyInfo("Freeing Cap", pos2);
                    return;
                }

                Visible lastEnemySet = mind.AIControl.lastEnemyGet;
                if (!lastEnemySet)
                {
                    ExpandBasePeaceful(mind, lvl, funds, grade, Cost);
                    if (DebugRawTechSpawner.ShowDebugFeedBack)
                        AIGlobals.PopupEnemyInfo("Safe Expand", pos2);
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
                            if (DebugRawTechSpawner.ShowDebugFeedBack)
                                AIGlobals.PopupEnemyInfo("Expand Fail", pos2);
                        }
                        else
                        {
                            RawTechTemplate BTemp = TempManager.ExternalEnemyTechsAll[spawnIndex];
                            RawTechLoader.SpawnBaseExpansion(tech, pos, tech.Team, BTemp);
                            if (DebugRawTechSpawner.ShowDebugFeedBack)
                                AIGlobals.PopupEnemyInfo("Combat Expand2", pos2);
                            return;
                        }
                    }
                    SpawnBaseTypes type = RawTechLoader.GetEnemyBaseType(mind.MainFaction, lvl, reason, Terra, maxGrade: grade, maxPrice: Cost);
                    if (RawTechLoader.IsFallback(type))
                    {
                        if (DebugRawTechSpawner.ShowDebugFeedBack)
                            AIGlobals.PopupEnemyInfo("Expand Fail2", pos2);
                        return;
                    }

                    if (RawTechLoader.SpawnBaseExpansion(tech, pos, tech.Team, type))
                    {
                        DebugTAC_AI.Log("TACtical_AI: ImTakingThatExpansion - Expanded");
                        if (DebugRawTechSpawner.ShowDebugFeedBack)
                            AIGlobals.PopupEnemyInfo("Combat Expand", pos2);
                    }
                    else
                    {
                        DebugTAC_AI.Log("TACtical_AI: SpawnBaseExpansion - Team " + tech.Team + ": Failiure on expansion");
                        if (DebugRawTechSpawner.ShowDebugFeedBack)
                            AIGlobals.PopupEnemyInfo("Expand Fail3", pos2);
                    }
                }
                else
                {   // Get new base location to expand
                    if (SetHQToStrongestOrRandomBase(funds))
                    {
                        if (TeamGlobalMobileTechCount(tech.Team) < KickStart.EnemyTeamTechLimit)
                        {
                            if (DebugRawTechSpawner.ShowDebugFeedBack)
                                AIGlobals.PopupEnemyInfo("Build Unit2", pos2);
                            BaseConstructTech(mind, tech, lvl, funds, grade, Cost);
                        }
                        else
                        {
                            if (DebugRawTechSpawner.ShowDebugFeedBack)
                                AIGlobals.PopupEnemyInfo("Upgrades2", pos2);
                            BaseUpgradeTechs(mind, tech, lvl, funds, GetTeamBaseFunders(tech.Team), grade, Cost);
                        }
                    }
                }
            }
            catch
            {
                DebugTAC_AI.Log("TACtical_AI: ImTakingThatExpansion - game is being stubborn");
                if (DebugRawTechSpawner.ShowDebugFeedBack)
                    AIGlobals.PopupEnemyInfo("ERROR", pos2);
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
                    if (SetHQToStrongestOrRandomBase(funds))
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
                if (!KickStart.AISelfRepair)
                    return;
                Tank toUpgrade = null;
                bool shouldChangeHarvesters = GetCountOfPurpose(BasePurpose.HasReceivers, funders) == 0;
                foreach (var item in TankAIManager.TeamActiveMobileTechs(tech.Team))
                {
                    var helper = item.GetComponent<TankAIHelper>();
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
                    SetHQToStrongestOrRandomBase(funds);
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
                            DebugTAC_AI.Log("TACtical_AI: InsureHarvester - There are no harvesters for FactionSubTypes " + mind.MainFaction + ", trying fallbacks");

                            FactionSubTypes FTE = mind.MainFaction;
                            SBT = RawTechLoader.GetEnemyBaseType(FTE, lvl, new HashSet<BasePurpose> { BasePurpose.Harvesting, BasePurpose.NotStationary }, BaseTerrain.AnyNonSea, maxPrice: funds.BuildBucks);
                            if (RawTechLoader.IsFallback(SBT))
                            {
                                DebugTAC_AI.Log("TACtical_AI: InsureHarvester - There are no harvesters for Vanilla Faction " + FTE + ", using GSO");

                                SBT = RawTechLoader.GetEnemyBaseType(FactionSubTypes.GSO, lvl, new HashSet<BasePurpose> { BasePurpose.Harvesting, BasePurpose.NotStationary }, BaseTerrain.AnyNonSea, maxPrice: funds.BuildBucks);
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

                List<EnemyBaseFunder> basesSorted = AllEnemyBases;
                // Remove the lower-end first
                foreach (EnemyBaseFunder fund in basesSorted.OrderBy((F) => F._tank.blockman.blockCount))
                {
                    if (fund.Team == tech.Team && fund != funds)
                    {
                        if (ForceRemove)
                        {
                            RecycleTechToTeam(fund._tank);
                            if (step >= attempts)
                                return;
                        }
                        if (RemoveReceivers && fund.purposes.Contains(BasePurpose.HasReceivers) && !fund.purposes.Contains(BasePurpose.Autominer))
                        {
                            RecycleTechToTeam(fund._tank);
                            if (step >= attempts)
                                return;
                        }
                        if (RemoveSpenders && !fund.GetComponent<TankAIHelper>().PendingDamageCheck
                            && fund.purposes.Contains(BasePurpose.TechProduction) && !fund.purposes.Contains(BasePurpose.Harvesting))
                        {
                            RecycleTechToTeam(fund._tank);
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
                foreach (EnemyBaseFunder fund in AllEnemyBases)
                {
                    if (fund.Team == tech.Team && fund != funds)
                    {
                        RecycleTechToTeam(fund._tank);
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
