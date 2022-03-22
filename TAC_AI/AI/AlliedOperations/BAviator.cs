using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TAC_AI.AI.AlliedOperations
{
    public static class BAviator {
        public static void MotivateFly(AIECore.TankAIHelper thisInst, Tank tank)
        {   // Will have to account for the different types of flight methods available
            BGeneral.ResetValues(thisInst);
            thisInst.AvoidStuff = true;

            if (thisInst.lastPlayer == null)
                return;

            if (!(thisInst.MovementController is AIControllerAir))
            {
                Debug.Log("TACtical_AI: AI " + tank.name + ":  FIRED MotivateFly WITHOUT THE REQUIRED AIControllerAir MODULE!!!");
                return;
            }

            float dist = (tank.boundsCentreWorldNoCheck - thisInst.lastPlayer.tank.boundsCentreWorldNoCheck).magnitude - AIECore.Extremes(thisInst.lastPlayer.tank.blockBounds.extents);
            float range = (thisInst.RangeToStopRush * 4) + AIECore.Extremes(tank.blockBounds.extents);
            // The range is nearly quadrupled here due to dogfighting conditions
            thisInst.lastRange = dist;

            float playerExt = AIECore.Extremes(thisInst.lastPlayer.tank.blockBounds.extents);
            thisInst.lastTechExtents = AIECore.Extremes(tank.blockBounds.extents) * 2;

            if (tank.wheelGrounded)
            {
                if (!thisInst.IsTechMoving(thisInst.EstTopSped / 8))
                    thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                else
                    thisInst.SettleDown();
            }

            float spacing = thisInst.lastTechExtents + playerExt;
            if (dist < ((spacing) * 2) + 5)
            {   // TOO CLOSE!!! WE DODGE!!!
                if (thisInst.lastEnemy != null && !thisInst.Retreat)
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                else
                    thisInst.lastDestination = thisInst.lastPlayer.tank.boundsCentreWorldNoCheck;
                thisInst.MoveFromObjective = true;
            }
            else if (dist > spacing && dist < range)
            {   // Follow the leader
                if (thisInst.lastEnemy != null && !thisInst.Retreat)
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                else
                    thisInst.lastDestination = thisInst.lastPlayer.tank.boundsCentreWorldNoCheck;
                thisInst.ProceedToObjective = true;
            }
            else
            {   // Far behind, must catch up
                thisInst.lastDestination = thisInst.lastPlayer.tank.boundsCentreWorldNoCheck;
                thisInst.ProceedToObjective = true;
                thisInst.Retreat = true;
                thisInst.BOOST = true; // boost in forwards direction towards objective
            }
        }


        public static void Dogfighting(AIECore.TankAIHelper thisInst, Tank tank)
        {   // Will have to account for the different types of flight methods available

            thisInst.DANGER = false;
            thisInst.lastEnemy = tank.Vision.GetFirstVisibleTechIsEnemy(tank.Team);
            if (thisInst.lastEnemy != null)
            {
                Vector3 aimTo = (thisInst.lastEnemy.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized;
                thisInst.Urgency += KickStart.AIClockPeriod / 5;
                AIControllerAir pilot = (AIControllerAir) thisInst.MovementController;
                /*
                if (KickStart.isWeaponAimModPresent && thisInst.SideToThreat && (pilot.LargeAircraft || pilot.BankOnly))
                {   // AC-130 broadside attack
                    if ( Mathf.Abs(Vector3.Dot(tank.rootBlockTrans.right, aimTo)) > 0.25f || thisInst.Urgency >= 30)
                    {
                        thisInst.DANGER = true;
                        //thisInst.Urgency = 50;
                        thisInst.SettleDown();
                    }
                }
                else
                {  */ // Normal Dogfighting
                if (Vector3.Dot(tank.rootBlockTrans.forward, aimTo) > 0.25f || thisInst.Urgency >= 30)
                    {
                        thisInst.DANGER = true;
                        //thisInst.Urgency = 50;
                        thisInst.SettleDown();
                    }
                //}
            }
            else
            {
                thisInst.Urgency = 0;
                thisInst.DANGER = false;
            }
        }
    }
}
