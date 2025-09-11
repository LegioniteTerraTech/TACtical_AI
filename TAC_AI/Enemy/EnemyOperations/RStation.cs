using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TerraTechETCUtil;
using TAC_AI.AI;
using TAC_AI.AI.Enemy;
using TAC_AI.AI.Movement;

namespace TAC_AI.AI.Enemy.EnemyOperations
{
    internal static class RStation
    {
        public static void AttackWham(TankAIHelper helper, Tank tank, EnemyMind mind, ref EControlOperatorSet direct)
        {
            //The Handler that tells the Tank (Escort) what to do movement-wise
            BGeneral.ResetValues(helper, ref direct);


            helper.Attempt3DNavi = true;
            helper.Retreat = true;    //Prevent the auto-driveaaaa

            float dist = helper.GetDistanceFromTask2D(mind.sceneStationaryPos);

            if (helper.lastEnemyGet == null)
            {
                // Bases cannot LollyGag
                //RGeneral.LollyGag(helper, tank, mind, ref direct, true);
                return;
            }

            if (dist > 6)
            {
                //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  HOLDING GROUND (or space)!!!");
                direct.DriveDest = EDriveDest.ToLastDestination;
                direct.DriveDir = EDriveFacing.Forwards;
                direct.SetLastDest(mind.sceneStationaryPos);
                if (Mathf.Abs(Vector3.Dot(mind.sceneStationaryPos - tank.boundsCentreWorldNoCheck, tank.rootBlockTrans.forward)) > 0.75f)
                {   //Move back because we have GONE TOO FAR BACKWARDS
                    helper.ThrottleState = AIThrottleState.ForceSpeed;
                    helper.DriveVar = 1;
                }
                else
                {   //Aim back
                    helper.ThrottleState = AIThrottleState.PivotOnly;
                }
            }
            else
            {
                direct.DriveDest = EDriveDest.ToLastDestination;
                direct.DriveDir = EDriveFacing.Forwards;
                helper.ThrottleState = AIThrottleState.PivotOnly;
                direct.SetLastDest(helper.lastEnemyGet.tank.boundsCentreWorldNoCheck);
            }
        }
    }
}
