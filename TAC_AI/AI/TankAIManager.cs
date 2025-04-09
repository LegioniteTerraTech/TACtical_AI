using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using TAC_AI.AI.AlliedOperations;
using TAC_AI.AI.Enemy;
using TAC_AI.AI.Movement;
using TAC_AI.Templates;
using TAC_AI.World;
using TerraTech.Network;
using TerraTechETCUtil;
using UnityEngine;
using static TAC_AI.AI.TankAIHelper;
using static WobblyLaser;

namespace TAC_AI.AI
{
    internal class TankAIManager : MonoBehaviour
    {
        internal static FieldInfo rangeOverride = typeof(ManTechs).GetField("m_SleepRangeFromCamera", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo liveSetPieces = typeof(ManWorld).GetField("m_SetPiecesPlacement", BindingFlags.NonPublic | BindingFlags.Instance);

        internal static TankAIManager inst;
        private static Tank lastPlayerTech;
        public static Event<TankAIHelper> AILoadedEvent = new Event<TankAIHelper>();
        public static Event<int> TeamCreatedEvent = new Event<int>();
        public static Event<int> TeamDestroyedEvent = new Event<int>();
        internal static List<ManWorld.TerrainSetPiecePlacement> SetPieces = null;

        internal static Dictionary<int, KeyValuePair<RequestSeverity, Visible>> targetingRequests = new Dictionary<int, KeyValuePair<RequestSeverity, Visible>>();

        public static HashSet<int> MissionTechs = new HashSet<int>();

        //public static EventNoParams QueueUpdater = new EventNoParams();
        private static Dictionary<int, TeamIndex> teamsIndexed;
        public static Event<TankAIHelper> TechRemovedEvent = new Event<TankAIHelper>();
        public static Event<int> MissionTechRemovedEvent = new Event<int>();
        private static float lastCombatTime = 0;
        internal static float terrainHeight = 0;
        private static float TargetTime = DefaultTime;
        internal static float LastRealTime = 0;
        internal static float DeltaRealTime = 0;

        internal static Vector3 GravVector = Physics.gravity;
        internal static float GravMagnitude = Physics.gravity.magnitude;

        private const float DefaultTime = 1.0f;
        private const float SlowedTime = 0.25f;
        private const float FastTime = 3f; // fooling around
        private const float ChangeRate = 1.5f;
        //public static AbilityToggle toggleAuto;
        public static ManToolbar.ToolbarToggle toggleAuto;
        internal static void TogglePlayerAutopilot(bool state)
        {
            if (KickStart.AllowPlayerRTSHUD && ManWorldRTS.PlayerIsInRTS)
            {
                KickStart.AutopilotPlayerRTS = state;
                toggleAuto.SetToggleState(KickStart.AutopilotPlayerRTS);
            }
            else
            {
                KickStart.AutopilotPlayerMain = state;
                toggleAuto.SetToggleState(KickStart.AutopilotPlayerMain);
            }
        }

        internal static void Initiate()
        {
            if (inst)
                return;
            inst = new GameObject("AIManager").AddComponent<TankAIManager>();
            AIEPathMapper.inst = inst.gameObject.AddComponent<AIEPathMapper>();
            //Allies = new List<Tank>();
            AIECore.Minables = new List<Visible>();
            AIECore.Depots = new List<ModuleHarvestReciever>();
            AIECore.BlockHandlers = new List<ModuleHarvestReciever>();
            AIECore.Chargers = new List<ModuleChargerTracker>();
            AIECore.RetreatingTeams = new HashSet<int>();
            teamsIndexed = new Dictionary<int, TeamIndex>();
            Singleton.Manager<ManPauseGame>.inst.PauseEvent.Subscribe(inst.OnPaused);
            Singleton.Manager<ManTechs>.inst.TankPostSpawnEvent.Subscribe(OnTankAddition);
            Singleton.Manager<ManTechs>.inst.TankTeamChangedEvent.Subscribe(OnTankChange);
            Singleton.Manager<ManTechs>.inst.PlayerTankChangedEvent.Subscribe(OnPlayerTechChange);
            Singleton.Manager<ManVisible>.inst.OnStoppedTrackingVisible.Subscribe(OnVisibleNoLongerTracked);
            Singleton.Manager<ManGameMode>.inst.ModeStartEvent.Subscribe(OnStartup);
            InvokeHelper.Invoke(GatherAllMissionTechs, 0.1f);
            //QueueUpdater.Subscribe(FetchAllAllies);
            DebugTAC_AI.Log(KickStart.ModID + ": Created AIECore Manager.");

            // Only change if no other mod changed
            DebugTAC_AI.Log(KickStart.ModID + ": Current AI interaction range is " + (float)rangeOverride.GetValue(ManTechs.inst) + ".");
            if ((float)rangeOverride.GetValue(ManTechs.inst) == 200f)
            {   // more than twice the range
                rangeOverride.SetValue(ManTechs.inst, AIGlobals.EnemyExtendActionRange);
                DebugTAC_AI.Log(KickStart.ModID + ": Extended enemy Tech interaction range to " + AIGlobals.EnemyExtendActionRange + ".");
            }
            SetPieces = (List<ManWorld.TerrainSetPiecePlacement>)liveSetPieces.GetValue(ManWorld.inst);
        }
#if STEAM
        internal void CheckNextFrameNeedsDeInit()
        {
            Invoke("DeInitCallToKickStart", 0.001f);
        }
        internal void DeInitCallToKickStart()
        {
            if (!KickStart.ShouldBeActive)
                KickStart.DeInitALL();
        }
        internal static void DeInit()
        {
            if (!inst)
                return;
            AIEPathMapper.ResetAll();
            Singleton.Manager<ManPauseGame>.inst.PauseEvent.Unsubscribe(inst.OnPaused);
            Singleton.Manager<ManTechs>.inst.TankPostSpawnEvent.Unsubscribe(OnTankAddition);
            Singleton.Manager<ManTechs>.inst.TankTeamChangedEvent.Unsubscribe(OnTankChange);
            Singleton.Manager<ManTechs>.inst.PlayerTankChangedEvent.Unsubscribe(OnPlayerTechChange);
            Singleton.Manager<ManVisible>.inst.OnStoppedTrackingVisible.Unsubscribe(OnVisibleNoLongerTracked);
            teamsIndexed = null;
            AIECore.RetreatingTeams = null;
            AIECore.Chargers = null;
            AIECore.BlockHandlers = null;
            AIECore.Depots = null;
            AIECore.Minables = null;
            AIECore.DestroyAllHelpers();
            inst.enabled = false;
            Destroy(inst.gameObject);
            inst = null;
            DebugTAC_AI.Log(KickStart.ModID + ": De-Init AIECore Manager.");

            // Only change if no other mod changed
            if ((float)rangeOverride.GetValue(ManTechs.inst) == AIGlobals.EnemyExtendActionRange)
            {   // more than twice the range
                rangeOverride.SetValue(ManTechs.inst, 200);
                DebugTAC_AI.Log(KickStart.ModID + ": Un-Extended enemy Tech interaction range to default 200.");
            }
        }
#endif

        public static void OnStartup(Mode mode)
        {
            InvokeHelper.Invoke(InsureLoadedCorrectly, 1f);
        }
        private static void InsureLoadedCorrectly()
        {
            //ForceReloadAll();
        }
        public static void RegisterMissionTechVisID(int vis)
        {
            MissionTechs.Add(vis);
        }
        private static void GatherAllMissionTechs()
        {
            foreach (var item in ManEncounter.inst.ActiveEncounters)
            {
                foreach (var item2 in item.GetVisibleNamesWithPrefix(string.Empty))
                {
                    EncounterVisibleData EVD = item.GetVisible(item2);
                    if (EVD.ObjectType == ObjectTypes.Vehicle)
                        RegisterMissionTechVisID(EVD.m_VisibleId);
                }
            }
        }
        private static void OnVisibleNoLongerTracked(TrackedVisible TV)
        {
            if (MissionTechs.Remove(TV.ID))
            {
                MissionTechRemovedEvent.Send(TV.ID);
            }
        }

        private static void OnTankAddition(Tank tonk)
        {
            var helper = tonk.GetHelperInsured();
            //IndexTech(tonk, tonk.Team);

            if (tonk.GetComponents<TankAIHelper>().Count() > 1)
                throw new InvalidOperationException(KickStart.ModID + ": ASSERT: THERE IS MORE THAN ONE TankAIHelper ON " + tonk.name + "!!!");

            //DebugTAC_AI.Log(KickStart.ModID + ": Allied AI " + tankInfo.name + ":  Called OnSpawn");
            //if (tankInfo.gameObject.GetComponent<TankAIHelper>().AIState != 0)
            //helper.ResetAll(tonk);
            //helper.OnTechTeamChange();
            helper.dirtyExtents = true;
            helper.dirtyAI = AIDirtyState.Dirty;
            helper.RunState = AIRunState.Advanced;
            helper.enabled = true;
            DebugTAC_AI.LogAISetup(KickStart.ModID + ": AI Helper " + tonk.name + ":  OnTankAddition - Spawned!");

            //QueueUpdater.Send();
        }
        private static void OnTankChange(Tank tonk, ManTechs.TeamChangeInfo info)
        {
            if (tonk == null)
            {
                DebugTAC_AI.Log(KickStart.ModID + ": OnTankChange tonk is NULL");
                return;
            }
            var helper = tonk.GetHelperInsured();
            RemoveTech(tonk);
            //helper.ResetAll(tonk);
            helper.OnTechTeamChange();
            if (tonk.FirstUpdateAfterSpawn)
            {
                helper.dirtyExtents = true;
                helper.dirtyAI = AIDirtyState.Dirty;
                helper.RunState = AIRunState.Advanced;
                helper.enabled = true;
                DebugTAC_AI.LogAISetup(KickStart.ModID + ": AI Helper " + tonk.name + ":  Spawned!");
            }
            IndexTech(tonk, info.m_NewTeam);
            CheckDestroyedTeams();
            //DebugTAC_AI.LogAISetup(KickStart.ModID + ": AI Helper " + tonk.name + ":  Called OnTankChange");
            //QueueUpdater.Send();
        }
        private static void OnTankRecycled(Tank recycledTech)
        {
            var helper = recycledTech.GetHelperInsured();
            recycledTech.TankRecycledEvent.Unsubscribe(OnTankRecycled);
            TechRemovedEvent.Send(helper);
            if (MissionTechs.Remove(recycledTech.visible.ID))
                MissionTechRemovedEvent.Send(recycledTech.visible.ID);
            helper.Recycled();
            RemoveTech(recycledTech);
            CheckDestroyedTeams();
            DebugTAC_AI.LogDevOnly(KickStart.ModID + ": Allied AI " + recycledTech.name + ":  Called OnTankRecycled");

            if (helper.AIControlOverride != null)
            {
                helper.AIControlOverride(helper, TankAIHelper.ExtControlStatus.Recycle);
                helper.AIControlOverride = null;
            }

            var mind = recycledTech.GetComponent<EnemyMind>();
            if ((bool)mind)
            {
#if !STEAM
                    ALossReact loss = ALossReact.Land;
                    switch (mind.EvilCommander)
                    {
                        case EnemyHandling.Naval:
                            loss = ALossReact.Sea;
                            break;
                        case EnemyHandling.Stationary:
                            loss = ALossReact.Base;
                            break;
                        case EnemyHandling.Airplane:
                        case EnemyHandling.Chopper:
                            loss = ALossReact.Air;
                            break;
                        case EnemyHandling.Starship:
                            loss = ALossReact.Space;
                            break;
                    }
                    AnimeAICompat.RespondToLoss(tonk, loss);
#endif
            }
            //QueueUpdater.Send();
        }
        private static void OnPlayerTechChange(Tank tonk, bool yes)
        {
            if (lastPlayerTech != tonk)
            {
                TankAIHelper helper;
                if (tonk != null)
                {
                    helper = tonk.GetHelperInsured();
                    //helper.OnTechTeamChange();
                    helper.ForceRebuildAlignment();
                }
                else
                {
                    try
                    {
                        if (lastPlayerTech)
                        {
                            helper = lastPlayerTech.GetHelperInsured();
                            helper.OnTechTeamChange();
                        }
                    }
                    catch { }
                }
                lastPlayerTech = tonk;
            }
        }

        /// <summary> DO NOT ALTER </summary>
        private static readonly HashSet<Tank> emptyHash = new HashSet<Tank>();
        /// <summary>  DO NOT EDIT OUTPUT </summary>
        public static IEnumerable<Tank> GetTeamTanks(int Team)
        {
            if (teamsIndexed.TryGetValue(Team, out TeamIndex TIndex))
            {
                //RemoveAllInvalid(TIndex.Teammates);
                return TIndex.Teammates;
            }
            return emptyHash;
        }
        /// <summary>  DO NOT EDIT OUTPUT </summary>
        public static HashSet<Tank> GetNonEnemyTanks(int Team)
        {
            if (teamsIndexed.TryGetValue(Team, out TeamIndex TIndex))
            {
                //RemoveAllInvalid(TIndex.NonHostile);
                return TIndex.NonHostile;
            }
            return emptyHash;
        }
        /// <summary>  DO NOT EDIT OUTPUT </summary>
        public static IEnumerable<Tank> GetTargetTanks(int Team)
        {
            if (teamsIndexed.TryGetValue(Team, out TeamIndex TIndex))
            {
                //RemoveAllInvalid(TIndex.Targets);
                return TIndex.Targets;
            }
            return emptyHash;
        }
        private static void RemoveAllInvalid(HashSet<Tank> list)
        {
            for (int step = list.Count - 1; step > -1; step--)
            {
                var ele = list.ElementAt(step);
                if (ele?.visible == null || !ele.visible.isActive)
                {
                    DebugTAC_AI.Assert(KickStart.ModID + ": RemoveAllInvalid - Tech indexes were desynced - a Tech that was null or had no blocks was in the collection!");
                    list.Remove(ele);
                }
            }
        }
        internal static void ForceReloadAll()
        {
            foreach (var item in ManTechs.inst.IterateTechs())
            {
                if (item == null)
                    continue;
                RemoveTech(item);
                IndexTech(item, item.Team);
            }
            CheckDestroyedTeams();
        }
        internal static void UpdateTechTeam(Tank tonk)
        {
            RemoveTech(tonk);
            IndexTech(tonk, tonk.Team);
            CheckDestroyedTeams();
        }

        private static List<Tank> TempReloaderCached = new List<Tank>();
        internal static void UpdateEntireTeam(int team)
        {
            TempReloaderCached.AddRange(GetTeamTanks(team));
            foreach (var item in TempReloaderCached)
            {
                RemoveTech(item);
                IndexTech(item, team);
            }
            TempReloaderCached.Clear();
            CheckDestroyedTeams();
        }
        private static void CacheTeam(int Team)
        {
            TeamIndex TI = new TeamIndex();
            foreach (var item in ManTechs.inst.IterateTechs())
            {
                if (item == null)
                    continue;
                if (ManBaseTeams.IsEnemy(item.Team, Team))
                    TI.Targets.Add(item);
                else
                    TI.NonHostile.Add(item);
            }
            //RemoveAllInvalid(TI.Targets);
            //RemoveAllInvalid(TI.NonHostile);
            teamsIndexed.Add(Team, TI);
            TeamCreatedEvent.Send(Team);
        }
        private static void IndexTech(Tank tonk, int Team)
        {
            if (tonk?.visible == null || !tonk.visible.isActive)
                return;
            //if (ManBaseTeams.IsEnemy(Team, Team))
            //    throw new InvalidOperationException("Cannot add tech which fights amongst selves - team " + TeamNamer.GetTeamName(Team));
            tonk.TankRecycledEvent.Subscribe(OnTankRecycled);
            try
            {
                if (!teamsIndexed.TryGetValue(Team, out TeamIndex TIndex))
                    CacheTeam(Team);
                foreach (KeyValuePair<int, TeamIndex> TI in teamsIndexed)
                {
                    if (TI.Key == Team)
                        TI.Value.Teammates.Add(tonk);
                    if (ManBaseTeams.IsEnemy(TI.Key, Team))
                        TI.Value.Targets.Add(tonk);
                    else
                        TI.Value.NonHostile.Add(tonk);
                }
                //DebugTAC_AI.Log("IndexTech added " + tonk.name + " of team " + Team);
            }
            catch (Exception e)
            {
                DebugTAC_AI.Log("Error in IndexTech " + e);
            }
        }
        private static void RemoveTech(Tank tonk)
        {
            if (tonk != null)
                tonk.TankRecycledEvent.Unsubscribe(OnTankRecycled);
            foreach (var TI in teamsIndexed)
            {
                TI.Value.Teammates.Remove(tonk);
                TI.Value.Targets.Remove(tonk);
                TI.Value.NonHostile.Remove(tonk);
            }
            //DebugTAC_AI.Log("RemoveTech " + tonk.name);
        }
        private static void CheckDestroyedTeams()
        {
            for (int step = teamsIndexed.Count - 1; 0 <= step; step--)
            {
                KeyValuePair<int, TeamIndex> TI = teamsIndexed.ElementAt(step);
                if (!TI.Value.Teammates.Any())
                {
                    //DebugTAC_AI.Assert("OnTeamDestroyedCheck - removed active team " + TI.Key + " due to no more teammates");
                    TeamDestroyedEvent.Send(TI.Key);
                    teamsIndexed.Remove(TI.Key);
                }
            }
            //DebugTAC_AI.Log("RemoveTech " + tonk.name);
        }

        internal void WarnPlayers()
        {
            try
            {
                //Singleton.Manager<UIMPChat>.inst.AddMissionMessage("Warning: This server is using Advanced AI!  If you are new to the game, I would suggest you play safe. Enemies RTS Mode: " + KickStart.AllowStrategicAI + "");
                SendChatServer("Warning: This server is using Advanced AI!  If you are new to the game, I would suggest you play safe. Enemies RTS Mode: " + KickStart.AllowStrategicAI + "");
            }
            catch { }
        }
        internal static string SendChatServer(string chatMsg)
        {
            try
            {
                if (ManNetwork.IsHost)
                    Singleton.Manager<ManNetworkLobby>.inst.LobbySystem.CurrentLobby.SendChat("[SERVER] " + chatMsg, -1, (uint)TTNetworkID.Invalid.GetHashCode());
            }
            catch { }
            return chatMsg;
        }
        internal void CorrectBlocksList()
        {
            BlockIndexer.ConstructBlockLookupListDelayed();
        }

        // AI comms
        internal static ManTechs.TechIterator TeamActiveTechs(int Team)
        {
            return ManTechs.inst.IterateTechsWhere(x => x.Team == Team);
        }
        internal static ManTechs.TechIterator TeamActiveMobileTechs(int Team)
        {
            return ManTechs.inst.IterateTechsWhere(x => x.Team == Team && !x.IsBase());
        }
        internal static ManTechs.TechIterator TeamActiveMobileTechsInCombat(int Team)
        {
            return ManTechs.inst.IterateTechsWhere(x => x.Team == Team && !x.IsBase() && 
            x.GetHelperInsured() is TankAIHelper helper && helper && helper.AttackEnemy && helper.lastEnemyGet);
        }
        private void RunFocusFireRequests()
        {
            foreach (KeyValuePair<int, KeyValuePair<RequestSeverity, Visible>> request in targetingRequests)
            {
                ProcessFocusFireRequestAllied(request.Key, request.Value.Value, request.Value.Key);
            }
            targetingRequests.Clear();
        }
        private static void ProcessFocusFireRequestAllied(int requestingTeam, Visible Target, RequestSeverity Priority)
        {
            try
            {
                switch (Priority)
                {
                    case RequestSeverity.ThinkMcFly:
                        foreach (Tank tech in GetTeamTanks(requestingTeam))
                        {
                            var helper = tech.GetComponent<TankAIHelper>();
                            if (!tech.IsAnchored && !helper.Retreat && helper.DediAI == AIType.Aegis)
                            {
                                helper.Provoked = AIGlobals.ProvokeTimeShort;
                                if (!(bool)helper.lastEnemyGet)
                                    helper.SetPursuit(Target);
                            }
                        }
                        break;
                    case RequestSeverity.Warn:
                        foreach (Tank tech in GetNonEnemyTanks(requestingTeam))
                        {
                            var helper = tech.GetComponent<TankAIHelper>();
                            if (!tech.IsAnchored && !helper.Retreat && (!ManSpawn.IsPlayerTeam(tech.Team) || helper.DediAI == AIType.Aegis))
                            {
                                helper.Provoked = AIGlobals.ProvokeTime;
                                if (!(bool)helper.lastEnemyGet)
                                    helper.SetPursuit(Target);
                            }
                        }
                        break;
                    case RequestSeverity.SameTeam:
                        foreach (Tank tech in GetTeamTanks(requestingTeam))
                        {
                            var helper = tech.GetComponent<TankAIHelper>();
                            helper.Provoked = AIGlobals.ProvokeTime;
                            if (!(bool)helper.lastEnemyGet)
                                helper.SetPursuit(Target);
                        }
                        break;
                    case RequestSeverity.AllHandsOnDeck:
                        foreach (Tank tech in GetNonEnemyTanks(requestingTeam))
                        {
                            var helper = tech.GetComponent<TankAIHelper>();
                            if (!tech.IsAnchored || (ManSpawn.IsPlayerTeam(tech.Team) && (helper.DediAI == AIType.Aegis || helper.AdvancedAI)))
                            {
                                helper.Provoked = AIGlobals.ProvokeTime;
                                if (!(bool)helper.lastEnemyGet)
                                    helper.SetPursuit(Target);
                            }
                        }
                        break;
                    default:
                        break;
                }
            }
            catch { }
        }

        private void OnPaused(bool state)
        {
            inst.enabled = !state;
        }
        private void ManageTimeRunner()
        {
            if (Time.timeScale > 0)
            {
                DeltaRealTime = Time.realtimeSinceStartup - LastRealTime;
                if (ManWorldRTS.PlayerIsInRTS && !ManNetwork.IsNetworked)
                {
                    if (Input.GetKey(KeyCode.LeftControl))
                    {
                        TargetTime = SlowedTime;
                    }
                    else if (Input.GetKey(KeyCode.RightControl))
                    {
                        TargetTime = FastTime;
                    }
                    else
                    {
                        TargetTime = DefaultTime;
                    }
                }
                else
                {
                    TargetTime = DefaultTime;
                }
                Time.timeScale = Mathf.MoveTowards(Time.timeScale, TargetTime, ChangeRate * DeltaRealTime);
            }
            LastRealTime = Time.realtimeSinceStartup;
        }
        public static bool IsPlayerRTSControlled(Tank tank) => PlayerControlledTanks.Contains(tank);
        private static HashSet<Tank> PlayerControlledTanks = new HashSet<Tank>();
        private void Update()
        {
            PlayerControlledTanks.Clear();
            if (ManNetwork.IsNetworked)
            {
                foreach (var item in ManNetwork.inst.GetAllPlayerTechs())
                    PlayerControlledTanks.Add(item);
            }
            else if (Singleton.playerTank)
                PlayerControlledTanks.Add(Singleton.playerTank);
            AIGlobals.SceneTechCount = -1;
            AIGlobals.HideHud = AIGlobals.GetHideHud;
            if (GravVector != Physics.gravity)
            {
                GravVector = Physics.gravity;
                GravMagnitude = GravVector.magnitude;
            }

            // if (Input.GetKeyDown(KeyCode.Quote))  AIECore.debugVisuals = !AIECore.debugVisuals;

            if (Input.GetKeyDown(KickStart.RetreatHotkey) && ManHUD.inst.HighlightedOverlay == null)
                AIECore.ToggleTeamRetreat(Singleton.Manager<ManPlayer>.inst.PlayerTeam);
            if (Singleton.playerTank)
            {
                var helper = Singleton.playerTank.GetComponent<TankAIHelper>();
                if (Input.GetMouseButton(0) && Singleton.playerTank.control.FireControl && ManPointer.inst.targetVisible)
                {
                    Visible couldBeObst = ManPointer.inst.targetVisible;
                    if (couldBeObst.GetComponent<ResourceDispenser>())
                    {
                        if ((couldBeObst.centrePosition - Singleton.playerTank.visible.centrePosition).sqrMagnitude <= 10000)
                        {
                            if (!Singleton.playerTank.Vision.GetFirstVisibleTechIsEnemy(Singleton.playerTank.Team))
                            {
                                helper.Obst = couldBeObst.transform;
                                helper.ActiveAimState = AIWeaponState.Obsticle;
                                goto conclusion; // I hate doing gotos but this is the only "fast" way
                            }
                        }
                    }
                }
                if (helper.Obst != null)
                {
                    helper.Obst = null;
                    helper.ActiveAimState = AIWeaponState.Normal;
                }
                conclusion:;
            }
            if (lastCombatTime > 6)
            {
                if (ManEncounterPlacement.IsOverlappingEncounter(Singleton.playerPos, 64, false))
                    AIECore._playerIsInNonCombatZone = true;
                if (AIECore.PlayerCombatLastState != AIECore._playerIsInNonCombatZone)
                {
                    AIECore.PlayerCombatLastState = AIECore._playerIsInNonCombatZone;
                }
                lastCombatTime = 0;
                if (Singleton.playerTank)
                if (ManWorld.inst.GetTerrainHeight(Singleton.playerTank.boundsCentreWorldNoCheck, out float height))
                    terrainHeight = height;
                if (!AIWiki.TooHighed && terrainHeight > AIGlobals.AirPromoteSpaceHeight)
                {
                    if (AIWiki.hintSpaceTooHigh.Show())
                        AIWiki.TooHighed = true;
                }
            }
            else
                lastCombatTime += Time.deltaTime;
            ManageTimeRunner();
            RunFocusFireRequests();
        }

        private List<TankAIHelper> helpersActive = new List<TankAIHelper>();
        private int clockHelperStepDirectors = 0;
        private int clockHelperStepOperations = 0;

        private float DirectorUpdateClock = 0;
        private float OperationsUpdateClock = 500;
        private int DirectorsToUpdateThisFrame()
        {
            DirectorUpdateClock += (float)helpersActive.Count / KickStart.AIDodgeCheapness;
            int count = Mathf.FloorToInt(DirectorUpdateClock);
            DirectorUpdateClock -= count;
            return count;
        }
        private int OperationsToUpdateThisFrame()
        {
            OperationsUpdateClock += (float)helpersActive.Count / KickStart.AIClockPeriod;
            int count = Mathf.FloorToInt(OperationsUpdateClock);
            OperationsUpdateClock -= count;
            return count;
        }
        private void UpdateAllHelpers()
        {
            if (!KickStart.EnableBetterAI)
                return;
            foreach (var helper in AIECore.IterateAllHelpers())
                helpersActive.Add(helper);
            try
            {
                foreach (var item in helpersActive)
                    item.OnPreUpdate();
            }
            catch (Exception e)
            {
                throw new Exception("A significant cascading failiure happened while updating all TankAIHelper.OnPreUpdate()", e);
            }
            StaggerUpdateAllHelpersDirAndOps();
            try
            {
                foreach (var item in helpersActive)
                    item.OnPostUpdate();
            }
            catch (Exception e)
            {
                throw new Exception("A significant cascading failiure happened while updating all TankAIHelper.OnPostUpdate()", e);
            }
            helpersActive.Clear();
        }
        private void StaggerUpdateAllHelpersDirAndOps()
        {
            int numDirUpdate = Mathf.Min(helpersActive.Count, DirectorsToUpdateThisFrame());
            int numOpUpdate = Mathf.Min(helpersActive.Count, OperationsToUpdateThisFrame());
            if (ManNetwork.IsHost)
            {
                try
                {
                    while (numDirUpdate > 0)
                    {
                        if (clockHelperStepDirectors >= helpersActive.Count)
                            clockHelperStepDirectors = 0;
                        helpersActive[clockHelperStepDirectors].OnUpdateHostAIDirectors();
                        clockHelperStepDirectors++;
                        numDirUpdate--;
                    }
                }
                catch (Exception e)
                {
                    throw new Exception("(Networked: " + ManNetwork.IsNetworked + ") A significant cascading failiure happened while updating all TankAIHelper.OnUpdateHostAIDirectors()", e);
                }
                try
                {
                    while (numOpUpdate > 0)
                    {
                        if (clockHelperStepOperations >= helpersActive.Count)
                            clockHelperStepOperations = 0;
                        helpersActive[clockHelperStepOperations].OnUpdateHostAIOperations();
                        clockHelperStepOperations++;
                        numOpUpdate--;
                    }
                }
                catch (Exception e)
                {
                    throw new Exception("(Networked: " + ManNetwork.IsNetworked + ") A significant cascading failiure happened while updating all TankAIHelper.OnUpdateHostAIOperations()", e);
                }
            }
            else
            {
                try
                {
                    while (numDirUpdate > 0)
                    {
                        if (clockHelperStepDirectors >= helpersActive.Count)
                            clockHelperStepDirectors = 0;
                        helpersActive[clockHelperStepDirectors].OnUpdateClientAIDirectors();
                        clockHelperStepDirectors++;
                        numDirUpdate--;
                    }
                }
                catch (Exception e)
                {
                    throw new Exception("(Networked: " + ManNetwork.IsNetworked + ") A significant cascading failiure happened while updating all TankAIHelper.OnUpdateClientAIDirectors()", e);
                }
                try
                {
                    while (numOpUpdate > 0)
                    {
                        if (clockHelperStepOperations >= helpersActive.Count)
                            clockHelperStepOperations = 0;
                        helpersActive[clockHelperStepOperations].OnUpdateClientAIOperations();
                        clockHelperStepOperations++;
                        numOpUpdate--;
                    }
                }
                catch (Exception e)
                {
                    throw new Exception("(Networked: " + ManNetwork.IsNetworked + ") A significant cascading failiure happened while updating all TankAIHelper.OnUpdateClientAIOperations()", e);
                }
            }
        }
        private void FixedUpdate()
        {
            if (!ManPauseGame.inst.IsPaused && KickStart.EnableBetterAI)
            {
                try
                {
                    UpdateAllHelpers();
                }
                catch (Exception e)
                {
                    if (!TankAIHelper.updateErrored)
                    {
                        DebugTAC_AI.LogWarnPlayerOnce("TankAIManager.FixedUpdate() Critical error", e);
                        TankAIHelper.updateErrored = true;
                    }
                }
            }
        }


        internal class GUIManaged
        {
            private static bool controlledDisp = false;
            private static bool typesDisp = false;
            private static HashSet<AIType> enabledTabs = null;
            public static void GUIGetTotalManaged()
            {
                if (enabledTabs == null)
                {
                    enabledTabs = new HashSet<AIType>();
                }
                GUILayout.Box("--- Helpers --- ");
                int activeCount = 0;
                int baseCount = 0;
                Dictionary<AIAlignment, int> alignments = new Dictionary<AIAlignment, int>();
                foreach (AIAlignment item in Enum.GetValues(typeof(AIAlignment)))
                {
                    alignments.Add(item, 0);
                }
                Dictionary<AIType, int> types = new Dictionary<AIType, int>();
                foreach (AIType item in Enum.GetValues(typeof(AIType)))
                {
                    types.Add(item, 0);
                }
                foreach (var helper in AIECore.IterateAllHelpers())
                {
                    activeCount++;
                    alignments[helper.AIAlign]++;
                    types[helper.DediAI]++;
                    if (helper.tank.IsAnchored)
                        baseCount++;
                }
                GUILayout.Label("  Capacity: " + KickStart.MaxEnemyWorldCapacity);
                GUILayout.Label("  Num Bases: " + baseCount);
                if (GUILayout.Button("Pooled: " + AIECore.HelperCountNoCheck + " | Active: " + activeCount))
                    controlledDisp = !controlledDisp;
                if (controlledDisp)
                {
                    foreach (var item in alignments)
                    {
                        GUILayout.Label("  Alignment: " + item.Key.ToString() + " - " + item.Value);
                    }
                }
                if (GUILayout.Button("Types: " + types.Count))
                    typesDisp = !typesDisp;
                if (typesDisp)
                {
                    foreach (var item in types)
                    {
                        if (GUILayout.Button("Type: " + item.Key.ToString() + " - " + item.Value))
                        {
                            if (enabledTabs.Contains(item.Key))
                                enabledTabs.Remove(item.Key);
                            else
                                enabledTabs.Add(item.Key);
                        }
                        if (enabledTabs.Contains(item.Key))
                        {
                            foreach (var item2 in AIECore.IterateAllHelpers(x => x.DediAI == item.Key))
                            {
                                Vector3 pos = item2.tank.boundsCentreWorldNoCheck;
                                GUILayout.Label("  Tech: " + item2.tank.name + " | Pos: " + pos);
                                DebugExtUtilities.DrawDirIndicator(pos, pos + new Vector3(0, 32, 0), Color.white);
                            }
                        }
                    }
                }
            }
        }
    }
}
