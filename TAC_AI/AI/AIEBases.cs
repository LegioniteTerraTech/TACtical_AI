using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TerraTechETCUtil;
using TAC_AI.Templates;
using TAC_AI.World;
using TAC_AI.AI.Movement;

namespace TAC_AI.AI
{
    public class AIEBases
    {
        internal static void BaseConstructTech(Tank tech, Vector3 aimedPos, Snapshot techToMake)
        {   // Expand the base!
            try
            {
                if (FindNewExpansionBase(tech, aimedPos, out Vector3 pos))
                {
                    var clone = techToMake.techData.GetShallowClonedCopy();
                    RawTechLoader.SpawnTechFragment(pos, tech.Team, new RawTechTemplate(clone));
                }
            }
            catch (Exception e)
            {
                DebugTAC_AI.Log("TACtical_AI: BaseConstructTech - game is being stubborn " + e);
            }
        }
        internal static void SetupBookmarkBuilder(AIECore.TankAIHelper helper)
        {
            BookmarkBuilder builder = helper.GetComponent<BookmarkBuilder>();
            if (builder)
            {
                helper.AutoRepair = true;
                helper.AILimitSettings.AdvancedAI = true;
                helper.UseInventory = true;

                helper.InsureTechMemor("SetupBookmarkBuilder", false);

                DebugTAC_AI.Log("TACtical_AI: Tech " + helper.tank.name + " Setup for SetupTechAutoConstruction");
                helper.TechMemor.SetupForNewTechConstruction(helper, builder.blueprint);
                if (builder.instant)
                {
                    AIERepair.Turboconstruct(helper.tank, helper.TechMemor, true);
                }
                helper.FinishedRepairEvent.Subscribe(OnFinishTechAutoConstruction);
            }
        }
        internal static bool CheckIfTechNeedsToBeBuilt(AIECore.TankAIHelper helper)
        {
            BookmarkBuilder builder = helper.GetComponent<BookmarkBuilder>();
            if (builder)
            {
                helper.AILimitSettings.AutoRepair = true;
                helper.AISetSettings.AutoRepair = true;
                helper.AILimitSettings.AdvancedAI = true;
                helper.AISetSettings.AdvancedAI = true;
                helper.AILimitSettings.UseInventory = true;
                helper.AISetSettings.UseInventory = true;
                helper.RequestBuildBeam = true;
                return true;
            }
            return false;
        }
        internal static void SetupTechAutoConstruction(AIECore.TankAIHelper helper)
        {
            BookmarkBuilder builder = helper.GetComponent<BookmarkBuilder>();
            if (builder)
            {
                DebugTAC_AI.Log("TACtical_AI: Tech " + helper.tank.name + 
                    " Setup for SetupTechAutoConstruction with string length " + builder.blueprint.Count());
                helper.TechMemor.SetupForNewTechConstruction(helper, builder.blueprint);
                if (builder.instant)
                {
                    AIERepair.Turboconstruct(helper.tank, helper.TechMemor, true);
                }
                helper.FinishedRepairEvent.Subscribe(OnFinishTechAutoConstruction);
            }
        }
        internal static void OnFinishTechAutoConstruction(AIECore.TankAIHelper helper)
        {
            BookmarkBuilder builder = helper.GetComponent<BookmarkBuilder>();
            if (builder)
            {
                UnityEngine.Object.DestroyImmediate(builder);
                helper.ForceRebuildAlignment();
            }
        }

