using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using UnityEngine;
using TerraTechETCUtil;
using TAC_AI.AI;
using TAC_AI.AI.Enemy;

namespace TAC_AI.Templates
{
    internal class TrackedAirborneAI
    {
        public Tank tank;
        public TrackedVisible trackVis;


        public TrackedAirborneAI(Tank set, bool IsSpace = false)
        {
            tank = set;
            trackVis = AIGlobals.GetTrackedVisible(set.visible.ID);
            if (!IsSpace)
                tank.SleepEvent.Subscribe(OnStop);
            if (trackVis != null)
                trackVis.OnDespawnEvent.Subscribe(OnRecycle);
        }
        public void OnRecycle(Visible vis)
        {   // It crashed 
            ManVisible.inst.ObliterateTrackedVisibleFromWorld(trackVis);
            SpecialAISpawner.AirPool.Remove(this);
            tank.SleepEvent.Unsubscribe(OnStop);
        }

        public void OnStop(bool yes)
        {   // It crashed 
            tank.SleepEvent.Unsubscribe(OnStop);
            if (trackVis != null)
                trackVis.OnDespawnEvent.Unsubscribe(OnRecycle);
            SpecialAISpawner.AirPool.Remove(this);
            if (tank)
                SpecialAISpawner.Eradicate(tank);
            else if (trackVis != null)
                ManVisible.inst.ObliterateTrackedVisibleFromWorld(trackVis);
            else
                DebugTAC_AI.LogError(KickStart.ModID + ": TrackedAirborneAI - Could not remove an aircraft from the world!");

        }
    }
    public class SpecialAISpawner : MonoBehaviour
    {   //  We handle all the AI goodies here when Population Injector is N/A
        //      This module should ONLY be active (when initated) in Campaign mode!!!

        //      If you need to request access this to be opened to public for coding reasons, 
        //          please let LegioniteTerraTech know.  
        //      For tech-related concerns or additions, confront Legionite on the TerraTech Community Discord.

        private static readonly bool forceOn = false;    // spawn in creative no matter what

        internal static SpecialAISpawner inst;
        private static ManLicenses Licences;

        private static Tank playerTank => Singleton.playerTank;
        internal const int trollTeam = -9001;
        internal static int EnemyTeam => AIGlobals.LonerEnemyTeam;
        public static float AirborneSpawnChance => 301 - (KickStart.difficulty + 50);

        public static float SpaceSpawnChance => KickStart.CommitDeathMode ? 0.1f : SpaceshipChance;


        /// <summary>
        /// AIRTECHS
        /// </summary>
        internal static List<TrackedAirborneAI> AirPool = new List<TrackedAirborneAI>();

        /// <summary>
        /// ERADICATORS (HUGE TECHS)
        /// </summary>
        /// 
        public static List<Tank> Eradicators = new List<Tank>();

        internal static bool thisActive = false;
        internal static bool CreativeMode = false;
        private float counter = 0;
        private int updateTimer = 0;

        const int MaxAirborneAIAllowed = 4;
        public const int AirborneAISpawnOdds = 30;   // Out of 300 (dynamically changed based on difficulty)
        public const float SpaceshipChance = 0.02f;     // Out of 100
        public const float AirSpawnDist = 400;
        public const float AirDespawnDist = 475;
        public const float SpaceBeginAltitude = 500;
        internal static float AirSpawnInterval = 30;


        public static void Initiate()
        {   // 
            if (inst)
                return;
            inst = new GameObject("AISpawnerAux").AddComponent<SpecialAISpawner>();
            Singleton.Manager<ManGameMode>.inst.ModeStartEvent.Subscribe(DetermineActiveOnMode);
            DebugTAC_AI.Log(KickStart.ModID + ": SpecialAISpawner - Initated!");
            inst.gameObject.SetActive(false);
            RawTechLoader.Initiate();

            DetermineActiveOnModeType();
        }
        public static void DeInit()
        {
            if (!inst)
                return;
            RawTechLoader.DeInitiate();
            Singleton.Manager<ManGameMode>.inst.ModeStartEvent.Unsubscribe(DetermineActiveOnMode);
            Destroy(inst);
            inst = null;
            DebugTAC_AI.Log(KickStart.ModID + ": SpecialAISpawner - DeInitated");
        }

        public static void DetermineActiveOnModeType()
        {
            ManGameMode.GameType mode = ManGameMode.inst.GetCurrentGameType();
            AirPool.Clear();
            RawTechLoader.inst.ClearQueue();
            RawTechExporter.ReloadCheck();
            OverrideManPop.QueuedChangeToRagnarokPop();
            DebugRawTechSpawner.ShouldBeActive();
            DebugTAC_AI.Log("(DetermineActiveOnModeTypeDelayed) Next mode is " + mode.ToString());
            if ((mode == ManGameMode.GameType.MainGame || mode == ManGameMode.GameType.Misc
                || mode == ManGameMode.GameType.CoOpCampaign || mode == ManGameMode.GameType.CoOpCreative) && ManNetwork.IsHost)
            {
                if (mode == ManGameMode.GameType.Misc || mode == ManGameMode.GameType.CoOpCreative)
                    CreativeMode = true;
                else
                    CreativeMode = false;
                Resume();
            }
            else
            {
                Pause();
                CreativeMode = false;
            }
        }
        public static void DetermineActiveOnMode(Mode mode)
        {   // 
            AirPool.Clear();
            RawTechLoader.inst.ClearQueue();
            RawTechExporter.ReloadCheck();
            OverrideManPop.QueuedChangeToRagnarokPop();
            DebugRawTechSpawner.ShouldBeActive();
            DebugTAC_AI.Log("(DetermineActiveOnMode) Next mode is " + mode.GetGameType().ToString());
            if ((mode is ModeMain || mode is ModeMisc || mode is ModeCoOpCampaign || mode is ModeCoOpCreative) && ManNetwork.IsHost)
            {
                if (mode is ModeMisc || mode is ModeCoOpCreative)
                    CreativeMode = true;
                else
                    CreativeMode = false;
                Resume();
            }
            else
            {
                Pause();
                CreativeMode = false;
            }
        }
        public static void UpdatePlayerTank(Tank tank, bool beam)
        {   // 
            if (tank.IsNotNull())
            {
            }
        }
        public static void UpdatePlayerTank()
        {   // 
        }
        public static void PlayerTankDeathCheck(Tank tank, ManDamage.DamageInfo oof)
        {   // 
            if (tank == playerTank && KickStart.Difficulty < 100)
            {   // Player could have been killed by airborneAI - remove all pop airborne AI
                DestroyAllPooledAirborneAI(true);
            }
        }


