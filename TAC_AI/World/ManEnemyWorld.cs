using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using TAC_AI.Templates;
using TAC_AI.AI.Enemy;
using TAC_AI.AI;

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
    public class ManEnemyWorld : MonoBehaviour
    {
        //-------------------------------------
        //              CONSTANTS
        //-------------------------------------
        // There are roughly around 6 chunks per node
        //  ETU = EnemyTechUnit = Unloaded, mobile enemy Tech
        //  EBU = EnemyBaseUnloaded = Unloaded, stationary enemy Base
        internal const int UpdateDelay = 4;             // How many seconds the AI will perform base actions
        internal const int UnitSightRadius = 2;         // How far an enemy Tech Unit can see other enemies. IN TILES
        internal const int BaseSightRadius = 4;         // How far an enemy Tech Unit can see other enemies. IN TILES
        internal const int EnemyBaseCullingExtents = 16; // How far from the player should enemy bases be removed 
        // from the world? IN TILES
        internal static int EnemyRaidProvokeExtents = 4;// How far the can the enemy bases issue raids on the player. IN TILES

        // Movement
        internal const int UpdateMoveDelay = 2;         // How many seconds the AI will perform a move
        internal static float TechTraverseMulti = 0.75f;// Multiplier for AI traverse speed over ALL terrain

        // Harvesting
        internal const float SurfaceHarvestingMulti = 5.5f; // The multiplier of unloaded
        internal const int ExpectedDPSDelitime = 60;    // How long we expect an ETU to be hitting an unloaded target for in seconds

        // Gains - (Per second)
        internal const int PassiveHQBonusIncome = 150;
        internal const int ExpansionIncome = 75;

        // Health-Based (Volume-Based)
        internal const float MobileHealthMulti = 0.05f;  // Health multiplier for out-of-play combat
        internal const float BaseHealthMulti = 0.1f;     // Health multiplier for out-of-play combat
        internal const float MobileCombatMulti = 10f;    // Damage multiplier for out-of-play combat
        internal const float BaseCombatMulti = 2f;      // Damage multiplier for out-of-play combat
        internal const int HealthRepairCost = 60;       // How much BB the AI should spend to repair unloaded damage
        internal const int HealthRepairRate = 15;       // How much the enemy should repair every turn

        // Corp Speeds For Each Corp when Unloaded
        private static readonly Dictionary<FactionTypesExt, float> corpSpeeds = new Dictionary<FactionTypesExt, float>() {
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


        private static readonly FieldInfo ProdDelay = typeof(ModuleItemProducer).GetField("m_SecPerItemProduced", BindingFlags.NonPublic | BindingFlags.Instance);

        private static float UpdateTimer = 0;
        private static float UpdateMoveTimer = 0;
        private static readonly Dictionary<int, EnemyPresence> EnemyTeams = new Dictionary<int, EnemyPresence>();
        private static readonly List<KeyValuePair<int, TileMoveCommand>> QueuedUnitMoves = new List<KeyValuePair<int, TileMoveCommand>>();

        public static Dictionary<int, EnemyPresence> AllTeamsUnloaded {
            get 
            {
                return new Dictionary<int, EnemyPresence>(EnemyTeams);
            }
        }


        private static bool setup = false;
        public static void Initiate()
        {
            if (!KickStart.AllowStrategicAI || inst)
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
            DebugTAC_AI.Log("TACtical_AI: Removed ManEnemyWorld.");
        }

        public static void LateInitiate()
        {
            if (!KickStart.AllowStrategicAI || setup)
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
            EnemyTeams.Clear();
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
                Singleton.Manager<ManWorld>.inst.TileManager.TilePopulatedEvent.Subscribe(OnTileTechsRespawned);
                Singleton.Manager<ManWorld>.inst.TileManager.TileDepopulatedEvent.Subscribe(OnTileTechsDespawned);
                subToTiles = true;
            }
            enabledThis = true;
            UpdateTimer = 0;
            inst.Invoke("OnWorldLoadEnd", 1);
        }
        public void OnWorldLoadEnd()
        {
            int count = 0;
            if (ManSaveGame.inst.CurrentState != null)
                ManSaveGame.inst.CurrentState.m_FileHasBeenTamperedWith = true;
            try
            {
                List<IntVector2> tiles = Singleton.Manager<ManSaveGame>.inst.CurrentState.m_StoredTiles.Keys.ToList();
                //List<ManSaveGame.StoredTile> tiles = Singleton.Manager<ManSaveGame>.inst.CurrentState.m_StoredTiles.Values.ToList();
                foreach (IntVector2 tile in tiles)
                {
                    ManSaveGame.StoredTile tileInst = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(tile, false);
                    if (tileInst == null)
                        continue;
                    if (tileInst.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> techs))
                    {
                        foreach (ManSaveGame.StoredVisible Vis in techs)
                        {
                            if (Vis is ManSaveGame.StoredTech tech)
                            {
                                RegisterTechUnloaded(tech, tile);
                                count++;
                            }
                        }
                    }
                }
                DebugTAC_AI.Log("TACtical_AI: OnWorldLoadEnd Handled " + count + " Techs");
                //Singleton.Manager<ManSaveGame>.inst.CurrentState.m_StoredTiles.Count();
            }
            catch { }
        }
        public static void OnWorldReset()
        {
            /*
            EnemyTeams.Clear();
            QueuedUnitMoves.Clear();
            */
        }

        public static void OnTileTechsRespawned(WorldTile WT)
        {
            if (!enabledThis)
                return;
            foreach (EnemyTechUnit ETU in GetTechsInTile(WT.Coord))
            {
                RemoveTechFromTeam(ETU); // Cannot manage loaded techs
                if (ETU is EnemyBaseUnit EBU)
                {
                    EBU.TryPushMoneyToLoadedInstance();
                }
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
                            RegisterTechUnloaded(tech, WT.Coord);
                    }
                }
            }
        }
        public static void OnTechDestroyed(Tank tech, ManDamage.DamageInfo poof)
        {
            if (!enabledThis)
                return;
            EnemyTechUnit ETU = GetETUFromTank(tech);
            if (ETU == null)
                return;
            if (poof.Damage != 0)
            {
                TechDestroyedEvent.Send(tech.Team, tech.visible.ID, true);
                UnloadedBases.RemoteRemove(ETU);
            }
        }






        public static bool TryRefindTech(IntVector2 prev, EnemyTechUnit tech, out IntVector2 found)
        {
            ManSaveGame.StoredTech techFind = tech.tech;
            found = prev;
            try
            {
                List<IntVector2> tiles = Singleton.Manager<ManSaveGame>.inst.CurrentState.m_StoredTiles.Keys.ToList();
                //List<ManSaveGame.StoredTile> tiles = Singleton.Manager<ManSaveGame>.inst.CurrentState.m_StoredTiles.Values.ToList();
                foreach (IntVector2 tile in tiles)
                {
                    ManSaveGame.StoredTile tileInst = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(tile, false);
                    if (tileInst == null)
                        continue;
                    if (tileInst.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> techs))
                    {
                        if (techs.Contains(techFind))
                        {
                            found = tile;
                            return true;
                        }
                    }
                }
                //Debug.Log("TACtical_AI: TryRefindTech - COULD NOT REFIND TECH!!!  Of name " + techFind.m_TechData.Name);
            }
            catch (Exception e)
            {
                DebugTAC_AI.Log("TACtical_AI: TryRefindTech - COULD NOT REFIND TECH!!! " + e);
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
        public static void RegisterTechUnloaded(ManSaveGame.StoredTech tech, IntVector2 tilePos, bool isNew = true, bool forceRegister = false)
        {
            int level = 0;
            if (AIGlobals.IsBaseTeam(tech.m_TeamID) || forceRegister)
            {   // Enemy Team
                List<TankPreset.BlockSpec> specs = tech.m_TechData.m_BlockSpecs;
                long healthAll = 0;
                if (tech.m_TechData.IsBase())
                {
                    try
                    {
                        if (ManVisible.inst.GetTrackedVisible(tech.m_ID) == null)
                        {
                            DebugTAC_AI.Log("TACtical_AI: Base unit " + tech.m_TechData.Name + " lacked TrackedVisible, fixing...");
                            RawTechLoader.TrackTank(tech, true);
                        }
                        EnemyBaseUnit EBU = new EnemyBaseUnit(tilePos, tech, PrepTeam(tech.m_TeamID));
                        level++;
                        foreach (TankPreset.BlockSpec spec in specs)
                        {
                            TankBlock TB = ManSpawn.inst.GetBlockPrefab(spec.GetBlockType());
                            if ((bool)TB)
                            {
                                var Weap = TB.GetComponent<ModuleWeapon>();
                                if ((bool)Weap)
                                {
                                    EBU.isArmed = true;
                                    EBU.AttackPower += TB.filledCells.Length;
                                }
                                var MIP = TB.GetComponent<ModuleItemProducer>();
                                if ((bool)MIP)
                                {
                                    EBU.revenue += (int)((GetBiomeAutominerGains(EBU.PosScene) * UpdateDelay) / (float)ProdDelay.GetValue(MIP));
                                }
                                healthAll += Mathf.Max(TB.GetComponent<ModuleDamage>().maxHealth, 1);
                            }
                        }
                        level++;
                        EBU.Faction = tech.m_TechData.GetMainCorpExt();
                        EBU.Health = (long)(healthAll * BaseHealthMulti);
                        EBU.MaxHealth = (long)(healthAll * BaseHealthMulti);
                        EBU.MoveSpeed = 0; //(STATIONARY)
                        level++;
                        EBU.BuildBucks = RBases.GetBuildBucksFromNameExt(tech.m_TechData.Name);
                        SpawnBaseTypes SBT = RawTechLoader.GetEnemyBaseTypeFromName(RBases.EnemyBaseFunder.GetActualName(tech.m_TechData.Name));
                        List<BasePurpose> BP = RawTechLoader.GetBaseTemplate(SBT).purposes;

                        level++;
                        if (BP.Contains(BasePurpose.Defense))
                            EBU.isDefense = true;
                        if (BP.Contains(BasePurpose.TechProduction))
                            EBU.isTechBuilder = true;
                        if (BP.Contains(BasePurpose.HasReceivers))
                        {
                            EBU.isHarvestBase = true;
                            EBU.revenue += GetBiomeSurfaceGains(ManWorld.inst.TileManager.CalcTileCentreScene(tilePos)) * UpdateDelay;
                        }
                        if (BP.Contains(BasePurpose.Headquarters))
                            EBU.isSiegeBase = true;

                        level++;
                        AddToTeam(EBU, isNew);
                    }
                    catch
                    {
                        DebugTAC_AI.Log("TACtical_AI: HandleTechUnloaded(EBU) Failiure on init at level " + level + "!");
                    }
                }
                else
                {
                    try
                    {
                        if (ManVisible.inst.GetTrackedVisible(tech.m_ID) == null)
                        {
                            DebugTAC_AI.Log("TACtical_AI: Tech unit " + tech.m_TechData.Name + " lacked TrackedVisible, fixing...");
                            RawTechLoader.TrackTank(tech);
                        }
                        EnemyTechUnit ETU = new EnemyTechUnit(tilePos, tech);
                        level++;
                        foreach (TankPreset.BlockSpec spec in specs)
                        {
                            TankBlock TB = ManSpawn.inst.GetBlockPrefab(spec.GetBlockType());
                            if ((bool)TB)
                            {
                                var Weap = TB.GetComponent<ModuleWeapon>();
                                if ((bool)Weap)
                                {
                                    ETU.isArmed = true;
                                    ETU.AttackPower += TB.filledCells.Length;
                                }
                                if (TB.GetComponent<ModuleItemHolderBeam>())
                                    ETU.canHarvest = true;
                                healthAll += Mathf.Max(TB.GetComponent<ModuleDamage>().maxHealth, 1);
                            }
                        }
                        level++;
                        ETU.isFounder = tech.m_TechData.IsTeamFounder();
                        ETU.Health = (long)(healthAll * MobileHealthMulti);
                        ETU.MaxHealth = (long)(healthAll * MobileHealthMulti);
                        ETU.Faction = tech.m_TechData.GetMainCorpExt();
                        ETU.MoveSpeed = 0;
                        level++;
                        if (!tech.m_TechData.CheckIsAnchored() && !tech.m_TechData.Name.Contains(" " + RawTechLoader.turretChar))
                        {
                            ETU.MoveSpeed = 25;
                            if (corpSpeeds.TryGetValue(ETU.Faction, out float sped))
                                ETU.MoveSpeed = sped;
                        }
                        level++;
                        AddToTeam(ETU, isNew);
                    }
                    catch
                    {
                        DebugTAC_AI.Log("TACtical_AI: HandleTechUnloaded(ETU) Failiure on init at level " + level + "!");
                    }
                }
            }
        }
        public static ManSaveGame.StoredTile GetTile(EnemyTechUnit tech)
        {
            ManSaveGame.StoredTile Tile = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(tech.tilePos);
            if (Tile != null)
            {
                if (Tile.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> techs))
                {
                    if (techs.Contains(tech.tech))
                        return Tile;
                }
            }
            return null;
        }
        public static bool TryMoveTechIntoTile(EnemyTechUnit tech, ManSaveGame.StoredTile tileToMoveInto, bool setPrecise = true)
        {
            if (tileToMoveInto != null)
            {
                int range = AIGlobals.EnemyExtendActionRange - AIGlobals.TileFringeDist;
                range *= range;
                Vector3 tilePosScene = WorldPosition.FromGameWorldPosition(ManWorld.inst.TileManager.CalcTileCentre(tileToMoveInto.coord)).ScenePosition;
                if (range > (tilePosScene - Singleton.playerPos).sqrMagnitude && ManWorld.inst.CheckIsTileAtPositionLoaded(tilePosScene))
                {   // Loading in an active Tech
                    if (AIGlobals.AtSceneTechMax())
                    {
                        DebugTAC_AI.Log("TACtical_AI: MoveTechIntoTile(loaded) - The scene is at the Tech max limit.  Cannot proceed.");
                        return false;
                    }
                    if (!FindFreeSpaceOnActiveTile(tech.tilePos - tileToMoveInto.coord, tileToMoveInto.coord, out Vector3 newPosOff))
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
                    List<int> BTs = new List<int>();
                    ManSaveGame.StoredTech ST = tech.tech;
                    foreach (TankPreset.BlockSpec mem in ST.m_TechData.m_BlockSpecs)
                    {
                        if (!BTs.Contains((int)mem.m_BlockType))
                        {
                            BTs.Add((int)mem.m_BlockType);
                        }
                    }
                    if (!FindFreeSpaceOnTile(tech.tilePos - tileToMoveInto.coord, tileToMoveInto, out Vector2 newPosOff))
                    {
                        DebugTAC_AI.Log("TACtical_AI: MoveTechIntoTile - Could not find a valid spot to move the Tech");
                        return false;
                    }
                    RemoveTechFromTile(tech);
                    RemoveTechFromTeam(tech);
                    Vector3 newPos = newPosOff.ToVector3XZ() + ManWorld.inst.TileManager.CalcTileCentreScene(tileToMoveInto.coord);
                    Quaternion fromDirect = Quaternion.LookRotation(newPos - ST.m_Position);
                    tileToMoveInto.AddSavedTech(ST.m_TechData, BTs.ToArray(), ST.m_TeamID, newPos, fromDirect, true, false, true, ST.m_ID, false, 1, true);
                    if (tileToMoveInto.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> SV))
                    {
                        ManSaveGame.StoredTech techInst = (ManSaveGame.StoredTech)SV.Last();

                        RegisterTechUnloaded(techInst, tileToMoveInto.coord, false);
                        DebugTAC_AI.Log("TACtical_AI: MoveTechIntoTile - Moved a Tech");
                        if (techInst.m_WorldPosition.TileCoord == tileToMoveInto.coord)
                        {
                            if (setPrecise)
                            {
                                Vector3 inTilePos = newPosOff.ToVector3XZ();
                                inTilePos.y = ManWorld.inst.TileManager.GetTerrainHeightAtPosition(inTilePos, out _);
                                techInst.m_WorldPosition = new WorldPosition(tileToMoveInto.coord, inTilePos); // Accurate it!
                            }
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
            float halfDist = (ManWorld.inst.TileSize - partitionScale) / 2;
            List<Vector2> possibleSpots = new List<Vector2>();
            ManSaveGame.StoredTile tileCache = tile;
            if (tileCache == null)
            {
                DebugTAC_AI.Log("TACtical_AI: FindFreeSpaceOnTile - Attempt to find free space failed: Tile is null");
                return false;
            }
            for (int stepX = (int)-halfDist; stepX < halfDist; stepX += (int)partitionScale)
            {
                for (int stepY = (int)-halfDist; stepY < halfDist; stepY += (int)partitionScale)
                {
                    Vector2 New = new Vector2(stepX, stepY);
                    if (GetTechsInTileCached(ref tileCache, tile.coord, New, partitionScale - 2).Count() == 0)
                        possibleSpots.Add(New);
                    //else
                    //    Debug.Log("TACtical_AI: FindFreeSpaceOnTile - Attempt to find free space failed on tile coord " + tile.coord + ", " + New);
                }
            }
            if (possibleSpots.Count == 0)
                return false;

            if (possibleSpots.Count == 1)
            {
                finalPos = possibleSpots.First();
                return true;
            }

            Vector2 Directed = headingDirection.normalized * ManWorld.inst.TileSize;
            possibleSpots = possibleSpots.OrderBy(x => (x - Directed).sqrMagnitude).ToList();
            finalPos = possibleSpots.First();
            return true;
        }
        /// <summary>
        /// Builds around the TechBuilder
        /// </summary>
        /// <param name="headingDirection"></param>
        /// <param name="tile"></param>
        /// <param name="finalPos"></param>
        /// <returns></returns>
        public static bool FindFreeSpaceOnTileCircle(EnemyBaseUnit TechBuilder, ManSaveGame.StoredTile tile, out Vector2 finalPos)
        {
            Vector2 PosInTile = (WorldPosition.FromScenePosition(TechBuilder.tech.GetBackwardsCompatiblePosition()).GameWorldPosition
                - ManWorld.inst.TileManager.CalcTileCentre(TechBuilder.tilePos)).ToVector2XZ();
            finalPos = Vector3.zero;
            //List<EnemyTechUnit> ETUs = GetTechsInTile(tile.coord);
            int partitions = (int)ManWorld.inst.TileSize / 64;
            float partitionScale = ManWorld.inst.TileSize / partitions;
            float halfDist = (ManWorld.inst.TileSize - partitionScale) / 2;
            List<Vector2> possibleSpots = new List<Vector2>();
            ManSaveGame.StoredTile tileCache = tile;
            if (tileCache == null)
            {
                DebugTAC_AI.Log("TACtical_AI: FindFreeSpaceOnTileCircle - Attempt to find free space failed: Tile is null");
                return false;
            }
            for (int stepX = (int)-halfDist; stepX < halfDist; stepX += (int)partitionScale)
            {
                for (int stepY = (int)-halfDist; stepY < halfDist; stepY += (int)partitionScale)
                {
                    Vector2 New = new Vector2(stepX, stepY);
                    if (GetTechsInTileCached(ref tileCache, tile.coord, New, partitionScale - 2).Count() == 0)
                        possibleSpots.Add(New);
                    //else
                    //    Debug.Log("TACtical_AI: FindFreeSpaceOnTileCircle - Attempt to find free space failed on tile coord " + tile.coord + ", " + New);
                }
            }
            if (possibleSpots.Count == 0)
                return false;

            if (possibleSpots.Count == 1)
            {
                finalPos = possibleSpots.First();
                return true;
            }

            possibleSpots = possibleSpots.OrderBy(x => (x - PosInTile).sqrMagnitude).ToList();
            finalPos = possibleSpots.First();
            return true;
        }


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
            float halfDist = ManWorld.inst.TileSize / 2;
            List<Vector3> possibleSpots = new List<Vector3>();
            Vector3 tileInPosScene = WorldPosition.FromGameWorldPosition(ManWorld.inst.TileManager.CalcTileCentre(tilePos)).ScenePosition;
            int extActionRange = AIGlobals.EnemyExtendActionRange - 48;
            extActionRange *= extActionRange;

            for (int stepX = (int)-halfDist; stepX < halfDist; stepX += (int)partitionScale)
            {
                for (int stepY = (int)-halfDist; stepY < halfDist; stepY += (int)partitionScale)
                {
                    Vector3 New = new Vector3(stepX + tileInPosScene.x, tileInPosScene.y, stepY + tileInPosScene.z);
                    if (ManWorld.inst.GetTerrainHeight(New, out float height))
                    {
                        float hPart = partitionScale / 2;
                        New.y = height + 6;
                        if (IsRadiusClearOfTechObst(New, hPart) && (New.SetY(0) - Singleton.playerPos.SetY(0)).sqrMagnitude < extActionRange)
                        {
                            //Debug.Log("TACtical_AI: FindFreeSpaceOnActiveTile spawn position at " + New);
                            possibleSpots.Add(New);
                        }
                    }
                    else
                    {
                        DebugTAC_AI.Log("TACtical_AI: FindFreeSpaceOnActiveTile Terrain null at " + New);
                    }
                }
            }
            if (possibleSpots.Count == 0)
                return false;

            if (possibleSpots.Count == 1)
            {
                finalPos = possibleSpots.First();
                return true;
            }

            Vector3 Directed = -(headingDirection.normalized * ManWorld.inst.TileSize);
            possibleSpots = possibleSpots.OrderBy(x => (x - Directed).sqrMagnitude).ToList();
            try
            {
                finalPos = possibleSpots.ElementAt(SpawnIndexThisFrame);
                //Debug.Log("TACtical_AI: FindFreeSpaceOnActiveTile target spawned at " + finalPos);
                SpawnIndexThisFrame++;
                return true;
            }
            catch { }
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
        public static void RemoveTechFromTeam(EnemyTechUnit tech)
        {
            try
            {
                EnemyPresence EP = GetTeam(tech.tech.m_TeamID);
                if (tech is EnemyBaseUnit EBU)
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
            catch { }
        }
        public static bool RemoveTechFromTile(EnemyTechUnit tech)
        {

            ManVisible.inst.ObliterateTrackedVisibleFromWorld(tech.tech.m_ID);
            if (ManVisible.inst.GetTrackedVisible(tech.tech.m_ID) != null)
            {
                ManVisible.inst.ObliterateTrackedVisibleFromWorld(tech.tech.m_ID);
                if (ManVisible.inst.GetTrackedVisible(tech.tech.m_ID) != null)
                {
                    DebugTAC_AI.LogError("TACtical_AI: Could not remove tech from tile the correct way");
                    var tile = GetTile(tech);
                    if (tile != null)
                    {
                        ManVisible.inst.StopTrackingVisible(tech.tech.m_ID);
                        tile.RemoveSavedVisible(ObjectTypes.Vehicle, tech.tech.m_ID);
                        return true;
                    }
                    ManVisible.inst.StopTrackingVisible(tech.tech.m_ID);
                    if (ManVisible.inst.GetTrackedVisible(tech.tech.m_ID) != null)
                    {
                        DebugTAC_AI.LogError("TACtical_AI: We tried to remove visible of ID " + tech.tech.m_ID
                    + " from the world but failed.  There will now be ghost techs on the minimap");
                        //Debug.FatalError();
                        return false;
                    }
                }
            }
            return true;
        }



        public static EnemyPresence PrepTeam(int Team)
        {
            if (!EnemyTeams.TryGetValue(Team, out EnemyPresence EP))
            {
                DebugTAC_AI.Log("TACtical_AI: ManEnemyWorld - New team " + Team + " added");
                EP = new EnemyPresence(Team);
                EnemyTeams.Add(Team, EP);
                TeamCreatedEvent.Send(Team);
            }
            return EP;
        }
        public static EnemyPresence GetTeam(int Team)
        {
            if (!EnemyTeams.TryGetValue(Team, out EnemyPresence EP))
            {
                EP = new EnemyPresence(Team);
                EnemyTeams.Add(Team, EP);
            }
            return EP;
        }
        public static void AddToTeam(EnemyTechUnit ETU, bool AnnounceNew)
        {
            bool notLoaded = !EnemyTeams.TryGetValue(ETU.tech.m_TeamID, out EnemyPresence EP);
            if (ETU is EnemyBaseUnit EBU)
            {
                if (notLoaded)
                {
                    EP = new EnemyPresence(ETU.tech.m_TeamID);
                    EnemyTeams.Add(ETU.tech.m_TeamID, EP);
                }
                if (!EP.EBUs.Exists(delegate (EnemyBaseUnit cand) { return cand.tech == EBU.tech; }))
                {
#if DEBUG
                    if (AnnounceNew)
                    {
                        Debug.Log("TACtical_AI: HandleTechUnloaded(EBU) New tech " + ETU.Name + " of type " + EBU.Faction + ", health " + EBU.MaxHealth + ", weapons " + EBU.AttackPower + ", funds " + EBU.BuildBucks);
                        Debug.Log("TACtical_AI: of Team " + ETU.tech.m_TeamID + ", tile " + ETU.tilePos);
                    }
#endif
                    EP.EBUs.Add(EBU);
                }
                else
                {
                    DebugTAC_AI.Log("TACtical_AI: HandleTechUnloaded(EBU) DUPLICATE TECH ADD REQUEST!");
                }
            }
            else
            {
                if (notLoaded)
                    return;
                if (!EP.ETUs.Exists(delegate (EnemyTechUnit cand) { return cand.tech == ETU.tech; }))
                {
#if DEBUG
                    if (AnnounceNew)
                    {
                        Debug.Log("TACtical_AI: HandleTechUnloaded(ETU) New tech " + ETU.Name + " of type " + ETU.Faction + ", health " + ETU.MaxHealth + ", weapons " + ETU.AttackPower);
                        Debug.Log("TACtical_AI: of Team " + ETU.tech.m_TeamID + ", tile " + ETU.tilePos);
                    }
#endif
                    if (ETU.isFounder)
                    {
                        if (EP.teamFounder != null)
                            DebugTAC_AI.Log("TACtical_AI: ASSERT - THERE ARE TWO TEAM FOUNDERS IN TEAM " + EP.team);
                        EP.teamFounder = ETU;
                    }
                    EP.ETUs.Add(ETU);
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
            if (EnemyTeams.TryGetValue(Team, out EnemyPresence EP))
            {
                EnemyTeams.Remove(Team);
                EP.ChangeTeamOfAllTechsUnloaded(newTeam);
                if (AIGlobals.IsBaseTeam(newTeam))
                    EnemyTeams.Add(newTeam, EP);
            }
        }


        public static int UnloadedBaseCount(int team)
        {
            EnemyPresence EP = GetTeam(team);
            if (EP == null)
                return 0;
            return EP.EBUs.Count;
        }
        public static int UnloadedMobileTechCount(int team)
        {
            EnemyPresence EP = GetTeam(team);
            if (EP == null)
                return 0;
            return EP.ETUs.Count;
        }





        // MOVEMENT
        public static bool CanSeePositionTile(EnemyBaseUnit EBU, Vector3 pos)
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
        public static bool StrategicMoveQueue(EnemyTechUnit ETU, IntVector2 target, out bool criticalFail)
        {
            criticalFail = false;
            if (ETU.tilePos == target || ETU.MoveSpeed < 2)
                return false;
            ManSaveGame.StoredTech ST = ETU.tech;
            bool worked = false;
            ETU.isMoving = false;
            //Debug.Log("TACtical_AI: Enemy Tech " + ST.m_TechData.Name + " wants to move to " + target);
            ManSaveGame.StoredTile Tile1 = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(ETU.tilePos, false);
            if (Tile1 != null)
            {
                if (Tile1.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> techs))
                {
                    if (techs.Contains(ST))
                    {
                        worked = true;
                    }
                }
            }
            if (!worked)
            {
                DebugTAC_AI.Log("TACtical_AI: StrategicMoveQueue - Enemy Tech " + ETU.Name + " - ETU is not in set tile: " + ETU.tilePos);
                if (!TryRefindTech(ETU.tilePos, ETU, out IntVector2 IV2))
                {
                    DebugTAC_AI.Log("TACtical_AI: StrategicMoveQueue - Enemy Tech " + ETU.Name + " - was destroyed!?");

                    criticalFail = true;
                    return false;
                }
                DebugTAC_AI.Log("TACtical_AI: StrategicMoveQueue - Enemy Tech " + ETU.Name + " - ETU is actually: " + IV2 + " setting to that.");
                ETU.tilePos = IV2;
                return false;
            }
            float moveRate = (ETU.MoveSpeed * TechTraverseMulti * UpdateMoveDelay) / Globals.inst.MilesPerGameUnit;
            Vector2 moveDist = target - ETU.tilePos;
            Vector2 moveTileDist = moveDist.Clamp(-Vector2.one, Vector2.one);
            float dist = moveTileDist.magnitude * ManWorld.inst.TileSize;
            int ETA = (int)Math.Ceiling(dist / moveRate); // how long will it take?

            IntVector2 newWorldPos = ETU.tilePos + new IntVector2(moveTileDist);

            ManSaveGame.StoredTile Tile2 = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(newWorldPos);
            if (Tile2 != null)
            {
                TileMoveCommand TMC = new TileMoveCommand
                {
                    ETU = ETU,
                    TargetTileCoord = newWorldPos
                };
                ETU.isMoving = true;
                QueuedUnitMoves.Add(new KeyValuePair<int, TileMoveCommand>(ETA, TMC));
#if DEBUG
                Debug.Log("TACtical_AI: StrategicMoveQueue - Enemy Tech " + ETU.Name + " Requested move to " + newWorldPos);
                Debug.Log("   ETA is " + ETA + " enemy team turns.");
#endif
                return true;
            }
            DebugTAC_AI.Log("TACtical_AI: StrategicMoveQueue - Enemy Tech " + ETU.Name + " - Destination tile IS NULL OR NOT LOADED!");
            return false;
        }
        public static bool StrategicMoveConcluded(TileMoveCommand TMC)
        {
            //Debug.Log("TACtical_AI: StrategicMoveConcluded - EXECUTING");
            return MoveUnloadedTech(TMC.ETU, TMC.TargetTileCoord);
        }
        public static bool MoveUnloadedTech(EnemyTechUnit ETU, IntVector2 TargetTileCoord)
        {
            //Debug.Log("TACtical_AI: StrategicMoveConcluded - EXECUTING");
            ManSaveGame.StoredTech ST = ETU.tech;
            bool worked = false;
            ETU.isMoving = false;
            ManSaveGame.StoredTile Tile1 = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(ETU.tilePos);
            if (Tile1 != null)
            {
                if (Tile1.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> techs))
                {
                    if (techs.Contains(ST))
                    {
                        worked = true;
                    }
                }
            }
            if (!worked)
            {
                IntVector2 tilePosOld = ETU.tilePos;
                if (TryRefindTech(ETU.tilePos, ETU, out ETU.tilePos))
                {
                    if (tilePosOld != ETU.tilePos)
                        DebugTAC_AI.Log("TACtical_AI: StrategicMoveConcluded - Enemy Tech " + ETU.Name + " Position was borked!  Refound positions!");

                }
                else
                {
                    DebugTAC_AI.Log("TACtical_AI: StrategicMoveConcluded - Enemy Tech " + ETU.Name + " was reloaded or destroyed before finishing move!");
                }
                return false;
            }
            ManSaveGame.StoredTile Tile2 = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(TargetTileCoord);
            if (Tile2 != null)
            {
                //ST.m_WorldPosition = new WorldPosition(Tile2.coord, Vector3.one);
                if (TryMoveTechIntoTile(ETU, Tile2))
                {
                    DebugTAC_AI.Log("TACtical_AI: StrategicMoveConcluded - Enemy Tech " + ETU.Name + " Moved to " + Tile2.coord);
                    return true;
                }
                else
                {
                    DebugTAC_AI.Log("TACtical_AI: StrategicMoveConcluded - Enemy Tech " + ETU.Name + " - Move operation cancelled.");
                    return false;
                }
            }
            DebugTAC_AI.Log("TACtical_AI: StrategicMoveConcluded - Enemy Tech " + ETU.Name + " - TILE IS NULL!");
            //Debug.Log("TACtical_AI: StrategicMoveConcluded - Enemy Tech " + TMC.ETU.name + " - CRITICAL MISSION FAILIURE");
            return false;
        }



        // TECH BUILDING
        public static void ConstructNewTech(EnemyBaseUnit BuilderTech, EnemyPresence EP, SpawnBaseTypes SBT)
        {
            ManSaveGame.StoredTile ST = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(BuilderTech.tilePos, true);
            if (ST != null)
            {
                if (!FindFreeSpaceOnTileCircle(BuilderTech, ST, out Vector2 newPosOff))
                    return;
                if (!KickStart.EnemiesHaveCreativeInventory)
                {
                    var funder = UnloadedBases.GetTeamFunder(EP);
                    funder.BuildBucks -= RawTechLoader.GetBaseTemplate(SBT).baseCost;
                }

                int ID = Singleton.Manager<ManSaveGame>.inst.CurrentState.GetNextVisibleID(ObjectTypes.Vehicle);
                Quaternion quat = BuilderTech.tech.m_Rotation;
                Vector3 pos = ManWorld.inst.TileManager.CalcTileCentreScene(ST.coord) + newPosOff.ToVector3XZ();
                TechData TD = RawTechLoader.GetUnloadedTech(RawTechLoader.GetBaseTemplate(SBT), BuilderTech.tech.m_TeamID, out int[] bIDs);
                if (TD != null)
                {
                    ST.AddSavedTech(TD, bIDs, EP.Team, pos, Quaternion.LookRotation(quat * Vector3.right, Vector3.up), true, false, true, ID, false, 99, false);

                    if (ST.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> SV))
                    {
                        ManSaveGame.StoredTech sTech = (ManSaveGame.StoredTech)SV.Last();
                        RawTechLoader.TrackTank(sTech);
                        RegisterTechUnloaded(sTech, BuilderTech.tilePos);
                    }
                }
            }
        }
        public static void ConstructNewExpansion(Vector3 position,EnemyBaseUnit BuilderTech, EnemyPresence EP, SpawnBaseTypes SBT)
        {
            ManSaveGame.StoredTile ST = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(BuilderTech.tilePos, true);
            if (ST != null)
            {
                if (!KickStart.EnemiesHaveCreativeInventory)
                {
                    var funder = UnloadedBases.GetTeamFunder(EP);
                    funder.BuildBucks -= RawTechLoader.GetBaseTemplate(SBT).baseCost;
                }

                int ID = Singleton.Manager<ManSaveGame>.inst.CurrentState.GetNextVisibleID(ObjectTypes.Vehicle);
                Quaternion quat = BuilderTech.tech.m_Rotation;
                TechData TD = RawTechLoader.GetBaseExpansionUnloaded(position, EP, RawTechLoader.GetBaseTemplate(SBT), out int[] bIDs);
                if (TD != null)
                {
                    ST.AddSavedTech(TD, bIDs, EP.Team, position, Quaternion.LookRotation(quat * Vector3.right, Vector3.up), true, false, true, ID, false, 99, false);

                    if (ST.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> SV))
                    {
                        ManSaveGame.StoredTech sTech = (ManSaveGame.StoredTech)SV.Last();

                        RawTechLoader.TrackTank(sTech, true);
                        RegisterTechUnloaded(sTech, BuilderTech.tilePos);
                    }
                }
            }
        }
        public static void ConstructNewTechExt(EnemyBaseUnit BuilderTech, EnemyPresence EP, BaseTemplate BT)
        {
            ManSaveGame.StoredTile ST = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(BuilderTech.tilePos, true);
            if (ST != null)
            {
                if (!FindFreeSpaceOnTileCircle(BuilderTech, ST, out Vector2 newPosOff))
                    return;
                if (!KickStart.EnemiesHaveCreativeInventory)
                {
                    var funder = UnloadedBases.GetTeamFunder(EP);
                    funder.BuildBucks -= BT.baseCost;
                }

                int ID = Singleton.Manager<ManSaveGame>.inst.CurrentState.GetNextVisibleID(ObjectTypes.Vehicle);
                Quaternion quat = BuilderTech.tech.m_Rotation;
                Vector3 pos = ManWorld.inst.TileManager.CalcTileCentreScene(ST.coord) + newPosOff.ToVector3XZ();
                TechData TD = RawTechLoader.GetUnloadedTech(BT, EP.Team, out int[] bIDs);
                if (TD != null)
                {
                    ST.AddSavedTech(TD, bIDs, EP.Team, pos, Quaternion.LookRotation(quat * Vector3.right, Vector3.up), true, false, true, ID, false, 99, false);

                    if (ST.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> SV))
                    {
                        var sTech = (ManSaveGame.StoredTech)SV.Last();
                        RawTechLoader.TrackTank(sTech, false);
                        RegisterTechUnloaded(sTech, BuilderTech.tilePos);
                    }
                }
            }
        }
        public static void ConstructNewExpansionExt(Vector3 position, EnemyBaseUnit BuilderTech, EnemyPresence EP, BaseTemplate BT)
        {
            ManSaveGame.StoredTile ST = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(BuilderTech.tilePos, true);
            if (ST != null)
            {
                if (!KickStart.EnemiesHaveCreativeInventory)
                {
                    var funder = UnloadedBases.GetTeamFunder(EP);
                    funder.BuildBucks -= BT.baseCost;
                }

                int ID = Singleton.Manager<ManSaveGame>.inst.CurrentState.GetNextVisibleID(ObjectTypes.Vehicle);
                Quaternion quat = BuilderTech.tech.m_Rotation;
                TechData TD = RawTechLoader.GetBaseExpansionUnloaded(position, EP, BT, out int[] bIDs);
                if (TD != null)
                {
                    ST.AddSavedTech(TD, bIDs, EP.Team, position, Quaternion.LookRotation(quat * Vector3.right, Vector3.up), true, false, true, ID, false, 99, false);
                    if (ST.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> SV))
                    {
                        var sTech = (ManSaveGame.StoredTech)SV.Last();
                        RawTechLoader.TrackTank(sTech, false);
                        RegisterTechUnloaded(sTech, BuilderTech.tilePos);
                    }
                }
            }
        }


        // UPDATE
        public void Update()
        {
            if (!ManPauseGame.inst.IsPaused && ManNetwork.IsHost)
            {
                // The Strategic AI thinks every UpdateDelay seconds
                UpdateTimer += Time.deltaTime;
                if (AIGlobals.TurboAICheat)
                    UpdateTimer = UpdateDelay;
                if (UpdateTimer >= UpdateDelay)
                {
                    UpdateTimer -= UpdateDelay;
                    //Debug.Log("TACtical_AI: ManEnemyWorld - Updating All EnemyPresence");

                    List<EnemyPresence> EPScrambled = EnemyTeams.Values.ToList();
                    EPScrambled.Shuffle();
                    int Count = EPScrambled.Count;
                    for (int step = 0; step < Count;)
                    {
                        EnemyPresence EP = EPScrambled.ElementAt(step);
                        if (EP.UpdateGrandCommand())
                        {
                            step++;
                            continue;
                        }

                        DebugTAC_AI.Info("TACtical_AI: UpdateGrandCommand - Team " + EP.Team + " has been unregistered");
                        TeamDestroyedEvent.Send(EP.Team);
                        EnemyTeams.Remove(EP.Team);
                        EPScrambled.RemoveAt(step);
                        Count--;
                    }
                    ManEnemySiege.UpdateThis();
                    DebugRawTechSpawner.RemoveOrphanTrackedVisibles();
                }
                // The techs move every UpdateMoveDelay seconds
                UpdateMoveTimer += Time.deltaTime;
                if (UpdateMoveTimer >= UpdateMoveDelay)
                {
                    UpdateMoveTimer -= UpdateMoveDelay;
                    SpawnIndexThisFrame = 0;
                    //Debug.Log("TACtical_AI: ManEnemyWorld - Updating unit move commands");
                    for (int step = QueuedUnitMoves.Count - 1; step >= 0;)
                    {
                        try
                        {
                            KeyValuePair<int, TileMoveCommand> move = QueuedUnitMoves.ElementAt(step);
                            if (move.Key <= 1)
                            {
                                StrategicMoveConcluded(move.Value);
                                QueuedUnitMoves.RemoveAt(step);
                            }
                            else
                            {
                                QueuedUnitMoves.Add(new KeyValuePair<int, TileMoveCommand>(move.Key - 1, move.Value));
                                QueuedUnitMoves.RemoveAt(step);
                            }
                        }
                        catch
                        {
                            DebugTAC_AI.Log("TACtical_AI: ManEnemyWorld(Update) - ERROR");
                            QueuedUnitMoves.RemoveAt(step);
                        }
                        step--;
                    }
                }
            }
        }

        // ETC
        private static EnemyTechUnit GetETUFromTank(Tank sTech)
        {
            EnemyTechUnit ETUo = null;
            if (EnemyTeams.TryGetValue(sTech.Team, out EnemyPresence EP))
            {
                ETUo = EP.EBUs.Find(delegate (EnemyBaseUnit cand) { return cand.tech.m_ID == sTech.visible.ID; });
                if (ETUo != null)
                    return ETUo;
                ETUo = EP.ETUs.Find(delegate (EnemyTechUnit cand) { return cand.tech.m_ID == sTech.visible.ID; });
            }
            return ETUo;
        }
        private static EnemyTechUnit GetETUFromInst(ManSaveGame.StoredTech sTech)
        {
            EnemyTechUnit ETUo = null;
            if (EnemyTeams.TryGetValue(sTech.m_TeamID, out EnemyPresence EP))
            {
                ETUo = EP.EBUs.Find(delegate (EnemyBaseUnit cand) { return cand.tech == sTech; });
                if (ETUo != null)
                    return ETUo;
                ETUo = EP.ETUs.Find(delegate (EnemyTechUnit cand) { return cand.tech == sTech; });
            }
            return ETUo;
        }
        
        internal static List<EnemyTechUnit> GetTechsInTile(IntVector2 tilePos)
        {
            List<EnemyTechUnit> ETUsInRange = new List<EnemyTechUnit>();

            ManSaveGame.StoredTile Tile = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(tilePos, false);
            if (Tile == null)
                return ETUsInRange;

            if (Tile.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> viss))
            {
                foreach (ManSaveGame.StoredVisible STV in viss)
                {
                    var tech = (ManSaveGame.StoredTech)STV;
                    ETUsInRange.Add(GetETUFromInst(tech));
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
        internal static List<EnemyTechUnit> GetTechsInTile(IntVector2 tilePos, Vector3 InTilePos, float radius)
        {
            //List<EnemyPresence> EPs = EnemyTeams.Values.ToList();
            List<EnemyTechUnit> ETUsInRange = new List<EnemyTechUnit>();
            float radS = radius * radius; 
            ManSaveGame.StoredTile Tile = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(tilePos);
            Vector3 tilePosWorld = ManWorld.inst.TileManager.CalcTileCentre(tilePos);
            if (Tile != null)
            { 
                //Singleton.Manager<ManVisible>.inst.ID
                if (Tile.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> viss))
                {
                    foreach (ManSaveGame.StoredVisible STV in viss)
                    {
                        var tech = (ManSaveGame.StoredTech)STV;
                        if (((WorldPosition.FromScenePosition(tech.GetBackwardsCompatiblePosition()).GameWorldPosition)
                            - (InTilePos + tilePosWorld)).sqrMagnitude <= radS)
                            ETUsInRange.Add(GetETUFromInst(tech));
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
        internal static List<EnemyTechUnit> GetTechsInTile(IntVector2 tilePos, Vector2 InTilePos, float radius)
        {
            //List<EnemyPresence> EPs = EnemyTeams.Values.ToList();
            List<EnemyTechUnit> ETUsInRange = new List<EnemyTechUnit>();
            float radS = radius * radius;
            ManSaveGame.StoredTile Tile = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(tilePos);
            Vector2 tilePosWorld = ManWorld.inst.TileManager.CalcTileCentre(tilePos).ToVector2XZ();
            if (Tile != null)
            {
                //Singleton.Manager<ManVisible>.inst.ID
                if (Tile.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> viss))
                {
                    foreach (ManSaveGame.StoredVisible STV in viss)
                    {
                        var tech = (ManSaveGame.StoredTech)STV;
                        if (((WorldPosition.FromScenePosition(tech.GetBackwardsCompatiblePosition()).GameWorldPosition).ToVector2XZ()
                            - (InTilePos + tilePosWorld)).sqrMagnitude <= radS)
                            ETUsInRange.Add(GetETUFromInst(tech));
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
        internal static List<EnemyTechUnit> GetTechsInTileCached(ref ManSaveGame.StoredTile Tile, IntVector2 tilePos, Vector2 InTilePos, float radius)
        {
            //List<EnemyPresence> EPs = EnemyTeams.Values.ToList();
            List<EnemyTechUnit> ETUsInRange = new List<EnemyTechUnit>();
            float radS = radius * radius;
            if (Tile == null)
                Tile = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(tilePos);
            Vector2 tilePosWorld = ManWorld.inst.TileManager.CalcTileCentre(tilePos).ToVector2XZ();
            if (Tile != null)
            {
                //Singleton.Manager<ManVisible>.inst.ID
                if (Tile.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> viss))
                {
                    foreach (ManSaveGame.StoredVisible STV in viss)
                    {
                        var tech = (ManSaveGame.StoredTech)STV;
                        if (((WorldPosition.FromScenePosition(tech.GetBackwardsCompatiblePosition()).GameWorldPosition).ToVector2XZ() 
                            - (InTilePos + tilePosWorld)).sqrMagnitude <= radS)
                            ETUsInRange.Add(GetETUFromInst(tech));
                    }
                }
            }
            return ETUsInRange;
        }

        public static IntVector2 GetClosestVendor(EnemyTechUnit tech)
        {
            var tile = GetTile(tech);
            Vector3 vendorPos = Vector3.zero;
            if (tile != null)
            {
                ManWorld.inst.TryFindNearestVendorPos(tech.PosOrigin, out vendorPos);
            }
            return WorldPosition.FromGameWorldPosition(vendorPos).TileCoord;
        }


        public static int GetBiomeAutominerGains(Vector3 scenePos)
        {
            ChunkTypes[] res = RBases.TryGetBiomeResource(scenePos);
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
    }
}
