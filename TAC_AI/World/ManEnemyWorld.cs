using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using TerraTechETCUtil;
using TAC_AI.Templates;
using TAC_AI.AI.Enemy;
using TAC_AI.AI;
using UnityEngine.UI;

namespace TAC_AI.World
{
    // Manages Enemy bases that are off-screen
    // Za wardo
    //  Enemy bases only attack if:
    //    PLAYER BASES (Only when player base is ON SCENE):
    //      An enemy team's base is close to the player's BASE position
    //      An enemy scout follows the player home to their base and shoots at it
    //      the player attacks the enemy and the enemy base is ON SCENE
    //    ENEMY BASES
    //      An enemy scout has found another enemy base
    // 
    //    Much like their active counterparts in AIECore.TankAIHelper,
    //      EnemyPresense has both a:
    //        Operator (Large Actions)
    //        Maintainer (Small Actions)
    // 
    //
    public class ManEnemyWorld : MonoBehaviour
    {
        //-------------------------------------
        //              CONSTANTS
        //-------------------------------------
        // There are roughly around 6 chunks per node
        //  ETU = EnemyTechUnit = Unloaded, mobile enemy Tech
        //  EBU = EnemyBaseUnloaded = Unloaded, stationary enemy Base
        internal const int OperatorTickDelay = 4;             // How many seconds the AI will perform base actions - default 4
        internal const int OperatorTicksKeepTarget = 4;             // How many seconds the AI will perform base actions - default 4
        internal const int UnitSightRadius = 2;         // How far an enemy Tech Unit can see other enemies. IN TILES
        internal const int BaseSightRadius = 4;         // How far an enemy Base Unit can see other enemies. IN TILES
        internal const int EnemyBaseCullingExtents = 8; // How far from the player should enemy bases be removed 
        // from the world? IN TILES
        internal static int EnemyRaidProvokeExtents = 4;// How far the can the enemy bases issue raids on the player. IN TILES

        // Movement
        internal const float MaintainerTickDelay = 0.5f;         // How many seconds the AI will perform a move - default 2
        internal static float TechTraverseMulti = 0.75f;// Multiplier for AI traverse speed over ALL terrain

        // Harvesting
        internal const float SurfaceHarvestingMulti = 5.5f; // The multiplier of unloaded
        internal const int ExpectedDPSDelitime = 1;    // How long we expect an ETU to be hitting an unloaded target for in seconds

        // Gains - (Per second)
        internal const int PassiveHQBonusIncome = 150;
        internal const int ExpansionIncome = 75;

        // Health-Based (Volume-Based)
        //bases
        internal const float BaseHealthMulti = 0.1f;    // Health multiplier for out-of-play combat
        internal const float BaseAccuraccy = 75f;       // Damage multiplier vs evasion
        internal const float BaseEvasion = 25f;        // Damage reducer
        //units
        internal const float MobileHealthMulti = 0.05f;  // Health multiplier for out-of-play combat
        internal const float MobileAccuraccy = 50f;       // Damage multiplier vs evasion
        internal const float MobileSpeedAccuraccyReduction = 0.25f;  // Damage multiplier vs evasion
        internal const float MobileSpeedToEvasion = 1f; // Damage reducer
        internal const WorldTile.LoadStep LevelToAttemptTechEntry = WorldTile.LoadStep.Loaded;

        // Repair
        internal const int HealthRepairCost = 60;       // How much BB the AI should spend to repair unloaded damage
        internal const int HealthRepairRate = 15;       // How much the enemy should repair every turn
        internal const float BatteryToHealthConversionRate = 0.5f; // Battery to health effectiveness
        internal const float RadiusBonus = 5;       // How much the enemy should repair every turn
        internal const float sphereForm = (4 / 3) * Mathf.PI * RadiusBonus;       // How much the enemy should repair every turn
        internal static int GetShieldRadiusHealthCoverage(float ShieldRadius)
        { // How much health a shield radius would account for
            return Mathf.CeilToInt(sphereForm * Mathf.Pow(ShieldRadius, 3));
        }


        // Corp Speeds For Each Corp when Unloaded
        internal static readonly Dictionary<FactionTypesExt, float> corpSpeeds = new Dictionary<FactionTypesExt, float>() {
            {
                FactionTypesExt.GSO , 60
            },
            {
                FactionTypesExt.GC , 40
            },
            {
                FactionTypesExt.VEN , 100
            },
            {
                FactionTypesExt.HE , 50
            },
            {
                FactionTypesExt.BF , 75
            },
            { FactionTypesExt.EXP, 45 },

            // MODDED UNOFFICIAL
            { FactionTypesExt.GT, 65 },
            { FactionTypesExt.TAC, 70 },
            { FactionTypesExt.OS, 45 },
        };

        //-------------------------------------
        //           LIVE VARIABLES
        //-------------------------------------
        public static ManEnemyWorld inst;
        public static bool enabledThis = false;
        private static bool subToTiles = false;

        /// <summary>
        /// (old TeamID, new TeamID) Sends when a enemy base team has "declared war" on the player
        /// </summary>
        public static Event<int, int> TeamWarEvent = new Event<int, int>();
        /// <summary>
        /// (old TeamID, new TeamID) Sends when a enemy base team has "made peace" on the player
        /// </summary>
        public static Event<int, int> TeamBribeEvent = new Event<int, int>();
        /// <summary>
        /// (Team, Tech Visible ID, Was loaded) Sends when an enemy Tech (BASE TEAM ONLY) is destroyed out-of-play and or in play
        /// </summary>
        public static Event<int, int, bool> TechDestroyedEvent = new Event<int, int, bool>();
        /// <summary>
        /// (TeamID) Sends when a new enemy base team is created out-of-play
        /// </summary>
        public static Event<int> TeamCreatedEvent = new Event<int>();
        /// <summary>
        /// (TeamID) Sends when a new enemy base team is destroyed out-of-play
        /// </summary>
        public static Event<int> TeamDestroyedEvent = new Event<int>();
        public static string CombatLog => GetCombatLog();

        private static StringBuilder combatLogger = new StringBuilder();
        private static Queue<string> logEntries = new Queue<string>();

        public static void AddToCombatLog(string str)
        {
            logEntries.Enqueue(str);
            if (logEntries.Count > 10)
                logEntries.Dequeue();
        }
        public static string GetCombatLog()
        {
            combatLogger.Clear();
            for (int step = logEntries.Count - 1; step > -1; step--)
            {
                combatLogger.AppendLine(logEntries.ElementAt(step));
            }
            return combatLogger.ToString();
        }

        internal static readonly FieldInfo ProdDelay = typeof(ModuleItemProducer).GetField("m_SecPerItemProduced", BindingFlags.NonPublic | BindingFlags.Instance);