        private static List<FactionSubTypes> factionsAvail = new List<FactionSubTypes>();
        public static void UpdateFactionsAvailAir()
        {
            factionsAvail.Clear();
            if (Licences.GetLicense(FactionSubTypes.GSO).CurrentLevel >= 0)// flight grade is 2 but random spawns start at 0
                factionsAvail.Add(FactionSubTypes.GSO);
            // GC literally can't fly an airborneAI
            if (Licences.GetLicense(FactionSubTypes.GC).IsDiscovered && Licences.GetLicense(FactionSubTypes.GC).CurrentLevel >= 1)
                factionsAvail.Add(FactionSubTypes.GC);
            if (Licences.GetLicense(FactionSubTypes.VEN).IsDiscovered && Licences.GetLicense(FactionSubTypes.VEN).CurrentLevel >= 0)// flight grade is 1 but random spawns start at 0
                factionsAvail.Add(FactionSubTypes.VEN);
            if (Licences.GetLicense(FactionSubTypes.HE).IsDiscovered && Licences.GetLicense(FactionSubTypes.HE).CurrentLevel >= 1)
                factionsAvail.Add(FactionSubTypes.HE);
            if (Licences.GetLicense(FactionSubTypes.BF).IsDiscovered && Licences.GetLicense(FactionSubTypes.BF).CurrentLevel >= 0)
                factionsAvail.Add(FactionSubTypes.BF);
            if (Licences.GetLicense(FactionSubTypes.SJ).IsDiscovered && Licences.GetLicense(FactionSubTypes.SJ).CurrentLevel >= 0)
                factionsAvail.Add(FactionSubTypes.SJ);
        }
        public static void UpdateFactionsAvailLand()
        {
            factionsAvail.Clear();
            if (Licences.GetLicense(FactionSubTypes.GSO).CurrentLevel >= 2)
                factionsAvail.Add(FactionSubTypes.GSO);
            if (Licences.GetLicense(FactionSubTypes.GC).IsDiscovered)
                factionsAvail.Add(FactionSubTypes.GC);
            if (Licences.GetLicense(FactionSubTypes.VEN).IsDiscovered)
                factionsAvail.Add(FactionSubTypes.VEN);
            if (Licences.GetLicense(FactionSubTypes.HE).IsDiscovered)
                factionsAvail.Add(FactionSubTypes.HE);
            if (Licences.GetLicense(FactionSubTypes.BF).IsDiscovered)
                factionsAvail.Add(FactionSubTypes.BF);
            if (Licences.GetLicense(FactionSubTypes.SJ).IsDiscovered)
                factionsAvail.Add(FactionSubTypes.SJ);
        }

        public static void OverrideSpawning(ManSpawn.TechSpawnParams TSP, Vector3 pos)
        {   // 
            if (TSP.m_IsPopulation)
            {
                if (KickStart.EnableBetterAI && (ManNetwork.IsHost || !ManNetwork.IsNetworked))
                {
                    AIWiki.hintADV.Show();
                    if (!KickStart.isPopInjectorPresent)
                    {
                        TechData newTech = TSP.m_TechToSpawn;
                        FactionSubTypes FST = TSP.m_TechToSpawn.GetMainCorp();
                        FactionLevel lvl = RawTechLoader.TryGetPlayerLicenceLevel();
                        if (KickStart.AllowSeaEnemiesToSpawn && KickStart.isWaterModPresent && 
                            AI.Movement.AIEPathing.AboveTheSeaForcedAccurate(pos) &&
                            RawTechBase.GetBaseTerrain(TSP.m_TechToSpawn, TSP.m_TechToSpawn.CheckIsAnchored()) == BaseTerrain.Land)
                        {
                            TrySetSpawnSea(TSP, FST, FST, lvl, ref newTech);
                        }
                        else if (UnityEngine.Random.Range(0, 100) < KickStart.LandEnemyOverrideChance) // Override for normal Tech spawns
                        {
                            TrySetSpawnLand(TSP, FST, FST, lvl, ref newTech);
                        }
                    }
                }
            }
        }
        public static void TrySetSpawnLand(ManSpawn.TechSpawnParams TSP, FactionSubTypes FTE, FactionSubTypes FST,
            FactionLevel lvl, ref TechData newTech)
        {
            // we try OVERRIDE TECH SPAWN
            try
            {
                int grade = 99;
                try
                {
                    if (!CreativeMode)
                        grade = ManLicenses.inst.GetCurrentLevel(FST);
                }
                catch { }
                RawTechPopParams RTF = RawTechPopParams.Default;
                RTF.Faction = FTE;
                RTF.Terrain = BaseTerrain.Land;
                RTF.Purpose = BasePurpose.NotStationary;
                RTF.Progression = lvl;
                RTF.TargetFactionGrade = grade;
                RTF.MaxPrice = KickStart.EnemySpawnPriceMatching;
                if (RawTechLoader.ShouldUseCustomTechs(out int randSelect, RTF))
                {
                    newTech = RawTechLoader.GetUnloadedTech(ModTechsDatabase.ExtPopTechsAllLookup(randSelect), TSP.m_Team, true, out _);

                    if (newTech == null)
                    {
                        DebugTAC_AI.Exception("Land Tech spawning override failed as fetched TechData is null.  Please report this.");
                        return;
                    }
                    if (newTech.m_BlockSpecs == null)
                    {
                        DebugTAC_AI.Exception("Land Tech spawning override failed as fetched TechData's block info is null.  Please report this.");
                        return;
                    }
                    if (newTech.m_BlockSpecs.Count == 0)
                    {
                        DebugTAC_AI.Exception("Land Tech spawning override failed as no blocks are present on modified spawning Tech.  Please report this.");
                        return;
                    }
                    DebugTAC_AI.Log(KickStart.ModID + ":  Tech " + TSP.m_TechToSpawn.Name + " has been swapped out for land tech " + newTech.Name + " instead");
                    TSP.m_TechToSpawn = newTech;
                }
                else
                {
                    SpawnBaseTypes type = RawTechLoader.GetEnemyBaseType(RTF);
                    if (type != SpawnBaseTypes.NotAvail && !RawTechLoader.IsFallback(type))
                    {
                        newTech = RawTechLoader.GetUnloadedTech(type, TSP.m_Team, true, out _);
                        if (newTech == null)
                        {
                            DebugTAC_AI.Exception("Land Tech spawning override(PREFAB) failed as \"" + newTech.Name + "\" fetched TechData is null.  Please report this.");
                            return;
                        }
                        if (newTech.m_BlockSpecs == null)
                        {
                            DebugTAC_AI.Exception("Land Tech spawning override(PREFAB) failed as \"" + newTech.Name + "\" fetched TechData's block info is null.  Please report this.");
                            return;
                        }
                        if (newTech.m_BlockSpecs.Count == 0)
                        {
                            DebugTAC_AI.Exception("Land Tech spawning override(PREFAB) failed as no blocks are present on modified spawning Tech \"" + newTech.Name + "\".  Please report this.");
                            return;
                        }

                        DebugTAC_AI.Log(KickStart.ModID + ":  Tech " + TSP.m_TechToSpawn.Name + " has been swapped out for land tech " + newTech.Name + " instead");
                        TSP.m_TechToSpawn = newTech;
                    }
                    // Else we don't do anything.
                }
            }
            catch (Exception e)
            {
                DebugTAC_AI.Log(KickStart.ModID + ": Attempt to swap Land tech failed! - " + e);
                throw e;
            }
        }
        public static void TrySetSpawnSea(ManSpawn.TechSpawnParams TSP, FactionSubTypes FTE, FactionSubTypes FST, 
            FactionLevel lvl, ref TechData newTech)
        {
            // OVERRIDE TO SHIP
            try
            {
                int grade = 99;
                try
                {
                    if (!SpecialAISpawner.CreativeMode)
                        grade = ManLicenses.inst.GetCurrentLevel(FST);
                }
                catch { }


                RawTechPopParams RTF = RawTechPopParams.Default;
                RTF.Faction = FTE;
                RTF.Progression = lvl;
                RTF.TargetFactionGrade = grade;
                RTF.Terrain = BaseTerrain.Sea;
                RTF.Purpose = BasePurpose.NotStationary;
                if (RawTechLoader.ShouldUseCustomTechs(out int randSelect, RTF))
                {
                    newTech = RawTechLoader.GetUnloadedTech(ModTechsDatabase.ExtPopTechsAllLookup(randSelect), TSP.m_Team, true, out _);

                    if (newTech == null)
                    {
                        DebugTAC_AI.Exception("Water Tech spawning override failed as fetched TechData is null.  Please report this.");
                        return;
                    }
                    if (newTech.m_BlockSpecs == null)
                    {
                        DebugTAC_AI.Exception("Water Tech spawning override failed as fetched TechData's block info is null.  Please report this.");
                        return;
                    }
                    if (newTech.m_BlockSpecs.Count == 0)
                    {
                        DebugTAC_AI.Exception("Water Tech spawning override failed as no blocks are present on modified spawning Tech.  Please report this.");
                        return;
                    }
                    DebugTAC_AI.Log(KickStart.ModID + ":  Tech " + TSP.m_TechToSpawn.Name + " landed in water and was likely not water-capable, naval Tech " + newTech.Name + " was substituted for the spawn instead");
                    TSP.m_TechToSpawn = newTech;
                    AIWiki.hintShip.Show();
                }
                else
                {
                    SpawnBaseTypes type = RawTechLoader.GetEnemyBaseType(RTF);
                    if (type != SpawnBaseTypes.NotAvail && !RawTechLoader.IsFallback(type))
                    {
                        newTech = RawTechLoader.GetUnloadedTech(type, TSP.m_Team, true, out _);
                        if (newTech == null)
                        {
                            DebugTAC_AI.Exception("Water Tech spawning override(PREFAB) failed as fetched TechData is null.  Please report this.");
                            return;
                        }
                        if (newTech.m_BlockSpecs == null)
                        {
                            DebugTAC_AI.Exception("Water Tech spawning override(PREFAB) failed as fetched TechData's block info is null.  Please report this.");
                            return;
                        }
                        if (newTech.m_BlockSpecs.Count == 0)
                        {
                            DebugTAC_AI.Exception("Water Tech spawning override(PREFAB) failed as no blocks are present on modified spawning Tech.  Please report this.");
                            return;
                        }
                        DebugTAC_AI.Log(KickStart.ModID + ":  Tech " + TSP.m_TechToSpawn.Name + " landed in water and was likely not water-capable, naval Tech " + newTech.Name + " was substituted for the spawn instead");

                        TSP.m_TechToSpawn = newTech;
                    }
                    // Else we don't do anything.
                }
            }
            catch (Exception e)
            {
                DebugTAC_AI.Log(KickStart.ModID + ":  Attempt to swap sea tech failed! - " + e);
                throw e;
            }
        }



