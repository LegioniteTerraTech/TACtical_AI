﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SafeSaves;
using UnityEngine;
using UnityEngine.Networking;
using TerraTechETCUtil;
using TAC_AI.AI;
using TAC_AI.AI.Enemy;
using TAC_AI.Templates;
using TAC_AI.World;

namespace TAC_AI
{
    public enum TeamRelations : int
    {
        Enemy, // Just leave it null
        HoldFire,
        Neutral,
        Friendly,
        AITeammate,
    }
    public static class TeamBasePointerExt
    {
        public static bool IsValid(this TeamBasePointer point) => point != null && point.valid;
    }
    public interface TeamBasePointer
    {
        Tank tank { get; }
        int BuildBucks { get; }
        void AddBuildBucks(int value);
        void SetBuildBucks(int value);
        int Team { get; }
        WorldPosition WorldPos { get; }
        int BlockCount { get; }
        bool valid { get; }

    }
    [Serializable]
    public class EnemyTeamData
    {
        public int teamID;
        internal int Team => teamID;
        public string teamName;
        private int buildBucks;
        internal int BuildBucks => buildBucks;
        public int SetBuildBucks
        {
            set
            {
                if (buildBucks != value)
                {
                    buildBucks = value;
                    bankrupt = buildBucks <= 0;
                    ManBaseTeams.BuildBucksUpdatedEvent.Send(teamID, value);
                }
            }
        }
        private int AddBuildBucks_Internal
        {
            set
            {
                int prevVal = buildBucks;
                checked
                {
                    try
                    {
                        buildBucks += value;
                    }
                    catch (OverflowException)
                    {
                        buildBucks = int.MaxValue;
                    }
                }

                if (buildBucks != prevVal)
                {
                    bankrupt = buildBucks <= 0;
                    ManBaseTeams.BuildBucksUpdatedEvent.Send(teamID, value);
                }
            }
        }
        internal bool bankrupt = false;
        /// <summary> This team has not enough Build Bucks </summary>
        internal bool Bankrupt => bankrupt;

        internal TeamBasePointer HQ  => _HQ;
        private TeamBasePointer _HQ = null;
        public int PlayerTeam = int.MinValue;

        public int relationInt = 0;


        internal TeamRelations relations
        {
            get => (TeamRelations)relationInt;
            set { relationInt = (int)value; }
        }
        /// <summary> teamID, TeamAlignment</summary>
        public Dictionary<int, TeamRelations> align = new Dictionary<int, TeamRelations>();

        internal HashSet<Tank> AllTechsIterator => TankAIManager.GetTeamTanks(teamID);

        public bool HasAnyTechsLeftAlive()
        {
            if (ManEnemyWorld.GetTeam(Team) != null)
                return true;
            if (AllTechsIterator.Any())
                return true;
            return false;
        }


        /// <summary> SERIALIZATION ONLY </summary>
        public EnemyTeamData() { }
        public EnemyTeamData(int team)
        {
            teamID = team;
            teamName = TeamNamer.GetTeamName(teamID);
            DebugTAC_AI.Log("New Team of name " + teamName + ", ID " + team);
        }
        public TeamRelations Alignment(int teamOther)
        {
            if (align == null)
                throw new NullReferenceException("EnemyTeamData align IS NULL");
            if (PlayerTeam != int.MinValue && ManBaseTeams.TryGetBaseTeam(teamOther, out var other) &&
                    other.align.TryGetValue(PlayerTeam, out var relate))
            {
                return relate;
            }
            else if (align.TryGetValue(teamOther, out relate))
            {
                return relate;
            }
            return relations;
        }


        // EnemyPurchase
        public void AddBuildBucks(int toAdd)
        {
            AddBuildBucks_Internal = toAdd;
        }
        public void SpendBuildBucks(int toUse)
        {
            AddBuildBucks_Internal = -toUse;
        }
        public int StealBuildBucks(float stealSeverity = 0.2f)
        {
            int stealAmount = Mathf.CeilToInt(BuildBucks * UnityEngine.Random.Range(0, stealSeverity));
            AddBuildBucks_Internal = -stealAmount;
            return stealAmount;
        }
        public bool PurchasePossible(int BBCost)
        {
            return BBCost <= BuildBucks;
        }
        public bool TryMakePurchase(BlockTypes bloc)
        {
            return TryMakePurchase(Singleton.Manager<RecipeManager>.inst.GetBlockBuyPrice(bloc, true));
        }
        public bool TryMakePurchase(int Pay)
        {
            if (PurchasePossible(Pay))
            {
                SpendBuildBucks(Pay);
                return true;
            }
            return false;
        }
        public void FlagBankrupt()
        {
            bankrupt = true;
        }

