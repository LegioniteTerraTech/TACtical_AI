using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using TerraTechETCUtil;
using TAC_AI.AI;
using TAC_AI.AI.Enemy;

namespace TAC_AI.Templates
{
    public class TrackedAirborneAI
    {
        public Tank airborneAI;
        public TrackedVisible trackVis;


        public TrackedAirborneAI(Tank set, bool IsSpace = false)
        {
            airborneAI = set;
            trackVis = ManVisible.inst.GetTrackedVisible(set.visible.ID);
            if (!IsSpace)
                airborneAI.SleepEvent.Subscribe(OnStop);
            if (trackVis != null)
                trackVis.OnDespawnEvent.Subscribe(OnRecycle);
        }
        public void OnRecycle(Visible vis)
        {   // It crashed 
            ManVisible.inst.ObliterateTrackedVisibleFromWorld(trackVis);
            SpecialAISpawner.AirPool.Remove(this);
            airborneAI.SleepEvent.Unsubscribe(OnStop);
        }

        public void OnStop(bool yes)
        {   // It crashed 
            airborneAI.SleepEvent.Unsubscribe(OnStop);
            if (trackVis != null)
                trackVis.OnDespawnEvent.Unsubscribe(OnRecycle);
            SpecialAISpawner.AirPool.Remove(this);
            if (airborneAI)
                SpecialAISpawner.Eradicate(airborneAI);
            else if (trackVis != null)
                ManVisible.inst.ObliterateTrackedVisibleFromWorld(trackVis);
            else
                DebugTAC_AI.LogError("TACtical_AI: TrackedAirborneAI - Could not remove an aircraft from the world!");

        }
    }
    public class SpecialAISpawner : MonoBehaviour
    {   //  We handle all the AI goodies here when Population Injector is N/A
        //      This module should ONLY be active (when initated) in Campaign mode!!!

        //      If you need to request access this to be opened to public for coding reasons, 
        //          please let LegioniteTerraTech know.  
        //      For tech-related concerns or additions, confront Legionite on the TerraTech Community Discord.

        private static readonly bool forceOn = false;    // spawn in creative no matter what

        private static SpecialAISpawner inst;
        private static ManLicenses Licences;

        private static Tank playerTank;
        internal const int trollTeam = -9001;
        internal static int EnemyTeam => ManSpawn.NewEnemyTeam;


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
        const int AirborneAISpawnOdds = 30;   // Out of 300 (dynamically changed based on difficulty)
        const int SpaceshipChance = 2;     // Out of 100
        const float AirSpawnDist = 400;
        const float AirDespawnDist = 475;
        const float SpaceBeginAltitude = 500;
        internal static float AirSpawnInterval = 30;


        public static void Initiate()
        {   // 
            if (inst)
                return;
            inst = new GameObject("AISpawnerAux").AddComponent<SpecialAISpawner>();
            Singleton.Manager<ManGameMode>.inst.ModeStartEvent.Subscribe(DetermineActiveOnMode);
            DebugTAC_AI.Log("TACtical_AI: SpecialAISpawner - Initated!");
            inst.gameObject.SetActive(false);
            RawTechLoader.Initiate();
            DetermineActiveOnModeType();
        }
        public static void DeInitiate()
        {
            if (!inst)
                return;
            RawTechLoader.DeInitiate();
            Singleton.Manager<ManGameMode>.inst.ModeStartEvent.Unsubscribe(DetermineActiveOnMode);
            Destroy(inst);
            inst = null;
            DebugTAC_AI.Log("TACtical_AI: SpecialAISpawner - DeInitated");
        }

        public static void DetermineActiveOnModeType()
        {
            ManGameMode.GameType mode = ManGameMode.inst.GetCurrentGameType();
            AirPool.Clear();
            RawTechLoader.inst.ClearQueue();
            RawTechExporter.Reload();
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
            RawTechExporter.Reload();
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
                playerTank = tank;
            }
        }
        public static void UpdatePlayerTank()
        {   // 
            playerTank = Singleton.playerTank;
        }
        public static void PlayerTankDeathCheck(Tank tank, ManDamage.DamageInfo oof)
        {   // 
            if (tank == playerTank && KickStart.Difficulty < 100)
            {   // Player could have been killed by airborneAI - remove all pop airborne AI
                DestroyAllPooledAirborneAI(true);
                playerTank = null;
            }
        }



