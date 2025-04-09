using System;
using UnityEngine;
using TAC_AI.AI.Movement;

namespace TAC_AI.AI
{
    internal static class AIEBeam
    {
        public static void BeamMaintainer(TankControl thisControl, TankAIHelper helper, Tank tank)
        {
            try
            {
                if (!helper.CanUseBuildBeam)
                {
                    if (tank.beam.IsActive)
                        tank.beam.EnableBeam(false);
                    return;
                }

                if (helper.BeamTimeoutClock > 0)
                {
                    if (!tank.beam.IsActive)
                        tank.beam.EnableBeam(true);
                    helper.FullBoost = false;
                    helper.LightBoost = false;
                    thisControl.BoostControlJets = false;
                    if (tank.rootBlockTrans.up.y > 0.95f)
                        helper.BeamTimeoutClock = 0;
                    else if (helper.BeamTimeoutClock > 40)
                    {
                        helper.BeamTimeoutClock = 0;
                    }
                    else
                        helper.BeamTimeoutClock++;
                }
                else
                    tank.beam.EnableBeam(false);

                if (helper.MovementController is AIControllerAir pilot)
                {   // Handoff all operations to AIEAirborne
                    if (!pilot.Grounded || AIEPathing.AboveHeightFromGroundTech(helper, helper.lastTechExtents))
                    {   //Become a ground vehicle for now
                        if (tank.grounded && IsTechTippedOver(tank, helper))
                        {
                            helper.BeamTimeoutClock = 10;
                        }
                        return;
                    }
                }


                if (!helper.IsMultiTech && helper.ForceSetBeam && helper.RequestBuildBeam)
                {
                    helper.BeamTimeoutClock = 35;
                }
                else if (!helper.IsMultiTech && IsTechTippedOver(tank, helper) && helper.RequestBuildBeam)
                {
                    if (helper.Attempt3DNavi)
                    {
                        //reduce build beam spam when aiming
                        helper.actionPause++;

                        if (helper.ActionPause == 70)
                            helper.actionPause = 100;
                        else if (helper.ActionPause > 80)
                        {
                            helper.BeamTimeoutClock = 1;
                            //helper.ActionPause--;
                        }
                        else if (helper.ActionPause == 80)
                            helper.actionPause = 0;
                    }
                    else
                    {
                        helper.BeamTimeoutClock = 1;
                    }
                }
                else
                {
                    if (helper.Attempt3DNavi)
                        helper.actionPause = 0;
                    if (helper.MTLockedToTechBeam && helper.IsMultiTech)
                    {   //Override and disable most driving abilities - We are going to follow the host tech!
                        if ((bool)helper.theResource?.tank)
                        {
                            if (helper.theResource.tank.beam.IsActive)
                            {
                                //tank.beam.EnableBeam(true);
                                var allyTrans = helper.theResource.tank.trans;
                                tank.rbody.velocity = Vector3.zero;
                                helper.tank.trans.rotation = AIGlobals.LookRot(allyTrans.TransformDirection(helper.MTOffsetRot), allyTrans.TransformDirection(helper.MTOffsetRotUp));
                                helper.tank.trans.position = allyTrans.TransformPoint(helper.MTOffsetPos);
                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            { DebugTAC_AI.Log(KickStart.ModID + ": Error in AIEBeam - " + e); }
        }

        /*
        //Disabled this as it proved annoying
        if (tank.blockman.blockCount > lastBlockCount)
        {
            helper.LastBuildClock = 0;
        }
        lastBlockCount = tank.blockman.blockCount;

        if (helper.LastBuildClock < 200)
        {
            thisControl.DriveControl = 0;
            thisControl.m_Movement.m_USE_AVOIDANCE = true;
            helper.LastBuildClock++;
            //thisControl.SetBeamControlState(true);
            tank.beam.nudgeSpeedForward = 0;
            tank.beam.EnableBeam(true);

            if (helper.DANGER && helper.lastEnemy != null)
            {
                var targetTank = helper.lastEnemy.gameObject.GetComponent<Tank>();
                thisControl.m_Weapons.FireAtTarget(tank, helper.lastEnemy.gameObject.transform.position, Extremes(targetTank.blockBounds.extents));
                helper.lastWeaponAction = 1;
            }
            else
                helper.lastWeaponAction = 0;
            return;
        }
        */

        /*
        if (helper.IsLikelyJammed && helper.recentSpeed < 10)
        {
            thisControl.DriveControl = 0;
            bool Stop = AttemptFree();
            if (Stop)
                return;
        }
        */


        private static bool IsTechTippedOver(Tank tank, TankAIHelper helper)
        {   // It's more crude than the built-in but should take less to process.
            if (tank.rootBlockTrans.up.y < 0)
            {   // the Tech is literally sideways
                return true;
            }
            else if (tank.rootBlockTrans.up.y < 0.25f)
            {   // If we are still moving, we DON'T Build Beam - we are climbing a slope
                return helper.recentSpeed < helper.EstTopSped / 8;
            }
            //tank.AI.IsTankOverturned()
            return false;
        }
    }
}
