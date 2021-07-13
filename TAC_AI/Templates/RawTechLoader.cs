using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using TAC_AI.AI;
using TAC_AI.AI.Movement;

namespace TAC_AI.Templates
{
    public class BookmarkBuilder : MonoBehaviour
    {
        public string blueprint;
        public bool infBlocks;
        public FactionSubTypes faction;
        public bool unprovoked = false;
    }

    public static class RawTechLoader
    {
        static bool ForceSpawn = false;  // Test a specific base
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
            if (spawnerTank.GetComponent<AIControllerAir>())
            {
                SpawnAirBase(spawnerTank, pos, Team, GetEnemyBaseType(spawnerTank.GetMainCorp(), purpose, BaseTerrain.Air), haveBB);
                return;
            }
            else if (KickStart.isWaterModPresent)
            {
                if (AIEPathing.AboveTheSea(pos))
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
            block.InitNew();

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


        // Mobile Techs
        public static Tank SpawnRandomTechAtPosition(Vector3 pos, int Team, BaseTerrain type = BaseTerrain.Land)
        {
            return SpawnMobileTech(pos, Vector3.forward, Team, GetEnemyBaseType(FactionSubTypes.NULL, BasePurpose.NotABase, type));
        }
        public static Tank SpawnRandomTechAtPosHead(Vector3 pos, Vector3 heading, int Team, BaseTerrain type = BaseTerrain.Land)
        {
            return SpawnMobileTech(pos, heading, Team, GetEnemyBaseType(FactionSubTypes.NULL, BasePurpose.NotABase, type));
        }
        public static Tank SpawnRandomTechAtPosHead(Vector3 pos, Vector3 heading, int Team, FactionSubTypes factionType, BaseTerrain terrainType = BaseTerrain.Land, bool unProvoked = false, bool AutoTerrain = true, int maxGrade = 99)
        {
            return SpawnMobileTech(pos, heading, Team, GetEnemyBaseType(factionType, BasePurpose.NotABase, terrainType, maxGrade: maxGrade), false, unProvoked, AutoTerrain);
        }
        public static Tank SpawnMobileTech(Vector3 pos, Vector3 heading, int Team, SpawnBaseTypes inputSpawn, bool silentFail = true, bool unProvoked = false, bool AutoTerrain = true)
        {
            SpawnBaseTypes toSpawn = inputSpawn;
            if (!IsBaseTemplateAvailable(toSpawn))
            {
                if (silentFail)
                    return null;
                else
                {
                    Debug.Log("TACtical_AI: SpawnMobileTech - FAILIURE TO SPAWN TECH!!!");
                }
            }

            string baseBlueprint = GetBlueprint(toSpawn);
            Vector3 position = pos;
            if (AutoTerrain)
            {
                Singleton.Manager<ManWorld>.inst.GetTerrainHeight(pos, out float offset);
                position.y = offset;
            }
            Quaternion quat = Quaternion.LookRotation(heading, Vector3.up);

            TankBlock block = Singleton.Manager<ManSpawn>.inst.SpawnBlock(AIERepair.JSONToFirstBlock(baseBlueprint), position, quat);
            block.InitNew();

            Tank theTech;
            theTech = Singleton.Manager<ManSpawn>.inst.WrapSingleBlock(null, block, Team, GetEnglishName(toSpawn));

            theTech.FixupAnchors(true);
            var namesav = theTech.gameObject.AddComponent<BookmarkBuilder>();
            namesav.blueprint = baseBlueprint;
            namesav.infBlocks = GetEnemyBaseSupplies(toSpawn);
            namesav.faction = GetMainCorp(toSpawn);
            namesav.unprovoked = unProvoked;
            return theTech;
        }
        public static bool SpawnAttractTech(Vector3 pos, int Team, Vector3 facingDirect, BaseTerrain terrainType = BaseTerrain.Land, FactionSubTypes faction = FactionSubTypes.NULL, BasePurpose purpose = BasePurpose.NotABase, bool silentFail = true)
        {
            SpawnBaseTypes toSpawn = GetEnemyBaseType(faction, purpose, terrainType, true);
            if (!IsBaseTemplateAvailable(toSpawn))
            {
                if (silentFail)
                    return false;
                else
                { // try again with a different one 
                    int attempts;
                    for (attempts = 6; attempts > 0; attempts--)
                    {
                        toSpawn = GetEnemyBaseType(faction, purpose, terrainType, true);
                        if (IsBaseTemplateAvailable(toSpawn))
                            break;
                    }
                    if (attempts == 0)
                    {
                        Debug.Log("TACtical_AI: SpawnAttractTech - FAILIURE TO SPAWN ANY TECH!!!");
                        return false;
                    }
                }
            }

            string baseBlueprint = GetBlueprint(toSpawn);
            Vector3 position = pos;
            Quaternion quat = Quaternion.LookRotation(facingDirect, Vector3.up);

            TankBlock block = Singleton.Manager<ManSpawn>.inst.SpawnBlock(AIERepair.JSONToFirstBlock(baseBlueprint), position, quat);
            block.InitNew();

            Tank theTech;
            theTech = Singleton.Manager<ManSpawn>.inst.WrapSingleBlock(null, block, Team, GetEnglishName(toSpawn));
            
            Debug.Log("TACtical_AI: SpawnAttractTech - Spawned " + GetEnglishName(toSpawn));
            var namesav = theTech.gameObject.AddComponent<BookmarkBuilder>();
            namesav.blueprint = baseBlueprint;
            namesav.infBlocks = GetEnemyBaseSupplies(toSpawn);
            namesav.faction = GetMainCorp(toSpawn);
            return true;
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
        public static bool IsBaseTemplateAvailable(SpawnBaseTypes toSpawn)
        {
            return TempManager.techBases.TryGetValue(toSpawn, out BaseTemplate baseT);
        }
        public static BaseTemplate GetBaseTemplate(SpawnBaseTypes toSpawn)
        {
            if (TempManager.techBases.TryGetValue(toSpawn, out BaseTemplate baseT))
                return baseT;
            return TempManager.techBases.ElementAtOrDefault(1).Value;
        }
        public static SpawnBaseTypes GetEnemyBaseType(FactionSubTypes faction, BasePurpose purpose, BaseTerrain terra, bool searchAttract = false, int maxGrade = 99)
        {
            if (ForceSpawn && !searchAttract)
                return forcedBaseSpawn;

            try
            {
                // Filters
                List<KeyValuePair<SpawnBaseTypes, BaseTemplate>> canidates;
                if (faction == FactionSubTypes.NULL)
                {
                    canidates = TempManager.techBases.ToList();
                }
                else
                {
                    canidates = TempManager.techBases.ToList().FindAll
                        (delegate (KeyValuePair<SpawnBaseTypes, BaseTemplate> cand) { return cand.Value.faction == faction; });
                }

                canidates = canidates.FindAll(delegate (KeyValuePair<SpawnBaseTypes, BaseTemplate> cand)
                {
                    if (purpose == BasePurpose.AnyNonHQ)
                    {
                        if (cand.Value.purposes.Contains(BasePurpose.Headquarters))
                            return false;
                        return true;
                    }
                    if (purpose != BasePurpose.NotABase && cand.Value.purposes.Contains(BasePurpose.NotABase))
                        return false;
                    if (!searchAttract && cand.Value.purposes.Contains(BasePurpose.NoAutoSearch))
                        return false;
                    if (searchAttract && cand.Value.purposes.Contains(BasePurpose.NoWeapons))
                        return false;
                    if (cand.Value.purposes.Count == 0)
                        return false;
                    return cand.Value.purposes.Contains(purpose);
                }); 
                
                canidates = canidates.FindAll
                    (delegate (KeyValuePair<SpawnBaseTypes, BaseTemplate> cand) { return cand.Value.terrain == terra; });

                if (maxGrade != 99)
                {
                    canidates = canidates.FindAll
                        (delegate (KeyValuePair<SpawnBaseTypes, BaseTemplate> cand) { return cand.Value.IntendedGrade <= maxGrade; });
                }

                if (canidates.Count == 0)
                    return SpawnBaseTypes.NotAvail;

                // final list compiling
                List<SpawnBaseTypes> final = new List<SpawnBaseTypes>();

                foreach (KeyValuePair<SpawnBaseTypes, BaseTemplate> pair in canidates)
                    final.Add(pair.Key);

                final.Shuffle();

                return final.First();
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
                int lookup = TempManager.techBases.Values.ToList().FindIndex(delegate (BaseTemplate cand) { return cand.techName == Name; });
                if (lookup == -1) return SpawnBaseTypes.NotAvail;
                return TempManager.techBases.ElementAt(lookup).Key;
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
        public static FactionSubTypes GetMainCorp(SpawnBaseTypes toSpawn)
        {
            return GetBaseTemplate(toSpawn).faction;
        }
        public static int GetBaseStartingFunds(SpawnBaseTypes toSpawn)
        {
            return GetBaseTemplate(toSpawn).startingFunds;
        }
        public static bool IsHQ(SpawnBaseTypes toSpawn)
        {
            if (TempManager.techBases.TryGetValue(toSpawn, out BaseTemplate baseT))
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
