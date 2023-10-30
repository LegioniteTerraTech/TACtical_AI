using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TAC_AI.AI.AlliedOperations
{
    internal static class BAviator 
    {
        public static void MotivateFly(TankAIHelper thisInst, Tank tank, ref EControlOperatorSet direct)
        {   // Will have to account for the different types of flight methods available
            thisInst.lastPlayer = thisInst.GetPlayerTech();
            thisInst.IsMultiTech = false;

            BGeneral.ResetValues(thisInst, ref direct);
            thisInst.AvoidStuff = true;

            if (thisInst.lastPlayer == null)
            {
                DebugTAC_AI.LogError("TACtical_AI: AI " + tank.name + ":  MotivateFly could not get valid lastPlayer");
                return;
            }

            if (!(thisInst.MovementController is AIControllerAir))
            {
                DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ":  FIRED MotivateFly WITHOUT THE REQUIRED AIControllerAir MODULE!!!");
                return;
            }

            float thisExtents = thisInst.lastTechExtents;
            float dist = thisInst.GetDistanceFromTask(thisInst.lastPlayer.tank.boundsCentreWorldNoCheck, thisInst.lastPlayer.GetCheapBounds());
            float range = thisInst.MaxObjectiveRange + thisExtents;

            float playerExt = thisInst.lastPlayer.GetCheapBounds();

            if (tank.wheelGrounded)
            {
                if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                    thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                else
                    thisInst.SettleDown();
            }
            float spacing = thisExtents + playerExt;
            if (thisInst.lastEnemyGet && !thisInst.Retreat && dist > range * 2)
            {
                float distCombat = thisInst.lastCombatRange;
                float spacingCombat = thisExtents + thisInst.lastEnemyGet.GetCheapBounds();
                direct.SetLastDest(thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck);
                if (distCombat < spacingCombat + (AIGlobals.PathfindingExtraSpace * 2))
                {   // TOO CLOSE!!! WE DODGE!!!
                    direct.DriveDest = EDriveDest.FromLastDestination;
                }
                else if (distCombat > spacing && distCombat < spacingCombat)
                {   // Follow the enemy
                    direct.DriveDest = EDriveDest.ToLastDestination;
                }
                else
                {   // Far behind, must catch up
                    // The range is nearly quadrupled here due to dogfighting conditions
                    direct.DriveDest = EDriveDest.ToLastDestination;
                    thisInst.FullBoost = true; // boost in forwards direction towards objective
                }
            }
            else
            {
                direct.SetLastDest(thisInst.lastPlayer.tank.boundsCentreWorldNoCheck);
                if (dist < spacing + (AIGlobals.PathfindingExtraSpace * 2))
                {   // TOO CLOSE!!! WE DODGE!!!
                    direct.DriveDest = EDriveDest.FromLastDestination;
                }
                else if (dist > spacing && dist < range)
                {   // Follow the leader
                    direct.DriveDest = EDriveDest.ToLastDestination;
                }
                else if (dist < range * 3)
                {   // Far behind, must catch up
                    // The range is nearly quadrupled here due to dogfighting conditions
                    direct.DriveDest = EDriveDest.ToLastDestination;
                    thisInst.FullBoost = true; // boost in forwards direction towards objective
                }
                else
                {   // SUPER Far behind, must catch up
                    direct.DriveDest = EDriveDest.ToLastDestination;
                    thisInst.Retreat = true;
                    thisInst.FullBoost = true; // boost in forwards direction towards objective
                }
            }
        }


        public static void Dogfighting(TankAIHelper thisInst, Tank tank)
        {   // Will have to account for the different types of flight methods available

            thisInst.AttackEnemy = false;
            thisInst.lastEnemy = tank.Vision.GetFirstVisibleTechIsEnemy(tank.Team);
            if (thisInst.lastEnemyGet != null)
            {
                Vector3 aimTo = (thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized;
                Vector3 foreDirect = tank.rootBlockTrans.InverseTransformDirection(aimTo);
                /*
                AIControllerAir pilot = (AIControllerAir) thisInst.MovementController;
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
