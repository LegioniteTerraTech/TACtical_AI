using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using System.Reflection;
using TAC_AI.AI.Movement;

namespace TAC_AI.AI.AlliedOperations
{
    internal static class BMultiTech
    {
        // Check MultiTechUtils for the Director
        public static void MTStatic(TankAIHelper helper, Tank tank, ref EControlOperatorSet direct)
        {   // stay still
            helper.lastPlayer = helper.GetPlayerTech();
            helper.IsMultiTech = true;

            BGeneral.ResetValues(helper, ref direct);

            helper.AttackEnemy = false;
            helper.ThrottleState = AIThrottleState.PivotOnly;
        }

        public static void MimicAllClosestAlly(TankAIHelper helper, Tank tank, ref EControlOperatorSet direct)
        {
            helper.lastPlayer = helper.GetPlayerTech();
            helper.IsMultiTech = true;
            helper.Attempt3DNavi = true;

            Tank hostTech;
            float dist;
            Tank copyTargVis;
            BGeneral.ResetValues(helper, ref direct);
            try
            {
                float range = helper.MaxObjectiveRange;
                if (helper.AllMT)
                {
                    if (AIECore.FetchCopyableAlly(tank.boundsCentreWorldNoCheck, helper, out float distSqr, out var vis))
                    {
                        hostTech = vis.tank;
                        var otherHelper = hostTech.GetHelperInsured();
                        if (otherHelper.MultiTechsAffiliated.Add(tank))
                            otherHelper.dirtyExtents = true;
                        //DebugTAC_AI.Log("Found " + hostTech.name);
                    }
                    else
                        hostTech = null;//helper.GetPlayerTech().tank;

                    //hostTech = AIEPathing.ClosestAllyPrecision(AIEPathing.AllyList(tank), tank.boundsCentreWorldNoCheck, out dist, tank);
                    //float distSqr = 0;
                    //if ((bool)helper.theResource?.tank)

                    if ((bool)hostTech)
                    {
                        //hostTech = helper.theResource.tank;
                        distSqr = (hostTech.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).sqrMagnitude;
                    }
                    else
                        hostTech = null;

                    if (hostTech == null || distSqr > range * range)
                    {
                        helper.MTLockedToTechBeam = false;
                        helper.MTMimicHostAvail = false;
                        return;
                    }
                    helper.theResource = hostTech.visible;
                    copyTargVis = hostTech;
                    dist = Mathf.Sqrt(distSqr);
                }
                else
                {
                    hostTech = helper.GetPlayerTech().tank;
                    if (hostTech == null)
                    {
                        helper.MTLockedToTechBeam = false;
                        helper.MTMimicHostAvail = false;
                        return;
                    }
                    if (hostTech == tank)
                    {
                        helper.MTLockedToTechBeam = false;
                        helper.MTMimicHostAvail = false;
                        return;
                    }
                    dist = (tank.boundsCentreWorldNoCheck - hostTech.boundsCentreWorldNoCheck).magnitude;
                    if (dist > range)
                    {
                        helper.MTLockedToTechBeam = false;
                        helper.MTMimicHostAvail = false;
                        return;
                    }
                    helper.theResource = hostTech.visible;
                    copyTargVis = hostTech;
                }

                //float range = helper.lastTechExtents + vis.GetCheapBounds();
                if (!helper.MTMimicHostAvail)
                {
                    helper.MTMimicHostAvail = true;
                }
                else if (helper.MTMimicHostAvail && dist > range)
                {
                    helper.MTMimicHostAvail = false;
                    // Make sure the player did not force the tech under the ground on release
                    if (AIEPathMapper.GetAltitudeLoadedOnly(tank.boundsCentreWorldNoCheck, out float height))
                        if (tank.boundsCentreWorldNoCheck.y < height)
                            tank.visible.MoveAboveGround();
                }
                if (!helper.MTLockedToTechBeam && copyTargVis.beam.IsActive && dist < range)
                {
                    helper.MTOffsetPos = copyTargVis.trans.InverseTransformPoint(tank.trans.position);
                    helper.MTOffsetRot = copyTargVis.trans.InverseTransformDirection(tank.trans.forward);
                    helper.MTOffsetRotUp = copyTargVis.trans.InverseTransformDirection(tank.trans.up);
                    //DebugTAC_AI.Log(KickStart.ModID + ":AI " + tank.name + ": Synced position to " + helper.MTOffsetPos + " and rot to " + helper.MTOffsetRot);
                    helper.MTLockedToTechBeam = true;
                }
                else if (helper.MTLockedToTechBeam && !copyTargVis.beam.IsActive)
                {
                    helper.MTLockedToTechBeam = false;
                }
                if (!helper.MTLockedToTechBeam && helper.MTMimicHostAvail)
                {
                    TankControl.State controlCopyTarget = helper.theResource.tank.control.CurState;
                    helper.FullBoost = controlCopyTarget.m_BoostJets;
                    helper.FirePROPS = controlCopyTarget.m_BoostProps;
                }
            }
            catch
            {
                helper.MTLockedToTechBeam = false;
                helper.MTMimicHostAvail = false;
            }
        }
        public static void BeamLockWithinBounds(TankAIHelper helper, Tank tank)
        {
            Tank hostTech;
            float dist;
            Tank vis;
            try
            {
                if (helper.AllMT)
                {
                    if (AIECore.FetchCopyableAlly(tank.boundsCentreWorldNoCheck, helper, out float distSqr, out var vis2))
                        hostTech = vis2.tank;
                    else
                        hostTech = null;
                    //hostTech = AIEPathing.ClosestAllyPrecision(AIEPathing.AllyList(tank), tank.boundsCentreWorldNoCheck, out dist, tank);
                    if (hostTech == null)
                    {
                        helper.MTLockedToTechBeam = false;
                        return;
                    }
                    helper.theResource = hostTech.visible;
                    vis = hostTech;
                    dist = Mathf.Sqrt(distSqr);
                }
                else
                {
                    hostTech = helper.GetPlayerTech().tank;
                    if (hostTech == null)
                    {
                        helper.MTLockedToTechBeam = false;
                        return;
                    }
                    if (hostTech == tank)
                    {
                        helper.MTLockedToTechBeam = false;
                        return;
                    }
                    helper.theResource = hostTech.visible;
                    vis = hostTech;
                    dist = (tank.boundsCentreWorldNoCheck - hostTech.boundsCentreWorldNoCheck).magnitude;
                }

                float range = helper.lastTechExtents + vis.GetCheapBounds();
                if (!helper.MTLockedToTechBeam && vis.beam.IsActive && dist < range)
                {
                    helper.MTOffsetPos = vis.trans.InverseTransformPoint(tank.trans.position);
                    helper.MTOffsetRot = vis.trans.InverseTransformDirection(tank.trans.forward);
                    helper.MTOffsetRotUp = vis.trans.InverseTransformDirection(tank.trans.up);
                    //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ": Synced position to " + helper.MTOffsetPos + " and rot to " + helper.MTOffsetRot);
                    helper.MTLockedToTechBeam = true;
                }
                else if (helper.MTLockedToTechBeam && !vis.beam.IsActive)
                {
                    helper.MTLockedToTechBeam = false;
                }
            }
            catch
            {
                helper.MTLockedToTechBeam = false;
            }
        }

        public static void MimicDefend(TankAIHelper helper, Tank tank)
        {
            // Determines the weapons actions and aiming of the AI, this one is for MTs that have a host
            helper.AttackEnemy = false;
            if (helper.theResource?.tank)
            {   //Get the tech the player is aiming at
                Visible playerTarget = helper.theResource.tank.Weapons.GetManualTarget();
                if (playerTarget != null)
                    helper.lastEnemy = playerTarget;
                else
                    helper.TryRefreshEnemyAllied();
            }
            else
                helper.TryRefreshEnemyAllied();
            if (helper.lastEnemyGet != null)
            {
                Vector3 aimTo = (helper.lastEnemyGet.transform.position - tank.transform.position).normalized;
                helper.Urgency++;
                if (Mathf.Abs((tank.rootBlockTrans.forward - aimTo).magnitude) < 0.15f || helper.Urgency >= 30)
                {
                    helper.AttackEnemy = true;
                    helper.Urgency = 30;
                }
            }
            else
            {
                helper.Urgency = 0;
                helper.AttackEnemy = false;
            }
        }
    }
}
