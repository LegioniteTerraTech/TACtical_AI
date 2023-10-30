using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using TerraTechETCUtil;
using TAC_AI.Templates;
using TAC_AI.AI.Enemy;

namespace TAC_AI.World
{
    public class UnloadedBases
    {
        public static NP_BaseUnit RefreshTeamMainBaseIfAnyPossible(NP_Presence EP)
        {
            if (EP.EBUs.Count == 0)
            {
                //DebugTAC_AI.Log("TACtical_AI: " + Team + " CALLED GetTeamFunds WITH NO BASE!!!");
                EP.MainBase = null;
                return null;
            }
            if (EP.MainBase != null && EP.MainBase.Exists())
            {
                return EP.MainBase;
            }
            else if (EP.EBUs.Count > 1)
            {
                //DebugTAC_AI.Log("TACtical_AI: " + EP.Team + " has " + EP.EBUs.Count + " bases on scene. The richest will be selected.");
                NP_BaseUnit funder = null;
                int highestFunds = -1;
                foreach (NP_BaseUnit funds in EP.EBUs)
                {
                    if (highestFunds < funds.BuildBucks)
                    {
                        highestFunds = funds.BuildBucks;
                        funder = funds;
                    }
                }
                EP.MainBase = funder;
                return funder;
            }
            EP.MainBase = EP.EBUs.FirstOrDefault();
            return EP.MainBase;
        }

        public static void RecycleLoadedTechToTeam(Tank tank)
        {
            if (ManBaseTeams.BaseTeamExists(tank.Team))
                RLoadedBases.RecycleTechToTeam(tank);
            else
            {
                NP_Presence EP = ManEnemyWorld.GetTeam(tank.Team);
                if (EP == null)
                    return;
                NP_BaseUnit EBU = RefreshTeamMainBaseIfAnyPossible(EP);
                if (EBU == null)
                    return;
                int tankCost = RawTechTemplate.GetBBCost(tank);
                EBU.AddBuildBucks(tankCost);
                SpecialAISpawner.Purge(tank);
            }
        }

        public static bool HasTooMuchOfType(NP_Presence EP, BasePurpose purpose)
        {
            int Count = 0;
            int Team = EP.Team;

            if (purpose == BasePurpose.Defense)
            {
                foreach (NP_TechUnit ETU in EP.EMUs)
                {
                    if (ETU.GetSpeed() < 10)
                    {
                        Count++;
                    }
                }
            }
            else
            foreach (NP_BaseUnit EBU in EP.EBUs)
            {
                switch (purpose) {
                    case BasePurpose.HasReceivers:
                        if (EBU.canHarvest)
                            Count++;
                        break;
                    case BasePurpose.Autominer:
                        if (EBU.revenue > 1)
                            Count++;
                        break;
                    case BasePurpose.TechProduction:
                        if (EBU.isTechBuilder)
                            Count++;
                        break;
                    case BasePurpose.Headquarters:
                        if (EBU.isSiegeBase)
                            Count++;
                        break;
                }
            }

            bool thisIsTrue;
            if (purpose == BasePurpose.Defense)
            {
                thisIsTrue = Count >= RLoadedBases.MaxDefenses;
                if (thisIsTrue)
                    DebugTAC_AI.Log("TACtical_AI: HasTooMuchOfType - Team " + Team + " already has too many defenses and cannot make more");
            }
            else if (purpose == BasePurpose.Autominer)
            {
                thisIsTrue = Count >= RLoadedBases.MaxAutominers;
                if (thisIsTrue)
                    DebugTAC_AI.Log("TACtical_AI: HasTooMuchOfType - Team " + Team + " already has too many autominers and cannot make more");
            }
            else if (purpose == BasePurpose.HasReceivers && RLoadedBases.FetchNearbyResourceCounts(Team) < AIGlobals.MinResourcesReqToCollect)
            {
                thisIsTrue = false;
                DebugTAC_AI.Log("TACtical_AI: HasTooMuchOfType - Team " + Team + " Does not have enough mineables in range to build Reciever bases.");
            }
            else
            {
                thisIsTrue = Count >= RLoadedBases.MaxSingleBaseType;
                if (thisIsTrue)
                    DebugTAC_AI.Log("TACtical_AI: HasTooMuchOfType - Team " + Team + " already has too many of type " + purpose.ToString() + " and cannot make more");
            }

            return thisIsTrue;
        }


