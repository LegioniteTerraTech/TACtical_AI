﻿using System;
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
            thisInst.lastPlayer = thisInst.GetPlayerTech();
            thisInst.IsMultiTech = false;

            BGeneral.ResetValues(thisInst);
            thisInst.AvoidStuff = true;

            if (thisInst.lastPlayer == null)
                return;

            if (!(thisInst.MovementController is AIControllerAir))
            {
                DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ":  FIRED MotivateFly WITHOUT THE REQUIRED AIControllerAir MODULE!!!");
                return;
            }

            float thisExtents = thisInst.lastTechExtents;
            float dist = thisInst.GetDistanceFromTask(thisInst.lastPlayer.tank.boundsCentreWorldNoCheck, thisInst.lastPlayer.GetCheapBounds());
            float range = thisInst.RangeToStopRush + thisExtents;

            float playerExt = thisInst.lastPlayer.GetCheapBounds();

            if (tank.wheelGrounded)
            {
                if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                    thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                else
                    thisInst.SettleDown();
            }

            float spacing = thisExtents + playerExt;
            if (dist < spacing + (AIGlobals.ExtraSpace * 2))
            {   // TOO CLOSE!!! WE DODGE!!!
                if (thisInst.lastEnemy != null && !thisInst.Retreat)
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                else
                    thisInst.lastDestination = thisInst.lastPlayer.tank.boundsCentreWorldNoCheck;
                thisInst.DriveDest = EDriveDest.FromLastDestination;
            }
            else if (dist > spacing && dist < range)
            {   // Follow the leader
                if (thisInst.lastEnemy != null && !thisInst.Retreat)
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                else
                    thisInst.lastDestination = thisInst.lastPlayer.tank.boundsCentreWorldNoCheck;
                thisInst.DriveDest = EDriveDest.ToLastDestination;
            }
            else if (dist < range * 3)
            {   // Far behind, must catch up
                // The range is nearly quadrupled here due to dogfighting conditions
                if (thisInst.lastEnemy != null && !thisInst.Retreat)
                {
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                }
                else
                {
                    thisInst.lastDestination = thisInst.lastPlayer.tank.boundsCentreWorldNoCheck;
                    thisInst.DriveDest = EDriveDest.ToLastDestination;
                    thisInst.BOOST = true; // boost in forwards direction towards objective
                }
            }
            else
            {   // SUPER Far behind, must catch up
                thisInst.lastDestination = thisInst.lastPlayer.tank.boundsCentreWorldNoCheck;
                thisInst.DriveDest = EDriveDest.ToLastDestination;
                thisInst.Retreat = true;
                thisInst.BOOST = true; // boost in forwards direction towards objective
            }
        }


        public static void Dogfighting(AIECore.TankAIHelper thisInst, Tank tank)
        {   // Will have to account for the different types of flight methods available

            thisInst.AttackEnemy = false;
            thisInst.lastEnemy = tank.Vision.GetFirstVisibleTechIsEnemy(tank.Team);
            if (thisInst.lastEnemy != null)
            {
                Vector3 aimTo = (thisInst.lastEnemy.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized;
                AIControllerAir pilot = (AIControllerAir) thisInst.MovementController;
                Vector3 foreDirect = tank.rootBlockTrans.InverseTransformDirection(aimTo);
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
                if (thisInst.SideToThreat)
                {   // Wide forwards attack
                    thisInst.Urgency += KickStart.AIClockPeriod / 5f;
                    if ((foreDirect.z > 0.15f && foreDirect.x > -0.5f && foreDirect.x < 0.5f) || thisInst.Urgency >= 30)
                    {
                        thisInst.AttackEnemy = true;
                        thisInst.SettleDown();
                    }
                }
                else
                {   // Normal Dogfighting
                    thisInst.Urgency += KickStart.AIClockPeriod / 5f;
                    if ((foreDirect.z > 0.15f && foreDirect.x > -0.35f && foreDirect.x < 0.35f) || thisInst.Urgency >= 30)
                    {
                        thisInst.AttackEnemy = true;
                        thisInst.SettleDown();
                    }
                }
                //}
            }
            else
            {
                thisInst.Urgency = 0;
                thisInst.AttackEnemy = false;
            }
        }
    }
}
