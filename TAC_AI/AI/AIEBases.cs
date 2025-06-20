using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using UnityEngine;
using TerraTechETCUtil;
using TAC_AI.Templates;
using TAC_AI.World;
using TAC_AI.AI.Movement;

namespace TAC_AI.AI
{
    internal class AIEBases
    {
        internal static bool BaseConstructTech(Tank tech, Snapshot techToMake)
        {   // Expand the base!
            InvokeHelper.Invoke(DoBaseConstructTech, 0.1f, tech, techToMake);
            return true;
        }
        private static void DoBaseConstructTech(Tank tech, Snapshot techToMake)
        {   // Expand the base!
            try
            {
                if (!Singleton.Manager<ManWorld>.inst.CheckIsTileAtPositionLoaded(tech.boundsCentreWorld))
                    DebugTAC_AI.Assert(KickStart.ModID + ": BaseConstructTech - Spawning Tech is out of world playable bounds at " +
                        tech.boundsCentreWorld.ToString() + " this should not be possible");
                if (FindNewExpansionBase(tech, tech.boundsCentreWorld + (tech.rootBlockTrans.forward * 
                    (techToMake.techData.Radius + 8 + tech.GetCheapBounds())),
                    techToMake.techData.Radius + 0.5f, AIGlobals.defaultExpandRadRange, 5, out Vector3 pos, true))
                {
                    //var clone = techToMake.techData.GetShallowClonedCopy();
                    RawTechLoader.SpawnTechFragment(pos, tech.Team, new RawTech(techToMake.techData, true));
                }
            }
            catch (Exception e)
            {
                DebugTAC_AI.Log(KickStart.ModID + ": BaseConstructTech - game is being stubborn " + e);
            }
        }
        internal static void SetupBookmarkBuilder(TankAIHelper helper)
        {
            if (BookmarkBuilder.TryGet(helper.tank, out BookmarkBuilder builder))
            {
                helper.AILimitSettings.OverrideForBuilder(true);

                helper.InsureTechMemor("SetupBookmarkBuilder", false);

                DebugTAC_AI.Log(KickStart.ModID + ": Tech " + helper.tank.name + " Setup for SetupTechAutoConstruction");
                helper.TechMemor.SetupForNewTechConstruction(helper, builder.blueprint.savedTech);
                if (builder.instant)
                {
                    AIERepair.Turboconstruct(helper.tank, helper.TechMemor, true);
                }
                helper.FinishedRepairEvent.Subscribe(OnFinishTechAutoConstruction);
            }
        }
        internal static bool CheckIfTechNeedsToBeBuilt(TankAIHelper helper)
        {
            if (BookmarkBuilder.Exists(helper.tank))
            {
                helper.AILimitSettings.OverrideForBuilder();
                helper.AISetSettings.OverrideForBuilder();
                helper.RequestBuildBeam = true;
                return true;
            }
            return false;
        }
        internal static void SetupTechAutoConstruction(TankAIHelper helper)
        {
            if (BookmarkBuilder.TryGet(helper.tank, out BookmarkBuilder builder))
            {
                DebugTAC_AI.Log(KickStart.ModID + ": Tech " + helper.tank.name + 
                    " Setup for SetupTechAutoConstruction with block count " + builder.blueprint.savedTech.Count);
                helper.TechMemor.SetupForNewTechConstruction(helper, builder.blueprint.savedTech);
                if (builder.instant)
                {
                    AIERepair.Turboconstruct(helper.tank, helper.TechMemor, true);
                }
                helper.FinishedRepairEvent.Subscribe(OnFinishTechAutoConstruction);
            }
        }
        internal static void OnFinishTechAutoConstruction(TankAIHelper helper)
        {
            if (BookmarkBuilder.Remove(helper.tank))
            {
                helper.ForceRebuildAlignment();
            }
            else
                DebugTAC_AI.LogError("OnFinishTechAutoConstruction expected the blueprint to be registered in BookmarkedPlans" + 
                    ", but it was not present in there?  Did we remove it earlier???");
        }
        private static IEnumerable<Vector3> IterateManhattanDiamondCreep(Vector3 target, float placementTargetSize, 
            float SearchRadius, int radiusDivisions)
        {
            float subdivisionUnit = SearchRadius / radiusDivisions;
            for (int radius = 0; radius < radiusDivisions; radius++)
            {
                if (radius == 0)
                {
                    yield return target;
                }
                else
                {
                    for (int diagStep = 0; diagStep <= radius; diagStep++)
                    {
                        yield return target + new Vector3(-diagStep * subdivisionUnit, 0, diagStep * subdivisionUnit);
                        yield return -target + new Vector3(diagStep * subdivisionUnit, 0, -diagStep * subdivisionUnit);
                    }
                    for (int diagStep = 1; diagStep < radius; diagStep++)
                    {
                        yield return target + new Vector3(-diagStep * subdivisionUnit, 0, -diagStep * subdivisionUnit);
                        yield return -target + new Vector3(diagStep * subdivisionUnit, 0, diagStep * subdivisionUnit);
                    }
                }
                target.x = target.x + subdivisionUnit;
            }
        }