        private static void TrySpawnAirborneAIInAir()
        {   //  Spawns airborneAI even when the parts required aren't available, but they will not
            //      attack unless provoked by the player or another enemy, which is unlikely.
            // MAKE SURE licences are grabbed!!!
            Licences = Singleton.Manager<ManLicenses>.inst;
            if (Licences.IsNull() && ManGameMode.inst.IsCurrentModeCampaign())
            {   // The game tried to enable creative spawns whilist no licences were active!?!?
                DebugTAC_AI.Log(KickStart.ModID + ": TrySpawnAirborneAIInAir - It's campaign mode but no licences were found?!?");
                return;
            }

            // Get the air spawn origin
            Tank SpawnOrigin = playerTank;
            if (ManNetwork.IsNetworked)
            {   // try getting another player 
               int randIndex = UnityEngine.Random.Range(0, ManNetwork.inst.GetNumPlayers());
               NetPlayer NP = ManNetwork.inst.GetPlayer(randIndex);
                SpawnOrigin = NP.CurTech.tech;
            }

            if (SpawnOrigin.IsNull() || AIGlobals.AtSceneTechMaxSpawnLimit())
                return;
            if (AirPool.Count >= MaxAirborneAIAllowed)
                return;

            Vector3 pos;
            if (SpawnOrigin.rbody.IsNotNull())
                pos = (SpawnOrigin.rbody.velocity * Time.deltaTime * 5) + SpawnOrigin.boundsCentreWorldNoCheck;
            else
                pos = SpawnOrigin.boundsCentreWorldNoCheck;

            Vector3 forwards = GetRandAirAngle();

            pos = GetAirOffsetFromPosition(pos, forwards);
            if (!ManWorld.inst.TileManager.IsTileAtPositionLoaded(pos))
                return; //DO NOT SPAWN OUT OF BOUNDS.  Since this spawn is not mandatory, we can hold off.


            Tank newAirborneAI;
            bool spawnSpace;
            if (SpawnOrigin.boundsCentreWorld.y > SpaceBeginAltitude)
            {
                spawnSpace = true;
            }
            else
                spawnSpace = UnityEngine.Random.Range(0, 1f) < SpaceSpawnChance;

            bool IsSpace = false;
            if (CreativeMode)
            {
                RawTechPopParams RTF = RawTechPopParams.Default;
                RTF.Faction = FactionSubTypes.NULL;
                RTF.Offset = RawTechOffset.OffGround60Meters;
                if (spawnSpace)
                {
                    RTF.Terrain = BaseTerrain.Space;
                    newAirborneAI = RawTechLoader.SpawnRandomTechAtPosHead(pos, forwards, EnemyTeam, RTF, true);
                    IsSpace = true;
                }
                else
                {
                    RTF.Terrain = BaseTerrain.Air;
                    newAirborneAI = RawTechLoader.SpawnRandomTechAtPosHead(pos, forwards, EnemyTeam, RTF, true);
                }
            }
            else
            {
                if (spawnSpace)
                {
                    newAirborneAI = SpawnPrefabSpaceship(pos, forwards, out bool worked);
                    if (worked)
                        IsSpace = true;
                    else
                        newAirborneAI = SpawnPrefabAircraft(pos, forwards);
                }
                else
                    newAirborneAI = SpawnPrefabAircraft(pos, forwards);
            }
            if (newAirborneAI == null)
            {
                //DebugTAC_AI.Log(KickStart.ModID + ": SpecialAISpawner - Could not spawn airborneAI - Player has no corps unlocked!?!");
                return;
            }
            TrackedAirborneAI newAir = new TrackedAirborneAI(newAirborneAI, IsSpace);
            AirPool.Add(newAir);
        }


