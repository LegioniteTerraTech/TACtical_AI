using System;
using UnityEngine;
using System.Reflection;
using TAC_AI.AI.Movement;

namespace TAC_AI.AI.AlliedOperations
{
    public static class BBase
    {
        public static void HoldPosition(AIECore.TankAIHelper thisInst, Tank tank)
        {
            //The Handler that tells the Tank (Base) what to do movement-wise
            BGeneral.ResetValues(thisInst);


            thisInst.Attempt3DNavi = true;
            thisInst.Retreat = true;    //Prevent the auto-driveaaaa

            thisInst.lastRange = 96; //arbitrary

            thisInst.PivotOnly = true;
            if (thisInst.lastEnemy)
            {
                thisInst.DriveDest = EDriveDest.ToLastDestination;
                thisInst.Steer = true;
                thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
            }
            else
            {
                thisInst.DriveDest = EDriveDest.None;
                thisInst.Steer = false;
                thisInst.lastDestination = tank.boundsCentreWorldNoCheck;
            }
        }
    }
}
