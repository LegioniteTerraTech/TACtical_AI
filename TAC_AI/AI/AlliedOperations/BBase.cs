using System;
using UnityEngine;
using System.Reflection;
using TAC_AI.AI.Movement;

namespace TAC_AI.AI.AlliedOperations
{
    internal static class BBase
    {
        /// <summary>
        /// Incomplete - artillery support mode
        /// </summary>
        /// <param name="thisInst"></param>
        /// <param name="tank"></param>
        public static void HoldSupport(TankAIHelper thisInst, Tank tank, ref EControlOperatorSet direct)
        {
            //The Handler that tells the Tank (Base) what to do movement-wise
            thisInst.IsMultiTech = false;
            thisInst.Attempt3DNavi = true;
            thisInst.ChaseThreat = false;    //Prevent the auto-driveaaaa

            BGeneral.ResetValues(thisInst, ref direct);

            thisInst.SetDistanceFromTaskUnneeded();

            thisInst.PivotOnly = true;
            thisInst.SettleDown();
            if (thisInst.lastEnemyGet)
            {
                direct.DriveDest = EDriveDest.ToLastDestination;
                direct.DriveDir = EDriveFacing.Forwards;
                direct.SetLastDest(thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck);
            }
            else
            {
                if (thisInst.ActionPause <= 0)
                {
                    thisInst.actionPause = UnityEngine.Random.Range(50, 300);
                    direct.DriveDest = EDriveDest.None;
                    direct.DriveDir = EDriveFacing.Neutral;
                    direct.SetLastDest(tank.boundsCentreWorldNoCheck + new Vector3(UnityEngine.Random.Range(-50, 50), 0, UnityEngine.Random.Range(-50, 50)));
                }
                else if (thisInst.ActionPause < 160)
                {
                    direct.DriveDest = EDriveDest.None;
                    direct.DriveDir = EDriveFacing.Neutral;
                }
                else
                {
                    direct.DriveDest = EDriveDest.ToLastDestination;
                    direct.DriveDir = EDriveFacing.Forwards;
                }
            }
        }
        public static void HoldProtect(TankAIHelper thisInst, Tank tank, ref EControlOperatorSet direct)
        {
            //The Handler that tells the Tank (Base) what to do movement-wise
            thisInst.IsMultiTech = false;
            thisInst.Attempt3DNavi = true;
            thisInst.ChaseThreat = false;    //Prevent the auto-driveaaaa

            BGeneral.ResetValues(thisInst, ref direct);

            thisInst.SetDistanceFromTaskUnneeded();

            thisInst.PivotOnly = true;
            thisInst.SettleDown();
            if (thisInst.lastEnemyGet)
            {
                direct.DriveDest = EDriveDest.ToLastDestination;
                direct.DriveDir = EDriveFacing.Forwards;
                direct.SetLastDest(thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck);
            }
            else
            {
                if (thisInst.ActionPause <= 0)
                {
                    thisInst.actionPause = UnityEngine.Random.Range(50, 300);
                    direct.DriveDest = EDriveDest.None;
                    direct.DriveDir = EDriveFacing.Neutral;
                    direct.SetLastDest(tank.boundsCentreWorldNoCheck + new Vector3(UnityEngine.Random.Range(-50, 50), 0, UnityEngine.Random.Range(-50, 50)));
                }
                else if (thisInst.ActionPause < 160)
                {
                    direct.DriveDest = EDriveDest.None;
                    direct.DriveDir = EDriveFacing.Neutral;
                }
                else
                {
                    direct.DriveDest = EDriveDest.ToLastDestination;
                    direct.DriveDir = EDriveFacing.Forwards;
                }
            }
        }
    }
}
