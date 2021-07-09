using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using UnityEngine;
using TAC_AI.AI;


namespace TAC_AI.AI.MovementAI
{
    public class VtolAI : AirplaneAI, IMovementAI
    {
        public override void Initiate(Tank tank, ITechDriver pilot)
        {
            base.Initiate(tank, pilot);
            this.pilot.FlyStyle = AIEAirborne.FlightType.VTOL;
        }
        public override bool DriveTech(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank)
        {
            if (pilot.Grounded)
            {   //Become a ground vehicle for now
                if (!AIEPathing.AboveHeightFromGround(tank.boundsCentreWorldNoCheck, AIECore.Extremes(tank.blockBounds.extents) * 2))
                {
                    return false;
                }
                //Try fighting the controls to land safely

                return true;
            }
            if (tank.wheelGrounded || pilot.ForcePitchUp)
            {   // Try and takeoff like helicopter
                pilot.MainThrottle = HelicopterUtils.ModerateUpwardsThrust(tank, thisInst, pilot, AIEPathing.OffsetFromGroundA(tank.boundsCentreWorldNoCheck, thisInst, AIECore.Extremes(tank.blockBounds.extents) * 2));
                AIEAirborne.UpdateThrottle(thisInst, pilot, thisControl);
                HelicopterUtils.AngleTowardsUp(thisControl, thisInst, tank, pilot, tank.boundsCentreWorldNoCheck, true);
            }
            else
            {   //Fly like plane
                if (pilot.PerformUTurn > 0)
                {   //The Immelmann Turn
                    //Debug.Log("TACtical_AI: Tech " + tank.name + "  U-Turn level " + pilot.PerformUTurn + "  throttle " + pilot.CurrentThrottle);
                    pilot.MainThrottle = 1;
                    AIEAirborne.UpdateThrottle(thisInst, pilot, thisControl);
                    if (tank.rootBlockTrans.InverseTransformVector(tank.rbody.velocity).z < AIEAirborne.AirAssistance.Stallspeed - 4)
                    {   //ABORT!!!
                        Debug.Log("TACtical_AI: Tech " + tank.name + "  Aborted U-Turn with velocity " + tank.rootBlockTrans.InverseTransformVector(pilot.Tank.rbody.velocity).z);
                        pilot.PerformUTurn = -1;
                    }
                    else if (Vector3.Dot(Vector3.down, tank.rbody.velocity.normalized) > 0.4f)
                    {   //ABORT!!!
                        Debug.Log("TACtical_AI: Tech " + tank.name + "  Aborted U-Turn as too much movement to the ground");
                        pilot.PerformUTurn = -1;
                    }
                    if (pilot.PerformUTurn == 1)
                    {
                        AircraftUtils.AngleTowards(thisControl, thisInst, tank, pilot, tank.boundsCentreWorldNoCheck + tank.rootBlockTrans.forward * 100);
                        if (pilot.CurrentThrottle > 0.95)
                            pilot.PerformUTurn = 2;
                    }
                    else if (pilot.PerformUTurn == 2)
                    {
                        AircraftUtils.AngleTowards(thisControl, thisInst, tank, pilot, tank.boundsCentreWorldNoCheck + (Vector3.up * 100));
                        if (Vector3.Dot(tank.rootBlockTrans.forward, Vector3.up) > 0.75f)
                            pilot.PerformUTurn = 3;
                    }
                    else if (pilot.PerformUTurn == 3)
                    {
                        AircraftUtils.AngleTowards(thisControl, thisInst, tank, pilot, pilot.AirborneDest);
                        if (Vector3.Dot((pilot.AirborneDest - tank.boundsCentreWorldNoCheck).normalized, tank.rootBlockTrans.forward) > 0.6f)
                            pilot.PerformUTurn = 0;
                    }
                    return true;
                }
                else if (pilot.PerformUTurn == -1)
                {
                    pilot.MainThrottle = 1;
                    AIEAirborne.UpdateThrottle(thisInst, pilot, thisControl);
                    AircraftUtils.AngleTowards(thisControl, thisInst, tank, pilot, pilot.AirborneDest);
                    if (Vector3.Dot(tank.rootBlockTrans.forward, (pilot.AirborneDest - tank.boundsCentreWorldNoCheck).normalized) > 0)
                        pilot.PerformUTurn = 0;
                    return true;
                }
                else
                {
                    AIEAirborne.UpdateThrottle(thisInst, pilot, thisControl);
                    AircraftUtils.AngleTowards(thisControl, thisInst, tank, pilot, pilot.AirborneDest);
                }
            }

            return true;
        }
    }
}