        public void ImproveRelations(int team)
        {
            TeamRelations TRP = Alignment(team);
            if (TRP >= TeamRelations.AITeammate)
                return;
            TeamRelations TRN = (TeamRelations)Mathf.Clamp((int)TRP + 1, 0, Enum.GetValues(typeof(TeamRelations)).Length);
            if (DebugRawTechSpawner.ShowDebugFeedBack)
            {
                switch (TRN)
                {
                    case TeamRelations.Enemy:
                        AIGlobals.PopupEnemyInfo(TRP.ToString() + " -> " + TRN.ToString(),
                            WorldPosition.FromScenePosition(Singleton.playerPos +
                            (Singleton.camera.transform.forward * 12)));
                        break;
                    case TeamRelations.HoldFire:
                    case TeamRelations.Neutral:
                        AIGlobals.PopupNeutralInfo(TRP.ToString() + " -> " + TRN.ToString(),
                            WorldPosition.FromScenePosition(Singleton.playerPos +
                            (Singleton.camera.transform.forward * 12)));
                        break;
                    case TeamRelations.Friendly:
                        AIGlobals.PopupAllyInfo(TRP.ToString() + " -> " + TRN.ToString(),
                            WorldPosition.FromScenePosition(Singleton.playerPos +
                            (Singleton.camera.transform.forward * 12)));
                        break;
                    default:
                        AIGlobals.PopupPlayerInfo(TRP.ToString() + " -> " + TRN.ToString(),
                            WorldPosition.FromScenePosition(Singleton.playerPos +
                            (Singleton.camera.transform.forward * 12)));
                        break;
                }
            }
            Set(team, TRN);
        }
        public void DegradeRelations(int team)
        {
            TeamRelations TRP = Alignment(team);
            if (TRP <= TeamRelations.Enemy)
                return;
            TeamRelations TRN = (TeamRelations)Mathf.Clamp((int)TRP - 1, 0, Enum.GetValues(typeof(TeamRelations)).Length);
            if (DebugRawTechSpawner.ShowDebugFeedBack)
            {
                switch (TRN)
                {
                    case TeamRelations.Enemy:
                        AIGlobals.PopupEnemyInfo(TRP.ToString() + " -> " + TRN.ToString(),
                            WorldPosition.FromScenePosition(Singleton.playerPos +
                            (Singleton.camera.transform.forward * 12)));
                        break;
                    case TeamRelations.HoldFire:
                    case TeamRelations.Neutral:
                        AIGlobals.PopupNeutralInfo(TRP.ToString() + " -> " + TRN.ToString(),
                            WorldPosition.FromScenePosition(Singleton.playerPos +
                            (Singleton.camera.transform.forward * 12)));
                        break;
                    case TeamRelations.Friendly:
                        AIGlobals.PopupAllyInfo(TRP.ToString() + " -> " + TRN.ToString(),
                            WorldPosition.FromScenePosition(Singleton.playerPos +
                            (Singleton.camera.transform.forward * 12)));
                        break;
                    default:
                        AIGlobals.PopupPlayerInfo(TRP.ToString() + " -> " + TRN.ToString(),
                            WorldPosition.FromScenePosition(Singleton.playerPos +
                            (Singleton.camera.transform.forward * 12)));
                        break;
                }
            }
            Set(team, TRN);
        }

        public void Set(int team, TeamRelations relate)
        {
            align[team] = relate;
            if (ManBaseTeams.inst.teams.TryGetValue(team, out var val) && val.Alignment(teamID) != relate)
            {
                val.align[teamID] = relate;
            }
            ManBaseTeams.TeamAlignmentDeltaEvent.Send(teamID);
        }
        public void SetNeutral(int team)
        {
            Set(team, TeamRelations.Neutral);
        }
        public void SetHoldFire(int team)
        {
            Set(team, TeamRelations.HoldFire);
        }
        public void SetFriendly(int team)
        {
            Set(team, TeamRelations.Friendly);
        }
        public void SetEnemy(int team)
        {
            Set(team, TeamRelations.Enemy);
        }

