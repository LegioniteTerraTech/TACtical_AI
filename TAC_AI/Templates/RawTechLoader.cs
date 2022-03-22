﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using TAC_AI.AI;
using TAC_AI.AI.Movement;
using TAC_AI.AI.Enemy;
using TAC_AI.World;

namespace TAC_AI.Templates
{
    internal class BookmarkBuilder : MonoBehaviour
    {
        public string blueprint;
        public bool infBlocks;
        public FactionTypesExt faction;
        public bool unprovoked = false;
        public bool instant = true;
    }
    internal class RequestAnchored : MonoBehaviour
    {
        sbyte delay = 2;
        private void Update()
        {
            delay--;
            if (delay == 0)
                Destroy(this);
        }
    }

    // For when the spawner is backlogged to prevent corruption
    internal class QueueInstantTech
    {
        internal QueueInstantTech(Action<Tank> endEvent, Vector3 pos, Vector3 forward, int Team, string name, string blueprint, bool grounded, bool ForceAnchor, bool population, bool skins)
        {
            this.endEvent = endEvent;
            this.name = name;
            this.blueprint = blueprint;
            this.pos = pos;
            this.forward = forward;
            this.Team = Team;
            this.grounded = grounded;
            this.ForceAnchor = ForceAnchor;
            this.population = population;
            this.skins = skins;
        }
        readonly int maxAttempts = 25;
        public int fails = 0;
        public Action<Tank> endEvent;
        public string name;
        public string blueprint;
        public Vector3 pos;
        public Vector3 forward;
        public int Team;
        public bool grounded;
        public bool skins;
        public bool ForceAnchor = false;
        public bool population = false;

        internal bool PushSpawn()
        {
            if (ManSpawn.inst.IsTechSpawning)
                return false; // Something else is using it!!  Hold off!
            Tank outcome = RawTechLoader.InstantTech(pos, forward, Team, name, blueprint, grounded, ForceAnchor, population, skins);
            if ((bool)outcome)
            {
                endEvent.Send(outcome);
                return true;
            }
            if (fails > maxAttempts)
                return true; // trash the request
            fails++;
            return false;
        }
    }

    public class RawTechLoader : MonoBehaviour
    {
        internal static RawTechLoader inst;

        const float MinimumBaseSpacing = 450;
        const int MaxBlockLimitAttract = 128;
        /// <summary>
        /// FOR CASES INVOLVING FIRST TECH SPAWNS, NOT ADDITIONS TO TEAMS
        /// </summary>
        internal static bool UseFactionSubTypes = false;  // Force FactionSubTypes instead of FactionTypesExt
        static bool ForceSpawn = false;  // Test a specific base
        static SpawnBaseTypes forcedBaseSpawn = SpawnBaseTypes.GSOMidBase;
        private static List<QueueInstantTech> TechBacklog = new List<QueueInstantTech>();


        public const int EnemyBaseTeamsStart = 5;
        public const int EnemyBaseTeamsEnd = 365;



        public static void Initiate()
        {
            if (!inst)
                inst = new GameObject("EnemyWorldManager").AddComponent<RawTechLoader>();
        }
        public void ClearQueue()
        {
            TechBacklog.Clear();
        }
        public void TryPushTechSpawn()
        {
            if (TechBacklog.Count > 0)
            {
                QueueInstantTech QIT = TechBacklog.ElementAt(0);
                if (QIT.PushSpawn())
                    TechBacklog.RemoveAt(0);
                else
                {   // Try again later
                    TechBacklog.RemoveAt(0);
                    TechBacklog.Add(QIT);
                }
            }
        }
        public void LateUpdate()
        {
            if (TechBacklog.Count > 0)
            {
                TryPushTechSpawn();
            }
        }

        // Main initiation function
        internal static void TrySpawnBase(Tank tank, AIECore.TankAIHelper thisInst, BasePurpose purpose = BasePurpose.Harvesting)
        {
            if (!KickStart.enablePainMode || !KickStart.AllowEnemiesToStartBases)
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

            int extraBB; // Extras for new bases
            if (spawnerTank.GetMainCorpExt() == FactionTypesExt.GSO)
            {
                switch (grade)
                {
                    case 0: // Really early game
                        extraBB = 500;
                        break;
                    case 1:
                        extraBB = 25000;
                        break;
                    case 2: // Tech builders active
                        extraBB = 50000;
                        break;
                    case 3:
                        extraBB = 75000;
                        break;
                    default:
                        extraBB = 100000;
                        break;
                }
            }
            else
            {
                switch (grade)
                {
                    case 0:
                        extraBB = 10000;
                        break;
                    case 1: // Tech builders active
                        extraBB = 50000;
                        break;
                    default:
                        extraBB = 75000;
                        break;
                }
            }
            try
            {
                float divider = 5 / Singleton.Manager<ManLicenses>.inst.GetLicense(KickStart.CorpExtToCorp(FactionTypesExt.GSO)).CurrentLevel;
                extraBB = (int)(extraBB / divider);
            }
            catch { }


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
            SpawnBaseTypes type = SpawnBaseTypes.NotAvail;
            FactionTypesExt FTE = spawnerTank.GetMainCorpExt();
            BaseTerrain BT = BaseTerrain.Land;
            if (spawnerTank.GetComponent<AIControllerAir>())
            {
                BT = BaseTerrain.Air;
                type = GetEnemyBaseType(FTE, purpose, BaseTerrain.Air, maxGrade: grade);
            }
            else if (KickStart.isWaterModPresent)
            {
                if (AIEPathing.AboveTheSea(pos))
                {
                    BT = BaseTerrain.Sea;
                    type = GetEnemyBaseType(FTE, purpose, BaseTerrain.Sea, maxGrade: grade);
                }
            }
            if (BT == BaseTerrain.Land)
                type = GetEnemyBaseType(FTE, purpose, BaseTerrain.Land, maxGrade: grade);

            if (ShouldUseCustomTechs(out List<int> valid, spawnerTank.GetMainCorpExt(), purpose, BT, false, grade))
            {
                int spawnIndex = valid.GetRandomEntry();
                if (spawnIndex == -1)
                {
                    Debug.Log("TACtical_AI: ShouldUseCustomTechs(SpawnBaseAtPosition) - Critical error on call - Expected a Custom Local Tech to exist but found none!");
                }
                else
                {
                    BaseTemplate BTemp = TempManager.ExternalEnemyTechs[spawnIndex];
                    SpawnEnemyTechExtBase(pos, Team, Vector3.forward, BTemp);
                    return BTemp.startingFunds;
                }
            }

            switch (BT)
            {
                case BaseTerrain.Air: 
                    return SpawnAirBase(Vector3.forward, pos, Team, type, haveBB, GetBaseStartingFunds(type) + extraBB);
                case BaseTerrain.Sea: 
                    return SpawnSeaBase(Vector3.forward, pos, Team, type, haveBB, GetBaseStartingFunds(type) + extraBB);
                default:
                    return SpawnLandBase(Vector3.forward, pos, Team, type, haveBB, GetBaseStartingFunds(type) + extraBB);
            }
        }
        internal static bool SpawnBaseExpansion(Tank spawnerTank, Vector3 pos, int Team, SpawnBaseTypes type)
        {   // All bases are off-set rotated right to prevent the base from being built diagonally
            TryClearAreaForBase(pos);

            bool haveBB = (ContainsPurpose(type, BasePurpose.Harvesting) || ContainsPurpose(type, BasePurpose.TechProduction)) && !ContainsPurpose(type, BasePurpose.NotStationary);

            BaseTerrain BT = BaseTerrain.Land;
            if (haveBB)
            {
                if (spawnerTank.GetComponent<AIControllerAir>())
                {
                    BT = BaseTerrain.Air;
                }
                else if (KickStart.isWaterModPresent)
                {
                    if (AIEPathing.AboveTheSea(pos))
                    {
                        BT = BaseTerrain.Sea;
                    }
                }
            }
            else
            {   // Defense
                if (!RBases.PurchasePossible(GetBaseBBCost(GetBlueprint(type)), Team))
                    return false;
                if (spawnerTank.GetComponent<AIControllerAir>())
                {
                    BT = BaseTerrain.Air;
                }
                else if (KickStart.isWaterModPresent)
                {
                    if (AIEPathing.AboveTheSea(pos))
                    {
                        BT = BaseTerrain.Sea;
                    }
                }
            }

            switch (BT)
            {
                case BaseTerrain.Air:
                    return SpawnAirBase(spawnerTank.rootBlockTrans.right, pos, Team, type, haveBB) > 0;
                case BaseTerrain.Sea:
                    return SpawnSeaBase(spawnerTank.rootBlockTrans.right, pos, Team, type, haveBB) > 0;
                default:
                    return SpawnLandBase(spawnerTank.rootBlockTrans.right, pos, Team, type, haveBB) > 0;
            }
        }
        

