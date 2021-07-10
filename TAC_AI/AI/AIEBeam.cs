using UnityEngine;
using TAC_AI.AI.Movement;

namespace TAC_AI.AI
{
    public static class AIEBeam
    {
        public static void BeamDirector(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank)
        {
            if (thisInst.beamClock > 0)
            {
                tank.beam.EnableBeam(true);
                thisInst.BOOST = false;
                thisInst.featherBoost = false;
                thisControl.BoostControlJets = false;
                if (tank.rootBlockTrans.up.y > 0.95f)
                    thisInst.beamClock = 0;
                else if (thisInst.beamClock > 40)
                {
                    thisInst.beamClock = 0;
                }
                else
                    thisInst.beamClock++;
            }
            else
                tank.beam.EnableBeam(false);

            if (thisInst.MovementController is AIControllerAir pilot)
            {   // Handoff all operations to AIEAirborne
                if (!pilot.Grounded || AIEPathing.AboveHeightFromGround(tank.boundsCentreWorldNoCheck, AIECore.Extremes(tank.blockBounds.extents) * 2))
                {   //Become a ground vehicle for now
                    if (tank.grounded && tank.AI.IsTankOverturned())
                    {
                        thisInst.beamClock = 1;
                    }
                    return;
                }
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

            if (!thisInst.IsMultiTech && thisInst.forceBeam && thisInst.RequestBuildBeam)
            {
                /*
                if (thisInst.lastPlayer.IsNotNull())
                {
                    Vector3 aimTo = (thisInst.lastPlayer.rbody.position - tank.rbody.position).normalized;
                    float driveAngle = Vector3.Angle(aimTo, tank.transform.forward);
                    float turnDrive = Mathf.Clamp((driveAngle / thisInst.AnchorAimDampening) * 30, -30, 30);
                    tank.beam.nudgeSpeedRotate = turnDrive;
                    tank.beam.nudgeSpeedForward = 5;
                }
                */
                thisInst.beamClock = 35;
            }
            else if (!thisInst.IsMultiTech && tank.AI.IsTankOverturned() && thisInst.RequestBuildBeam)
            {
                if (thisInst.DediAI == AIType.Astrotech)
                {
                    //reduce build beam spam when aiming
                    thisInst.ActionPause++;

                    if (thisInst.ActionPause == 70)
                        thisInst.ActionPause = 100;
                    else if (thisInst.ActionPause > 80)
                    {
                        thisInst.beamClock = 1;
                        thisInst.ActionPause--;
                    }
                    else if (thisInst.ActionPause == 80)
                        thisInst.ActionPause = 0;
                }
                else
                {
                    thisInst.beamClock = 1;
                }
            }
            else
            {

                if (thisInst.DediAI == AIType.Astrotech)
                    thisInst.ActionPause = 0;
                if (thisInst.MTLockedToTechBeam && thisInst.IsMultiTech)
                {   //Override and disable most driving abilities - We are going to follow the host tech!
                    if (thisInst.LastCloseAlly!= null)
                    {
                        if (thisInst.LastCloseAlly.beam.IsActive)
                        {
                            //tank.beam.EnableBeam(true);
                            var allyTrans = thisInst.LastCloseAlly.trans;
                            tank.rbody.velocity = Vector3.zero;
                            thisInst.tank.trans.rotation = Quaternion.LookRotation(allyTrans.TransformDirection(thisInst.MTOffsetRot), allyTrans.TransformDirection(thisInst.MTOffsetRotUp));
                            thisInst.tank.trans.position = allyTrans.TransformPoint(thisInst.MTOffsetPos);
                            return;
                        }
                    }
                }
            }
        }


        //On second thought the ability to unjam two techs is unfair compared to the enemy so I'll leave this be 
        //  The boosters help this greatly already
        /*
        private bool AttemptFree()
        {
            // Attempts to separate two jammed techs
            var thisInst = gameObject.GetComponent<TankAIHelper>();
            Vector3 pos = tank.rbody.position;
            Tank closest = ClosestAlly(pos, out float movedist);
            if (movedist < thisInst.lastTechExtents + Extremes(closest.blockBounds.extents))
            {
                Debug.Log("TACtical_AI:AttemptFree");
                tank.beam.EnableBeam(true);
                Vector3 aimTo = -(closest.rbody.position - tank.rbody.position).normalized;
                float driveAngle = Vector3.Angle(aimTo, tank.transform.forward);
                float turnDrive = Mathf.Clamp((driveAngle / thisInst.AnchorAimDampening) * 30, -30, 30);
                return true;
            }
            return false;
        }
        */
    }
}
