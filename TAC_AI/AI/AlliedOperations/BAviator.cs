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
        public static void MotivateFly(TankAIHelper helper, Tank tank, ref EControlOperatorSet direct)
        {   // Will have to account for the different types of flight methods available
            helper.lastPlayer = helper.GetPlayerTech();
            helper.IsMultiTech = false;

            BGeneral.ResetValues(helper, ref direct);
            helper.AvoidStuff = true;

            if (helper.lastPlayer == null)
            {
                DebugTAC_AI.LogError(KickStart.ModID + ": AI " + tank.name + ":  MotivateFly could not get valid lastPlayer");
                return;
            }
            if (helper.lastPlayer == tank.visible)
            {   // WE ARE FOLLOWING OURSELVES, just hold position!
                direct.DriveDest = EDriveDest.None;
                return;
            }

            if (!(helper.MovementController is AIControllerAir))
            {
                DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  FIRED MotivateFly WITHOUT THE REQUIRED AIControllerAir MODULE!!!");
                return;
            }

            float thisExtents = helper.lastTechExtents;
            float dist = helper.GetDistanceFromTask(helper.lastPlayer.tank.boundsCentreWorldNoCheck, helper.lastPlayer.GetCheapBounds());
            float range = helper.MaxObjectiveRange + thisExtents;

            float playerExt = helper.lastPlayer.GetCheapBounds();

            if (tank.wheelGrounded)
            {
                if (!helper.IsTechMovingAbs(helper.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                    helper.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                else
                    helper.SettleDown();
            }
            float spacing = thisExtents + playerExt;
            if (helper.lastEnemyGet && !helper.Retreat && dist > range * 2)
            {
                float distCombat = helper.lastCombatRange;
                float spacingCombat = thisExtents + helper.lastEnemyGet.GetCheapBounds();
                direct.SetLastDest(helper.lastEnemyGet.tank.boundsCentreWorldNoCheck);
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
                    helper.FullBoost = true; // boost in forwards direction towards objective
                }
            }
            else
            {
                direct.SetLastDest(helper.lastPlayer.tank.boundsCentreWorldNoCheck);
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
                    helper.FullBoost = true; // boost in forwards direction towards objective
                }
                else
                {   // SUPER Far behind, must catch up
                    direct.DriveDest = EDriveDest.ToLastDestination;
                    helper.Retreat = true;
                    helper.FullBoost = true; // boost in forwards direction towards objective
                }
            }
        }


        public static void Dogfighting(TankAIHelper helper, Tank tank)
        {   // Will have to account for the different types of flight methods available

            helper.AttackEnemy = false;
            helper.TryRefreshEnemyAllied();
            if (helper.lastEnemyGet != null)
            {
                Vector3 aimTo = (helper.lastEnemyGet.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized;
                Vector3 foreDirect = tank.rootBlockTrans.InverseTransformDirection(aimTo);
                /*
                AIControllerAir pilot = (AIControllerAir) helper.MovementController;
                if (KickStart.isWeaponAimModPresent && helper.SideToThreat && (pilot.LargeAircraft || pilot.BankOnly))
                {   // AC-130 broadside attack
                    if ( Mathf.Abs(Vector3.Dot(tank.rootBlockTrans.right, aimTo)) > 0.25f || helper.Urgency >= 30)
                    {
                        helper.DANGER = true;
                        //helper.Urgency = 50;
                        helper.SettleDown();
                    }
                }
                else
                {  */ // Normal Dogfighting
                if (helper.SideToThreat)
                {   // Wide forwards attack
                    helper.Urgency += KickStart.AIClockPeriod / 5f;
                    if ((foreDirect.z > 0.15f && foreDirect.x > -0.5f && foreDirect.x < 0.5f) || helper.Urgency >= 30)
                    {
                        helper.AttackEnemy = true;
                        helper.SettleDown();
                    }
                }
                else
                {   // Normal Dogfighting
                    helper.Urgency += KickStart.AIClockPeriod / 5f;
                    if ((foreDirect.z > 0.15f && foreDirect.x > -0.35f && foreDirect.x < 0.35f) || helper.Urgency >= 30)
                    {
                        helper.AttackEnemy = true;
                        helper.SettleDown();
                    }
                }
                //}
            }
            else
            {
                helper.Urgency = 0;
                helper.AttackEnemy = false;
            }
        }
    }
}