        internal static readonly FieldInfo PowCond = typeof(ModuleEnergy).GetField("m_OutputConditions", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static readonly FieldInfo PowDelay = typeof(ModuleEnergy).GetField("m_OutputPerSecond", BindingFlags.NonPublic | BindingFlags.Instance);

        private static float OperatorTicker = 0;
        private static float MaintainerTicker = 0;
        private static readonly Dictionary<int, NP_Presence> NPTTeams = new Dictionary<int, NP_Presence>();
        internal static readonly Dictionary<NP_TechUnit, TileMoveCommand> QueuedUnitMoves = new Dictionary<NP_TechUnit, TileMoveCommand>();
        private static List<IntVector2> AllSavedWorldTileCoords
        {
            get {
                allSavedWorldTileCoordsCache.Clear();
                allSavedWorldTileCoordsCache.AddRange(ManSaveGame.inst.CurrentState.m_StoredTiles.Keys);
                allSavedWorldTileCoordsCache.AddRange(ManSaveGame.inst.CurrentState.m_StoredTilesJSON.Keys);
                return allSavedWorldTileCoordsCache;
            }
        }
        private static List<IntVector2> allSavedWorldTileCoordsCache = new List<IntVector2>();

        public static Dictionary<int, NP_Presence> AllTeamsUnloaded {
            get
            {
                return new Dictionary<int, NP_Presence>(NPTTeams);
            }
        }


        private static bool setup = false;
        public static void Initiate()
        {
            if (inst)
                return;
            inst = new GameObject("ManEnemyWorld").AddComponent<ManEnemyWorld>();
            DebugTAC_AI.Log("TACtical_AI: Created ManEnemyWorld.");
#if STEAM
            LateInitiate();
#endif
        }
        public static void DeInit()
        {
            if (!inst)
                return;
            Singleton.Manager<ManTechs>.inst.TankDestroyedEvent.Unsubscribe(OnTechDestroyed);
            Singleton.Manager<ManGameMode>.inst.ModeStartEvent.Unsubscribe(OnWorldLoad);
            Singleton.Manager<ManGameMode>.inst.ModeSwitchEvent.Unsubscribe(OnWorldReset);
            ManPlayerRTS.DeInit();
            Destroy(inst.gameObject);
            inst = null;
            setup = false;
            logEntries.Clear();
            DebugTAC_AI.Log("TACtical_AI: Removed ManEnemyWorld.");
        }

        public static void LateInitiate()
        {
            if (setup)
                return;
            DebugTAC_AI.Log("TACtical_AI: Late Init ManEnemyWorld.");
            Singleton.Manager<ManTechs>.inst.TankDestroyedEvent.Subscribe(OnTechDestroyed);
            Singleton.Manager<ManGameMode>.inst.ModeStartEvent.Subscribe(OnWorldLoad);
            Singleton.Manager<ManGameMode>.inst.ModeSwitchEvent.Subscribe(OnWorldReset);
            ManPlayerRTS.Initiate();
            ManEnemySiege.Init();
            setup = true;
        }

        public static void OnWorldLoad(Mode mode)
        {
            NPTTeams.Clear();
            QueuedUnitMoves.Clear();
            ManEnemySiege.EndSiege(true);
            ManEnemySiege.ResetSiegeTimer(true);
            if (!(mode is ModeMain || mode is ModeMisc || mode is ModeCoOpCreative || mode is ModeCoOpCampaign))
            {
                enabledThis = false;
                return;
            }
            if (!subToTiles)
            {
                Singleton.Manager<ManWorld>.inst.TileManager.TileStartPopulatingEvent.Subscribe(OnTileTechsBeforeRespawn);
                Singleton.Manager<ManWorld>.inst.TileManager.TilePopulatedEvent.Subscribe(OnTileTechsRespawned);
                Singleton.Manager<ManWorld>.inst.TileManager.TileDepopulatedEvent.Subscribe(OnTileTechsDespawned);
                subToTiles = true;
            }
            enabledThis = true;
            OperatorTicker = 0;
            inst.Invoke("OnWorldLoadEnd", 5);
        }
        public void OnWorldLoadEnd()
        {
            int count = 0;
            if (ManSaveGame.inst.CurrentState != null)
                ManSaveGame.inst.CurrentState.m_FileHasBeenTamperedWith = true;
            DebugRawTechSpawner.DestroyAllInvalidVisibles();
            try
            {
                HashSet<int> loaded = new HashSet<int>();
                foreach (var item in ManTechs.inst.IterateTechs())
                {
                    loaded.Add(item.visible.ID);
                }
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
                                RegisterTechUnloaded(tech);
                                count++;
                            }
                        }
                    }
                }
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
                                RegisterTechUnloaded(tech);
                                count++;
                            }
                        }
                    }

                }
                DebugTAC_AI.Log("TACtical_AI: OnWorldLoadEnd Handled " + count + " Techs");
            }
            catch (Exception e)
            {
                DebugTAC_AI.Log("TACtical_AI: OnWorldLoadEnd FAILED at " + count + " Techs - " + e);
            }
        }
        public static void OnWorldReset()
        {
            /*
            EnemyTeams.Clear();
            QueuedUnitMoves.Clear();
            */
        }

        public static void OnTileTechsBeforeRespawn(WorldTile WT)
        {
            if (!enabledThis)
                return;
            foreach (NP_TechUnit ETU in GetUnloadedTechsInTile(WT.Coord))
            {
                ETU.ApplyDamage();
            }
        }
        public static void OnTileTechsRespawned(WorldTile WT)
        {
            if (!enabledThis)
                return;
            foreach (NP_TechUnit ETU in GetUnloadedTechsInTile(WT.Coord))
            {
                RemoveTechFromTeam(ETU); // Cannot manage loaded techs
                if (ETU is NP_BaseUnit EBU)
                {
                    EBU.TryPushMoneyToLoadedInstance();
                }
                if (ETU.ShouldApplyShields())
                    ETU.DoApplyShields(ETU.GetActiveTech());
            }
        }
        public static void OnTileTechsDespawned(WorldTile WT)
        {
            if (!enabledThis)
                return;
            ManSaveGame.StoredTile tileInst = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(WT.Coord, false);
            if (tileInst == null)
                return;
            if (tileInst.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> techs))
            {
                foreach (ManSaveGame.StoredVisible Vis in techs)
                {
                    if (Vis is ManSaveGame.StoredTech tech)
                    {
                        if (tech.m_TechData.IsBase() || GetTeam(tech.m_TeamID) != null)
                            RegisterTechUnloaded(tech);
                    }
                }
            }
        }
        public static void OnTechDestroyed(Tank tech, ManDamage.DamageInfo poof)
        {
            if (!enabledThis)
                return;
            if (!TryGetETUFromTank(tech, out NP_TechUnit ETU))
                return;
            if (poof.Damage != 0)
            {
                TechDestroyedEvent.Send(tech.Team, tech.visible.ID, true);
                UnloadedBases.RemoteRemove(ETU);
            }
        }




        public static bool TryRefindTech(IntVector2 prev, NP_TechUnit tech, out IntVector2 found)
        {
            ManSaveGame.StoredTech techFind = tech.tech;
            found = prev;
            try
            {
                List<IntVector2> tiles = AllSavedWorldTileCoords;
                foreach (IntVector2 tile in tiles)
                {
                    ManSaveGame.StoredTile tileInst = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(tile, false);
                    if (tileInst == null)
                        continue;
                    if (tileInst.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> techs))
                    {
                        if (techs.Exists(x => x.m_ID == tech.ID))
                        {
                            found = tile;
                            return true;
                        }
                    }
                }
                //DebugTAC_AI.Log("TACtical_AI: TryRefindTech - COULD NOT REFIND TECH!!!  Of name " + techFind.m_TechData.Name);
            }
            catch (Exception e)
            {
                DebugTAC_AI.Log("TACtical_AI: TryRefindTech - COULD NOT REFIND TECH!!! " + e);
            }
            return false;
        }
        public static bool IsTechOnSetTile(NP_TechUnit tech)
        {
            ManSaveGame.StoredTech techFind = tech.tech;
            try
            {
                ManSaveGame.StoredTile tileInst = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(tech.tilePos, false);
                if (tileInst == null)
                {
                    DebugTAC_AI.Log("TACtical_AI: IsTechOnSetTile - m_StoredVisibles is missing!");
                    return false;
                }
                if (tileInst.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> techs))
                {
                    if (techs.Exists(x => x.m_ID == tech.ID))
                    {
                        return true;
                    }
                    else
                    {
                        /*
                        DebugTAC_AI.Log("TACtical_AI: IsTechOnSetTile - Tech not present in techs " + techs.Count + "!");
                        DebugTAC_AI.Log("TACtical_AI: Main - " + tech.ID);
                        foreach (var item in techs)
                        {
                            DebugTAC_AI.Log("TACtical_AI: - " + item.m_ID);
                        }*/
                    }
                }
                else
                    DebugTAC_AI.Log("TACtical_AI: IsTechOnSetTile - StoredTile is NULL");
            }
            catch (Exception e)
            {
                DebugTAC_AI.Log("TACtical_AI: IsTechOnSetTile - COULD NOT FIND TECH!!! " + e);
            }
            return false;
        }


        // WORLD Loading
        /// <summary>
        /// Does not support teams not within the BaseTeams range declared in RawTechLoader.
        /// To force-support, set forceRegister to true.
        /// </summary>
        /// <param name="tech"></param>
        /// <param name="tilePos"></param>
        /// <param name="isNew"></param>
        public static void RegisterTechUnloaded(ManSaveGame.StoredTech tech, bool isNew = true, bool forceRegister = false)
        {
            var TV = ManVisible.inst.GetTrackedVisible(tech.m_ID);
            if (TV == null)
            {
                DebugTAC_AI.Log("TACtical_AI: Tech unit " + tech.m_TechData.Name + " lacked TrackedVisible, fixing...");
                TV = RawTechLoader.TrackTank(tech, tech.m_ID);
            }
            int team = TV.RadarTeamID;
            //if (TV.TeamID != tech.m_TeamID)
            //    throw new Exception("NP_BaseUnit and TrackedVisible TeamID Mismatch " + TV.TeamID + " vs " + tech.m_TeamID);
            //if (TV.TeamID != TV.RadarTeamID)
            //    throw new Exception("NP_BaseUnit and TrackedVisible RadarTeamID Mismatch " + TV.TeamID + " vs " + TV.RadarTeamID);
            if (AIGlobals.IsBaseTeam(team) || forceRegister)
            {   // Enemy Team
                if (tech.m_TechData.IsBase())
                {
                    try
                    {
                        NP_BaseUnit EBU = new NP_BaseUnit(tech, InsureTeam(team));
                        EBU.SetTracker(TV);
                        if (TV.ID != tech.m_ID)
                            throw new Exception("NP_BaseUnit and TrackedVisible ID Mismatch");
                        if (!IsTechOnSetTile(EBU))
                            throw new Exception("NP_BaseUnit is not on given tile");
                        AddToTeam(EBU, isNew);
                    }
                    catch (Exception e)
                    {
                        DebugTAC_AI.Log("TACtical_AI: HandleTechUnloaded(EBU) Failiure on BASE init! - " + e);
                    }
                }
                else
                {
                    try
                    {
                        NP_TechUnit ETU = new NP_TechUnit(tech, InsureTeam(team), tech.m_TechData.GetMainCorpExt());
                        ETU.SetTracker(TV);
                        if (TV.ID != tech.m_ID)
                            throw new Exception("NP_TechUnit and TrackedVisible ID Mismatch");
                        if (!IsTechOnSetTile(ETU))
                            throw new Exception("NP_TechUnit is not on given tile");
                        AddToTeam(ETU, isNew);
                    }
                    catch (Exception e)
                    {
                        DebugTAC_AI.Log("TACtical_AI: HandleTechUnloaded(ETU) Failiure on BASE init! - " + e);
                    }
                }
            }
            //else
            //    DebugTAC_AI.Log("TACtical_AI: HandleTechUnloaded() Failed because tech " + tech.m_TechData.Name + "'s team [" + team + "] is not a valid base team");
        }


        public static ManSaveGame.StoredTile GetTile(NP_TechUnit tech)
        {
            ManSaveGame.StoredTile Tile = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(tech.tilePos);
            if (Tile != null)
            {
                if (Tile.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> techs))
                {
                    if (techs.Exists(x => x.m_ID == tech.ID))
                        return Tile;
                }
            }
            return null;
        }
        public static bool CanMoveUnloadedTechIntoTile(IntVector2 coord)
        {
            var tile = ManWorld.inst.TileManager.LookupTile(coord);
            if (tile != null)
            {
                if (tile.m_LoadStep >= LevelToAttemptTechEntry && AIGlobals.AtSceneTechMax())
                {
                    DebugTAC_AI.Info("TACtical_AI: CanMoveUnloadedTechIntoTile(Loaded) - The scene is at the Tech max limit.  Cannot proceed.");
                    return false;
                }
            }
            return true;
        }
        public static bool CanMoveUnloadedTechIntoInactiveTile(IntVector2 coord)
        {
            var tile = ManWorld.inst.TileManager.LookupTile(coord);
            if (tile != null)
            {
                if (tile.m_LoadStep < LevelToAttemptTechEntry)
                    return true;
                DebugTAC_AI.Info("TACtical_AI: CanMoveUnloadedTechIntoInactiveTile - Tile is at load state " 
                    + tile.m_LoadStep + ", which is above allowance for unloaded Techs at " + LevelToAttemptTechEntry + ".");
                return false;
            }
            return true;
        }
        public static bool CanMoveUnloadedTechIntoActiveTile(WorldTile tile)
        {
            if (tile == null)
                return false;
            if (AIGlobals.AtSceneTechMax())
            {
                DebugTAC_AI.Info("TACtical_AI: CanMoveUnloadedTechIntoActiveTile - The scene is at the Tech max limit.  Cannot proceed.");
                return false;
            }
            /*
            int range = AIGlobals.EnemyExtendActionRange - AIGlobals.TileFringeDist;
            range *= range;
            Vector3 tilePosScene = ManWorld.inst.TileManager.CalcTileCentreScene(tile.Coord);
            */
            return tile.m_LoadStep >= LevelToAttemptTechEntry; //&& range > (tilePosScene - Singleton.playerPos).sqrMagnitude;
        }
        private static List<int> BlockTypeCache = new List<int>();
        public static bool TryMoveTechIntoTile(NP_TechUnit tech, ManSaveGame.StoredTile tileToMoveInto, bool setPrecise = true)
        {
            if (tileToMoveInto != null)
            {
                Vector3 tilePosScene = ManWorld.inst.TileManager.CalcTileCentreScene(tileToMoveInto.coord);
                var tile = ManWorld.inst.TileManager.LookupTile(tilePosScene);
                if (CanMoveUnloadedTechIntoActiveTile(tile))
                {   // Loading in an active Tech
                    if (!FindFreeSpaceOnActiveTile(tilePosScene - tech.PosScene, tileToMoveInto.coord, out Vector3 newPosOff))
                    {
                        DebugTAC_AI.Log("TACtical_AI: MoveTechIntoTile(loaded) - Could not find a valid spot to move the Tech");
                        return false;
                    }
                    Vector3 newPos = newPosOff.SetY(tilePosScene.y);
                    if (ManWorld.inst.GetTerrainHeight(newPos, out float Height))
                    {
                        newPos.y = Height + 64;
                    }
                    else
                    {
                        DebugTAC_AI.Log("TACtical_AI: MoveTechIntoTile(loaded) - The tile exists but the terrain doesn't?!? ScenePos " + newPos);
                        return false;
                    }
                    tech.ApplyDamage();
                    bool shouldApplyShields = tech.ShouldApplyShields();
                    ManSpawn.TankSpawnParams tankSpawn = new ManSpawn.TankSpawnParams
                    {
                        techData = tech.tech.m_TechData,
                        blockIDs = null,
                        teamID = tech.tech.m_TeamID,
                        position = newPos,
                        rotation = Quaternion.LookRotation(Singleton.cameraTrans.position - tech.tech.GetBackwardsCompatiblePosition(), Vector3.up),
                        ignoreSceneryOnSpawnProjection = false,
                        forceSpawn = true,
                        isPopulation = false,
                        grounded = tech.tech.m_Grounded
                    };
                    Tank newTech = Singleton.Manager<ManSpawn>.inst.SpawnTank(tankSpawn, true);
                    if (newTech != null)
                    {
                        if (shouldApplyShields)
                            tech.DoApplyShields(newTech);
                        RemoveTechFromTile(tech);
                        RemoveTechFromTeam(tech);
                        DebugTAC_AI.Log("TACtical_AI: MoveTechIntoTile - Tech " + tech.Name + " has moved to in-play world coordinate " + newPos + "!");
                        return true;
                    }
                    else
                        DebugTAC_AI.Log("TACtical_AI: MoveTechIntoTile - Failiure on spawning Tech!");

                }
                else
                {   // Loading in an Inactive Tech
                    BlockTypeCache.Clear();
                    ManSaveGame.StoredTech ST = tech.tech;
                    foreach (TankPreset.BlockSpec mem in ST.m_TechData.m_BlockSpecs)
                    {
                        if (!BlockTypeCache.Contains((int)mem.m_BlockType))
                        {
                            BlockTypeCache.Add((int)mem.m_BlockType);
                        }
                    }
                    if (!FindFreeSpaceOnTile(tech.tilePos - tileToMoveInto.coord, tileToMoveInto, out Vector2 newPosOff))
                    {
                        DebugTAC_AI.Log("TACtical_AI: MoveTechIntoTile - Could not find a valid spot to move the Tech");
                        return false;
                    }
                    RemoveTechFromTile(tech);
                    //RemoveTechFromTeam(tech);
                    Vector3 newPos = newPosOff.ToVector3XZ() + ManWorld.inst.TileManager.CalcTileOriginScene(tileToMoveInto.coord);
                    Quaternion fromDirect = Quaternion.LookRotation(newPos - ST.m_Position);
                    if (setPrecise)
                    {
                        //Vector3 inTilePos = newPosOff.ToVector3XZ();
                        ManWorld.inst.GetTerrainHeight(newPos, out newPos.y);
                        //techInst.m_WorldPosition = new WorldPosition(tileToMoveInto.coord, inTilePos); // Accurate it!
                    }
                    if (AddTechToTileAndSetETU(tech, tileToMoveInto, ST.m_TechData, BlockTypeCache.ToArray(), ST.m_TeamID, newPos, fromDirect, false))
                    {
                        //RegisterTechUnloaded(techInst, tileToMoveInto.coord, false);
                        //DebugTAC_AI.Log("TACtical_AI: MoveTechIntoTile - Moved a Tech");
                        if (tech.tilePos == tileToMoveInto.coord)
                        {
                            tech.UpdateTVLocation();
                            //if (tech.Exists())
                            //    DebugTAC_AI.Assert("TACtical_AI: MoveTechIntoTile - tech was moved but not part of team!!!");
                        }
                        else
                            DebugTAC_AI.Log("TACtical_AI: MoveTechIntoTile - tile coord mismatch!");
                    }
                    else
                    {
                        DebugTAC_AI.Log("TACtical_AI: MoveTechIntoTile - Tech was created but WE HAVE MISPLACED IT!  The Tech may or may not be gone forever");
                        DebugTAC_AI.FatalError();
                    }
                }
                return true;
            }
            return false;
        }

        private static List<Vector2> possibleSpotsCache = new List<Vector2>();
        /// <summary>
        /// Uses headingDirection to determine the corner where to start placing the Techs flat on the tile
        /// </summary>
        /// <param name="headingDirection"></param>
        /// <param name="tile"></param>
        /// <param name="finalPos"></param>
        /// <returns></returns>
        public static bool FindFreeSpaceOnTile(Vector2 headingDirection, ManSaveGame.StoredTile tile, out Vector2 finalPos)
        {
            finalPos = Vector3.zero;
            //List<EnemyTechUnit> ETUs = GetTechsInTile(tile.coord);
            int partitions = (int)ManWorld.inst.TileSize / 64;
            float partitionScale = ManWorld.inst.TileSize / partitions;
            float partDist = ManWorld.inst.TileSize - partitionScale;
            possibleSpotsCache.Clear();
            ManSaveGame.StoredTile tileCache = tile;
            if (tileCache == null)
            {
                DebugTAC_AI.Log("TACtical_AI: FindFreeSpaceOnTile - Attempt to find free space failed: Tile is null");
                return false;
            }
            for (int stepX = 0; stepX < partDist; stepX += (int)partitionScale)
            {
                for (int stepY = 0; stepY < partDist; stepY += (int)partitionScale)
                {
                    Vector2 New = new Vector2(stepX, stepY);
                    if (GetTechsInTileCached(ref tileCache, tile.coord, New, partitionScale - 2).Count() == 0)
                        possibleSpotsCache.Add(New);
                    //else
                    //    DebugTAC_AI.Log("TACtical_AI: FindFreeSpaceOnTile - Attempt to find free space failed on tile coord " + tile.coord + ", " + New);
                }
            }
            if (!possibleSpotsCache.Any())
                return false;

            if (possibleSpotsCache.Count == 1)
            {
                finalPos = possibleSpotsCache.First();
                return true;
            }

            Vector2 Directed = -(headingDirection.normalized * ManWorld.inst.TileSize) + (Vector2.one * (partDist / 2));
            finalPos = possibleSpotsCache.OrderBy(x => (x - Directed).sqrMagnitude).First();
            return true;
        }
        /// <summary>
        /// Builds around the TechBuilder
        /// </summary>
        /// <param name="headingDirection"></param>
        /// <param name="tile"></param>
        /// <param name="finalPosOffsetOrigin"></param>
        /// <returns></returns>
        public static bool FindFreeSpaceOnTileCircle(NP_BaseUnit TechBuilder, ManSaveGame.StoredTile tile, out Vector2 finalPosOffsetOrigin)
        {
            Vector2 PosInTile = WorldPosition.FromScenePosition(TechBuilder.tech.GetBackwardsCompatiblePosition()).TileRelativePos;
            finalPosOffsetOrigin = Vector3.zero;
            //List<EnemyTechUnit> ETUs = GetTechsInTile(tile.coord);
            int partitions = (int)ManWorld.inst.TileSize / 64;
            float partitionScale = ManWorld.inst.TileSize / partitions;
            float partDist = ManWorld.inst.TileSize - partitionScale;
            possibleSpotsCache.Clear();
            ManSaveGame.StoredTile tileCache = tile;
            if (tileCache == null)
            {
                DebugTAC_AI.Log("TACtical_AI: FindFreeSpaceOnTileCircle - Attempt to find free space failed: Tile is null");
                return false;
            }
            for (int stepX = 0; stepX < partDist; stepX += (int)partitionScale)
            {
                for (int stepY = 0; stepY < partDist; stepY += (int)partitionScale)
                {
                    Vector2 New = new Vector2(stepX, stepY);
                    if (!IsTechInTile(ref tileCache, tile.coord, New, partitionScale - 2))
                        possibleSpotsCache.Add(New);
                    //else
                    //    DebugTAC_AI.Log("TACtical_AI: FindFreeSpaceOnTileCircle - Attempt to find free space failed on tile coord " + tile.coord + ", " + New);
                }
            }
            if (!possibleSpotsCache.Any())
                return false;

            if (possibleSpotsCache.Count == 1)
            {
                finalPosOffsetOrigin = possibleSpotsCache.First();
                return true;
            }

            finalPosOffsetOrigin = possibleSpotsCache.OrderBy(x => (x - PosInTile).sqrMagnitude).First();
            return true;
        }


        private static List<Vector3> possibleSpotsCache3 = new List<Vector3>();
        private static int SpawnIndexThisFrame = 0;
        /// <summary>
        /// Finds a space on a tile without techs or obstructions
        /// </summary>
        /// <param name="headingDirection"></param>
        /// <param name="tilePos"></param>
        /// <param name="finalPos">The final spot IN SCENE</param>
        /// <returns></returns>
        public static bool FindFreeSpaceOnActiveTile(Vector2 headingDirection, IntVector2 tilePos, out Vector3 finalPos)
        {
            finalPos = Vector3.zero;
            //List<EnemyTechUnit> ETUs = GetTechsInTile(tile.coord);
            int partitions = (int)ManWorld.inst.TileSize / 80; // rough tech spacing needed
            float partitionScale = ManWorld.inst.TileSize / partitions;
            float partDist = ManWorld.inst.TileSize;
            Vector3 tileInPosScene = ManWorld.inst.TileManager.CalcTileOriginScene(tilePos);
            int extActionRange = AIGlobals.EnemyExtendActionRange - 48;
            extActionRange *= extActionRange;

            for (int stepX = 0; stepX < partDist; stepX += (int)partitionScale)
            {
                for (int stepZ = 0; stepZ < partDist; stepZ += (int)partitionScale)
                {
                    Vector3 New = new Vector3(stepX + tileInPosScene.x, tileInPosScene.y, stepZ + tileInPosScene.z);
                    if (ManWorld.inst.GetTerrainHeight(New, out float height))
                    {
                        float hPart = partitionScale / 2;
                        New.y = height + 6;
                        if ((New.SetY(0) - Singleton.playerPos.SetY(0)).sqrMagnitude > extActionRange 
                            && IsRadiusClearOfTechObst(New, hPart))
                        {
                            //DebugTAC_AI.Log("TACtical_AI: FindFreeSpaceOnActiveTile spawn position at " + New);
                            possibleSpotsCache3.Add(New);
                        }
                    }
                    else
                    {
                        DebugTAC_AI.Log("TACtical_AI: FindFreeSpaceOnActiveTile Terrain null at " + New);
                    }
                }
            }
            if (!possibleSpotsCache3.Any())
                return false;

            if (possibleSpotsCache3.Count == 1)
            {
                finalPos = possibleSpotsCache3.First();
                possibleSpotsCache3.Clear();
                return true;
            }

            try
            {
                Vector3 Directed = -(headingDirection.normalized * ManWorld.inst.TileSize);
                finalPos = possibleSpotsCache3.OrderBy(x => (x - Directed).sqrMagnitude).ElementAt(SpawnIndexThisFrame);
                possibleSpotsCache3.Clear();
                //DebugTAC_AI.Log("TACtical_AI: FindFreeSpaceOnActiveTile target spawned at " + finalPos);
                SpawnIndexThisFrame++;
                return true;
            }
            catch { }
            possibleSpotsCache3.Clear();
            return false;
        }
        private static bool IsRadiusClearOfTechObst(Vector3 pos, float radius)
        {
            foreach (Visible vis in Singleton.Manager<ManVisible>.inst.VisiblesTouchingRadius(pos, radius, new Bitfield<ObjectTypes>(new ObjectTypes[2] { ObjectTypes.Vehicle, ObjectTypes.Scenery })))
            {
                if (vis.isActive)
                {
                    return false;
                }
            }
            return true;
        }
        public static void AddTechToTile(ManSaveGame.StoredTile ST, TechData TD, int[] bIDs, int Team, Vector3 posScene, Quaternion forwards, bool anchored = false)
        {
            int ID = Singleton.Manager<ManSaveGame>.inst.CurrentState.GetNextVisibleID(ObjectTypes.Vehicle);
            ST.AddSavedTech(TD, bIDs, Team, posScene, Quaternion.LookRotation(forwards * Vector3.right, Vector3.up), true, false, true, ID, false, 99, false);
            if (ST.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> SV))
            {
                var sTech = (ManSaveGame.StoredTech)SV.Last();
                RawTechLoader.TrackTank(sTech, ID, anchored);
                sTech.m_ID = ID;
                RegisterTechUnloaded(sTech, true, true);
            }
            else
                throw new Exception("AddTechToTile added saved tech but could not find the added saved Tech afterwards!");
        }
        public static bool AddTechToTileAndSetETU(NP_TechUnit ETU, ManSaveGame.StoredTile ST, TechData TD, int[] bIDs, int Team, Vector3 posScene, Quaternion forwards, bool anchored = false)
        {
            int ID = Singleton.Manager<ManSaveGame>.inst.CurrentState.GetNextVisibleID(ObjectTypes.Vehicle);
            ST.AddSavedTech(TD, bIDs, Team, posScene, Quaternion.LookRotation(forwards * Vector3.right, Vector3.up), true, false, true, ID, false, 99, false);
            if (ST.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> SV))
            {
                var sTech = (ManSaveGame.StoredTech)SV.Last();
                ETU.SetTracker(RawTechLoader.TrackTank(sTech, ID, anchored));
                sTech.m_ID = ID;
                ETU.SetTech(sTech);
                return true;
            }
            return false;
        }
        public static void RemoveTechFromTeam(NP_TechUnit tech)
        {
            NP_Presence EP = GetTeam(tech.tech.m_TeamID);
            if (EP != null)
            {
                if (tech is NP_BaseUnit EBU)
                {
                    EP.EBUs.Remove(EBU);
                }
                else
                {
                    if (tech.isFounder)
                        EP.teamFounder = null;
                    EP.ETUs.Remove(tech);
                }
            }
        }
        public static bool RemoveTechFromTile(NP_TechUnit tech)
        {
            var tile = ManSaveGame.inst.GetStoredTile(tech.tilePos, false);
            if (tile != null && tile.m_StoredVisibles.TryGetValue(1, out var vals))
            {
                //tile.RemoveSavedVisible(ObjectTypes.Vehicle, tech.ID);
                for (int step = 0; step < vals.Count; step++)
                {
                    var val = vals[step];
                    if (val.m_ID == tech.ID)
                    {
                        vals.RemoveAt(step);
                        break;
                    }
                    else if (val is ManSaveGame.StoredTech tech2 && tech2 == tech.tech)
                    {
                        DebugTAC_AI.LogError("TACtical_AI: ManSaveGame altered ID for some reason? " + tech.Name + " prev, "
                            + tech.ID + " vs " + tech2.m_ID);
                        vals.RemoveAt(step);
                        break;
                    }
                }
            }
            try
            {

            }
            catch (Exception e)
            {
                DebugTAC_AI.Log("TACtical_AI: RemoveTechFromTile - Failed to purge " + tech.Name + " (SINGLE Player)");
                foreach (var item in ManVisible.inst.AllTrackedVisibles)
                {
                    if (item != null && item.visible == null && item.ObjectType == ObjectTypes.Vehicle
                        && ManWorld.inst.TileManager.IsTileAtPositionLoaded(item.Position))
                        DebugTAC_AI.Log("  Invalid Tech visible " + item.ID + ",  Team " + item.TeamID + ",  Destroyed " + item.wasDestroyed);
                }
                DebugTAC_AI.Log("TACtical_AI: RemoveTechFromTile - Error backtrace - " + e);
            }
            ManVisible.inst.StopTrackingVisible(tech.ID);
            /*
            if (!SpecialAISpawner.PurgeHost(tech.ID, tech.Name))
            {
                DebugTAC_AI.LogError("TACtical_AI: We tried to remove visible of ID " + tech.ID
                    + " from the world but failed.  There will now be ghost techs on the minimap");
                return false;
            }
            else
            {
                var tile = ManSaveGame.inst.GetStoredTile(tech.tilePos, false);
                if (tile != null && tile.m_StoredVisibles.TryGetValue(1, out var vals))
                {
                    for (int step = 0; step < vals.Count; step++)
                    {
                        var val = vals[step];
                        if (val.m_ID == tech.ID)
                        {
                            DebugTAC_AI.LogError("TACtical_AI: RemoveTechFromTile used PurgeHost to remove an item from save but it wasn't removed." +
                                "\n  Removing manually...");
                            vals.RemoveAt(step);
                            break;
                        }
                    }
                }
            }*/
            return true;
        }



        public static NP_Presence InsureTeam(int Team)
        {
            if (!NPTTeams.TryGetValue(Team, out NP_Presence EP))
            {
                EP = new NP_Presence(Team, AIGlobals.IsEnemyBaseTeam(Team));
                if (Team != 1)
                {
                    DebugTAC_AI.Log("TACtical_AI: ManEnemyWorld - New team " + Team + " added");
                    NPTTeams.Add(Team, EP);
                    TeamCreatedEvent.Send(Team);
                }
            }
            return EP;
        }
        public static NP_Presence GetTeam(int Team)
        {
            if (NPTTeams.TryGetValue(Team, out NP_Presence EP))
                return EP;
            return null;
        }
        public static void AddToTeam(NP_TechUnit ETU, bool AnnounceNew)
        {
            int team = ETU.tech.m_TeamID;
            bool notLoaded = !NPTTeams.TryGetValue(team, out NP_Presence EP);
            if (ETU is NP_BaseUnit EBU)
            {
                if (notLoaded)
                {
                    EP = new NP_Presence(team, AIGlobals.IsEnemyBaseTeam(ETU.tech.m_TeamID));
                    NPTTeams.Add(team, EP);
                }
                if (!EP.EBUs.ToList().Exists(delegate (NP_BaseUnit cand) { return cand.ID == EBU.ID; }))
                {
#if DEBUG
                    if (AnnounceNew)
                    {
                        DebugTAC_AI.Log("TACtical_AI: HandleTechUnloaded(EBU) New tech " + ETU.Name + " of type " + EBU.Faction + ", health " + EBU.MaxHealth + ", weapons " + EBU.AttackPower + ", funds " + EBU.BuildBucks);
                        DebugTAC_AI.Log("TACtical_AI: of Team " + ETU.tech.m_TeamID + ", tile " + ETU.tilePos);
                    }
#endif
                    if (EP.EBUs.Add(EBU))
                        DebugTAC_AI.Info("TACtical_AI: HandleTechUnloaded(EBUs) Added " + EBU.Name);
                    else
                        DebugTAC_AI.Log("TACtical_AI: HandleTechUnloaded(EBU) Hash Fail!");
                }
                else
                {
                    DebugTAC_AI.Log("TACtical_AI: HandleTechUnloaded(EBU) DUPLICATE TECH ADD REQUEST!");
                }
            }
            else
            {
                if (notLoaded)
                {
                    EP = new NP_Presence(team, AIGlobals.IsEnemyBaseTeam(ETU.tech.m_TeamID));
                    NPTTeams.Add(team, EP);
                }
                if (!EP.ETUs.ToList().Exists(delegate (NP_TechUnit cand) { return cand.ID == ETU.ID; }))
                {
#if DEBUG
                    if (AnnounceNew)
                    {
                        DebugTAC_AI.Log("TACtical_AI: HandleTechUnloaded(ETU) New tech " + ETU.Name + " of type " + ETU.Faction + ", health " + ETU.MaxHealth + ", weapons " + ETU.AttackPower);
                        DebugTAC_AI.Log("TACtical_AI: of Team " + ETU.tech.m_TeamID + ", tile " + ETU.tilePos);
                    }
#endif
                    if (ETU.isFounder)
                    {
                        if (EP.teamFounder != null)
                            DebugTAC_AI.Log("TACtical_AI: ASSERT - THERE ARE TWO TEAM FOUNDERS IN TEAM " + EP.team);
                        EP.teamFounder = ETU;
                    }
                    if (EP.ETUs.Add(ETU))
                        DebugTAC_AI.Info("TACtical_AI: HandleTechUnloaded(ETU) Added " + ETU.Name);
                    else
                        DebugTAC_AI.Log("TACtical_AI: HandleTechUnloaded(ETU) Hash Fail!");
                }
                else
                {
                    DebugTAC_AI.Log("TACtical_AI: HandleTechUnloaded(ETU) DUPLICATE TECH ADD REQUEST!");
                }
            }
        }


        public static void ChangeTeam(int Team, int newTeam)
        {
            foreach (var item in ManTechs.inst.CurrentTechs)
            {
                if (item)
                {
                    if (item.Team == Team)
                        item.SetTeam(newTeam);
                }
            }
            if (NPTTeams.TryGetValue(Team, out NP_Presence EP))
            {
                NPTTeams.Remove(Team);
                EP.ChangeTeamOfAllTechsUnloaded(newTeam);
                if (AIGlobals.IsBaseTeam(newTeam))
                    NPTTeams.Add(newTeam, EP);
            }
        }


        public static int UnloadedBaseCount(int team)
        {
            NP_Presence EP = GetTeam(team);
            if (EP == null)
                return 0;
            return EP.EBUs.Count;
        }
        public static int UnloadedMobileTechCount(int team)
        {
            NP_Presence EP = GetTeam(team);
            if (EP == null)
                return 0;
            return EP.ETUs.Count;
        }





        // MOVEMENT
        public static bool CanSeePositionTile(NP_BaseUnit EBU, Vector3 pos)
        {
            Vector2 vec = EBU.tilePos - WorldPosition.FromGameWorldPosition(pos).TileCoord;
            return vec.sqrMagnitude < BaseSightRadius * BaseSightRadius;
        }
        /// <summary>
        /// Moves the provided ETU in roughly 1 tile per movement token
        /// </summary>
        /// <param name="ETU"></param>
        /// <param name="target"></param>
        /// <returns>True if it's queued moving</returns>
        public static bool StrategicMoveQueue(NP_TechUnit ETU, IntVector2 target, Action<TileMoveCommand, bool, bool> onFinished, out bool criticalFail)
        {
            criticalFail = false;
            IntVector2 tilePosInitial = ETU.tilePos;
            if (ETU.MoveSpeed < 2)
            {
                DebugTAC_AI.Log("TACtical_AI: StrategicMoveQueue - Enemy Tech " + ETU.Name + " - Is too slow with " + ETU.MoveSpeed + " to move!");
                return false;
            }
            ManSaveGame.StoredTech ST = ETU.tech;
            //DebugTAC_AI.Log("TACtical_AI: Enemy Tech " + ST.m_TechData.Name + " wants to move to " + target);
            if (!IsTechOnSetTile(ETU))
            {
                DebugTAC_AI.Log("TACtical_AI: StrategicMoveQueue - Enemy Tech " + ETU.Name + " - ETU is not in set tile: " + ETU.tilePos);
                DebugTAC_AI.Log("TACtical_AI: StrategicMoveQueue - Enemy Tech " + ETU.Name + " - was destroyed!?");
                /*
                if (!TryRefindTech(ETU.tilePos, ETU, out IntVector2 IV2))
                {
                    DebugTAC_AI.Log("TACtical_AI: StrategicMoveQueue - Enemy Tech " + ETU.Name + " - was destroyed!?");

                    criticalFail = true;
                    return false;
                }
                DebugTAC_AI.Log("TACtical_AI: StrategicMoveQueue - Enemy Tech " + ETU.Name + " - ETU is actually: " + IV2 + " setting to that.");
                ETU.tilePos = IV2;*/
                return false;
            }
            float moveRate = ETU.MoveSpeed * TechTraverseMulti * MaintainerTickDelay;
            Vector2 moveDist = (target - tilePosInitial) * 2;
            Vector2 moveTileDist = moveDist.Clamp(-Vector2.one, Vector2.one);
            float dist = moveTileDist.magnitude * ManWorld.inst.TileSize;
            int ETA = (int)Math.Ceiling(dist / moveRate); // how long will it take?

            IntVector2 newWorldPos = tilePosInitial + new IntVector2(moveTileDist);
            if (!CanMoveUnloadedTechIntoTile(newWorldPos))
            {
                //DebugTAC_AI.Log("TACtical_AI: StrategicMoveQueue - Enemy Tech " + ETU.Name + " - Cannot Enter tile at " + newWorldPos +".");
                return false;
            }

            ManSaveGame.StoredTile Tile2 = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(newWorldPos);
            if (Tile2 != null)
            {
                TileMoveCommand TMC = new TileMoveCommand(ETU, newWorldPos, ETA, onFinished);
                QueuedUnitMoves.Add(ETU, TMC);
#if DEBUG
                DebugTAC_AI.Log("TACtical_AI: StrategicMoveQueue - Enemy Tech " + ETU.Name + " Requested move to " + newWorldPos);
                DebugTAC_AI.Log("   ETA is " + ETA + " enemy team turns.");
#endif
                return true;
            }
            DebugTAC_AI.Log("TACtical_AI: StrategicMoveQueue - Enemy Tech " + ETU.Name + " - Destination tile IS NULL OR NOT LOADED!");
            return false;
        }
        public static bool StrategicMoveConcluded(TileMoveCommand TMC)
        {
            //DebugTAC_AI.Log("TACtical_AI: StrategicMoveConcluded - EXECUTING");
            bool worked = MoveUnloadedTech(TMC.ETU, TMC.TargetTileCoord);
            TMC.OnFinish(worked, TMC.ETU.Exists());
            return worked;
        }
        public static bool MoveUnloadedTech(NP_TechUnit ETU, IntVector2 TargetTileCoord)
        {
            //DebugTAC_AI.Log("TACtical_AI: StrategicMoveConcluded - EXECUTING");
            if (!IsTechOnSetTile(ETU))
            {
                DebugTAC_AI.Log("TACtical_AI: StrategicMoveConcluded - Enemy Tech " + ETU.Name + " was reloaded or destroyed before finishing move!");
                return false;
            }
            ManSaveGame.StoredTile Tile2 = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(TargetTileCoord, true);
            if (Tile2 != null)
            {
                //ST.m_WorldPosition = new WorldPosition(Tile2.coord, Vector3.one);
                if (TryMoveTechIntoTile(ETU, Tile2))
                {
                    //DebugTAC_AI.Log("TACtical_AI: StrategicMoveConcluded - Enemy Tech " + ETU.Name + " Moved to " + Tile2.coord);
                    return true;
                }
                else
                {
                    DebugTAC_AI.Log("TACtical_AI: StrategicMoveConcluded - Enemy Tech " + ETU.Name + " - Move operation cancelled.");
                    return false;
                }
            }
            DebugTAC_AI.Log("TACtical_AI: StrategicMoveConcluded - Enemy Tech " + ETU.Name + " - TILE IS NULL!");
            //DebugTAC_AI.Log("TACtical_AI: StrategicMoveConcluded - Enemy Tech " + TMC.ETU.name + " - CRITICAL MISSION FAILIURE");
            return false;
        }



        // TECH BUILDING
        public static void ConstructNewTech(NP_BaseUnit BuilderTech, NP_Presence EP, SpawnBaseTypes SBT)
        {
            ManSaveGame.StoredTile ST = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(BuilderTech.tilePos, true);
            if (ST != null)
            {
                if (!FindFreeSpaceOnTileCircle(BuilderTech, ST, out Vector2 newPosOff))
                    return;
                if (!KickStart.EnemiesHaveCreativeInventory)
                {
                    var funder = UnloadedBases.GetSetTeamMainBase(EP);
                    funder.BuildBucks -= RawTechLoader.GetBaseTemplate(SBT).baseCost;
                }

                Quaternion quat = BuilderTech.tech.m_Rotation;
                Vector3 pos = ManWorld.inst.TileManager.CalcTileOriginScene(ST.coord) + newPosOff.ToVector3XZ();
                TechData TD = RawTechLoader.GetUnloadedTech(RawTechLoader.GetBaseTemplate(SBT), BuilderTech.tech.m_TeamID, out int[] bIDs);
                if (TD != null)
                {
                    AddTechToTile(ST, TD, bIDs, BuilderTech.tech.m_TeamID, pos, quat, false);
                }
            }
        }
        public static void ConstructNewExpansion(Vector3 position, NP_BaseUnit BuilderTech, NP_Presence EP, SpawnBaseTypes SBT)
        {
            ManSaveGame.StoredTile ST = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(BuilderTech.tilePos, true);
            if (ST != null)
            {
                if (!KickStart.EnemiesHaveCreativeInventory)
                {
                    var funder = UnloadedBases.GetSetTeamMainBase(EP);
                    funder.BuildBucks -= RawTechLoader.GetBaseTemplate(SBT).baseCost;
                }

                Quaternion quat = BuilderTech.tech.m_Rotation;
                TechData TD = RawTechLoader.GetBaseExpansionUnloaded(position, EP, RawTechLoader.GetBaseTemplate(SBT), out int[] bIDs);
                if (TD != null)
                {
                    AddTechToTile(ST, TD, bIDs, BuilderTech.tech.m_TeamID, position, quat, true);
                }
            }
        }
        public static void ConstructNewTechExt(NP_BaseUnit BuilderTech, NP_Presence EP, RawTechTemplate BT)
        {
            ManSaveGame.StoredTile ST = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(BuilderTech.tilePos, true);
            if (ST != null)
            {
                if (!FindFreeSpaceOnTileCircle(BuilderTech, ST, out Vector2 newPosOff))
                    return;
                if (!KickStart.EnemiesHaveCreativeInventory)
                {
                    var funder = UnloadedBases.GetSetTeamMainBase(EP);
                    funder.BuildBucks -= BT.baseCost;
                }

                Quaternion quat = BuilderTech.tech.m_Rotation;
                Vector3 pos = ManWorld.inst.TileManager.CalcTileOriginScene(ST.coord) + newPosOff.ToVector3XZ();
                TechData TD = RawTechLoader.GetUnloadedTech(BT, EP.Team, out int[] bIDs);
                if (TD != null)
                {
                    AddTechToTile(ST, TD, bIDs, BuilderTech.tech.m_TeamID, pos, quat, false);
                }
            }
        }
        public static void ConstructNewExpansionExt(Vector3 position, NP_BaseUnit BuilderTech, NP_Presence EP, RawTechTemplate BT)
        {
            ManSaveGame.StoredTile ST = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(BuilderTech.tilePos, true);
            if (ST != null)
            {
                if (!KickStart.EnemiesHaveCreativeInventory)
                {
                    var funder = UnloadedBases.GetSetTeamMainBase(EP);
                    funder.BuildBucks -= BT.baseCost;
                }

                Quaternion quat = BuilderTech.tech.m_Rotation;
                TechData TD = RawTechLoader.GetBaseExpansionUnloaded(position, EP, BT, out int[] bIDs);
                if (TD != null)
                {
                    AddTechToTile(ST, TD, bIDs, BuilderTech.tech.m_TeamID, position, quat, true);
                }
            }
        }


        // UPDATE
        public void Update()
        {
            if (!ManPauseGame.inst.IsPaused && ManNetwork.IsHost)
            {
                // The Strategic AI thinks every OperatorTickDelay seconds
                if (AIGlobals.TurboAICheat)
                    OperatorTicker = 0;
                if (OperatorTicker <= Time.time)
                {
                    OperatorTicker = Time.time + OperatorTickDelay;
                    UpdateOperators();
                }
                // The Strategic AI does movement every MaintainerTickDelay seconds
                if (MaintainerTicker <= Time.time)
                {
                    MaintainerTicker = Time.time + MaintainerTickDelay;
                    UpdateMaintainers();
                }
            }
        }
        public void UpdateOperators()
        {
            //DebugTAC_AI.Log("TACtical_AI: ManEnemyWorld - Updating All EnemyPresence");

            List<NP_Presence> EPScrambled = NPTTeams.Values.ToList();
            EPScrambled.Shuffle();
            int Count = EPScrambled.Count;
            if (KickStart.AllowStrategicAI)
            {
                DebugTAC_AI.Info("TACtical_AI: ManEnemyWorld.Update()[RTS] - There are " + ManTechs.inst.IterateTechs().Count() + " total Techs on scene.");
                for (int step = 0; step < Count;)
                {
                    NP_Presence EP = EPScrambled.ElementAt(step);
                    if (EP.UpdateOperatorRTS())
                    {
                        step++;
                        continue;
                    }

                    DebugTAC_AI.Info("TACtical_AI: ManEnemyWorld.Update()[RTS] - Team " + EP.Team + " has been unregistered");
                    TeamDestroyedEvent.Send(EP.Team);
                    NPTTeams.Remove(EP.Team);
                    EPScrambled.RemoveAt(step);
                    Count--;
                }
                ManEnemySiege.UpdateThis();
            }
            else
            {
                DebugTAC_AI.Info("TACtical_AI: ManEnemyWorld.Update() - There are " + ManTechs.inst.IterateTechs().Count() + " total Techs on scene.");
                for (int step = 0; step < Count;)
                {
                    NP_Presence EP = EPScrambled.ElementAt(step);
                    if (EP.UpdateOperator())
                    {
                        step++;
                        continue;
                    }

                    DebugTAC_AI.Info("TACtical_AI: ManEnemyWorld.Update() - Team " + EP.Team + " has been unregistered");
                    TeamDestroyedEvent.Send(EP.Team);
                    NPTTeams.Remove(EP.Team);
                    EPScrambled.RemoveAt(step);
                    Count--;
                }
            }
            //DebugRawTechSpawner.RemoveOrphanTrackedVisibles();
        }

        public void UpdateMaintainers()
        {
            SpawnIndexThisFrame = 0;
            foreach (var item in NPTTeams)
            {
                item.Value.UpdateMaintainer(MaintainerTickDelay);
            }
            //DebugTAC_AI.Log("TACtical_AI: ManEnemyWorld - UpdateMaintainers()");
            for (int step = QueuedUnitMoves.Count - 1; step >= 0;)
            {
                var pair = QueuedUnitMoves.ElementAt(step);
                TileMoveCommand move = pair.Value;
                try
                {
                    if (!move.IsValid())
                        QueuedUnitMoves.Remove(pair.Key);
                    else if (move.CurrentTurn >= move.ExpectedMoveTurns)
                    {
                        StrategicMoveConcluded(move);
                        QueuedUnitMoves.Remove(pair.Key);
                    }
                    else
                    {
                        move.CurrentTurn++;
                        //DebugTAC_AI.Log("TACtical_AI: Turn " + move.CurrentTurn + "/" + move.ExpectedMoveTurns + " for " + move.ETU.tech.m_TechData.Name);
                        move.ETU.SetFakeTVLocation(move.PosSceneCurTime());
                    }
                }
                catch (Exception e)
                {
                    DebugTAC_AI.Log("TACtical_AI: ManEnemyWorld(UpdateMaintainers) - ERROR - " + e);
                    QueuedUnitMoves.Remove(pair.Key);
                }
                step--;
            }
        }


        // ETC
        private static NP_TechUnit GetETUFromTank(Tank sTech)
        {
            NP_TechUnit ETUo = null;
            if (NPTTeams.TryGetValue(sTech.Team, out NP_Presence EP))
            {
                ETUo = EP.EBUs.ToList().Find(delegate (NP_BaseUnit cand) { return cand.ID == sTech.visible.ID; });
                if (ETUo != null)
                    return ETUo;
                ETUo = EP.ETUs.ToList().Find(delegate (NP_TechUnit cand) { return cand.ID == sTech.visible.ID; });
                if (ETUo == null)
                    throw new NullReferenceException("GetETUFromTank could not find StoredTech " + sTech.name + " team " + sTech.Team);
            }
            else
                throw new NullReferenceException("GetETUFromTank could not find enemy team of ID " + sTech.Team);
            return ETUo;
        }
        private static bool TryGetETUFromTank(Tank sTech, out NP_TechUnit ETU)
        {
            if (NPTTeams.TryGetValue(sTech.Team, out NP_Presence EP))
            {
                ETU = EP.EBUs.ToList().Find(delegate (NP_BaseUnit cand) { return cand.ID == sTech.visible.ID; });
                if (ETU != null)
                    return true;
                ETU = EP.ETUs.ToList().Find(delegate (NP_TechUnit cand) { return cand.ID == sTech.visible.ID; });
                if (ETU != null)
                    return true;
            }
            ETU = null;
            return false;
        }
        private static NP_TechUnit GetETUFromInst(ManSaveGame.StoredTech sTech)
        {
            NP_TechUnit ETUo = null;
            if (NPTTeams.TryGetValue(sTech.m_TeamID, out NP_Presence EP))
            {
                ETUo = EP.EBUs.ToList().Find(delegate (NP_BaseUnit cand) { return cand.ID == sTech.m_ID; });
                if (ETUo != null)
                    return ETUo;
                ETUo = EP.ETUs.ToList().Find(delegate (NP_TechUnit cand) { return cand.ID == sTech.m_ID; });
                if (ETUo == null)
                    throw new NullReferenceException("GetETUFromInst could not find StoredTech " + sTech.m_TechData.Name + " team " + sTech.m_TeamID);
            }
            else
                throw new NullReferenceException("GetETUFromInst could not find enemy team of ID " + sTech.m_TeamID);

            return ETUo;
        }
        private static bool TryGetETUFromInst(ManSaveGame.StoredTech sTech, out NP_TechUnit ETU)
        {
            if (NPTTeams.TryGetValue(sTech.m_TeamID, out NP_Presence EP))
            {
                ETU = EP.EBUs.ToList().Find(delegate (NP_BaseUnit cand) { return cand.ID == sTech.m_ID; });
                if (ETU != null)
                    return true;
                ETU = EP.ETUs.ToList().Find(delegate (NP_TechUnit cand) { return cand.ID == sTech.m_ID; });
                if (ETU != null)
                    return true;
            }
            ETU = null;
            return false;
        }

        private static List<NP_TechUnit> ETUsInRange = new List<NP_TechUnit>();
        internal static List<NP_TechUnit> GetUnloadedTechsInTile(IntVector2 tilePos)
        {
            ETUsInRange.Clear();
            ManSaveGame.StoredTile Tile = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(tilePos, false);
            if (Tile == null)
                return ETUsInRange;

            if (Tile.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> viss))
            {
                foreach (ManSaveGame.StoredVisible STV in viss)
                {
                    var tech = (ManSaveGame.StoredTech)STV;
                    if (TryGetETUFromInst(tech, out var unit))
                        ETUsInRange.Add(unit);
                }
            }
            return ETUsInRange;
        }
        /// <summary>
        /// SPHERE Search!  ALL directions.
        /// </summary>
        /// <param name="tilePos"></param>
        /// <param name="InTilePos"></param>
        /// <param name="radius"></param>
        /// <returns></returns>
        internal static List<NP_TechUnit> GetTechsInTile(IntVector2 tilePos, Vector3 InTilePos, float radius)
        {
            //List<EnemyPresence> EPs = EnemyTeams.Values.ToList();
            ETUsInRange.Clear();
            float radS = radius * radius;
            ManSaveGame.StoredTile Tile = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(tilePos);
            if (Tile != null)
            {
                //Singleton.Manager<ManVisible>.inst.ID
                if (Tile.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> viss))
                {
                    Vector3 tilePosWorld = ManWorld.inst.TileManager.CalcTileCentre(tilePos);
                    foreach (ManSaveGame.StoredVisible STV in viss)
                    {
                        var tech = (ManSaveGame.StoredTech)STV;
                        if (((WorldPosition.FromScenePosition(tech.GetBackwardsCompatiblePosition()).GameWorldPosition)
                            - (InTilePos + tilePosWorld)).sqrMagnitude <= radS)
                            if (TryGetETUFromInst(tech, out var unit))
                                ETUsInRange.Add(unit);
                    }
                }
            }
            return ETUsInRange;
        }
        /// <summary>
        /// CYLINDER Search!  Forwards, backwards, left, right.
        /// </summary>
        /// <param name="tilePos"></param>
        /// <param name="InTilePos"></param>
        /// <param name="radius"></param>
        /// <returns></returns>
        internal static List<NP_TechUnit> GetTechsInTile(IntVector2 tilePos, Vector2 InTilePos, float radius)
        {
            //List<EnemyPresence> EPs = EnemyTeams.Values.ToList();
            ETUsInRange.Clear();
            float radS = radius * radius;
            ManSaveGame.StoredTile Tile = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(tilePos);
            if (Tile != null)
            {
                //Singleton.Manager<ManVisible>.inst.ID
                if (Tile.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> viss))
                {
                    Vector2 tilePosWorld = ManWorld.inst.TileManager.CalcTileOrigin(tilePos).ToVector2XZ();
                    foreach (ManSaveGame.StoredVisible STV in viss)
                    {
                        var tech = (ManSaveGame.StoredTech)STV;
                        if (((WorldPosition.FromScenePosition(tech.GetBackwardsCompatiblePosition()).GameWorldPosition).ToVector2XZ()
                            - (InTilePos + tilePosWorld)).sqrMagnitude <= radS)
                            if (TryGetETUFromInst(tech, out var unit))
                                ETUsInRange.Add(unit);
                    }
                }
            }
            return ETUsInRange;
        }

        /// <summary>
        /// CYLINDER Search!  Forwards, backwards, left, right.
        /// </summary>
        /// <param name="Tile"></param>
        /// <param name="tilePos"></param>
        /// <param name="InTilePos"></param>
        /// <param name="radius"></param>
        /// <returns></returns>
        internal static List<NP_TechUnit> GetTechsInTileCached(ref ManSaveGame.StoredTile Tile, IntVector2 tilePos, Vector2 InTilePos, float radius)
        {
            //List<EnemyPresence> EPs = EnemyTeams.Values.ToList();
            ETUsInRange.Clear();
            float radS = radius * radius;
            if (Tile == null)
                Tile = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(tilePos);
            if (Tile != null)
            {
                //Singleton.Manager<ManVisible>.inst.ID
                if (Tile.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> viss))
                {
                    Vector2 tilePosWorld = ManWorld.inst.TileManager.CalcTileOrigin(tilePos).ToVector2XZ();
                    foreach (ManSaveGame.StoredVisible STV in viss)
                    {
                        var tech = (ManSaveGame.StoredTech)STV;
                        if (((WorldPosition.FromScenePosition(tech.GetBackwardsCompatiblePosition()).GameWorldPosition).ToVector2XZ()
                            - (InTilePos + tilePosWorld)).sqrMagnitude <= radS)
                            if (TryGetETUFromInst(tech, out var unit))
                                ETUsInRange.Add(unit);
                    }
                }
            }
            return ETUsInRange;
        }

        internal static bool IsTechInTile(ref ManSaveGame.StoredTile Tile, IntVector2 tilePos, Vector2 InTilePos, float radius)
        {
            //List<EnemyPresence> EPs = EnemyTeams.Values.ToList();
            ETUsInRange.Clear();
            float radS = radius * radius;
            if (Tile == null)
                Tile = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(tilePos);
            if (Tile != null)
            {
                //Singleton.Manager<ManVisible>.inst.ID
                if (Tile.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> viss))
                {
                    Vector2 tilePosWorld = ManWorld.inst.TileManager.CalcTileOrigin(tilePos).ToVector2XZ();
                    foreach (ManSaveGame.StoredVisible STV in viss)
                    {
                        var tech = (ManSaveGame.StoredTech)STV;
                        if (((WorldPosition.FromScenePosition(tech.GetBackwardsCompatiblePosition()).GameWorldPosition).ToVector2XZ()
                            - (InTilePos + tilePosWorld)).sqrMagnitude <= radS)
                            if (TryGetETUFromInst(tech, out var unit))
                                return true;
                    }
                }
            }
            return false;
        }

        public static IntVector2 GetClosestVendor(NP_TechUnit tech)
        {
            var tile = GetTile(tech);
            Vector3 vendorPos = Vector3.zero;
            if (tile != null)
            {
                ManWorld.inst.TryFindNearestVendorPos(tech.PosWorld, out vendorPos);
            }
            return WorldPosition.FromGameWorldPosition(vendorPos).TileCoord;
        }


        public static int GetBiomeAutominerGains(Vector3 scenePos)
        {
            ChunkTypes[] res = RLoadedBases.TryGetBiomeResource(scenePos);
            int resCount = res.Count();
            int Gains = 0;
            for (int step = 0; resCount > step; step++)
            {
                Gains += ResourceManager.inst.GetResourceDef(UnloadedBases.TransChunker(res[step])).saleValue;
            }
            Gains /= resCount;
            return Gains;
        }
        public static int GetBiomeSurfaceGains(Vector3 scenePos)
        {
            ChunkTypes[] res = UnloadedBases.GetBiomeResourcesSurface(scenePos);
            int resCount = res.Count();
            int Gains = 0;
            for (int step = 0; resCount > step; step++)
            {
                Gains += ResourceManager.inst.GetResourceDef(UnloadedBases.TransChunker(res[step])).saleValue;
            }
            Gains /= resCount;
            return Mathf.RoundToInt(Gains * SurfaceHarvestingMulti);
        }


        internal class GUIManaged
        {
            private static bool teamsUnloadedDisp = false;
            private static bool combatLogDisp = false;
            private static HashSet<NP_Types> enabledTabs = null;
            private static HashSet<int> enabledTeams = null;
            public static void GUIGetTotalManaged()
            {
                if (!inst)
                {
                    GUILayout.Box("--- World (Unloaded) --- ");
                    return;
                }
                if (enabledTabs == null)
                {
                    enabledTabs = new HashSet<NP_Types>();
                    enabledTeams = new HashSet<int>();
                }
                GUILayout.Box("--- World --- ");
                int activeCount = 0;
                Dictionary<NP_Types, int> types = new Dictionary<NP_Types, int>();
                foreach (NP_Types item in Enum.GetValues(typeof(NP_Types)))
                {
                    types.Add(item, 0);
                }
                foreach (var item in AllTeamsUnloaded)
                {
                    if (item.Value != null)
                    {
                        activeCount++;
                        types[AIGlobals.GetNPTTeamType(item.Key)]++;
                    }
                }
                if (GUILayout.Button("Total: " + AllTeamsUnloaded.Count + " | Active: " + activeCount))
                    teamsUnloadedDisp = !teamsUnloadedDisp;
                if (teamsUnloadedDisp)
                {
                    foreach (var item in types)
                    {
                        if (GUILayout.Button("Alignment: " + item.Key.ToString() + " - " + item.Value))
                        {
                            if (enabledTabs.Contains(item.Key))
                                enabledTabs.Remove(item.Key);
                            else
                                enabledTabs.Add(item.Key);
                        }
                        if (enabledTabs.Contains(item.Key))
                        {
                            foreach (var item2 in AllTeamsUnloaded.ToList().FindAll(x => AIGlobals.GetNPTTeamType(x.Key) == item.Key))
                            {
                                if (GUILayout.Button("Team: [" + item2.Key.ToString() + "] - " + TeamNamer.GetTeamName(item2.Key)))
                                {
                                    if (enabledTeams.Contains(item2.Key))
                                        enabledTeams.Remove(item2.Key);
                                    else
                                        enabledTeams.Add(item2.Key);
                                }
                                if (enabledTeams.Contains(item2.Key))
                                {
                                    if (item2.Value.teamFounder != null)
                                    {
                                        var founder = item2.Value.teamFounder;
                                        GUILayout.Label("  Founder: " + founder.Name + " | Coord: " + founder.tilePos);
                                        GUILayout.Label("    PosWorld: " + founder.WorldPos.GameWorldPosition);
                                        GUILayout.Label("    Health: " + founder.Health + "/" + founder.MaxHealth + 
                                            " | Shield: " + founder.Shield + "/" + founder.MaxShield);
                                        GUILayout.Label("    Attack: " + founder.AttackPower);
                                    }
                                    else
                                    {
                                        GUILayout.Label("  Founder: N/A");
                                        GUILayout.Label("    PosWorld: N/A");
                                        GUILayout.Label("    Health: 0");
                                        GUILayout.Label("    Attack: 0");
                                    }
                                    if (item2.Value.MainBase != null)
                                    {
                                        var mb = item2.Value.MainBase;
                                        GUILayout.Label("  HQ: " + mb.Name + " | Coord: " + item2.Value.homeTile);
                                        GUILayout.Label("    PosWorld: " + mb.WorldPos.GameWorldPosition);
                                        GUILayout.Label("    Health: " + mb.Health + "/" + mb.MaxHealth + 
                                            " | Shield: " + mb.Shield + "/" + mb.MaxShield);
                                        GUILayout.Label("    Funds: " + mb.BuildBucks + " | Attack: " + mb.AttackPower);
                                    }
                                    else
                                    {
                                        GUILayout.Label("  HQ: N/A");
                                        GUILayout.Label("    PosWorld: N/A");
                                        GUILayout.Label("    Health: 0");
                                        GUILayout.Label("    Funds: Bankrupt");
                                    }
                                    GUILayout.Label("  Mode: " + item2.Value.TeamMode.ToString());
                                    GUILayout.Label("  Attacking: " + (item2.Value.attackStarted ? 
                                        ("true | Coord: " + item2.Value.attackTile) : "false"));
                                    GUILayout.Label("  Techs: " + item2.Value.ETUs.Count);
                                    StringBuilder SB = new StringBuilder();
                                    foreach (var item3 in item2.Value.ETUs)
                                    {
                                        SB.Append(item3.Name + ", ");
                                        var posV = ManVisible.inst.GetTrackedVisible(item3.ID);
                                        if (posV != null)
                                        {
                                            var vec = posV.GetWorldPosition().ScenePosition;
                                            switch (AIGlobals.GetNPTTeamType(item2.Value.team))
                                            {
                                                case NP_Types.Player:
                                                    DebugRawTechSpawner.DrawDirIndicator(vec, vec + new Vector3(0, 4, 0),
                                                        AIGlobals.PlayerColor, Time.deltaTime);
                                                    break;
                                                case NP_Types.Friendly:
                                                    DebugRawTechSpawner.DrawDirIndicator(vec, vec + new Vector3(0, 4, 0),
                                                        AIGlobals.FriendlyColor, Time.deltaTime);
                                                    break;
                                                case NP_Types.Neutral:
                                                    DebugRawTechSpawner.DrawDirIndicator(vec, vec + new Vector3(0, 4, 0),
                                                        AIGlobals.NeutralColor, Time.deltaTime);
                                                    break;
                                                case NP_Types.NonAggressive:
                                                    DebugRawTechSpawner.DrawDirIndicator(vec, vec + new Vector3(0, 4, 0),
                                                        AIGlobals.NeutralColor, Time.deltaTime);
                                                    break;
                                                case NP_Types.SubNeutral:
                                                    DebugRawTechSpawner.DrawDirIndicator(vec, vec + new Vector3(0, 4, 0),
                                                        AIGlobals.NeutralColor, Time.deltaTime);
                                                    break;
                                                case NP_Types.Enemy:
                                                    DebugRawTechSpawner.DrawDirIndicator(vec, vec + new Vector3(0, 4, 0), 
                                                        AIGlobals.EnemyColor, Time.deltaTime);
                                                    break;
                                                default:
                                                    break;
                                            }
                                        }
                                    }
                                    GUILayout.Label("    Names: " + SB.ToString());
                                    SB.Clear();
                                    foreach (var item3 in item2.Value.EBUs)
                                    {
                                        SB.Append(item3.Name + ", ");
                                    }
                                    GUILayout.Label("  Bases: " + item2.Value.EBUs.Count);
                                    GUILayout.Label("    Names: " + SB.ToString());
                                }
                            }
                        }
                    }
                }

                if (GUILayout.Button("Combat Active: " + NPTTeams.ToList().Exists(x => x.Value.IsFighting)))
                    combatLogDisp = !combatLogDisp;
                if (combatLogDisp)
                {
                    GUILayout.Label("Events:");
                    GUILayout.Label(GetCombatLog());
                }
            }
        }
    }
}