        /// <summary>
        /// Tile-based target-finding 
        /// </summary>
        /// <param name="ETU"></param>
        public static void GetScannedTilesAroundTech(NP_TechUnit ETU)
        {
            NP_Presence EP = ETU.teamInst;
            IntVector2 scanPos = ETU.tilePos;
            int dist = ManEnemyWorld.UnitSightRadius;
            if (ETU is NP_BaseUnit)
                dist = ManEnemyWorld.BaseSightRadius;
            GetScannedTilesAtCoord(EP, scanPos, dist);
        }
        public static void GetScannedTilesAtCoord(NP_Presence EP, IntVector2 scanPos, int sightRadTiles = ManEnemyWorld.UnitSightRadius)
        {
            if (EP.attackStarted || EP.scannedPositions.Contains(scanPos))
                return;
            EP.scannedPositions.Add(scanPos); 
            SearchPatternCachFetch(scanPos, sightRadTiles, ref EP.scannedEnemyTiles);
        }
        private static List<Vector2> cachSearchPattern = new List<Vector2>();
        private static void SearchPatternCachFetch(IntVector2 tilePos, int Dist, ref HashSet<IntVector2> SearchedTiles)
        {
            cachSearchPattern.Clear();
            int sightRad = Dist;
            int sightRad2 = sightRad * sightRad;
            for (int stepx = -sightRad; stepx < sightRad; stepx++)
            {
                for (int stepy = -sightRad; stepy < sightRad; stepy++)
                {
                    Vector2 V2 = new Vector2(stepx, stepy);
                    if (V2.sqrMagnitude <= sightRad2)
                    {
                        cachSearchPattern.Add(V2 + (Vector2)tilePos);
                    }
                }
            }

            int numScanned = 0;
            foreach (IntVector2 IV2 in cachSearchPattern)
            {
                if (!SearchedTiles.Contains(IV2))
                    SearchedTiles.Add(IV2);
                //DebugTAC_AI.Log("TACtical_AI: SearchPatternCachFetch - Scanned " + IV2);
                numScanned++;
            }
            //DebugTAC_AI.Log("TACtical_AI: SearchPatternCachFetch - Scanned a total of " + numScanned);
        }
        internal static bool SearchPatternCacheNoSort(NP_Presence EP, HashSet<IntVector2> SearchedTiles, out IntVector2 tilePosEnemy)
        {
            tilePosEnemy = IntVector2.zero;
            foreach (IntVector2 IV2 in SearchedTiles)
            {
                if (TileHasTargetableEnemy(EP, IV2))
                {
                    tilePosEnemy = IV2;
                    DebugTAC_AI.Log("TACtical_AI: SearchPatternCacheNoSort - Enemy found at " + tilePosEnemy);
                    return true;
                }
            }
            return false;
        }
        internal static bool SearchPatternCacheSort(NP_Presence EP, IntVector2 tilePos, HashSet<IntVector2> SearchedTiles, out IntVector2 tilePosEnemy)
        {
            tilePosEnemy = IntVector2.zero;
            int numScanned = 0;
            foreach (IntVector2 IV2 in SearchedTiles.ToList().OrderBy(x => new Vector2(x.x - tilePos.x, x.y - tilePos.y).sqrMagnitude))
            {
                if (TileHasTargetableEnemy(EP, IV2))
                {
                    tilePosEnemy = IV2;
                    //DebugTAC_AI.Log("TACtical_AI: SearchPatternCacheSort - Enemy found at " + tilePosEnemy);
                    return true;
                }
                //DebugTAC_AI.Log("TACtical_AI: SearchPatternCacheSort - Scanned " + IV2);
                numScanned++;
            }
            //DebugTAC_AI.Log("TACtical_AI: SearchPatternCacheSort - Scanned a total of " + numScanned);
            return false;
        }
        public static bool TileHasTargetableEnemy(NP_Presence EP, IntVector2 tilePos)
        {
            List<NP_TechUnit> ETUe = ManEnemyWorld.GetUnloadedTechsInTile(tilePos);
            //DebugTAC_AI.Log("TACtical_AI: TileHasEnemy - Tile count " + ETUe.Count());
            return ETUe.Exists(delegate (NP_TechUnit cand) {
                DebugTAC_AI.Assert(cand == null, "TileHasEnemy - cand IS NULL");
                if (cand.tech == null)
                    return false;
                return AIGlobals.IsBaseTeam(cand.tech.m_TeamID)  
                && Tank.IsEnemy(cand.tech.m_TeamID, EP.Team); 
            });
        }
        public static IntVector2 FindTeamBaseTile(NP_Presence EP)
        {
            try
            {
                return RefreshTeamMainBaseIfAnyPossible(EP).tilePos;
            }
            catch { return IntVector2.zero; }
        }
        public static void RemoteRemove(NP_TechUnit ETU)
        {
            try
            {
                ManEnemyWorld.EntirelyRemoveUnitFromTile(ETU);
                ManEnemyWorld.StopManagingUnit(ETU);
            }
            catch (Exception e) { DebugTAC_AI.Log("TACtical_AI: RemoteRemove - Fail for " + ETU.Name + " - " + e); }
        }
        public static void RemoteRecycle(NP_TechUnit ETU)
        {
            NP_Presence EP = ManEnemyWorld.GetTeam(ETU.tech.m_TeamID);
            if (EP != null)
                EP.AddBuildBucks(RawTechTemplate.GetBBCost(ETU.tech));
            RemoteRemove(ETU);
        }
        public static void RemoteDestroy(NP_TechUnit ETU)
        {
            ManEnemyWorld.TechDestroyedEvent.Send(ETU.tech.m_TeamID, ETU.ID, false);
            RemoteRemove(ETU);
        }

