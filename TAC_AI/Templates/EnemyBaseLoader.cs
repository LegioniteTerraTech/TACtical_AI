using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TAC_AI.Templates
{
    public class BookmarkName : MonoBehaviour
    {
        public string savedName;
        public string blueprint;
        public bool infBlocks;
    }

    public static class EnemyBaseLoader
    {
        static bool ForceSpawn = true;
        static SpawnBaseTypes forcedBaseSpawn = SpawnBaseTypes.GSOMidBase;

        public static void TrySpawnBase(Tank tank, AI.AIECore.TankAIHelper thisInst)
        {
            if (!KickStart.AllowEnemiesToStartBases)
                return;

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


            MakeSureCanExistWithBase(tank);
            if (GetEnemyBaseCountForTeam(tank.Team) > 0)
                return; // want no base spam on world load
            // We validated?  
            //   Alright let's spawn the base!
            SpawnBaseAtPosition(tank, pos, tank.Team, GetEnemyBaseType(tank.GetMainCorp()));
        }

        /// <summary>
            /// Spawns a LOYAL enemy base 
            /// - this means this shouldn't be called for capture base missions.
            /// </summary>
            /// <param name="pos"></param>
            /// <param name="Team"></param>
            /// <param name="toSpawn"></param>
        public static void SpawnBaseAtPosition(Tank spawnerTank, Vector3 pos, int Team, SpawnBaseTypes toSpawn)
        {
            TryClearAreaForBase(pos);

            if (KickStart.isWaterModPresent)
            {
                if (AI.AIEPathing.AboveTheSea(pos))
                {
                    SpawnSeaBase(spawnerTank, pos, Team, toSpawn);
                    return;
                }
            }
            SpawnLandBase(spawnerTank, pos, Team, toSpawn);
        }
        public static void SpawnLandBase(Tank spawnerTank, Vector3 pos, int Team, SpawnBaseTypes toSpawn)
        {
            Singleton.Manager<ManWorld>.inst.GetTerrainHeight(pos, out float offset);
            string baseTemplate = GetBaseTemplate(toSpawn);
            Vector3 position = pos;
            position.y = offset;
            Quaternion quat = Quaternion.LookRotation(Vector3.forward, Vector3.up);

            TankBlock block = Singleton.Manager<ManSpawn>.inst.SpawnBlock(AI.AIERepair.JSONToFirstBlock(baseTemplate), position, quat);

            Tank theBase = Singleton.Manager<ManSpawn>.inst.WrapSingleBlock(null, block, Team, "INSTANTIATED_BASE");

            theBase.FixupAnchors(true);
            var namesav = theBase.gameObject.AddComponent<BookmarkName>();
            namesav.savedName = GetEnglishName(toSpawn);
            namesav.blueprint = baseTemplate;
            namesav.infBlocks = GetEnemyBaseSupplies(toSpawn);
        }
        public static void SpawnSeaBase(Tank spawnerTank, Vector3 pos, int Team, SpawnBaseTypes toSpawn)
        {   // N/A!!! WIP!!!
            Debug.Log("TACtical_AI: - SpawnSeaBase: Tried to launch unfinished function");
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
        public static SpawnBaseTypes GetEnemyBaseType(FactionSubTypes faction)
        {
            if (ForceSpawn)
            {
                return forcedBaseSpawn;
            }
            int lowerRANDRange = 0;
            int higherRANDRange = 18;
            if (faction == FactionSubTypes.GSO)
            {
                lowerRANDRange = 0;
                higherRANDRange = 4;
            }
            else if (faction == FactionSubTypes.GC)
            {
                lowerRANDRange = 5;
                higherRANDRange = 8;
            }
            else if (faction == FactionSubTypes.VEN)
            {
                lowerRANDRange = 9;
                higherRANDRange = 12;
            }
            else if (faction == FactionSubTypes.HE)
            {
                lowerRANDRange = 13;
                higherRANDRange = 18;
            }

            return (SpawnBaseTypes)UnityEngine.Random.Range(lowerRANDRange, higherRANDRange);
        }
        public static string GetBaseTemplate(SpawnBaseTypes toSpawn)
        {
            switch (toSpawn)
            {
                case SpawnBaseTypes.GSOSeller:
                    return LandBaseTemplates.GSOSeller;
                case SpawnBaseTypes.GSOMidBase:
                    return LandBaseTemplates.GSOMidBase;
                //WIP!!!
                case SpawnBaseTypes.GSOAIMinerProduction:
                    return LandBaseTemplates.GSOSeller;
                default:
                    return LandBaseTemplates.GSOSeller;
            }
        }
        public static string GetEnglishName(SpawnBaseTypes toSpawn)
        {
            switch (toSpawn)
            {
                case SpawnBaseTypes.GSOSeller:
                    return "GSO Seller Base";
                case SpawnBaseTypes.GSOMidBase:
                    return "GSO Furlough Base";
                //WIP!!!
                case SpawnBaseTypes.GSOAIMinerProduction:
                    return "GSO Production";
                default:
                    return "error on load";
            }
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