        public static void OverrideSpawning(ManSpawn.TechSpawnParams TSP, Vector3 pos)
        {   // 
            if (TSP.m_IsPopulation)
            {
                if (!KickStart.isPopInjectorPresent && KickStart.EnableBetterAI && (ManNetwork.IsHost || !ManNetwork.IsNetworked))
                {
                    RawTechLoader.UseFactionSubTypes = true;
                    TechData newTech = TSP.m_TechToSpawn;
                    FactionTypesExt FTE = TSP.m_TechToSpawn.GetMainCorpExt();
                    FactionSubTypes FST = KickStart.CorpExtToCorp(FTE);
                    FactionLevel lvl = RawTechLoader.TryGetPlayerLicenceLevel();
                    if (KickStart.AllowSeaEnemiesToSpawn && KickStart.isWaterModPresent && AI.Movement.AIEPathing.AboveTheSea(pos) &&
                        RawTechTemplate.GetBaseTerrain(TSP.m_TechToSpawn, TSP.m_TechToSpawn.CheckIsAnchored()) == BaseTerrain.Land)
                    {
                        SetSpawnSea(TSP, FTE, FST, lvl, ref newTech);
                    }
                    else if (UnityEngine.Random.Range(0, 100) < KickStart.LandEnemyOverrideChance) // Override for normal Tech spawns
                    {
                        SetSpawnLand(TSP, FTE, FST, lvl, ref newTech);
                    }

                    RawTechLoader.UseFactionSubTypes = false;
                }
            }
        }
        public static void SetSpawnLand(ManSpawn.TechSpawnParams TSP, FactionTypesExt FTE, FactionSubTypes FST,
            FactionLevel lvl, ref TechData newTech)
        {
            // OVERRIDE TECH SPAWN
            try
            {
                int grade = 99;
                try
                {
                    if (!CreativeMode)
                        grade = ManLicenses.inst.GetCurrentLevel(FST);
                }
                catch { }
                if (RawTechLoader.ShouldUseCustomTechs(out List<int> valid, FTE, lvl, BasePurpose.NotStationary, BaseTerrain.Land, maxGrade: grade, maxPrice: KickStart.EnemySpawnPriceMatching))
                {
                    int randSelect = valid.GetRandomEntry();
                    newTech = RawTechLoader.GetUnloadedTech(TempManager.ExternalEnemyTechsAll[randSelect], TSP.m_Team, out _);

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
                    DebugTAC_AI.Log("TACtical_AI:  Tech " + TSP.m_TechToSpawn.Name + " has been swapped out for land tech " + newTech.Name + " instead");
                    TSP.m_TechToSpawn = newTech;
                }
                else
                {
                    SpawnBaseTypes type = RawTechLoader.GetEnemyBaseType(FTE, lvl, BasePurpose.NotStationary, BaseTerrain.Land, maxGrade: grade, maxPrice: KickStart.EnemySpawnPriceMatching);
                    if (type != SpawnBaseTypes.NotAvail && !RawTechLoader.IsFallback(type))
                    {
                        newTech = RawTechLoader.GetUnloadedTech(type, TSP.m_Team, out _);
                        if (newTech == null)
                        {
                            DebugTAC_AI.Exception("Land Tech spawning override(PREFAB) failed as fetched TechData is null.  Please report this.");
                            return;
                        }
                        if (newTech.m_BlockSpecs == null)
                        {
                            DebugTAC_AI.Exception("Land Tech spawning override(PREFAB) failed as fetched TechData's block info is null.  Please report this.");
                            return;
                        }
                        if (newTech.m_BlockSpecs.Count == 0)
                        {
                            DebugTAC_AI.Exception("Land Tech spawning override(PREFAB) failed as no blocks are present on modified spawning Tech.  Please report this.");
                            return;
                        }

                        DebugTAC_AI.Log("TACtical_AI:  Tech " + TSP.m_TechToSpawn.Name + " has been swapped out for land tech " + newTech.Name + " instead");
                        TSP.m_TechToSpawn = newTech;
                    }
                    // Else we don't do anything.
                }
            }
            catch
            {
                DebugTAC_AI.Assert(true, "TACtical_AI: Attempt to swap Land tech failed!");
            }
        }
        public static void SetSpawnSea(ManSpawn.TechSpawnParams TSP, FactionTypesExt FTE, FactionSubTypes FST, 
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


                if (RawTechLoader.ShouldUseCustomTechs(out List<int> valid, FTE, lvl, BasePurpose.NotStationary, BaseTerrain.Sea, maxGrade: grade))
                {
                    int randSelect = valid.GetRandomEntry();
                    newTech = RawTechLoader.GetUnloadedTech(TempManager.ExternalEnemyTechsAll[randSelect], TSP.m_Team, out _);

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
                    DebugTAC_AI.Log("TACtical_AI:  Tech " + TSP.m_TechToSpawn.Name + " landed in water and was likely not water-capable, naval Tech " + newTech.Name + " was substituted for the spawn instead");
                    TSP.m_TechToSpawn = newTech;
                }
                else
                {
                    SpawnBaseTypes type = RawTechLoader.GetEnemyBaseType(FTE, lvl, BasePurpose.NotStationary, BaseTerrain.Sea, maxGrade: grade);
                    if (type != SpawnBaseTypes.NotAvail && !RawTechLoader.IsFallback(type))
                    {
                        newTech = RawTechLoader.GetUnloadedTech(type, TSP.m_Team, out _);
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
                        DebugTAC_AI.Log("TACtical_AI:  Tech " + TSP.m_TechToSpawn.Name + " landed in water and was likely not water-capable, naval Tech " + newTech.Name + " was substituted for the spawn instead");

                        TSP.m_TechToSpawn = newTech;
                    }
                    // Else we don't do anything.
                }
            }
            catch
            {
                DebugTAC_AI.Assert(true, "TACtical_AI:  Attempt to swap sea tech failed!");
            }
        }

        private static void TrySpawnAirborneAIInAir()
        {   //  Spawns airborneAI even when the parts required aren't available, but they will not
            //      attack unless provoked by the player or another enemy, which is unlikely.
            // MAKE SURE licences are grabbed!!!
            Licences = Singleton.Manager<ManLicenses>.inst;
            if (Licences.IsNull() && ManGameMode.inst.IsCurrentModeCampaign())
            {   // The game tried to enable creative spawns whilist no licences were active!?!?
                DebugTAC_AI.Log("TACtical_AI: TrySpawnAirborneAIInAir - It's campaign mode but no licences were found?!?");
                return;
            }

            if (playerTank.IsNull() || AIGlobals.AtSceneTechMax())
                return;
            if (AirPool.Count >= MaxAirborneAIAllowed)
                return;

            Vector3 pos;
            if (playerTank.rbody.IsNotNull())
                pos = (playerTank.rbody.velocity * Time.deltaTime * 5) + playerTank.boundsCentreWorldNoCheck;
            else
                pos = playerTank.boundsCentreWorldNoCheck;

            Vector3 forwards = GetRandAirAngle();

            pos = GetAirOffsetFromPosition(pos, forwards);


            Tank newAirborneAI;
            bool spawnSpace;
            if (KickStart.CommitDeathMode)
            {
                if (playerTank.boundsCentreWorld.y > SpaceBeginAltitude)
                {
                    spawnSpace = true;
                }
                else
                    spawnSpace = UnityEngine.Random.Range(0, 10) < 1;
            }
            else
            {
                if (playerTank.boundsCentreWorld.y > SpaceBeginAltitude)
                {
                    spawnSpace = true;
                }
                else
                    spawnSpace = UnityEngine.Random.Range(0, 100) < SpaceshipChance;
            }

            bool IsSpace = false;
            if (CreativeMode)
            {
                if (spawnSpace)
                {
                    newAirborneAI = RawTechLoader.SpawnRandomTechAtPosHead(pos, forwards, EnemyTeam, FactionTypesExt.NULL, BaseTerrain.Space, snapTerrain: false);
                    IsSpace = true;
                }
                else
                    newAirborneAI = RawTechLoader.SpawnRandomTechAtPosHead(pos, forwards, EnemyTeam, FactionTypesExt.NULL, BaseTerrain.Air, snapTerrain: false);
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
                //DebugTAC_AI.Log("TACtical_AI: SpecialAISpawner - Could not spawn airborneAI - Player has no corps unlocked!?!");
                return;
            }
            TrackedAirborneAI newAir = new TrackedAirborneAI(newAirborneAI, IsSpace);
            AirPool.Add(newAir);
        }
        private static Tank SpawnPrefabAircraft(Vector3 pos, Vector3 forwards)
        {   // 
            try
            {
                List<FactionTypesExt> factionsAvail = new List<FactionTypesExt>();
                
                if (Licences.GetLicense(KickStart.CorpExtToCorp(FactionTypesExt.GSO)).CurrentLevel >= 0)// flight grade is 2 but random spawns start at 0
                    factionsAvail.Add(FactionTypesExt.GSO);
                // GC literally can't fly an airborneAI
                if (Licences.GetLicense(KickStart.CorpExtToCorp(FactionTypesExt.GC)).IsDiscovered && Licences.GetLicense(KickStart.CorpExtToCorp(FactionTypesExt.GC)).CurrentLevel >= 1)
                    factionsAvail.Add(FactionTypesExt.GC);
                if (Licences.GetLicense(KickStart.CorpExtToCorp(FactionTypesExt.VEN)).IsDiscovered && Licences.GetLicense(KickStart.CorpExtToCorp(FactionTypesExt.VEN)).CurrentLevel >= 0)// flight grade is 1 but random spawns start at 0
                    factionsAvail.Add(FactionTypesExt.VEN);
                if (Licences.GetLicense(KickStart.CorpExtToCorp(FactionTypesExt.HE)).IsDiscovered && Licences.GetLicense(KickStart.CorpExtToCorp(FactionTypesExt.HE)).CurrentLevel >= 1)
                    factionsAvail.Add(FactionTypesExt.HE);
                if (Licences.GetLicense(KickStart.CorpExtToCorp(FactionTypesExt.BF)).IsDiscovered && Licences.GetLicense(KickStart.CorpExtToCorp(FactionTypesExt.BF)).CurrentLevel >= 0)
                    factionsAvail.Add(FactionTypesExt.BF);
                if (factionsAvail.Count == 0)
                    return null;

                bool hasAllDone = true;
                if (factionsAvail.Count > 5)
                {
                    foreach (FactionTypesExt faction in factionsAvail)
                    {
                        if (!Licences.GetLicense(KickStart.CorpExtToCorp(faction)).HasReachedMaxLevel)
                        {
                            hasAllDone = false;
                            break;
                        }
                    }
                }
                else
                    hasAllDone = false;
                if (factionsAvail.Contains(FactionTypesExt.GC))
                    factionsAvail.Remove(FactionTypesExt.GC);
                if (factionsAvail.Contains(FactionTypesExt.EXP))
                    factionsAvail.Remove(FactionTypesExt.EXP);

                // spawn and return the airborneAI
                if (hasAllDone) // all corps unlocked by player
                    return RawTechLoader.SpawnRandomTechAtPosHead(pos, forwards, EnemyTeam, FactionTypesExt.NULL, BaseTerrain.Air, maxPrice: KickStart.EnemySpawnPriceMatching);

                // if we don't have all corps possible maxed, we do the normal spawn

                // determine corp
                factionsAvail.Shuffle();
                FactionTypesExt finalFaction = factionsAvail.First();

                bool unProvoked = true;
                switch (finalFaction)
                {   // contains minimum grades (index) needed before flying parts become available
                    case FactionTypesExt.GSO:
                        if (Licences.GetLicense(KickStart.CorpExtToCorp(FactionTypesExt.GSO)).CurrentLevel >= 2)
                            unProvoked = false;
                        break;
                    case FactionTypesExt.VEN:
                        if (Licences.GetLicense(KickStart.CorpExtToCorp(FactionTypesExt.VEN)).CurrentLevel >= 1)
                            unProvoked = false;
                        break;
                    case FactionTypesExt.HE:
                        if (Licences.GetLicense(KickStart.CorpExtToCorp(FactionTypesExt.HE)).CurrentLevel >= 1)
                            unProvoked = false;
                        break;
                    case FactionTypesExt.BF:
                        unProvoked = false;
                        break;
                    default:
                        if (Licences.GetLicense(KickStart.CorpExtToCorp(FactionTypesExt.GSO)).CurrentLevel == 4)
                            unProvoked = false;
                        break;
                }

                try
                {
                    Singleton.Manager<UIMPChat>.inst.AddMissionMessage("<b>Unidentified flying object spotted!</b>");
                }
                catch { }
                DebugTAC_AI.Log("TACtical_AI: There are now " + (AirPool.Count + 1) + " airborneAI present on-scene");
                if (unProvoked)
                {
                    RawTechLoader.UseFactionSubTypes = true;
                    if (RawTechLoader.SpawnRandomTechAtPosHead(pos, forwards, EnemyTeam, out Tank finalTank, finalFaction, BaseTerrain.Air, unProvoked, snapTerrain: false, Licences.GetLicense(KickStart.CorpExtToCorp(finalFaction)).CurrentLevel, maxPrice: KickStart.EnemySpawnPriceMatching))
                        return finalTank;
                    else
                        return null;
                }
                // else we do default spawn
                RawTechLoader.UseFactionSubTypes = true;
                return RawTechLoader.SpawnRandomTechAtPosHead(pos, forwards, EnemyTeam, finalFaction, BaseTerrain.Air, unProvoked, snapTerrain: false, Licences.GetLicense(KickStart.CorpExtToCorp(finalFaction)).CurrentLevel, maxPrice: KickStart.EnemySpawnPriceMatching);
            }
            catch { }
            DebugTAC_AI.Log("TACtical_AI: SpecialAISpawner - Could not fetch corps, resorting to random spawns");
            return RawTechLoader.SpawnRandomTechAtPosHead(pos, forwards, EnemyTeam, FactionTypesExt.NULL, BaseTerrain.Air, snapTerrain: false, maxPrice: KickStart.EnemySpawnPriceMatching);
        }
        private static Tank SpawnPrefabSpaceship(Vector3 pos, Vector3 forwards, out bool worked)
        {   // 
            worked = false;
            try
            {
                List<FactionTypesExt> factionsAvail = new List<FactionTypesExt>();

                if (Licences.GetLicense(KickStart.CorpExtToCorp(FactionTypesExt.GSO)).CurrentLevel >= 2)
                    factionsAvail.Add(FactionTypesExt.GSO);
                // GC literally can't fly an airborneAI
                if (Licences.GetLicense(KickStart.CorpExtToCorp(FactionTypesExt.GC)).IsDiscovered && Licences.GetLicense(KickStart.CorpExtToCorp(FactionTypesExt.GC)).CurrentLevel >= 2)
                    factionsAvail.Add(FactionTypesExt.GC);
                if (Licences.GetLicense(KickStart.CorpExtToCorp(FactionTypesExt.VEN)).IsDiscovered && Licences.GetLicense(KickStart.CorpExtToCorp(FactionTypesExt.VEN)).CurrentLevel >= 1)
                    factionsAvail.Add(FactionTypesExt.VEN);
                if (Licences.GetLicense(KickStart.CorpExtToCorp(FactionTypesExt.HE)).IsDiscovered && Licences.GetLicense(KickStart.CorpExtToCorp(FactionTypesExt.HE)).CurrentLevel >= 1)
                    factionsAvail.Add(FactionTypesExt.HE);
                if (Licences.GetLicense(KickStart.CorpExtToCorp(FactionTypesExt.BF)).IsDiscovered && Licences.GetLicense(KickStart.CorpExtToCorp(FactionTypesExt.BF)).CurrentLevel >= 0)
                    factionsAvail.Add(FactionTypesExt.BF);
                if (factionsAvail.Count == 0)
                    return null;

                bool hasAllDone = true;
                if (factionsAvail.Count > 5)
                {
                    foreach (FactionTypesExt faction in factionsAvail)
                    {
                        if (!Licences.GetLicense(KickStart.CorpExtToCorp(faction)).HasReachedMaxLevel)
                            hasAllDone = false;
                    }
                }
                else
                    hasAllDone = false;
                if (factionsAvail.Contains(FactionTypesExt.GC))
                    factionsAvail.Remove(FactionTypesExt.GC);
                if (factionsAvail.Contains(FactionTypesExt.EXP))
                    factionsAvail.Remove(FactionTypesExt.EXP);

                // spawn and return the airborneAI
                if (hasAllDone) // all corps unlocked by player
                    return RawTechLoader.SpawnRandomTechAtPosHead(pos, forwards, EnemyTeam, FactionTypesExt.NULL, BaseTerrain.Space, maxPrice: KickStart.EnemySpawnPriceMatching);

                // if we don't have all corps possible maxed, we do the normal spawn

                // determine corp
                factionsAvail.Shuffle();
                FactionTypesExt finalFaction = factionsAvail.First();

                bool unProvoked = true;
                switch (finalFaction)
                {   // contains minimum grades (index) needed before flying parts become available
                    case FactionTypesExt.GSO:
                        if (Licences.GetLicense(KickStart.CorpExtToCorp(FactionTypesExt.GSO)).CurrentLevel >= 2)
                            unProvoked = false;
                        break;
                    case FactionTypesExt.VEN:
                        if (Licences.GetLicense(KickStart.CorpExtToCorp(FactionTypesExt.VEN)).CurrentLevel >= 1)
                            unProvoked = false;
                        break;
                    case FactionTypesExt.HE:
                        if (Licences.GetLicense(KickStart.CorpExtToCorp(FactionTypesExt.HE)).CurrentLevel >= 1)
                            unProvoked = false;
                        break;
                    case FactionTypesExt.BF:
                        unProvoked = false;
                        break;
                    default:
                        if (Licences.GetLicense(KickStart.CorpExtToCorp(FactionTypesExt.GSO)).CurrentLevel == 4)
                            unProvoked = false;
                        break;
                }

                try
                {
                    Singleton.Manager<UIMPChat>.inst.AddMissionMessage("<b>HUGE unidentified flying object spotted!</b>");
                }
                catch { }
                DebugTAC_AI.Log("TACtical_AI: There are now " + (AirPool.Count + 1) + " airborneAI present on-scene");
                RawTechLoader.UseFactionSubTypes = true;
                worked = RawTechLoader.SpawnRandomTechAtPosHead(pos, forwards, AIGlobals.GetRandomBaseTeam(), out Tank tech, finalFaction, BaseTerrain.Space, unProvoked, snapTerrain: false, Licences.GetLicense(KickStart.CorpExtToCorp(finalFaction)).CurrentLevel, maxPrice: KickStart.EnemySpawnPriceMatching);
                return tech;
            }
            catch { }
            DebugTAC_AI.Log("TACtical_AI: SpecialAISpawner - Could not fetch corps, resorting to random spawns");
            worked = true;
            return RawTechLoader.SpawnRandomTechAtPosHead(pos, forwards, AIGlobals.GetRandomBaseTeam(), FactionTypesExt.NULL, BaseTerrain.Space, snapTerrain: false, maxPrice: KickStart.EnemySpawnPriceMatching);
        }

        public static void TrySpawnTraderTroll(Vector3 pos)
        {   // Spawn trader trolls to make bigger techs fight harder

            DebugTAC_AI.Log("TACtical_AI: TrySpawnTraderTroll - Queued request at " + pos + "!");
            if (ManNetwork.IsNetworked && !ManNetwork.IsHost)
                return;

            if (UnityEngine.Random.Range(-50, 150) > KickStart.Difficulty)
                return;

            if (!AIEBases.IsLocationGridEmpty(pos))
                return;

            try
            {
                List<FactionTypesExt> factionsAvail = new List<FactionTypesExt>();

                if (Licences.GetLicense(KickStart.CorpExtToCorp(FactionTypesExt.GSO)).CurrentLevel >= 2)
                    factionsAvail.Add(FactionTypesExt.GSO);
                if (Licences.GetLicense(KickStart.CorpExtToCorp(FactionTypesExt.GC)).IsDiscovered)
                    factionsAvail.Add(FactionTypesExt.GC);
                if (Licences.GetLicense(KickStart.CorpExtToCorp(FactionTypesExt.VEN)).IsDiscovered)
                    factionsAvail.Add(FactionTypesExt.VEN);
                if (Licences.GetLicense(KickStart.CorpExtToCorp(FactionTypesExt.HE)).IsDiscovered)
                    factionsAvail.Add(FactionTypesExt.HE);
                if (Licences.GetLicense(KickStart.CorpExtToCorp(FactionTypesExt.BF)).IsDiscovered)
                    factionsAvail.Add(FactionTypesExt.BF);
                if (factionsAvail.Count == 0)
                    return;
                FactionTypesExt factionSelect = factionsAvail.GetRandomEntry();

                //pos = GetOffsetPosAngle(pos); 

                if (!AIEBases.TryFindExpansionLocationGrid(pos, pos + (UnityEngine.Random.insideUnitCircle.ToVector3XZ() * 128), out Vector3 pos3))
                    return;

                RawTechLoader.UseFactionSubTypes = true;
                int licence = Licences.GetLicense(KickStart.CorpExtToCorp(factionSelect)).CurrentLevel;
                if (AIGlobals.EnemyBaseMakerChance >= UnityEngine.Random.Range(0, 100))
                {
                    int team = AIGlobals.GetRandomEnemyBaseTeam();
                    RawTechLoader.StartBaseAtPositionNoFounder(factionSelect, pos3, team, 
                        BasePurpose.AnyNonHQ, licence);
                    if (AIEBases.TryFindExpansionLocationGrid(pos3, pos3 + new Vector3(0,0,64), out Vector3 pos4))
                    {
                        RawTechLoader.StartBaseAtPositionNoFounder(factionSelect, pos3, team, 
                            BasePurpose.NotStationary, licence);
                    }
                }
                else
                    RawTechLoader.SpawnSpecificTechSafe(pos3, Vector3.forward, trollTeam,
                        new HashSet<BasePurpose> { BasePurpose.Defense }, faction: factionSelect,
                        maxGrade: licence,  maxPrice: KickStart.EnemySpawnPriceMatching, isPopulation: true);

                DebugTAC_AI.Log("TACtical_AI: TrySpawnTraderTroll - Spawned!");
                try
                {
                    Singleton.Manager<UIMPChat>.inst.AddMissionMessage("<b>Trader Troll ahead!</b>");
                }
                catch { }

                return;
            }
            catch { }
            DebugTAC_AI.Log("TACtical_AI: TrySpawnTraderTroll - Could not fetch corps, resorting to random spawns");

            if (!AIEBases.TryFindExpansionLocationGrid(pos, pos + (UnityEngine.Random.insideUnitCircle.ToVector3XZ() * 128), out Vector3 pos2))
                return;
            RawTechLoader.UseFactionSubTypes = true;
            RawTechLoader.SpawnSpecificTechSafe(pos2, Vector3.forward, trollTeam, new HashSet<BasePurpose> { BasePurpose.Defense }, faction: FactionTypesExt.NULL, isPopulation: true);

            DebugTAC_AI.Log("TACtical_AI: TrySpawnTraderTroll - Spawned!");
            try
            {
                Singleton.Manager<UIMPChat>.inst.AddMissionMessage("<b>Trader Troll ahead!</b>");
            }
            catch { }

        }

        private static void ManagePooledAIs()
        {   // 
            ManagePooledEradicators();
            ManagePooledAirborneAI();
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
            if (!KickStart.AllowAirEnemiesToSpawn || ManPop.inst.IsSpawningEnabled)
                DestroyAllPooledAirborneAI(KickStart.AllowAirEnemiesToSpawn);
            int count = AirPool.Count();
            int deadairborneAICount = 0;
            for (int step = 0; count > step; step++)
            {
                try
                {
                    Tank airborneAI = AirPool[step].airborneAI;
                    float sqrMag = (airborneAI.boundsCentreWorldNoCheck - playerTank.boundsCentreWorldNoCheck).sqrMagnitude;
                    if (airborneAI.IsNull() || !airborneAI.visible.isActive)
                    {
                        AirPool.RemoveAt(step);
                        DebugTAC_AI.Info("TACtical_AI: SpecialAISpawner - Removed and recycled " + airborneAI.name + " from AirPool as it may have despawned.");
                        step--;
                        count--;
                        deadairborneAICount++;
                    }
                    else if (airborneAI.trans.position.y > AIGlobals.AirNPTMaxHeightOffset + Singleton.playerPos.y)
                    {
                        AirPool.RemoveAt(step);
                        DebugTAC_AI.Info("TACtical_AI: SpecialAISpawner - Removed and recycled " + airborneAI.name + " from AirPool as it flew above player distance.");
                        Purge(airborneAI);
                        step--;
                        count--;
                    }
                    else if ((airborneAI.boundsCentreWorldNoCheck - playerTank.boundsCentreWorldNoCheck).sqrMagnitude > AirDespawnDist * AirDespawnDist)
                    {
                        AirPool.RemoveAt(step);
                        DebugTAC_AI.Info("TACtical_AI: SpecialAISpawner - Removed and recycled " + airborneAI.name + " from AirPool as it left AirDespawnDist radius.");
                        Purge(airborneAI);
                        step--;
                        count--;
                    }
                }
                catch
                {
                    AirPool.RemoveAt(step);
                    step--;
                    count--;
                    deadairborneAICount++;
                }
            }
            if (deadairborneAICount > 0)
                DebugTAC_AI.Log("TACtical_AI: SpecialAISpawner - Removed " + deadairborneAICount + " dead airborneAI(s) from AirPool");
        }
        private static void DestroyAllPooledAirborneAI(bool onlyPopulation)
        {   // 
            if (AirPool.Count == 0)
                return;
            if (onlyPopulation)
            {
                for (int step = 0; step < AirPool.Count; )
                {
                    var airborneAI = AirPool.ElementAt(step);
                    if (airborneAI.airborneAI.IsNotNull())
                    {
                        if (airborneAI.airborneAI.IsPopulation)
                        {
                            Purge(airborneAI.airborneAI);
                            AirPool.RemoveAt(step);
                        }
                        else
                            step++;
                    }
                    else
                        AirPool.RemoveAt(step);
                }
            }
            else
            {
                foreach (TrackedAirborneAI airborneAI in AirPool)
                {
                    if (airborneAI.airborneAI.IsNotNull())
                        Purge(airborneAI.airborneAI);
                }
                AirPool.Clear();
                DebugTAC_AI.Log("TACtical_AI: SpecialAISpawner - Destroyed all enemy pooled airborneAI");
            }
        }
        private static void CollectPossibleAirborneAI()
        {   // 
            foreach (Tank tech in Singleton.Manager<ManTechs>.inst.IterateTechsWhere(x => x && x.GetComponent<AIECore.TankAIHelper>()
            && x.IsPopulation))
            {
                var em = tech.GetComponent<EnemyMind>();
                if (em)
                {
                    if (tech.GetComponent<AIECore.TankAIHelper>().MovementController is AIControllerAir || 
                        em.EvilCommander == EnemyHandling.Starship)
                    {
                        try
                        {
                            TrackedAirborneAI newAir = new TrackedAirborneAI(tech);
                            AirPool.Add(newAir);
                        }
                        catch
                        {
                            DebugTAC_AI.Log("TACtical_AI: SpecialAISpawner - Error on handling enemy airborne AI pool");
                        }
                    }
                }
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
            DebugTAC_AI.Log("TACtical_AI: InflictPercentDamage target " + tech.Name + " removed " + (curCount * percent) + "!");
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
                    DebugTAC_AI.Log("TACtical_AI: Purge - Trying to Purge by visible " + tech.name);
                    tech.visible.RemoveFromGame();
                }
            }
        }
        /// <summary>
        /// Remove a Tech from existance
        /// </summary>
        /// <param name="tech"></param>
        /// <param name="player"></param>
        internal static bool PurgeHost(int HostID, string name)
        {   // 
            if (!ManNetwork.IsHost)
                throw new Exception("TACtical_AI: SpecialAISpawner.PurgeHost called on non-host");
            DebugTAC_AI.Log("TACtical_AI: PurgeHost - Name " + name +  " | " + HostID + "  Callstack: " + StackTraceUtility.ExtractStackTrace());
            if (ManNetwork.IsNetworked)
            {
                try
                {
                    TrackedVisible TV = ManVisible.inst.GetTrackedVisibleByHostID(HostID);
                    Singleton.Manager<ManNetwork>.inst.SendToServer(TTMsgType.UnspawnTech, new UnspawnTechMessage
                    {
                        m_HostID = TV.HostID,
                        m_CheatBypassInventory = true,
                    }
                    );
                    DebugTAC_AI.Log("TACtical_AI: Purge - PURGED " + name + " (MP)");
                    return true;
                }
                catch (Exception e)
                {
                    DebugTAC_AI.Log("TACtical_AI: Purge - Failed to purge " + name + " (MP)");
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
                                    if (AIGlobals.IsBaseTeam(item.TeamID))
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
                    DebugTAC_AI.Log("TACtical_AI: Purge - Error backtrace - " + e);
                }
            }
            else
            {
                try
                {
                    ManVisible.inst.ObliterateTrackedVisibleFromWorld(HostID);
                    DebugTAC_AI.Log("TACtical_AI: Purge - PURGED " + name);
                    return true;
                }
                catch (Exception e)
                {
                    DebugTAC_AI.Log("TACtical_AI: Purge - Failed to purge " + name + " (SINGLE player)");
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
                                    if (AIGlobals.IsBaseTeam(item.TeamID))
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
                    DebugTAC_AI.Log("TACtical_AI: Purge - Error backtrace - " + e);
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
                List<TankBlock> toDestroy = tech.blockman.IterateBlocks().ToList();
                tech.blockman.Disintegrate();
                foreach (TankBlock block in toDestroy)
                {
                    try
                    {
                        if (!block.damage.AboutToDie)
                            block.damage.SelfDestruct(0.5f);
                    }
                    catch { }
                }
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
                inst.gameObject.SetActive(true);
                DebugTAC_AI.Log("TACtical_AI: SpecialAISpawner - Activated special enemy spawns");
                thisActive = true;
            }
            CollectPossibleAirborneAI();
        }
        private static void Pause()
        {   // 
            if (thisActive)
            {
                inst.gameObject.SetActive(false);
                Singleton.Manager<ManTechs>.inst.PlayerTankChangedEvent.Unsubscribe(UpdatePlayerTank);
                Singleton.Manager<ManTechs>.inst.TankDestroyedEvent.Unsubscribe(PlayerTankDeathCheck);
                inst.counter = 0;
                Licences = null;
                DebugTAC_AI.Log("TACtical_AI: SpecialAISpawner - Deactivated special enemy spawns");
                thisActive = false;
            }
        }
        public void Update()
        {   // 
            //DebugTAC_AI.Log("TACtical_AI: SpecialAISpawner - ACTIVE!!!  time" + counter);
            bool doubleSpawnRate = false;
            try
            {
                doubleSpawnRate = Singleton.cameraTrans.position.y > AIGlobals.AirPromoteSpaceHeight;
            }
            catch { }
            if ((Singleton.Manager<ManPop>.inst.IsSpawningEnabled || forceOn) && counter > (AirSpawnInterval / (doubleSpawnRate ? 2 : 1)) / ((KickStart.Difficulty / 100) + 1.5f))
            {   // determine if we should spawn new one, also manage existing pooled airborneAIs
                //DebugTAC_AI.Log("TACtical_AI: SpecialAISpawner - Spawn lerp");
                if (KickStart.EnableBetterAI && KickStart.enablePainMode && !AIGlobals.AtSceneTechMax())
                {
                    if (KickStart.AllowAirEnemiesToSpawn && UnityEngine.Random.Range(EnemyTeam, 301 - (KickStart.difficulty + 50)) < AirborneAISpawnOdds)
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
            if (updateTimer > 25)
            {   // manager timer
                ManagePooledAIs();
                updateTimer = 0;
            }
            if (!Singleton.Manager<ManPauseGame>.inst.IsPaused)
            {
                counter += Time.deltaTime;
                updateTimer++;
            }
            else
                updateTimer = 0;
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
            return AI.Movement.AIEPathing.OffsetFromGroundAAlt(pos + -(angleHeading * AirSpawnDist) + (Singleton.cameraTrans.forward * 25), 75);
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
                    if (air != null && air.airborneAI)
                    {
                        activeCount++;
                        int team = air.airborneAI.Team;
                        types[AIGlobals.GetNPTTeamType(team)]++;
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
                            foreach (var item2 in AirPool.FindAll(x => x.airborneAI && 
                            AIGlobals.GetNPTTeamType(x.airborneAI.Team) == item.Key))
                            {
                                GUILayout.Label("      Tech: " + item2.airborneAI.name);
                                Vector3 pos = item2.airborneAI.boundsCentreWorldNoCheck;
                                DebugRawTechSpawner.DrawDirIndicator(pos, pos + new Vector3(0, -10, 0), Color.red);
                            }
                        }
                    }
                }
            }
        }
    }
}
