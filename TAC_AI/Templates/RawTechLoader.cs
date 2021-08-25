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
        const float MinimumBaseSpacing = 450;
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

            int GradeLim = 0;
            try
            {
                if (ManLicenses.inst.GetLicense(tank.GetMainCorp()).IsDiscovered)
                    GradeLim = ManLicenses.inst.GetLicense(tank.GetMainCorp()).CurrentLevel;
            }
            catch 
            {
                GradeLim = 99; // - creative or something else
            }

            // We validated?  
            //   Alright let's spawn the base!
            SpawnBaseAtPosition(tank, pos, tank.Team, purpose, GradeLim);
        }


        /// <summary>
        /// Spawns a LOYAL enemy base 
        /// - this means this shouldn't be called for capture base missions.
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="Team"></param>
        /// <param name="toSpawn"></param>
        internal static int SpawnBaseAtPosition(Tank spawnerTank, Vector3 pos, int Team, BasePurpose purpose, int grade = 99)
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
                        if (KickStart.DisplayEnemyEvents)
                        {
                            WorldPosition pos2 = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(spawnerTank.visible);

                            Patches.PopupEnemyInfo("Enemy HQ!", pos2);

                            Singleton.Manager<UIMPChat>.inst.AddMissionMessage("Enemy HQ!");
                            Singleton.Manager<UIMPChat>.inst.AddMissionMessage("Protect your terra prospectors!!");
                        }
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
                        if (KickStart.DisplayEnemyEvents)
                        {
                            WorldPosition pos3 = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(spawnerTank.visible);

                            Patches.PopupEnemyInfo("Rival!", pos3);

                            Singleton.Manager<UIMPChat>.inst.AddMissionMessage("Rival Prospector Spotted!");
                            Singleton.Manager<UIMPChat>.inst.AddMissionMessage("Protect your terra prospectors!!");
                        }
                    }
                    catch { }
            break;
                default:
                    haveBB = false;
                    break;
            }

            int extraBB = 100000; // Extras for new bases
            // Are we a defended HQ?
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
                return SpawnAirBase(Vector3.forward, pos, Team, GetEnemyBaseType(spawnerTank.GetMainCorp(), purpose, BaseTerrain.Air, maxGrade: grade), haveBB, extraBB);
            }
            else if (KickStart.isWaterModPresent)
            {
                if (AIEPathing.AboveTheSea(pos))
                {
                    return SpawnSeaBase(Vector3.forward, pos, Team, GetEnemyBaseType(spawnerTank.GetMainCorp(), purpose, BaseTerrain.Sea, maxGrade: grade), haveBB, extraBB);
                }
            }
            return SpawnLandBase(Vector3.forward, pos, Team, GetEnemyBaseType(spawnerTank.GetMainCorp(), purpose, BaseTerrain.Land, maxGrade: grade), haveBB, extraBB);
        }
        internal static bool SpawnBaseExpansion(Tank spawnerTank, Vector3 pos, int Team, SpawnBaseTypes type)
        {   // All bases are off-set rotated right to prevent the base from being built diagonally
            TryClearAreaForBase(pos);

            bool haveBB = (ContainsPurpose(type, BasePurpose.Harvesting) || ContainsPurpose(type, BasePurpose.TechProduction)) && !ContainsPurpose(type, BasePurpose.NotStationary);

            if (haveBB)
            {
                if (spawnerTank.GetComponent<AIControllerAir>())
                {
                    return SpawnAirBase(spawnerTank.rootBlockTrans.right, pos, Team, type, haveBB) > 0;
                }
                else if (KickStart.isWaterModPresent)
                {
                    if (AIEPathing.AboveTheSea(pos))
                    {
                        return SpawnSeaBase(spawnerTank.rootBlockTrans.right, pos, Team, type, haveBB) > 0;
                    }
                }
                return SpawnLandBase(spawnerTank.rootBlockTrans.right, pos, Team, type, haveBB) > 0;
            }
            else
            {   // Defense
                if (!RBases.TryMakePurchase(GetBaseBBCost(GetBlueprint(type)), Team))
                    return false;
                if (spawnerTank.GetComponent<AIControllerAir>())
                {
                    return SpawnAirBase(spawnerTank.rootBlockTrans.right, pos, Team, type, haveBB) > 0;
                }
                else if (KickStart.isWaterModPresent)
                {
                    if (AIEPathing.AboveTheSea(pos))
                    {
                        return SpawnSeaBase(spawnerTank.rootBlockTrans.right, pos, Team, type, haveBB) > 0;
                    }
                }
                return SpawnLandBase(spawnerTank.rootBlockTrans.right, pos, Team, type, haveBB) > 0;
            }
        }

        internal static int SpawnBase(Vector3 pos, int Team, SpawnBaseTypes toSpawn, bool storeBB, int ExtraBB = 0)
        {
            Singleton.Manager<ManWorld>.inst.GetTerrainHeight(pos, out float offset);
            string baseBlueprint = GetBlueprint(toSpawn);
            Vector3 position = pos;
            position.y = offset;
            Quaternion quat = Quaternion.LookRotation(Vector3.forward, Vector3.up);

            TankBlock block = SpawnBlockS(AIERepair.JSONToFirstBlock(baseBlueprint), position, quat, out bool worked);
            if (!worked)
            {
                Debug.Log("TACtical_AI: SpawnLandBase - FAILIURE TO SPAWN TECH!!!  FIRST BLOCK WAS NULL OR TILE NOT LOADED");
                return 0;
            }

            Tank theBase;
            if (storeBB)
                theBase = TechFromBlock(block, Team, GetEnglishName(toSpawn) + " ¥¥" + (GetBaseStartingFunds(toSpawn) + ExtraBB));
            else
            {
                theBase = TechFromBlock(block, Team, GetEnglishName(toSpawn) + " â");
            }


            theBase.FixupAnchors(true);
            var namesav = theBase.gameObject.GetOrAddComponent<BookmarkBuilder>();
            namesav.blueprint = baseBlueprint;
            namesav.infBlocks = GetEnemyBaseSupplies(toSpawn);
            namesav.faction = GetMainCorp(toSpawn);
            namesav.unprovoked = false;
            namesav.instant = false;
            return GetBaseBBCost(baseBlueprint);
        }
        private static int SpawnLandBase(Vector3 spawnerForwards, Vector3 pos, int Team, SpawnBaseTypes toSpawn, bool storeBB, int ExtraBB = 0)
        {
            Singleton.Manager<ManWorld>.inst.GetTerrainHeight(pos, out float offset);
            string baseBlueprint = GetBlueprint(toSpawn);
            Vector3 position = pos;
            position.y = offset;
            Quaternion quat = Quaternion.LookRotation(spawnerForwards, Vector3.up);

            TankBlock block = SpawnBlockS(AIERepair.JSONToFirstBlock(baseBlueprint), position, quat, out bool worked);
            if (!worked)
            {
                Debug.Log("TACtical_AI: SpawnLandBase - FAILIURE TO SPAWN TECH!!!  FIRST BLOCK WAS NULL OR TILE NOT LOADED");
                return 0;
            }

            Tank theBase;
            if (storeBB)
                theBase = TechFromBlock(block, Team, GetEnglishName(toSpawn) + " ¥¥" + (GetBaseStartingFunds(toSpawn) + ExtraBB));
            else
            {
                theBase = TechFromBlock(block, Team, GetEnglishName(toSpawn) + " â");
            }
            
            
            theBase.FixupAnchors(true);
            var namesav = theBase.gameObject.GetOrAddComponent<BookmarkBuilder>();
            namesav.blueprint = baseBlueprint;
            namesav.infBlocks = GetEnemyBaseSupplies(toSpawn);
            namesav.faction = GetMainCorp(toSpawn);
            namesav.unprovoked = false;
            namesav.instant = false;
            return GetBaseBBCost(baseBlueprint);
        }
        private static int SpawnSeaBase(Vector3 spawnerForwards, Vector3 pos, int Team, SpawnBaseTypes toSpawn, bool storeBB, int ExtraBB = 0)
        {   // N/A!!! WIP!!!
            Debug.Log("TACtical_AI: - SpawnSeaBase: Tried to launch unfinished function - falling back to existing");
            return SpawnLandBase(spawnerForwards, pos, Team, toSpawn, storeBB, ExtraBB);
            /*
            Vector3 position = AIEPathing.ForceOffsetToSea(pos);
            string baseBlueprint = GetBlueprint(toSpawn);
            Quaternion quat = Quaternion.LookRotation(Vector3.forward, Vector3.up);

            TankBlock block = SpawnBlockS(AIERepair.JSONToFirstBlock(baseBlueprint), position, quat, out bool worked);
            if (!worked)
            {
                Debug.Log("TACtical_AI: SpawnSeaBase - FAILIURE TO SPAWN TECH!!!  FIRST BLOCK WAS NULL OR TILE NOT LOADED");
                return 0;
            }

            Tank theBase;
            if (storeBB)
                theBase = TechFromBlock(block, Team, GetEnglishName(toSpawn) + " ¥¥" + (GetBaseStartingFunds(toSpawn) + ExtraBB));
            else
            {
                theBase = TechFromBlock(block, Team, GetEnglishName(toSpawn) + " â");
            }


            theBase.FixupAnchors(true);
            var namesav = theBase.gameObject.GetOrAddComponent<BookmarkBuilder>();
            namesav.blueprint = baseBlueprint;
            namesav.infBlocks = GetEnemyBaseSupplies(toSpawn);
            namesav.faction = GetMainCorp(toSpawn);
            namesav.unprovoked = false;
            namesav.instant = false;
            return GetBaseBBCost(baseBlueprint);
            */
        }
        private static int SpawnAirBase(Vector3 spawnerForwards, Vector3 pos, int Team, SpawnBaseTypes toSpawn, bool storeBB, int ExtraBB = 0)
        {   // N/A!!! WIP!!!
            Debug.Log("TACtical_AI: - SpawnAirBase: Tried to launch unfinished function - falling back to existing");
            return SpawnLandBase(spawnerForwards, pos, Team, toSpawn, storeBB, ExtraBB);
            /*
            Vector3 position = AIEPathing.ForceOffsetToSea(pos);
            string baseBlueprint = GetBlueprint(toSpawn);
            Quaternion quat = Quaternion.LookRotation(Vector3.forward, Vector3.up);

            TankBlock block = SpawnBlockS(AIERepair.JSONToFirstBlock(baseBlueprint), position, quat, out bool worked);
            if (!worked)
            {
                Debug.Log("TACtical_AI: SpawnAirBase - FAILIURE TO SPAWN TECH!!!  FIRST BLOCK WAS NULL OR TILE NOT LOADED");
                return 0;
            }

            Tank theBase;
            if (storeBB)
                theBase = TechFromBlock(block, Team, GetEnglishName(toSpawn) + " ¥¥" + (GetBaseStartingFunds(toSpawn) + ExtraBB));
            else
            {
                theBase = TechFromBlock(block, Team, GetEnglishName(toSpawn) + " â");
            }


            theBase.FixupAnchors(true);
            var namesav = theBase.gameObject.GetOrAddComponent<BookmarkBuilder>();
            namesav.blueprint = baseBlueprint;
            namesav.infBlocks = GetEnemyBaseSupplies(toSpawn);
            namesav.faction = GetMainCorp(toSpawn);
            namesav.unprovoked = false;
            namesav.instant = false;
            return GetBaseBBCost(baseBlueprint);
            */
        }
        


        // Mobile Enemy Techs
        internal static Tank SpawnRandomTechAtPosHead(Vector3 pos, Vector3 heading, int Team, FactionSubTypes factionType = FactionSubTypes.NULL, BaseTerrain terrainType = BaseTerrain.Land, bool unProvoked = false, bool AutoTerrain = true, int maxGrade = 99, int maxPrice = 0)
        {   // This will try to spawn player-made enemy techs as well

            Tank outTank;
            if (ShouldUseCustomTechs(factionType, BasePurpose.NotStationary, terrainType, false, maxGrade, unProvoked: unProvoked))
            {
                outTank = SpawnEnemyTechExternal(pos, Team, heading, TempManager.ExternalEnemyTechs[GetExternalIndex(factionType, BasePurpose.NotStationary, terrainType, maxGrade: maxGrade, maxPrice: maxPrice, unProvoked: unProvoked)], unProvoked, AutoTerrain);
            }
            else
            {
                outTank = SpawnMobileTech(pos, heading, Team, GetEnemyBaseType(factionType, BasePurpose.NotStationary, terrainType, maxGrade: maxGrade, unProvoked: unProvoked, maxPrice: maxPrice), false, unProvoked, AutoTerrain);
            }

            return outTank;
        }
        internal static bool SpawnRandomTechAtPosHead(Vector3 pos, Vector3 heading, int Team, List<BasePurpose> purposes, out Tank outTank, FactionSubTypes factionType = FactionSubTypes.NULL, BaseTerrain terrainType = BaseTerrain.Land, bool unProvoked = false, bool AutoTerrain = true, int maxGrade = 99, int maxPrice = 0)
        {   // This will try to spawn player-made enemy techs as well
            if (ShouldUseCustomTechs(factionType, purposes, terrainType, false, maxGrade, maxPrice, unProvoked))
            {
                outTank = SpawnEnemyTechExternal(pos, Team, heading, TempManager.ExternalEnemyTechs[GetExternalIndex(factionType, purposes, terrainType, maxGrade: maxGrade, maxPrice: maxPrice, unProvoked: unProvoked)], unProvoked, AutoTerrain);
            }
            else
            {
                SpawnBaseTypes type = GetEnemyBaseType(factionType, purposes, terrainType, maxGrade: maxGrade);
                if (type == SpawnBaseTypes.NotAvail)
                {
                    outTank = null;
                    return false;
                }
                outTank = SpawnMobileTech(pos, heading, Team, GetEnemyBaseType(factionType, purposes, terrainType, maxGrade: maxGrade, unProvoked: unProvoked, maxPrice: maxPrice), false, unProvoked, AutoTerrain);
            }

            return true;
        }
        internal static bool SpawnRandomTechAtPosHead(Vector3 pos, Vector3 heading, int Team, out Tank outTank, FactionSubTypes factionType = FactionSubTypes.NULL, BaseTerrain terrainType = BaseTerrain.Land, bool unProvoked = false, bool AutoTerrain = true, int maxGrade = 99, int maxPrice = 0)
        {   // This will try to spawn player-made enemy techs as well

            if (ShouldUseCustomTechs(factionType, BasePurpose.NotStationary, terrainType, false, maxGrade, unProvoked: unProvoked, maxPrice: maxPrice))
            {
                outTank = SpawnEnemyTechExternal(pos, Team, heading, TempManager.ExternalEnemyTechs[GetExternalIndex(factionType, BasePurpose.NotStationary, terrainType, maxGrade: maxGrade, unProvoked: unProvoked, maxPrice: maxPrice)], unProvoked, AutoTerrain);
            }
            else
            {
                SpawnBaseTypes type = GetEnemyBaseType(factionType, BasePurpose.NotStationary, terrainType, maxGrade: maxGrade, unProvoked: unProvoked, maxPrice: maxPrice);
                if (type == SpawnBaseTypes.NotAvail)
                {
                    outTank = null;
                    return false;
                }
                outTank = SpawnMobileTech(pos, heading, Team, type, false, unProvoked, AutoTerrain);
            }

            return true;
        }
        internal static Tank SpawnMobileTech(Vector3 pos, Vector3 heading, int Team, SpawnBaseTypes inputSpawn, bool silentFail = true, bool unProvoked = false, bool AutoTerrain = true)
        {
            SpawnBaseTypes toSpawn = inputSpawn;
            if (!IsBaseTemplateAvailable(toSpawn) || IsFallback(toSpawn))
            {
                if (silentFail)
                    return null;
                else
                {
                    Debug.Log("TACtical_AI: SpawnMobileTech - FAILIURE TO SPAWN TECH!!!");
                }
            }

            string baseBlueprint = GetBlueprint(toSpawn);


            Tank theTech = InstantTech(pos, heading, Team, GetEnglishName(toSpawn), GetBlueprint(toSpawn), AutoTerrain);
            if (theTech.IsNull())
            {   // Generate via the failsafe method
                Debug.Log("TACtical_AI: SpawnMobileTech - Generation failed, falling back to slower, reliable Tech building method");

                Vector3 position = pos;
                if (AutoTerrain)
                {
                    Singleton.Manager<ManWorld>.inst.GetTerrainHeight(pos, out float offset);
                    position.y = offset;
                }
                Quaternion quat = Quaternion.LookRotation(heading, Vector3.up);
                TankBlock block = SpawnBlockS(AIERepair.JSONToFirstBlock(baseBlueprint), position, quat, out bool worked);
                if (!worked)
                {
                    Debug.Log("TACtical_AI: SpawnMobileTech - FAILIURE TO SPAWN TECH!!!  FIRST BLOCK WAS NULL OR TILE NOT LOADED");
                    return null;
                }
                theTech = TechFromBlock(block, Team, GetEnglishName(toSpawn));

                var namesav = theTech.gameObject.GetOrAddComponent<BookmarkBuilder>();
                namesav.blueprint = baseBlueprint;
                namesav.infBlocks = GetEnemyBaseSupplies(toSpawn);
                namesav.faction = GetMainCorp(toSpawn);
                namesav.unprovoked = unProvoked;
            }

            theTech.FixupAnchors(true);

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

                Tank theTech = InstantTech(pos, facingDirect, Team, GetEnglishName(toSpawn), baseBlueprint, false);
                if (theTech.IsNull())
                {   // Generate via the failsafe method
                    Debug.Log("TACtical_AI: SpawnAttractTech - Generation failed, falling back to slower, reliable Tech building method");
                    Vector3 position = pos;
                    Quaternion quat = Quaternion.LookRotation(facingDirect, Vector3.up);
                    TankBlock block = SpawnBlockS(AIERepair.JSONToFirstBlock(baseBlueprint), position, quat, out bool worked);
                    if (!worked)
                    {
                        Debug.Log("TACtical_AI: SpawnAttractTech - FAILIURE TO SPAWN TECH!!!  FIRST BLOCK WAS NULL OR TILE NOT LOADED");
                        return false;
                    }
                    theTech = TechFromBlock(block, Team, GetEnglishName(toSpawn));

                    var namesav = theTech.gameObject.GetOrAddComponent<BookmarkBuilder>();
                    namesav.blueprint = baseBlueprint;
                    namesav.infBlocks = GetEnemyBaseSupplies(toSpawn);
                    namesav.faction = GetMainCorp(toSpawn);
                }

                Debug.Log("TACtical_AI: SpawnAttractTech - Spawned " + GetEnglishName(toSpawn));
                return true;
            }
        }
        internal static bool SpawnSpecificTypeTech(Vector3 pos, int Team, Vector3 facingDirect, List<BasePurpose> purposes, BaseTerrain terrainType = BaseTerrain.Land, FactionSubTypes faction = FactionSubTypes.NULL, bool silentFail = true)
        {
            if (ShouldUseCustomTechs(faction, purposes, terrainType, true))
            {
                return SpawnEnemyTechExternal(pos, Team, facingDirect, TempManager.ExternalEnemyTechs[GetExternalIndex(faction, BasePurpose.NotStationary, terrainType, true)]);
            }
            else
            {
                SpawnBaseTypes toSpawn = GetEnemyBaseType(faction, purposes, terrainType, true);
                if (!IsBaseTemplateAvailable(toSpawn))
                {
                    if (silentFail)
                        return false;
                    else
                    { // try again with a different one 
                        int attempts;
                        for (attempts = 6; attempts > 0; attempts--)
                        {
                            toSpawn = GetEnemyBaseType(faction, purposes, terrainType, true);
                            if (IsBaseTemplateAvailable(toSpawn))
                                break;
                        }
                        if (attempts == 0)
                        {
                            Debug.Log("TACtical_AI: SpawnSpecificTypeTech - FAILIURE TO SPAWN ANY TECH!!!");
                            return false;
                        }
                    }
                }

                string baseBlueprint = GetBlueprint(toSpawn);

                Tank theTech;
                if (!ContainsPurpose(toSpawn, BasePurpose.NotStationary))
                {
                    theTech = null; //InstantTech does not handle this correctly 
                }
                else
                    theTech = InstantTech(pos, facingDirect, Team, GetEnglishName(toSpawn), baseBlueprint, false);

                if (theTech.IsNull())
                {   // Generate via the failsafe method
                    //Debug.Log("TACtical_AI: SpawnSpecificTypeTech - Generation failed, falling back to slower, reliable Tech building method");
                    Vector3 position = pos;
                    Quaternion quat = Quaternion.LookRotation(facingDirect, Vector3.up);

                    TankBlock block = SpawnBlockS(AIERepair.JSONToFirstBlock(baseBlueprint), position, quat, out bool worked);
                    if (!worked)
                    {
                        Debug.Log("TACtical_AI: SpawnSpecificTypeTech - FAILIURE TO SPAWN TECH!!!  FIRST BLOCK WAS NULL OR TILE NOT LOADED");
                        return false;
                    }

                    bool storeBB = !ContainsPurpose(toSpawn, BasePurpose.NotStationary) && (ContainsPurpose(toSpawn, BasePurpose.Harvesting) || ContainsPurpose(toSpawn, BasePurpose.TechProduction));

                    if (storeBB)
                    {
                        theTech = TechFromBlock(block, Team, GetEnglishName(toSpawn) + " ¥¥" + 5);
                        theTech.FixupAnchors(true);
                    }
                    else
                        theTech = TechFromBlock(block, Team, GetEnglishName(toSpawn));

                    var namesav = theTech.gameObject.GetOrAddComponent<BookmarkBuilder>();
                    namesav.blueprint = baseBlueprint;
                    namesav.infBlocks = GetEnemyBaseSupplies(toSpawn);
                    namesav.faction = GetMainCorp(toSpawn);
                }

                Debug.Log("TACtical_AI: SpawnSpecificTypeTech - Spawned " + GetEnglishName(toSpawn));
                return true;
            }
        }


        // imported ENEMY cases
        internal static Tank SpawnEnemyTechExternal(Vector3 pos, int Team, Vector3 facingDirect, BaseTemplate Blueprint, bool unProvoked = false, bool AutoTerrain = true)
        {
            string baseBlueprint = Blueprint.savedTech;

            Tank theTech = InstantTech(pos, facingDirect, Team, Blueprint.techName, baseBlueprint, AutoTerrain);
            if (theTech.IsNull())
            {   // Generate via the failsafe method
                Debug.Log("TACtical_AI: SpawnTechExternal - Generation failed, falling back to slower, reliable Tech building method");

                Vector3 position = pos;
                if (AutoTerrain)
                {
                    Singleton.Manager<ManWorld>.inst.GetTerrainHeight(pos, out float offset);
                    position.y = offset;
                }

                Quaternion quat = Quaternion.LookRotation(facingDirect, Vector3.up);
                TankBlock block = SpawnBlockS(AIERepair.JSONToFirstBlock(baseBlueprint), position, quat, out bool worked);
                if (!worked)
                {
                    Debug.Log("TACtical_AI: SpawnEnemyTechExternal - FAILIURE TO SPAWN TECH!!!  FIRST BLOCK WAS NULL OR TILE NOT LOADED");
                    return null;
                }
                theTech = TechFromBlock(block, Team, Blueprint.techName);

                var namesav = theTech.gameObject.GetOrAddComponent<BookmarkBuilder>();
                namesav.blueprint = baseBlueprint;
                namesav.infBlocks = false;
                namesav.faction = Blueprint.faction;
                namesav.unprovoked = unProvoked;
            }

            Debug.Log("TACtical_AI: SpawnTechExternal - Spawned " + Blueprint.techName);

            return theTech;
        }
        
        internal static List<int> GetExternalIndexes(FactionSubTypes faction, BasePurpose purpose, BaseTerrain terra, bool searchAttract = false, int maxGrade = 99, int maxPrice = 0, bool unProvoked = false)
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
                    if (unProvoked && cand.purposes.Contains(BasePurpose.NoWeapons))
                        return true;
                    if (SpecialAISpawner.Eradicators.Count >= KickStart.MaxEradicatorTechs && cand.purposes.Contains(BasePurpose.NANI))
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

                if (maxPrice > 0)
                {
                    canidates = canidates.FindAll
                        (delegate (BaseTemplate cand) { return cand.startingFunds <= maxPrice; });
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
        internal static List<int> GetExternalIndexes(FactionSubTypes faction, List<BasePurpose> purposes, BaseTerrain terra, bool searchAttract = false, int maxGrade = 99, int maxPrice = 0, bool unProvoked = false)
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
                    if (purposes.Contains(BasePurpose.AnyNonHQ))
                    {
                        if (cand.purposes.Contains(BasePurpose.Headquarters))
                            return false;
                        return true;
                    }
                    if (!purposes.Contains(BasePurpose.NotStationary) && cand.purposes.Contains(BasePurpose.NotStationary))
                        return false;
                    if (!searchAttract && cand.purposes.Contains(BasePurpose.NoAutoSearch))
                        return false;
                    if (searchAttract && cand.purposes.Contains(BasePurpose.NoWeapons))
                        return false;
                    if (unProvoked && cand.purposes.Contains(BasePurpose.NoWeapons))
                        return true;
                    if (SpecialAISpawner.Eradicators.Count >= KickStart.MaxEradicatorTechs && cand.purposes.Contains(BasePurpose.NANI))
                        return false;
                    if (cand.purposes.Count == 0)
                        return false;

                    bool valid = true;
                    foreach (BasePurpose purpose in purposes)
                    {
                        if (!cand.purposes.Contains(purpose))
                            valid = false;
                    }
                    return valid;
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

                if (maxPrice > 0)
                {
                    canidates = canidates.FindAll
                        (delegate (BaseTemplate cand) { return cand.startingFunds <= maxPrice; });
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
        
        internal static int GetExternalIndex(FactionSubTypes faction, BasePurpose purpose, BaseTerrain terra, bool searchAttract = false, int maxGrade = 99, int maxPrice = 0, bool unProvoked = false)
        {
            try
            {
                return GetExternalIndexes(faction, purpose, terra, searchAttract, maxGrade, maxPrice, unProvoked).GetRandomEntry();
            }
            catch { }

            return -1;
        }
        internal static int GetExternalIndex(FactionSubTypes faction, List<BasePurpose> purposes, BaseTerrain terra, bool searchAttract = false, int maxGrade = 99, int maxPrice = 0, bool unProvoked = false)
        {
            try
            {
                return GetExternalIndexes(faction, purposes, terra, searchAttract, maxGrade, maxPrice, unProvoked).GetRandomEntry();
            }
            catch { }

            return -1;
        }

        internal static bool ShouldUseCustomTechs(FactionSubTypes faction, BasePurpose purpose, BaseTerrain terra, bool searchAttract = false, int maxGrade = 99, int maxPrice = 0, bool unProvoked = false)
        {
            int CustomTechs = GetExternalIndexes(faction, purpose, terra, searchAttract, maxGrade, maxPrice, unProvoked).Count;
            int PrefabTechs = GetEnemyBaseTypes(faction, purpose, terra, searchAttract, maxGrade, maxPrice, unProvoked).Count;

            int CombinedVal = CustomTechs + PrefabTechs;

            if (KickStart.TryForceOnlyPlayerSpawns)
            {
                Debug.Log("TACtical_AI: ShouldUseCustomTechs - Forced Player-Made Techs spawn possible: " + ((PrefabTechs > 0) ? "true" : "false"));
                if (PrefabTechs > 0)
                {
                    return true;
                }
            }
            else
            {
                float RAND = UnityEngine.Random.Range(0, CombinedVal);
                Debug.Log("TACtical_AI: ShouldUseCustomTechs - Chance " + CustomTechs + "/" + CombinedVal + ", meaning a " + (int)(((float)CustomTechs / (float)CombinedVal) * 100f) + "% chance.   RAND value " + RAND);
                if (RAND > PrefabTechs)
                {
                    return true;
                }
            }
            return false;
        }
        internal static bool ShouldUseCustomTechs(FactionSubTypes faction, List<BasePurpose> purposes, BaseTerrain terra, bool searchAttract = false, int maxGrade = 99, int maxPrice = 0, bool unProvoked = false)
        {
            int CustomTechs = GetExternalIndexes(faction, purposes, terra, searchAttract, maxGrade, maxPrice, unProvoked).Count;
            int PrefabTechs = GetEnemyBaseTypes(faction, purposes, terra, searchAttract, maxGrade, maxPrice, unProvoked).Count;

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
            TankBlock block = SpawnBlockS(AIERepair.JSONToFirstBlock(GetBlueprint(techType)), playerPos, playerFacing, out bool worked);
            if (!worked)
            {
                Debug.Log("TACtical_AI: StripPlayerTechOfBlocks - FAILIURE TO SPAWN TECH!!!  FIRST BLOCK WAS NULL OR TILE NOT LOADED");
                return;
            }
            tech.visible.RemoveFromGame();

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

            Tank theTech = InstantTech(pos, facingDirect, Team, Blueprint.Name, baseBlueprint, AutoTerrain);
            if (theTech.IsNull())
            {   // Generate via the failsafe method
                Debug.Log("TACtical_AI: SpawnTechExternal - Generation failed, falling back to slower, reliable Tech building method");

                Vector3 position = pos;
                if (AutoTerrain)
                {
                    Singleton.Manager<ManWorld>.inst.GetTerrainHeight(pos, out float offset);
                    position.y = offset;
                }
                Quaternion quat = Quaternion.LookRotation(facingDirect, Vector3.up);
                TankBlock block = SpawnBlockS(AIERepair.JSONToFirstBlock(baseBlueprint), position, quat, out bool worked);
                if (!worked)
                {
                    Debug.Log("TACtical_AI: SpawnTechExternal - FAILIURE TO SPAWN TECH!!!  FIRST BLOCK WAS NULL OR TILE NOT LOADED");
                    return null;
                }

                theTech = TechFromBlock(block, Team, Blueprint.Name);
                AIERepair.TurboconstructExt(theTech, AIERepair.DesignMemory.JSONToTechExternal(baseBlueprint), Charged);
                
                if (theTech.IsEnemy())//enemy
                {
                    var namesav = theTech.gameObject.GetOrAddComponent<BookmarkBuilder>();
                    namesav.blueprint = baseBlueprint;
                    namesav.infBlocks = Blueprint.InfBlocks;
                    namesav.faction = Blueprint.Faction;
                    namesav.unprovoked = Blueprint.NonAggressive;
                }
            }
            Debug.Log("TACtical_AI: SpawnTechExternal - Spawned " + Blueprint.Name + " at " + pos + ". Snapped to terrain " + AutoTerrain);


            if (Team == -2)//neutral
            {   // be crafty mike and face the player
                theTech.AI.SetBehaviorType(AITreeType.AITypes.FacePlayer);
            }

            return theTech;
        }

        public static Tank TechTransformer(Tank tech, string JSONTechBlueprint)
        {
            int team = tech.Team;
            string OGName = tech.name;
            Vector3 techPos = tech.transform.position;
            Quaternion techFacing = tech.transform.rotation;


            Tank theTech = InstantTech(techPos, techFacing * Vector3.forward, team, OGName, JSONTechBlueprint, false);
            if (theTech.IsNull())
            {   // Generate via the failsafe method
                Debug.Log("TACtical_AI: TechTransformer - Generation failed, falling back to slower, reliable Tech building method");

                TankBlock block = SpawnBlockS(AIERepair.JSONToFirstBlock(JSONTechBlueprint), techPos, techFacing, out bool worked);
                if (!worked)
                {
                    return tech;
                }

                theTech = TechFromBlock(block, team, OGName);
            }

            tech.visible.RemoveFromGame();

            return theTech;
        }



        // Override
        internal static TankBlock SpawnBlockS(BlockTypes type, Vector3 position, Quaternion quat, out bool worked)
        {
            if (Singleton.Manager<ManWorld>.inst.CheckIsTileAtPositionLoaded(position) && Singleton.Manager<ManSpawn>.inst.IsTankBlockLoaded(type) && Singleton.Manager<ManSpawn>.inst.IsBlockAllowedInCurrentGameMode(type) && TechDataAvailValidation.IsBlockAvailableInMode(type))
            {
                worked = true;

                return Singleton.Manager<ManLooseBlocks>.inst.HostSpawnBlock(type, position, quat, true);
            }
            try
            {
                Debug.Log("TACtical AI: SpawnBlockS - Error on block " + type.ToString());
            }
            catch
            {
                Debug.Log("TACtical AI: SpawnBlockS - Error on unfetchable block");
            }
            if (!Singleton.Manager<ManSpawn>.inst.IsTankBlockLoaded(type))
                Debug.Log("TACtical AI: SpawnBlockS - Could not spawn block!  Block does not exist!");
            else
                Debug.Log("TACtical AI: SpawnBlockS - Could not spawn block!  Block is invalid in current gamemode!");

            worked = false;
            return null;
        }
        internal static Tank TechFromBlock(TankBlock block, int Team, string name)
        {
            //if (ManNetwork.inst.IsMultiplayer)
            //    Team = ManSpawn.inst.gette
            Tank theTech = Singleton.Manager<ManSpawn>.inst.WrapSingleBlock(null, block, Team, name);
            TryForceIntoPop(theTech);
            return theTech;
        }
        internal static Tank InstantTech(Vector3 pos, Vector3 forward, int Team, string name, string blueprint, bool grounded, bool ForceAnchor = false)
        {
            TechData data = new TechData();
            data.Name = name;
            data.m_Bounds = new IntVector3(new Vector3(18, 18, 18));
            data.m_SkinMapping = new Dictionary<uint, string>();
            data.m_TechSaveState = new Dictionary<int, TechComponent.SerialData>();
            data.m_CreationData = new TechData.CreationData();
            data.m_BlockSpecs = new List<TankPreset.BlockSpec>();
            List<BlockMemory> mems = AIERepair.DesignMemory.JSONToTechExternal(blueprint);
            foreach (BlockMemory mem in mems)
            {
                BlockTypes type = AIERepair.StringToBlockType(mem.t);
                if (!Singleton.Manager<ManSpawn>.inst.IsBlockAllowedInCurrentGameMode(type) || !TechDataAvailValidation.IsBlockAvailableInMode(type))
                {
                    Debug.Log("TACtical_AI: InstantTech - Removed " + mem.t + " as it was invalidated");
                    continue;
                }
                TankPreset.BlockSpec spec = default;
                spec.block = mem.t;
                spec.m_BlockType = type;
                spec.orthoRotation = new OrthoRotation(mem.r);
                spec.position = mem.p;
                spec.saveState = new Dictionary<int, Module.SerialData>();
                spec.textSerialData = new List<string>();
                spec.m_SkinID = 0;

                data.m_BlockSpecs.Add(spec);
            }
            ManSpawn.TankSpawnParams tankSpawn = new ManSpawn.TankSpawnParams();
            tankSpawn.techData = data;
            tankSpawn.blockIDs = null;
            tankSpawn.teamID = Team;
            tankSpawn.position = pos;
            tankSpawn.rotation = Quaternion.LookRotation(forward);
            if (ForceAnchor)
                tankSpawn.grounded = true;
            else
                tankSpawn.grounded = grounded;
            Tank theTech = Singleton.Manager<ManSpawn>.inst.SpawnTank(tankSpawn, true);
            if (theTech.IsNull())
            {
                Debug.Log("TACtical_AI: InstantTech - error on SpawnTank");
            }
            else
                TryForceIntoPop(theTech);
            if (ForceAnchor)
            {
                theTech.Anchors.UnanchorAll(false);
                theTech.TryToggleTechAnchor();
                if (!theTech.IsAnchored)
                    theTech.TryToggleTechAnchor();
                if (!theTech.IsAnchored)
                    theTech.Anchors.TryAnchorAll(true);
                if (!theTech.IsAnchored)
                {
                    theTech.Anchors.RetryAnchorOnBeam = true;
                    theTech.Anchors.TryAnchorAll(true);
                }
                if (!theTech.IsAnchored)
                    Debug.Log("TACtical_AI: InstantTech - Game is being stubborn - repeated attempts to anchor failed");
            }
            ReconstructConveyorSequencing(theTech);

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

        internal static bool CanBeMiner(EnemyMind mind)
        {
            if (mind.StartedAnchored)
                return false;
            bool can = true;
            SpawnBaseTypes type = GetEnemyBaseTypeFromName(mind.Tank.name);
            if (type != SpawnBaseTypes.NotAvail)
            {
                TempManager.techBases.TryGetValue(type, out BaseTemplate val);
                can = !val.environ;
            }
            else if (TempManager.ExternalEnemyTechs.Exists(delegate (BaseTemplate cand) { return cand.techName == mind.Tank.name; }))
            {
                can = !TempManager.ExternalEnemyTechs.Find(delegate (BaseTemplate cand) { return cand.techName == mind.Tank.name; }).environ;
            }
            return can;
        }
        internal static bool ShouldDetonateBoltsNow(EnemyMind mind)
        {
            bool can = false;
            try
            {
                SpawnBaseTypes type = GetEnemyBaseTypeFromName(mind.Tank.name);
                if (type != SpawnBaseTypes.NotAvail)
                {
                    if (TempManager.techBases.TryGetValue(type, out BaseTemplate val))
                        can = val.deployBoltsASAP;
                }
                else if (TempManager.ExternalEnemyTechs.Exists(delegate (BaseTemplate cand) { return cand.techName == mind.Tank.name; }))
                {
                    can = TempManager.ExternalEnemyTechs.Find(delegate (BaseTemplate cand) { return cand.techName == mind.Tank.name; }).deployBoltsASAP;
                }
            }
            catch { }
            return can;
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

        internal static List<SpawnBaseTypes> GetEnemyBaseTypes(FactionSubTypes faction, BasePurpose purpose, BaseTerrain terra, bool searchAttract = false, int maxGrade = 99, int maxPrice = 0, bool unProvoked = false)
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
                    if (Singleton.Manager<ManGameMode>.inst.IsCurrentModeMultiplayer() && cand.Value.purposes.Contains(BasePurpose.MPUnsafe))
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
                    if (unProvoked && cand.Value.purposes.Contains(BasePurpose.NoWeapons))
                        return true;
                    if (SpecialAISpawner.Eradicators.Count >= KickStart.MaxEradicatorTechs && cand.Value.purposes.Contains(BasePurpose.NANI))
                        return false;
                    if (cand.Value.purposes.Count == 0)
                        return false;
                    return cand.Value.purposes.Contains(purpose);
                });

                if (terra == BaseTerrain.Any)
                { 
                    //allow all
                }
                else if (terra == BaseTerrain.AnyNonSea)
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

                if (maxPrice > 0)
                {   
                    canidates = canidates.FindAll
                        (delegate (KeyValuePair<SpawnBaseTypes, BaseTemplate> cand) { return cand.Value.startingFunds <= maxPrice; });
                }

                if (searchAttract)
                {   // prevent laggy techs from entering
                    canidates = canidates.FindAll
                        (delegate (KeyValuePair<SpawnBaseTypes, BaseTemplate> cand) { return AIERepair.DesignMemory.JSONToTechExternal(cand.Value.savedTech).Count <= MaxBlockLimitAttract; });
                }
                // finally, remove those which are N/A

                if (canidates.Count == 0)
                    return FallbackHandler(faction);

                // final list compiling
                List<SpawnBaseTypes> final = new List<SpawnBaseTypes>();

                foreach (KeyValuePair<SpawnBaseTypes, BaseTemplate> pair in canidates)
                    final.Add(pair.Key);

                final.Shuffle();

                return final;
            }
            catch { }
            return FallbackHandler(faction);
        }
        internal static List<SpawnBaseTypes> GetEnemyBaseTypes(FactionSubTypes faction, List<BasePurpose> purposes, BaseTerrain terra, bool searchAttract = false, int maxGrade = 99, int maxPrice = 0, bool unProvoked = false)
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
                    if (Singleton.Manager<ManGameMode>.inst.IsCurrentModeMultiplayer() && cand.Value.purposes.Contains(BasePurpose.MPUnsafe))
                    {   // no illegal base in MP
                        return false;
                    }
                    if (purposes.Contains(BasePurpose.HarvestingNoHQ))
                    {
                        if (cand.Value.purposes.Contains(BasePurpose.Headquarters))
                            return false;
                        if (cand.Value.purposes.Contains(BasePurpose.Harvesting))
                            return true;
                        return false;
                    }
                    if (purposes.Contains(BasePurpose.AnyNonHQ))
                    {
                        if (cand.Value.purposes.Contains(BasePurpose.Headquarters))
                            return false;
                        return true;
                    }
                    if (!purposes.Contains(BasePurpose.NotStationary) && cand.Value.purposes.Contains(BasePurpose.NotStationary))
                        return false;
                    if (!searchAttract && cand.Value.purposes.Contains(BasePurpose.NoAutoSearch))
                        return false;
                    if (searchAttract && cand.Value.purposes.Contains(BasePurpose.NoWeapons))
                        return false;
                    if (unProvoked && cand.Value.purposes.Contains(BasePurpose.NoWeapons))
                        return true;
                    if (SpecialAISpawner.Eradicators.Count >= KickStart.MaxEradicatorTechs && cand.Value.purposes.Contains(BasePurpose.NANI))
                        return false;
                    if (cand.Value.purposes.Count == 0)
                        return false;

                    bool valid = true;
                    foreach (BasePurpose purpose in purposes)
                    {
                        if (!cand.Value.purposes.Contains(purpose))
                            valid = false;
                    }
                    return valid;
                });


                if (terra == BaseTerrain.Any)
                {
                    //allow all
                }
                else if (terra == BaseTerrain.AnyNonSea)
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
                if (maxPrice > 0)
                {
                    canidates = canidates.FindAll
                        (delegate (KeyValuePair<SpawnBaseTypes, BaseTemplate> cand) { return cand.Value.startingFunds <= maxPrice; });
                }

                if (searchAttract)
                {   // prevent laggy techs from entering
                    canidates = canidates.FindAll
                        (delegate (KeyValuePair<SpawnBaseTypes, BaseTemplate> cand) { return AIERepair.DesignMemory.JSONToTechExternal(cand.Value.savedTech).Count <= MaxBlockLimitAttract; });
                }
                // finally, remove those which are N/A

                if (canidates.Count == 0)
                    return FallbackHandler(faction);

                // final list compiling
                List<SpawnBaseTypes> final = new List<SpawnBaseTypes>();

                foreach (KeyValuePair<SpawnBaseTypes, BaseTemplate> pair in canidates)
                    final.Add(pair.Key);

                final.Shuffle();

                return final;
            }
            catch { } // we resort to legacy
            return FallbackHandler(faction);
        }
        internal static List<SpawnBaseTypes> FallbackHandler(FactionSubTypes faction)
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
                    if (Singleton.Manager<ManGameMode>.inst.IsCurrentModeMultiplayer() && cand.Value.purposes.Contains(BasePurpose.MPUnsafe))
                    {   // no illegal base in MP
                        return false;
                    }
                    if (cand.Value.purposes.Contains(BasePurpose.Fallback))
                    {
                        return true;
                    }
                    return false;
                });

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

        internal static SpawnBaseTypes GetEnemyBaseType(FactionSubTypes faction, BasePurpose purpose, BaseTerrain terra, bool searchAttract = false, int maxGrade = 99, int maxPrice = 0, bool unProvoked = false)
        {
            if (ForceSpawn && !searchAttract)
                return forcedBaseSpawn;

            try
            {
                return GetEnemyBaseTypes(faction, purpose, terra, searchAttract, maxGrade, maxPrice).GetRandomEntry();
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
        internal static SpawnBaseTypes GetEnemyBaseType(FactionSubTypes faction, List<BasePurpose> purposes, BaseTerrain terra, bool searchAttract = false, int maxGrade = 99, int maxPrice = 0, bool unProvoked = false)
        {
            if (ForceSpawn && !searchAttract)
                return forcedBaseSpawn;

            try
            {
                return GetEnemyBaseTypes(faction, purposes, terra, searchAttract, maxGrade).GetRandomEntry();
            }
            catch { }
            return SpawnBaseTypes.NotAvail;
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
        private static bool IsRadiusClearOfTechObst(Tank tank, Vector3 pos, float radius)
        {
            bool validLocation = true;
            foreach (Visible vis in Singleton.Manager<ManVisible>.inst.VisiblesTouchingRadius(pos, radius, new Bitfield<ObjectTypes>(new ObjectTypes[2] { ObjectTypes.Vehicle, ObjectTypes.Scenery })))
            {
                if (vis.isActive)
                {
                    //if (vis.tank != tank)
                    validLocation = false;
                }
            }
            return validLocation;
        }
        internal static bool IsFallback(SpawnBaseTypes type)
        {
            try
            {
                TempManager.techBases.TryGetValue(type, out BaseTemplate val);
                if (val.purposes.Contains(BasePurpose.Fallback))
                    return true;
                return false;
            }
            catch { } 
            return false;
        }
        internal static BaseTerrain GetTerrain(Vector3 pos)
        {
            try
            {
                if (AIEPathing.AboveHeightFromGround(pos, 25))
                {
                    return BaseTerrain.Air;
                }
                else if (KickStart.isWaterModPresent)
                {
                    if (AIEPathing.AboveTheSea(pos))
                    {
                        return BaseTerrain.Sea;
                    }
                }
            }
            catch { }
            return BaseTerrain.Land;
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
            List<int> teamsGrabbed = new List<int>();
            foreach (Visible vis in Singleton.Manager<ManVisible>.inst.VisiblesTouchingRadius(pos, radius, new Bitfield<ObjectTypes>(new ObjectTypes[1] { ObjectTypes.Vehicle })))
            {
                if (vis.tank.IsNotNull())
                {
                    Tank tech = vis.tank;
                    if (tech.IsEnemy() && tech.IsAnchored && !teamsGrabbed.Contains(tech.Team))
                    {
                        teamsGrabbed.Add(tech.Team);
                    }
                }
            }
            return teamsGrabbed.Count;
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


        // Utilities
        private static void MakeSureCanExistWithBase(Tank tank)
        {
            if (!tank.IsFriendly(tank.Team) || tank.Team == -1)
            {
                int set = UnityEngine.Random.Range(5, 365);
                Debug.Log("TACtical_AI: Tech " + tank.name + " spawned team " + tank.Team + " that fights against themselves, setting to team " + set + " instead");
                tank.SetTeam(set);
                TryRemoveFromPop(tank);
            }
        }
        private static int ReassignToRandomEnemyBaseTeam()
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

        private static void ReconstructConveyorSequencing(Tank tank)
        {
            try
            {
                foreach (ModuleItemConveyor chain in tank.blockman.IterateBlockComponents<ModuleItemConveyor>())
                {   // Reconstruct
                    chain.FlipLoopDirection(backward: false);
                }
            }
            catch { }
        }
    }
}
