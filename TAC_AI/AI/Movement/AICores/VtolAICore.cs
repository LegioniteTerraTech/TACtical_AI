using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using UnityEngine;
using TAC_AI.AI;


namespace TAC_AI.AI.Movement.AICores
{
    internal class VtolAICore : AirplaneAICore, IMovementAICore
    {
        public override void Initiate(Tank tank, IMovementAIController pilot)
        {
            base.Initiate(tank, pilot);
            this.pilot.FlyStyle = AIControllerAir.FlightType.VTOL;
            pilot.Helper.GroundOffsetHeight = pilot.Helper.lastTechExtents + AIGlobals.GroundOffsetAircraft;
        }
        public override bool DriveMaintainer(TankAIHelper helper, Tank tank, ref EControlCoreSet core)
        {
            if (pilot.Grounded)
            {   //Become a ground vehicle for now
                if (!AIEPathing.AboveHeightFromGroundTech(helper, helper.lastTechExtents * 2))
                {
                    return false;
                }
                //Try fighting the controls to land safely

                return true;
            }
            if (tank.wheelGrounded || pilot.ForcePitchUp)
            {   // Try and takeoff like helicopter
                pilot.MainThrottle = HelicopterUtils.ModerateUpwardsThrust(tank, helper, pilot,
                    AIEPathing.OffsetFromGroundA(tank.boundsCentreWorldNoCheck, helper, helper.lastTechExtents * 2).y);
                pilot.UpdateThrottle(helper);
                HelicopterUtils.AngleTowardsUp(pilot, tank.boundsCentreWorldNoCheck, helper.lastDestinationCore, ref core, true);
            }
            else
            {   //Fly like plane
                if (PerformUTurn > 0)
                {   //The Immelmann Turn
                    //DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + "  U-Turn level " + pilot.PerformUTurn + "  throttle " + pilot.CurrentThrottle);
                    pilot.MainThrottle = 1;
                    pilot.UpdateThrottle(helper);
                    if ( helper.LocalSafeVelocity.z < AIGlobals.AirStallSpeed - 4)
                    {   //ABORT!!!
                        DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + "  Aborted U-Turn with velocity " + helper.LocalSafeVelocity.z);
                        PerformUTurn = -1;
                    }
                    else if (Vector3.Dot(Vector3.down,  helper.SafeVelocity.normalized) > 0.4f)
                    {   //ABORT!!!
                        DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + "  Aborted U-Turn as too much movement to the ground");
                        PerformUTurn = -1;
                    }
                    if (PerformUTurn == 1)
                    {
                        AngleTowards(helper, tank, pilot, tank.boundsCentreWorldNoCheck + tank.rootBlockTrans.forward * 100);
                        if (pilot.CurrentThrottle > 0.95)
                            PerformUTurn = 2;
                    }
                    else if (PerformUTurn == 2)
                    {
                        AngleTowards(helper, tank, pilot, tank.boundsCentreWorldNoCheck + (Vector3.up * 100));
                        if (Vector3.Dot(tank.rootBlockTrans.forward, Vector3.up) > 0.75f)
                            PerformUTurn = 3;
                    }
                    else if (PerformUTurn == 3)
                    {
                        AngleTowards(helper, tank, pilot, pilot.PathPointSet);
                        if (Vector3.Dot((pilot.PathPointSet - tank.boundsCentreWorldNoCheck).normalized, tank.rootBlockTrans.forward) > 0.6f)
                            PerformUTurn = 0;
                    }
                    return true;
                }
                else if (PerformUTurn == -1)
                {
                    pilot.MainThrottle = 1;
                    pilot.UpdateThrottle(helper);
                    AngleTowards(helper, tank, pilot, pilot.PathPointSet);
                    if (Vector3.Dot(tank.rootBlockTrans.forward, (pilot.PathPointSet - tank.boundsCentreWorldNoCheck).normalized) > 0)
                        PerformUTurn = 0;
                    return true;
                }
                else
                {
                    pilot.UpdateThrottle(helper);
                    AngleTowards(helper, tank, pilot, pilot.PathPointSet);
                }
            }

            return true;
        }
    }
}
