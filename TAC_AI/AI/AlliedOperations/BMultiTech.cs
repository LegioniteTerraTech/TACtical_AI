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
        public static void MTStatic(AIECore.TankAIHelper thisInst, Tank tank)
        {   // stay still
            thisInst.lastPlayer = thisInst.GetPlayerTech();
            thisInst.IsMultiTech = true;

            BGeneral.ResetValues(thisInst);

            thisInst.DANGER = false;
            thisInst.PivotOnly = true;
        }

        public static void MimicAllClosestAlly(AIECore.TankAIHelper thisInst, Tank tank)
        {
            thisInst.lastPlayer = thisInst.GetPlayerTech();
            thisInst.IsMultiTech = true;
            thisInst.Attempt3DNavi = true;

            Tank hostTech;
            float dist = 0;
            Tank vis;
            BGeneral.ResetValues(thisInst);
            try
            {
                if (thisInst.OnlyPlayerMT)
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
                    thisInst.LastCloseAlly = hostTech;
                    vis = thisInst.LastCloseAlly;
                    dist = (tank.boundsCentreWorldNoCheck - vis.boundsCentreWorldNoCheck).magnitude;
                }
                else
                {
                    hostTech = AIEPathing.ClosestAllyPrecision(tank.boundsCentreWorldNoCheck, out dist, tank);
                    if (hostTech == null)
                        hostTech = thisInst.GetPlayerTech().tank;
                    if (hostTech == null)
                    {
                        thisInst.MTLockedToTechBeam = false;
                        thisInst.MTMimicHostAvail = false;
                        return;
                    }
                    thisInst.LastCloseAlly = hostTech;
                    vis = thisInst.LastCloseAlly;
                    dist = (tank.boundsCentreWorldNoCheck - vis.boundsCentreWorldNoCheck).magnitude;
                }

                //float range = thisInst.lastTechExtents + vis.GetCheapBounds();
                float range = thisInst.IdealRangeCombat;
                if (!thisInst.MTMimicHostAvail)
                {
                    thisInst.MTMimicHostAvail = true;
                }
                else if (thisInst.MTMimicHostAvail && dist > range)
                {
                    thisInst.MTMimicHostAvail = false;
                    // Make sure the player did not force the tech under the ground on release
                    if (Singleton.Manager<ManWorld>.inst.GetTerrainHeight(tank.boundsCentreWorldNoCheck, out float height))
                        if (tank.boundsCentreWorldNoCheck.y < height)
                            tank.visible.MoveAboveGround();
                }
                if (!thisInst.MTLockedToTechBeam && vis.beam.IsActive && dist < range)
                {
                    thisInst.MTOffsetPos = vis.trans.InverseTransformPoint(tank.trans.position);
                    thisInst.MTOffsetRot = vis.trans.InverseTransformDirection(tank.trans.forward);
                    thisInst.MTOffsetRotUp = vis.trans.InverseTransformDirection(tank.trans.up);
                    //Debug.Log("TACtical_AI:AI " + tank.name + ": Synced position to " + thisInst.MTOffsetPos + " and rot to " + thisInst.MTOffsetRot);
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
                if (thisInst.OnlyPlayerMT)
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
                    thisInst.LastCloseAlly = hostTech;
                    vis = thisInst.LastCloseAlly;
                    dist = (tank.boundsCentreWorldNoCheck - vis.boundsCentreWorldNoCheck).magnitude;
                }
                else
                {
                    hostTech = AIEPathing.ClosestAllyPrecision(tank.boundsCentreWorldNoCheck, out dist, tank);
                    if (hostTech == null)
                    {
                        thisInst.MTLockedToTechBeam = false;
                        return;
                    }
                    thisInst.LastCloseAlly = hostTech;
                    vis = thisInst.LastCloseAlly;
                    dist = (tank.boundsCentreWorldNoCheck - vis.boundsCentreWorldNoCheck).magnitude;
                }

                float range = thisInst.lastTechExtents + vis.GetCheapBounds();
                if (!thisInst.MTLockedToTechBeam && vis.beam.IsActive && dist < range)
                {
                    thisInst.MTOffsetPos = vis.trans.InverseTransformPoint(tank.trans.position);
                    thisInst.MTOffsetRot = vis.trans.InverseTransformDirection(tank.trans.forward);
                    thisInst.MTOffsetRotUp = vis.trans.InverseTransformDirection(tank.trans.up);
                    //Debug.Log("TACtical_AI: AI " + tank.name + ": Synced position to " + thisInst.MTOffsetPos + " and rot to " + thisInst.MTOffsetRot);
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
            thisInst.DANGER = false;
            if (thisInst.LastCloseAlly.IsNotNull())
            {   //Get the tech the player is aiming at
                thisInst.lastEnemy = thisInst.LastCloseAlly.Weapons.GetManualTarget();
                if (thisInst.lastEnemy.IsNull())
                    thisInst.lastEnemy = tank.Vision.GetFirstVisibleTechIsEnemy(tank.Team);
            }
            else
                thisInst.lastEnemy = tank.Vision.GetFirstVisibleTechIsEnemy(tank.Team);
            if (thisInst.lastEnemy != null)
            {
                Vector3 aimTo = (thisInst.lastEnemy.transform.position - tank.transform.position).normalized;
                thisInst.Urgency++;
                if (Mathf.Abs((tank.rootBlockTrans.forward - aimTo).magnitude) < 0.15f || thisInst.Urgency >= 30)
                {
                    thisInst.DANGER = true;
                    thisInst.Urgency = 30;
                }
            }
            else
            {
                thisInst.Urgency = 0;
                thisInst.DANGER = false;
            }
        }
    }
}
