using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TAC_AI.Templates
{
    public class SpecialAISpawner : MonoBehaviour
    {   //  We handle all the AI goodies here when Population Injector is N/A
        //      This module should ONLY be active (when initated) in Campaign mode!!!

        //      If you need to request access this to be opened to public for coding reasons, 
        //          please let LegioniteTerraTech know.  
        //      For tech-related concerns or additions, confront Legionite on the TerraTech Community Discord.

        private static SpecialAISpawner inst;
        private static ManLicenses Licences;

        private static Tank playerTank;
        private static List<Tank> AirPool = new List<Tank>();

        private static bool thisActive = false;
        private float counter = 0;

        const int MaxAircraftAllowed = 3;
        const float AirSpawnDist = 600;
        const float AirDespawnDist = 1000;

        public static void Initiate()
        {   // 
            inst = Instantiate(new GameObject("AISpawnerAux")).AddComponent<SpecialAISpawner>();
            Singleton.Manager<ManGameMode>.inst.ModeStartEvent.Subscribe(DetermineActiveOnMode);
            Singleton.Manager<ManTechs>.inst.PlayerTankChangedEvent.Subscribe(UpdatePlayerTank);
            Singleton.Manager<ManTechs>.inst.TankDestroyedEvent.Subscribe(PlayerTankDeathCheck);
        }
        public static void DetermineActiveOnMode(Mode mode)
        {   // 
            if (mode is ModeMain || mode is ModeMisc)
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
        public static void PlayerTankDeathCheck(Tank tank, ManDamage.DamageInfo oof)
        {   // 
            if (tank == playerTank)
            {   // Player could have been killed by aircraft - remove all enemies
                DestroyAllAircraft();
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

            Vector3 forwards = GetAirOffsetFromPosition(pos);

            Tank newAircraft = SpawnPrefabAircraft(pos, forwards);
            if (newAircraft == null)
            {
                Debug.Log("TACtical_AI: SpecialAISpawner - Could not spawn aircraft - Player has no corps unlocked!?!");
                return;
            }
            AirPool.Add(newAircraft);
        }
        private static Tank SpawnPrefabAircraft(Vector3 pos, Vector3 forwards)
        {   // 
            List<FactionSubTypes> factionsAvail = new List<FactionSubTypes>();
            if (Licences.GetLicense(FactionSubTypes.GSO).CurrentLevel >= 2)
                factionsAvail.Add(FactionSubTypes.GSO);
            // GC literally can't fly an aircraft
            //if (Licences.GetLicense(FactionSubTypes.GC).IsDiscovered && Licences.GetLicense(FactionSubTypes.GC).CurrentLevel >= 2)
            //    factionsAvail.Add(FactionSubTypes.GC);
            if (Licences.GetLicense(FactionSubTypes.VEN).IsDiscovered && Licences.GetLicense(FactionSubTypes.VEN).CurrentLevel >= 1)
                factionsAvail.Add(FactionSubTypes.VEN);
            if (Licences.GetLicense(FactionSubTypes.HE).IsDiscovered && Licences.GetLicense(FactionSubTypes.HE).CurrentLevel >= 1)
                factionsAvail.Add(FactionSubTypes.HE);
            if (Licences.GetLicense(FactionSubTypes.BF).IsDiscovered && Licences.GetLicense(FactionSubTypes.BF).CurrentLevel >= 0)
                factionsAvail.Add(FactionSubTypes.BF);
            if (factionsAvail.Count == 0)
                return null;


            // determine corp
            factionsAvail.Shuffle();
            FactionSubTypes finalFaction = factionsAvail.First();

            bool unProvoked = true;
            switch (finalFaction)
            {
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

            // spawn and return the aircraft
            return RawTechLoader.SpawnRandomTechAtPosHead(pos, forwards, -1, finalFaction, BaseTerrain.Air, unProvoked);
        }
        private static void ManageAircraft()
        {   // 
            int count = AirPool.Count();
            int deadAircraftCount = 0;
            for (int step = 0; count > step; step++)
            {
                Tank aircraft = AirPool[step];

                if (aircraft.IsNull() || !aircraft.visible.isActive)
                {
                    AirPool.Remove(aircraft);
                    step--;
                    count--;
                    deadAircraftCount++;
                }
                else if ((aircraft.boundsCentreWorldNoCheck - playerTank.boundsCentreWorldNoCheck).sqrMagnitude > AirDespawnDist * AirDespawnDist)
                {
                    AirPool.Remove(aircraft);
                    Debug.Log("TACtical_AI: SpecialAISpawner - Removed and recycled " + aircraft.name + " from AirPool as it left AirDespawnDist radius.");
                    aircraft.visible.RemoveFromGame();
                    step--;
                    count--;
                }
            }
            if (deadAircraftCount > 0)
                Debug.Log("TACtical_AI: SpecialAISpawner - Removed " + deadAircraftCount + " dead aircraft(s) from AirPool");
        }
        private static void DestroyAllAircraft()
        {   // 
            if (AirPool.Count == 0)
                return;
            foreach (Tank aircraft in AirPool)
            {
                if (aircraft.IsNotNull())
                    aircraft.visible.RemoveFromGame();
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
                inst.gameObject.SetActive(true);
                Debug.Log("TACtical_AI: SpecialAISpawner - Activated special enemy spawns");
            }
        }
        private static void Pause()
        {   // 
            if (thisActive)
            {
                inst.gameObject.SetActive(false);
                inst.counter = 0;
                Licences = null;
                Debug.Log("TACtical_AI: SpecialAISpawner - Deactivated special enemy spawns");
            }
        }
        private void Update()
        {   // 
            if (counter > 120 && Singleton.Manager<ManPop>.inst.IsSpawningEnabled)
            {   // determine if we should spawn new one, also manage existing pooled aircrafts
                TrySpawnAircraftInAir();
                ManageAircraft();
                counter = 0;
            }
            counter += Time.deltaTime;
        }


        // Utilities
        private static Vector3 GetAirOffsetFromPosition(Vector3 pos)
        {   // 
            float randAngle = UnityEngine.Random.Range(0, 360);
            Vector3 angleHeading = Quaternion.AngleAxis(randAngle, Vector3.up) * Vector3.forward;
            pos = pos + (angleHeading * AirSpawnDist);
            return angleHeading;
        }
    }
}