        public static Dictionary<FactionSubTypes, int> AirAggressionGrades = new Dictionary<FactionSubTypes, int>()
        {
            { FactionSubTypes.GSO, 3},
            { FactionSubTypes.VEN, 1},
            { FactionSubTypes.HE, 1},
            { FactionSubTypes.BF, 0},
        };
        private static bool ShouldBePassive(FactionSubTypes spawnFaction)
        {
            if (AirAggressionGrades.TryGetValue(spawnFaction, out int val))
                return Licences.GetLicense(FactionSubTypes.GSO).CurrentLevel < val;
            return Licences.GetLicense(FactionSubTypes.GSO).CurrentLevel < 4;
        }
        /// <summary>
        /// CAN RETURN NULL
        /// </summary>
        private static Tank SpawnPrefabAircraft(Vector3 pos, Vector3 forwards)
        {   // 
            RawTechPopParams RTF;
            try
            {
                UpdateFactionsAvailAir();
                if (factionsAvail.Count == 0)
                    return null;

                bool hasAllDone = true;
                if (factionsAvail.Count > 5)
                {
                    foreach (FactionSubTypes faction in factionsAvail)
                    {
                        if (!Licences.GetLicense(faction).HasReachedMaxLevel)
                        {
                            hasAllDone = false;
                            break;
                        }
                    }
                }
                else
                    hasAllDone = false;

                // spawn and return the airborneAI
                if (hasAllDone) // all corps unlocked by player
                {
                    RTF = RawTechPopParams.Default;
                    RTF.Terrain = BaseTerrain.Air;
                    RTF.Offset = RawTechOffset.OffGround60Meters;
                    RTF.MaxPrice = KickStart.EnemySpawnPriceMatching;
                    return RawTechLoader.SpawnRandomTechAtPosHead(pos, forwards, EnemyTeam, RTF, true);
                }

                // if we don't have all corps possible maxed, we do the normal spawn

                // determine corp
                factionsAvail.Shuffle();
                FactionSubTypes finalFaction = factionsAvail.FirstOrDefault();

                bool unProvoked = ShouldBePassive(finalFaction);

                try
                {
                    Singleton.Manager<UIMPChat>.inst.AddMissionMessage("<b>Unidentified flying object spotted!</b>");
                }
                catch { }
                DebugTAC_AI.Log(KickStart.ModID + ": There are now " + (AirPool.Count + 1) + " airborneAI present on-scene");
                RTF = RawTechPopParams.Default;
                RTF.Faction = finalFaction;
                RTF.TargetFactionGrade = Licences.GetLicense(finalFaction).CurrentLevel;
                RTF.MaxPrice = KickStart.EnemySpawnPriceMatching;
                RTF.Offset = RawTechOffset.OffGround60Meters;
                RTF.Terrain = BaseTerrain.Air;
                RTF.Disarmed = unProvoked;
                if (unProvoked)
                {
                    if (RawTechLoader.SpawnRandomTechAtPosHead(pos, forwards, EnemyTeam, out Tank finalTank, RTF))
                    {
                        AIWiki.hintAir.Show();
                        if (unProvoked)
                            AIWiki.hintAirSafe.Show();
                        else
                            AIWiki.hintAirWarning.Show();
                        return finalTank;
                    }
                    else
                        return null;
                }
                // else we do default spawn
                return RawTechLoader.SpawnRandomTechAtPosHead(pos, forwards, EnemyTeam, RTF, true);
            }
            catch { }
            DebugTAC_AI.Log(KickStart.ModID + ": SpecialAISpawner - Could not fetch corps, resorting to random spawns");
            RTF = RawTechPopParams.Default;
            RTF.Terrain = BaseTerrain.Air;
            RTF.Offset = RawTechOffset.OffGround60Meters;
            RTF.MaxPrice = KickStart.EnemySpawnPriceMatching;
            return RawTechLoader.SpawnRandomTechAtPosHead(pos, forwards, EnemyTeam, RTF, true);
        }

        /// <summary>
        /// CAN RETURN NULL
        /// </summary>
        private static Tank SpawnPrefabSpaceship(Vector3 pos, Vector3 forwards, out bool worked)
        {   // 
            worked = false;
            RawTechPopParams RTF;
            try
            {
                UpdateFactionsAvailAir();
                if (factionsAvail.Count == 0)
                    return null;

                bool hasAllDone = true;
                if (factionsAvail.Count > 5)
                {
                    foreach (FactionSubTypes faction in factionsAvail)
                    {
                        if (!Licences.GetLicense(faction).HasReachedMaxLevel)
                            hasAllDone = false;
                    }
                }
                else
                    hasAllDone = false;

                // spawn and return the airborneAI
                if (hasAllDone) // all corps unlocked by player
                {
                    RTF = RawTechPopParams.Default;
                    RTF.Terrain = BaseTerrain.Air;
                    RTF.Offset = RawTechOffset.OffGround60Meters;
                    RTF.MaxPrice = KickStart.EnemySpawnPriceMatching;
                    return RawTechLoader.SpawnRandomTechAtPosHead(pos, forwards, EnemyTeam, RTF, true);
                }

                // if we don't have all corps possible maxed, we do the normal spawn

                // determine corp
                factionsAvail.Shuffle();
                FactionSubTypes finalFaction = factionsAvail.FirstOrDefault();

                bool unProvoked = ShouldBePassive(finalFaction);

                try
                {
                    Singleton.Manager<UIMPChat>.inst.AddMissionMessage("<b>HUGE unidentified flying object spotted!</b>");
                }
                catch { }
                DebugTAC_AI.Log(KickStart.ModID + ": There are now " + (AirPool.Count + 1) + " airborneAI present on-scene");

                RTF = RawTechPopParams.Default;
                RTF.Faction = finalFaction;
                RTF.TargetFactionGrade = Licences.GetLicense(finalFaction).CurrentLevel;
                RTF.MaxPrice = KickStart.EnemySpawnPriceMatching;
                RTF.Offset = RawTechOffset.OffGround60Meters;
                RTF.Terrain = BaseTerrain.Space;
                RTF.Disarmed = unProvoked;
                worked = RawTechLoader.SpawnRandomTechAtPosHead(pos, forwards, AIGlobals.GetRandomBaseTeam(), out Tank tech, RTF);
                if (worked)
                {
                    AIWiki.hintSpace.Show();
                    if (unProvoked)
                        AIWiki.hintSpaceSafe.Show();
                    else
                        AIWiki.hintSpaceWarning.Show();
                }
                return tech;
            }
            catch { }
            DebugTAC_AI.Log(KickStart.ModID + ": SpecialAISpawner - Could not fetch corps, resorting to random spawns");
            worked = true;
            RTF = RawTechPopParams.Default;
            RTF.Terrain = BaseTerrain.Space;
            RTF.Offset = RawTechOffset.OffGround60Meters;
            RTF.MaxPrice = KickStart.EnemySpawnPriceMatching;
            return RawTechLoader.SpawnRandomTechAtPosHead(pos, forwards, AIGlobals.GetRandomBaseTeam(), RTF, true);
        }

        public static void TrySpawnTraderTroll(Vector3 pos)
        {   // Spawn trader trolls to make bigger techs fight harder

            DebugTAC_AI.Log(KickStart.ModID + ": TrySpawnTraderTroll - Queued request at " + pos + "!");
            if (ManNetwork.IsNetworked && !ManNetwork.IsHost)
                return;

            if (UnityEngine.Random.Range(-50, 150) > KickStart.Difficulty)
                return;

            if (!AIEBases.IsLocationGridEmpty(pos, AIGlobals.defaultExpandRad))
                return;

            RawTechPopParams RTF;
            try
            {
                UpdateFactionsAvailLand();
                if (factionsAvail.Count == 0)
                    return;
                FactionSubTypes factionSelect = factionsAvail.GetRandomEntry();

                //pos = GetOffsetPosAngle(pos); 

                if (!AIEBases.TryFindExpansionLocationGrid(pos, pos + (UnityEngine.Random.insideUnitCircle.ToVector3XZ() * 128), out Vector3 pos3))
                    return;
                bool spawned = false;
                int licence = Licences.GetLicense(factionSelect).CurrentLevel;
                if (AIGlobals.EnemyBaseMakerChance >= UnityEngine.Random.Range(0, 100))
                {
                    int team = AIGlobals.GetRandomEnemyBaseTeam();
                    RawTechLoader.TrySpawnBaseAtPositionNoFounder(factionSelect, pos3, team,
                        BasePurpose.AnyNonHQ, licence);
                    if (AIEBases.TryFindExpansionLocationGrid(pos3, pos3 + new Vector3(0, 0, 64), out Vector3 pos4))
                    {
                        RawTechLoader.TrySpawnBaseAtPositionNoFounder(factionSelect, pos3, team,
                            BasePurpose.NotStationary, licence);
                    }
                }
                else
                {
                    if (1 >= UnityEngine.Random.Range(0, 2f))
                    {   // Spawn harvest tech
                        RTF = RawTechPopParams.Default;
                        RTF.Faction = factionSelect;
                        RTF.TargetFactionGrade = licence;
                        RTF.MaxPrice = KickStart.EnemySpawnPriceMatching;
                        RTF.IsPopulation = true;
                        RTF.Purposes = new HashSet<BasePurpose> { BasePurpose.Harvesting, BasePurpose.NotStationary };

                        if (RawTechLoader.TrySpawnSpecificTechSafe(pos3, Vector3.forward, trollTeam, RTF))
                            spawned = true;
                    }
                    else // Spawn turret lol
                    {
                        RTF = RawTechPopParams.Default;
                        RTF.Faction = factionSelect;
                        RTF.TargetFactionGrade = licence;
                        RTF.MaxPrice = KickStart.EnemySpawnPriceMatching;
                        RTF.IsPopulation = true;
                        RTF.Purposes = new HashSet<BasePurpose> { BasePurpose.Defense };

                        if (RawTechLoader.TrySpawnSpecificTechSafe(pos3, Vector3.forward, trollTeam, RTF))
                            spawned = true;
                    }
                }

                if (spawned)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": TrySpawnTraderTroll - Spawned!");
                    try
                    {
                        Singleton.Manager<UIMPChat>.inst.AddMissionMessage("<b>Trader Troll ahead!</b>");
                    }
                    catch { }
                }
                else
                    DebugTAC_AI.Log(KickStart.ModID + ": TrySpawnTraderTroll - It is likely too early and we could not find a good canidate");

