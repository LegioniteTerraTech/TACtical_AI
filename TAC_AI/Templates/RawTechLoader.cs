using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using TAC_AI.AI;
using TAC_AI.AI.Movement;
using TAC_AI.AI.Enemy;

namespace TAC_AI.Templates
{
    internal class BookmarkBuilder : MonoBehaviour
    {
        public string blueprint;
        public bool infBlocks;
        public FactionSubTypes faction;
        public bool unprovoked = false;
        public bool instant = true;
    }

    public static class RawTechLoader
    {
        const float MinimumBaseSpacing = 80;
        const int MaxBlockLimitAttract = 128;

        static bool ForceSpawn = false;  // Test a specific base
        static SpawnBaseTypes forcedBaseSpawn = SpawnBaseTypes.GSOMidBase;

        // Main initiation function
        internal static void TrySpawnBase(Tank tank, AIECore.TankAIHelper thisInst, BasePurpose purpose = BasePurpose.Harvesting)
        {
            if (!KickStart.AllowEnemiesToStartBases)
                return;
            if (Singleton.Manager<ManNetwork>.inst.IsMultiplayer() && !Singleton.Manager<ManNetwork>.inst.IsServer)
                return; // no want each client to have enemies spawn in new bases - stacked base incident!

            MakeSureCanExistWithBase(tank);

            if (GetEnemyBaseCountSearchRadius(tank.boundsCentreWorldNoCheck, MinimumBaseSpacing) >= KickStart.MaxEnemyBaseLimit)
            {
                int teamswatch = ReassignToRandomEnemyBaseTeam();
                if (teamswatch == -1)
                    return;
                tank.SetTeam(teamswatch);
                TryRemoveFromPop(tank);
                return;
            }

            if (GetEnemyBaseCountForTeam(tank.Team) > 0)
                return; // want no base spam on world load

            Vector3 pos = (tank.rootBlockTrans.forward * (thisInst.lastTechExtents + 8)) + tank.boundsCentreWorldNoCheck;

            if (!IsRadiusClearOfTechObst(tank, pos, thisInst.lastTechExtents))
            {   // try behind
                pos = (-tank.rootBlockTrans.forward * (thisInst.lastTechExtents + 8)) + tank.boundsCentreWorldNoCheck;

                if (!IsRadiusClearOfTechObst(tank, pos, thisInst.lastTechExtents))
                    return;
            }


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
        internal static int SpawnBaseAtPosition(Tank spawnerTank, Vector3 pos, int Team, BasePurpose purpose)
        {
            TryClearAreaForBase(pos);

            // this shouldn't be able to happen without being the server or being in single player
            bool haveBB;
            switch (purpose)
            {
                case BasePurpose.Headquarters:
                    haveBB = true;
                    try
                    {
                        WorldPosition pos2 = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(spawnerTank.visible);
                        Singleton.Manager<ManOverlay>.inst.AddFloatingTextOverlay("Enemy HQ!", pos2);

                        Singleton.Manager<UIMPChat>.inst.AddMissionMessage("Enemy HQ Spotted!");
                        Singleton.Manager<UIMPChat>.inst.AddMissionMessage("Protect your terra prospectors!!");
                    }
                    catch { }
                    break;
                case BasePurpose.HarvestingNoHQ:
                case BasePurpose.Harvesting:
                case BasePurpose.TechProduction:
                case BasePurpose.AnyNonHQ:
                    haveBB = true;

                    try
                    {
                        WorldPosition pos3 = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(spawnerTank.visible);
                        Singleton.Manager<ManOverlay>.inst.AddFloatingTextOverlay("Rival!", pos3);

                        Singleton.Manager<UIMPChat>.inst.AddMissionMessage("Rival Prospector Spotted!");
                        Singleton.Manager<UIMPChat>.inst.AddMissionMessage("Protect your terra prospectors!!");
                    }
                    catch { }
            break;
                default:
                    haveBB = false;
                    break;
            }

            // Are we a defended HQ?
            int extraBB = 0;
            if (purpose == BasePurpose.Headquarters)
            {   // Summon additional defenses - DO NOT LET THIS RECURSIVELY TRIGGER!!!
                extraBB += SpawnBaseAtPosition(spawnerTank, pos + (Vector3.forward * 64), Team, BasePurpose.Defense);
                extraBB += SpawnBaseAtPosition(spawnerTank, pos - (Vector3.forward * 64), Team, BasePurpose.Defense);
                extraBB += SpawnBaseAtPosition(spawnerTank, pos + (Vector3.right * 64), Team, BasePurpose.Defense);
                extraBB += SpawnBaseAtPosition(spawnerTank, pos - (Vector3.right * 64), Team, BasePurpose.Defense);
                Singleton.Manager<ManSFX>.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimHEPayTerminal);
            }

            // Now spawn teh main host
            if (spawnerTank.GetComponent<AIControllerAir>())
            {
                return SpawnAirBase(spawnerTank, pos, Team, GetEnemyBaseType(spawnerTank.GetMainCorp(), purpose, BaseTerrain.Air), haveBB, extraBB);
            }
            else if (KickStart.isWaterModPresent)
            {
                if (AIEPathing.AboveTheSea(pos))
                {
                    return SpawnSeaBase(spawnerTank, pos, Team, GetEnemyBaseType(spawnerTank.GetMainCorp(), purpose, BaseTerrain.Sea), haveBB, extraBB);
                }
            }
            return SpawnLandBase(spawnerTank, pos, Team, GetEnemyBaseType(spawnerTank.GetMainCorp(), purpose, BaseTerrain.Land), haveBB, extraBB);
        }
        internal static int SpawnLandBase(Tank spawnerTank, Vector3 pos, int Team, SpawnBaseTypes toSpawn, bool storeBB, int ExtraBB = 0)
        {
            Singleton.Manager<ManWorld>.inst.GetTerrainHeight(pos, out float offset);
            string baseBlueprint = GetBlueprint(toSpawn);
            Vector3 position = pos;
            position.y = offset;
            Quaternion quat = Quaternion.LookRotation(Vector3.forward, Vector3.up);

            TankBlock block = SpawnBlockS(AIERepair.JSONToFirstBlock(baseBlueprint), position, quat);

            Tank theBase;
            if (storeBB)
                theBase = TechFromBlock(block, Team, GetEnglishName(toSpawn) + " ¥¥" + (GetBaseStartingFunds(toSpawn) + ExtraBB));
            else
            {
                theBase = TechFromBlock(block, Team, GetEnglishName(toSpawn) + " â");
            }
            
            
            theBase.FixupAnchors(true);
            var namesav = theBase.gameObject.AddComponent<BookmarkBuilder>();
            namesav.blueprint = baseBlueprint;
            namesav.infBlocks = GetEnemyBaseSupplies(toSpawn);
            namesav.faction = GetMainCorp(toSpawn);
            namesav.unprovoked = false;
            namesav.instant = false;
            return GetBaseBBCost(baseBlueprint);
        }
        internal static int SpawnSeaBase(Tank spawnerTank, Vector3 pos, int Team, SpawnBaseTypes toSpawn, bool storeBB, int ExtraBB = 0)
        {   // N/A!!! WIP!!!
            //Debug.Log("TACtical_AI: - SpawnSeaBase: Tried to launch unfinished function");
            Vector3 position = AIEPathing.ForceOffsetToSea(pos);
            string baseBlueprint = GetBlueprint(toSpawn);
            Quaternion quat = Quaternion.LookRotation(Vector3.forward, Vector3.up);

            TankBlock block = SpawnBlockS(AIERepair.JSONToFirstBlock(baseBlueprint), position, quat);

            Tank theBase;
            if (storeBB)
                theBase = TechFromBlock(block, Team, GetEnglishName(toSpawn) + " ¥¥" + (GetBaseStartingFunds(toSpawn) + ExtraBB));
            else
            {
                theBase = TechFromBlock(block, Team, GetEnglishName(toSpawn) + " â");
            }


            theBase.FixupAnchors(true);
            var namesav = theBase.gameObject.AddComponent<BookmarkBuilder>();
            namesav.blueprint = baseBlueprint;
            namesav.infBlocks = GetEnemyBaseSupplies(toSpawn);
            namesav.faction = GetMainCorp(toSpawn);
            namesav.unprovoked = false;
            namesav.instant = false;
            return GetBaseBBCost(baseBlueprint);
        }
        internal static int SpawnAirBase(Tank spawnerTank, Vector3 pos, int Team, SpawnBaseTypes toSpawn, bool storeBB, int ExtraBB = 0)
        {   // N/A!!! WIP!!!
            //Debug.Log("TACtical_AI: - SpawnAirBase: Tried to launch unfinished function");
            Vector3 position = AIEPathing.ForceOffsetToSea(pos);
            string baseBlueprint = GetBlueprint(toSpawn);
            Quaternion quat = Quaternion.LookRotation(Vector3.forward, Vector3.up);

            TankBlock block = SpawnBlockS(AIERepair.JSONToFirstBlock(baseBlueprint), position, quat);

            Tank theBase;
            if (storeBB)
                theBase = TechFromBlock(block, Team, GetEnglishName(toSpawn) + " ¥¥" + (GetBaseStartingFunds(toSpawn) + ExtraBB));
            else
            {
                theBase = TechFromBlock(block, Team, GetEnglishName(toSpawn) + " â");
            }


            theBase.FixupAnchors(true);
            var namesav = theBase.gameObject.AddComponent<BookmarkBuilder>();
            namesav.blueprint = baseBlueprint;
            namesav.infBlocks = GetEnemyBaseSupplies(toSpawn);
            namesav.faction = GetMainCorp(toSpawn);
            namesav.unprovoked = false;
            namesav.instant = false;
            return GetBaseBBCost(baseBlueprint);
        }


