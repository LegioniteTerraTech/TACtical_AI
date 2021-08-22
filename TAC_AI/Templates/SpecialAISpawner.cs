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
    public class TrackedairborneAI
    {
        public Tank airborneAI;
        public void Setup(Tank set, bool IsSpace = false)
        {   // 
            airborneAI = set;
            if (!IsSpace)
                airborneAI.SleepEvent.Subscribe(OnSleep);
        }
        public void OnSleep(bool yes)
        {   // It crashed 
            airborneAI.SleepEvent.Unsubscribe(OnSleep);
            SpecialAISpawner.AirPool.Remove(this);
            SpecialAISpawner.Eradicate(airborneAI);
        }
    }
    public class SpecialAISpawner : MonoBehaviour
    {   //  We handle all the AI goodies here when Population Injector is N/A
        //      This module should ONLY be active (when initated) in Campaign mode!!!

        //      If you need to request access this to be opened to public for coding reasons, 
        //          please let LegioniteTerraTech know.  
        //      For tech-related concerns or additions, confront Legionite on the TerraTech Community Discord.

        private static bool forceOn = false;    // spawn in creative no matter what

        private static SpecialAISpawner inst;
        private static ManLicenses Licences;

        private static Tank playerTank;

        /// <summary>
        /// AIRTECHS
        /// </summary>
        internal static List<TrackedairborneAI> AirPool = new List<TrackedairborneAI>();

        internal static bool thisActive = false;
        internal static bool CreativeMode = false;
        internal static bool IsAttract = false;
        private float counter = 0;
        private int updateTimer = 0;

        const int MaxAirborneAIAllowed = 4;
        const int AirborneAISpawnOdds = 15;   // Out of 100
        const int SpaceshipChance = 2;     // Out of 100
        const float AirSpawnDist = 400;
        const float AirDespawnDist = 475;
        const float AirSpawnInterval = 30;


        public static void Initiate()
        {   // 
            var startup = new GameObject("AISpawnerAux");
            startup.AddComponent<SpecialAISpawner>();
            inst = startup.GetComponent<SpecialAISpawner>();
            Singleton.Manager<ManGameMode>.inst.ModeStartEvent.Subscribe(DetermineActiveOnMode);
            Debug.Log("TACtical_AI: SpecialAISpawner - Initated!");
            startup.SetActive(false);
        }
        public static void DetermineActiveOnMode(Mode mode)
        {   // 
            AirPool.Clear();
            RawTechExporter.Reload();
            OverrideManPop.QueuedChangeToRagnarokPop();
            DebugRawTechSpawner.ShouldBeActive();
            if ((mode is ModeMain || mode is ModeMisc || mode is ModeCoOpCampaign || mode is ModeCoOpCreative) && (ManNetwork.inst.IsServer || !ManNetwork.inst.IsMultiplayer()))
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
            IsAttract = mode is ModeAttract;
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
            if (playerTank.IsNull())
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
                spawnSpace = UnityEngine.Random.Range(0, 10) < 1;
            }
            else
                spawnSpace = UnityEngine.Random.Range(0, 100) < SpaceshipChance;

            bool IsSpace = false;
            if (CreativeMode)
            {
                if (spawnSpace)
                {
                    newAirborneAI = RawTechLoader.SpawnRandomTechAtPosHead(pos, forwards, GetRANDTeam(), FactionSubTypes.NULL, BaseTerrain.Space, AutoTerrain: false);
                    IsSpace = true;
                }
                else
                    newAirborneAI = RawTechLoader.SpawnRandomTechAtPosHead(pos, forwards, GetRANDTeam(), FactionSubTypes.NULL, BaseTerrain.Air, AutoTerrain: false);
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
            if (!newAirborneAI.IsEnemy(newAirborneAI.Team))
                newAirborneAI.SetTeam(GetRANDTeam(), true);
            else
                newAirborneAI.SetTeam(-1, true);
            TrackedairborneAI newAir = new TrackedairborneAI();
            newAir.Setup(newAirborneAI, IsSpace);
            AirPool.Add(newAir);
        }
        private static Tank SpawnPrefabAircraft(Vector3 pos, Vector3 forwards)
        {   // 
            try
            {
                List<FactionSubTypes> factionsAvail = new List<FactionSubTypes>();
                
                if (Licences.GetLicense(FactionSubTypes.GSO).CurrentLevel >= 0)// flight grade is 2 but random spawns start at 0
                    factionsAvail.Add(FactionSubTypes.GSO);
                // GC literally can't fly an airborneAI
                if (Licences.GetLicense(FactionSubTypes.GC).IsDiscovered && Licences.GetLicense(FactionSubTypes.GC).CurrentLevel >= 2)
                    factionsAvail.Add(FactionSubTypes.GC);
                if (Licences.GetLicense(FactionSubTypes.VEN).IsDiscovered && Licences.GetLicense(FactionSubTypes.VEN).CurrentLevel >= 0)// flight grade is 1 but random spawns start at 0
                    factionsAvail.Add(FactionSubTypes.VEN);
                if (Licences.GetLicense(FactionSubTypes.HE).IsDiscovered && Licences.GetLicense(FactionSubTypes.HE).CurrentLevel >= 1)
                    factionsAvail.Add(FactionSubTypes.HE);
                if (Licences.GetLicense(FactionSubTypes.BF).IsDiscovered && Licences.GetLicense(FactionSubTypes.BF).CurrentLevel >= 0)
                    factionsAvail.Add(FactionSubTypes.BF);
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
                if (factionsAvail.Contains(FactionSubTypes.GC))
                    factionsAvail.Remove(FactionSubTypes.GC);
                if (factionsAvail.Contains(FactionSubTypes.EXP))
                    factionsAvail.Remove(FactionSubTypes.EXP);

                // spawn and return the airborneAI
                if (hasAllDone) // all corps unlocked by player
                    return RawTechLoader.SpawnRandomTechAtPosHead(pos, forwards, -1, FactionSubTypes.NULL, BaseTerrain.Air, maxPrice: KickStart.EnemySpawnPriceMatching);

                // if we don't have all corps possible maxed, we do the normal spawn

                // determine corp
                factionsAvail.Shuffle();
                FactionSubTypes finalFaction = factionsAvail.First();

                bool unProvoked = true;
                switch (finalFaction)
                {   // contains minimum grades (index) needed before flying parts become available
                    case FactionSubTypes.GSO:
                        if (Licences.GetLicense(FactionSubTypes.GSO).CurrentLevel >= 2)
                            unProvoked = false;
                        break;
                    case FactionSubTypes.VEN:
                        if (Licences.GetLicense(FactionSubTypes.VEN).CurrentLevel >= 1)
                            unProvoked = false;
                        break;
                    case FactionSubTypes.HE:
                        if (Licences.GetLicense(FactionSubTypes.HE).CurrentLevel >= 1)
                            unProvoked = false;
                        break;
                    case FactionSubTypes.BF:
                        unProvoked = false;
                        break;
                    default:
                        if (Licences.GetLicense(FactionSubTypes.GSO).CurrentLevel == 4)
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
                    if (RawTechLoader.SpawnRandomTechAtPosHead(pos, forwards, -1, new List<BasePurpose> { BasePurpose.NotStationary, BasePurpose.NoWeapons }, out Tank finalTank, finalFaction, BaseTerrain.Air, unProvoked, AutoTerrain: false, Licences.GetLicense(finalFaction).CurrentLevel, maxPrice: KickStart.EnemySpawnPriceMatching))
                        return finalTank;
                    else
                        return null;
                }
                // else we do default spawn
                return RawTechLoader.SpawnRandomTechAtPosHead(pos, forwards, -1, finalFaction, BaseTerrain.Air, unProvoked, AutoTerrain: false, Licences.GetLicense(finalFaction).CurrentLevel, maxPrice: KickStart.EnemySpawnPriceMatching);
            }
            catch { }
            Debug.Log("TACtical_AI: SpecialAISpawner - Could not fetch corps, resorting to random spawns");
            return RawTechLoader.SpawnRandomTechAtPosHead(pos, forwards, -1, FactionSubTypes.NULL, BaseTerrain.Air, AutoTerrain: false, maxPrice: KickStart.EnemySpawnPriceMatching);
        }
        private static Tank SpawnPrefabSpaceship(Vector3 pos, Vector3 forwards, out bool worked)
        {   // 
            worked = false;
            try
            {
                List<FactionSubTypes> factionsAvail = new List<FactionSubTypes>();

                if (Licences.GetLicense(FactionSubTypes.GSO).CurrentLevel >= 2)
                    factionsAvail.Add(FactionSubTypes.GSO);
                // GC literally can't fly an airborneAI
                if (Licences.GetLicense(FactionSubTypes.GC).IsDiscovered && Licences.GetLicense(FactionSubTypes.GC).CurrentLevel >= 2)
                    factionsAvail.Add(FactionSubTypes.GC);
                if (Licences.GetLicense(FactionSubTypes.VEN).IsDiscovered && Licences.GetLicense(FactionSubTypes.VEN).CurrentLevel >= 1)
                    factionsAvail.Add(FactionSubTypes.VEN);
                if (Licences.GetLicense(FactionSubTypes.HE).IsDiscovered && Licences.GetLicense(FactionSubTypes.HE).CurrentLevel >= 1)
                    factionsAvail.Add(FactionSubTypes.HE);
                if (Licences.GetLicense(FactionSubTypes.BF).IsDiscovered && Licences.GetLicense(FactionSubTypes.BF).CurrentLevel >= 0)
                    factionsAvail.Add(FactionSubTypes.BF);
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
                if (factionsAvail.Contains(FactionSubTypes.GC))
                    factionsAvail.Remove(FactionSubTypes.GC);
                if (factionsAvail.Contains(FactionSubTypes.EXP))
                    factionsAvail.Remove(FactionSubTypes.EXP);

                // spawn and return the airborneAI
                if (hasAllDone) // all corps unlocked by player
                    return RawTechLoader.SpawnRandomTechAtPosHead(pos, forwards, -1, FactionSubTypes.NULL, BaseTerrain.Space, maxPrice: KickStart.EnemySpawnPriceMatching);

                // if we don't have all corps possible maxed, we do the normal spawn

                // determine corp
                factionsAvail.Shuffle();
                FactionSubTypes finalFaction = factionsAvail.First();

                bool unProvoked = true;
                switch (finalFaction)
                {   // contains minimum grades (index) needed before flying parts become available
                    case FactionSubTypes.GSO:
                        if (Licences.GetLicense(FactionSubTypes.GSO).CurrentLevel >= 2)
                            unProvoked = false;
                        break;
                    case FactionSubTypes.VEN:
                        if (Licences.GetLicense(FactionSubTypes.VEN).CurrentLevel >= 1)
                            unProvoked = false;
                        break;
                    case FactionSubTypes.HE:
                        if (Licences.GetLicense(FactionSubTypes.HE).CurrentLevel >= 1)
                            unProvoked = false;
                        break;
                    case FactionSubTypes.BF:
                        unProvoked = false;
                        break;
                    default:
                        if (Licences.GetLicense(FactionSubTypes.GSO).CurrentLevel == 4)
                            unProvoked = false;
                        break;
                }

                try
                {
                    Singleton.Manager<UIMPChat>.inst.AddMissionMessage("<b>HUGE unidentified flying object spotted!</b>");
                }
                catch { }
                Debug.Log("TACtical_AI: There are now " + (AirPool.Count + 1) + " airborneAI present on-scene"); 
                worked = RawTechLoader.SpawnRandomTechAtPosHead(pos, forwards, -1, out Tank tech, finalFaction, BaseTerrain.Space, unProvoked, AutoTerrain: false, Licences.GetLicense(finalFaction).CurrentLevel, maxPrice: KickStart.EnemySpawnPriceMatching);
                return tech;
            }
            catch { }
            Debug.Log("TACtical_AI: SpecialAISpawner - Could not fetch corps, resorting to random spawns");
            worked = true;
            return RawTechLoader.SpawnRandomTechAtPosHead(pos, forwards, -1, FactionSubTypes.NULL, BaseTerrain.Space, AutoTerrain: false, maxPrice: KickStart.EnemySpawnPriceMatching);
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
                        step--;
                        count--;
                        deadairborneAICount++;
                    }
                    else if (airborneAI.trans.position.y > KickStart.AirMaxHeightOffset + Singleton.playerPos.y)
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
                catch { }
            }
            if (deadairborneAICount > 0)
                Debug.Log("TACtical_AI: SpecialAISpawner - Removed " + deadairborneAICount + " dead airborneAI(s) from AirPool");
        }
        private static void DestroyAllPooledAirborneAI()
        {   // 
            if (AirPool.Count == 0)
                return;
            foreach (TrackedairborneAI airborneAI in AirPool)
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
                if (tech.GetComponent<AIECore.TankAIHelper>() && tech.IsPopulation)
                {
                    if (tech.GetComponent<AIECore.TankAIHelper>().MovementController is AIControllerAir || tech.GetComponent<EnemyMind>().EvilCommander == EnemyHandling.Starship)
                    {
                        TrackedairborneAI newAir = new TrackedairborneAI();
                        newAir.Setup(tech);
                        AirPool.Add(newAir);
                    }
                }
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
                //tech.netTech.RequestRemoveFromGame(player, false);
                tech.netTech.RequestRemoveFromGame(tech.netTech.NetPlayer, false);
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
                //tech.netTech.SetToSelfDestruct(true, 0.1f);// only blows up cab
                tech.netTech.RequestRemoveFromGame(tech.netTech.NetPlayer, false);
            }
            else
            {
                foreach (TankBlock block in tech.blockman.IterateBlocks())
                {
                    block.damage.SelfDestruct(0.5f);
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
                doubleSpawnRate = Singleton.cameraTrans.position.y > KickStart.AirPromoteHeight;
            }
            catch { }
            if ((Singleton.Manager<ManPop>.inst.IsSpawningEnabled || forceOn) && counter > (AirSpawnInterval / (doubleSpawnRate ? 2 : 1)) / ((KickStart.Difficulty / 100) + 1.5f))
            {   // determine if we should spawn new one, also manage existing pooled airborneAIs
                //Debug.Log("TACtical_AI: SpecialAISpawner - Spawn lerp");
                if (KickStart.EnableBetterAI && KickStart.enablePainMode)
                {
                    if (KickStart.AllowAirEnemiesToSpawn && UnityEngine.Random.Range(-1, 101) < AirborneAISpawnOdds)
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
                ManagePooledAirborneAI();
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
        private static Vector3 GetRandAirAngle()
        {   // 
            float randAngle = UnityEngine.Random.Range(0, 360);
            Vector3 angleHeading = Quaternion.AngleAxis(randAngle, Vector3.up) * Vector3.forward;
            return angleHeading;
        }
        private static Vector3 GetAirOffsetFromPosition(Vector3 pos, Vector3 angleHeading)
        {   // 
            return AI.Movement.AIEPathing.OffsetFromGroundAAlt(pos + -(angleHeading * AirSpawnDist) + (Singleton.cameraTrans.forward * 25), 50);
        }
        private static int GetRANDTeam()
        {   // 
            return UnityEngine.Random.Range(30, 28170);
        }
    }
}
