using UnityEngine;
using TAC_AI.AI.Movement;
using TAC_AI.AI.Movement.AICores;

namespace TAC_AI.AI.AlliedOperations
{
    internal static class BBase
    {
        /// <summary>
        /// Incomplete - artillery support mode
        /// </summary>
        /// <param name="helper"></param>
        /// <param name="tank"></param>
        public static void HoldSupport(TankAIHelper helper, Tank tank, ref EControlOperatorSet direct)
        {
            //The Handler that tells the Tank (Base) what to do movement-wise
            helper.IsMultiTech = false;
            helper.Attempt3DNavi = true;
            helper.ChaseThreat = false;    //Prevent the auto-driveaaaa

            BGeneral.ResetValues(helper, ref direct);

            helper.SetDistanceFromTaskUnneeded();

            helper.ThrottleState = AIThrottleState.PivotOnly;
            helper.SettleDown();
            if (helper.lastEnemyGet)
            {
                direct.DriveDest = EDriveDest.ToLastDestination;
                direct.DriveDir = EDriveFacing.Forwards;
                direct.SetLastDest(helper.lastEnemyGet.tank.boundsCentreWorldNoCheck);
            }
            else
            {
                if (helper.ActionPause <= 0)
                {
                    helper.actionPause = UnityEngine.Random.Range(50, 300);
                    direct.DriveDest = EDriveDest.None;
                    direct.DriveDir = EDriveFacing.Neutral;
                    direct.SetLastDest(tank.boundsCentreWorldNoCheck + new Vector3(UnityEngine.Random.Range(-50, 50), 0, UnityEngine.Random.Range(-50, 50)));
                }
                else if (helper.ActionPause < 160)
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
        public static void HoldProtect(TankAIHelper helper, Tank tank, ref EControlOperatorSet direct)
        {
            //The Handler that tells the Tank (Base) what to do movement-wise
            helper.IsMultiTech = false;
            helper.Attempt3DNavi = true;
            helper.ChaseThreat = false;    //Prevent the auto-driveaaaa

            BGeneral.ResetValues(helper, ref direct);

            helper.SetDistanceFromTaskUnneeded();

            helper.ThrottleState = AIThrottleState.PivotOnly;
            helper.SettleDown();
            if (helper.lastEnemyGet)
            {
                direct.DriveDest = EDriveDest.ToLastDestination;
                direct.DriveDir = EDriveFacing.Forwards;
                direct.SetLastDest(helper.lastEnemyGet.tank.boundsCentreWorldNoCheck);
            }
            else
            {
                if (helper.ActionPause <= 0)
                {
                    helper.actionPause = UnityEngine.Random.Range(50, 300);
                    direct.DriveDest = EDriveDest.None;
                    direct.DriveDir = EDriveFacing.Neutral;
                    direct.SetLastDest(tank.boundsCentreWorldNoCheck + new Vector3(UnityEngine.Random.Range(-50, 50), 0, UnityEngine.Random.Range(-50, 50)));
                }
                else if (helper.ActionPause < 160)
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