        private static List<TeamBasePointer> baseFundersCache = new List<TeamBasePointer>();
        /// <summary>
        /// This is VERY lazy.  There's only a small chance it will actually transfer the funds to the real strongest base.
        /// <para>This is normally handled automatically by the manager, but you can call this if you want to move the cash NOW</para>
        /// </summary>
        /// <param name="funds">The EnemyBaseFunder that contains the money to move</param>
        /// <returns>True if it actually moved the money</returns>
        public bool SetHQToStrongestOrRandomBase()
        {
            try
            {
                RLoadedBases.CollectTeamBaseFunders(Team, baseFundersCache);
                var teamInstU = ManEnemyWorld.GetTeam(Team);
                if (teamInstU != null)
                {
                    foreach (var item in teamInstU.EBUs)
                    {
                        baseFundersCache.Add(item);
                    }
                }
                int baseSize = 0;
                foreach (TeamBasePointer fundC in baseFundersCache)
                {
                    int blockC = fundC.BlockCount;
                    if (baseSize < blockC)
                    {
                        baseSize = blockC;
                        _HQ = fundC;
                    }
                }
            }
            finally
            {
                baseFundersCache.Clear();
            }
            return true;
        }

        internal void ManageBases()
        {
            NP_Presence presence = ManEnemyWorld.GetTeam(Team);
            if (presence != null)
                UnloadedBases.TryUnloadedBaseOperations(presence);
            if (HQ != null)
            {
                if (HQ is RLoadedBases.EnemyBaseFunder funder)
                {
                    if (funder)
                    {
                        EnemyMind mind = funder.GetComponent<EnemyMind>();
                        if (mind)
                            RLoadedBases.UpdateBaseOperations(mind);
                    }
                }
                else if (HQ is NP_BaseUnit funderU)
                {
                    /*
                    if (funderU.IsValid())
                    {
                        NP_Presence presence = ManEnemyWorld.GetTeam(Team);
                        if (presence != null)
                            UnloadedBases.TryUnloadedBaseOperations(presence);
                    }*/
                }
            }
        }
    }

    [AutoSaveManager]
    public class ManBaseTeams : MonoBehaviour
    {
        public const float percentChanceExisting = 0.35f;
        [SSManagerInst]
        public static ManBaseTeams inst = null;
        public int ready = 0;
        private int lowTeam = AIGlobals.EnemyTeamsRangeStart;
        [SSaveField]
        public Dictionary<int, EnemyTeamData> teams = new Dictionary<int, EnemyTeamData>();
        /// <summary> Team, Amount </summary>
        public static Event<int, int> BuildBucksUpdatedEvent = new Event<int, int>();
        public static Event<int> TeamAlignmentDeltaEvent = new Event<int>();
        public static Event<int> TeamRemovedEvent = new Event<int>();


        public static void Initiate()
        {
            if (inst)
                return;
            inst = new GameObject("ManEnemyTeams").AddComponent<ManBaseTeams>();
            ManGameMode.inst.ModeStartEvent.Subscribe(OnModeStart);
            ManGameMode.inst.ModeSwitchEvent.Subscribe(OnModeSwitch);
            TankAIManager.TeamDestroyedEvent.Subscribe(OnTeamDestroyedCheck);
            ManEnemyWorld.TeamDestroyedEvent.Subscribe(OnTeamDestroyedCheck);
            InsureNetHooks();
            if (inst.teams == null)
                inst.teams = new Dictionary<int, EnemyTeamData>();
        }
        public static void DeInit()
        {
            if (!inst)
                return;
            ManEnemyWorld.TeamDestroyedEvent.Unsubscribe(OnTeamDestroyedCheck);
            TankAIManager.TeamDestroyedEvent.Unsubscribe(OnTeamDestroyedCheck);
            ManGameMode.inst.ModeSwitchEvent.Unsubscribe(OnModeSwitch);
            ManGameMode.inst.ModeStartEvent.Unsubscribe(OnModeStart);
            Destroy(inst.gameObject);
            inst = null;
        }

        public static void OnModeSwitch()
        {
            try
            {
                inst.teams.Clear();
                inst.ready = 0;
                inst.lowTeam = AIGlobals.EnemyTeamsRangeStart;
            }
            catch { }
        }
        public static void OnModeStart(Mode mode)
        {
            try
            {
                if (inst.teams == null)
                    inst.teams = new Dictionary<int, EnemyTeamData>();
                CheckNeedNetworkHooks(mode);
            }
            catch { }
        }


        public static void OnTeamDestroyedCheck(int team)
        {
            if (inst.teams != null && inst.teams.TryGetValue(team, out var teamInst) && !teamInst.HasAnyTechsLeftAlive())
            {
                DebugTAC_AI.Log("OnTeamDestroyedCheck - Team " + TeamNamer.GetTeamName(team) + " has been completely obliterated!");
                TeamRemovedEvent.Send(team);
                inst.teams.Remove(team);
            }
        }
        public static void OnTeamDestroyedRemoteClient(int team)
        {
            DebugTAC_AI.Log("OnTeamDestroyedRemote - Team " + TeamNamer.GetTeamName(team) + " has been completely obliterated!");
            TeamRemovedEvent.Send(team);
            inst.teams.Remove(team);
        }



