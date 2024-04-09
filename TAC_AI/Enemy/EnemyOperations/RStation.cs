using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TAC_AI.AI.Enemy.EnemyOperations
{
    internal static class RStation
    {
        public static void AttackWham(TankAIHelper thisInst, Tank tank, EnemyMind mind, ref EControlOperatorSet direct)
        {
            //The Handler that tells the Tank (Escort) what to do movement-wise
            BGeneral.ResetValues(thisInst, ref direct);


            thisInst.Attempt3DNavi = true;
            thisInst.Retreat = true;    //Prevent the auto-driveaaaa

            float dist = thisInst.GetDistanceFromTask2D(mind.sceneStationaryPos);

            if (thisInst.lastEnemyGet == null)
            {
                // Bases cannot LollyGag
                //RGeneral.LollyGag(thisInst, tank, mind, ref direct, true);
                return;
            }
            RGeneral.Engadge(thisInst, tank, mind);

            if (dist > 6)
            {
                //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  HOLDING GROUND (or space)!!!");
                direct.DriveDest = EDriveDest.ToLastDestination;
                direct.DriveDir = EDriveFacing.Forwards;
                direct.SetLastDest(mind.sceneStationaryPos);
                if (Mathf.Abs(Vector3.Dot(mind.sceneStationaryPos - tank.boundsCentreWorldNoCheck, tank.rootBlockTrans.forward)) > 0.75f)
                {   //Move back because we have GONE TOO FAR BACKWARDS
                    thisInst.ForceSetDrive = true;
                    thisInst.DriveVar = 1;
                }
                else
                {   //Aim back
                    thisInst.PivotOnly = true;
                }
            }
            else
            {
                direct.DriveDest = EDriveDest.ToLastDestination;
                direct.DriveDir = EDriveFacing.Forwards;
                thisInst.PivotOnly = true;
                direct.SetLastDest(thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck);
            }
        }
    }
}
