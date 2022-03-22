﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TAC_AI.AI.Movement.AICores
{
    internal class VehicleUtils
    {
        //private const int ignoreTurning = 50;
        private const float ignoreTurning = 0.875f;
        private const float MinThrottleToTurnFull = 0.75f;
        public static bool Turner(TankControl thisControl, AIECore.TankAIHelper helper, Vector3 destinationVec, out float turnVal)
        {
            turnVal = 1;
            float forwards = Vector2.Dot(destinationVec.normalized.ToVector2XZ(), helper.tank.rootBlockTrans.forward.ToVector2XZ());

            if (forwards > ignoreTurning && thisControl.DriveControl >= MinThrottleToTurnFull)
                return false;
            if (helper.DriveDir == EDriveType.Perpendicular)
            {
                if (!(bool)helper.LastCloseAlly)
                {
                    float strength = 1 - forwards;
                    turnVal = Mathf.Clamp(strength, 0, 1);
                }
                else if (forwards > 0.65f)
                {
                    float strength = 1 - (forwards / 1.5f);
                    turnVal = Mathf.Clamp(strength, 0, 1);
                }
            }
            else
            {
                /*
                if (!(bool)helper.LastCloseAlly && forwards > 0.65f)
                {
                    float strength = 1 - forwards;
                    turnVal = Mathf.Clamp(strength, 0, 1);
                }*/
                if (!(bool)helper.LastCloseAlly && forwards > 0.7f)
                {
                    float strength = 1 - forwards;
                    turnVal = Mathf.Clamp(strength, 0, 1);
                }
            }
            return true;
        }
    }
}
