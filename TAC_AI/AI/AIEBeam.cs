using System;
using UnityEngine;
using TAC_AI.AI.Movement;

namespace TAC_AI.AI
{
    internal static class AIEBeam
    {
        public static void BeamMaintainer(TankControl thisControl, TankAIHelper thisInst, Tank tank)
        {
            try
            {
                if (!thisInst.CanUseBuildBeam)
                {
                    if (tank.beam.IsActive)
                        tank.beam.EnableBeam(false);
                    return;
                }

                if (thisInst.BeamTimeoutClock > 0)
                {
                    if (!tank.beam.IsActive)
                        tank.beam.EnableBeam(true);
                    thisInst.FullBoost = false;
                    thisInst.LightBoost = false;
                    thisControl.BoostControlJets = false;
                    if (tank.rootBlockTrans.up.y > 0.95f)
                        thisInst.BeamTimeoutClock = 0;
                    else if (thisInst.BeamTimeoutClock > 40)
                    {
                        thisInst.BeamTimeoutClock = 0;
                    }
                    else
                        thisInst.BeamTimeoutClock++;
                }
                else
                    tank.beam.EnableBeam(false);

                if (thisInst.MovementController is AIControllerAir pilot)
                {   // Handoff all operations to AIEAirborne
                    if (!pilot.Grounded || AIEPathing.AboveHeightFromGroundTech(thisInst, thisInst.lastTechExtents))
                    {   //Become a ground vehicle for now
                        if (tank.grounded && IsTechTippedOver(tank, thisInst))
                        {
                            thisInst.BeamTimeoutClock = 10;
                        }
                        return;
                    }
                }


                if (!thisInst.IsMultiTech && thisInst.ForceSetBeam && thisInst.RequestBuildBeam)
                {
                    thisInst.BeamTimeoutClock = 35;
                }
                else if (!thisInst.IsMultiTech && IsTechTippedOver(tank, thisInst) && thisInst.RequestBuildBeam)
                {
                    if (thisInst.Attempt3DNavi)
                    {
                        //reduce build beam spam when aiming
                        thisInst.actionPause++;

                        if (thisInst.ActionPause == 70)
                            thisInst.actionPause = 100;
                        else if (thisInst.ActionPause > 80)
                        {
                            thisInst.BeamTimeoutClock = 1;
                            //thisInst.ActionPause--;
                        }
                        else if (thisInst.ActionPause == 80)
                            thisInst.actionPause = 0;
                    }
                    else
                    {
                        thisInst.BeamTimeoutClock = 1;
                    }
                }
                else
                {
                    if (thisInst.Attempt3DNavi)
                        thisInst.actionPause = 0;
                    if (thisInst.MTLockedToTechBeam && thisInst.IsMultiTech)
                    {   //Override and disable most driving abilities - We are going to follow the host tech!
                        if (thisInst.lastCloseAlly != null)
                        {
                            if (thisInst.lastCloseAlly.beam.IsActive)
                            {
                                //tank.beam.EnableBeam(true);
                                var allyTrans = thisInst.lastCloseAlly.trans;
                                tank.rbody.velocity = Vector3.zero;
                                thisInst.tank.trans.rotation = Quaternion.LookRotation(allyTrans.TransformDirection(thisInst.MTOffsetRot), allyTrans.TransformDirection(thisInst.MTOffsetRotUp));
                                thisInst.tank.trans.position = allyTrans.TransformPoint(thisInst.MTOffsetPos);
                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            { DebugTAC_AI.Log("TACtical_AI: Error in AIEBeam - " + e); }
        }

        /*
        //Disabled this as it proved annoying
        if (tank.blockman.blockCount > lastBlockCount)
        {
            thisInst.LastBuildClock = 0;
        }
        lastBlockCount = tank.blockman.blockCount;

        if (thisInst.LastBuildClock < 200)
        {
            thisControl.DriveControl = 0;
            thisControl.m_Movement.m_USE_AVOIDANCE = true;
            thisInst.LastBuildClock++;
            //thisControl.SetBeamControlState(true);
            tank.beam.nudgeSpeedForward = 0;
            tank.beam.EnableBeam(true);

            if (thisInst.DANGER && thisInst.lastEnemy != null)
            {
                var targetTank = thisInst.lastEnemy.gameObject.GetComponent<Tank>();
                thisControl.m_Weapons.FireAtTarget(tank, thisInst.lastEnemy.gameObject.transform.position, Extremes(targetTank.blockBounds.extents));
                thisInst.lastWeaponAction = 1;
            }
            else
                thisInst.lastWeaponAction = 0;
            return;
        }
        */

        /*
        if (thisInst.IsLikelyJammed && thisInst.recentSpeed < 10)
        {
            thisControl.DriveControl = 0;
            bool Stop = AttemptFree();
            if (Stop)
                return;
        }
        */


        private static bool IsTechTippedOver(Tank tank, TankAIHelper thisInst)
        {   // It's more crude than the built-in but should take less to process.
            if (tank.rootBlockTrans.up.y < 0)
            {   // the Tech is literally sideways
                return true;
            }
            else if (tank.rootBlockTrans.up.y < 0.25f)
            {   // If we are still moving, we DON'T Build Beam - we are climbing a slope
                return thisInst.recentSpeed < thisInst.EstTopSped / 8;
            }
            //tank.AI.IsTankOverturned()
            return false;
        }
    }
}