        internal static bool FindNewExpansionBase(Tank tank, Vector3 targetWorld, out Vector3 pos)
        {
            List<Vector3> location = new List<Vector3>
            {
                tank.transform.TransformPoint(new Vector3(64, 0, 0)),
                tank.transform.TransformPoint(new Vector3(-64, 0, 0)),
                tank.transform.TransformPoint(new Vector3(0, 0, 64)),
                tank.transform.TransformPoint(new Vector3(0, 0, -64)),
                tank.transform.TransformPoint(new Vector3(64, 0, 64)),
                tank.transform.TransformPoint(new Vector3(-64, 0, 64)),
                tank.transform.TransformPoint(new Vector3(64, 0, -64)),
                tank.transform.TransformPoint(new Vector3(-64, 0, -64)),
                tank.transform.TransformPoint(new Vector3(0, 0, 0))
            };

            location = location.OrderBy(x => (x - targetWorld).sqrMagnitude).ToList();

            int locationsCount = location.Count;
            bool constant = false;
            while (locationsCount > 0)
            {
                Vector3 posCase = location[0] + tank.blockBounds.center;
                if (IsLocationValid(posCase, ref constant, true, false))
                {
                    pos = posCase;
                    return true;
                }
                location.RemoveAt(0);
                locationsCount--;
            }
            pos = tank.boundsCentreWorldNoCheck;
            return false;
        }
        private static bool IsLocationValid(Vector3 pos, ref bool ChainCancel, bool resourcesToo = true, bool IgnoreNeutral = false)
        {
            if (ChainCancel)
                return false;
            bool validLocation = true;
            if (!Singleton.Manager<ManWorld>.inst.GetTerrainHeight(pos, out _))
            {
                return false;
            }

            foreach (Visible vis in Singleton.Manager<ManVisible>.inst.VisiblesTouchingRadius(pos, 24, new Bitfield<ObjectTypes>()))
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
                    if (helper.TechMemor)
                    {
                        if (helper.PendingDamageCheck)
                            ChainCancel = true; // A tech is still being built here - we cannot build more until done!
                    }
                    validLocation = false;
                }
            }
            return validLocation;
        }

        internal static bool IsLocationGridEmpty(Vector3 expansionCenter, bool ignoreNeutrals = true)
        {
            bool chained = false;
            if (!IsLocationValid(expansionCenter + (Vector3.forward * 64), ref chained, false, ignoreNeutrals))
                return false;
            if (!IsLocationValid(expansionCenter - (Vector3.forward * 64), ref chained, false, ignoreNeutrals))
                return false;
            if (!IsLocationValid(expansionCenter - (Vector3.right * 64), ref chained, false, ignoreNeutrals))
                return false;
            if (!IsLocationValid(expansionCenter + (Vector3.right * 64), ref chained, false, ignoreNeutrals))
                return false;
            if (!IsLocationValid(expansionCenter + ((Vector3.right + Vector3.forward) * 64), ref chained, false, ignoreNeutrals))
                return false;
            if (!IsLocationValid(expansionCenter - ((Vector3.right + Vector3.forward) * 64), ref chained, false, ignoreNeutrals))
                return false;
            if (!IsLocationValid(expansionCenter + ((Vector3.right - Vector3.forward) * 64), ref chained, false, ignoreNeutrals))
                return false;
            if (!IsLocationValid(expansionCenter - ((Vector3.right - Vector3.forward) * 64), ref chained, false, ignoreNeutrals))
                return false;
            return true;
        }

        internal static bool TryFindExpansionLocationGrid(Vector3 expansionCenter, Vector3 targetWorld, out Vector3 pos)
        {
            List<Vector3> location = new List<Vector3>
            {
                expansionCenter + new Vector3(64, 0, 0),
                expansionCenter + new Vector3(-64, 0, 0),
                expansionCenter + new Vector3(0, 0, 64),
                expansionCenter + new Vector3(0, 0, -64),
                expansionCenter + new Vector3(64, 0, 64),
                expansionCenter + new Vector3(-64, 0, 64),
                expansionCenter + new Vector3(64, 0, -64),
                expansionCenter + new Vector3(-64, 0, -64),
                expansionCenter + new Vector3(0, 0, 0)
            };

            location = location.OrderBy(x => (x - targetWorld).sqrMagnitude).ToList();

            int locationsCount = location.Count;
            bool constant = false;
            while (locationsCount > 0)
            {
                Vector3 posCase = location[0];
                if (IsLocationValid(posCase, ref constant, true, false))
                {
                    pos = posCase;
                    return true;
                }
                location.RemoveAt(0);
                locationsCount--;
            }
            pos = expansionCenter;
            return false;
        }


        internal static bool TryFindExpansionLocationDirect(Tank tank, Vector3 expansionCenter, out Vector3 pos)
        {
            bool chained = false;
            if (IsLocationValid(expansionCenter - (tank.rootBlockTrans.forward * 64), ref chained))
            {
                pos = expansionCenter - (tank.rootBlockTrans.forward * 64);
                return true;
            }
            else if (IsLocationValid(expansionCenter + (tank.rootBlockTrans.forward * 64), ref chained))
            {
                pos = expansionCenter + (tank.rootBlockTrans.forward * 64);
                return true;
            }
            else if (IsLocationValid(expansionCenter - (tank.rootBlockTrans.right * 64), ref chained))
            {
                pos = expansionCenter - (tank.rootBlockTrans.right * 64);
                return true;
            }
            else if (IsLocationValid(expansionCenter + (tank.rootBlockTrans.right * 64), ref chained))
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
        internal static bool TryFindExpansionLocationCorner(Tank tank, Vector3 expansionCenter, out Vector3 pos)
        {
            bool chained = false;
            if (IsLocationValid(expansionCenter + ((tank.rootBlockTrans.right + tank.rootBlockTrans.forward) * 64), ref chained))
            {
                pos = expansionCenter + ((tank.rootBlockTrans.right + tank.rootBlockTrans.forward) * 64);
                return true;
            }
            else if (IsLocationValid(expansionCenter - ((tank.rootBlockTrans.right + tank.rootBlockTrans.forward) * 64), ref chained))
            {
                pos = expansionCenter - ((tank.rootBlockTrans.right + tank.rootBlockTrans.forward) * 64);
                return true;
            }
            else if (IsLocationValid(expansionCenter + ((tank.rootBlockTrans.right - tank.rootBlockTrans.forward) * 64), ref chained))
            {
                pos = expansionCenter + ((tank.rootBlockTrans.right - tank.rootBlockTrans.forward) * 64);
                return true;
            }
            else if (IsLocationValid(expansionCenter - ((tank.rootBlockTrans.right - tank.rootBlockTrans.forward) * 64), ref chained))
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