        public static void PurgeAllUnder(NP_Presence EP)
        {
            int count = EP.EBUs.Count;
            for (int step = 0; step < count; count--)
            {
                NP_BaseUnit EBUcase = EP.EBUs.ElementAt(0);
                RemoteRemove(EBUcase);
            }
            int count2 = EP.EMUs.Count;
            for (int step = 0; step < count2; count2--)
            {
                NP_TechUnit ETUcase = EP.EMUs.ElementAt(0);
                RemoteRemove(ETUcase);
            }
        }

        public static bool IsPlayerWithinProvokeDist(IntVector2 tilePos)
        {
            if (Singleton.playerTank)
            {
                if ((tilePos - WorldPosition.FromScenePosition(Singleton.playerTank.boundsCentreWorld).TileCoord).WithinBox(ManEnemyWorld.EnemyRaidProvokeExtents))
                {
                    return true;
                }
            }
            return false;
        }


        // Base Operations
        public static bool PurgeIfNeeded(NP_Presence EP, NP_BaseUnit EBU)
        {
            try
            {
                if (EBU != null)
                {
                    if (KickStart.CullFarEnemyBases && (EBU.tilePos - WorldPosition.FromScenePosition(Singleton.playerPos).TileCoord).WithinBox(AIGlobals.IgnoreBaseCullingTilesFromOrigin))
                    {
                        // Note: GetBackwardsCompatiblePosition gets the SCENEposition (Position relative to the WorldTreadmillOrigin)!
                        if (!(EBU.tilePos - WorldPosition.FromScenePosition(Singleton.playerPos).TileCoord).WithinBox(ManEnemyWorld.EnemyBaseCullingExtents))
                        {
                            PurgeAllUnder(EP);
                            return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        internal static void TryUnloadedBaseOperations(NP_Presence EP)
        {
            RefreshTeamMainBaseIfAnyPossible(EP);
            if (EP.MainBase != null)
            {
                if (PurgeIfNeeded(EP, EP.MainBase))
                    return;

                bool turboCheat = SpecialAISpawner.CreativeMode && Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.Backspace);

                if (turboCheat)
                {
                    if (EP.MainBase.BuildBucks < 1000000)
                        EP.MainBase.AddBuildBucks(AIGlobals.MinimumBBToTryExpand);
                }
                if (EP.MainBase.BuildBucks < AIGlobals.MinimumBBToTryExpand)
                    return; // Reduce expansion lag
                if (EP.MainBase != null)
                    if (EP.MainBase.Health == EP.MainBase.MaxHealth && UnityEngine.Random.Range(1, 100) <= AIGlobals.BaseExpandChance + (EP.BuildBucks() / 10000))
                        ImTakingThatExpansion(EP, EP.MainBase);
            }
        }

        internal static void ImTakingThatExpansion(NP_Presence EP, NP_BaseUnit EBU)
        {   // Expand the base!
            try
            {
                if (AIGlobals.IsAttract)
                    return; // no branching

                FactionLevel lvl = RawTechLoader.TryGetPlayerLicenceLevel();
                int grade = 99;
                try
                {
                    if (!SpecialAISpawner.CreativeMode)
                        grade = Singleton.Manager<ManLicenses>.inst.GetCurrentLevel(EBU.Faction);
                }
                catch { }

                int Cost = EP.BuildBucks();
                if (EP.GlobalMakerBaseCount() >= KickStart.MaxBasesPerTeam)
                {// Build a mobile Tech 
                    TryFreeUpBaseSlots(EP, lvl);
                    if (EP.GlobalMobileTechCount() > KickStart.EnemyTeamTechLimit)
                        return;
                    if (!IsActivelySieging(EP))
                    {
                        if (RawTechLoader.ShouldUseCustomTechs(out List<int> valid, EBU.Faction, lvl, BasePurpose.NotStationary, BaseTerrain.AnyNonSea, false, grade, maxPrice: Cost))
                        {
                            int spawnIndex = valid.GetRandomEntry();
                            if (spawnIndex == -1)
                            {
                                DebugTAC_AI.Log("TACtical_AI: ShouldUseCustomTechs(ImTakingThatExpansion -UnloadedBases) - Critical error on call - Expected a Custom Local Tech to exist but found none!");
                            }
                            else
                            {
                                RawTechTemplate BTemp = TempManager.ExternalEnemyTechsAll[spawnIndex];
                                ManEnemyWorld.ConstructNewTechExt(EBU, EP, BTemp);
                                //DebugTAC_AI.Log("TACtical_AI: ImTakingThatExpansion(EXT) - Team " + EP.Team + ": Built new mobile tech " + BTemp.techName);
                                return;
                            }
                        }
                        SpawnBaseTypes type = RawTechLoader.GetEnemyBaseType(EBU.Faction, lvl, BasePurpose.NotStationary, BaseTerrain.AnyNonSea, maxGrade: grade, maxPrice: Cost);
                        if (RawTechLoader.IsFallback(type))
                            return;
                        ManEnemyWorld.ConstructNewTech(EBU, EP, type);
                        //DebugTAC_AI.Log("TACtical_AI: ImTakingThatExpansion - Team " + EP.Team + ": Built new mobile tech " + type);
                    }
                    return;
                }

                BasePurpose reason;
                BaseTerrain Terra;
                ManSaveGame.StoredTile ST = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(EBU.tilePos, true);
                if (ST != null && ManEnemyWorld.FindFreeSpaceOnTileCircle(EBU, ST, out Vector2 newPosOff))
                {   // Try spawning defense
                    Vector3 pos = ManWorld.inst.TileManager.CalcTileOriginScene(ST.coord) + newPosOff.ToVector3XZ();
                    reason = PickBuildBasedOnPriorities(EP, lvl);
                    Terra = RawTechLoader.GetTerrain(pos);
                    if (RawTechLoader.ShouldUseCustomTechs(out List<int> valid, EBU.Faction, lvl, reason, Terra, false, grade, maxPrice: Cost))
                    {
                        int spawnIndex = valid.GetRandomEntry();
                        if (spawnIndex == -1)
                        {
                            DebugTAC_AI.Log("TACtical_AI: ShouldUseCustomTechs(ImTakingThatExpansion - UnloadedBases) - Critical error on call - Expected a Custom Local Tech to exist but found none!");
                        }
                        else
                        {
                            RawTechTemplate BTemp = TempManager.ExternalEnemyTechsAll[spawnIndex];
                            ManEnemyWorld.ConstructNewExpansionExt(pos, EBU, EP, BTemp);
                            //DebugTAC_AI.Log("TACtical_AI: ImTakingThatExpansion(EXT) - Team " + EP.Team + ": That expansion is mine!");
                            return;
                        }
                    }
                    SpawnBaseTypes type = RawTechLoader.GetEnemyBaseType(EBU.Faction, lvl, reason, Terra, maxGrade: grade, maxPrice: Cost);
                    if (RawTechLoader.IsFallback(type))
                        return;
                    ManEnemyWorld.ConstructNewExpansion(pos, EBU, EP, type);
                    //DebugTAC_AI.Log("TACtical_AI: ImTakingThatExpansion - Team " + EP.Team + ": That expansion is mine!");
                }
                else
                {   // Find new home base position
                    TryFreeUpBaseSlots(EP, lvl);
                    RefreshTeamMainBaseIfAnyPossible(EP);
                    if (EP.GlobalMobileTechCount() > KickStart.EnemyTeamTechLimit)
                        return;
                    if (!IsActivelySieging(EP))
                    {
                        Terra = RawTechLoader.GetTerrain(EBU.tech.GetBackwardsCompatiblePosition());
                        if (RawTechLoader.ShouldUseCustomTechs(out List<int> valid, EBU.Faction, lvl, BasePurpose.NotStationary, Terra, false, grade, maxPrice: Cost))
                        {
                            int spawnIndex = valid.GetRandomEntry();
                            if (spawnIndex == -1)
                            {
                                DebugTAC_AI.Log("TACtical_AI: ShouldUseCustomTechs(ImTakingThatExpansion -UnloadedBases) - Critical error on call - Expected a Custom Local Tech to exist but found none!");
                            }
                            else
                            {
                                RawTechTemplate BTemp = TempManager.ExternalEnemyTechsAll[spawnIndex];
                                ManEnemyWorld.ConstructNewTechExt(EBU, EP, BTemp);
                                //DebugTAC_AI.Log("TACtical_AI: ImTakingThatExpansion(EXT) - Team " + EP.Team + ": Built new mobile tech " + BTemp.techName);
                                return;
                            }
                        }
                        SpawnBaseTypes type = RawTechLoader.GetEnemyBaseType(EBU.Faction, lvl, BasePurpose.NotStationary, Terra, maxGrade: grade, maxPrice: Cost);
                        if (RawTechLoader.IsFallback(type))
                            return;
                        ManEnemyWorld.ConstructNewTech(EBU, EP, type);
                        //DebugTAC_AI.Log("TACtical_AI: ImTakingThatExpansion - Team " + EP.Team + ": Built new mobile tech " + type);
                    }
                }
            }
            catch (Exception e)
            {
                DebugTAC_AI.Log("TACtical_AI: ImTakingThatExpansion - Error on execution: " + e);
            }
        }
        internal static void TryFreeUpBaseSlots(NP_Presence EP, FactionLevel lvl)
        {   // Remove uneeeded garbage
            try
            {
                NP_BaseUnit Main = RefreshTeamMainBaseIfAnyPossible(EP);
                int TeamBaseCount = EP.GlobalMakerBaseCount();
                //bool RemoveReceivers = FetchNearbyResourceCounts(tech.Team) == 0;
                bool RemoveSpenders = EP.BuildBucks() < CheapestAutominerPrice(Main.Faction, lvl) / 2;
                bool ForceRemove = TeamBaseCount > KickStart.MaxBasesPerTeam;

                int attempts = 1;
                int step = 0;

                if (ForceRemove)
                {
                    attempts = KickStart.MaxBasesPerTeam - TeamBaseCount;
                }

                // Remove the lower-end first
                foreach (NP_BaseUnit fund in EP.EBUs.ToList().OrderBy((F) => F.MaxHealth))
                {
                    if (fund != Main)
                    {
                        if (ForceRemove)
                        {
                            RemoteRecycle(fund);
                            if (step >= attempts)
                                return;
                        }
                        /*
                        if (RemoveReceivers && fund.isHarvestBase && fund.revenue > 1)
                        {
                            RemoteRemove(fund);
                            if (step >= attempts)
                                return;
                        }*/
                        if (RemoveSpenders && fund.Health < fund.MaxHealth
                            && fund.isTechBuilder && !fund.isHarvestBase)
                        {
                            RemoteRecycle(fund);
                            if (step >= attempts)
                                return;
                        }
                        step++;
                    }
                }
            }
            catch
            {
                DebugTAC_AI.Log("TACtical_AI: TryFreeUpBaseSlots - game is being stubborn");
            }
        }
        private static BasePurpose PickBuildBasedOnPriorities(NP_Presence EP, FactionLevel lvl)
        {   // Expand the base!
            if (EP.BuildBucks() <= CheapestAutominerPrice(RefreshTeamMainBaseIfAnyPossible(EP).Faction, lvl) && !HasTooMuchOfType(EP, BasePurpose.Autominer))
            {   // YOU MUST CONSTRUCT ADDITIONAL PYLONS
                return BasePurpose.Autominer;
            }
            else if (EP.WasInCombat())
            {
                switch (UnityEngine.Random.Range(1, 7))
                {
                    case 1:
                        if (HasTooMuchOfType(EP, BasePurpose.Defense))
                            return BasePurpose.TechProduction;
                        return BasePurpose.Defense;
                    case 2:
                        if (HasTooMuchOfType(EP, BasePurpose.Harvesting))
                            return BasePurpose.TechProduction;
                        return BasePurpose.Harvesting;
                    case 3:
                        if (HasTooMuchOfType(EP, BasePurpose.HasReceivers))
                            return BasePurpose.TechProduction;
                        return BasePurpose.HasReceivers;
                    case 4:
                        return BasePurpose.TechProduction;
                    case 5:
                        if (HasTooMuchOfType(EP, BasePurpose.Autominer))
                            return BasePurpose.TechProduction;
                        return BasePurpose.Autominer;
                    default:
                        if (HasTooMuchOfType(EP, BasePurpose.Defense))
                            return BasePurpose.TechProduction;
                        return BasePurpose.Defense;
                }
            }
            else
            {
                switch (UnityEngine.Random.Range(0, 5))
                {
                    case 1:
                        if (HasTooMuchOfType(EP, BasePurpose.Defense))
                            return BasePurpose.TechProduction;
                        return BasePurpose.Defense;
                    case 2:
                        if (HasTooMuchOfType(EP, BasePurpose.Harvesting))
                            return BasePurpose.TechProduction;
                        return BasePurpose.Harvesting;
                    case 3:
                        if (HasTooMuchOfType(EP, BasePurpose.HasReceivers))
                            return BasePurpose.TechProduction;
                        return BasePurpose.HasReceivers;
                    case 4:
                        return BasePurpose.TechProduction;
                    case 5:
                        if (HasTooMuchOfType(EP, BasePurpose.Autominer))
                            return BasePurpose.TechProduction;
                        return BasePurpose.Autominer;
                    default:
                        if (HasTooMuchOfType(EP, BasePurpose.Harvesting))
                            return BasePurpose.TechProduction;
                        return BasePurpose.AnyNonHQ;
                }
            }
        }
        private static BasePurpose PickBuildNonDefense(NP_Presence EP)
        {   // Expand the base!
            switch (UnityEngine.Random.Range(0, 5))
            {
                case 2:
                    if (HasTooMuchOfType(EP, BasePurpose.Harvesting))
                        return BasePurpose.TechProduction;
                    return BasePurpose.Harvesting;
                case 3:
                    if (HasTooMuchOfType(EP, BasePurpose.HasReceivers))
                        return BasePurpose.TechProduction;
                    return BasePurpose.HasReceivers;
                case 4:
                case 5:
                    return BasePurpose.TechProduction;
                default:
                    if (HasTooMuchOfType(EP, BasePurpose.Autominer))
                        return BasePurpose.TechProduction;
                    return BasePurpose.Autominer;
            }
        }


        // Utilities
        /*
        internal static bool IsLocationGridEmpty(Vector3 expansionCenter)
        {
            bool chained = false;
            if (!IsLocationValid(expansionCenter + (Vector3.forward * 64), ref chained))
                return false;
            if (!IsLocationValid(expansionCenter - (Vector3.forward * 64), ref chained))
                return false;
            if (!IsLocationValid(expansionCenter - (Vector3.right * 64), ref chained))
                return false;
            if (!IsLocationValid(expansionCenter + (Vector3.right * 64), ref chained))
                return false;
            if (!IsLocationValid(expansionCenter + ((Vector3.right + Vector3.forward) * 64), ref chained))
                return false;
            if (!IsLocationValid(expansionCenter - ((Vector3.right + Vector3.forward) * 64), ref chained))
                return false;
            if (!IsLocationValid(expansionCenter + ((Vector3.right - Vector3.forward) * 64), ref chained))
                return false;
            if (!IsLocationValid(expansionCenter - ((Vector3.right - Vector3.forward) * 64), ref chained))
                return false;
            return true;
        }
        internal static bool TryFindExpansionLocationGrid(Vector3 expansionCenter, out Vector3 pos)
        {
            bool chained = false;
            int MaxPossibleLocations = 7;
            List<int> location = new List<int>();
            for (int step = 0; step < MaxPossibleLocations; step++)
            {
                location.Add(step);
            }

            int locationsCount = MaxPossibleLocations;
            while (locationsCount > 0)
            {
                int choice = location.GetRandomEntry();
                location.Remove(choice);
                switch (choice)
                {
                    case 0:
                        if (IsLocationValid(expansionCenter + (Vector3.forward * 64), ref chained))
                        {
                            pos = expansionCenter + (Vector3.forward * 64);
                            return true;
                        }
                        break;
                    case 1:
                        if (IsLocationValid(expansionCenter - (Vector3.forward * 64), ref chained))
                        {
                            pos = expansionCenter - (Vector3.forward * 64);
                            return true;
                        }
                        break;
                    case 2:
                        if (IsLocationValid(expansionCenter - (Vector3.right * 64), ref chained))
                        {
                            pos = expansionCenter - (Vector3.right * 64);
                            return true;
                        }
                        break;
                    case 3:
                        if (IsLocationValid(expansionCenter + (Vector3.right * 64), ref chained))
                        {
                            pos = expansionCenter + (Vector3.right * 64);
                            return true;
                        }
                        break;
                    case 4:
                        if (IsLocationValid(expansionCenter + ((Vector3.right + Vector3.forward) * 64), ref chained))
                        {
                            pos = expansionCenter + ((Vector3.right + Vector3.forward) * 64);
                            return true;
                        }
                        break;
                    case 5:
                        if (IsLocationValid(expansionCenter - ((Vector3.right + Vector3.forward) * 64), ref chained))
                        {
                            pos = expansionCenter - ((Vector3.right + Vector3.forward) * 64);
                            return true;
                        }
                        break;
                    case 6:
                        if (IsLocationValid(expansionCenter + ((Vector3.right - Vector3.forward) * 64), ref chained))
                        {
                            pos = expansionCenter + ((Vector3.right - Vector3.forward) * 64);
                            return true;
                        }
                        break;
                    case 7:
                        if (IsLocationValid(expansionCenter - ((Vector3.right - Vector3.forward) * 64), ref chained))
                        {
                            pos = expansionCenter - ((Vector3.right - Vector3.forward) * 64);
                            return true;
                        }
                        break;
                }
                locationsCount--;
            }
            pos = expansionCenter;
            return false;
        }*/

        private static bool TryFindExpansionLocation(NP_TechUnit tank, WorldPosition WP, out Vector3 pos)
        {
            bool chained = false;
            Quaternion quat = tank.tech.m_Rotation;
            if (WP == null)
            {
                WP = WorldPosition.FromScenePosition(tank.tech.GetBackwardsCompatiblePosition());
            }
            if (IsLocationValid(WP.TileCoord, WP.TileRelativePos + ((quat * Vector3.forward) * 64), ref chained))
            {
                pos = WP.ScenePosition + ((quat * Vector3.forward) * 64);
                return true;
            }
            else if (IsLocationValid(WP.TileCoord, WP.TileRelativePos - ((quat * Vector3.forward) * 64), ref chained))
            {
                pos = WP.ScenePosition - ((quat * Vector3.forward) * 64);
                return true;
            }
            else if (IsLocationValid(WP.TileCoord, WP.TileRelativePos - ((quat * Vector3.right) * 64), ref chained))
            {
                pos = WP.ScenePosition - ((quat * Vector3.right) * 64);
                return true;
            }
            else if (IsLocationValid(WP.TileCoord, WP.TileRelativePos + ((quat * Vector3.right) * 64), ref chained))
            {
                pos = WP.ScenePosition + ((quat * Vector3.right) * 64);
                return true;
            }
            else
            {
                pos = WP.ScenePosition;
                return false;
            }
        }
        private static bool TryFindExpansionLocation2(NP_TechUnit tank, WorldPosition WP, out Vector3 pos)
        {
            bool chained = false;
            Quaternion quat = tank.tech.m_Rotation;
            if (WP == null)
            {
                WP = WorldPosition.FromScenePosition(tank.tech.GetBackwardsCompatiblePosition());
            }
            if (IsLocationValid(WP.TileCoord, WP.TileRelativePos + (((quat * Vector3.right) + (quat * Vector3.forward)) * 64), ref chained))
            {
                pos = WP.ScenePosition + (((quat * Vector3.right) + (quat * Vector3.forward)) * 64);
                return true;
            }
            else if (IsLocationValid(WP.TileCoord, WP.TileRelativePos - (((quat * Vector3.right) + (quat * Vector3.forward)) * 64), ref chained))
            {
                pos = WP.ScenePosition - (((quat * Vector3.right) + (quat * Vector3.forward)) * 64);
                return true;
            }
            else if (IsLocationValid(WP.TileCoord, WP.TileRelativePos + (((quat * Vector3.right) - (quat * Vector3.forward)) * 64), ref chained))
            {
                pos = WP.ScenePosition + (((quat * Vector3.right) - (quat * Vector3.forward)) * 64);
                return true;
            }
            else if (IsLocationValid(WP.TileCoord, WP.TileRelativePos - (((quat * Vector3.right) - (quat * Vector3.forward)) * 64), ref chained))
            {
                pos = WP.ScenePosition - (((quat * Vector3.right) - (quat * Vector3.forward)) * 64);
                return true;
            }
            else
            {
                pos = WP.ScenePosition;
                return false;
            }
        }
        private static bool IsLocationValid(IntVector2 TileCoord, Vector3 posInTile, ref bool ChainCancel)
        {
            if (ChainCancel)
                return false;
            bool validLocation = true;
            /*
            if (!Singleton.Manager<ManWorld>.inst.GetTerrainHeight(pos, out _))
            {
                return false;
            }*/
            //WorldPosition WP = WorldPosition.FromScenePosition(posScene);

            foreach (NP_TechUnit ETU in ManEnemyWorld.GetTechsInTile(TileCoord, posInTile, 32))
            {
                if (ETU is NP_BaseUnit EBU)
                {
                    if (EBU.Health < EBU.MaxHealth)
                        ChainCancel = true; // A tech is still being built here - we cannot build more until done!
                }
                validLocation = false;
            }
            return validLocation;
        }
        private static int CheapestAutominerPrice(FactionSubTypes FST, FactionLevel lvl)
        {
            List<SpawnBaseTypes> types = RawTechLoader.GetEnemyBaseTypes(FST, lvl, BasePurpose.Autominer, BaseTerrain.Land);
            int lowest = 150000;
            foreach (SpawnBaseTypes type in types)
            {
                int tryThis = RawTechLoader.GetBaseTemplate(type).baseCost;
                if (tryThis < lowest)
                {
                    lowest = tryThis;
                }
            }
            return lowest;
        }


        private static bool IsActivelySieging(NP_Presence EP)
        {
            if (ManEnemySiege.SiegingEnemyTeam != null)
            {
                if (ManEnemySiege.SiegingEnemyTeam == EP)
                    return true;
            }
            return false;
        }

        private static readonly FieldInfo ProdSys = typeof(ModuleRecipeProvider).GetField("m_RecipeLists", BindingFlags.NonPublic | BindingFlags.Instance);
        private static List<RecipeListWrapper> chunkConverter;
        private static readonly List<RecipeTable.Recipe> chunkConversion = new List<RecipeTable.Recipe>();
        public static ChunkTypes TransChunker(ChunkTypes CT)
        {   // make autominers mine deep based on biome
            if (chunkConverter == null)
            {
                chunkConverter = ((RecipeListWrapper[])ProdSys.GetValue(
                    ManSpawn.inst.GetBlockPrefab(BlockTypes.GSORefinery_222).GetComponent<ModuleRecipeProvider>())).ToList();
                foreach (RecipeListWrapper RLW in chunkConverter)
                {
                    chunkConversion.AddRange(RLW.target.m_Recipes);
                }
            }
            if (CT == ChunkTypes._deprecated_Stone)
                return ChunkTypes._deprecated_Stone;
            try
            {
                return (ChunkTypes)chunkConversion.Find(x => x.InputsContain(new ItemTypeInfo(ObjectTypes.Chunk, (int)CT))).m_OutputItems.FirstOrDefault().m_Item.ItemType;
            }
            catch { }
            return ChunkTypes._deprecated_Stone;
        }
        public static ChunkTypes[] GetBiomeResourcesSurface(Vector3 pos)
        {   // get rough mining yields
            switch (ManWorld.inst.GetBiomeWeightsAtScenePosition(pos).Biome(0).BiomeType)
            {
                case BiomeTypes.Grassland:
                    return new ChunkTypes[12] {
                        ChunkTypes.Wood,
                        ChunkTypes.Wood,
                        ChunkTypes.Wood,
                        ChunkTypes.Wood,
                        ChunkTypes.Wood,
                        ChunkTypes.RubberJelly,
                        ChunkTypes.RubberJelly,
                        ChunkTypes.LuxiteShard,
                        ChunkTypes.LuxiteShard,
                        ChunkTypes.PlumbiteOre, 
                        ChunkTypes.TitaniteOre,
                        ChunkTypes.EruditeShard,
                    };
                case BiomeTypes.Desert:
                    return new ChunkTypes[11] {
                        ChunkTypes.Wood,
                        ChunkTypes.Wood,
                        ChunkTypes.Wood,
                        ChunkTypes.RubberJelly,
                        ChunkTypes.Wood,
                        ChunkTypes.Wood,
                        ChunkTypes.Wood,
                        ChunkTypes.RubberJelly,
                        ChunkTypes.OleiteJelly,
                        ChunkTypes.OleiteJelly,
                        ChunkTypes.IgniteShard 
                    };
                case BiomeTypes.Mountains:
                    return new ChunkTypes[9] {
                        ChunkTypes.Wood,
                        ChunkTypes.Wood,
                        ChunkTypes.Wood,
                        ChunkTypes.RubberJelly,
                        ChunkTypes.PlumbiteOre,
                        ChunkTypes.TitaniteOre,
                        ChunkTypes.PlumbiteOre,
                        ChunkTypes.TitaniteOre,
                        ChunkTypes.RoditeOre,
                    };
                case BiomeTypes.SaltFlats:
                case BiomeTypes.Ice:
                    return new ChunkTypes[9] {
                        ChunkTypes.PlumbiteOre,
                        ChunkTypes.TitaniteOre,
                        ChunkTypes.PlumbiteOre,
                        ChunkTypes.TitaniteOre,
                        ChunkTypes.PlumbiteOre,
                        ChunkTypes.TitaniteOre,
                        ChunkTypes.CarbiteOre, 
                        ChunkTypes.CarbiteOre, 
                        ChunkTypes.CelestiteShard };

                case BiomeTypes.Pillars:
                    return new ChunkTypes[10] {
                        ChunkTypes.Wood,
                        ChunkTypes.Wood,
                        ChunkTypes.Wood,
                        ChunkTypes.RubberJelly,
                        ChunkTypes.CelestiteShard,
                        ChunkTypes.IgniteShard,
                        ChunkTypes.EruditeShard,
                        ChunkTypes.CelestiteShard,
                        ChunkTypes.IgniteShard,
                        ChunkTypes.EruditeShard,
                    };

                default:
                    return new ChunkTypes[2] { ChunkTypes.PlumbiteOre, ChunkTypes.TitaniteOre };
            }
        }
    }
}
