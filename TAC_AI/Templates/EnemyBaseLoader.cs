using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TAC_AI.Templates
{
    /*
    public class BookmarkName : MonoBehaviour
    {
        public string savedName;
        public string blueprint;
        public bool infBlocks;
    }
    */

    public static class EnemyBaseLoader
    {
        static bool ForceSpawn = true;  // Test a specific base
        static SpawnBaseTypes forcedBaseSpawn = SpawnBaseTypes.GSOMidBase;

        // Main initiation function
        public static void TrySpawnBase(Tank tank, AI.AIECore.TankAIHelper thisInst, BasePurpose purpose = BasePurpose.Harvesting)
        {
            if (!KickStart.AllowEnemiesToStartBases)
                return;

            MakeSureCanExistWithBase(tank);
            if (GetEnemyBaseCountForTeam(tank.Team) > 0)
                return; // want no base spam on world load

            if (GetEnemyBaseCount() >= KickStart.MaxEnemyBaseLimit)
            {
                int teamswatch = ReassignToRandomEnemyBaseTeam();
                if (teamswatch == -1)
                    return;
                tank.SetTeam(teamswatch);
                return;
            }

            Vector3 pos = (tank.rootBlockTrans.forward * (thisInst.lastTechExtents + 8)) + tank.boundsCentreWorldNoCheck;

            bool validLocation = true;
            foreach (Visible vis in Singleton.Manager<ManVisible>.inst.VisiblesTouchingRadius(pos, thisInst.lastTechExtents, new Bitfield<ObjectTypes>()))
            {
                if (vis.tank.IsNotNull())
                {
                    if (vis.tank != tank)
                        validLocation = false;
                }
            }
            if (!validLocation)
                return;


            // We validated?  
            //   Alright let's spawn the base!
            SpawnBaseAtPosition(tank, pos, tank.Team, purpose);
        }


        /// <summary>
        /// Spawns a LOYAL enemy base 
        /// - this means this shouldn't be called for capture base missions.
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="Team"></param>
        /// <param name="toSpawn"></param>
        public static void SpawnBaseAtPosition(Tank spawnerTank, Vector3 pos, int Team, BasePurpose purpose)
        {
            TryClearAreaForBase(pos);

            bool haveBB;
            switch (purpose)
            {
                case BasePurpose.Headquarters:
                case BasePurpose.Harvesting:
                case BasePurpose.TechProduction:
                    haveBB = true;
                    break;
                default:
                    haveBB = false;
                    break;
            }

            // Are we a defended HQ?
            if (purpose == BasePurpose.Headquarters)
            {   // Summon additional defenses
                SpawnBaseAtPosition(spawnerTank, pos + (Vector3.forward * 64), Team, BasePurpose.Defense);
                SpawnBaseAtPosition(spawnerTank, pos - (Vector3.forward * 64), Team, BasePurpose.Defense);
                SpawnBaseAtPosition(spawnerTank, pos + (Vector3.right * 64), Team, BasePurpose.Defense);
                SpawnBaseAtPosition(spawnerTank, pos - (Vector3.right * 64), Team, BasePurpose.Defense);
                Singleton.Manager<ManSFX>.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimHEPayTerminal);
            }

            // Now spawn teh main host
            if (spawnerTank.GetComponent<AI.AIEAirborne.AirAssistance>())
            {
                SpawnAirBase(spawnerTank, pos, Team, GetEnemyBaseType(spawnerTank.GetMainCorp(), purpose, BaseTerrain.Air), haveBB);
                return;
            }
            else if (KickStart.isWaterModPresent)
            {
                if (AI.AIEPathing.AboveTheSea(pos))
                {
                    SpawnSeaBase(spawnerTank, pos, Team, GetEnemyBaseType(spawnerTank.GetMainCorp(), purpose, BaseTerrain.Sea), haveBB);
                    return;
                }
            }
            SpawnLandBase(spawnerTank, pos, Team, GetEnemyBaseType(spawnerTank.GetMainCorp(), purpose, BaseTerrain.Land), haveBB);
        }
        public static void SpawnLandBase(Tank spawnerTank, Vector3 pos, int Team, SpawnBaseTypes toSpawn, bool storeBB)
        {
            Singleton.Manager<ManWorld>.inst.GetTerrainHeight(pos, out float offset);
            string baseBlueprint = GetBlueprint(toSpawn);
            Vector3 position = pos;
            position.y = offset;
            Quaternion quat = Quaternion.LookRotation(Vector3.forward, Vector3.up);

            TankBlock block = Singleton.Manager<ManSpawn>.inst.SpawnBlock(AI.AIERepair.JSONToFirstBlock(baseBlueprint), position, quat);

            Tank theBase;
            if (storeBB)
                theBase = Singleton.Manager<ManSpawn>.inst.WrapSingleBlock(null, block, Team, GetEnglishName(toSpawn) + " ¥¥" + GetBaseStartingFunds(toSpawn));
            else
                theBase = Singleton.Manager<ManSpawn>.inst.WrapSingleBlock(null, block, Team, GetEnglishName(toSpawn));
            
            
            theBase.FixupAnchors(true);
            /*;
            var namesav = theBase.gameObject.AddComponent<BookmarkName>();
            namesav.savedName = GetEnglishName(toSpawn);
            namesav.blueprint = baseBlueprint;
            namesav.infBlocks = GetEnemyBaseSupplies(toSpawn);
            */
        }
        public static void SpawnSeaBase(Tank spawnerTank, Vector3 pos, int Team, SpawnBaseTypes toSpawn, bool storeBB)
        {   // N/A!!! WIP!!!
            Debug.Log("TACtical_AI: - SpawnSeaBase: Tried to launch unfinished function");
        }
        public static void SpawnAirBase(Tank spawnerTank, Vector3 pos, int Team, SpawnBaseTypes toSpawn, bool storeBB)
        {   // N/A!!! WIP!!!
            Debug.Log("TACtical_AI: - SpawnAirBase: Tried to launch unfinished function");
        }


        public static void TryClearAreaForBase(Vector3 vector3)
        {   //N/A

        }
        public static bool GetEnemyBaseSupplies(SpawnBaseTypes toSpawn)
        {
            switch (toSpawn)
            {
                // GSO
                case SpawnBaseTypes.GSOMilitaryBase:
                case SpawnBaseTypes.GSOStarport:
                case SpawnBaseTypes.GSOTechFactory:
                // GC
                case SpawnBaseTypes.GCHeadquarters:
                case SpawnBaseTypes.GCProspectorHub:
                // VEN
                case SpawnBaseTypes.VENTuningShop:
                // HE
                case SpawnBaseTypes.HETankFactory:
                case SpawnBaseTypes.HECombatStation:
                case SpawnBaseTypes.HEAircraftGarrison:
                    return true;
                default:
                    return false;
            }
        }
        public static BaseTemplate GetBaseTemplate(SpawnBaseTypes toSpawn)
        {
            if (AllBaseTemplates.techBases.TryGetValue(toSpawn, out BaseTemplate baseT)) //Find(delegate (BaseTemplate cand) { return cand.baseType == toSpawn; });
                return baseT;
            return AllBaseTemplates.techBases.ElementAtOrDefault(1).Value;
        }
        public static SpawnBaseTypes GetEnemyBaseType(FactionSubTypes faction, BasePurpose purpose, BaseTerrain terra)
        {
            if (ForceSpawn)
                return forcedBaseSpawn;

            try
            {
                List<KeyValuePair<SpawnBaseTypes, BaseTemplate>> canidates = AllBaseTemplates.techBases.ToList().FindAll
                    (delegate (KeyValuePair<SpawnBaseTypes, BaseTemplate> cand) { return cand.Value.faction == faction; });

                canidates = canidates.FindAll(delegate (KeyValuePair<SpawnBaseTypes, BaseTemplate> cand)
                {
                    if (purpose == BasePurpose.AnyNonHQ)
                    {
                        if (cand.Value.purposes.Contains(BasePurpose.Headquarters))
                            return false;
                        return true;
                    }
                    if (cand.Value.purposes.Count == 0)
                        return false;
                    return cand.Value.purposes.Contains(purpose);
                }); 
                
                canidates = canidates.FindAll
                    (delegate (KeyValuePair<SpawnBaseTypes, BaseTemplate> cand) { return cand.Value.terrain == terra; });

                if (canidates.Count == 0)
                    return forcedBaseSpawn;
                List<SpawnBaseTypes> final = new List<SpawnBaseTypes>();
                foreach (KeyValuePair<SpawnBaseTypes, BaseTemplate> pair in canidates)
                    final.Add(pair.Key);

                return final.ElementAt(UnityEngine.Random.Range(0, canidates.Count - 1));
            }
            catch { } // we resort to legacy

            int lowerRANDRange = 1;
            int higherRANDRange = 20;
            if (faction == FactionSubTypes.GSO)
            {
                lowerRANDRange = 1;
                higherRANDRange = 6;
            }
            else if (faction == FactionSubTypes.GC)
            {
                lowerRANDRange = 7;
                higherRANDRange = 10;
            }
            else if (faction == FactionSubTypes.VEN)
            {
                lowerRANDRange = 11;
                higherRANDRange = 14;
            }
            else if (faction == FactionSubTypes.HE)
            {
                lowerRANDRange = 15;
                higherRANDRange = 20;
            }

            return (SpawnBaseTypes)UnityEngine.Random.Range(lowerRANDRange, higherRANDRange);
        }
        public static SpawnBaseTypes GetEnemyBaseTypeFromName(string Name)
        {
            try
            {
                int lookup = AllBaseTemplates.techBases.Values.ToList().FindIndex(delegate (BaseTemplate cand) { return cand.techName == Name; });
                if (lookup == -1) return SpawnBaseTypes.NotAvail;
                return AllBaseTemplates.techBases.ElementAt(lookup).Key;
            }
            catch 
            {
                return SpawnBaseTypes.NotAvail;
            }
        }
        public static string GetBlueprint(SpawnBaseTypes toSpawn)
        {
            return GetBaseTemplate(toSpawn).savedTech;
        }
        public static string GetEnglishName(SpawnBaseTypes toSpawn)
        {
            return GetBaseTemplate(toSpawn).techName;
        }
        public static int GetBaseStartingFunds(SpawnBaseTypes toSpawn)
        {
            return GetBaseTemplate(toSpawn).startingFunds;
        }
        public static bool IsHQ(SpawnBaseTypes toSpawn)
        {
            if (AllBaseTemplates.techBases.TryGetValue(toSpawn, out BaseTemplate baseT))
                return baseT.purposes.Contains(BasePurpose.Headquarters);
            return false;
        }


        public static int GetEnemyBaseCount()
        {
            int baseCount = 0;
            List<Tank> tanks = Singleton.Manager<ManTechs>.inst.CurrentTechs.ToList();
            for (int step = 0; step < tanks.Count; step++)
            {
                if (tanks.ElementAt(step).IsEnemy() && tanks.ElementAt(step).IsAnchored)
                    baseCount++;
            }
            return baseCount;
        }
        public static int GetEnemyBaseCountForTeam(int Team)
        {
            int baseCount = 0;
            List<Tank> tanks = Singleton.Manager<ManTechs>.inst.CurrentTechs.ToList();
            for (int step = 0; step < tanks.Count; step++)
            {
                if (tanks.ElementAt(step).IsEnemy() && tanks.ElementAt(step).IsAnchored && tanks.ElementAt(step).IsFriendly(Team))
                    baseCount++;
            }
            return baseCount;
        }

        public static void MakeSureCanExistWithBase(Tank tank)
        {
            if (!tank.IsFriendly(tank.Team))
            {
                int set = UnityEngine.Random.Range(5, 365);
                Debug.Log("TACtical_AI: Tech " + tank.name + " spawned team " + tank.Team + " that fights against themselves, setting to team " + set + " instead");
                tank.SetTeam(set);
            }
        }
        public static int ReassignToRandomEnemyBaseTeam()
        {
            List<Tank> tanks = Singleton.Manager<ManTechs>.inst.CurrentTechs.ToList();
            List<Tank> enemyBases = new List<Tank>();
            for (int step = 0; step < tanks.Count; step++)
            {
                if (tanks.ElementAt(step).IsEnemy() && tanks.ElementAt(step).IsAnchored)
                    enemyBases.Add(tanks.ElementAt(step));
            }
            if (enemyBases.Count == 0)
                return -1;
            int steppe = UnityEngine.Random.Range(0, enemyBases.Count - 1);
            
            return enemyBases.ElementAt(steppe).Team;
        }
    }
}