                return;
            }
            catch { }
            DebugTAC_AI.Log(KickStart.ModID + ": TrySpawnTraderTroll - Could not fetch corps, resorting to random spawns");

            if (!AIEBases.TryFindExpansionLocationGrid(pos, pos + (UnityEngine.Random.insideUnitCircle.ToVector3XZ() * 128), out Vector3 pos2))
                return;
            RTF = RawTechPopParams.Default;
            RTF.Faction = FactionSubTypes.NULL;
            RTF.IsPopulation = true;
            RTF.Purposes = new HashSet<BasePurpose> { BasePurpose.Defense };

            if (RawTechLoader.TrySpawnSpecificTechSafe(pos2, Vector3.forward, trollTeam, RTF))
            {

                DebugTAC_AI.Log(KickStart.ModID + ": TrySpawnTraderTroll - Spawned!");
                try
                {
                    Singleton.Manager<UIMPChat>.inst.AddMissionMessage("<b>Trader Troll ahead!</b>");
                }
                catch { }
            }
            else // We failed so we do nothing
                DebugTAC_AI.Log(KickStart.ModID + ": TrySpawnTraderTroll - It is likely too early and we could not find a good canidate");
        }

        private static void ManagePooledAIs()
        {   // 
            ManagePooledEradicators();
            ManagePooledAirborneAI();
            ManageBaseTeamAI();
        }
        private static void ManagePooledEradicators()
        {   // 
            int count = Eradicators.Count();
            if (count > 0)
            {
                for (int step = 0; count > step; step++)
                {
                    try
                    {
                        Tank erad = Eradicators[step];
                        if (!erad.visible.isActive)
                        {
                            Eradicators.Remove(erad);
                            step--;
                            count--;
                        }
                    }
                    catch { }
                }
            }
        }
        private static void ManagePooledAirborneAI()
        {   // 
            if (!KickStart.AllowAirEnemiesToSpawn || !ManPop.inst.IsSpawningEnabled)
            {
                DestroyAllPooledAirborneAI(KickStart.AllowAirEnemiesToSpawn);
            }
            int count = AirPool.Count();
            int deadairborneAICount = 0;
            for (int step = 0; count > step; step++)
            {
                try
                {
                    if (AirPool[step] == null)
                    {
                        AirPool.RemoveAt(step);
                        DebugTAC_AI.Info(KickStart.ModID + ": SpecialAISpawner - Removed and recycled a tech from AirPool as it was NULL.");
                        step--;
                        count--;
                        deadairborneAICount++;
                        continue;
                    }
                    Tank airborneAI = AirPool[step].tank;
                    //float sqrMag = (airborneAI.boundsCentreWorldNoCheck - playerTank.boundsCentreWorldNoCheck).sqrMagnitude;
                    if (airborneAI.IsNull() || !airborneAI.visible.isActive)
                    {
                        AirPool.RemoveAt(step);
                        DebugTAC_AI.Info(KickStart.ModID + ": SpecialAISpawner - Removed and recycled " + airborneAI.name + " from AirPool as it may have despawned.");
                        step--;
                        count--;
                        deadairborneAICount++;
                    }
                    else if (airborneAI.IsSleeping)
                    {
                        airborneAI.SetSleeping(false);
                        DebugTAC_AI.Info(KickStart.ModID + ": SpecialAISpawner - Awakened " + airborneAI.name + " in AirPool as it froze.");
                    }
                    else if (AIGlobals.IsBaseTeamDynamic(airborneAI.Team) && AIGlobals.SceneTechMaxNeedsRemoval(out int remove))
                    {
                        AirPool.RemoveAt(step);
                        DebugTAC_AI.Info(KickStart.ModID + ": SpecialAISpawner - Removed and recycled " + airborneAI.name + " from AirPool as we have bypassed the max tech limit.");
                        Purge(airborneAI);
                        step--;
                        count--;
                    }
                    else if (airborneAI.trans.position.y > AIGlobals.AirNPTMaxHeightOffset + Singleton.playerPos.y)
                    {
                        AirPool.RemoveAt(step);
                        DebugTAC_AI.Info(KickStart.ModID + ": SpecialAISpawner - Removed and recycled " + airborneAI.name + " from AirPool as it flew above player distance.");
                        Purge(airborneAI);
                        step--;
                        count--;
                    }
                    else if ((airborneAI.boundsCentreWorldNoCheck - playerTank.boundsCentreWorldNoCheck).sqrMagnitude > AirDespawnDist * AirDespawnDist)
                    {
                        AirPool.RemoveAt(step);
                        DebugTAC_AI.Info(KickStart.ModID + ": SpecialAISpawner - Removed and recycled " + airborneAI.name + " from AirPool as it left AirDespawnDist radius.");
                        Purge(airborneAI);
                        step--;
                        count--;
                    }
                }
                catch (Exception e)
                {
                    DebugTAC_AI.Info(KickStart.ModID + ": SpecialAISpawner - Removed and recycled a tech from AirPool as it was errored. " + e);
                    AirPool.RemoveAt(step);
                    step--;
                    count--;
                    deadairborneAICount++;
                }
            }
            if (deadairborneAICount > 0)
                DebugTAC_AI.Log(KickStart.ModID + ": SpecialAISpawner - Removed " + deadairborneAICount + " dead airborneAI(s) from AirPool");
        }
        private static void DestroyAllPooledAirborneAI(bool onlyPopulation)
        {   // 
            if (AirPool.Count == 0)
                return;
            if (onlyPopulation)
            {
                for (int step = AirPool.Count - 1; step > 0; step--)
                {
                    var airborneAI = AirPool.ElementAt(step);
                    if (airborneAI.tank.IsNotNull())
                    {
                        if (AIGlobals.TechIsSafelyRemoveable(airborneAI.tank))
                        {
                            Purge(airborneAI.tank);
                            AirPool.RemoveAt(step);
                        }
                    }
                    else
                        AirPool.RemoveAt(step);
                }
            }
            else
            {
                foreach (TrackedAirborneAI airborneAI in AirPool)
                {
                    if (airborneAI.tank.IsNotNull())
                        Purge(airborneAI.tank);
                }
                AirPool.Clear();
                DebugTAC_AI.Log(KickStart.ModID + ": SpecialAISpawner - Destroyed all non-mission pooled airborneAI");
            }
        }
        private static void CollectPossibleAirborneAI()
        {   // 
            foreach (Tank tech in Singleton.Manager<ManTechs>.inst.IterateTechsWhere(x => x && x.GetComponent<TankAIHelper>()
            && x.IsPopulation))
            {
                var em = tech.GetComponent<EnemyMind>();
                if (em)
                {
                    if (tech.GetComponent<TankAIHelper>().MovementController is AIControllerAir || 
                        em.EvilCommander == EnemyHandling.Starship)
                    {
                        try
                        {
                            TrackedAirborneAI newAir = new TrackedAirborneAI(tech);
                            AirPool.Add(newAir);
                        }
                        catch
                        {
                            DebugTAC_AI.Log(KickStart.ModID + ": SpecialAISpawner - Error on handling enemy airborne AI pool");
                        }
                    }
                }
            }
        }

        private static List<Tank> techsTracker = new List<Tank>();
        private static void ManageBaseTeamAI()
        {   // 
            if (AIGlobals.SceneTechMaxNeedsRemoval(out int removal))
            {
                techsTracker.Clear();
                foreach (var item in ManTechs.inst.IterateEnemyTechs())
                {
                    techsTracker.Add(item);
                }
                int count = techsTracker.Count();
                int removedCount = 0;
                for (int step = 0; count > step; step++)
                {
                    try
                    {
                        Tank tank = techsTracker[step];
                        float sqrMag = (tank.boundsCentreWorldNoCheck - playerTank.boundsCentreWorldNoCheck).sqrMagnitude;
                        if (AIGlobals.TechIsSafelyRemoveable(tank))
                        {
                            DebugTAC_AI.Info(KickStart.ModID + ": SpecialAISpawner - Removed and recycled " + tank.name + 
                                " from world as we have bypassed the max tech limit.");
                            Purge(tank);
                            removal--;
                            if (removal == 0)
                                break;
                            step--;
                            count--;
                            removedCount++;
                        }
                    }
                    catch
                    {
                        step--;
                        count--;
                        removedCount++;
                    }
                }
                if (removedCount > 0)
                    DebugTAC_AI.Log(KickStart.ModID + ": SpecialAISpawner - Removed " + removedCount + " managed Tech(s) from active population");
            }
        }



        /// <summary>
        /// Remove some blocks on spawn
        /// </summary>
        /// <param name="tech"></param>
        /// <param name="percent"></param>
        public static void InflictPercentDamage(TechData tech, float percent)
        {
            int curCount = tech.m_BlockSpecs.Count;
            int toKeep = Mathf.CeilToInt(curCount * percent);
            for (int step = curCount - 1; step < toKeep; step--)
            {
                tech.m_BlockSpecs.RemoveAt(step);
            }
            DebugTAC_AI.Log(KickStart.ModID + ": InflictPercentDamage target " + tech.Name + " removed " + (curCount * percent) + "!");
        }


        /// <summary>
        /// Remove a Tech from existance
        /// </summary>
        /// <param name="tech"></param>
        /// <param name="player"></param>
        internal static void Purge(Tank tech)
        {   // 
            if (ManNetwork.IsNetworked)
            {
                PurgeHost(tech.visible.ID, tech.name);
            }
            else
            {
                if (!PurgeHost(tech.visible.ID, tech.name))
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Purge - Trying to Purge by visible " + tech.name);
                    tech.visible.RemoveFromGame();
                }
            }
        }
        /// <summary>
        /// Remove a Tech from existance
        /// </summary>
        /// <param name="tech"></param>
        /// <param name="player"></param>
        internal static bool PurgeHost(int HostVisibleID, string name)
        {   // 
            if (!ManNetwork.IsHost)
                throw new Exception(KickStart.ModID + ": SpecialAISpawner.PurgeHost called on non-host");
            DebugTAC_AI.Info(KickStart.ModID + ": PurgeHost - Name " + name +  " | " + HostVisibleID + "  Callstack: " + StackTraceUtility.ExtractStackTrace());
            if (ManNetwork.IsNetworked)
            {
                try
                {
                    TrackedVisible TV = ManVisible.inst.GetTrackedVisibleByHostID(HostVisibleID);
                    Singleton.Manager<ManNetwork>.inst.SendToServer(TTMsgType.UnspawnTech, new UnspawnTechMessage
                    {
                        m_HostID = TV.HostID,
                        m_CheatBypassInventory = true,
                    }
                    );
                    DebugTAC_AI.Log(KickStart.ModID + ": Purge - PURGED " + name + " (MP)");
                    AIGlobals.SceneTechCount = -1;
                    return true;
                }
                catch (Exception e)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Purge - Failed to purge " + name + " (MP)");
                    foreach (var item in new List<TrackedVisible>(ManVisible.inst.AllTrackedVisibles))
                    {
                        if (item == null)
                            continue;
                        if (item.ObjectType == ObjectTypes.Vehicle)
                        {
                            if (ManWorld.inst.TileManager.IsTileAtPositionLoaded(item.Position))
                            {
                                if (item.wasDestroyed || item.visible == null)
                                {
                                    if (AIGlobals.IsBaseTeamDynamic(item.TeamID))
                                    {
                                        DebugTAC_AI.Log("  Invalid Base Team Tech visible " + item.ID + ",  Team " + item.TeamID + ",  Destroyed " + item.wasDestroyed);
                                        ManVisible.inst.StopTrackingVisible(item.ID);
                                    }
                                }
                            }
                        }
                    }
                    /*
                    foreach (var item in new List<TrackedVisible>(ManVisible.inst.AllTrackedVisibles))
                    {
                        if (item != null && item.visible == null && item.ObjectType == ObjectTypes.Vehicle
                            && ManWorld.inst.TileManager.IsTileAtPositionLoaded(item.Position))
                        {
                            if (item.wasDestroyed)
                            {
                                if (AIGlobals.IsBaseTeam(item.TeamID))
                                {
                                    DebugTAC_AI.Log("  Invalid Base Team Tech visible " + item.ID + ",  Team " + item.TeamID + ",  Destroyed " + item.wasDestroyed);
                                    ManVisible.inst.StopTrackingVisible(item.ID);
                                }
                                else
                                    DebugTAC_AI.Log("  Invalid Tech visible " + item.ID + ",  Team " + item.TeamID + ",  Destroyed " + item.wasDestroyed);
                            }
                            else
                                DebugTAC_AI.Log("  Not Destroyed Tech visible " + item.ID + ",  Team " + item.TeamID + ",  Destroyed " + item.wasDestroyed);
                        }
                        else
                            DebugTAC_AI.Log("  Other Tech visible " + item.ID + ",  Team " + item.TeamID + ",  Destroyed " + item.wasDestroyed);
                    }*/
                    DebugTAC_AI.Log(KickStart.ModID + ": Purge - Error backtrace - " + e);
                }
            }
            else
            {
                try
                {
                    TrackedVisible TV = ManVisible.inst.GetTrackedVisible(HostVisibleID);
                    if (TV != null)
                    {
                        ManVisible.inst.ObliterateTrackedVisibleFromWorld(TV);
                        DebugTAC_AI.Log(KickStart.ModID + ": Purge - PURGED " + name);
                    }
                    else
                    {
                        DebugTAC_AI.Assert(KickStart.ModID + ": Purge - failed to purge visible!!!!");
                    }
                    AIGlobals.SceneTechCount = -1;
                    return true;
                }
                catch (Exception e)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Purge - Failed to purge " + name + " (SINGLE player)");
                    foreach (var item in new List<TrackedVisible>(ManVisible.inst.AllTrackedVisibles))
                    {
                        if (item == null)
                            continue;
                        if (item.ObjectType == ObjectTypes.Vehicle)
                        {
                            if (ManWorld.inst.TileManager.IsTileAtPositionLoaded(item.Position))
                            {
                                if (item.wasDestroyed || item.visible == null)
                                {
                                    if (AIGlobals.IsBaseTeamDynamic(item.TeamID))
                                    {
                                        DebugTAC_AI.Log("  Invalid Base Team Tech visible " + item.ID + ",  Team " + item.TeamID + ",  Destroyed " + item.wasDestroyed);
                                        ManVisible.inst.StopTrackingVisible(item.ID);
                                    }
                                }
                            }
                        }
                    }
                    /*
                    foreach (var item in new List<TrackedVisible>(ManVisible.inst.AllTrackedVisibles))
                    {
                        if (item != null && item.visible == null && item.ObjectType == ObjectTypes.Vehicle
                            && ManWorld.inst.TileManager.IsTileAtPositionLoaded(item.Position))
                        {
                            if (item.wasDestroyed)
                            {
                                if (AIGlobals.IsBaseTeam(item.TeamID))
                                {
                                    DebugTAC_AI.Log("  Invalid Base Team Tech visible " + item.ID + ",  Team " + item.TeamID + ",  Destroyed " + item.wasDestroyed);
                                    ManVisible.inst.StopTrackingVisible(item.ID);
                                }
                                else
                                    DebugTAC_AI.Log("  Invalid Tech visible " + item.ID + ",  Team " + item.TeamID + ",  Destroyed " + item.wasDestroyed);
                            }
                            else
                                DebugTAC_AI.Log("  Not Destroyed Tech visible " + item.ID + ",  Team " + item.TeamID + ",  Destroyed " + item.wasDestroyed);
                        }
                        else
                            DebugTAC_AI.Log("  Other Tech visible " + item.ID + ",  Team " + item.TeamID + ",  Destroyed " + item.wasDestroyed);
                    }*/
                    DebugTAC_AI.Log(KickStart.ModID + ": Purge - Error backtrace - " + e);
                }
            }
            return false;
        }
        /// <summary>
        /// Remove a Tech from existance the cool way
        /// </summary>
        /// <param name="tech"></param>
        /// <param name="player"></param>
        internal static void Eradicate(Tank tech)
        {   // 
           if (ManNetwork.IsNetworked)
            {
                try
                {
                    TrackedVisible TV = ManVisible.inst.GetTrackedVisibleByHostID(tech.netTech.HostID);
                    Singleton.Manager<ManNetwork>.inst.SendToServer(TTMsgType.UnspawnTech, new UnspawnTechMessage
                    { 
                        m_HostID = TV.HostID,
                        m_CheatBypassInventory = true,
                    }
                    );
                }
                catch { }
            }
            else
            {
                if ((Singleton.playerPos - tech.boundsCentreWorld).sqrMagnitude > AIGlobals.EradicateEffectMaxDistanceSqr)
                {
                    Purge(tech);
                    return;
                }
                foreach (TankBlock block in tech.blockman.IterateBlocks())
                {
                    try
                    {
                        if (!block.damage.AboutToDie)
                            block.damage.SelfDestruct(0.5f);
                    }
                    catch { }
                }
                tech.blockman.Disintegrate();
            }
        }

        private static void Resume()
        {   // 
            if (!thisActive)
            {
                Licences = Singleton.Manager<ManLicenses>.inst;
                inst.counter = 0;
                UpdatePlayerTank();
                Singleton.Manager<ManTechs>.inst.PlayerTankChangedEvent.Subscribe(UpdatePlayerTank);
                Singleton.Manager<ManTechs>.inst.TankDestroyedEvent.Subscribe(PlayerTankDeathCheck);
                Singleton.Manager<ManPauseGame>.inst.PauseEvent.Subscribe(inst.OnPaused);
                inst.OnPaused(ManPauseGame.inst.IsPaused);
                inst.gameObject.SetActive(true);
                DebugTAC_AI.Log(KickStart.ModID + ": SpecialAISpawner - Activated special enemy spawns");
                thisActive = true;
            }
            CollectPossibleAirborneAI();
        }
        private static void Pause()
        {   // 
            if (thisActive)
            {
                inst.gameObject.SetActive(false);
                Singleton.Manager<ManPauseGame>.inst.PauseEvent.Unsubscribe(inst.OnPaused);
                Singleton.Manager<ManTechs>.inst.PlayerTankChangedEvent.Unsubscribe(UpdatePlayerTank);
                Singleton.Manager<ManTechs>.inst.TankDestroyedEvent.Unsubscribe(PlayerTankDeathCheck);
                inst.counter = 0;
                Licences = null;
                DebugTAC_AI.Log(KickStart.ModID + ": SpecialAISpawner - Deactivated special enemy spawns");
                thisActive = false;
            }
        }
        private void OnPaused(bool state)
        {   // 
            enabled = !state;
        }
        public void Update()
        {   // 
            //DebugTAC_AI.Log(KickStart.ModID + ": SpecialAISpawner - ACTIVE!!!  time" + counter);
            if (Singleton.Manager<ManPop>.inst.IsSpawningEnabled || forceOn)
            {   // determine if we should spawn new one, also manage existing pooled airborneAIs
                bool doubleSpawnRate = false;
                try
                {
                    doubleSpawnRate = Singleton.cameraTrans.position.y > AIGlobals.AirPromoteSpaceHeight;
                }
                catch { }
                if (counter > (AirSpawnInterval / (doubleSpawnRate ? 2 : 1)) / ((KickStart.Difficulty / 100) + 1.5f))
                {
                    //DebugTAC_AI.Log(KickStart.ModID + ": SpecialAISpawner - Spawn lerp");
                    if (KickStart.EnableBetterAI && KickStart.enablePainMode && !AIGlobals.AtSceneTechMaxSpawnLimit())
                    {
                        if (KickStart.AllowAirEnemiesToSpawn && UnityEngine.Random.Range(0, AirborneSpawnChance) < AirborneAISpawnOdds)
                            TrySpawnAirborneAIInAir();
                        if (KickStart.CommitDeathMode)
                        { // endless enemy havoc
                            try
                            {
                                Singleton.Manager<ManPop>.inst.DebugForceSpawn();
                            }
                            catch { }
                        }
                    }
                    counter = 0;
                }
            }
            if (updateTimer > 25)
            {   // manager timer
                ManagePooledAIs();
                updateTimer = 0;
            }
            counter += Time.deltaTime;
            updateTimer++;
        }


        // Utilities
        private static Vector3 GetOffsetPosAngle(Vector3 pos)
        {   // 
            float randAngle = UnityEngine.Random.Range(0, 360);
            Vector3 angleHeading = Quaternion.AngleAxis(randAngle, Vector3.up) * Vector3.forward;
            return pos + (angleHeading * 64);
        }
        private static Vector3 GetRandAirAngle()
        {   // 
            float randAngle = UnityEngine.Random.Range(0, 360);
            Vector3 angleHeading = Quaternion.AngleAxis(randAngle, Vector3.up) * Vector3.forward;
            return angleHeading;
        }
        private static Vector3 GetAirOffsetFromPosition(Vector3 pos, Vector3 angleHeading)
        {   // 
            return AI.Movement.AIEPathing.OffsetFromGroundAAlt(pos + -(angleHeading * AirSpawnDist), 75);
        }




        private static HashSet<string> SelectedSpawns => BaseGamePopSpecials.SelectedSpawns;
        private static HashSet<TechData> BaseGameTechPool = new HashSet<TechData>();
        private static FieldInfo spawnPoolBaseGame = typeof(ManPop).GetField("m_PopTypeRuntimes", BindingFlags.NonPublic | BindingFlags.Instance);
        private static Type hidddden = typeof(ManPop).GetNestedType("PopTypeRuntime", BindingFlags.NonPublic | BindingFlags.CreateInstance | BindingFlags.Instance);
        private static FieldInfo spawnable = hidddden.GetField("m_CachedPresets", BindingFlags.Public | BindingFlags.Instance);

        public static void InsureGrabBaseGameSpawns()
        {
            if (BaseGameTechPool.Any() || ManGameMode.inst.GetCurrentGameType() == ManGameMode.GameType.Attract)
                return;
            Array sek = (Array)spawnPoolBaseGame.GetValue(ManPop.inst);
            foreach (var item in sek)
            {
                List<PresetInfo> PIs = (List<PresetInfo>)spawnable.GetValue(item);
                if (PIs == null)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Found null pool");
                    continue;
                }
                DebugTAC_AI.Log(KickStart.ModID + ": Found 1 pool with " + PIs.Count + " techs(s) in it");
                foreach (var item2 in PIs)
                {
                    if (item2 == null)
                        continue;
                    TechData TD = item2.TechData;
                    if (TD != null)
                        BaseGameTechPool.Add(TD);
                }
            }
        }


        /// <summary>
        /// EXTREMELY EXPENSIVE - Only call on the complier, not the client computer!!!
        /// </summary>
        public static void GatherAllPotentialTechsForPool()
        {
            InsureGrabBaseGameSpawns();
            DebugTAC_AI.Log("---------------- EXPANDED POOL FROM BASE GAME: ----------------");
            foreach (var item in BaseGameTechPool)
            {
                try
                {
                    RawTechTemplate RTT = new RawTechTemplate(item);
                    DebugTAC_AI.Log("\"" + item.Name + "\" - " + RTT.terrain + ",  " + RTT.faction + ",  " + RTT.baseCost + ",  " + RTT.IntendedGrade);
                }
                catch { }
            }
            DebugTAC_AI.Log("---------------------- END EXPANDED POOL ----------------------");
        }
        /// <summary>
        /// EXTREMELY EXPENSIVE - Only call on the complier, not the client computer!!!
        /// </summary>
        public static void GatherAllPotentialNonLandForPool()
        {
            InsureGrabBaseGameSpawns();
            DebugTAC_AI.Log("---------------- EXPANDED POOL FROM BASE GAME: ----------------");
            foreach (var item in BaseGameTechPool)
            {
                try
                {
                    RawTechTemplate RTT = new RawTechTemplate(item);
                    if (RTT.terrain != BaseTerrain.Land)
                    {
                        DebugTAC_AI.Log("\"" + item.Name + "\",");
                    }
                }
                catch { }
            }
            DebugTAC_AI.Log("---------------------- END EXPANDED POOL ----------------------");
        }
        /// <summary>
        /// EXTREMELY EXPENSIVE - Only call on the complier, not the client computer!!!
        /// </summary>
        public static void GatherAllPotentialAircraftsForPool()
        {
            InsureGrabBaseGameSpawns();
            DebugTAC_AI.Log("---------------- EXPANDED POOL FROM BASE GAME: ----------------");
            foreach (var item in BaseGameTechPool)
            {
                try
                {
                    RawTechTemplate RTT = new RawTechTemplate(item);
                    if (RTT.terrain == BaseTerrain.Air || RTT.terrain == BaseTerrain.Chopper)
                    {
                        DebugTAC_AI.Log("\"" + item.Name + "\",");
                    }
                }
                catch { }
            }
            DebugTAC_AI.Log("---------------------- END EXPANDED POOL ----------------------");
        }
        internal static void GrabSpecialBaseGameSpawns(HashSet<string> names, List<TechData> compile)
        {
            InsureGrabBaseGameSpawns();
            foreach (var item in BaseGameTechPool)
            {
                if (names.Contains(item.Name))
                    compile.Add(item);
            }
            DebugTAC_AI.Log("SpecialAISpawner: Found & Compiled " + compile.Count + " base game spawns");
        }
        internal static List<KeyValuePair<SpawnBaseTypes, RawTechTemplate>> ReturnAllBaseGameSpawns()
        {
            List<TechData> temp = new List<TechData>();
            List<KeyValuePair<SpawnBaseTypes, RawTechTemplate>> tempOut = new List<KeyValuePair<SpawnBaseTypes, RawTechTemplate>>();
            GrabSpecialBaseGameSpawns(SelectedSpawns, temp);
            SpawnBaseTypes excess = (SpawnBaseTypes)Enum.GetValues(typeof(SpawnBaseTypes)).Length;
            foreach (var item in temp)
            {
                tempOut.Add(new KeyValuePair<SpawnBaseTypes, RawTechTemplate>(excess, new RawTechTemplate(item)));
                excess++;
            }
            DebugTAC_AI.Log("ReturnAllBaseGameSpawns Ready");
            return tempOut;
        }

        internal class GUIManaged
        {
            private static bool typesDisp = false;
            private static HashSet<NP_Types> enabledTabs = null;
            public static void GUIGetTotalManaged()
            {
                if (enabledTabs == null)
                {
                    enabledTabs = new HashSet<NP_Types>();
                }
                GUILayout.Box("--- Base Game ---");
                GUILayout.BeginVertical(AltUI.TextfieldBordered);
                if (GUILayout.Button("Print All"))
                {
                    BaseGameTechPool = null;
                    GatherAllPotentialTechsForPool();
                }
                if (GUILayout.Button("Print Non-Land"))
                {
                    BaseGameTechPool = null;
                    GatherAllPotentialNonLandForPool();
                }
                if (GUILayout.Button("Print Flying"))
                {
                    BaseGameTechPool = null;
                    GatherAllPotentialAircraftsForPool();
                }
                GUILayout.EndVertical();

                GUILayout.Box("--- AIrborne --- ");
                GUILayout.Label("  Capacity: " + Mathf.Min(KickStart.MaxEnemyWorldCapacity, MaxAirborneAIAllowed));
                int activeCount = 0;
                Dictionary<NP_Types, int> types = new Dictionary<NP_Types, int>();
                foreach (NP_Types item in Enum.GetValues(typeof(NP_Types)))
                {
                    types.Add(item, 0);
                }
                foreach (var air in AirPool)
                {
                    if (air != null && air.tank)
                    {
                        activeCount++;
                        int team = air.tank.Team;
                        types[AIGlobals.GetNPTTeamTypeForDebug(team)]++;
                    }
                }
                if (GUILayout.Button("  Total: " + AirPool.Count + " | Active: " + activeCount))
                    typesDisp = !typesDisp;
                if (typesDisp)
                {
                    foreach (var item in types)
                    {
                        if (GUILayout.Button("    Alignment: " + item.Key.ToString() + " - " + item.Value))
                        {
                            if (enabledTabs.Contains(item.Key))
                                enabledTabs.Remove(item.Key);
                            else
                                enabledTabs.Add(item.Key);
                        }
                        if (enabledTabs.Contains(item.Key))
                        {
                            foreach (var item2 in AirPool.FindAll(x => x.tank && 
                            AIGlobals.GetNPTTeamTypeForDebug(x.tank.Team) == item.Key))
                            {
                                GUILayout.Label("      Tech: " + item2.tank.name);
                                Vector3 pos = item2.tank.boundsCentreWorldNoCheck;
                                DebugExtUtilities.DrawDirIndicator(pos, pos + new Vector3(0, -10, 0), Color.red);
                            }
                        }
                    }
                }
            }
        }
    }
}
