using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TAC_AI.Templates
{
    public class TrackedAircraft
    {
        public Tank aircraft;
        public void Setup(Tank set)
        {   // 
            aircraft = set;
            aircraft.SleepEvent.Subscribe(OnSleep);
        }
        public void OnSleep(bool yes)
        {   // It crashed 
            aircraft.SleepEvent.Unsubscribe(OnSleep);
            foreach (TankBlock block in aircraft.blockman.IterateBlocks())
            {
                block.damage.SelfDestruct(0.5f);
            }
            aircraft.blockman.Disintegrate();
            SpecialAISpawner.AirPool.Remove(this);
        }
    }
    public class SpecialAISpawner : MonoBehaviour
    {   //  We handle all the AI goodies here when Population Injector is N/A
        //      This module should ONLY be active (when initated) in Campaign mode!!!

        //      If you need to request access this to be opened to public for coding reasons, 
        //          please let LegioniteTerraTech know.  
        //      For tech-related concerns or additions, confront Legionite on the TerraTech Community Discord.

        private static SpecialAISpawner inst;
        private static ManLicenses Licences;

        private static Tank playerTank;
        internal static List<TrackedAircraft> AirPool = new List<TrackedAircraft>();

        private static bool thisActive = false;
        private float counter = 0;
        private int updateTimer = 0;

        const int MaxAircraftAllowed = 4;
        const int AircraftSpawnOdds = 37;//15    // Out of 100
        const float AirSpawnDist = 400;
        const float AirDespawnDist = 475;
        const float AirSpawnInterval = 30;
        const float AirMaxHeightOffset = 175;

        private static bool forceOn = true;    // spawn in creative no matter what

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
            if ((mode is ModeMain || mode is ModeMisc || mode is ModeCoOpCampaign || mode is ModeCoOpCreative) && (ManNetwork.inst.IsServer || !ManNetwork.inst.IsMultiplayer()))
            {
                Resume();
            }
            else
            {
                Pause();
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
            foreach (Tank tech in Singleton.Manager<ManTechs>.inst.IteratePlayerTechsControllable())
            {
                if (tech.IsPlayer)
                {
                    playerTank = tech;
                    break;
                }
            }
        }
        public static void PlayerTankDeathCheck(Tank tank, ManDamage.DamageInfo oof)
        {   // 
            if (tank == playerTank && KickStart.Difficulty < 100)
            {   // Player could have been killed by aircraft - remove all enemies
                DestroyAllPooledAircraft();
                playerTank = null;
            }
        }


        private static void TrySpawnAircraftInAir()
        {   //  Spawns aircraft even when the parts required aren't available, but they will not
            //      attack unless provoked by the player or another enemy, which is unlikely.
            if (playerTank.IsNull())
                return;
            if (AirPool.Count >= MaxAircraftAllowed)
                return;

            Vector3 pos;
            if (playerTank.rbody.IsNotNull())
                pos = (playerTank.rbody.velocity * Time.deltaTime * 5) + playerTank.boundsCentreWorldNoCheck;
            else
                pos = playerTank.boundsCentreWorldNoCheck;

            Vector3 forwards = GetRandAirAngle();

            pos = GetAirOffsetFromPosition(pos, forwards);

            Tank newAircraft = SpawnPrefabAircraft(pos, forwards);
            if (newAircraft == null)
            {
                Debug.Log("TACtical_AI: SpecialAISpawner - Could not spawn aircraft - Player has no corps unlocked!?!");
                return;
            }
            TrackedAircraft newAir = new TrackedAircraft();
            newAir.Setup(newAircraft);
            AirPool.Add(newAir);
        }
        private static Tank SpawnPrefabAircraft(Vector3 pos, Vector3 forwards)
        {   // 
            try
            {
                List<FactionSubTypes> factionsAvail = new List<FactionSubTypes>();
                
                if (Licences.GetLicense(FactionSubTypes.GSO).CurrentLevel >= 2)
                    factionsAvail.Add(FactionSubTypes.GSO);
                // GC literally can't fly an aircraft
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

                // spawn and return the aircraft
                if (hasAllDone) // all corps unlocked by player
                    return RawTechLoader.SpawnRandomTechAtPosHead(pos, forwards, -1, FactionSubTypes.NULL, BaseTerrain.Air);

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
                Debug.Log("TACtical_AI: There are now " + (AirPool.Count + 1) + " aircraft present on-scene");
                return RawTechLoader.SpawnRandomTechAtPosHead(pos, forwards, -1, finalFaction, BaseTerrain.Air, unProvoked, AutoTerrain: false, Licences.GetLicense(finalFaction).CurrentLevel);
            }
            catch { }
            Debug.Log("TACtical_AI: SpecialAISpawner - Could not fetch corps, resorting to random spawns");
            return RawTechLoader.SpawnRandomTechAtPosHead(pos, forwards, -1, FactionSubTypes.NULL, BaseTerrain.Air, AutoTerrain: false);
        }
        private static void ManagePooledAircraft()
        {   // 
            if (!KickStart.AllowAirEnemiesToSpawn)
                DestroyAllPooledAircraft();
            int count = AirPool.Count();
            int deadAircraftCount = 0;
            for (int step = 0; count > step; step++)
            {
                try
                {
                    Tank aircraft = AirPool[step].aircraft;
                    float sqrMag = (aircraft.boundsCentreWorldNoCheck - playerTank.boundsCentreWorldNoCheck).sqrMagnitude;
                    if (aircraft.IsNull() || !aircraft.visible.isActive)
                    {
                        AirPool.RemoveAt(step);
                        step--;
                        count--;
                        deadAircraftCount++;
                    }
                    else if (aircraft.trans.position.y > AirMaxHeightOffset + Singleton.playerPos.y)
                    {
                        AirPool.RemoveAt(step);
                        Debug.Log("TACtical_AI: SpecialAISpawner - Removed and recycled " + aircraft.name + " from AirPool as it flew above player distance.");
                        aircraft.visible.RemoveFromGame();
                        step--;
                        count--;
                    }
                    else if ((aircraft.boundsCentreWorldNoCheck - playerTank.boundsCentreWorldNoCheck).sqrMagnitude > AirDespawnDist * AirDespawnDist)
                    {
                        AirPool.RemoveAt(step);
                        Debug.Log("TACtical_AI: SpecialAISpawner - Removed and recycled " + aircraft.name + " from AirPool as it left AirDespawnDist radius.");
                        aircraft.visible.RemoveFromGame();
                        step--;
                        count--;
                    }
                }
                catch { }
            }
            if (deadAircraftCount > 0)
                Debug.Log("TACtical_AI: SpecialAISpawner - Removed " + deadAircraftCount + " dead aircraft(s) from AirPool");
        }
        private static void DestroyAllPooledAircraft()
        {   // 
            if (AirPool.Count == 0)
                return;
            foreach (TrackedAircraft aircraft in AirPool)
            {
                if (aircraft.aircraft.IsNotNull())
                    aircraft.aircraft.visible.RemoveFromGame();
            }
            AirPool.Clear();
            Debug.Log("TACtical_AI: SpecialAISpawner - Destroyed all enemy pooled aircraft");
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
            }
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
            }
        }
        public void Update()
        {   // 
            //Debug.Log("TACtical_AI: SpecialAISpawner - ACTIVE!!!  time" + counter);
            if (counter > AirSpawnInterval / ((KickStart.Difficulty / 100) + 1.5f) && (Singleton.Manager<ManPop>.inst.IsSpawningEnabled || forceOn))
            {   // determine if we should spawn new one, also manage existing pooled aircrafts
                //Debug.Log("TACtical_AI: SpecialAISpawner - Spawn lerp");
                if (UnityEngine.Random.Range(-1, 101) < AircraftSpawnOdds && KickStart.AllowAirEnemiesToSpawn)
                    TrySpawnAircraftInAir();
                counter = 0;
            }
            if (updateTimer > 25)
            {   // manager timer
                ManagePooledAircraft();
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
            return AI.Movement.AIEPathing.ForceOffsetFromGroundA(pos + -(angleHeading * AirSpawnDist) + (Singleton.cameraTrans.forward * 25), 50);
        }
    }
}
