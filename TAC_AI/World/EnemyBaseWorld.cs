using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using TAC_AI.Templates;
using TAC_AI.AI.Enemy;

namespace TAC_AI.World
{
    public class EnemyBaseWorld
    {
        public static EnemyBaseUnloaded GetTeamFunder(EnemyPresence EP)
        {
            if (EP.EBUs.Count == 0)
            {
                //Debug.Log("TACtical_AI: " + Team + " CALLED GetTeamFunds WITH NO BASE!!!");
                return null;
            }
            if (EP.EBUs.Count > 1)
            {
                //Debug.Log("TACtical_AI: " + EP.Team + " has " + EP.EBUs.Count + " bases on scene. The richest will be selected.");
                EnemyBaseUnloaded funder = null;
                int highestFunds = -1;
                foreach (EnemyBaseUnloaded funds in EP.EBUs)
                {
                    if (highestFunds < funds.Funds)
                    {
                        highestFunds = funds.Funds;
                        funder = funds;
                    }
                }
                return funder;
            }
            return EP.EBUs.First();
        }

        public static bool HasTooMuchOfType(EnemyPresence EP, BasePurpose purpose)
        {
            int Count = 0;
            int Team = EP.Team;

            if (purpose == BasePurpose.Defense)
            {
                foreach (EnemyTechUnit ETU in EP.ETUs)
                {
                    if (ETU.MoveSpeed < 10)
                    {
                        Count++;
                    }
                }
            }
            else
            foreach (EnemyBaseUnloaded EBU in EP.EBUs)
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

            bool thisIsTrue = false;
            if (purpose == BasePurpose.Defense)
            {
                thisIsTrue = Count >= RBases.MaxDefenses;
                if (thisIsTrue)
                    Debug.Log("TACtical_AI: HasTooMuchOfType - Team " + Team + " already has too many defenses and cannot make more");
            }
            else if (purpose == BasePurpose.Autominer)
            {
                thisIsTrue = Count >= RBases.MaxAutominers;
                if (thisIsTrue)
                    Debug.Log("TACtical_AI: HasTooMuchOfType - Team " + Team + " already has too many autominers and cannot make more");
            }
            else if (purpose == BasePurpose.HasReceivers && RBases.FetchNearbyResourceCounts(Team) < RBases.MinResourcesReqToCollect)
            {
                thisIsTrue = false;
                Debug.Log("TACtical_AI: HasTooMuchOfType - Team " + Team + " Does not have enough mineables in range to build Reciever bases.");
            }
            else
            {
                thisIsTrue = Count >= RBases.MaxSingleBaseType;
                if (thisIsTrue)
                    Debug.Log("TACtical_AI: HasTooMuchOfType - Team " + Team + " already has too many of type " + purpose.ToString() + " and cannot make more");
            }

            return thisIsTrue;
        }

        public static void PoolTeamMoney(EnemyPresence EP)
        {
            int MoneyPool = 0;
            foreach (EnemyBaseUnloaded EBU in EP.EBUs)
            {
                MoneyPool += EBU.Funds;
                EBU.Funds = 0;
            }
            GetTeamFunder(EP).Funds = MoneyPool;
        }
        public static void EmergencyMoveMoney(EnemyBaseUnloaded aboutToDie)
        {
            EnemyPresence EP = aboutToDie.teamInst;
            int MoneyPool = 0;
            foreach (EnemyBaseUnloaded EBU in EP.EBUs)
            {
                MoneyPool += EBU.Funds;
                EBU.Funds = 0;
            }
            long bestHealth = 0;
            EnemyBaseUnloaded EBUS = null;
            foreach (EnemyBaseUnloaded EBU in EP.EBUs)
            {
                if (bestHealth < EBU.Health && EBU != aboutToDie)
                {
                    bestHealth = EBU.Health;
                    EBUS = EBU;
                }
            }
            if (EBUS == null)
                return; // the money is GONE FOREVER!
            EBUS.Funds = MoneyPool;
        }

