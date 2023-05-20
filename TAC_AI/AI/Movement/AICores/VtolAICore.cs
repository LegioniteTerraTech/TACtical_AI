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
    public class VtolAICore : AirplaneAICore, IMovementAICore
    {
        public override void Initiate(Tank tank, IMovementAIController pilot)
        {
            base.Initiate(tank, pilot);
            this.pilot.FlyStyle = AIControllerAir.FlightType.VTOL;
            pilot.Helper.GroundOffsetHeight = pilot.Helper.lastTechExtents + AIGlobals.GroundOffsetAircraft;
        }
        public override bool DriveMaintainer(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank, ref EControlCoreSet core)
        {
            if (pilot.Grounded)
            {   //Become a ground vehicle for now
                if (!AIEPathing.AboveHeightFromGroundTech(thisInst, thisInst.lastTechExtents * 2))
                {
                    return false;
                }
                //Try fighting the controls to land safely

                return true;
            }
            if (tank.wheelGrounded || pilot.ForcePitchUp)
            {   // Try and takeoff like helicopter
                pilot.MainThrottle = HelicopterUtils.ModerateUpwardsThrust(tank, thisInst, pilot, AIEPathing.OffsetFromGroundA(tank.boundsCentreWorldNoCheck, thisInst, thisInst.lastTechExtents * 2));
                this.pilot.UpdateThrottle(thisInst, thisControl);
                HelicopterUtils.AngleTowardsUp(thisControl, pilot, tank.boundsCentreWorldNoCheck, thisInst.lastDestinationCore, ref core, true);
            }
            else
            {   //Fly like plane
                if (pilot.PerformUTurn > 0)
                {   //The Immelmann Turn
                    //DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + "  U-Turn level " + pilot.PerformUTurn + "  throttle " + pilot.CurrentThrottle);
                    pilot.MainThrottle = 1;
                    this.pilot.UpdateThrottle(thisInst, thisControl);
                    if (tank.rootBlockTrans.InverseTransformVector(tank.rbody.velocity).z < AIGlobals.AirStallSpeed - 4)
                    {   //ABORT!!!
                        DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + "  Aborted U-Turn with velocity " + tank.rootBlockTrans.InverseTransformVector(pilot.Tank.rbody.velocity).z);
                        pilot.PerformUTurn = -1;
                    }
                    else if (Vector3.Dot(Vector3.down, tank.rbody.velocity.normalized) > 0.4f)
                    {   //ABORT!!!
                        DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + "  Aborted U-Turn as too much movement to the ground");
                        pilot.PerformUTurn = -1;
                    }
                    if (pilot.PerformUTurn == 1)
                    {
                        AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, tank.boundsCentreWorldNoCheck + tank.rootBlockTrans.forward * 100);
                        if (pilot.CurrentThrottle > 0.95)
                            pilot.PerformUTurn = 2;
                    }
                    else if (pilot.PerformUTurn == 2)
                    {
                        AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, tank.boundsCentreWorldNoCheck + (Vector3.up * 100));
                        if (Vector3.Dot(tank.rootBlockTrans.forward, Vector3.up) > 0.75f)
                            pilot.PerformUTurn = 3;
                    }
                    else if (pilot.PerformUTurn == 3)
                    {
                        AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, pilot.PathPointSet);
                        if (Vector3.Dot((pilot.PathPointSet - tank.boundsCentreWorldNoCheck).normalized, tank.rootBlockTrans.forward) > 0.6f)
                            pilot.PerformUTurn = 0;
                    }
                    return true;
                }
                else if (pilot.PerformUTurn == -1)
                {
                    pilot.MainThrottle = 1;
                    this.pilot.UpdateThrottle(thisInst, thisControl);
                    AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, pilot.PathPointSet);
                    if (Vector3.Dot(tank.rootBlockTrans.forward, (pilot.PathPointSet - tank.boundsCentreWorldNoCheck).normalized) > 0)
                        pilot.PerformUTurn = 0;
                    return true;
                }
                else
                {
                    this.pilot.UpdateThrottle(thisInst, thisControl);
                    AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, pilot.PathPointSet);
                }
            }

            return true;
        }
    }
}