        private static Vector3[] location = new Vector3[9];
        internal static bool FindNewExpansionBase(Tank tank, Vector3 targetWorld, float placeSize, float searchRadius, int radiusDivisions, out Vector3 pos, bool IgnoreCurrentlyBuilding = false)
        {
            float offsetRadApprox = 80;
            bool buildingCancel = false;
            Vector3 coreOffset = tank.boundsCentreWorld - tank.transform.position;
            foreach (Vector3 IterateVec in IterateManhattanDiamondCreep(Vector3.zero, placeSize, searchRadius, radiusDivisions))
            {
                Vector3 checkVec = tank.transform.TransformPoint(IterateVec) + coreOffset;
                if (IgnoreCurrentlyBuilding)
                    buildingCancel = false;
                if (IsLocationValid(checkVec, placeSize, ref buildingCancel))
                {
                    pos = checkVec;
                    return true;
                }
            }
            /*
            location[0] = tank.transform.TransformPoint(new Vector3(offsetRadApprox, 0, 0)) + coreOffset;
            location[1] = tank.transform.TransformPoint(new Vector3(-offsetRadApprox, 0, 0)) + coreOffset;
            location[2] = tank.transform.TransformPoint(new Vector3(0, 0, offsetRadApprox)) + coreOffset;
            location[3] = tank.transform.TransformPoint(new Vector3(0, 0, -offsetRadApprox)) + coreOffset;
            location[4] = tank.transform.TransformPoint(new Vector3(offsetRadApprox, 0, offsetRadApprox)) + coreOffset;
            location[5] = tank.transform.TransformPoint(new Vector3(-offsetRadApprox, 0, offsetRadApprox)) + coreOffset;
            location[6] = tank.transform.TransformPoint(new Vector3(offsetRadApprox, 0, -offsetRadApprox)) + coreOffset;
            location[7] = tank.transform.TransformPoint(new Vector3(-offsetRadApprox, 0, -offsetRadApprox)) + coreOffset;
            location[8] = tank.transform.TransformPoint(new Vector3(0, 0, 0)) + coreOffset;

            foreach (var item in location.OrderBy(x => (x - targetWorld).sqrMagnitude))
            {
                Vector3 posCase = item;
                if (IgnoreCurrentlyBuilding)
                    buildingCancel = false;
                if (IsLocationValid(posCase, ref buildingCancel))
                {
                    pos = posCase;
                    return true;
                }
            }
            */
            pos = tank.boundsCentreWorldNoCheck;
            return false;
        }
        private static bool IsLocationValid(Vector3 pos, float placeRadius, ref bool ChainCancel, bool resourcesToo = true, bool IgnoreNeutral = false)
        {
            if (ChainCancel)
                return false;
            bool validLocation = true;
            if (!Singleton.Manager<ManWorld>.inst.GetTerrainHeight(pos, out _))
            {
                return false;
            }

            foreach (Visible vis in Singleton.Manager<ManVisible>.inst.VisiblesTouchingRadius(pos, placeRadius, AIGlobals.emptyBitMask))
            {
                if (resourcesToo && vis.resdisp.IsNotNull())
                {
                    if (vis.isActive)
                        validLocation = false;
                }
                if (vis.tank.IsNotNull())
                {
                    if (IgnoreNeutral && vis.tank.Team == -2)
                        continue;
                    var helper = vis.tank.GetHelperInsured();
                    if (helper && helper.PendingDamageCheck)
                    {
                        ChainCancel = true;
                        return false;
                    }
                    validLocation = false;
                }
            }
            return validLocation;
        }

