using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
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
                Debug.LogError("TACtical_AI: TrackedAirborneAI - Could not remove an aircraft from the world!");

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
        internal static bool IsAttract => ManGameMode.inst.IsCurrent<ModeAttract>();
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
            var startup = new GameObject("AISpawnerAux");
            startup.AddComponent<SpecialAISpawner>();
            inst = startup.GetComponent<SpecialAISpawner>();
            Singleton.Manager<ManGameMode>.inst.ModeStartEvent.Subscribe(DetermineActiveOnMode);
            Debug.Log("TACtical_AI: SpecialAISpawner - Initated!");
            startup.SetActive(false);
            RawTechLoader.Initiate();
        }
        public static void DeInitiate()
        {
            RawTechLoader.DeInitiate();
            Singleton.Manager<ManGameMode>.inst.ModeStartEvent.Unsubscribe(DetermineActiveOnMode);
            Destroy(inst);
            inst = null;
            Debug.Log("TACtical_AI: SpecialAISpawner - DeInitated");
        }
        
        public static void DetermineActiveOnModeType(ManGameMode.GameType mode)
        {   // 
            AirPool.Clear();
            RawTechLoader.inst.ClearQueue();
            RawTechExporter.Reload();
            OverrideManPop.QueuedChangeToRagnarokPop();
            DebugRawTechSpawner.ShouldBeActive();
            if ((mode == ManGameMode.GameType.MainGame || mode == ManGameMode.GameType.Misc
                || mode == ManGameMode.GameType.CoOpCampaign || mode == ManGameMode.GameType.CoOpCreative) 
                && (ManNetwork.IsHost || !ManNetwork.IsNetworked))
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
            if ((mode is ModeMain || mode is ModeMisc || mode is ModeCoOpCampaign || mode is ModeCoOpCreative) && (ManNetwork.IsHost || !ManNetwork.IsNetworked))
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
            {   // Player could have been killed by airborneAI - remove all enemies
                DestroyAllPooledAirborneAI();
                playerTank = null;
            }
        }


        private static void TrySpawnAirborneAIInAir()
        {   //  Spawns airborneAI even when the parts required aren't available, but they will not
            //      attack unless provoked by the player or another enemy, which is unlikely.
            // MAKE SURE licences are grabbed!!!
            Licences = Singleton.Manager<ManLicenses>.inst;
            if (Licences.IsNull() && ManGameMode.inst.IsCurrentModeCampaign())
            {   // The game tried to enable creative spawns whilist no licences were active!?!?
                Debug.Log("TACtical_AI: TrySpawnAirborneAIInAir - It's campaign mode but no licences were found?!?");
                return;
            }

            if (playerTank.IsNull())
                return;
            if (AirPool.Count >= MaxAirborneAIAllowed)
                return;
            if (10 / Time.deltaTime < 20) // game is lagging to much
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
                    newAirborneAI = RawTechLoader.SpawnRandomTechAtPosHead(pos, forwards, EnemyTeam, FactionTypesExt.NULL, BaseTerrain.Space, AutoTerrain: false);
                    IsSpace = true;
                }
                else
                    newAirborneAI = RawTechLoader.SpawnRandomTechAtPosHead(pos, forwards, EnemyTeam, FactionTypesExt.NULL, BaseTerrain.Air, AutoTerrain: false);
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
                //Debug.Log("TACtical_AI: SpecialAISpawner - Could not spawn airborneAI - Player has no corps unlocked!?!");
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
                if (Licences.GetLicense(KickStart.CorpExtToCorp(FactionTypesExt.GC)).IsDiscovered && Licences.GetLicense(KickStart.CorpExtToCorp(FactionTypesExt.GC)).CurrentLevel >= 2)
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
                Debug.Log("TACtical_AI: There are now " + (AirPool.Count + 1) + " airborneAI present on-scene");
                if (unProvoked)
                {
                    RawTechLoader.UseFactionSubTypes = true;
                    if (RawTechLoader.SpawnRandomTechAtPosHead(pos, forwards, EnemyTeam, out Tank finalTank, finalFaction, BaseTerrain.Air, unProvoked, AutoTerrain: false, Licences.GetLicense(KickStart.CorpExtToCorp(finalFaction)).CurrentLevel, maxPrice: KickStart.EnemySpawnPriceMatching))
                        return finalTank;
                    else
                        return null;
                }
                // else we do default spawn
                RawTechLoader.UseFactionSubTypes = true;
                return RawTechLoader.SpawnRandomTechAtPosHead(pos, forwards, EnemyTeam, finalFaction, BaseTerrain.Air, unProvoked, AutoTerrain: false, Licences.GetLicense(KickStart.CorpExtToCorp(finalFaction)).CurrentLevel, maxPrice: KickStart.EnemySpawnPriceMatching);
            }
            catch { }
            Debug.Log("TACtical_AI: SpecialAISpawner - Could not fetch corps, resorting to random spawns");
            return RawTechLoader.SpawnRandomTechAtPosHead(pos, forwards, EnemyTeam, FactionTypesExt.NULL, BaseTerrain.Air, AutoTerrain: false, maxPrice: KickStart.EnemySpawnPriceMatching);
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
                Debug.Log("TACtical_AI: There are now " + (AirPool.Count + 1) + " airborneAI present on-scene");
                RawTechLoader.UseFactionSubTypes = true;
                worked = RawTechLoader.SpawnRandomTechAtPosHead(pos, forwards, RawTechLoader.GetRandomBaseTeam(), out Tank tech, finalFaction, BaseTerrain.Space, unProvoked, AutoTerrain: false, Licences.GetLicense(KickStart.CorpExtToCorp(finalFaction)).CurrentLevel, maxPrice: KickStart.EnemySpawnPriceMatching);
                return tech;
            }
            catch { }
            Debug.Log("TACtical_AI: SpecialAISpawner - Could not fetch corps, resorting to random spawns");
            worked = true;
            return RawTechLoader.SpawnRandomTechAtPosHead(pos, forwards, RawTechLoader.GetRandomBaseTeam(), FactionTypesExt.NULL, BaseTerrain.Space, AutoTerrain: false, maxPrice: KickStart.EnemySpawnPriceMatching);
        }

        public static void TrySpawnTraderTroll(Vector3 pos)
        {   // Spawn trader trolls to make bigger techs fight harder

            Debug.Log("TACtical_AI: TrySpawnTraderTroll - Queued request at " + pos + "!");
            if (ManNetwork.IsNetworked && !ManNetwork.IsHost)
                return;

            if (UnityEngine.Random.Range(-50, 150) > KickStart.Difficulty)
                return;

            if (!RBases.IsLocationGridEmpty(pos))
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

                if (!RBases.TryFindExpansionLocationGrid(pos, pos + (UnityEngine.Random.insideUnitCircle.ToVector3XZ() * 128), out Vector3 pos3))
                    return;

                RawTechLoader.UseFactionSubTypes = true;
                RawTechLoader.SpawnSpecificTechSafe(pos3, Vector3.forward, trollTeam, new List<BasePurpose> { BasePurpose.Defense }, faction: factionSelect, maxGrade: Licences.GetLicense(KickStart.CorpExtToCorp(factionSelect)).CurrentLevel, maxPrice: KickStart.EnemySpawnPriceMatching, isPopulation: true);

                Debug.Log("TACtical_AI: TrySpawnTraderTroll - Spawned!");
                try
                {
                    Singleton.Manager<UIMPChat>.inst.AddMissionMessage("<b>Trader Troll ahead!</b>");
                }
                catch { }

                return;
            }
            catch { }
            Debug.Log("TACtical_AI: TrySpawnTraderTroll - Could not fetch corps, resorting to random spawns");

            if (!RBases.TryFindExpansionLocationGrid(pos, pos + (UnityEngine.Random.insideUnitCircle.ToVector3XZ() * 128), out Vector3 pos2))
                return;
            RawTechLoader.UseFactionSubTypes = true;
            RawTechLoader.SpawnSpecificTechSafe(pos2, Vector3.forward, trollTeam, new List<BasePurpose> { BasePurpose.Defense }, faction: FactionTypesExt.NULL, isPopulation: true);

            Debug.Log("TACtical_AI: TrySpawnTraderTroll - Spawned!");
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
            if (!KickStart.AllowAirEnemiesToSpawn)
                DestroyAllPooledAirborneAI();
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
                        Debug.Log("TACtical_AI: SpecialAISpawner - Removed and recycled " + airborneAI.name + " from AirPool as it may have despawned.");
                        step--;
                        count--;
                        deadairborneAICount++;
                    }
                    else if (airborneAI.trans.position.y > AIGlobals.AirMaxHeightOffset + Singleton.playerPos.y)
                    {
                        AirPool.RemoveAt(step);
                        Debug.Log("TACtical_AI: SpecialAISpawner - Removed and recycled " + airborneAI.name + " from AirPool as it flew above player distance.");
                        Purge(airborneAI);
                        step--;
                        count--;
                    }
                    else if ((airborneAI.boundsCentreWorldNoCheck - playerTank.boundsCentreWorldNoCheck).sqrMagnitude > AirDespawnDist * AirDespawnDist)
                    {
                        AirPool.RemoveAt(step);
                        Debug.Log("TACtical_AI: SpecialAISpawner - Removed and recycled " + airborneAI.name + " from AirPool as it left AirDespawnDist radius.");
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
                Debug.Log("TACtical_AI: SpecialAISpawner - Removed " + deadairborneAICount + " dead airborneAI(s) from AirPool");
        }
        private static void DestroyAllPooledAirborneAI()
        {   // 
            if (AirPool.Count == 0)
                return;
            foreach (TrackedAirborneAI airborneAI in AirPool)
            {
                if (airborneAI.airborneAI.IsNotNull())
                    Purge(airborneAI.airborneAI);
            }
            AirPool.Clear();
            Debug.Log("TACtical_AI: SpecialAISpawner - Destroyed all enemy pooled airborneAI");
        }
        private static void CollectPossibleAirborneAI()
        {   // 
            foreach (Tank tech in Singleton.Manager<ManTechs>.inst.CurrentTechs)
            {
                try
                {
                    if (tech.GetComponent<AIECore.TankAIHelper>() && tech.IsPopulation)
                    {
                        bool IsAirBorne = false;
                        if (tech.GetComponent<EnemyMind>())
                            if (tech.GetComponent<EnemyMind>().EvilCommander == EnemyHandling.Starship)
                                IsAirBorne = true;
                        if (tech.GetComponent<AIECore.TankAIHelper>().MovementController is AIControllerAir || IsAirBorne)
                        {
                            try
                            {
                                TrackedAirborneAI newAir = new TrackedAirborneAI(tech);
                                AirPool.Add(newAir);
                            }
                            catch
                            {
                                Debug.Log("TACtical_AI: SpecialAISpawner - Error on handling enemy airborne AI pool");
                            }
                        }
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// Remove a Tech from existance
        /// </summary>
        /// <param name="tech"></param>
        /// <param name="player"></param>
        internal static void Purge(Tank tech)
        {   // 
            Debug.Log("TACtical_AI: Purge - PURGED " + tech.name);
            if (ManNetwork.IsNetworked)
            {
                try
                {
                    TrackedVisible TV = ManVisible.inst.GetTrackedVisibleByHostID(tech.netTech.HostID);
                    Singleton.Manager<ManNetwork>.inst.SendToServer(TTMsgType.UnspawnTech, new UnspawnTechMessage
                    { m_HostID = TV.HostID });
                }
                catch { }
            }
            else
                tech.visible.RemoveFromGame();
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
                    { m_HostID = TV.HostID });
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
                Debug.Log("TACtical_AI: SpecialAISpawner - Activated special enemy spawns");
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
                Debug.Log("TACtical_AI: SpecialAISpawner - Deactivated special enemy spawns");
                thisActive = false;
            }
        }
        public void Update()
        {   // 
            //Debug.Log("TACtical_AI: SpecialAISpawner - ACTIVE!!!  time" + counter);
            bool doubleSpawnRate = false;
            try
            {
                doubleSpawnRate = Singleton.cameraTrans.position.y > AIGlobals.AirPromoteHeight;
            }
            catch { }
            if ((Singleton.Manager<ManPop>.inst.IsSpawningEnabled || forceOn) && counter > (AirSpawnInterval / (doubleSpawnRate ? 2 : 1)) / ((KickStart.Difficulty / 100) + 1.5f))
            {   // determine if we should spawn new one, also manage existing pooled airborneAIs
                //Debug.Log("TACtical_AI: SpecialAISpawner - Spawn lerp");
                if (KickStart.EnableBetterAI && KickStart.enablePainMode)
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
    }
}
