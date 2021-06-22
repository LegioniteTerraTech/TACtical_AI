﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RandomAdditions.AI
{

    public static class AIEPathing
    {
        public static List<Tank> Allies
        {
            get
            {
                return AIECore.Allies;
            }
        }

        //Handles the ground steering
        /// <summary> Only handles the steering (land) - translational drive must be controled somewhere else </summary>
        /// <param name="tank"></param>
        /// <param name="target"></param>
        /// <param name="thisControl"></param>
        public static void DriveTarget(Tank tank, Vector3 target, TankControl thisControl)
        {
            Vector3 heading = (target - tank.boundsCentreWorld).SetY(0f);
            DriveHeading(tank, heading, thisControl);
        }
        public static void DriveHeading(Tank tank, Vector3 heading, TankControl thisControl)
        {
            Vector3 tankForw = tank.rootBlockTrans.forward;
            float num = Mathf.Acos(Vector3.Dot(heading.normalized, tankForw)) * 25;//57.29578f
            Vector3 turnAim = heading.normalized;
            turnAim.y = 0;
            if (Vector3.Dot(heading.normalized, tankForw) > 0.95f)// we are almost looking directly at target
            {
                thisControl.TurnControl = 0;
            }
            else if (Vector3.Dot(turnAim, tankForw) > 0f)
            {
                thisControl.TurnControl = num;
            }
            else
            {
                thisControl.TurnControl = -num;
            }
        }

        //3-axis steering is handled in AIEDrive


        // COLLISION AVOIDENCE
        public static Tank ClosestAlly(Vector3 tankPos, out float bestValue)
        {
            // Finds the closest ally and outputs their respective distance as well as their being
            bestValue = 500;
            int bestStep = 0;
            Tank closestTank = null;
            try
            {
                for (int stepper = 0; Allies.Count > stepper; stepper++)
                {
                    float temp = (Allies.ElementAt(stepper).rbody.position - tankPos).sqrMagnitude;
                    if (bestValue > temp && temp != 0)
                    {
                        bestValue = temp;
                        bestStep = stepper;
                    }
                }
                bestValue = (Allies.ElementAt(bestStep).rbody.position - tankPos).magnitude;
                closestTank = Allies.ElementAt(bestStep);
                //Debug.Log("TACtical_AI:ClosestAllyProcess " + closestTank.name);
            }
            catch (Exception e)
            {
                Debug.Log("TACtical_AI: Crash on ClosestAllyProcess " + e);
            }
            return closestTank;
        }

        public static Tank ClosestAllyPrecision(Vector3 tankPos, out float bestValue)
        {
            // Finds the closest ally and outputs their respective distance as well as their being
            //  For when the size matters of the object to dodge
            //  DEMANDS MORE PROCESSING THAN THE ABOVE
            bestValue = 500;
            int bestStep = 0;
            Tank closestTank = null;
            try
            {
                for (int stepper = 0; Allies.Count > stepper; stepper++)
                {
                    float temp = (Allies.ElementAt(stepper).rbody.position - tankPos).sqrMagnitude - AIECore.Extremes(Allies.ElementAt(stepper).blockBounds.extents);
                    if (bestValue > temp && temp != 0)
                    {
                        bestValue = temp;
                        bestStep = stepper;
                    }
                }
                bestValue = (Allies.ElementAt(bestStep).rbody.position - tankPos).magnitude;
                closestTank = Allies.ElementAt(bestStep);
                //Debug.Log("TACtical_AI: ClosestAllyProcess " + closestTank.name);
            }
            catch (Exception e)
            {
                Debug.Log("TACtical_AI: Crash on ClosestAllyPrecisionProcess " + e);
            }
            return closestTank;
        }


        public static Tank SecondClosestAlly(Vector3 tankPos, out Tank secondTank, out float bestValue, out float auxBestValue)
        {
            // Finds the two closest allies and outputs their respective distances as well as their beings
            bestValue = 500;
            auxBestValue = 500;
            int bestStep = 0;
            int auxStep = 0;
            Tank closestTank;
            try
            {
                for (int stepper = 0; Allies.Count > stepper; stepper++)
                {
                    float temp = (Allies.ElementAt(stepper).rbody.position - tankPos).sqrMagnitude;
                    if (bestValue > temp && temp != 0)
                    {
                        auxStep = bestStep;
                        bestStep = stepper;
                        auxBestValue = bestValue;
                        bestValue = temp;
                    }
                    else if (bestValue < temp && auxBestValue > temp && temp != 0)
                    {
                        auxStep = stepper;
                        auxBestValue = temp;
                    }
                }
                secondTank = Allies.ElementAt(auxStep);
                closestTank = Allies.ElementAt(bestStep);
                auxBestValue = (Allies.ElementAt(auxStep).rbody.position - tankPos).magnitude;
                bestValue = (Allies.ElementAt(bestStep).rbody.position - tankPos).magnitude;
                //Debug.Log("TACtical_AI: ClosestAllyProcess " + closestTank.name);
                return closestTank;
            }
            catch (Exception e)
            {
                Debug.Log("TACtical_AI: Crash on SecondClosestAllyProcess " + e);
            }
            Debug.Log("TACtical_AI: SecondClosestAlly - COULD NOT FETCH TANK");
            secondTank = null;
            return null;
        }

        public static Tank SecondClosestAllyPrecision(Vector3 tankPos, out Tank secondTank, out float bestValue, out float auxBestValue)
        {
            // Finds the two closest allies and outputs their respective distances as well as their beings
            //  For when the size matters of the object to dodge
            //  DEMANDS MORE PROCESSING THAN THE ABOVE
            bestValue = 500;
            auxBestValue = 500;
            int bestStep = 0;
            int auxStep = 0;
            Tank closestTank;
            try
            {
                for (int stepper = 0; Allies.Count > stepper; stepper++)
                {
                    float temp = (Allies.ElementAt(stepper).rbody.position - tankPos).sqrMagnitude - AIECore.Extremes(Allies.ElementAt(stepper).blockBounds.extents);
                    if (bestValue > temp && temp != 0)
                    {
                        auxStep = bestStep;
                        bestStep = stepper;
                        auxBestValue = bestValue;
                        bestValue = temp;
                    }
                    else if (bestValue < temp && auxBestValue > temp && temp != 0)
                    {
                        auxStep = stepper;
                        auxBestValue = temp;
                    }
                }
                /*
                if (auxBestValue == 500 && Allies.Count > 2)
                { //TRY AGAIN
                    for (int stepper = Allies.Count; 0 < stepper; stepper--)
                    {
                        float temp = (Allies.ElementAt(stepper).rbody.position - tankPos).sqrMagnitude - AIEnhancedCore.Extremes(Allies.ElementAt(stepper).blockBounds.extents);
                        if (bestValue > temp && temp != 0)
                        {
                            auxStep = bestStep;
                            bestStep = stepper;
                            auxBestValue = bestValue;
                            bestValue = temp;
                        }
                    }
                }
                if (auxBestValue == 500)
                    Debug.Log("TACtical_AI: SecondClosestAllyPrecisionProcess EPIC FAIL!");
                */
                secondTank = Allies.ElementAt(auxStep);
                closestTank = Allies.ElementAt(bestStep);
                auxBestValue = (Allies.ElementAt(auxStep).rbody.position - tankPos).magnitude;
                bestValue = (Allies.ElementAt(bestStep).rbody.position - tankPos).magnitude;
                //Debug.Log("TACtical_AI: ClosestAllyProcess " + closestTank.name);
                return closestTank;
            }
            catch (Exception e)
            {
                Debug.Log("TACtical_AI: Crash on SecondClosestAllyPrecisionProcess " + e);
            }
            Debug.Log("TACtical_AI: SecondClosestAllyPrecision - COULD NOT FETCH TANK");
            secondTank = null;
            return null;
        }

        public static Vector3 GetDriveApproxAir(Tank tankToCopy, AIECore.TankAIHelper AIHelp)
        {
            //Get the position in which to drive inherited from player controls
            //  NOTE THAT THIS ONLY SUPPORTS THE DISTANCE OF PLAYER TECH'S SIZE PLUS THE MT TECH!!!
            Tank tank = AIHelp.tank;
            Vector3 end;
            //first we get the 
            Vector3 centerThis = tank.boundsCentreWorld;
            Vector3 offsetTo = tankToCopy.trans.InverseTransformPoint(centerThis);

            FieldInfo controlGet = typeof(TankControl).GetField("m_ControlState", BindingFlags.NonPublic | BindingFlags.Instance);
            TankControl.ControlState controlOverload = (TankControl.ControlState)controlGet.GetValue(tankToCopy.control);

            // Grab a vector to-go to set how the other tech should react in accordance to the host
            Vector3 DAdjuster = controlOverload.m_State.m_InputMovement * 35;
            Vector3 RAdjuster = controlOverload.m_State.m_InputRotation * -60;
            //Debug.Log("TACtical_AI: AI " + tank.name + ": Host Steering " + controlOverload.m_State.m_InputRotation);
            Vector3 directed = Quaternion.Euler(RAdjuster.x, RAdjuster.y, RAdjuster.z) * offsetTo;
            Vector3 posToGo = directed + DAdjuster;

            //Run ETC copies
            TankControl.ControlState controlOverloadThis = (TankControl.ControlState)controlGet.GetValue(tank.control);
            controlOverloadThis.m_State.m_BoostJets = controlOverload.m_State.m_BoostJets; 
            controlOverloadThis.m_State.m_BoostProps = controlOverload.m_State.m_BoostProps;
            controlGet.SetValue(tank.control, controlOverloadThis);

            //Anchor handling
            if (AIHelp.AutoAnchor)
            {
                if (tankToCopy.IsAnchored && tank.Anchors.NumPossibleAnchors >= 1 && !AIHelp.DANGER)
                {
                    if (tank.Anchors.NumIsAnchored == 0 && AIHelp.anchorAttempts <= 3)//Half the escort's attempts
                    {
                        tank.TryToggleTechAnchor();
                        AIHelp.anchorAttempts++;
                    }
                }
                else if (!tankToCopy.IsAnchored && tank.Anchors.NumPossibleAnchors >= 1)
                {
                    AIHelp.anchorAttempts = 0;
                    if (tank.Anchors.NumIsAnchored > 0)
                    {
                        tank.TryToggleTechAnchor();
                        AIHelp.JustUnanchored = true;
                    }
                }
            }

            // Then we pack it all up nicely in the end
            end = tankToCopy.trans.TransformPoint(posToGo);
            //Debug.Log("TACtical_AI: AI " + tank.name + ": Drive Mimic " + (end - centerThis));
            return end;
        }
        public static Vector3 OffsetFromGround(Vector3 input, AIECore.TankAIHelper thisInst)
        {
            float final_y;
            Vector3 final = input;
            bool terrain = Singleton.Manager<ManWorld>.inst.GetTerrainHeight(input, out float height);
            if (terrain)
                final_y = height + thisInst.GroundOffsetHeight;
            else
                final_y = 50 + thisInst.GroundOffsetHeight;
            if (KickStart.isWaterModPresent)
            {
                if (WaterMod.QPatch.WaterHeight > height)
                    final_y = WaterMod.QPatch.WaterHeight + thisInst.GroundOffsetHeight;
            }
            if (input.y < final_y)
            {
                final.y = final_y;
            }
            return final;
        }
        public static Vector3 OffsetToSea(Vector3 input, AIECore.TankAIHelper thisInst)
        {
            Vector3 final = input;
            bool terrain = Singleton.Manager<ManWorld>.inst.GetTerrainHeight(input, out float height);
            if (terrain)
            {
                if (height > WaterMod.QPatch.WaterHeight - 20)// avoid high terrain pathing!
                    final = thisInst.lastDestination;
                else
                    final.y = WaterMod.QPatch.WaterHeight;
            }
            else
                final.y = WaterMod.QPatch.WaterHeight;
            return final;
        }

    }
}