        /// <summary>
        /// Tile-based target-finding 
        /// </summary>
        /// <param name="ETU"></param>
        public static void NaviFind(EnemyTechUnit ETU)
        {
            try
            {
                EnemyPresence EP = EnemyWorldManager.GetTeam(ETU.tech.m_TeamID);
                IntVector2 scanPos = ETU.tilePos;
                if (EP.eventStarted || EP.scannedPositions.Contains(scanPos))
                    return;
                EP.scannedPositions.Add(scanPos);
                int dist = EnemyWorldManager.UnitSightRadius;
                if (ETU is EnemyBaseUnloaded)
                    dist = EnemyWorldManager.BaseSightRadius;
                if (SearchPattern(EP, scanPos, dist, out IntVector2 posEnemy))
                {
                    EP.SetEvent(posEnemy);
                }
            }
            catch { }
        }
        public static void NaviFind(EnemyPresence EP, IntVector2 scanPos)
        {
            try
            {
                if (EP.eventStarted || EP.scannedPositions.Contains(scanPos))
                    return;
                EP.scannedPositions.Add(scanPos);
                int dist = EnemyWorldManager.UnitSightRadius;
                if (SearchPattern(EP, scanPos, dist, out IntVector2 posEnemy))
                {
                    EP.SetEvent(posEnemy);
                }
            }
            catch { }
        }
        public static bool SearchPattern(EnemyPresence EP, IntVector2 tilePos, int Dist, out IntVector2 tilePosEnemy)
        {
            tilePosEnemy = tilePos;
            List<Vector2> posToCheck = new List<Vector2>();
            int sightRad = Dist;
            int sightRad2 = sightRad * sightRad;
            for (int stepx = -sightRad; stepx < sightRad; stepx++)
            {
                for (int stepy = -sightRad; stepy < sightRad; stepy++)
                {
                    Vector2 V2 = new Vector2(stepx, stepy);
                    if (V2.sqrMagnitude <= sightRad2)
                    {
                        posToCheck.Add(V2 + (Vector2)tilePos);
                    }
                }
            }
            posToCheck.OrderBy(x => x.sqrMagnitude);

            int numScanned = 0;
            foreach (Vector2 NV2 in posToCheck)
            {
                IntVector2 IV2 = NV2;
                if (TileHasEnemy(EP, IV2))
                {
                    tilePosEnemy = IV2;
                    Debug.Log("TACtical_AI: SearchPattern - Enemy found at " + tilePosEnemy);
                    return true;
                }
                //Debug.Log("TACtical_AI: SearchPattern - Scanned " + IV2);
                numScanned++;
            }
            //Debug.Log("TACtical_AI: SearchPattern - Scanned a total of " + numScanned);
            return false;
        }
        public static bool TileHasEnemy(EnemyPresence EP, IntVector2 tilePos)
        {
            List<EnemyTechUnit> ETUe = EnemyWorldManager.GetTechsInTile(tilePos);
            //Debug.Log("TACtical_AI: TileHasEnemy - Tile count " + ETUe.Count());
            return ETUe.Exists(delegate (EnemyTechUnit cand) { return Tank.IsEnemy(cand.tech.m_TeamID, EP.Team); });
        }
        public static IntVector2 FindTeamBaseTile(EnemyPresence EP)
        {
            try
            {
                return GetTeamFunder(EP).tilePos;
            }
            catch { return IntVector2.zero; }
        }
        public static void RemoteRemove(EnemyTechUnit ETU)
        {
            try
            {
                EnemyWorldManager.RemoveTechFromTeam(ETU);
                EnemyWorldManager.RemoveTechFromTile(ETU);
            }
            catch { }
        }


