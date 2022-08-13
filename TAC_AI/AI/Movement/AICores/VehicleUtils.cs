using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TAC_AI.AI.Movement.AICores
{
    internal class VehicleUtils
    {
        private const float ignoreSteeringAboveAngle = 0.875f;
        private const float forwardsLowerSteeringAboveAngle = 0.7f;
        private const float MinLookAngleToTurnFineSideways = 0.65f;
        private const float MaxThrottleToTurnFull = 0.75f;
        private const float MaxThrottleToTurnAccurate = 0.5f;
        /// <summary>
        /// Controls how hard the Tech should turn when pursuing a target vector
        /// </summary>
        public static bool Turner(TankControl thisControl, AIECore.TankAIHelper helper, Vector3 destinationVec, out float turnVal)
        {
            turnVal = 1;
            float forwards = Vector2.Dot(destinationVec.ToVector2XZ().normalized, helper.tank.rootBlockTrans.forward.ToVector2XZ().normalized);

            if (forwards > ignoreSteeringAboveAngle && thisControl.DriveControl >= MaxThrottleToTurnFull)
                return false;
            else
            {
                if (helper.DriveDir == EDriveFacing.Perpendicular)
                {
                    if (!(bool)helper.lastCloseAlly)
                    {
                        float strength = 1 - forwards;
                        turnVal = Mathf.Clamp(strength, 0, 1);
                    }
                    else if (forwards > MinLookAngleToTurnFineSideways)
                    {
                        float strength = 1 - (forwards / 1.5f);
                        turnVal = Mathf.Clamp(strength, 0, 1);
                    }
                }
                else
                {
                    if (thisControl.DriveControl <= MaxThrottleToTurnAccurate)
                    {
                        if (!(bool)helper.lastCloseAlly && forwards > forwardsLowerSteeringAboveAngle)
                        {
                            float strength = 1 - Mathf.Log10(1 + (forwards * 9));
                            turnVal = Mathf.Clamp(strength, 0, 1);
                        }
                    }
                    else if (!(bool)helper.lastCloseAlly && forwards > forwardsLowerSteeringAboveAngle)
                    {
                        float strength = 1 - forwards;
                        turnVal = Mathf.Clamp(strength, 0, 1);
                    }
                }
                return true;
            }
        }

        private const float ignoreSteeringAboveAngleAir = 0.95f;
        private const float forwardsLowerSteeringAboveAngleAir = 0.5f;
        private const float MinLookAngleToTurnFineSidewaysAir = 0.65f;
        /// <summary>
        /// Controls how hard the Tech should turn when pursuing a target vector
        /// </summary>
        public static bool TurnerHovership(TankControl thisControl, AIECore.TankAIHelper helper, Vector3 destinationVec, out float turnVal)
        {
            turnVal = 1;
            float forwards = Vector2.Dot(destinationVec.ToVector2XZ().normalized, helper.tank.rootBlockTrans.forward.ToVector2XZ().normalized);

            if (forwards > ignoreSteeringAboveAngleAir)
                return false;
            else
            {
                if (helper.DriveDir == EDriveFacing.Perpendicular)
                {
                    if (!(bool)helper.lastCloseAlly)
                    {
                        float strength = 1 - forwards;
                        turnVal = Mathf.Clamp(strength, 0, 1);
                    }
                    else if (forwards > MinLookAngleToTurnFineSidewaysAir)
                    {
                        float strength = 1 - (forwards / 1.5f);
                        turnVal = Mathf.Clamp(strength, 0, 1);
                    }
                }
                else
                {
                    if (!(bool)helper.lastCloseAlly && forwards > forwardsLowerSteeringAboveAngleAir)
                    {
                        float strength = 1 - Mathf.Log10(1 + (forwards * 9));
                        turnVal = Mathf.Clamp(strength, 0, 1);
                    }
                }
                return true;
            }
        }
    }
}