        public static EnemyTeamData GetNewBaseTeam()
        {
            checked
            {
                try
                {
                    while (inst.teams.ContainsKey(inst.lowTeam))
                    {
                        inst.lowTeam--;
                    }
                    var valNew = new EnemyTeamData(inst.lowTeam);
                    inst.teams.Add(inst.lowTeam, valNew);
                    inst.lowTeam--;
                    return valNew;
                }
                catch (OverflowException)
                {
                    DebugTAC_AI.Assert("GetNewBaseTeam has run out of indicies!");
                    return InsureBaseTeam(AIGlobals.EnemyTeamsRangeStart);
                }
            }
        }
        public static EnemyTeamData GetTeamAIBaseTeam(int team)
        {
            checked
            {
                try
                {
                    var findable = inst.teams.Values.ToList().Find(x => x.Alignment(team) == TeamRelations.AITeammate);
                    if (findable != null)
                        return findable;
                    while (inst.teams.ContainsKey(inst.lowTeam))
                    {
                        inst.lowTeam--;
                    }
                    var valNew = new EnemyTeamData(inst.lowTeam);
                    valNew.Set(team, TeamRelations.AITeammate);
                    valNew.PlayerTeam = team;
                    inst.teams.Add(inst.lowTeam, valNew);
                    inst.lowTeam--;
                    return valNew;
                }
                catch (OverflowException)
                {
                    DebugTAC_AI.Assert("GetNewBaseTeam has run out of indicies!");
                    return InsureBaseTeam(AIGlobals.EnemyTeamsRangeStart);
                }
            }
        }
        internal static EnemyTeamData InsureBaseTeam(int team)
        {
            if (inst.teams.TryGetValue(team, out var val))
                return val;
            else
            {
                if (AIGlobals.IsPlayerTeam(team))
                    throw new InvalidOperationException("Player team cannot be assigned as a BaseTeam");
                var valNew = new EnemyTeamData(team);
                inst.teams.Add(team, valNew);
                return valNew;
            }
        }
        internal static bool InsureBaseTeam(int team, out EnemyTeamData ETD)
        {
            if (!inst.teams.TryGetValue(team, out ETD))
            {
                ETD = InsureBaseTeam(team);
            }
            return true;
        }
        public static bool BaseTeamExists(int team)
        {
            return inst.teams.ContainsKey(team);
        }
        public static bool TryGetBaseTeam(int team, out EnemyTeamData ETD)
        {
            if (inst.teams.TryGetValue(team, out ETD))
            {
                return true;
            }
            ETD = null;
            return false;
        }
        public static EnemyTeamData GetRandomExistingBaseTeam()
        {
            return inst.teams.Values.ToList().GetRandomEntry();
        }

        public static bool IsBaseTeam(int team)
        {
            return team == SpecialAISpawner.trollTeam || (!AIGlobals.IsPlayerTeam(team) && inst.teams.ContainsKey(team));
        }

        public static bool IsTeamHQ(TeamBasePointer pointer)
        {
            if (TryGetBaseTeam(pointer.Team, out var ETD))
                return ETD == pointer;
            return false;
        }
        public static int GetTeamMoney(int team)
        {
            if (TryGetBaseTeam(team, out var ETD))
                return ETD.BuildBucks;
            return 0;
        }


        // Relations
        public static TeamRelations GetRelations(int teamID1, int teamID2)
        {
            if (inst.teams.TryGetValue(teamID1, out var val))
                return val.Alignment(teamID2);
            if (inst.teams.TryGetValue(teamID2, out var val2))
                return val2.Alignment(teamID1);
            return TeamRelations.Neutral;
        }
        public static bool Relations(int teamID1, int teamID2, TeamRelations relation)
        {
            if (inst.teams.TryGetValue(teamID1, out var val))
                return val.Alignment(teamID2) == relation;
            if (inst.teams.TryGetValue(teamID2, out var val2))
                return val2.Alignment(teamID1) == relation;
            return false;
        }
        public static bool RelationsLess(int teamID1, int teamID2, TeamRelations relation)
        {
            if (inst.teams.TryGetValue(teamID1, out var val))
                return relation >= val.Alignment(teamID2);
            if (inst.teams.TryGetValue(teamID2, out var val2))
                return relation >= val2.Alignment(teamID1);
            return relation == TeamRelations.AITeammate;
        }
        public static bool RelationsGreater(int teamID1, int teamID2, TeamRelations relation)
        {
            if (inst.teams.TryGetValue(teamID1, out var val))
                return relation <= val.Alignment(teamID2);
            if (inst.teams.TryGetValue(teamID2, out var val2))
                return relation <= val2.Alignment(teamID1);
            return relation == TeamRelations.Enemy;
        }
        public static bool IsEnemy(int teamID1, int teamID2)
        {
            return RelationsLess(teamID1, teamID2, TeamRelations.HoldFire);
        }
        public static bool IsFriendly(int teamID1, int teamID2)
        {
            return RelationsGreater(teamID1, teamID2, TeamRelations.Friendly);
        }
        public static bool IsUnattackable(int teamID1, int teamID2)
        {
            return RelationsGreater(teamID1, teamID2, TeamRelations.Neutral);
        }
        public static bool IsTeammate(int teamID1, int teamID2)
        {
            return Relations(teamID1, teamID2, TeamRelations.AITeammate);
        }