        // Mobile Enemy Techs
        internal static Tank SpawnRandomTechAtPosHead(Vector3 pos, Vector3 heading, int Team, FactionSubTypes factionType = FactionSubTypes.NULL, BaseTerrain terrainType = BaseTerrain.Land, bool unProvoked = false, bool AutoTerrain = true, int maxGrade = 99)
        {   // This will try to spawn player-made enemy techs as well

            Tank outTank;
            if (ShouldUseCustomTechs(factionType, BasePurpose.NotStationary, terrainType, false, maxGrade))
            {
                outTank = SpawnEnemyTechExternal(pos, Team, heading, TempManager.ExternalEnemyTechs[GetExternalIndex(factionType, BasePurpose.NotStationary, terrainType, maxGrade: maxGrade)], unProvoked, AutoTerrain);
            }
            else
            {
                outTank = SpawnMobileTech(pos, heading, Team, GetEnemyBaseType(factionType, BasePurpose.NotStationary, terrainType, maxGrade: maxGrade), false, unProvoked, AutoTerrain);
            }

            return outTank; 
        }
        internal static Tank SpawnMobileTech(Vector3 pos, Vector3 heading, int Team, SpawnBaseTypes inputSpawn, bool silentFail = true, bool unProvoked = false, bool AutoTerrain = true)
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