        internal static bool IsLocationGridEmpty(Vector3 expansionCenter, float placeRadius, bool ignoreNeutrals = true)
        {
            bool chained = false;
            if (!IsLocationValid(expansionCenter + (Vector3.forward * 64), placeRadius, ref chained, false, ignoreNeutrals))
                return false;
            if (!IsLocationValid(expansionCenter - (Vector3.forward * 64), placeRadius, ref chained, false, ignoreNeutrals))
                return false;
            if (!IsLocationValid(expansionCenter - (Vector3.right * 64), placeRadius, ref chained, false, ignoreNeutrals))
                return false;
            if (!IsLocationValid(expansionCenter + (Vector3.right * 64), placeRadius, ref chained, false, ignoreNeutrals))
                return false;
            if (!IsLocationValid(expansionCenter + ((Vector3.right + Vector3.forward) * 64), placeRadius, ref chained, false, ignoreNeutrals))
                return false;
            if (!IsLocationValid(expansionCenter - ((Vector3.right + Vector3.forward) * 64), placeRadius, ref chained, false, ignoreNeutrals))
                return false;
            if (!IsLocationValid(expansionCenter + ((Vector3.right - Vector3.forward) * 64), placeRadius, ref chained, false, ignoreNeutrals))
                return false;
            if (!IsLocationValid(expansionCenter - ((Vector3.right - Vector3.forward) * 64), placeRadius, ref chained, false, ignoreNeutrals))
                return false;
            return true;
        }

        internal static bool TryFindExpansionLocationGrid(Vector3 expansionCenter, Vector3 targetWorld, out Vector3 pos)
        {
            location[0] = expansionCenter + new Vector3(64, 0, 0);
            location[1] = expansionCenter + new Vector3(-64, 0, 0);
            location[2] = expansionCenter + new Vector3(0, 0, 64);
            location[3] = expansionCenter + new Vector3(0, 0, -64);
            location[4] = expansionCenter + new Vector3(64, 0, 64);
            location[5] = expansionCenter + new Vector3(-64, 0, 64);
            location[6] = expansionCenter + new Vector3(64, 0, -64);
            location[7] = expansionCenter + new Vector3(-64, 0, -64);
            location[8] = expansionCenter + new Vector3(0, 0, 0);

            bool constant = false;
            foreach (var item in location.OrderBy(x => (x - targetWorld).sqrMagnitude))
            {
                if (IsLocationValid(item, AIGlobals.defaultExpandRad, ref constant, true, false))
                {
                    pos = item;
                    return true;
                }
            }
            pos = expansionCenter;
            return false;
        }


        internal static bool TryFindExpansionLocationDirect(Tank tank, Vector3 expansionCenter, float placeRadius, out Vector3 pos)
        {
            bool chained = false;
            if (IsLocationValid(expansionCenter - (tank.rootBlockTrans.forward * 64), placeRadius, ref chained))
            {
                pos = expansionCenter - (tank.rootBlockTrans.forward * 64);
                return true;
            }
            else if (IsLocationValid(expansionCenter + (tank.rootBlockTrans.forward * 64), placeRadius, ref chained))
            {
                pos = expansionCenter + (tank.rootBlockTrans.forward * 64);
                return true;
            }
            else if (IsLocationValid(expansionCenter - (tank.rootBlockTrans.right * 64), placeRadius, ref chained))
            {
                pos = expansionCenter - (tank.rootBlockTrans.right * 64);
                return true;
            }
            else if (IsLocationValid(expansionCenter + (tank.rootBlockTrans.right * 64), placeRadius, ref chained))
            {
                pos = expansionCenter + (tank.rootBlockTrans.right * 64);
                return true;
            }
            else
            {
                pos = expansionCenter;
                return false;
            }
        }
        internal static bool TryFindExpansionLocationCorner(Tank tank, Vector3 expansionCenter, float placeRadius, out Vector3 pos)
        {
            bool chained = false;
            if (IsLocationValid(expansionCenter + ((tank.rootBlockTrans.right + tank.rootBlockTrans.forward) * 64), placeRadius, ref chained))
            {
                pos = expansionCenter + ((tank.rootBlockTrans.right + tank.rootBlockTrans.forward) * 64);
                return true;
            }
            else if (IsLocationValid(expansionCenter - ((tank.rootBlockTrans.right + tank.rootBlockTrans.forward) * 64), placeRadius, ref chained))
            {
                pos = expansionCenter - ((tank.rootBlockTrans.right + tank.rootBlockTrans.forward) * 64);
                return true;
            }
            else if (IsLocationValid(expansionCenter + ((tank.rootBlockTrans.right - tank.rootBlockTrans.forward) * 64), placeRadius, ref chained))
            {
                pos = expansionCenter + ((tank.rootBlockTrans.right - tank.rootBlockTrans.forward) * 64);
                return true;
            }
            else if (IsLocationValid(expansionCenter - ((tank.rootBlockTrans.right - tank.rootBlockTrans.forward) * 64), placeRadius, ref chained))
            {
                pos = expansionCenter - ((tank.rootBlockTrans.right - tank.rootBlockTrans.forward) * 64);
                return true;
            }
            else
            {
                pos = expansionCenter;
                return false;
            }
        }
    }
}