        private static int playerTeam => ManPlayer.inst.PlayerTeam;
        public static bool IsEnemyBaseTeam(int team)
        {
            return team == SpecialAISpawner.trollTeam || (inst.teams.TryGetValue(team, out var val) && val.Alignment(playerTeam) == TeamRelations.Enemy);
        }
        public static bool IsNonAggressiveTeam(int team)
        {
            return team != SpecialAISpawner.trollTeam && inst.teams.TryGetValue(team, out var val) && val.Alignment(playerTeam) != TeamRelations.Enemy;
        }
        public static bool IsSubNeutralBaseTeam(int team)
        {
            return inst.teams.TryGetValue(team, out var val) && val.Alignment(playerTeam) == TeamRelations.HoldFire;
        }
        public static bool IsNeutralBaseTeam(int team)
        {
            return inst.teams.TryGetValue(team, out var val) && val.Alignment(playerTeam) == TeamRelations.Neutral;
        }
        public static bool IsFriendlyBaseTeam(int team)
        {
            return inst.teams.TryGetValue(team, out var val) && val.Alignment(playerTeam) >= TeamRelations.Friendly;
        }


        internal static void UpdateTeams()
        {
            foreach (var item in inst.teams.Values)
            {
                int Team = item.Team;
                if (AIECore.RetreatingTeams.Contains(Team))
                {
                    float averageTechDMG = 0;
                    int count = 0;
                    foreach (var item2 in TankAIManager.TeamActiveMobileTechs(Team))
                    {
                        averageTechDMG += item2.GetHelperInsured().DamageThreshold;
                        count++;
                    }
                    if (averageTechDMG == 0)
                        return;
                    averageTechDMG /= count;
                    if (averageTechDMG <= AIGlobals.RetreatBelowTeamDamageThreshold)
                        AIECore.TeamRetreat(Team, false, true);
                }
                item.ManageBases();
            }
        }