            TankBlock block = SpawnBlockS(AIERepair.JSONToFirstBlock(baseBlueprint), position, quat);

            Tank theTech;
            theTech = TechFromBlock(block, Team, GetEnglishName(toSpawn));

            theTech.FixupAnchors(true);
            var namesav = theTech.gameObject.AddComponent<BookmarkBuilder>();
            namesav.blueprint = baseBlueprint;
            namesav.infBlocks = GetEnemyBaseSupplies(toSpawn);
            namesav.faction = GetMainCorp(toSpawn);
            namesav.unprovoked = unProvoked;

            return theTech;
        }
        internal static bool SpawnAttractTech(Vector3 pos, int Team, Vector3 facingDirect, BaseTerrain terrainType = BaseTerrain.Land, FactionSubTypes faction = FactionSubTypes.NULL, BasePurpose purpose = BasePurpose.NotStationary, bool silentFail = true)
        {
            if (ShouldUseCustomTechs(faction, BasePurpose.NotStationary, terrainType, true))
            {
                return SpawnEnemyTechExternal(pos, Team, facingDirect, TempManager.ExternalEnemyTechs[GetExternalIndex(faction, BasePurpose.NotStationary, terrainType, true)]);
            }
            else
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

                TankBlock block = SpawnBlockS(AIERepair.JSONToFirstBlock(baseBlueprint), position, quat);

                Tank theTech;
                theTech = TechFromBlock(block, Team, GetEnglishName(toSpawn));

                Debug.Log("TACtical_AI: SpawnAttractTech - Spawned " + GetEnglishName(toSpawn));
                var namesav = theTech.gameObject.AddComponent<BookmarkBuilder>();
                namesav.blueprint = baseBlueprint;
                namesav.infBlocks = GetEnemyBaseSupplies(toSpawn);
                namesav.faction = GetMainCorp(toSpawn);
                return true;
            }
        }


        // imported ENEMY cases
        internal static Tank SpawnEnemyTechExternal(Vector3 pos, int Team, Vector3 facingDirect, BaseTemplate Blueprint, bool unProvoked = false, bool AutoTerrain = true)
        {
            string baseBlueprint = Blueprint.savedTech;
            Vector3 position = pos;
            if (AutoTerrain)
            {
                Singleton.Manager<ManWorld>.inst.GetTerrainHeight(pos, out float offset);
                position.y = offset;
            }
            Quaternion quat = Quaternion.LookRotation(facingDirect, Vector3.up);

            TankBlock block = SpawnBlockS(AIERepair.JSONToFirstBlock(baseBlueprint), position, quat);

            Tank theTech;
            theTech = TechFromBlock(block, Team, Blueprint.techName);

            Debug.Log("TACtical_AI: SpawnTechExternal - Spawned " + Blueprint.techName);
            var namesav = theTech.gameObject.AddComponent<BookmarkBuilder>();
            namesav.blueprint = baseBlueprint;
            namesav.infBlocks = false;
            namesav.faction = Blueprint.faction;
            namesav.unprovoked = unProvoked;

            return theTech;
        }
        internal static List<int> GetExternalIndexes(FactionSubTypes faction, BasePurpose purpose, BaseTerrain terra, bool searchAttract = false, int maxGrade = 99)
        {
            try
            {
                // Filters
                List<BaseTemplate> canidates;
                if (faction == FactionSubTypes.NULL)
                {
                    canidates = TempManager.ExternalEnemyTechs;
                }
                else
                {
                    canidates = TempManager.ExternalEnemyTechs.FindAll
                        (delegate (BaseTemplate cand) { return cand.faction == faction; });
                }

                canidates = canidates.FindAll(delegate (BaseTemplate cand)
                {
                    if (purpose == BasePurpose.AnyNonHQ)
                    {
                        if (cand.purposes.Contains(BasePurpose.Headquarters))
                            return false;
                        return true;
                    }
                    if (purpose != BasePurpose.NotStationary && cand.purposes.Contains(BasePurpose.NotStationary))
                        return false;
                    if (!searchAttract && cand.purposes.Contains(BasePurpose.NoAutoSearch))
                        return false;
                    if (searchAttract && cand.purposes.Contains(BasePurpose.NoWeapons))
                        return false;
                    if (cand.purposes.Count == 0)
                        return false;
                    return cand.purposes.Contains(purpose);
                });

                if (terra == BaseTerrain.AnyNonSea)
                {
                    canidates = canidates.FindAll
                        (delegate (BaseTemplate cand) { return cand.terrain != BaseTerrain.Sea; });
                }
                else
                {
                    canidates = canidates.FindAll
                        (delegate (BaseTemplate cand) { return cand.terrain == terra; });
                }

                if (maxGrade != 99)
                {
                    canidates = canidates.FindAll
                        (delegate (BaseTemplate cand) { return cand.IntendedGrade <= maxGrade; });
                }
                if (searchAttract)
                {   // prevent laggy techs from entering
                    canidates = canidates.FindAll
                        (delegate (BaseTemplate cand) { return AIERepair.DesignMemory.JSONToTechExternal(cand.savedTech).Count <= MaxBlockLimitAttract; });
                }

                if (canidates.Count == 0)
                    return new List<int> { -1 };

                // final list compiling
                List<int> final = new List<int>();
                foreach (BaseTemplate temp in canidates)
                    final.Add(TempManager.ExternalEnemyTechs.IndexOf(temp));

                final.Shuffle();

                return final;
            }
            catch { }

            return new List<int> { -1 };
        }
        internal static int GetExternalIndex(FactionSubTypes faction, BasePurpose purpose, BaseTerrain terra, bool searchAttract = false, int maxGrade = 99)
        {
            try
            {
                return GetExternalIndexes(faction, purpose, terra, searchAttract, maxGrade).GetRandomEntry();
            }
            catch { }

            return -1;
        }
        internal static bool ShouldUseCustomTechs(FactionSubTypes faction, BasePurpose purpose, BaseTerrain terra, bool searchAttract = false, int maxGrade = 99)
        {
            int CustomTechs = GetExternalIndexes(faction, purpose, terra, searchAttract, maxGrade).Count;
            int PrefabTechs = GetEnemyBaseTypes(faction, purpose, terra, searchAttract, maxGrade).Count;

            int CombinedVal = CustomTechs + PrefabTechs;

            float RAND = UnityEngine.Random.Range(0, CombinedVal);
            if (RAND > PrefabTechs)
            {
                return true;
            }
            return false;
        }

        // player cases - rebuild for bote
        internal static void StripPlayerTechOfBlocks(SpawnBaseTypes techType)
        {
            Tank tech = Singleton.playerTank;
            int playerTeam = tech.Team;
            Vector3 playerPos = tech.transform.position;
            Quaternion playerFacing = tech.transform.rotation;
            tech.visible.RemoveFromGame();
            /*
            List<TankBlock> toRemove = new List<TankBlock>();
            foreach (TankBlock block in tech.blockman.IterateBlocks())
            { 
                if (block != tech.blockman.GetRootBlock())
                {
                    toRemove.Add(block);
                }
            }
            int fireTimes = toRemove.Count;
            for (int step = 0; step < fireTimes; step++)
            {
                try
                {
                    toRemove.First().Separate();
                    toRemove.First().visible.RemoveFromGame();
                }
                catch { }
            }*/
            TankBlock block = SpawnBlockS(AIERepair.JSONToFirstBlock(GetBlueprint(techType)), playerPos, playerFacing);

            Tank theTech;
            theTech = TechFromBlock(block, playerTeam, GetEnglishName(techType));

            Singleton.Manager<ManTechs>.inst.RequestSetPlayerTank(theTech);
        }
        internal static void ReconstructPlayerTech(SpawnBaseTypes techType, SpawnBaseTypes fallbackTechType)
        {
            SpawnBaseTypes toSpawn;
            if (IsBaseTemplateAvailable(techType))
            {
                toSpawn = techType;
            }
            else if (IsBaseTemplateAvailable(fallbackTechType))
            {
                toSpawn = fallbackTechType;
            }
            else
            {
                Debug.Log("TACtical_AI: ReconstructPlayerTech - Failed, could not find main or fallback!");
                return; // compromised - cannot load anything!
            }
            StripPlayerTechOfBlocks(toSpawn);

            string baseBlueprint = GetBlueprint(techType);

            Tank theTech = Singleton.playerTank;

            AIERepair.TurboconstructExt(theTech, AIERepair.DesignMemory.JSONToTechExternal(baseBlueprint), false);
            Debug.Log("TACtical_AI: ReconstructPlayerTech - Retrofitted player FTUE tech to " + GetEnglishName(toSpawn));
        }



        // Use this for external cases
        public static Tank SpawnTechExternal(Vector3 pos, int Team, Vector3 facingDirect, BuilderExternal Blueprint, bool AutoTerrain = false, bool Charged = false)
        {
            if (Blueprint == null)
            {
                Debug.Log("TACtical_AI: SpawnTechExternal - Was handed a NULL Blueprint! \n" + StackTraceUtility.ExtractStackTrace());
                return null;
            }
            string baseBlueprint = Blueprint.Blueprint;
            Vector3 position = pos;
            if (AutoTerrain)
            {
                Singleton.Manager<ManWorld>.inst.GetTerrainHeight(pos, out float offset);
                position.y = offset;
            }
            Quaternion quat = Quaternion.LookRotation(facingDirect, Vector3.up);

            TankBlock block = SpawnBlockS(AIERepair.JSONToFirstBlock(baseBlueprint), position, quat);

            Tank theTech;
            theTech = TechFromBlock(block, Team, Blueprint.Name);
            Debug.Log("TACtical_AI: SpawnTechExternal - Spawned " + Blueprint.Name + " at " + pos + ". Snapped to terrain " + AutoTerrain);

            AIERepair.TurboconstructExt(theTech, AIERepair.DesignMemory.JSONToTechExternal(baseBlueprint), Charged);

            if (Team == -2)//neutral
            {   // be crafty mike and face the player
                theTech.AI.SetBehaviorType(AITreeType.AITypes.FacePlayer);
            }
            if (theTech.IsEnemy())//enemy
            {
                var namesav = theTech.gameObject.AddComponent<BookmarkBuilder>();
                namesav.blueprint = baseBlueprint;
                namesav.infBlocks = Blueprint.InfBlocks;
                namesav.faction = Blueprint.Faction;
                namesav.unprovoked = Blueprint.NonAggressive;
            }

            return theTech;
        }

        public static Tank TechTransformer(Tank tech, string JSONTechBlueprint)
        {
            int playerTeam = tech.Team;
            string OGName = tech.name;
            Vector3 techPos = tech.transform.position;
            Quaternion techFacing = tech.transform.rotation;
            tech.visible.RemoveFromGame();
            TankBlock block = SpawnBlockS(AIERepair.JSONToFirstBlock(JSONTechBlueprint), techPos, techFacing);

            Tank theTech;
            theTech = TechFromBlock(block, playerTeam, OGName);
            return theTech;
        }



        // Override
        internal static TankBlock SpawnBlockS(BlockTypes type, Vector3 position, Quaternion quat)
        {
            return Singleton.Manager<ManLooseBlocks>.inst.HostSpawnBlock(type, position, quat, true);
        }
        internal static Tank TechFromBlock(TankBlock block, int Team, string name)
        {
            //if (ManNetwork.inst.IsMultiplayer)
            //    Team = ManSpawn.inst.gette
            Tank theTech = Singleton.Manager<ManSpawn>.inst.WrapSingleBlock(null, block, Team, name);
            TryForceIntoPop(theTech);
            return theTech;
        }
        
        private static FieldInfo forceInsert = typeof(ManPop).GetField("m_SpawnedTechs", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static void TryForceIntoPop(Tank tank)
        {
            if (tank.Team == -1) // the wild tech pop number
            {
                TrackedVisible tracked = new TrackedVisible(tank.visible.ID, tank.visible, ObjectTypes.Vehicle, RadarTypes.Vehicle);
                ManVisible.inst.TrackVisible(tracked);
                List<TrackedVisible> visT = (List<TrackedVisible>)forceInsert.GetValue(ManPop.inst);
                visT.Add(tracked);
                forceInsert.SetValue(ManPop.inst, visT);
                Debug.Log("TACtical_AI: RawTechLoader - Forced " + tank.name + " into population");
            }
        }
        internal static void TryRemoveFromPop(Tank tank)
        {
            if (tank.Team != -1) // the wild tech pop number
            {
                try
                {
                    TrackedVisible tracked = ManVisible.inst.GetTrackedVisible(tank.visible.ID);
                    ManVisible.inst.StopTrackingVisible(tank.visible.ID);
                    List<TrackedVisible> visT = (List<TrackedVisible>)forceInsert.GetValue(ManPop.inst);
                    visT.Remove(tracked);
                    forceInsert.SetValue(ManPop.inst, visT);
                    Debug.Log("TACtical_AI: RawTechLoader - Removed " + tank.name + " from population");
                }
                catch { }
            }
        }


        // Determination
        public static void TryClearAreaForBase(Vector3 vector3)
        {   //N/A
            int removeCount = 0;
            foreach (Visible vis in Singleton.Manager<ManVisible>.inst.VisiblesTouchingRadius(vector3, 8, new Bitfield<ObjectTypes>(new ObjectTypes[1] { ObjectTypes.Scenery })))
            {   // Does not compensate for bases that are 64x64 diagonally!
                if (vis.resdisp.IsNotNull())
                {
                    vis.resdisp.RemoveFromWorld(false);
                    removeCount++;
                }
            }
            Debug.Log("TACtical_AI: removed " + removeCount + " trees around new enemy base setup");
        }
        internal static bool GetEnemyBaseSupplies(SpawnBaseTypes toSpawn)
        {
            if (IsHQ(toSpawn))
            {
                return true;
            }
            else if (ContainsPurpose(toSpawn, BasePurpose.Harvesting))
            {
                return true;
            }
            return false;
        }
        internal static bool IsBaseTemplateAvailable(SpawnBaseTypes toSpawn)
        {
            return TempManager.techBases.TryGetValue(toSpawn, out BaseTemplate baseT);
        }
        internal static BaseTemplate GetBaseTemplate(SpawnBaseTypes toSpawn)
        {
            if (TempManager.techBases.TryGetValue(toSpawn, out BaseTemplate baseT))
                return baseT;
            return TempManager.techBases.ElementAtOrDefault(1).Value;
        }
        internal static List<SpawnBaseTypes> GetEnemyBaseTypes(FactionSubTypes faction, BasePurpose purpose, BaseTerrain terra, bool searchAttract = false, int maxGrade = 99)
        {
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
                    if (Singleton.Manager<ManGameMode>.inst.IsCurrentModeMultiplayer() && !cand.Value.purposes.Contains(BasePurpose.MPSafe))
                    {   // no illegal base in MP
                        return false;
                    }
                    if (purpose == BasePurpose.HarvestingNoHQ)
                    {
                        if (cand.Value.purposes.Contains(BasePurpose.Headquarters))
                            return false;
                        if (cand.Value.purposes.Contains(BasePurpose.Harvesting))
                            return true;
                        return false;
                    }
                    if (purpose == BasePurpose.AnyNonHQ)
                    {
                        if (cand.Value.purposes.Contains(BasePurpose.Headquarters))
                            return false;
                        return true;
                    }
                    if (purpose != BasePurpose.NotStationary && cand.Value.purposes.Contains(BasePurpose.NotStationary))
                        return false;
                    if (!searchAttract && cand.Value.purposes.Contains(BasePurpose.NoAutoSearch))
                        return false;
                    if (searchAttract && cand.Value.purposes.Contains(BasePurpose.NoWeapons))
                        return false;
                    if (cand.Value.purposes.Count == 0)
                        return false;
                    return cand.Value.purposes.Contains(purpose);
                });

                if (terra == BaseTerrain.AnyNonSea)
                {
                    canidates = canidates.FindAll
                        (delegate (KeyValuePair<SpawnBaseTypes, BaseTemplate> cand) { return cand.Value.terrain != BaseTerrain.Sea; });
                }
                else
                {
                    canidates = canidates.FindAll
                        (delegate (KeyValuePair<SpawnBaseTypes, BaseTemplate> cand) { return cand.Value.terrain == terra; });
                }

                if (maxGrade != 99 && Singleton.Manager<ManGameMode>.inst.CanEarnXp())
                {
                    canidates = canidates.FindAll
                        (delegate (KeyValuePair<SpawnBaseTypes, BaseTemplate> cand) { return cand.Value.IntendedGrade <= maxGrade; });
                }

                if (searchAttract)
                {   // prevent laggy techs from entering
                    canidates = canidates.FindAll
                        (delegate (KeyValuePair<SpawnBaseTypes, BaseTemplate> cand) { return AIERepair.DesignMemory.JSONToTechExternal(cand.Value.savedTech).Count <= MaxBlockLimitAttract; });
                }
                // finally, remove those which are N/A

                if (canidates.Count == 0)
                    return new List<SpawnBaseTypes> { SpawnBaseTypes.NotAvail };

                // final list compiling
                List<SpawnBaseTypes> final = new List<SpawnBaseTypes>();

                foreach (KeyValuePair<SpawnBaseTypes, BaseTemplate> pair in canidates)
                    final.Add(pair.Key);

                final.Shuffle();

                return final;
            }
            catch { } // we resort to legacy
            return new List<SpawnBaseTypes> { SpawnBaseTypes.NotAvail };
        }
        internal static SpawnBaseTypes GetEnemyBaseType(FactionSubTypes faction, BasePurpose purpose, BaseTerrain terra, bool searchAttract = false, int maxGrade = 99)
        {
            if (ForceSpawn && !searchAttract)
                return forcedBaseSpawn;

            try
            {
                return GetEnemyBaseTypes(faction, purpose, terra, searchAttract, maxGrade).GetRandomEntry();
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
        internal static SpawnBaseTypes GetEnemyBaseTypeFromName(string Name)
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
        internal static string GetBlueprint(SpawnBaseTypes toSpawn)
        {
            return GetBaseTemplate(toSpawn).savedTech;
        }
        internal static string GetEnglishName(SpawnBaseTypes toSpawn)
        {
            return GetBaseTemplate(toSpawn).techName;
        }
        internal static FactionSubTypes GetMainCorp(SpawnBaseTypes toSpawn)
        {
            return GetBaseTemplate(toSpawn).faction;
        }
        internal static int GetBaseStartingFunds(SpawnBaseTypes toSpawn)
        {
            return GetBaseTemplate(toSpawn).startingFunds;
        }
        internal static int GetBaseBBCost(string JSONTechBlueprint)
        {
            int output = 0;
            List<BlockMemory> mem = AIERepair.DesignMemory.JSONToTechExternal(JSONTechBlueprint);
            foreach (BlockMemory block in mem)
            {
                output += Singleton.Manager<RecipeManager>.inst.GetBlockBuyPrice((BlockTypes)Enum.Parse(typeof(BlockTypes), block.t), true);
            }
            return output;
        }

        internal static bool IsHQ(SpawnBaseTypes toSpawn)
        {
            if (TempManager.techBases.TryGetValue(toSpawn, out BaseTemplate baseT))
                return baseT.purposes.Contains(BasePurpose.Headquarters);
            return false;
        }
        internal static bool ContainsPurpose(SpawnBaseTypes toSpawn, BasePurpose purpose)
        {
            if (TempManager.techBases.TryGetValue(toSpawn, out BaseTemplate baseT))
                return baseT.purposes.Contains(purpose);
            return false;
        }
        internal static bool IsRadiusClearOfTechObst(Tank tank, Vector3 pos, float radius)
        {
            bool validLocation = true;
            foreach (Visible vis in Singleton.Manager<ManVisible>.inst.VisiblesTouchingRadius(pos, radius, new Bitfield<ObjectTypes>(new ObjectTypes[1] { ObjectTypes.Vehicle })))
            {
                if (vis.tank.IsNotNull())
                {
                    if (vis.tank != tank)
                        validLocation = false;
                }
            }
            return validLocation;
        }
       


        internal static int GetEnemyBaseCount()
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
        internal static int GetEnemyBaseCountSearchRadius(Vector3 pos, float radius)
        {
            int baseCount = 0;
            foreach (Visible vis in Singleton.Manager<ManVisible>.inst.VisiblesTouchingRadius(pos, radius, new Bitfield<ObjectTypes>(new ObjectTypes[1] { ObjectTypes.Vehicle })))
            {
                if (vis.tank.IsNotNull())
                {
                    if (vis.tank.IsEnemy() && vis.tank.IsAnchored)
                        baseCount++;
                }
            }
            return baseCount;
        }
        internal static int GetEnemyBaseCountForTeam(int Team)
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

        internal static void MakeSureCanExistWithBase(Tank tank)
        {
            if (!tank.IsFriendly(tank.Team) || tank.Team == -1)
            {
                int set = UnityEngine.Random.Range(5, 365);
                Debug.Log("TACtical_AI: Tech " + tank.name + " spawned team " + tank.Team + " that fights against themselves, setting to team " + set + " instead");
                tank.SetTeam(set);
                TryRemoveFromPop(tank);
            }
        }
        internal static int ReassignToRandomEnemyBaseTeam()
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
