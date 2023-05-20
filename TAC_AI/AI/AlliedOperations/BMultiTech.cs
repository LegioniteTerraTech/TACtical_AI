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
    public static class BMultiTech
    {
        public static void MTStatic(AIECore.TankAIHelper thisInst, Tank tank, ref EControlOperatorSet direct)
        {   // stay still
            thisInst.lastPlayer = thisInst.GetPlayerTech();
            thisInst.IsMultiTech = true;

            BGeneral.ResetValues(thisInst, ref direct);

            thisInst.AttackEnemy = false;
            thisInst.PivotOnly = true;
        }

        public static void MimicAllClosestAlly(AIECore.TankAIHelper thisInst, Tank tank, ref EControlOperatorSet direct)
        {
            thisInst.lastPlayer = thisInst.GetPlayerTech();
            thisInst.IsMultiTech = true;
            thisInst.Attempt3DNavi = true;

            Tank hostTech;
            float dist;
            Tank copyTargVis;
            BGeneral.ResetValues(thisInst, ref direct);
            try
            {
                float range = thisInst.MinCombatRange;
                if (!thisInst.AllMT)
                {
                    hostTech = thisInst.GetPlayerTech().tank;
                    if (hostTech == null)
                    {
                        thisInst.MTLockedToTechBeam = false;
                        thisInst.MTMimicHostAvail = false;
                        return;
                    }
                    if (hostTech == tank)
                    {
                        thisInst.MTLockedToTechBeam = false;
                        thisInst.MTMimicHostAvail = false;
                        return;
                    }
                    dist = (tank.boundsCentreWorldNoCheck - hostTech.boundsCentreWorldNoCheck).magnitude;
                    if (dist > range)
                    {
                        thisInst.MTLockedToTechBeam = false;
                        thisInst.MTMimicHostAvail = false;
                        return;
                    }
                    thisInst.theResource = hostTech.visible;
                    copyTargVis = hostTech;
                }
                else
                {
                    if (AIECore.FetchCopyableAlly(tank.boundsCentreWorldNoCheck, thisInst, out float distSqr, out var vis))
                        hostTech = vis.tank;
                    else
                        hostTech = null;
                    //hostTech = AIEPathing.ClosestAllyPrecision(AIEPathing.AllyList(tank), tank.boundsCentreWorldNoCheck, out dist, tank);
                    if (hostTech == null)
                        hostTech = thisInst.GetPlayerTech().tank;
                    if (hostTech == null || distSqr > range * range)
                    {
                        thisInst.MTLockedToTechBeam = false;
                        thisInst.MTMimicHostAvail = false;
                        return;
                    }
                    thisInst.theResource = hostTech.visible;
                    copyTargVis = hostTech;
                    dist = Mathf.Sqrt(distSqr);
                }

                //float range = thisInst.lastTechExtents + vis.GetCheapBounds();
                if (!thisInst.MTMimicHostAvail)
                {
                    thisInst.MTMimicHostAvail = true;
                }
                else if (thisInst.MTMimicHostAvail && dist > range)
                {
                    thisInst.MTMimicHostAvail = false;
                    // Make sure the player did not force the tech under the ground on release
                    if (AIEPathMapper.GetAltitudeLoadedOnly(tank.boundsCentreWorldNoCheck, out float height))
                        if (tank.boundsCentreWorldNoCheck.y < height)
                            tank.visible.MoveAboveGround();
                }
                if (!thisInst.MTLockedToTechBeam && copyTargVis.beam.IsActive && dist < range)
                {
                    thisInst.MTOffsetPos = copyTargVis.trans.InverseTransformPoint(tank.trans.position);
                    thisInst.MTOffsetRot = copyTargVis.trans.InverseTransformDirection(tank.trans.forward);
                    thisInst.MTOffsetRotUp = copyTargVis.trans.InverseTransformDirection(tank.trans.up);
                    //DebugTAC_AI.Log("TACtical_AI:AI " + tank.name + ": Synced position to " + thisInst.MTOffsetPos + " and rot to " + thisInst.MTOffsetRot);
                    thisInst.MTLockedToTechBeam = true;
                }
                else if (thisInst.MTLockedToTechBeam && !copyTargVis.beam.IsActive)
                {
                    thisInst.MTLockedToTechBeam = false;
                }
                if (!thisInst.MTLockedToTechBeam && thisInst.MTMimicHostAvail)
                {
                    TankControl.ControlState controlCopyTarget = (TankControl.ControlState)AIEPathing.controlGet.GetValue(thisInst.theResource.tank.control);
                    thisInst.FullBoost = controlCopyTarget.m_State.m_BoostJets;
                    thisInst.FirePROPS = controlCopyTarget.m_State.m_BoostProps;
                }
            }
            catch
            {
                thisInst.MTLockedToTechBeam = false;
                thisInst.MTMimicHostAvail = false;
            }
        }
        public static void BeamLockWithinBounds(AIECore.TankAIHelper thisInst, Tank tank)
        {
            Tank hostTech;
            float dist;
            Tank vis;
            try
            {
                if (!thisInst.AllMT)
                {
                    hostTech = thisInst.GetPlayerTech().tank;
                    if (hostTech == null)
                    {
                        thisInst.MTLockedToTechBeam = false;
                        return;
                    }
                    if (hostTech == tank)
                    {
                        thisInst.MTLockedToTechBeam = false;
                        return;
                    }
                    thisInst.theResource = hostTech.visible;
                    vis = hostTech;
                    dist = (tank.boundsCentreWorldNoCheck - hostTech.boundsCentreWorldNoCheck).magnitude;
                }
                else
                {
                    if (AIECore.FetchCopyableAlly(tank.boundsCentreWorldNoCheck, thisInst, out float distSqr, out var vis2))
                        hostTech = vis2.tank;
                    else
                        hostTech = null;
                    //hostTech = AIEPathing.ClosestAllyPrecision(AIEPathing.AllyList(tank), tank.boundsCentreWorldNoCheck, out dist, tank);
                    if (hostTech == null)
                    {
                        thisInst.MTLockedToTechBeam = false;
                        return;
                    }
                    thisInst.theResource = hostTech.visible;
                    vis = hostTech;
                    dist = Mathf.Sqrt(distSqr);
                }

                float range = thisInst.lastTechExtents + vis.GetCheapBounds();
                if (!thisInst.MTLockedToTechBeam && vis.beam.IsActive && dist < range)
                {
                    thisInst.MTOffsetPos = vis.trans.InverseTransformPoint(tank.trans.position);
                    thisInst.MTOffsetRot = vis.trans.InverseTransformDirection(tank.trans.forward);
                    thisInst.MTOffsetRotUp = vis.trans.InverseTransformDirection(tank.trans.up);
                    //DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ": Synced position to " + thisInst.MTOffsetPos + " and rot to " + thisInst.MTOffsetRot);
                    thisInst.MTLockedToTechBeam = true;
                }
                else if (thisInst.MTLockedToTechBeam && !vis.beam.IsActive)
                {
                    thisInst.MTLockedToTechBeam = false;
                }
            }
            catch
            {
                thisInst.MTLockedToTechBeam = false;
            }
        }

        public static void MimicDefend(AIECore.TankAIHelper thisInst, Tank tank)
        {
            // Determines the weapons actions and aiming of the AI, this one is for MTs that have a host
            thisInst.AttackEnemy = false;
            if (thisInst.theResource?.tank)
            {   //Get the tech the player is aiming at
                thisInst.lastEnemy = thisInst.theResource.tank.Weapons.GetManualTarget();
                if (thisInst.lastEnemyGet.IsNull())
                    thisInst.lastEnemy = tank.Vision.GetFirstVisibleTechIsEnemy(tank.Team);
            }
            else
                thisInst.lastEnemy = tank.Vision.GetFirstVisibleTechIsEnemy(tank.Team);
            if (thisInst.lastEnemyGet != null)
            {
                Vector3 aimTo = (thisInst.lastEnemyGet.transform.position - tank.transform.position).normalized;
                thisInst.Urgency++;
                if (Mathf.Abs((tank.rootBlockTrans.forward - aimTo).magnitude) < 0.15f || thisInst.Urgency >= 30)
                {
                    thisInst.AttackEnemy = true;
                    thisInst.Urgency = 30;
                }
            }
            else
            {
                thisInst.Urgency = 0;
                thisInst.AttackEnemy = false;
            }
        }
    }
}