        public void MigrateTeamsToNewSaveFormat()
        {
            int count = 0;
            try
            {
                int playerTeam = ManPlayer.inst.PlayerTeam;
                HashSet<int> loaded = new HashSet<int>(); // INFREQUENTLY CALLED
                foreach (var item in ManTechs.inst.IterateTechs())
                {
                    loaded.Add(item.visible.ID);
                }
                if (Singleton.Manager<ManSaveGame>.inst.CurrentState.m_StoredTiles != null)
                {
                    foreach (var item in new Dictionary<IntVector2, ManSaveGame.StoredTile>(Singleton.Manager<ManSaveGame>.inst.CurrentState.m_StoredTiles))
                    {
                        ManSaveGame.StoredTile storedTile = item.Value;
                        if (storedTile != null && storedTile.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> techs)
                            && techs.Count > 0)
                        {
                            foreach (ManSaveGame.StoredVisible Vis in techs)
                            {
                                if (Vis is ManSaveGame.StoredTech tech && !loaded.Contains(tech.m_ID))
                                {
                                    switch (GetNPTTeamType(tech.m_TeamID))
                                    {
                                        case NP_Types.Friendly:
                                            InsureBaseTeam(tech.m_TeamID).SetFriendly(playerTeam);
                                            break;
                                        case NP_Types.Neutral:
                                            InsureBaseTeam(tech.m_TeamID).relations = TeamRelations.Neutral;
                                            break;
                                        case NP_Types.SubNeutral:
                                            InsureBaseTeam(tech.m_TeamID).relations = TeamRelations.HoldFire;
                                            break;
                                        case NP_Types.Enemy:
                                            InsureBaseTeam(tech.m_TeamID).SetEnemy(playerTeam);
                                            break;
                                    }
                                    count++;
                                }
                            }
                        }
                    }
                }
                if (Singleton.Manager<ManSaveGame>.inst.CurrentState.m_StoredTilesJSON != null)
                {
                    foreach (var item in new Dictionary<IntVector2, string>(Singleton.Manager<ManSaveGame>.inst.CurrentState.m_StoredTilesJSON))
                    {
                        ManSaveGame.StoredTile storedTile = null;
                        ManSaveGame.LoadObjectFromRawJson(ref storedTile, item.Value, false, false);
                        if (storedTile != null && storedTile.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> techs)
                            && techs.Count > 0)
                        {
                            foreach (ManSaveGame.StoredVisible Vis in techs)
                            {
                                if (Vis is ManSaveGame.StoredTech tech && !loaded.Contains(tech.m_ID))
                                {
                                    switch (GetNPTTeamType(tech.m_TeamID))
                                    {
                                        case NP_Types.Friendly:
                                            InsureBaseTeam(tech.m_TeamID).SetFriendly(playerTeam);
                                            break;
                                        case NP_Types.Neutral:
                                            InsureBaseTeam(tech.m_TeamID).relations = TeamRelations.Neutral;
                                            break;
                                        case NP_Types.SubNeutral:
                                            InsureBaseTeam(tech.m_TeamID).relations = TeamRelations.HoldFire;
                                            break;
                                        case NP_Types.Enemy:
                                            InsureBaseTeam(tech.m_TeamID).SetEnemy(playerTeam);
                                            break;
                                    }
                                    count++;
                                }
                            }
                        }

                    }
                }
                if (count > 0)
                    DebugTAC_AI.Log("TACtical_AI: MigrateTeamsToNewSaveFormat Handled " + count + " Techs");
            }
            catch (Exception e)
            {
                DebugTAC_AI.Log("TACtical_AI: MigrateTeamsToNewSaveFormat FAILED at " + count + " Techs - " + e);
            }
        }


        public static void OnWorldSave()
        {
            try
            {
            }
            catch { }
        }
        public static void OnWorldFinishSave()
        {
            try
            {
                DebugTAC_AI.Log("Saved " + inst.teams.Count + " NPT base teams.");
            }
            catch { }
        }
        public static void OnWorldPreLoad()
        {
            try
            {
                //inst.teams = null;
            }
            catch { }
        }
        public static void OnWorldLoad()
        {
            try
            {
                if (inst.teams == null)
                {
                    inst.teams = new Dictionary<int, EnemyTeamData>();
                    inst.MigrateTeamsToNewSaveFormat();
                }
                else
                {
                    foreach (var item in inst.teams)
                    {
                        DebugTAC_AI.Log("  Team " + item.Value.teamName + ", relation " + item.Value.Alignment(playerTeam));
                    }
                    DebugTAC_AI.Log("Loaded " + inst.teams.Count + " NPT base teams.");
                }
            }
            catch { }
        }


        internal class GUIManaged
        {
            private static bool typesDisp = false;
            private static HashSet<int> enabledTabs = null;
            public static void GUIGetTotalManaged()
            {
                if (enabledTabs == null)
                {
                    enabledTabs = new HashSet<int>();
                }
                GUILayout.Box("--- Team Alignments --- ");
                if (GUILayout.Button("  Total: " + inst.teams.Count))
                    typesDisp = !typesDisp;
                if (typesDisp)
                {
                    foreach (var item in inst.teams)
                    {
                        if (GUILayout.Button("    Team: " + item.Key.ToString() + " - " + item.Value.teamName))
                        {
                            if (enabledTabs.Contains(item.Key))
                                enabledTabs.Remove(item.Key);
                            else
                                enabledTabs.Add(item.Key);
                        }
                        if (enabledTabs.Contains(item.Key))
                        {
                            GUILayout.BeginHorizontal();
                            GUILayout.Label("Default Relations: ");
                            GUILayout.Label(item.Value.relations.ToString());
                            GUILayout.FlexibleSpace();
                            GUILayout.EndHorizontal();
                            GUILayout.BeginHorizontal();
                            GUILayout.Label("To Player: ");
                            GUILayout.Label(item.Value.Alignment(playerTeam).ToString());
                            GUILayout.FlexibleSpace();
                            GUILayout.EndHorizontal();
                            foreach (var item2 in item.Value.align)
                            {
                                GUILayout.BeginHorizontal();
                                GUILayout.Label("Team: ");
                                GUILayout.Label(item2.Key.ToString());
                                GUILayout.Label(" | ");
                                GUILayout.Label(item2.Value.ToString());
                                GUILayout.FlexibleSpace();
                                GUILayout.EndHorizontal();
                            }
                        }
                    }
                }
            }
        }



        private static bool NetReady = false;
        private static Dictionary<int, byte> ToSend = null;
        public static void CheckNeedNetworkHooks(Mode mode)
        {
            if (mode.IsMultiplayer && ManNetwork.IsHostOrWillBe)
            {
                if (!NetReady)
                {
                    NetReady = true;
                    ToSend = new Dictionary<int, byte>();
                    BuildBucksUpdatedEvent.Subscribe(OnNetTeamBBChange);
                    TeamAlignmentDeltaEvent.Subscribe(OnNetTeamAlignChange);
                    TeamRemovedEvent.Subscribe(OnNetTeamDestroyed);
                    InvokeHelper.InvokeSingleRepeat(PushTeamDeltasToClients, 1);
                }
            }
            else
            {
                if (NetReady)
                {
                    NetReady = false;
                    InvokeHelper.CancelInvokeSingleRepeat(PushTeamDeltasToClients);
                    TeamRemovedEvent.Unsubscribe(OnNetTeamDestroyed);
                    TeamAlignmentDeltaEvent.Unsubscribe(OnNetTeamAlignChange);
                    BuildBucksUpdatedEvent.Unsubscribe(OnNetTeamBBChange);
                    ToSend = null;
                }
            }
        }
        public static void OnNetTeamBBChange(int team, int bbSet)
        {
            if (ToSend.TryGetValue(team, out byte val))
                return;
            ToSend[team] = 0;
        }
        public static void OnNetTeamAlignChange(int team)
        {
            if (ToSend.TryGetValue(team, out byte val) && val > 0)
                return;
            ToSend[team] = 1;
        }
        public static void OnNetTeamDestroyed(int team)
        {
            ToSend[team] = 2;
        }

        public static void PushTeamDeltasToClients()
        {
            if (ToSend.Any())
            {
                netHook.TryBroadcast(new NetworkedAITeamUpdate());
            }
        }

        public class NetworkedAITeamUpdate : MessageBase
        {
            public NetworkedAITeamUpdate() { }

            public override void Serialize(NetworkWriter write)
            {
                try
                {
                    int count = ToSend.Count;
                    if (count >= byte.MaxValue)
                    {
                        write.Write((byte)count);
                        foreach (var item in ToSend)
                        {
                            if (inst.teams.TryGetValue(item.Key, out EnemyTeamData ETD))
                            {
                                switch (item.Value)
                                {
                                    case 0:
                                        PackTeamBBInfo(ref write, ETD);
                                        break;
                                    case 1:
                                        PackTeamInfo(ref write, ETD);
                                        break;
                                    case 2:
                                        PackTeamRemovedInfo(ref write, item.Key);
                                        break;
                                    default:
                                        break;
                                }
                            }
                            else
                            {
                                PackTeamRemovedInfo(ref write, item.Key);
                            }
                        }
                    }
                else
                    {
                        DebugTAC_AI.Assert("Team netupdates EXCEEDED " + byte.MaxValue + "!!!");
                        count = byte.MaxValue;
                        write.Write(byte.MaxValue);
                        foreach (var item in ToSend)
                        {
                            if (inst.teams.TryGetValue(item.Key, out EnemyTeamData ETD))
                            {
                                switch (item.Value)
                                {
                                    case 0:
                                        PackTeamBBInfo(ref write, ETD);
                                        break;
                                    case 1:
                                        PackTeamInfo(ref write, ETD);
                                        break;
                                    case 2:
                                        PackTeamRemovedInfo(ref write, item.Key);
                                        break;
                                    default:
                                        break;
                                }
                            }
                            else
                            {
                                PackTeamRemovedInfo(ref write, item.Key);
                            }
                            count--;
                            if (count == 0)
                                break;
                        }
                    }
                } 
                finally
                {
                    ToSend.Clear();
                }
            }
            public override void Deserialize(NetworkReader read)
            {
                int count = read.ReadByte();
                for (int step = 0; step < count; step++)
                {
                    UnpackTeamInfo(ref read);
                }
            }


            private void PackTeamInfo(ref NetworkWriter write, EnemyTeamData ETD)
            {
                write.WritePackedInt32(ETD.teamID);
                write.WritePackedInt32(ETD.BuildBucks);
                PackDictInfo(ref write, ETD.align);
            }
            private void PackTeamBBInfo(ref NetworkWriter write, EnemyTeamData ETD)
            {
                write.WritePackedInt32(0);
                write.WritePackedInt32(ETD.BuildBucks);
                write.WritePackedInt32(ETD.teamID);
            }
            private void PackTeamRemovedInfo(ref NetworkWriter write, int team)
            {
                write.WritePackedInt32(int.MinValue);
                write.WritePackedInt32(team);
            }
            private void UnpackTeamInfo(ref NetworkReader read)
            {
                int team = read.ReadPackedInt32();
                int BB = read.ReadPackedInt32();
                if (BB == int.MinValue)
                {   // REMOVE
                    OnTeamDestroyedRemoteClient(team);
                }
                else
                {   // Add/Update
                    var teamInst = InsureBaseTeam(team);
                    teamInst.SetBuildBucks = BB;
                    UnpackDictInfo(ref read, ref teamInst.align);
                }
            }
            private void PackDictInfo(ref NetworkWriter write, Dictionary<int,TeamRelations> input)
            {
                write.WritePackedInt32(input.Count);
                foreach (var item in input)
                {
                    write.WritePackedInt32(item.Key);
                    write.WritePackedInt32((int)item.Value);
                }
            }
            private void UnpackDictInfo(ref NetworkReader read, ref Dictionary<int, TeamRelations> input)
            {
                int unpack = read.ReadPackedInt32();
                for (int step = 0; step < unpack; step++)
                {
                    TeamRelations val = (TeamRelations)read.ReadPackedInt32();
                    input[read.ReadPackedInt32()] = val;
                }
            }


            [EnumFlag]
            public uint PackingInfo;
            public int TeamID;
            public int BribeAmount;
        }
        private static NetworkHook<NetworkedAITeamUpdate> netHook = new NetworkHook<NetworkedAITeamUpdate>(OnReceiveTeamUpdate, NetMessageType.ToClientsOnly);

        internal static void InsureNetHooks()
        {
            netHook.Register();
        }
        internal static bool OnReceiveTeamUpdate(NetworkedAITeamUpdate update, bool isServer)
        {
            return true;
        }



        // ------------------------------------
        //               LEGACY
        // ------------------------------------

        public static bool IsLegacyBaseTeam(int team)
        {
            return (team >= BaseTeamsStart && team <= BaseTeamsEnd) || team == SpecialAISpawner.trollTeam;
        }
        public static NP_Types GetNPTTeamType(int team)
        {
            if (team == ManPlayer.inst.PlayerTeam)
                return NP_Types.Player;
            else if (IsLegacyBaseTeam(team))
            {
                if (IsLegacyEnemyBaseTeam(team))
                    return NP_Types.Enemy;
                else if (IsLegacyNeutralBaseTeam(team))
                    return NP_Types.Neutral;
                else if (IsLegacyNonAggressiveTeam(team))
                    return NP_Types.NonAggressive;
                else if (IsLegacySubNeutralBaseTeam(team))
                    return NP_Types.SubNeutral;
                else
                    return NP_Types.Friendly;
            }
            else
                return NP_Types.NonNPT;
        }

        public const int EnemyBaseTeamsStart = 256;
        public const int EnemyBaseTeamsEnd = 356;

        public const int SubNeutralBaseTeamsStart = 357;
        public const int SubNeutralBaseTeamsEnd = 406;

        public const int NeutralBaseTeamsStart = 407;
        public const int NeutralBaseTeamsEnd = 456;

        public const int FriendlyBaseTeamsStart = 457;
        public const int FriendlyBaseTeamsEnd = 506;

        public const int BaseTeamsStart = 256;
        public const int BaseTeamsEnd = 506;

        public static bool IsLegacyEnemyBaseTeam(int team)
        {
            return (team >= EnemyBaseTeamsStart && team <= EnemyBaseTeamsEnd) || team == SpecialAISpawner.trollTeam;
        }
        public static bool IsLegacyNonAggressiveTeam(int team)
        {
            return team >= SubNeutralBaseTeamsStart && team <= NeutralBaseTeamsEnd;
        }
        public static bool IsLegacySubNeutralBaseTeam(int team)
        {
            return team >= SubNeutralBaseTeamsStart && team <= SubNeutralBaseTeamsEnd;
        }
        public static bool IsLegacyNeutralBaseTeam(int team)
        {
            return team >= NeutralBaseTeamsStart && team <= NeutralBaseTeamsEnd;
        }
        public static bool IsLegacyFriendlyBaseTeam(int team)
        {
            return team >= FriendlyBaseTeamsStart && team <= FriendlyBaseTeamsEnd;
        }


    }
}