        // Base Operations
        public static void TryUnloadedBaseOperations(EnemyPresence EP)
        {
            try
            {
                PoolTeamMoney(EP);
                EnemyBaseUnloaded EBU = GetTeamFunder(EP);
                if (EBU != null)
                    if (EBU.Health == EBU.MaxHealth)
                        ImTakingThatExpansion(EP, EBU);
            }
            catch { }
        }
        public static void ImTakingThatExpansion(EnemyPresence EP, EnemyBaseUnloaded EBU)
        {   // Expand the base!
            try
            {
                if (SpecialAISpawner.IsAttract)
                    return; // no branching

                if (KickStart.CullFarEnemyBases)
                {
                    if ((EBU.tech.GetBackwardsCompatiblePosition() - Singleton.playerPos).sqrMagnitude > EnemyWorldManager.EnemyBaseCullingRangeSq)
                    {
                        int count = EP.EBUs.Count;
                        for (int step = 0; step <= count; count--)
                        {
                            EnemyBaseUnloaded EBUcase = EP.EBUs.ElementAt(0);
                            RemoteRemove(EBUcase);
                        }
                        int count2 = EP.ETUs.Count;
                        for (int step = 0; step <= count2; count2--)
                        {
                            EnemyTechUnit ETUcase = EP.ETUs.ElementAt(0);
                            RemoteRemove(ETUcase);
                        }
                        return;
                    }
                }

                bool turboCheat = SpecialAISpawner.CreativeMode && Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.Backspace);

                if (turboCheat)
                {
                    if (EBU.Funds < 1000000)
                    EBU.Funds += RBases.MinimumBBRequired;
                }
                if (EBU.Funds < RBases.MinimumBBRequired)
                    return; // Reduce expansion lag


                int grade = 99;
                try
                {
                    if (!SpecialAISpawner.CreativeMode)
                        grade = Singleton.Manager<ManLicenses>.inst.GetCurrentLevel(KickStart.CorpExtToCorp(EBU.Faction));
                }
                catch { }

                int Cost = EP.BuildBucks();
                if (EP.GetBaseCount() >= KickStart.MaxBasesPerTeam)
                {// Build a mobile Tech 
                    TryFreeUpBaseSlots(EP);
                    if (EP.ETUs.Count * 2 > KickStart.EnemyTeamTechLimit)
                        return;
                    if (RawTechLoader.ShouldUseCustomTechs(out List<int> valid, EBU.Faction, BasePurpose.NotStationary, BaseTerrain.AnyNonSea, false, grade, maxPrice: Cost))
                    {
                        int spawnIndex = valid.GetRandomEntry();
                        if (spawnIndex == -1)
                        {
                            Debug.Log("TACtical_AI: ShouldUseCustomTechs(ImTakingThatExpansion -EnemyBaseWorld) - Critical error on call - Expected a Custom Local Tech to exist but found none!");
                        }
                        else
                        {
                            BaseTemplate BTemp = TempManager.ExternalEnemyTechs[spawnIndex];
                            EnemyWorldManager.ConstructNewTechExt(EBU, EP, BTemp);
                            //Debug.Log("TACtical_AI: ImTakingThatExpansion(EXT) - Team " + EP.Team + ": Built new mobile tech " + BTemp.techName);
                            return;
                        }
                    }
                    SpawnBaseTypes type = RawTechLoader.GetEnemyBaseType(EBU.Faction, BasePurpose.NotStationary, BaseTerrain.AnyNonSea, maxGrade: grade, maxPrice: Cost);
                    if (RawTechLoader.IsFallback(type))
                        return;
                    EnemyWorldManager.ConstructNewTech(EBU, EP, type);
                    //Debug.Log("TACtical_AI: ImTakingThatExpansion - Team " + EP.Team + ": Built new mobile tech " + type);
                    return;
                }

                BasePurpose reason;
                BaseTerrain Terra;
                if (TryFindExpansionLocation(EBU, EBU.tech.GetBackwardsCompatiblePosition(), out Vector3 pos))
                {   // Try spawning defense
                    reason = PickBuildBasedOnPriorities(EP);
                    Terra = RawTechLoader.GetTerrain(pos);
                    if (RawTechLoader.ShouldUseCustomTechs(out List<int> valid, EBU.Faction, reason, Terra, false, grade, maxPrice: Cost))
                    {
                        int spawnIndex = valid.GetRandomEntry();
                        if (spawnIndex == -1)
                        {
                            Debug.Log("TACtical_AI: ShouldUseCustomTechs(ImTakingThatExpansion -EnemyBaseWorld) - Critical error on call - Expected a Custom Local Tech to exist but found none!");
                        }
                        else
                        {
                            BaseTemplate BTemp = TempManager.ExternalEnemyTechs[spawnIndex];
                            EnemyWorldManager.ConstructNewExpansionExt(pos, EBU, EP, BTemp);
                            //Debug.Log("TACtical_AI: ImTakingThatExpansion(EXT) - Team " + EP.Team + ": That expansion is mine!");
                            return;
                        }
                    }
                    SpawnBaseTypes type = RawTechLoader.GetEnemyBaseType(EBU.Faction, reason, Terra, maxGrade: grade, maxPrice: Cost);
                    if (RawTechLoader.IsFallback(type))
                        return;
                    EnemyWorldManager.ConstructNewExpansion(pos, EBU, EP, type);
                    //Debug.Log("TACtical_AI: ImTakingThatExpansion - Team " + EP.Team + ": That expansion is mine!");
                }
                else if (TryFindExpansionLocation2(EBU, EBU.tech.GetBackwardsCompatiblePosition(), out Vector3 pos2))
                {   // Try spawning base extensions
                    reason = PickBuildNonDefense(EP);
                    Terra = RawTechLoader.GetTerrain(pos2);
                    if (RawTechLoader.ShouldUseCustomTechs(out List<int> valid, EBU.Faction, reason, Terra, false, grade, maxPrice: Cost))
                    {
                        int spawnIndex = valid.GetRandomEntry();
                        if (spawnIndex == -1)
                        {
                            Debug.Log("TACtical_AI: ShouldUseCustomTechs(ImTakingThatExpansion -EnemyBaseWorld) - Critical error on call - Expected a Custom Local Tech to exist but found none!");
                        }
                        else
                        {
                            BaseTemplate BTemp = TempManager.ExternalEnemyTechs[spawnIndex];
                            EnemyWorldManager.ConstructNewExpansionExt(pos2, EBU, EP, BTemp);
                            //Debug.Log("TACtical_AI: ImTakingThatExpansion(EXT) - Team " + EP.Team + ": That expansion is mine!");
                            return;
                        }
                    }
                    SpawnBaseTypes type = RawTechLoader.GetEnemyBaseType(EBU.Faction, reason, Terra, maxGrade: grade, maxPrice: Cost);
                    if (RawTechLoader.IsFallback(type))
                        return;
                    EnemyWorldManager.ConstructNewExpansion(pos2, EBU, EP, type);
                    //Debug.Log("TACtical_AI: ImTakingThatExpansion - Team " + EP.Team + ": That expansion is mine!");
                }
                else
                {   // Find new home base position
                    TryFreeUpBaseSlots(EP);
                    EmergencyMoveMoney(GetTeamFunder(EP));
                    if (EP.ETUs.Count * 3 > KickStart.EnemyTeamTechLimit)
                        return;
                    Terra = RawTechLoader.GetTerrain(pos2);
                    if (RawTechLoader.ShouldUseCustomTechs(out List<int> valid, EBU.Faction, BasePurpose.NotStationary, Terra, false, grade, maxPrice: Cost))
                    {
                        int spawnIndex = valid.GetRandomEntry();
                        if (spawnIndex == -1)
                        {
                            Debug.Log("TACtical_AI: ShouldUseCustomTechs(ImTakingThatExpansion -EnemyBaseWorld) - Critical error on call - Expected a Custom Local Tech to exist but found none!");
                        }
                        else
                        {
                            BaseTemplate BTemp = TempManager.ExternalEnemyTechs[spawnIndex];
                            EnemyWorldManager.ConstructNewTechExt(EBU, EP, BTemp);
                            //Debug.Log("TACtical_AI: ImTakingThatExpansion(EXT) - Team " + EP.Team + ": Built new mobile tech " + BTemp.techName);
                            return;
                        }
                    }
                    SpawnBaseTypes type = RawTechLoader.GetEnemyBaseType(EBU.Faction, BasePurpose.NotStationary, Terra, maxGrade: grade, maxPrice: Cost);
                    if (RawTechLoader.IsFallback(type))
                        return;
                    EnemyWorldManager.ConstructNewTech(EBU, EP, type);
                    //Debug.Log("TACtical_AI: ImTakingThatExpansion - Team " + EP.Team + ": Built new mobile tech " + type);
                }
            }
            catch (Exception e)
            {
                Debug.Log("TACtical_AI: ImTakingThatExpansion - game is being stubborn: " + e);
            }
        }
        public static void TryFreeUpBaseSlots(EnemyPresence EP)
        {   // Remove uneeeded garbage
            try
            {
                EnemyBaseUnloaded Main = GetTeamFunder(EP);
                int TeamBaseCount = EP.GetBaseCount();
                //bool RemoveReceivers = FetchNearbyResourceCounts(tech.Team) == 0;
                bool RemoveSpenders = EP.BuildBucks() < CheapestAutominerPrice(Main.Faction) / 2;
                bool ForceRemove = TeamBaseCount > KickStart.MaxBasesPerTeam;

                int attempts = 1;
                int step = 0;

                if (ForceRemove)
                {
                    attempts = KickStart.MaxBasesPerTeam - TeamBaseCount;
                }

                List<EnemyBaseUnloaded> basesSorted = EP.EBUs;
                // Remove the lower-end first
                basesSorted.OrderBy((F) => F.MaxHealth);

                foreach (EnemyBaseUnloaded fund in basesSorted)
                {
                    if (fund != Main)
                    {
                        if (ForceRemove)
                        {
                            RemoteRemove(fund);
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
                            RemoteRemove(fund);
                            if (step >= attempts)
                                return;
                        }
                        step++;
                    }
                }
            }
            catch
            {
                Debug.Log("TACtical_AI: TryFreeUpBaseSlots - game is being stubborn");
            }
        }
        public static BasePurpose PickBuildBasedOnPriorities(EnemyPresence EP)
        {   // Expand the base!
            if (EP.BuildBucks() <= CheapestAutominerPrice(GetTeamFunder(EP).Faction) && !HasTooMuchOfType(EP, BasePurpose.Autominer))
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
        public static BasePurpose PickBuildNonDefense(EnemyPresence EP)
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
        }
        private static bool TryFindExpansionLocation(EnemyTechUnit tank, Vector3 expansionCenter, out Vector3 pos)
        {
            bool chained = false;
            Quaternion quat = tank.tech.m_Rotation;
            if (IsLocationValid(expansionCenter + ((quat * Vector3.forward) * 64), ref chained))
            {
                pos = expansionCenter + ((quat * Vector3.forward) * 64);
                return true;
            }
            else if (IsLocationValid(expansionCenter - ((quat * Vector3.forward) * 64), ref chained))
            {
                pos = expansionCenter - ((quat * Vector3.forward) * 64);
                return true;
            }
            else if (IsLocationValid(expansionCenter - ((quat * Vector3.right) * 64), ref chained))
            {
                pos = expansionCenter - ((quat * Vector3.right) * 64);
                return true;
            }
            else if (IsLocationValid(expansionCenter + ((quat * Vector3.right) * 64), ref chained))
            {
                pos = expansionCenter + ((quat * Vector3.right) * 64);
                return true;
            }
            else
            {
                pos = expansionCenter;
                return false;
            }
        }
        private static bool TryFindExpansionLocation2(EnemyTechUnit tank, Vector3 expansionCenter, out Vector3 pos)
        {
            bool chained = false;
            Quaternion quat = tank.tech.m_Rotation;
            if (IsLocationValid(expansionCenter + (((quat * Vector3.right) + (quat * Vector3.forward)) * 64), ref chained))
            {
                pos = expansionCenter + (((quat * Vector3.right) + (quat * Vector3.forward)) * 64);
                return true;
            }
            else if (IsLocationValid(expansionCenter - (((quat * Vector3.right) + (quat * Vector3.forward)) * 64), ref chained))
            {
                pos = expansionCenter - (((quat * Vector3.right) + (quat * Vector3.forward)) * 64);
                return true;
            }
            else if (IsLocationValid(expansionCenter + (((quat * Vector3.right) - (quat * Vector3.forward)) * 64), ref chained))
            {
                pos = expansionCenter + (((quat * Vector3.right) - (quat * Vector3.forward)) * 64);
                return true;
            }
            else if (IsLocationValid(expansionCenter - (((quat * Vector3.right) - (quat * Vector3.forward)) * 64), ref chained))
            {
                pos = expansionCenter - (((quat * Vector3.right) - (quat * Vector3.forward)) * 64);
                return true;
            }
            else
            {
                pos = expansionCenter;
                return false;
            }
        }
        private static bool IsLocationValid(Vector3 pos, ref bool ChainCancel)
        {
            if (ChainCancel)
                return false;
            bool validLocation = true;
            /*
            if (!Singleton.Manager<ManWorld>.inst.GetTerrainHeight(pos, out _))
            {
                return false;
            }*/
            IntVector2 tileCoord = WorldPosition.FromGameWorldPosition(pos).TileCoord;

            foreach (EnemyTechUnit ETU in EnemyWorldManager.GetTechsInTile(tileCoord, pos - ManWorld.inst.TileManager.CalcTileCentre(tileCoord), 32))
            {
                if (ETU is EnemyBaseUnloaded EBU)
                {
                    if (EBU.Health < EBU.MaxHealth)
                        ChainCancel = true; // A tech is still being built here - we cannot build more until done!
                    validLocation = false;
                }
            }
            return validLocation;
        }
        private static int CheapestAutominerPrice(FactionTypesExt FST)
        {
            List<SpawnBaseTypes> types = RawTechLoader.GetEnemyBaseTypes(FST, BasePurpose.Autominer, BaseTerrain.Land);
            int lowest = 150000;
            foreach (SpawnBaseTypes type in types)
            {
                int tryThis = RawTechLoader.GetBaseStartingFunds(type);
                if (tryThis < lowest)
                {
                    lowest = tryThis;
                }
            }
            return lowest;
        }

        private static FieldInfo ProdSys = typeof(ModuleRecipeProvider).GetField("m_RecipeLists", BindingFlags.NonPublic | BindingFlags.Instance);
        private static List<RecipeListWrapper> chunkConverter;
        private static List<RecipeTable.Recipe> chunkConversion = new List<RecipeTable.Recipe>();
        public static ChunkTypes TransChunker(ChunkTypes CT)
        {   // make autominers mine deep based on biome
            if (chunkConverter == null)
            {
                chunkConverter = ((RecipeListWrapper[])ProdSys.GetValue(ManSpawn.inst.GetBlockPrefab(BlockTypes.GSORefinery_222).GetComponent<ModuleRecipeProvider>())).ToList();
                foreach (RecipeListWrapper RLW in chunkConverter)
                {
                    chunkConversion.AddRange(RLW.target.m_Recipes);
                }
            }
            if (CT == ChunkTypes._deprecated_Stone)
                return ChunkTypes._deprecated_Stone;
            try
            {
                return (ChunkTypes)chunkConversion.Find(delegate (RecipeTable.Recipe cand) { return cand.InputsContain(new ItemTypeInfo(ObjectTypes.Chunk, (int)CT)); }).m_OutputItems.First().m_Item.ItemType;
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