        // LOADED
        /// <summary>
        /// For loading bases from Debug
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="Team"></param>
        /// <param name="toSpawn"></param>
        /// <param name="storeBB"></param>
        /// <param name="ExtraBB"></param>
        /// <returns></returns>
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
            namesav.faction = GetMainCorpExt(toSpawn);
            namesav.unprovoked = false;
            namesav.instant = false;
            return GetBaseBBCost(baseBlueprint);
        }
        /// <summary>
        /// For loading bases for natural enemy spawns
        /// </summary>
        /// <param name="spawnerForwards"></param>
        /// <param name="pos"></param>
        /// <param name="Team"></param>
        /// <param name="toSpawn"></param>
        /// <param name="storeBB"></param>
        /// <param name="SpawnBB"></param>
        /// <returns></returns>
        private static int SpawnLandBase(Vector3 spawnerForwards, Vector3 pos, int Team, SpawnBaseTypes toSpawn, bool storeBB, int SpawnBB = 0)
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
                theBase = TechFromBlock(block, Team, GetEnglishName(toSpawn) + " ¥¥" + SpawnBB);
            else
            {
                theBase = TechFromBlock(block, Team, GetEnglishName(toSpawn) + " â");
            }
            
            
            theBase.FixupAnchors(true);
            var namesav = theBase.gameObject.GetOrAddComponent<BookmarkBuilder>();
            namesav.blueprint = baseBlueprint;
            namesav.infBlocks = GetEnemyBaseSupplies(toSpawn);
            namesav.faction = GetMainCorpExt(toSpawn);
            namesav.unprovoked = false;
            namesav.instant = false;
            return GetBaseBBCost(baseBlueprint);
        }
        private static int SpawnSeaBase(Vector3 spawnerForwards, Vector3 pos, int Team, SpawnBaseTypes toSpawn, bool storeBB, int SpawnBB = 0)
        {   // N/A!!! WIP!!!
            Debug.Log("TACtical_AI: - SpawnSeaBase: Tried to launch unfinished function - falling back to existing");
            return SpawnLandBase(spawnerForwards, pos, Team, toSpawn, storeBB, SpawnBB);
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
        private static int SpawnAirBase(Vector3 spawnerForwards, Vector3 pos, int Team, SpawnBaseTypes toSpawn, bool storeBB, int SpawnBB = 0)
        {   // N/A!!! WIP!!!
            Debug.Log("TACtical_AI: - SpawnAirBase: Tried to launch unfinished function - falling back to existing");
            return SpawnLandBase(spawnerForwards, pos, Team, toSpawn, storeBB, SpawnBB);
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
       


        // UNLOADED
        internal static TechData GetBaseExpansionUnloaded(Vector3 pos, EnemyPresence EP, SpawnBaseTypes type, out int[] bIDs)
        {   // All bases are off-set rotated right to prevent the base from being built diagonally
            TryClearAreaForBase(pos);

            bool haveBB = (ContainsPurpose(type, BasePurpose.Harvesting) || ContainsPurpose(type, BasePurpose.TechProduction)) && !ContainsPurpose(type, BasePurpose.NotStationary);
            bIDs = new int[1] { 1 };
            if (haveBB)
            {
                return SpawnUnloadedBase(type, haveBB, out bIDs);
            }
            else
            {   // Defense
                if (!RBases.PurchasePossible(GetBaseBBCost(GetBlueprint(type)), EP.Team))
                    return null;
                return SpawnUnloadedBase(type, haveBB, out bIDs);
            }
        }
        private static TechData SpawnUnloadedBase(SpawnBaseTypes toSpawn, bool storeBB, out int[] blocIDs, int SpawnBB = 0)
        {
            string baseBlueprint = GetBlueprint(toSpawn);
            string name;

            if (storeBB)
                name = GetEnglishName(toSpawn) + " ¥¥" + SpawnBB;
            else
            {
                name = GetEnglishName(toSpawn) + " â";
            }
            return ExportRawTechToTechData(name, baseBlueprint, out blocIDs);
        }
        internal static TechData SpawnUnloadedTech(SpawnBaseTypes toSpawn, out int[] blocIDs)
        {
            string baseBlueprint = GetBlueprint(toSpawn);
            string name = GetEnglishName(toSpawn);
            return ExportRawTechToTechData(name, baseBlueprint, out blocIDs);
        }
        internal static TechData GetBaseExpansionUnloadedExt(Vector3 pos, EnemyPresence EP, BaseTemplate BT, out int[] bIDs)
        {   // All bases are off-set rotated right to prevent the base from being built diagonally
            TryClearAreaForBase(pos);

            bool haveBB = (BT.purposes.Contains(BasePurpose.Harvesting) || BT.purposes.Contains(BasePurpose.TechProduction)) && !BT.purposes.Contains(BasePurpose.NotStationary);
            bIDs = new int[1] { 1 };
            if (haveBB)
            {
                return SpawnUnloadedBaseExt(BT, haveBB, out bIDs);
            }
            else
            {   // Defense
                if (!RBases.PurchasePossible(GetBaseBBCost(BT.savedTech), EP.Team))
                    return null;
                return SpawnUnloadedBaseExt(BT, haveBB, out bIDs);
            }
        }
        private static TechData SpawnUnloadedBaseExt(BaseTemplate BT, bool storeBB, out int[] blocIDs, int SpawnBB = 0)
        {
            string baseBlueprint = BT.savedTech;
            string name;

            if (storeBB)
                name = BT.techName + " ¥¥" + SpawnBB;
            else
            {
                name = BT.techName + " â";
            }
            return ExportRawTechToTechData(name, baseBlueprint, out blocIDs);
        }
        internal static TechData SpawnUnloadedTechExt(BaseTemplate BT, out int[] blocIDs)
        {
            string baseBlueprint = BT.savedTech;
            string name = BT.techName;
            return ExportRawTechToTechData(name, baseBlueprint, out blocIDs);
        }


        // Mobile Enemy Techs
        internal static Tank SpawnRandomTechAtPosHead(Vector3 pos, Vector3 heading, int Team, FactionTypesExt factionType = FactionTypesExt.NULL, BaseTerrain terrainType = BaseTerrain.Land, bool unProvoked = false, bool AutoTerrain = true, int maxGrade = 99, int maxPrice = 0)
        {   // This will try to spawn player-made enemy techs as well

            Tank outTank;
            if (ShouldUseCustomTechs(out List<int> valid, factionType, BasePurpose.NotStationary, terrainType, false, maxGrade, maxPrice: maxPrice, unProvoked: unProvoked))
            {
                int spawnIndex = valid.GetRandomEntry();
                if (spawnIndex == -1)
                {
                    Debug.Log("TACtical_AI: ShouldUseCustomTechs - Critical error on call - Expected a Custom Local Tech to exist but found none!");
                    outTank = SpawnMobileTech(pos, heading, Team, GetEnemyBaseType(factionType, BasePurpose.NotStationary, terrainType, maxGrade: maxGrade, unProvoked: unProvoked, maxPrice: maxPrice), false, unProvoked, AutoTerrain);
                }
                else
                    outTank = SpawnEnemyTechExt(pos, Team, heading, TempManager.ExternalEnemyTechs[spawnIndex], unProvoked, AutoTerrain);
            }
            else
            {
                outTank = SpawnMobileTech(pos, heading, Team, GetEnemyBaseType(factionType, BasePurpose.NotStationary, terrainType, maxGrade: maxGrade, unProvoked: unProvoked, maxPrice: maxPrice), false, unProvoked, AutoTerrain);
            }

            return outTank;
        }

        internal static bool SpawnRandomTechAtPosHead(Vector3 pos, Vector3 heading, int Team, out Tank outTank, FactionTypesExt factionType = FactionTypesExt.NULL, BaseTerrain terrainType = BaseTerrain.Land, bool unProvoked = false, bool AutoTerrain = true, int maxGrade = 99, int maxPrice = 0)
        {   // This will try to spawn player-made enemy techs as well

            if (ShouldUseCustomTechs(out List<int> valid, factionType, BasePurpose.NotStationary, terrainType, false, maxGrade, unProvoked: unProvoked, maxPrice: maxPrice))
            {
                outTank = SpawnEnemyTechExt(pos, Team, heading, TempManager.ExternalEnemyTechs[GetExternalIndex(factionType, BasePurpose.NotStationary, terrainType, maxGrade: maxGrade, unProvoked: unProvoked, maxPrice: maxPrice)], unProvoked, AutoTerrain);
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
                namesav.faction = GetMainCorpExt(toSpawn);
                namesav.unprovoked = unProvoked;
            }

            theTech.FixupAnchors(true);

            return theTech;
        }
        internal static bool SpawnAttractTech(Vector3 pos, int Team, Vector3 facingDirect, BaseTerrain terrainType = BaseTerrain.Land, FactionTypesExt faction = FactionTypesExt.NULL, BasePurpose purpose = BasePurpose.NotStationary, bool silentFail = true)
        {
            if (ShouldUseCustomTechs(out List<int> valid, faction, BasePurpose.NotStationary, terrainType, true))
            {
                int spawnIndex = valid.GetRandomEntry();
                if (spawnIndex == -1)
                {
                    Debug.Log("TACtical_AI: ShouldUseCustomTechs - Critical error on call - Expected a Custom Local tech to exist but found none!");
                    return false;
                }
                else
                    return SpawnEnemyTechExt(pos, Team, facingDirect, TempManager.ExternalEnemyTechs[spawnIndex]);
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
                    namesav.faction = GetMainCorpExt(toSpawn);
                }

                Debug.Log("TACtical_AI: SpawnAttractTech - Spawned " + GetEnglishName(toSpawn));
                return true;
            }
        }
        internal static bool SpawnSpecificTypeTech(Vector3 pos, int Team, Vector3 facingDirect, List<BasePurpose> purposes, BaseTerrain terrainType = BaseTerrain.Land, FactionTypesExt faction = FactionTypesExt.NULL, bool silentFail = true, bool unProvoked = false, bool AutoTerrain = true, int maxGrade = 99, int maxPrice = 0, bool forceInstant = false, bool isPopulation = false)
        {
            if (ShouldUseCustomTechs(faction, purposes, terrainType, true))
            {
                return SpawnEnemyTechExt(pos, Team, facingDirect, TempManager.ExternalEnemyTechs[GetExternalIndex(faction, BasePurpose.NotStationary, terrainType, ManGameMode.inst.IsCurrent<ModeAttract>(), maxGrade, maxPrice, unProvoked)], unProvoked, AutoTerrain, forceInstant, isPopulation);
            }
            else
            {
                SpawnBaseTypes toSpawn = GetEnemyBaseType(faction, purposes, terrainType, ManGameMode.inst.IsCurrent<ModeAttract>(), maxGrade, maxPrice, unProvoked);
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

                bool MustBeAnchored = !ContainsPurpose(toSpawn, BasePurpose.NotStationary);

                Tank theTech;
                if (MustBeAnchored)
                {
                    //theTech = null; //InstantTech does not handle this correctly 
                    bool storeBB = ContainsPurpose(toSpawn, BasePurpose.Harvesting) || ContainsPurpose(toSpawn, BasePurpose.TechProduction);

                    if (storeBB)
                    {
                        theTech = InstantTech(pos, facingDirect, Team, GetEnglishName(toSpawn) + " ¥¥" + 5000000, baseBlueprint, AutoTerrain, MustBeAnchored, isPopulation);
                    }
                    else
                    {
                        theTech = InstantTech(pos, facingDirect, Team, GetEnglishName(toSpawn), baseBlueprint, AutoTerrain, MustBeAnchored, isPopulation);
                    }
                }
                else
                {
                    if (ContainsPurpose(toSpawn, BasePurpose.Defense))
                        theTech = InstantTech(pos, facingDirect, Team, GetEnglishName(toSpawn) + " â", baseBlueprint, AutoTerrain, MustBeAnchored, isPopulation);
                    else
                        theTech = InstantTech(pos, facingDirect, Team, GetEnglishName(toSpawn), baseBlueprint, AutoTerrain, MustBeAnchored, isPopulation);
                }

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
                    {
                        if (ContainsPurpose(toSpawn, BasePurpose.Defense))
                            theTech = TechFromBlock(block, Team, GetEnglishName(toSpawn) + " â");
                        else
                            theTech = TechFromBlock(block, Team, GetEnglishName(toSpawn));
                    }

                    var namesav = theTech.gameObject.GetOrAddComponent<BookmarkBuilder>();
                    namesav.blueprint = baseBlueprint;
                    namesav.infBlocks = GetEnemyBaseSupplies(toSpawn);
                    namesav.faction = GetMainCorpExt(toSpawn);
                }

                if (theTech.IsNotNull())
                    Debug.Log("TACtical_AI: SpawnSpecificTypeTech - Spawned " + GetEnglishName(toSpawn));
                return true;
            }
        }
        internal static void SpawnSpecificTypeTechSafe(Vector3 pos, int Team, Vector3 facingDirect, List<BasePurpose> purposes, BaseTerrain terrainType = BaseTerrain.Land, FactionTypesExt faction = FactionTypesExt.NULL, bool silentFail = true, bool unProvoked = false, bool AutoTerrain = true, int maxGrade = 99, int maxPrice = 0, bool forceInstant = false, bool isPopulation = false, Action<Tank> fallbackOp = null)
        {
            if (ShouldUseCustomTechs(faction, purposes, terrainType, true))
            {
                SpawnEnemyTechExtSafe(pos, Team, facingDirect, TempManager.ExternalEnemyTechs[GetExternalIndex(faction, BasePurpose.NotStationary, terrainType, ManGameMode.inst.IsCurrent<ModeAttract>(), maxGrade, maxPrice, unProvoked)], unProvoked, AutoTerrain, forceInstant, isPopulation, fallbackOp);
            }
            else
            {
                SpawnBaseTypes toSpawn = GetEnemyBaseType(faction, purposes, terrainType, ManGameMode.inst.IsCurrent<ModeAttract>(), maxGrade, maxPrice, unProvoked);
                if (!IsBaseTemplateAvailable(toSpawn))
                {
                    if (silentFail)
                        return;
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
                            Debug.Log("TACtical_AI: SpawnSpecificTypeTechSafe - FAILIURE TO SPAWN ANY TECH!!!");
                        }
                    }
                }

                string baseBlueprint = GetBlueprint(toSpawn);

                bool MustBeAnchored = !ContainsPurpose(toSpawn, BasePurpose.NotStationary);

                if (MustBeAnchored)
                {
                    //theTech = null; //InstantTech does not handle this correctly 
                    bool storeBB = ContainsPurpose(toSpawn, BasePurpose.Harvesting) || ContainsPurpose(toSpawn, BasePurpose.TechProduction);

                    if (storeBB)
                    {
                        InstantTechSafe(pos, facingDirect, Team, GetEnglishName(toSpawn) + " ¥¥" + 5000000, baseBlueprint, AutoTerrain, MustBeAnchored, isPopulation, fallbackOp);
                    }
                    else
                    {
                        InstantTechSafe(pos, facingDirect, Team, GetEnglishName(toSpawn), baseBlueprint, AutoTerrain, MustBeAnchored, isPopulation, fallbackOp);
                    }
                }
                else
                {
                    if (ContainsPurpose(toSpawn, BasePurpose.Defense))
                        InstantTechSafe(pos, facingDirect, Team, GetEnglishName(toSpawn) + " â", baseBlueprint, AutoTerrain, MustBeAnchored, isPopulation, fallbackOp);
                    else
                        InstantTechSafe(pos, facingDirect, Team, GetEnglishName(toSpawn), baseBlueprint, AutoTerrain, MustBeAnchored, isPopulation, fallbackOp);
                }
                Debug.Log("TACtical_AI: SpawnSpecificTypeTechSafe - Spawned " + GetEnglishName(toSpawn));
            }
        }


        // imported ENEMY cases
        internal static Tank SpawnEnemyTechExt(Vector3 pos, int Team, Vector3 facingDirect, BaseTemplate Blueprint, bool unProvoked = false, bool AutoTerrain = true, bool forceInstant = false, bool pop = false)
        {
            string baseBlueprint = Blueprint.savedTech;

            Tank theTech;

            bool MustBeAnchored = !Blueprint.purposes.Contains(BasePurpose.NotStationary);

            if (!forceInstant && MustBeAnchored)
            {
                //theTech = null; //InstantTech does not handle this correctly 
                bool storeBB = Blueprint.purposes.Contains(BasePurpose.Harvesting) || Blueprint.purposes.Contains(BasePurpose.TechProduction);

                if (storeBB)
                {
                    theTech = InstantTech(pos, facingDirect, Team, Blueprint.techName + " ¥¥" + 5000000, baseBlueprint, AutoTerrain, MustBeAnchored, pop);
                }
                else
                {
                    theTech = InstantTech(pos, facingDirect, Team, Blueprint.techName, baseBlueprint, AutoTerrain, MustBeAnchored, pop);
                }
            }
            else
            {
                if (Blueprint.purposes.Contains(BasePurpose.Defense))
                    theTech = InstantTech(pos, facingDirect, Team, Blueprint.techName + " â", baseBlueprint, AutoTerrain, MustBeAnchored, pop);
                else
                    theTech = InstantTech(pos, facingDirect, Team, Blueprint.techName, baseBlueprint, AutoTerrain, MustBeAnchored, pop);
            }
            
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
                    Debug.Log("TACtical_AI: SpawnEnemyTechExt - FAILIURE TO SPAWN TECH!!!  FIRST BLOCK WAS NULL OR TILE NOT LOADED");
                    return null;
                }
                bool storeBB = !Blueprint.purposes.Contains(BasePurpose.NotStationary) && (Blueprint.purposes.Contains(BasePurpose.Harvesting) || Blueprint.purposes.Contains(BasePurpose.TechProduction));

                if (storeBB)
                {
                    theTech = TechFromBlock(block, Team, Blueprint.techName + " ¥¥" + 5);
                    theTech.FixupAnchors(true);
                }
                else
                {
                    if (Blueprint.purposes.Contains(BasePurpose.Defense))
                        theTech = TechFromBlock(block, Team, Blueprint.techName + " â");
                    else
                        theTech = TechFromBlock(block, Team, Blueprint.techName);
                }

                var namesav = theTech.gameObject.GetOrAddComponent<BookmarkBuilder>();
                namesav.blueprint = baseBlueprint;
                namesav.infBlocks = false;
                namesav.faction = Blueprint.faction;
                namesav.unprovoked = unProvoked;
            }

            Debug.Log("TACtical_AI: SpawnTechExternal - Spawned " + Blueprint.techName);

            return theTech;
        }
        internal static Tank SpawnEnemyTechExtBase(Vector3 pos, int Team, Vector3 facingDirect, BaseTemplate Blueprint, bool unProvoked = false, bool AutoTerrain = true)
        {
            string baseBlueprint = Blueprint.savedTech;

            Tank theTech;

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
                Debug.Log("TACtical_AI: SpawnEnemyTechExt - FAILIURE TO SPAWN TECH!!!  FIRST BLOCK WAS NULL OR TILE NOT LOADED");
                return null;
            }
            bool storeBB = !Blueprint.purposes.Contains(BasePurpose.NotStationary) && (Blueprint.purposes.Contains(BasePurpose.Harvesting) || Blueprint.purposes.Contains(BasePurpose.TechProduction));

            if (storeBB)
            {
                theTech = TechFromBlock(block, Team, Blueprint.techName + " ¥¥" + 5);
                theTech.FixupAnchors(true);
            }
            else
            {
                if (Blueprint.purposes.Contains(BasePurpose.Defense))
                    theTech = TechFromBlock(block, Team, Blueprint.techName + " â");
                else
                    theTech = TechFromBlock(block, Team, Blueprint.techName);
            }

            var namesav = theTech.gameObject.GetOrAddComponent<BookmarkBuilder>();
            namesav.blueprint = baseBlueprint;
            namesav.infBlocks = false;
            namesav.faction = Blueprint.faction;
            namesav.unprovoked = unProvoked;
            namesav.instant = false;

            Debug.Log("TACtical_AI: SpawnEnemyTechExtBase - Spawned " + Blueprint.techName);

            return theTech;
        }
        internal static void SpawnEnemyTechExtSafe(Vector3 pos, int Team, Vector3 facingDirect, BaseTemplate Blueprint, bool unProvoked = false, bool AutoTerrain = true, bool forceInstant = false, bool pop = false, Action<Tank> fallbackOp = null)
        {
            string baseBlueprint = Blueprint.savedTech;


            bool MustBeAnchored = !Blueprint.purposes.Contains(BasePurpose.NotStationary);

            if (!forceInstant && MustBeAnchored)
            {
                bool storeBB = Blueprint.purposes.Contains(BasePurpose.Harvesting) || Blueprint.purposes.Contains(BasePurpose.TechProduction);

                if (storeBB)
                {
                    InstantTechSafe(pos, facingDirect, Team, Blueprint.techName + " ¥¥" + 5000000, baseBlueprint, AutoTerrain, MustBeAnchored, pop, fallbackOp);
                }
                else
                {
                    InstantTechSafe(pos, facingDirect, Team, Blueprint.techName, baseBlueprint, AutoTerrain, MustBeAnchored, pop, fallbackOp);
                }
            }
            else
            {
                if (Blueprint.purposes.Contains(BasePurpose.Defense))
                    InstantTechSafe(pos, facingDirect, Team, Blueprint.techName + " â", baseBlueprint, AutoTerrain, MustBeAnchored, pop, fallbackOp);
                else
                    InstantTechSafe(pos, facingDirect, Team, Blueprint.techName, baseBlueprint, AutoTerrain, MustBeAnchored, pop, fallbackOp);
            }

            Debug.Log("TACtical_AI: SpawnTechExternal - Spawned " + Blueprint.techName);
        }

        internal static List<int> GetExternalIndexes(FactionTypesExt faction, BasePurpose purpose, BaseTerrain terra, bool searchAttract = false, int maxGrade = 99, int maxPrice = 0, bool unProvoked = false)
        {
            try
            {   // Filters
                List<BaseTemplate> canidates;
                if (faction == FactionTypesExt.NULL)
                {
                    canidates = TempManager.ExternalEnemyTechs;
                }
                else
                {
                    if (UseFactionSubTypes)
                    {
                        canidates = TempManager.ExternalEnemyTechs.FindAll
                            (delegate (BaseTemplate cand) { return KickStart.CorpExtToCorp(cand.faction) == KickStart.CorpExtToCorp(faction); });
                        UseFactionSubTypes = false;
                    }
                    else
                    {
                        canidates = TempManager.ExternalEnemyTechs.FindAll
                            (delegate (BaseTemplate cand) { return cand.faction == faction; });
                    }
                }

                bool cantSpawnErad = (!KickStart.EnemyEradicators || SpecialAISpawner.Eradicators.Count >= KickStart.MaxEradicatorTechs);
                canidates = canidates.FindAll(delegate (BaseTemplate cand)
                {
                    List<BasePurpose> techPurposes = cand.purposes;
                    if (Singleton.Manager<ManGameMode>.inst.IsCurrentModeMultiplayer() && techPurposes.Contains(BasePurpose.MPUnsafe))
                    {   // no illegal base in MP
                        return false;
                    }
                    if (purpose == BasePurpose.HarvestingNoHQ)
                    {
                        if (techPurposes.Contains(BasePurpose.Headquarters))
                            return false;
                        if (techPurposes.Contains(BasePurpose.Harvesting))
                            return true;
                        return false;
                    }
                    if (purpose == BasePurpose.AnyNonHQ)
                    {
                        if (techPurposes.Exists(delegate (BasePurpose cand2) { return cand2 == BasePurpose.Headquarters || cand2 == BasePurpose.NotStationary; }))
                            return false;
                        return true;
                    }
                    bool notStationary = techPurposes.Contains(BasePurpose.NotStationary);
                    if (purpose != BasePurpose.NotStationary && notStationary)
                        return false;
                    if (!searchAttract && techPurposes.Contains(BasePurpose.AttractTech))
                        return false;
                    bool noWeapons = techPurposes.Contains(BasePurpose.NoWeapons);
                    if (searchAttract && noWeapons)
                        return false;
                    if (unProvoked && noWeapons)
                        return true;
                    if (cantSpawnErad && techPurposes.Contains(BasePurpose.NANI))
                        return false;

                    if (purpose == BasePurpose.Harvesting && notStationary)
                        return false;

                    if (techPurposes.Count == 0)
                        return false;
                    return techPurposes.Contains(purpose);
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
                        (delegate (BaseTemplate cand) { return AIERepair.DesignMemory.JSONToMemoryExternal(cand.savedTech).Count <= MaxBlockLimitAttract; });
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
        internal static List<int> GetExternalIndexes(FactionTypesExt faction, List<BasePurpose> purposes, BaseTerrain terra, bool searchAttract = false, int maxGrade = 99, int maxPrice = 0, bool unProvoked = false)
        {
            try
            {
                // Filters
                List<BaseTemplate> canidates;
                if (faction == FactionTypesExt.NULL)
                {
                    canidates = TempManager.ExternalEnemyTechs;
                }
                else
                {
                    if (UseFactionSubTypes)
                    {
                        canidates = TempManager.ExternalEnemyTechs.FindAll
                            (delegate (BaseTemplate cand) { return KickStart.CorpExtToCorp(cand.faction) == KickStart.CorpExtToCorp(faction); });
                        UseFactionSubTypes = false;
                    }
                    else
                    {
                        canidates = TempManager.ExternalEnemyTechs.FindAll
                        (delegate (BaseTemplate cand) { return cand.faction == faction; });
                    }
                }

                bool cantSpawnErad = !KickStart.EnemyEradicators || SpecialAISpawner.Eradicators.Count >= KickStart.MaxEradicatorTechs;
                canidates = canidates.FindAll(delegate (BaseTemplate cand)
                {
                    List<BasePurpose> techPurposes = cand.purposes;
                    if (Singleton.Manager<ManGameMode>.inst.IsCurrentModeMultiplayer() && techPurposes.Contains(BasePurpose.MPUnsafe))
                    {   // no illegal base in MP
                        return false;
                    }
                    if (purposes.Contains(BasePurpose.HarvestingNoHQ))
                    {
                        if (techPurposes.Contains(BasePurpose.Headquarters))
                            return false;
                        if (techPurposes.Contains(BasePurpose.Harvesting))
                            return true;
                        return false;
                    }
                    if (purposes.Contains(BasePurpose.AnyNonHQ))
                    {
                        if (techPurposes.Contains(BasePurpose.Headquarters))
                            return false;
                        return true;
                    }
                    if (!purposes.Contains(BasePurpose.NotStationary) && techPurposes.Contains(BasePurpose.NotStationary))
                        return false;
                    if (!searchAttract && techPurposes.Contains(BasePurpose.AttractTech))
                        return false;
                    if (searchAttract && techPurposes.Contains(BasePurpose.NoWeapons))
                        return false;
                    if (unProvoked && techPurposes.Contains(BasePurpose.NoWeapons))
                        return true;
                    if (cantSpawnErad && techPurposes.Contains(BasePurpose.NANI))
                        return false;

                    if (purposes.Contains(BasePurpose.Harvesting) && techPurposes.Contains(BasePurpose.NotStationary) && !purposes.Contains(BasePurpose.NotStationary))
                        return false;

                    if (techPurposes.Count == 0)
                        return false;

                    bool valid = true;
                    foreach (BasePurpose purpose in purposes)
                    {
                        if (!techPurposes.Contains(purpose))
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
                        (delegate (BaseTemplate cand) { return AIERepair.DesignMemory.JSONToMemoryExternal(cand.savedTech).Count <= MaxBlockLimitAttract; });
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
        
        internal static int GetExternalIndex(FactionTypesExt faction, BasePurpose purpose, BaseTerrain terra, bool searchAttract = false, int maxGrade = 99, int maxPrice = 0, bool unProvoked = false)
        {
            try
            {
                return GetExternalIndexes(faction, purpose, terra, searchAttract, maxGrade, maxPrice, unProvoked).GetRandomEntry();
            }
            catch { }

            return -1;
        }
        internal static int GetExternalIndex(FactionTypesExt faction, List<BasePurpose> purposes, BaseTerrain terra, bool searchAttract = false, int maxGrade = 99, int maxPrice = 0, bool unProvoked = false)
        {
            try
            {
                return GetExternalIndexes(faction, purposes, terra, searchAttract, maxGrade, maxPrice, unProvoked).GetRandomEntry();
            }
            catch { }

            return -1;
        }

        internal static bool ShouldUseCustomTechs(out List<int> validIndexes, FactionTypesExt faction, BasePurpose purpose, BaseTerrain terra, bool searchAttract = false, int maxGrade = 99, int maxPrice = 0, bool unProvoked = false)
        {
            bool cacheSubTypes = UseFactionSubTypes;
            validIndexes = GetExternalIndexes(faction, purpose, terra, searchAttract, maxGrade, maxPrice, unProvoked);
            int CustomTechs = validIndexes.Count;
            UseFactionSubTypes = cacheSubTypes;
            List<SpawnBaseTypes> SBT = GetEnemyBaseTypes(faction, purpose, terra, searchAttract, maxGrade, maxPrice, unProvoked);
            UseFactionSubTypes = cacheSubTypes;
            int PrefabTechs = SBT.Count;
             
            if (validIndexes.First() == -1)
                CustomTechs = 0;
            if (SBT.First() == SpawnBaseTypes.NotAvail)
                PrefabTechs = 0;

            int CombinedVal = CustomTechs + PrefabTechs;

            if (KickStart.TryForceOnlyPlayerSpawns)
            {
                if (CustomTechs > 0)
                {
                    //Debug.Log("TACtical_AI: ShouldUseCustomTechs - Forced Local Techs spawn possible: true");
                    //Debug.Log("TACtical_AI: ShouldUseCustomTechs - Indexes Available: ");
                    StringBuilder SB = new StringBuilder();
                    foreach (int val in validIndexes)
                    {
                        SB.Append(val + ", ");
                    }
                    Debug.Log(SB.ToString());
                    return true;
                }
                //else
                //    Debug.Log("TACtical_AI: ShouldUseCustomTechs - Forced Player-Made Techs spawn possible: false");
            }
            else
            {
                if (PrefabTechs == 0)
                {
                    if (CustomTechs > 0)
                    {
                        //Debug.Log("TACtical_AI: ShouldUseCustomTechs - There's only Local Techs available");
                        //Debug.Log("TACtical_AI: ShouldUseCustomTechs - Indexes Available: ");
                        StringBuilder SB = new StringBuilder();
                        foreach (int val in validIndexes)
                        {
                            SB.Append(val + ", ");
                        }
                        Debug.Log(SB.ToString());
                        return true;
                    }
                    //else
                    //    Debug.Log("TACtical_AI: ShouldUseCustomTechs - No Techs found");
                    return false;
                }
                float RAND = UnityEngine.Random.Range(0, CombinedVal);
                //Debug.Log("TACtical_AI: ShouldUseCustomTechs - Chance " + CustomTechs + "/" + CombinedVal + ", meaning a " + (int)(((float)CustomTechs / (float)CombinedVal) * 100f) + "% chance.   RAND value " + RAND);
                if (RAND > PrefabTechs)
                {
                    return true;
                }
            }
            return false;
        }
        internal static bool ShouldUseCustomTechs(FactionTypesExt faction, List<BasePurpose> purposes, BaseTerrain terra, bool searchAttract = false, int maxGrade = 99, int maxPrice = 0, bool unProvoked = false)
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

            AIERepair.TurboconstructExt(theTech, AIERepair.DesignMemory.JSONToMemoryExternal(baseBlueprint), false);
            Debug.Log("TACtical_AI: ReconstructPlayerTech - Retrofitted player FTUE tech to " + GetEnglishName(toSpawn));
        }



        // Use this for external cases
        /// <summary>
        /// Spawns a RawTech IMMEDEATELY.  Do NOT Call while calling BlockMan or spawner blocks or the game will break!
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="Team"></param>
        /// <param name="facingDirect"></param>
        /// <param name="Blueprint"></param>
        /// <param name="AutoTerrain"></param>
        /// <param name="Charged"></param>
        /// <param name="ForceInstant"></param>
        /// <returns></returns>
        public static Tank SpawnTechExternal(Vector3 pos, int Team, Vector3 facingDirect, BuilderExternal Blueprint, bool AutoTerrain = false, bool Charged = false, bool RandSkins = false)
        {
            if (Blueprint == null)
            {
                Debug.Log("TACtical_AI: SpawnTechExternal - Was handed a NULL Blueprint! \n" + StackTraceUtility.ExtractStackTrace());
                return null;
            }
            string baseBlueprint = Blueprint.Blueprint;

            Tank theTech = InstantTech(pos, facingDirect, Team, Blueprint.Name, baseBlueprint, AutoTerrain, ForceAnchor: Blueprint.IsAnchored, Team == -1, RandSkins);

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
                AIERepair.TurboconstructExt(theTech, AIERepair.DesignMemory.JSONToMemoryExternal(baseBlueprint), Charged);

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

        /// <summary>
        /// Spawns a RawTech safely.  There is an update delay on call so it's not immedeate on call.
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="Team"></param>
        /// <param name="facingDirect"></param>
        /// <param name="Blueprint"></param>
        /// <param name="AutoTerrain"></param>
        /// <param name="Charged"></param>
        /// <param name="ForceInstant"></param>
        /// <param name="AfterAction">Assign the action you want given the spawned Tech after it spawns.</param>
        public static void SpawnTechExternalSafe(Vector3 pos, int Team, Vector3 facingDirect, BuilderExternal Blueprint, bool AutoTerrain = false, bool randomSkins = false, Action<Tank> AfterAction = null)
        {
            if (Blueprint == null)
            {
                Debug.Log("TACtical_AI: SpawnTechExternal - Was handed a NULL Blueprint! \n" + StackTraceUtility.ExtractStackTrace());
                return;
            }
            QueueInstantTech queue;
            queue = new QueueInstantTech(AfterAction, pos, facingDirect, Team, Blueprint.Name, Blueprint.Blueprint, AutoTerrain, Blueprint.IsAnchored, Team == -1, randomSkins);
            TechBacklog.Add(queue);
            Debug.Log("TACtical_AI: SpawnTechExternalSafe - Adding to Queue - In Queue: " + TechBacklog.Count);
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
            if (Singleton.Manager<ManWorld>.inst.CheckIsTileAtPositionLoaded(position) && Singleton.Manager<ManSpawn>.inst.IsBlockAllowedInCurrentGameMode(type))
            {
                worked = true;

                TankBlock block = Singleton.Manager<ManLooseBlocks>.inst.HostSpawnBlock(type, position, quat, false);
                if (block.GetComponent<Damageable>())
                {
                    if (!block.GetComponent<Damageable>().IsAtFullHealth)
                        block.InitNew();
                }
                return block;
            }
            else
            {   // It's trying to work out of bounds
                worked = false;
                //return null;
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
            if ((bool)theTech)
                TryForceIntoPop(theTech);
            return theTech;
        }
        internal static void InstantTechSafe(Vector3 pos, Vector3 forward, int Team, string name, string blueprint, bool grounded, bool ForceAnchor = false, bool population = false, Action<Tank> fallbackOp = null, bool randomSkins = true)
        {
            QueueInstantTech queue = new QueueInstantTech(fallbackOp, pos, forward, Team, name, blueprint, grounded, ForceAnchor, population, randomSkins);
            TechBacklog.Add(queue);
            Debug.Log("TACtical_AI: InstantTech - Adding to Queue - In Queue: " + TechBacklog.Count);
        }
        internal static Tank InstantTech(Vector3 pos, Vector3 forward, int Team, string name, string blueprint, bool grounded, bool ForceAnchor = false, bool population = false, bool randomSkins = true)
        {
            TechData data = new TechData();
            data.Name = name;
            data.m_Bounds = new IntVector3(new Vector3(18, 18, 18));
            data.m_SkinMapping = new Dictionary<uint, string>();
            data.m_TechSaveState = new Dictionary<int, TechComponent.SerialData>();
            data.m_CreationData = new TechData.CreationData();
            data.m_BlockSpecs = new List<TankPreset.BlockSpec>();
            List<BlockMemory> mems = AIERepair.DesignMemory.JSONToMemoryExternal(blueprint);

            bool skinChaotic = false;
            if (randomSkins)
            {
                skinChaotic = UnityEngine.Random.Range(0, 100) < 2;
            }
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

                if (randomSkins)
                {
                    FactionTypesExt factType = KickStart.GetCorpExtended(type);
                    FactionSubTypes FST = KickStart.CorpExtToCorp(factType);
                    if (skinChaotic)
                    {
                        spec.m_SkinID = GetSkinIDRand((int)FST);
                    }
                    else
                    {
                        spec.m_SkinID = GetSkinIDSet((int)FST);
                    }
                }
                else
                    spec.m_SkinID = 0;

                data.m_BlockSpecs.Add(spec);
            }
            ResetSkinIDSet();
            ManSpawn.TankSpawnParams tankSpawn = new ManSpawn.TankSpawnParams();
            tankSpawn.techData = data;
            tankSpawn.blockIDs = null;
            tankSpawn.teamID = Team;
            tankSpawn.position = pos;
            tankSpawn.rotation = Quaternion.LookRotation(forward, Vector3.up);//Singleton.cameraTrans.position - pos
            tankSpawn.ignoreSceneryOnSpawnProjection = false;
            tankSpawn.forceSpawn = true;
            tankSpawn.isPopulation = population;
            if (ForceAnchor)
                tankSpawn.grounded = true;
            else
                tankSpawn.grounded = grounded;
            Tank theTech = Singleton.Manager<ManSpawn>.inst.SpawnTank(tankSpawn, true);
            if (theTech.IsNull())
            {
                Debug.Log("TACtical_AI: InstantTech - error on SpawnTank");
                return null;
            }
            else
                TryForceIntoPop(theTech);
            if (ForceAnchor)
            {
                if (!theTech.IsAnchored)
                {
                    theTech.FixupAnchors(true);
                    //theTech.Anchors.UnanchorAll(false);
                    //theTech.TryToggleTechAnchor();
                }
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

            ForceAllBubblesUp(theTech);
            ReconstructConveyorSequencing(theTech);
            if (ForceAnchor)
                theTech.gameObject.AddComponent<RequestAnchored>();

            Debug.Log("TACtical_AI: InstantTech - Built " + name);

            return theTech;
        }

        internal static TechData ExportRawTechToTechData(string name, string blueprint, out int[] blockIDs)
        {
            TechData data = new TechData();
            data.Name = name;
            data.m_Bounds = new IntVector3(new Vector3(18, 18, 18));
            data.m_SkinMapping = new Dictionary<uint, string>();
            data.m_TechSaveState = new Dictionary<int, TechComponent.SerialData>();
            data.m_CreationData = new TechData.CreationData();
            data.m_BlockSpecs = new List<TankPreset.BlockSpec>();
            List<BlockMemory> mems = AIERepair.DesignMemory.JSONToMemoryExternal(blueprint);
            List<int> BTs = new List<int>();

            bool skinChaotic = UnityEngine.Random.Range(0, 100) < 2;

            foreach (BlockMemory mem in mems)
            {
                BlockTypes type = AIERepair.StringToBlockType(mem.t);
                if (!Singleton.Manager<ManSpawn>.inst.IsBlockAllowedInCurrentGameMode(type) || !TechDataAvailValidation.IsBlockAvailableInMode(type))
                {
                    Debug.Log("TACtical_AI: InstantTech - Removed " + mem.t + " as it was invalidated");
                    continue;
                }
                if (!BTs.Contains((int)type))
                {
                    BTs.Add((int)type);
                }

                TankPreset.BlockSpec spec = default;
                spec.block = mem.t;
                spec.m_BlockType = type;
                spec.orthoRotation = new OrthoRotation(mem.r);
                spec.position = mem.p;
                spec.saveState = new Dictionary<int, Module.SerialData>();
                spec.textSerialData = new List<string>();
                FactionTypesExt factType = KickStart.GetCorpExtended(type);
                FactionSubTypes FST = KickStart.CorpExtToCorp(factType);
                if (skinChaotic)
                {
                    spec.m_SkinID = GetSkinIDRand((int)FST);
                }
                else
                {
                    spec.m_SkinID = GetSkinIDSet((int)FST);
                }

                data.m_BlockSpecs.Add(spec);
            }
            ResetSkinIDSet();
            //Debug.Log("TACtical_AI: ExportRawTechToTechData - Exported " + name);

            blockIDs = BTs.ToArray();
            return data;
        }

        private static Dictionary<int, List<byte>> valid = new Dictionary<int, List<byte>>();
        private static Dictionary<int, byte> valid2 = new Dictionary<int, byte>();
        internal static void ResetSkinIDSet()
        {
            valid.Clear();
            valid2.Clear();
        }
        internal static byte GetSkinIDSet(int faction)
        {
            if (valid2.TryGetValue(faction, out byte num))
            {
                return num;
            }
            else
            {
                try
                {
                    byte pick = GetSkinIDRand(faction);
                    valid2.Add(faction, pick);
                    return pick;
                }
                catch { }// corp has no skins!
            }
            return 0;
        }
        internal static byte GetSkinIDSetForTeam(int team, int faction)
        {
            if (valid2.TryGetValue(faction, out byte num))
            {
                return num;
            }
            else
            {
                try
                {
                    byte pick = GetSkinIDCase(team, faction);
                    valid2.Add(faction, pick);
                    return pick;
                }
                catch { }// corp has no skins!
            }
            return 0;
        }
        internal static byte GetSkinIDRand(int faction)
        {
            if (valid.TryGetValue(faction, out List<byte> num))
            {
                return num.GetRandomEntry();
            }
            else
            {
                try
                {
                    FactionSubTypes FST = (FactionSubTypes)faction;
                    List<byte> num2 = new List<byte>();
                    int count = ManCustomSkins.inst.GetNumSkinsInCorp(FST);
                    for (int step = 0; step < count; step++)
                    {
                        byte skin = ManCustomSkins.inst.SkinIndexToID((byte)step, FST);
                        if (!ManDLC.inst.IsSkinLocked(skin, FST))
                        {
                            num2.Add(skin);
                            //Debug.Log("SKINSSSSSS " + ManCustomSkins.inst.GetSkinNameForSnapshot(FST, skin));
                        }
                    }
                    valid.Add(faction, num2);
                    return num2.GetRandomEntry();
                }
                catch { }// corp has no skins!
            }
            return 0;
        }

        internal static byte GetSkinIDCase(int team, int faction)
        {
            if (valid.TryGetValue(faction, out List<byte> num))
            {
                return num[team % (num.Count - 1)];
            }
            else
            {
                try
                {
                    FactionSubTypes FST = (FactionSubTypes)faction;
                    int count = ManCustomSkins.inst.GetNumSkinsInCorp(FST);
                    List<byte> num2 = new List<byte>();
                    for (int step = 0; step < count; step++)
                    {
                        byte skin = ManCustomSkins.inst.SkinIndexToID((byte)step, FST);
                        if (!ManDLC.inst.IsSkinLocked(skin, FST))
                        {
                            num2.Add(skin);
                        }
                    }
                    valid.Add(faction, num2);
                    return num2[team % (num2.Count - 1)];
                }
                catch { }// corp has no skins!
            }
            return 0;
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
                    //ManVisible.inst.StopTrackingVisible(tank.visible.ID);
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
            if (mind?.Tank && !mind.Tank.name.NullOrEmpty())
            {
                SpawnBaseTypes type = GetEnemyBaseTypeFromName(mind.Tank.name);
                if (type != SpawnBaseTypes.NotAvail)
                {
                    if (TempManager.techBases.TryGetValue(type, out BaseTemplate val))
                        can = !val.environ;
                }
                else if (TempManager.ExternalEnemyTechs.Exists(delegate (BaseTemplate cand) { return cand.techName == mind.Tank.name; }))
                {
                    can = !TempManager.ExternalEnemyTechs.Find(delegate (BaseTemplate cand) { return cand.techName == mind.Tank.name; }).environ;
                }
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

        internal static List<SpawnBaseTypes> GetEnemyBaseTypes(FactionTypesExt faction, BasePurpose purpose, BaseTerrain terra, bool searchAttract = false, int maxGrade = 99, int maxPrice = 0, bool unProvoked = false)
        {
            try
            {
                // Filters
                List<KeyValuePair<SpawnBaseTypes, BaseTemplate>> canidates;
                if (faction == FactionTypesExt.NULL)
                {
                    canidates = TempManager.techBases.ToList();
                    UseFactionSubTypes = false;
                }
                else
                {
                    if (UseFactionSubTypes)
                    {
                        canidates = TempManager.techBases.ToList().FindAll
                            (delegate (KeyValuePair<SpawnBaseTypes, BaseTemplate> cand) { return KickStart.CorpExtToCorp(cand.Value.faction) == KickStart.CorpExtToCorp(faction); });
                        UseFactionSubTypes = false;
                    }
                    else
                    {
                        canidates = TempManager.techBases.ToList().FindAll
                        (delegate (KeyValuePair<SpawnBaseTypes, BaseTemplate> cand) { return cand.Value.faction == faction; });
                    }
                }

                bool cantSpawnErad = !KickStart.EnemyEradicators || SpecialAISpawner.Eradicators.Count >= KickStart.MaxEradicatorTechs;
                canidates = canidates.FindAll(delegate (KeyValuePair<SpawnBaseTypes, BaseTemplate> cand)
                {
                    List<BasePurpose> techPurposes = cand.Value.purposes;
                    if (Singleton.Manager<ManGameMode>.inst.IsCurrentModeMultiplayer() && techPurposes.Contains(BasePurpose.MPUnsafe))
                    {   // no illegal base in MP
                        return false;
                    }
                    if (purpose == BasePurpose.HarvestingNoHQ)
                    {
                        if (techPurposes.Contains(BasePurpose.Headquarters))
                            return false;
                        if (techPurposes.Contains(BasePurpose.Harvesting))
                            return true;
                        return false;
                    }
                    if (purpose == BasePurpose.AnyNonHQ)
                    {
                        if (techPurposes.Exists(delegate (BasePurpose cand2) { return cand2 == BasePurpose.Headquarters || cand2 == BasePurpose.NotStationary; }))
                            return false;
                        return true;
                    }
                    bool notStationary = techPurposes.Contains(BasePurpose.NotStationary);
                    if (purpose != BasePurpose.NotStationary && notStationary)
                        return false;
                    if (!searchAttract && techPurposes.Contains(BasePurpose.AttractTech))
                        return false;
                    bool noWeapons = techPurposes.Contains(BasePurpose.NoWeapons);
                    if (searchAttract && noWeapons)
                        return false;
                    if (unProvoked && noWeapons)
                        return true;
                    if (cantSpawnErad && techPurposes.Contains(BasePurpose.NANI))
                        return false;

                    if (purpose == BasePurpose.Harvesting && notStationary)
                        return false;

                    if (techPurposes.Count == 0)
                        return false;
                    return techPurposes.Contains(purpose);
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
                        (delegate (KeyValuePair<SpawnBaseTypes, BaseTemplate> cand) { return AIERepair.DesignMemory.JSONToMemoryExternal(cand.Value.savedTech).Count <= MaxBlockLimitAttract; });
                }
                // finally, remove those which are N/A

                //Debug.Log("TACtical_AI: GetEnemyBaseTypes - Found " + canidates.Count + " options");
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
        internal static List<SpawnBaseTypes> GetEnemyBaseTypes(FactionTypesExt faction, List<BasePurpose> purposes, BaseTerrain terra, bool searchAttract = false, int maxGrade = 99, int maxPrice = 0, bool unProvoked = false)
        {
            try
            {
                // Filters
                List<KeyValuePair<SpawnBaseTypes, BaseTemplate>> canidates;
                if (faction == FactionTypesExt.NULL)
                {
                    canidates = TempManager.techBases.ToList();
                    UseFactionSubTypes = false;
                }
                else
                {
                    if (UseFactionSubTypes)
                    {
                        canidates = TempManager.techBases.ToList().FindAll
                            (delegate (KeyValuePair<SpawnBaseTypes, BaseTemplate> cand) { return KickStart.CorpExtToCorp(cand.Value.faction) == KickStart.CorpExtToCorp(faction); });
                        UseFactionSubTypes = false;
                    }
                    else
                    {
                        canidates = TempManager.techBases.ToList().FindAll
                        (delegate (KeyValuePair<SpawnBaseTypes, BaseTemplate> cand) { return cand.Value.faction == faction; });
                    }
                }

                canidates = canidates.FindAll(delegate (KeyValuePair<SpawnBaseTypes, BaseTemplate> cand)
                {
                    List<BasePurpose> techPurposes = cand.Value.purposes;
                    if (Singleton.Manager<ManGameMode>.inst.IsCurrentModeMultiplayer() && techPurposes.Contains(BasePurpose.MPUnsafe))
                    {   // no illegal base in MP
                        return false;
                    }
                    if (purposes.Contains(BasePurpose.HarvestingNoHQ))
                    {
                        if (techPurposes.Contains(BasePurpose.Headquarters))
                            return false;
                        if (techPurposes.Contains(BasePurpose.Harvesting))
                            return true;
                        return false;
                    }
                    if (purposes.Contains(BasePurpose.AnyNonHQ))
                    {
                        if (techPurposes.Contains(BasePurpose.Headquarters))
                            return false;
                        return true;
                    }
                    if (!purposes.Contains(BasePurpose.NotStationary) && techPurposes.Contains(BasePurpose.NotStationary))
                        return false;
                    if (!searchAttract && techPurposes.Contains(BasePurpose.AttractTech))
                        return false;
                    if (searchAttract && techPurposes.Contains(BasePurpose.NoWeapons))
                        return false;
                    if (unProvoked && techPurposes.Contains(BasePurpose.NoWeapons))
                        return true;
                    if ((!KickStart.EnemyEradicators || SpecialAISpawner.Eradicators.Count >= KickStart.MaxEradicatorTechs) && techPurposes.Contains(BasePurpose.NANI))
                        return false;

                    if (purposes.Contains(BasePurpose.Harvesting) && techPurposes.Contains(BasePurpose.NotStationary) && !purposes.Contains(BasePurpose.NotStationary))
                        return false;

                    if (techPurposes.Count == 0)
                        return false;

                    bool valid = true;
                    foreach (BasePurpose purpose in purposes)
                    {
                        if (!techPurposes.Contains(purpose))
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
                        (delegate (KeyValuePair<SpawnBaseTypes, BaseTemplate> cand) { return AIERepair.DesignMemory.JSONToMemoryExternal(cand.Value.savedTech).Count <= MaxBlockLimitAttract; });
                }
                // finally, remove those which are N/A

                //Debug.Log("TACtical_AI: GetEnemyBaseTypes - Found " + canidates.Count + " options");
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
      
        internal static List<SpawnBaseTypes> FallbackHandler(FactionTypesExt faction)
        {
            try
            {
                // Filters
                List<KeyValuePair<SpawnBaseTypes, BaseTemplate>> canidates;
                if (faction == FactionTypesExt.NULL)
                {
                    canidates = TempManager.techBases.ToList();
                }
                else
                {
                    FactionSubTypes fallback = KickStart.CorpExtToCorp(faction);
                    canidates = TempManager.techBases.ToList().FindAll
                        (delegate (KeyValuePair<SpawnBaseTypes, BaseTemplate> cand) { return (FactionSubTypes)cand.Value.faction == fallback; });
                }

                canidates = canidates.FindAll(delegate (KeyValuePair<SpawnBaseTypes, BaseTemplate> cand)
                {
                    if (ManNetwork.IsNetworked && cand.Value.purposes.Contains(BasePurpose.MPUnsafe))
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
                {
                    Debug.Log("TACtical_AI: FallbackHandler - COULD NOT FIND FALLBACK FOR " + KickStart.CorpExtToCorp(faction));
                    return new List<SpawnBaseTypes> { SpawnBaseTypes.NotAvail };
                }

                // final list compiling
                List<SpawnBaseTypes> final = new List<SpawnBaseTypes>();

                foreach (KeyValuePair<SpawnBaseTypes, BaseTemplate> pair in canidates)
                    final.Add(pair.Key);

                final.Shuffle();

                return final;
            }
            catch { } // we resort to legacy
            Debug.Log("TACtical_AI: FallbackHandler(ERROR) - COULD NOT FIND FALLBACK FOR " + KickStart.CorpExtToCorp(faction));
            return new List<SpawnBaseTypes> { SpawnBaseTypes.NotAvail };
        }

        internal static SpawnBaseTypes GetEnemyBaseType(FactionTypesExt faction, BasePurpose purpose, BaseTerrain terra, bool searchAttract = false, int maxGrade = 99, int maxPrice = 0, bool unProvoked = false)
        {
            if (ForceSpawn && !searchAttract)
                return forcedBaseSpawn;

            try
            {
                return GetEnemyBaseTypes(faction, purpose, terra, searchAttract, maxGrade, maxPrice, unProvoked).GetRandomEntry();
            }
            catch { } // we resort to legacy

            int lowerRANDRange = 1;
            int higherRANDRange = 20;
            if (faction == FactionTypesExt.GSO)
            {
                lowerRANDRange = 1;
                higherRANDRange = 6;
            }
            else if (faction == FactionTypesExt.GC)
            {
                lowerRANDRange = 7;
                higherRANDRange = 10;
            }
            else if (faction == FactionTypesExt.VEN)
            {
                lowerRANDRange = 11;
                higherRANDRange = 14;
            }
            else if (faction == FactionTypesExt.HE)
            {
                lowerRANDRange = 15;
                higherRANDRange = 20;
            }

            return (SpawnBaseTypes)UnityEngine.Random.Range(lowerRANDRange, higherRANDRange);
        }
        internal static SpawnBaseTypes GetEnemyBaseType(FactionTypesExt faction, List<BasePurpose> purposes, BaseTerrain terra, bool searchAttract = false, int maxGrade = 99, int maxPrice = 0, bool unProvoked = false)
        {
            if (ForceSpawn && !searchAttract)
                return forcedBaseSpawn;

            try
            {
                return GetEnemyBaseTypes(faction, purposes, terra, searchAttract, maxGrade, maxPrice, unProvoked).GetRandomEntry();
            }
            catch { }
            return SpawnBaseTypes.NotAvail;
        }

        // External techs
        internal static BaseTemplate GetExtEnemyBaseFromName(string Name)
        {
            int nameNum = Name.GetHashCode();
            try
            {
                int lookup = TempManager.ExternalEnemyTechs.FindIndex(delegate (BaseTemplate cand) { return cand.techName.GetHashCode() == nameNum; });
                if (lookup == -1) 
                    return null;
                return TempManager.ExternalEnemyTechs[lookup];
            }
            catch
            {
                return null;
            }
        }

        // SpawnBaseTypes (Built-In)
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
            return KickStart.CorpExtToCorp(GetBaseTemplate(toSpawn).faction);
        }
        internal static FactionTypesExt GetMainCorpExt(SpawnBaseTypes toSpawn)
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
            List<BlockMemory> mem = AIERepair.DesignMemory.JSONToMemoryExternal(JSONTechBlueprint);
            foreach (BlockMemory block in mem)
            {
                output += Singleton.Manager<RecipeManager>.inst.GetBlockBuyPrice(AIERepair.StringToBlockType(block.t), true);
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
        public static bool IsEnemyBaseTeam(int team)
        {
            return (team >= EnemyBaseTeamsStart && team <= EnemyBaseTeamsEnd) || team == SpecialAISpawner.trollTeam;
        }
        public static int GetRandomEnemyBaseTeam()
        {
            return UnityEngine.Random.Range(EnemyBaseTeamsStart, EnemyBaseTeamsEnd);
        }
        private static void MakeSureCanExistWithBase(Tank tank)
        {
            if (!tank.IsFriendly(tank.Team) || tank.Team == -1)
            {
                int set = GetRandomEnemyBaseTeam();
                Debug.Log("TACtical_AI: Tech " + tank.name + " spawned team " + tank.Team + " that fights against themselves, setting to team " + set + " instead");
                tank.SetTeam(set, false);
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
                if (ManNetwork.IsNetworked && !ManNetwork.IsHost)
                    return;

                List<BlockMemory> mems = new List<BlockMemory>();
                List<TankBlock> blocs = new List<TankBlock>();
                foreach (TankBlock chain in tank.blockman.IterateBlocks())
                {   // intel
                    if (chain.GetComponent<ModuleItemConveyor>())
                    {
                        BlockMemory mem = new BlockMemory();
                        mem.r = chain.cachedLocalRotation.rot;
                        mem.p = chain.cachedLocalPosition;
                        mems.Add(mem);
                        blocs.Add(chain);
                    }
                }
                if (mems.Count() == 0)
                    return;

                foreach (TankBlock block in blocs)
                {   // detach
                    try
                    {
                        if (block.IsAttached)
                            tank.blockman.Detach(block, false, false, false);
                    }
                    catch
                    {
                        Debug.Log("TACtical_AI: ReconstructConveyorSequencing - error 1");
                    }
                }

                foreach (BlockMemory mem in mems)
                {   // reconstruct
                    try
                    {
                        if (!tank.blockman.AddBlockToTech(blocs[mems.IndexOf(mem)], mem.p, new OrthoRotation(mem.r)))
                            Debug.Log("TACtical_AI: ReconstructConveyorSequencing - error 3");
                    }
                    catch
                    {
                        Debug.Log("TACtical_AI: ReconstructConveyorSequencing - error 2");
                    }
                }
            }
            catch
            {
                Debug.Log("TACtical_AI: ReconstructConveyorSequencing - error 0");
            }
        }

        internal static FieldInfo charge = typeof(ModuleShieldGenerator).GetField("m_EnergyDeficit", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static FieldInfo charge2 = typeof(ModuleShieldGenerator).GetField("m_State", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static FieldInfo charge3 = typeof(ModuleShieldGenerator).GetField("m_Shield", BindingFlags.NonPublic | BindingFlags.Instance);
        private static void ForceAllBubblesUp(Tank tank)
        {
            try
            {
                if (ManNetwork.IsNetworked && !ManNetwork.IsHost)
                    return;

                foreach (ModuleShieldGenerator buubles in tank.blockman.IterateBlockComponents<ModuleShieldGenerator>())
                {   
                    if ((bool)buubles)
                    {
                        charge.SetValue(buubles, 0);
                        charge2.SetValue(buubles, 2);
                        BubbleShield shield = (BubbleShield)charge3.GetValue(buubles);
                        shield.SetTargetScale(buubles.m_Radius);
                    }
                }
            }
            catch
            {
                Debug.Log("TACtical_AI: ForceAllBubblesUp - error");
            }
        }
        public static void ChargeAndClean(Tank tank)
        {
            try
            {
                tank.EnergyRegulator.SetAllStoresAmount(1);
                ForceAllBubblesUp(tank);
            }
            catch
            {
                Debug.Log("TACtical_AI: ChargeAndClean - error");
            }
        }
    }
}
