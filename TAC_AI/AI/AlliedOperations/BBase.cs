using System;
using UnityEngine;
using System.Reflection;
using TAC_AI.AI.Movement;

namespace TAC_AI.AI.AlliedOperations
{
    public static class BBase
    {
        /// <summary>
        /// Incomplete - artillery support mode
        /// </summary>
        /// <param name="thisInst"></param>
        /// <param name="tank"></param>
        public static void HoldSupport(AIECore.TankAIHelper thisInst, Tank tank)
        {
            //The Handler that tells the Tank (Base) what to do movement-wise
            thisInst.IsMultiTech = false;
            thisInst.Attempt3DNavi = true;
            thisInst.Retreat = true;    //Prevent the auto-driveaaaa

            BGeneral.ResetValues(thisInst);

            thisInst.SetDistanceFromTaskUnneeded();

            thisInst.PivotOnly = true;
            thisInst.SettleDown();
            if (thisInst.lastEnemy)
            {
                thisInst.DriveDest = EDriveDest.ToLastDestination;
                thisInst.Steer = true;
                thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
            }
            else
            {
                if (thisInst.ActionPause <= 0)
                {
                    thisInst.ActionPause = UnityEngine.Random.Range(50, 300);
                    thisInst.DriveDest = EDriveDest.None;
                    thisInst.Steer = false;
                    thisInst.lastDestination = tank.boundsCentreWorldNoCheck + new Vector3(UnityEngine.Random.Range(-50, 50), 0, UnityEngine.Random.Range(-50, 50));
                }
                else if (thisInst.ActionPause < 160)
                {
                    thisInst.DriveDest = EDriveDest.None;
                    thisInst.Steer = false;
                }
                else
                {
                    thisInst.DriveDest = EDriveDest.ToLastDestination;
                    thisInst.Steer = true;
                }
            }
        }
        public static void HoldProtect(AIECore.TankAIHelper thisInst, Tank tank)
        {
            //The Handler that tells the Tank (Base) what to do movement-wise
            thisInst.IsMultiTech = false;
            thisInst.Attempt3DNavi = true;
            thisInst.Retreat = true;    //Prevent the auto-driveaaaa

            BGeneral.ResetValues(thisInst);

            thisInst.SetDistanceFromTaskUnneeded();

            thisInst.PivotOnly = true;
            thisInst.SettleDown();
            if (thisInst.lastEnemy)
            {
                thisInst.DriveDest = EDriveDest.ToLastDestination;
                thisInst.Steer = true;
                thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
            }
            else
            {
                if (thisInst.ActionPause <= 0)
                {
                    thisInst.ActionPause = UnityEngine.Random.Range(50, 300);
                    thisInst.DriveDest = EDriveDest.None;
                    thisInst.Steer = false;
                    thisInst.lastDestination = tank.boundsCentreWorldNoCheck + new Vector3(UnityEngine.Random.Range(-50, 50), 0, UnityEngine.Random.Range(-50, 50));
                }
                else if (thisInst.ActionPause < 160)
                {
                    thisInst.DriveDest = EDriveDest.None;
                    thisInst.Steer = false;
                }
                else
                {
                    thisInst.DriveDest = EDriveDest.ToLastDestination;
                    thisInst.Steer = true;
                }
            }
        }
    }
}
