using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace TAC_AI.AI.Movement
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

        public const float ShipDepth = -3;

        //The default steering handles the ground steering

        //3-axis steering is handled in AIEDrive

        // OBSTICLE AVOIDENCE
        public static List<Visible> ObstructionAwareness(Vector3 posWorld, AIECore.TankAIHelper thisInst)
        {
            List<Visible> ObstList = new List<Visible>();
            try
            {
                foreach (Visible vis in Singleton.Manager<ManVisible>.inst.VisiblesTouchingRadius(posWorld, thisInst.lastTechExtents + 12, new Bitfield<ObjectTypes>()))
                {
                    if (vis.resdisp.IsNotNull())
                    {
                        ObstList.Add(vis);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Log("TACtical_AI: Error on ObstructionAwareness");
                Debug.Log(e);
            }
            return ObstList;
        }
        public static Vector3 ObstOtherDir(Tank tank, AIECore.TankAIHelper thisInst, Visible vis)
        {
            //What actually does the avoidence
            Vector3 inputOffset = tank.transform.position - vis.centrePosition;
            float inputSpacing = vis.Radius + thisInst.lastTechExtents + AIECore.TankAIHelper.DodgeStrength;
            Vector3 Final = (inputOffset.normalized * inputSpacing) + tank.transform.position;
            return Final;
        }
        public static Vector3 ObstDodgeOffset(Tank tank, AIECore.TankAIHelper thisInst, Vector3 targetIn, out bool worked, bool useTwo = false)
        {
            worked = false;
            if (KickStart.AIDodgeCheapness >= 60 || thisInst.ProceedToMine || thisInst.ProceedToBase)   // are we desperate for performance or going to mine
                return Vector3.zero;    // don't bother with this
            Vector3 Offset = Vector3.zero;

            if (tank.rbody == null)
                return Vector3.zero; // no need, we are stationary

            List<Visible> ObstList = ObstructionAwareness(tank.boundsCentreWorldNoCheck + tank.rbody.velocity, thisInst);
            try
            {
                int bestStep = 0;
                int auxStep = 0;
                float bestValue = 500;
                float auxBestValue = 500;
                int steps = ObstList.Count;
                bool moreThan2 = false;
                if (steps <= 0)
                    return Vector3.zero;
                else if (steps > 1)
                    moreThan2 = true;
                for (int stepper = 0; steps > stepper; stepper++)
                {
                    float temp = Mathf.Clamp((ObstList.ElementAt(stepper).centrePosition - tank.boundsCentreWorldNoCheck).sqrMagnitude - ObstList.ElementAt(stepper).Radius, 0, 500);
                    if (bestValue > temp && temp != 0)
                    {
                        auxStep = bestStep;
                        bestStep = stepper;
                        auxBestValue = bestValue;
                        bestValue = temp;
                    }
                    else if (useTwo && bestValue < temp && auxBestValue > temp && temp != 0)
                    {
                        auxStep = stepper;
                        auxBestValue = temp;
                    }
                }
                thisInst.Yield = true;
                worked = true;
                if (useTwo && moreThan2)
                {
                    Offset = (ObstOtherDir(tank, thisInst, ObstList.ElementAt(bestStep)) + ObstOtherDir(tank, thisInst, ObstList.ElementAt(auxStep))) / 2;
                }
                else
                    Offset = ObstOtherDir(tank, thisInst, ObstList.ElementAt(bestStep));
            }
            catch (Exception e)
            {
                Debug.Log("TACtical_AI: Error on ObstDodgeOffset");
                Debug.Log(e);
            }
            return Offset;
        }


        // ALLY COLLISION AVOIDENCE
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
                    float temp = (Allies.ElementAt(stepper).boundsCentreWorldNoCheck - tankPos).sqrMagnitude;
                    if (bestValue > temp && temp != 0)
                    {
                        bestValue = temp;
                        bestStep = stepper;
                    }
                }
                bestValue = (Allies.ElementAt(bestStep).boundsCentreWorldNoCheck - tankPos).magnitude;
                closestTank = Allies.ElementAt(bestStep);
                //Debug.Log("TACtical_AI:ClosestAllyProcess " + closestTank.name);
            }
            catch //(Exception e)
            {
                //Debug.Log("TACtical_AI: Crash on ClosestAllyProcess " + e);
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
                    float temp = (Allies.ElementAt(stepper).boundsCentreWorldNoCheck - tankPos).sqrMagnitude - AIECore.Extremes(Allies.ElementAt(stepper).blockBounds.extents);
                    if (bestValue > temp && temp != 0)
                    {
                        bestValue = temp;
                        bestStep = stepper;
                    }
                }
                bestValue = (Allies.ElementAt(bestStep).boundsCentreWorldNoCheck - tankPos).magnitude;
                closestTank = Allies.ElementAt(bestStep);
                //Debug.Log("TACtical_AI: ClosestAllyProcess " + closestTank.name);
            }
            catch //(Exception e)
            {
                //Debug.Log("TACtical_AI: Crash on ClosestAllyPrecisionProcess " + e);
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
                    float temp = (Allies.ElementAt(stepper).boundsCentreWorldNoCheck - tankPos).sqrMagnitude;
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
                auxBestValue = (Allies.ElementAt(auxStep).boundsCentreWorldNoCheck - tankPos).magnitude;
                bestValue = (Allies.ElementAt(bestStep).boundsCentreWorldNoCheck - tankPos).magnitude;
                //Debug.Log("TACtical_AI: ClosestAllyProcess " + closestTank.name);
                return closestTank;
            }
            catch //(Exception e)
            {
                //Debug.Log("TACtical_AI: Crash on SecondClosestAllyProcess " + e);
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
                    float temp = (Allies.ElementAt(stepper).boundsCentreWorldNoCheck - tankPos).sqrMagnitude - AIECore.Extremes(Allies.ElementAt(stepper).blockBounds.extents);
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
                auxBestValue = (Allies.ElementAt(auxStep).boundsCentreWorldNoCheck - tankPos).magnitude;
                bestValue = (Allies.ElementAt(bestStep).boundsCentreWorldNoCheck - tankPos).magnitude;
                //Debug.Log("TACtical_AI: ClosestAllyProcess " + closestTank.name);
                return closestTank;
            }
            catch //(Exception e)
            {
                //Debug.Log("TACtical_AI: Crash on SecondClosestAllyPrecisionProcess " + e);
            }
            Debug.Log("TACtical_AI: SecondClosestAllyPrecision - COULD NOT FETCH TANK");
            secondTank = null;
            return null;
        }


        // Other navigation utilities
        public static Vector3 GetDriveApproxAir(Tank tankToCopy, AIECore.TankAIHelper AIHelp)
        {
            //Get the position in which to drive inherited from player controls
            //  NOTE THAT THIS ONLY SUPPORTS THE DISTANCE OF PLAYER TECH'S SIZE PLUS THE MT TECH!!!
            Tank tank = AIHelp.tank;
            Vector3 end;
            //first we get the 
            Vector3 centerThis = tank.boundsCentreWorldNoCheck;
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
        public static bool AboveHeightFromGround(Vector3 input, float groundOffset = 50)
        {
            float final_y;
            bool terrain = Singleton.Manager<ManWorld>.inst.GetTerrainHeight(input, out float height);
            if (terrain)
                final_y = height + groundOffset;
            else
                final_y = 50 + groundOffset;
            if (KickStart.isWaterModPresent)
            {
                if (KickStart.WaterHeight > height)
                    final_y = KickStart.WaterHeight + groundOffset;
            }
            return (input.y > final_y);
        }
        public static bool AboveTheSea(Vector3 input)
        {
            bool terrain = Singleton.Manager<ManWorld>.inst.GetTerrainHeight(input, out float height);
            if (terrain)
            {
                if (height < KickStart.WaterHeight)
                    return true;
            }
            else
                if (50 < KickStart.WaterHeight)
                    return true;
            return false;
        }


        /// <summary>
        /// For use with land AI
        /// </summary>
        /// <param name="input"></param>
        /// <param name="thisInst"></param>
        /// <param name="groundOffset"></param>
        /// <returns></returns>
        public static Vector3 OffsetFromGround(Vector3 input, AIECore.TankAIHelper thisInst, float groundOffset = 0)
        {
            float final_y;
            Vector3 final = input;
            bool terrain = Singleton.Manager<ManWorld>.inst.GetTerrainHeight(input, out float height);
            if (groundOffset == 0) groundOffset = thisInst.GroundOffsetHeight;
            if (terrain)
                final_y = height + groundOffset;
            else
                final_y = 50 + groundOffset;
            if (thisInst.AdviseAway)// && thisInst.lastEnemy.IsNull()
            {
                try
                {
                    //Still keep dist from ground
                    if (KickStart.isWaterModPresent)
                    {
                        if (KickStart.WaterHeight > height)
                            final_y = KickStart.WaterHeight + groundOffset;
                    }
                    if (input.y < final_y)
                    {
                        final.x = thisInst.tank.boundsCentreWorldNoCheck.x;
                        final.z = thisInst.tank.boundsCentreWorldNoCheck.z;
                        final.y = height;
                    }
                    else
                    {
                        final.y = thisInst.tank.boundsCentreWorldNoCheck.y;
                    }
                }
                catch { }
            }
            else
            {
                if (KickStart.isWaterModPresent)
                {
                    if (KickStart.WaterHeight > height)
                        final_y = KickStart.WaterHeight + groundOffset;
                }
                if (input.y < final_y)
                {
                    final.y = final_y;
                }
            }
            return final;
        }

        /// <summary>
        /// For use with Air AI
        /// </summary>
        /// <param name="input"></param>
        /// <param name="thisInst"></param>
        /// <param name="groundOffset"></param>
        /// <returns></returns>
        public static Vector3 OffsetFromGroundA(Vector3 input, AIECore.TankAIHelper thisInst, float groundOffset = 0)
        {
            float final_y;
            Vector3 final = input;
            bool terrain = Singleton.Manager<ManWorld>.inst.GetTerrainHeight(input, out float height);
            if (groundOffset == 0) groundOffset = thisInst.GroundOffsetHeight;
            if (terrain)
                final_y = height + groundOffset;
            else
                final_y = 50 + groundOffset;
            if (KickStart.isWaterModPresent)
            {
                if (KickStart.WaterHeight > height)
                    final_y = KickStart.WaterHeight + groundOffset;
            }
            if (input.y < final_y)
            {
                final.y = final_y;
            }
            return final;
        }
        public static Vector3 ForceOffsetFromGroundA(Vector3 input, AIECore.TankAIHelper thisInst, float groundOffset = 0)
        {
            float final_y;
            Vector3 final = input;
            bool terrain = Singleton.Manager<ManWorld>.inst.GetTerrainHeight(input, out float height);
            if (groundOffset == 0) groundOffset = thisInst.GroundOffsetHeight;
            if (terrain)
                final_y = height + groundOffset;
            else
                final_y = 50 + groundOffset;
            if (KickStart.isWaterModPresent)
            {
                if (KickStart.WaterHeight > height)
                    final_y = KickStart.WaterHeight + groundOffset;
            }
            final.y = final_y;
            return final;
        }
        public static Vector3 ForceOffsetFromGroundA(Vector3 input, float groundOffset = 35)
        {
            float final_y;
            Vector3 final = input;
            bool terrain = Singleton.Manager<ManWorld>.inst.GetTerrainHeight(input, out float height);
            if (terrain)
                final_y = height + groundOffset;
            else
                final_y = 50 + groundOffset;
            if (KickStart.isWaterModPresent)
            {
                if (KickStart.WaterHeight > height)
                    final_y = KickStart.WaterHeight + groundOffset;
            }
            final.y = final_y;
            return final;
        }
        public static Vector3 OffsetToSea(Vector3 input, Tank tank, AIECore.TankAIHelper thisInst)
        {
            Vector3 final = input;
            float heightTank;
            if (tank.rbody != null)
                Singleton.Manager<ManWorld>.inst.GetTerrainHeight(tank.rbody.velocity, out heightTank);
            else
                Singleton.Manager<ManWorld>.inst.GetTerrainHeight(tank.boundsCentreWorldNoCheck, out heightTank);
            bool terrain = Singleton.Manager<ManWorld>.inst.GetTerrainHeight(input, out float height);
            if (terrain)
            {
                /*
                float estHeight;
                if (Mathf.Abs(tank.transform.InverseTransformDirection(tank.rootBlockTrans.up).z) > 0.8f)
                    estHeight = tank.blockman.blockCentreBounds.extents.z;
                else if (Mathf.Abs(tank.transform.InverseTransformDirection(tank.rootBlockTrans.up).x) > 0.8f)
                    estHeight = tank.blockman.blockCentreBounds.extents.x;
                else
                    estHeight = tank.blockman.blockCentreBounds.extents.y;
                */
                if (thisInst.PendingHeightCheck)
                {
                    thisInst.GetLowestPointOnTech();
                    thisInst.PendingHeightCheck = false;
                }
                float operatingDepth = tank.boundsCentreWorldNoCheck.y + thisInst.LowestPointOnTech;
                if (height > operatingDepth || heightTank > operatingDepth)// avoid terrain pathing!
                {
                    // Iterate highest terrain spots to build up a bias to avoid
                    int stepxM = 5;
                    int stepzM = 5;
                    int vecCount = 0;
                    Vector3 posAll = Vector3.zero;
                    for (int stepz = 0; stepz < stepzM; stepz++)
                    {
                        for (int stepx = 0; stepx < stepxM; stepx++)
                        {
                            Vector3 wow = tank.boundsCentreWorldNoCheck;
                            wow.x += (stepx * 20) - 50;
                            wow.z += (stepz * 20) - 50;
                            if (!Singleton.Manager<ManWorld>.inst.GetTerrainHeight(wow, out float heightC))
                                continue;
                            if (heightC < heightTank)
                            {
                                posAll += wow;
                                vecCount++;
                            }
                        }
                    }
                    if (vecCount == 25)
                    {
                        //thisInst.Yield = true;
                        Debug.Log("TACtical_AI: Tech " + thisInst.tank.name + " is jammed on land!");
                        final = thisInst.tank.boundsCentreWorldNoCheck - ((input - thisInst.tank.boundsCentreWorldNoCheck).normalized * AIECore.TankAIHelper.DodgeStrength);
                    }
                    else if (vecCount > 0)
                    {
                        Debug.Log("TACtical_AI: Tech " + thisInst.tank.name + " is trying to avoid terrain");
                        final = thisInst.tank.boundsCentreWorldNoCheck + ((tank.boundsCentreWorldNoCheck - (posAll / vecCount)).normalized * AIECore.TankAIHelper.DodgeStrength);
                    }
                }
            }
            final.y = KickStart.WaterHeight;
            return final;
        }
        public static Vector3 ForceOffsetToSea(Vector3 input)
        {
            Vector3 final = input;
            final.y = KickStart.WaterHeight;

            return final;
        }
        public static Vector3 OffsetFromSea(Vector3 input, Tank tank, AIECore.TankAIHelper thisInst)
        {
            if (!KickStart.isWaterModPresent)
                return input;
            float heightTank;
            if (tank.rbody != null)
                Singleton.Manager<ManWorld>.inst.GetTerrainHeight(tank.rbody.velocity, out heightTank);
            else
                Singleton.Manager<ManWorld>.inst.GetTerrainHeight(tank.boundsCentreWorldNoCheck, out heightTank);
            Vector3 final = input;
            bool terrain = Singleton.Manager<ManWorld>.inst.GetTerrainHeight(input, out float height);
            if (terrain)
            {
                if (height < KickStart.WaterHeight || heightTank < KickStart.WaterHeight)// avoid sea pathing!
                {
                    // Iterate closest terrain spots
                    int stepxM = 3;
                    int stepzM = 3;
                    float highestHeight = KickStart.WaterHeight;
                    Vector3 posBest = Vector3.zero;
                    for (int stepz = 0; stepz < stepzM; stepz++)
                    {
                        for (int stepx = 0; stepx < stepxM; stepx++)
                        {
                            Vector3 wow = tank.boundsCentreWorldNoCheck;
                            wow.x -= 45;
                            wow.z -= 45;
                            wow.x += stepx * 30;
                            wow.z += stepz * 30;
                            if (!Singleton.Manager<ManWorld>.inst.GetTerrainHeight(wow, out float heightC))
                                continue;
                            if (heightC > highestHeight)
                            {
                                highestHeight = heightC;
                                posBest = wow;
                            }
                        }
                    }
                    if (highestHeight > KickStart.WaterHeight)
                    {
                        //Debug.Log("TACtical_AI: highest terrain  of depth " + highestHeight + " found at " + posBest);
                        final = posBest;
                    }
                    else
                        final = thisInst.tank.boundsCentreWorldNoCheck - ((input - thisInst.tank.boundsCentreWorldNoCheck).normalized * AIECore.TankAIHelper.DodgeStrength);
                }
                else
                    final.y = height;
            }
            else
                final.y = height;
            return final;
        }
    }
}
