using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using TAC_AI.World;

namespace TAC_AI.AI.Movement
{
    public static class AIEPathing
    {
        public static HashSet<Tank> AllyList(Tank tank)
        {
            return TankAIManager.GetNonEnemyTanks(tank.Team);
        }

        public const float ShipDepth = -3;
        public const float DefaultExtraSpacing = 2;


        //The default steering handles the ground steering

        //3-axis steering is handled in AIEDrive

        // OBSTICLE AVOIDENCE
        private static List<Visible> ObstList = new List<Visible>();
        public static List<Visible> ObstructionAwareness(Vector3 posWorld, TankAIHelper thisInst, float radAdd = DefaultExtraSpacing, bool ignoreDestructable = false)
        {
            ObstList.Clear();
            try
            {
                if (ignoreDestructable)
                {
                    foreach (Visible vis in Singleton.Manager<ManVisible>.inst.VisiblesTouchingRadius(posWorld, thisInst.lastTechExtents + radAdd, AIGlobals.sceneryBitMask))
                    {
                        if (vis.resdisp.IsNotNull() && vis.isActive && vis.damageable.Invulnerable 
                            && AIECore.IndestructableScenery.Contains(vis.resdisp.GetSceneryType()))
                        {
                            ObstList.Add(vis);
                        }
                    }
                }
                else
                {
                    foreach (Visible vis in Singleton.Manager<ManVisible>.inst.VisiblesTouchingRadius(posWorld, thisInst.lastTechExtents + radAdd, AIGlobals.sceneryBitMask))
                    {
                        if (vis.resdisp.IsNotNull() && vis.isActive)
                        {
                            ObstList.Add(vis);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                DebugTAC_AI.Log("TACtical_AI: Error on ObstructionAwareness");
                DebugTAC_AI.Log(e);
            }
            return ObstList;
        }
        public static bool ObstructionAwarenessAny(Vector3 posWorld, TankAIHelper thisInst, float radius)
        {
            try
            {
                foreach (Visible vis in Singleton.Manager<ManVisible>.inst.VisiblesTouchingRadius(posWorld, radius, AIGlobals.sceneryBitMask))
                {
                    if (vis.resdisp.IsNotNull() && vis.isActive)
                    {
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                DebugTAC_AI.Log("TACtical_AI: Error on ObstructionAwarenessAny");
                DebugTAC_AI.Log(e);
            }
            return false;
        }
        public static Vector3 ObstOtherDir(Tank tank, TankAIHelper thisInst, Visible vis)
        {
            //What actually does the avoidence
            Vector3 inputOffset = tank.transform.position - vis.centrePosition;
            float inputSpacing = vis.Radius + thisInst.lastTechExtents + thisInst.DodgeStrength;
            Vector3 Final = (inputOffset.normalized * inputSpacing) + tank.transform.position;
            return Final;
        }
        public static Vector3 ObstDodgeOffset(Tank tank, TankAIHelper thisInst, bool DoDodge, out bool worked, bool useTwo = false, bool ignoreDestructable = false)
        {
            worked = false;
            if (!DoDodge || KickStart.AIDodgeCheapness >= 75 || thisInst.DriveDestDirected == EDriveDest.ToMine || thisInst.DriveDestDirected == EDriveDest.ToBase)   // are we desperate for performance or going to mine
                return Vector3.zero;    // don't bother with this
            Vector3 Offset = Vector3.zero;

            if (tank.rbody == null)
                return Vector3.zero; // no need, we are stationary

            List<Visible> ObstList = ObstructionAwareness(tank.boundsCentreWorldNoCheck + tank.rbody.velocity, thisInst, 2, ignoreDestructable);
            try
            {
                int bestStep = 0;
                int auxStep = 0;
                float bestValue = 1500;
                float auxBestValue = 1500;
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
                    if (ObstructionAwarenessSetPiece(tank.boundsCentreWorldNoCheck + tank.rbody.velocity, tank, thisInst, out Vector3 posMon))
                        Offset = (ObstOtherDir(tank, thisInst, ObstList.ElementAt(bestStep)) + ObstOtherDir(tank, thisInst, ObstList.ElementAt(auxStep)) + posMon) / 3;
                    else
                        Offset = (ObstOtherDir(tank, thisInst, ObstList.ElementAt(bestStep)) + ObstOtherDir(tank, thisInst, ObstList.ElementAt(auxStep))) / 2;
                }
                else
                {
                    if (ObstructionAwarenessSetPiece(tank.boundsCentreWorldNoCheck + tank.rbody.velocity, tank, thisInst, out Vector3 posMon))
                        Offset = (ObstOtherDir(tank, thisInst, ObstList.ElementAt(bestStep)) + posMon) / 2;
                    else
                        Offset = ObstOtherDir(tank, thisInst, ObstList.ElementAt(bestStep));
                }
            }
            catch (Exception e)
            {
                DebugTAC_AI.Log("TACtical_AI: Error on ObstDodgeOffset");
                DebugTAC_AI.Log(e);
            }
            return Offset;
        }

        /// <summary>
        /// For inverted output
        /// </summary>
        /// <param name="tank"></param>
        /// <param name="thisInst"></param>
        /// <param name="vis"></param>
        /// <returns></returns>
        public static Vector3 ObstDir(Tank tank, TankAIHelper thisInst, Visible vis)
        {
            //What actually does the avoidence
            Vector3 inputOffset = tank.transform.position - vis.centrePosition;
            float inputSpacing = vis.Radius + thisInst.lastTechExtents + thisInst.DodgeStrength;
            Vector3 Final = -(inputOffset.normalized * inputSpacing) + tank.transform.position;
            return Final;
        }
        public static Vector3 ObstDodgeOffsetInv(Tank tank, TankAIHelper thisInst, bool DoDodge, out bool worked, bool useTwo = false, bool useLargeObstAvoid = false, bool ignoreDestructable = false)
        {
            worked = false;
            if (!DoDodge || KickStart.AIDodgeCheapness >= 60 || thisInst.DriveDestDirected == EDriveDest.ToMine || thisInst.DriveDestDirected == EDriveDest.ToBase)   // are we desperate for performance or going to mine
                return Vector3.zero;    // don't bother with this
            Vector3 Offset = Vector3.zero;

            if (tank.rbody == null)
                return Vector3.zero; // no need, we are stationary

            List<Visible> ObstList = ObstructionAwareness(tank.boundsCentreWorldNoCheck + tank.rbody.velocity, thisInst, 2, ignoreDestructable);
            try
            {
                int bestStep = 0;
                int auxStep = 0;
                float bestValue = 1500;
                float auxBestValue = 1500;
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
                    if (useLargeObstAvoid && ObstructionAwarenessSetPiece(tank.boundsCentreWorldNoCheck + tank.rbody.velocity, tank, thisInst, out Vector3 posMon, true))
                        Offset = (ObstDir(tank, thisInst, ObstList.ElementAt(bestStep)) + ObstDir(tank, thisInst, ObstList.ElementAt(auxStep)) + posMon) / 3;
                    else
                        Offset = (ObstDir(tank, thisInst, ObstList.ElementAt(bestStep)) + ObstDir(tank, thisInst, ObstList.ElementAt(auxStep))) / 2;
                }
                else
                {
                    if (useLargeObstAvoid && ObstructionAwarenessSetPiece(tank.boundsCentreWorldNoCheck + tank.rbody.velocity, tank, thisInst, out Vector3 posMon, true))
                        Offset = (ObstDir(tank, thisInst, ObstList.ElementAt(bestStep)) + posMon) / 2;
                    else
                        Offset = ObstDir(tank, thisInst, ObstList.ElementAt(bestStep));
                }
            }
            catch (Exception e)
            {
                DebugTAC_AI.Log("TACtical_AI: Error on ObstDodgeOffset");
                DebugTAC_AI.Log(e);
            }
            return Offset;
        }

        /// <summary>
        /// also handles sceneryblockers
        /// </summary>
        /// <param name="posScene"></param>
        /// <param name="tank"></param>
        /// <param name="thisInst"></param>
        /// <param name="pos"></param>
        /// <param name="invert"></param>
        /// <returns></returns>
        public static bool ObstructionAwarenessSetPiece(Vector3 posScene, Tank tank, TankAIHelper thisInst, out Vector3 pos, bool invert = false)
        {
            pos = Vector3.zero;
            ManWorld world = Singleton.Manager<ManWorld>.inst;
            if (!thisInst.tank.IsAnchored && world.GetSetPiecePlacement().Count > 0)
            {
                List<ManWorld.SavedSetPiece> ObstList = world.GetSetPiecePlacement();
                float inRange = 1500;
                bool isInRange = false;
                int steps = ObstList.Count;
                for (int stepper = 0; steps > stepper; stepper++)
                {
                    float temp = Mathf.Clamp((ObstList.ElementAt(stepper).m_WorldPosition.ScenePosition - posScene).sqrMagnitude, 0, 500);
                    if (inRange > temp && temp != 0)
                    {
                        isInRange = true;
                        break;
                    }
                }
                if (!isInRange)
                {
                    if (world.CheckIfInsideSceneryBlocker(SceneryBlocker.BlockMode.Spawn, posScene, thisInst.lastTechExtents + 12))
                    {
                        if (world.LandmarkSpawner.GetNearestBlocker(posScene, out Vector3 landmarkWorld))
                        {
                            pos = landmarkWorld;
                            return true;
                        }
                    }
                    return false;
                }
                try
                {
                    LayerMask monuments = Globals.inst.layerLandmark.mask;
                    Ray ray = new Ray(posScene, thisInst.tank.rootBlockTrans.forward);
                    Physics.Raycast(ray, out RaycastHit hitInfo, world.TileSize, monuments);
                    if ((bool)hitInfo.collider)
                    {
                        if (hitInfo.collider.GetComponent<TerrainSetPiece>())
                        {
                            TerrainSetPiece piece = hitInfo.collider.GetComponent<TerrainSetPiece>();
                            if (invert)
                            {
                                pos = ObstDirSetPiece(tank, thisInst, posScene, piece);
                            }
                            else
                            {
                                pos = ObstOtherDirSetPiece(tank, thisInst, posScene, piece);
                            }
                            return true;
                        }
                    }
                }
                catch (Exception e)
                {
                    DebugTAC_AI.Log("TACtical_AI: Error on ObstructionAwarenessMonument");
                    DebugTAC_AI.Log(e);
                }
            }
            return false;
        }
        public static bool ObstructionAwarenessSetPieceAny(Vector3 posScene, TankAIHelper thisInst, float radius)
        {
            if (!thisInst.tank.IsAnchored && ManWorld.inst.GetSetPiecePlacement().Count > 0)
            {
                if (ManWorld.inst.CheckIfInsideSceneryBlocker(SceneryBlocker.BlockMode.Spawn, posScene, radius))
                {
                    return true;
                }
            }
            return false;
        }
        public static bool ObstructionAwarenessTerrain(Vector3 posScene, TankAIHelper thisInst, float radius)
        {
            if (!thisInst.tank.IsAnchored)
            {
                float height = AIEPathMapper.GetHighestAltInRadius(posScene, radius, false);
                if (height > posScene.y - radius)
                    return true;
            }
            return false;
        }
        public static Vector3 ObstOtherDirSetPiece(Tank tank, TankAIHelper thisInst, Vector3 pos, TerrainSetPiece vis)
        {   //What actually does the avoidence
            Vector3 inputOffset = tank.transform.position - pos;
            float inputSpacing = vis.GetApproxCellRadius() + thisInst.lastTechExtents + thisInst.DodgeStrength;
            Vector3 Final = (inputOffset.normalized * inputSpacing) + tank.transform.position;
            return Final;
        }
        public static Vector3 ObstDirSetPiece(Tank tank, TankAIHelper thisInst, Vector3 pos, TerrainSetPiece vis)
        {   //What actually does the avoidence
            Vector3 inputOffset = tank.transform.position - pos;
            float inputSpacing = vis.GetApproxCellRadius() + thisInst.lastTechExtents + thisInst.DodgeStrength;
            Vector3 Final = -(inputOffset.normalized * inputSpacing) + tank.transform.position;
            return Final;
        }





        // ALLY COLLISION AVOIDENCE
        private static bool InvalidTank(Tank tank)
        {
            return tank == null || !tank.visible.isActive;
        }
        public static Tank ClosestAlly(HashSet<Tank> AlliesAlt, Vector3 tankPos, out float bestValue, Tank thisTank)
        {
            // Finds the closest ally and outputs their respective distance as well as their being
            bestValue = 500;
            int bestStep = 0;
            Tank closestTank = null;
            try
            {
                for (int stepper = 0; AlliesAlt.Count > stepper; stepper++)
                {
                    var otherTech = AlliesAlt.ElementAt(stepper);
                    if (InvalidTank(otherTech) || thisTank == otherTech)
                        continue;
                    float temp = (otherTech.boundsCentreWorldNoCheck - tankPos).sqrMagnitude;
                    if (bestValue > temp)
                    {
                        bestValue = temp;
                        bestStep = stepper;
                    }
                }
                bestValue = (AlliesAlt.ElementAt(bestStep).boundsCentreWorldNoCheck - tankPos).magnitude;
                closestTank = AlliesAlt.ElementAt(bestStep);
                //DebugTAC_AI.Log("TACtical_AI:ClosestAllyProcess " + closestTank.name);
            }
            catch //(Exception e)
            {
                //DebugTAC_AI.Log("TACtical_AI: Crash on ClosestAllyProcess " + e);
            }
            return closestTank;
        }
        public static Tank ClosestAllyPrecision(HashSet<Tank> AlliesAlt, Vector3 tankPos, out float bestValue, Tank thisTank)
        {
            // Finds the closest ally and outputs their respective distance as well as their being
            //  For when the size matters of the object to dodge
            //  DEMANDS MORE PROCESSING THAN THE ABOVE
            bestValue = 500;
            int bestStep = 0;
            Tank closestTank = null;
            try
            {
                for (int stepper = 0; AlliesAlt.Count > stepper; stepper++)
                {
                    var otherTech = AlliesAlt.ElementAt(stepper);
                    if (InvalidTank(otherTech) || thisTank == otherTech)
                        continue;
                    float temp = (otherTech.boundsCentreWorldNoCheck - tankPos).sqrMagnitude - otherTech.GetCheapBounds();
                    if (bestValue > temp)
                    {
                        bestValue = temp;
                        bestStep = stepper;
                    }
                }
                bestValue = (AlliesAlt.ElementAt(bestStep).boundsCentreWorldNoCheck - tankPos).magnitude;
                closestTank = AlliesAlt.ElementAt(bestStep);
                //DebugTAC_AI.Log("TACtical_AI: ClosestAllyProcess " + closestTank.name);
            }
            catch //(Exception e)
            {
                //DebugTAC_AI.Log("TACtical_AI: Crash on ClosestAllyPrecisionProcess " + e);
            }
            return closestTank;
        }

        public static Tank SecondClosestAlly(HashSet<Tank> AlliesAlt, Vector3 tankPos, out Tank secondTank, out float bestValue, out float auxBestValue, Tank thisTank)
        {
            // Finds the two closest allies and outputs their respective distances as well as their beings
            bestValue = 500;
            auxBestValue = 500;
            int bestStep = 0;
            int auxStep = 0;
            Tank closestTank;
            try
            {
                for (int stepper = 0; AlliesAlt.Count > stepper; stepper++)
                {
                    var otherTech = AlliesAlt.ElementAt(stepper);
                    if (InvalidTank(otherTech) || thisTank == otherTech)
                        continue;
                    float temp = (otherTech.boundsCentreWorldNoCheck - tankPos).sqrMagnitude;
                    if (bestValue > temp)
                    {
                        auxStep = bestStep;
                        bestStep = stepper;
                        auxBestValue = bestValue;
                        bestValue = temp;
                    }
                    else if (bestValue < temp && auxBestValue > temp)
                    {
                        auxStep = stepper;
                        auxBestValue = temp;
                    }
                }
                secondTank = AlliesAlt.ElementAt(auxStep);
                closestTank = AlliesAlt.ElementAt(bestStep);
                auxBestValue = (AlliesAlt.ElementAt(auxStep).boundsCentreWorldNoCheck - tankPos).magnitude;
                bestValue = (AlliesAlt.ElementAt(bestStep).boundsCentreWorldNoCheck - tankPos).magnitude;
                //DebugTAC_AI.Log("TACtical_AI: ClosestAllyProcess " + closestTank.name);
                return closestTank;
            }
            catch //(Exception e)
            {
                //DebugTAC_AI.Log("TACtical_AI: Crash on SecondClosestAllyProcess " + e);
            }
            DebugTAC_AI.Log("TACtical_AI: SecondClosestAlly - COULD NOT FETCH TANK");
            secondTank = null;
            return null;
        }
        public static Tank SecondClosestAllyPrecision(HashSet<Tank> AlliesAlt, Vector3 tankPos, out Tank secondTank, out float bestValue, out float auxBestValue, Tank thisTank)
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
                for (int stepper = 0; AlliesAlt.Count > stepper; stepper++)
                {
                    var otherTech = AlliesAlt.ElementAt(stepper);
                    if (InvalidTank(otherTech) || thisTank == otherTech)
                        continue;
                    float temp = (otherTech.boundsCentreWorldNoCheck - tankPos).sqrMagnitude - otherTech.GetCheapBounds();
                    if (bestValue > temp)
                    {
                        auxStep = bestStep;
                        bestStep = stepper;
                        auxBestValue = bestValue;
                        bestValue = temp;
                    }
                    else if (bestValue < temp && auxBestValue > temp)
                    {
                        auxStep = stepper;
                        auxBestValue = temp;
                    }
                }
                secondTank = AlliesAlt.ElementAt(auxStep);
                closestTank = AlliesAlt.ElementAt(bestStep);
                auxBestValue = (AlliesAlt.ElementAt(auxStep).boundsCentreWorldNoCheck - tankPos).magnitude;
                bestValue = (AlliesAlt.ElementAt(bestStep).boundsCentreWorldNoCheck - tankPos).magnitude;
                //DebugTAC_AI.Log("TACtical_AI: ClosestAllyProcess " + closestTank.name);
                return closestTank;
            }
            catch //(Exception e)
            {
                //DebugTAC_AI.Log("TACtical_AI: Crash on SecondClosestAllyPrecisionProcess " + e);
            }
            DebugTAC_AI.Log("TACtical_AI: SecondClosestAllyPrecision - COULD NOT FETCH TANK");
            secondTank = null;
            return null;
        }
        
        public static Tank ClosestUnanchoredAlly(HashSet<Tank> AlliesAlt, Vector3 tankPos, float rangeSqr, out float bestValue, Tank thisTank)
        {
            // Finds the closest ally and outputs their respective distance as well as their being
            bestValue = rangeSqr;
            int bestStep = -1;
            Tank closestTank = null;
            try
            {
                for (int stepper = 0; AlliesAlt.Count > stepper; stepper++)
                {
                    var otherTech = AlliesAlt.ElementAt(stepper);
                    if (InvalidTank(otherTech) || thisTank == otherTech || otherTech.IsAnchored)
                        continue;
                    float temp = (otherTech.boundsCentreWorldNoCheck - tankPos).sqrMagnitude;
                    if (bestValue > temp)
                    {
                        bestValue = temp;
                        bestStep = stepper;
                    }
                }
                if (bestStep == -1)
                    return null;
                bestValue = (AlliesAlt.ElementAt(bestStep).boundsCentreWorldNoCheck - tankPos).magnitude;
                closestTank = AlliesAlt.ElementAt(bestStep);
                //DebugTAC_AI.Log("TACtical_AI:ClosestAllyProcess " + closestTank.name);
            }
            catch //(Exception e)
            {
                //DebugTAC_AI.Log("TACtical_AI: Crash on ClosestAllyProcess " + e);
            }
            return closestTank;
        }


        // Other navigation utilities
        public static Vector3 GetDriveApproxAirDirector(Tank tankToCopy, TankAIHelper AIHelp, out bool IsMoving)
        {
            //Get the position in which to drive inherited from player controls
            //  NOTE THAT THIS ONLY SUPPORTS THE DISTANCE OF PLAYER TECH'S SIZE PLUS THE MT TECH!!!
            Tank tank = AIHelp.tank;
            Vector3 end;
            //first we get the offset
            Vector3 offsetTo = tankToCopy.trans.InverseTransformPoint(tank.boundsCentreWorldNoCheck) - tankToCopy.blockBounds.center;

            TankControl.State controlCopyTarget = tankToCopy.control.CurState;


            Vector3 InputLineVal = controlCopyTarget.m_InputMovement;
            // Copy LME Here
            if (tankToCopy.control.GetThrottle(0, out float throttleX))
            {   // X 
                InputLineVal.x += throttleX;
            }
            if (tankToCopy.control.GetThrottle(1, out float throttleY))
            {   // Y
                InputLineVal.y += throttleY;
            }
            if (tankToCopy.control.GetThrottle(2, out float throttleZ))
            {   // X
                InputLineVal.z += throttleZ;
            }
            InputLineVal = InputLineVal.Clamp01Box();

            // Grab a vector to-go to set how the other tech should react in accordance to the host
            Vector3 DAdjuster = InputLineVal * 2000;
            Vector3 RAdjuster = controlCopyTarget.m_InputRotation * -1;
            //DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ": Host Steering " + controlOverload.m_State.m_InputRotation);
            // Generate a rough tangent
            Vector3 MoveDirectionUnthrottled = ((Quaternion.Euler(RAdjuster.x, RAdjuster.y, RAdjuster.z) * offsetTo) - offsetTo).normalized * (1000 * AIHelp.lastTechExtents);

            Vector3 posToGo = MoveDirectionUnthrottled + DAdjuster;


            //Anchor handling
            if (AIHelp.AutoAnchor)
            {
                if (tankToCopy.IsAnchored && tank.Anchors.NumPossibleAnchors >= 1 && !AIHelp.AttackEnemy)
                {
                    if (tank.Anchors.NumIsAnchored == 0 && AIHelp.anchorAttempts <= AIGlobals.AlliedAnchorAttempts / 2)//Half the escort's attempts
                    {
                        AIHelp.TryAnchor();
                        AIHelp.anchorAttempts++;
                    }
                }
                else if (!tankToCopy.IsAnchored && tank.Anchors.NumPossibleAnchors >= 1)
                {
                    AIHelp.anchorAttempts = 0;
                    if (tank.Anchors.NumIsAnchored > 0)
                    {
                        AIHelp.UnAnchor();
                    }
                }
            }

            // Then we pack it all up nicely in the end
            end = tankToCopy.trans.TransformPoint(posToGo + tankToCopy.blockBounds.center);
            //DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ": Drive Mimic " + (end - centerThis));
            IsMoving = !(InputLineVal + controlCopyTarget.m_InputRotation).Approximately(Vector3.zero, 0.05f);
            return end;
        }
        /// <summary>
        /// Needs to be setup like a Maintainer
        /// </summary>
        /// <param name="tankToCopy"></param>
        /// <param name="AIHelp"></param>
        /// <param name="IsMoving"></param>
        /// <returns></returns>
        public static Vector3 GetDriveApproxAirMaintainer(Tank tankToCopy, TankAIHelper AIHelp, out bool IsMoving)
        {
            //Get the position in which to drive inherited from player controls
            //  NOTE THAT THIS ONLY SUPPORTS THE DISTANCE OF PLAYER TECH'S SIZE PLUS THE MT TECH!!!
            Tank tank = AIHelp.tank;
            Vector3 end;
            //first we get the offset
            Vector3 offsetTo = tankToCopy.trans.InverseTransformPoint(tank.boundsCentreWorldNoCheck) - tankToCopy.blockBounds.center;

            TankControl.State controlCopyTarget = tankToCopy.control.CurState;


            Vector3 InputLineVal = controlCopyTarget.m_InputMovement;
            // Copy LME Here
            if (tankToCopy.control.GetThrottle(0, out float throttleX))
            {   // X 
                InputLineVal.x += throttleX;
            }
            if (tankToCopy.control.GetThrottle(1, out float throttleY))
            {   // Y
                InputLineVal.y += throttleY;
            }
            if (tankToCopy.control.GetThrottle(2, out float throttleZ))
            {   // X
                InputLineVal.z += throttleZ;
            }
            InputLineVal = InputLineVal.Clamp01Box();

            // Grab a vector to-go to set how the other tech should react in accordance to the host
            Vector3 DAdjuster = InputLineVal * 2000;
            Vector3 RAdjuster = controlCopyTarget.m_InputRotation * -1;
            //DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ": Host Steering " + controlOverload.m_State.m_InputRotation);
            // Generate a rough tangent
            Vector3 MoveDirectionUnthrottled = ((Quaternion.Euler(RAdjuster.x, RAdjuster.y, RAdjuster.z) * offsetTo) - offsetTo).normalized * (1000 * AIHelp.lastTechExtents);

            Vector3 posToGo = MoveDirectionUnthrottled + DAdjuster;

            //Run ETC copies
            tank.control.CollectMovementInput(Vector3.zero, Vector3.zero, Vector3.zero, 
                controlCopyTarget.m_BoostProps, controlCopyTarget.m_BoostJets);

            //Anchor handling
            if (AIHelp.AutoAnchor)
            {
                if (tankToCopy.IsAnchored && tank.Anchors.NumPossibleAnchors >= 1 && !AIHelp.AttackEnemy)
                {
                    if (tank.Anchors.NumIsAnchored == 0 && AIHelp.anchorAttempts <= AIGlobals.AlliedAnchorAttempts / 2)//Half the escort's attempts
                    {
                        AIHelp.TryAnchor();
                        AIHelp.anchorAttempts++;
                    }
                }
                else if (!tankToCopy.IsAnchored && tank.Anchors.NumPossibleAnchors >= 1)
                {
                    AIHelp.anchorAttempts = 0;
                    if (tank.Anchors.NumIsAnchored > 0)
                    {
                        AIHelp.UnAnchor();
                    }
                }
            }

            // Then we pack it all up nicely in the end
            end = tankToCopy.trans.TransformPoint(posToGo + tankToCopy.blockBounds.center);
            //DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ": Drive Mimic " + (end - centerThis));
            IsMoving = !(InputLineVal + controlCopyTarget.m_InputRotation).Approximately(Vector3.zero, 0.05f);
            return end;
        }
        public static bool AboveHeightFromGround(Vector3 posScene, float groundOffset = 50)
        {
            float final_y;
            bool terrain = AIEPathMapper.GetAltitudeLoadedOnly(posScene, out float height);
            if (terrain)
                final_y = height + groundOffset;
            else
                final_y = 50 + groundOffset;
            if (KickStart.isWaterModPresent)
            {
                if (KickStart.WaterHeight > height)
                    final_y = KickStart.WaterHeight + groundOffset;
            }
            return (posScene.y > final_y);
        }
        public static bool AboveHeightFromGroundRadius(Vector3 posScene, float radius, float groundOffset = 50)
        {
            float final_y;
            bool terrain = AIEPathMapper.GetHighestAltInRadiusLoadedOnly(posScene, radius, out float height, false);
            if (terrain)
                final_y = height + groundOffset;
            else
                final_y = 50 + groundOffset;
            if (KickStart.isWaterModPresent)
            {
                if (KickStart.WaterHeight > height)
                    final_y = KickStart.WaterHeight + groundOffset;
            }
            return (posScene.y > final_y);
        }
        public static bool AboveHeightFromGroundTech(TankAIHelper helper, float groundOffset = 50)
        {
            float final_y;
            float height = helper.GetFrameHeight();
            final_y = height + groundOffset;
            if (KickStart.isWaterModPresent)
            {
                if (KickStart.WaterHeight > height)
                    final_y = KickStart.WaterHeight + groundOffset;
            }
            return (helper.tank.boundsCentreWorldNoCheck.y > final_y);
        }
        public static bool AboveTheSea(Vector3 posScene)
        {
            if (!KickStart.isWaterModPresent)
                return false;
            bool terrain = AIEPathMapper.GetAltitudeLoadedOnly(posScene, out float height);
            if (terrain)
            {
                if (height < KickStart.WaterHeight)
                    return true;
            }
            else if (50 < KickStart.WaterHeight)
                    return true;
            return false;
        }
        public static bool AboveTheSea(TankAIHelper helper)
        {
            return helper.GetFrameHeight() > KickStart.WaterHeight;
        }


        /// <summary>
        /// For use with land AI
        /// </summary>
        /// <param name="input"></param>
        /// <param name="thisInst"></param>
        /// <param name="groundOffset"></param>
        /// <returns></returns>
        public static Vector3 OffsetFromGround(Vector3 input, TankAIHelper thisInst, float groundOffset = 0)
        {
            float final_y;
            Vector3 final = input;
            bool terrain = AIEPathMapper.GetAltitudeLoadedOnly(input, out float height);
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

        /// <summary>
        /// For use with hover AI
        /// </summary>
        /// <param name="input"></param>
        /// <param name="thisInst"></param>
        /// <param name="groundOffset"></param>
        /// <returns></returns>
        public static Vector3 OffsetFromGroundH(Vector3 input, TankAIHelper thisInst, float groundOffset = 0)
        {
            float final_y;
            Vector3 final = input;
            bool terrain = AIEPathMapper.GetAltitudeLoadedOnly(input, out float height);
            if (groundOffset == 0) groundOffset = thisInst.GroundOffsetHeight;
            if (terrain)
                final_y = height + groundOffset;
            else
                final_y = 50 + groundOffset;
            if (thisInst.AdviseAwayCore)// && thisInst.lastEnemy.IsNull()
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
        /// For use with Aircraft AI
        /// </summary>
        /// <param name="input"></param>
        /// <param name="thisInst"></param>
        /// <param name="groundOffset"></param>
        /// <returns></returns>
        public static Vector3 OffsetFromGroundA(Vector3 input, TankAIHelper thisInst, float groundOffset = 0)
        {
            float final_y;
            Vector3 final = input;
            bool terrain = AIEPathMapper.GetAltitudeLoadedOnly(input, out float height);
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
        public static Vector3 SnapOffsetFromGroundA(Vector3 input, TankAIHelper thisInst, float groundOffset = 0)
        {
            float final_y;
            Vector3 final = input;
            bool terrain = AIEPathMapper.GetAltitudeLoadedOnly(input, out float height);
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
        public static Vector3 SnapOffsetFromGroundA(Vector3 input, float groundOffset = 35)
        {
            float final_y;
            Vector3 final = input;
            bool terrain = AIEPathMapper.GetAltitudeLoadedOnly(input, out float height);
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
        public static Vector3 OffsetFromGroundAAlt(Vector3 input, float groundOffset = 35)
        {
            float final_y;
            Vector3 final = input;
            bool terrain = AIEPathMapper.GetAltitudeLoadedOnly(input, out float height);
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
                final.y = final_y;
            return final;
        }

        // Sea
        public static Vector3 OffsetToSea(Vector3 input, Tank tank, TankAIHelper thisInst)
        {
            Vector3 final = input;
            float heightTank;
            if (tank.rbody != null)
                AIEPathMapper.GetAltitudeLoadedOnly(tank.boundsCentreWorldNoCheck + tank.rbody.velocity.Clamp(-75 * Vector3.one, 75 * Vector3.one), out heightTank);
            else
                AIEPathMapper.GetAltitudeLoadedOnly(tank.boundsCentreWorldNoCheck, out heightTank);
            bool terrain = AIEPathMapper.GetAltitudeLoadedOnly(input, out float height);
            if (terrain)
            {
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
                            if (!AIEPathMapper.GetAltitudeLoadedOnly(wow, out float heightC))
                                continue;
                            if (heightC < heightTank)
                            {
                                posAll += wow;
                                vecCount++;
                                thisInst.Yield = true;
                            }
                        }
                    }
                    if (vecCount == 25)
                    {
                        //thisInst.Yield = true;
                        //DebugTAC_AI.Log("TACtical_AI: Tech " + thisInst.tank.name + " is jammed on land!");
                        if (thisInst.AdviseAwayCore)
                        { // Reverse
                            final = thisInst.tank.boundsCentreWorldNoCheck + ((input - thisInst.tank.boundsCentreWorldNoCheck).normalized * thisInst.DodgeStrength);
                        }
                        else
                            final = thisInst.tank.boundsCentreWorldNoCheck - ((input - thisInst.tank.boundsCentreWorldNoCheck).normalized * thisInst.DodgeStrength);
                    }
                    else if (vecCount > 0)
                    {
                        //DebugTAC_AI.Log("TACtical_AI: Tech " + thisInst.tank.name + " is trying to avoid terrain");
                        if (thisInst.AdviseAwayCore)
                        { // Reverse
                            final = thisInst.tank.boundsCentreWorldNoCheck - ((tank.boundsCentreWorldNoCheck - (posAll / vecCount)).normalized * thisInst.DodgeStrength);
                        }
                        else
                            final = thisInst.tank.boundsCentreWorldNoCheck + ((tank.boundsCentreWorldNoCheck - (posAll / vecCount)).normalized * thisInst.DodgeStrength);
                    }
                }
            }
            final.y = KickStart.WaterHeight;
            return final;
        }
        public static Vector3 SnapOffsetToSea(Vector3 input)
        {   // Lowest ground or sea
            Vector3 final = input;
            final.y = KickStart.WaterHeight;
            if (AIEPathMapper.GetAltitudeLoadedOnly(input, out float height))
            {
                if (height > final.y)
                    final.y = height;
            }

            return final;
        }
        public static Vector3 OffsetFromSea(Vector3 input, Tank tank, TankAIHelper thisInst)
        {
            if (!KickStart.isWaterModPresent)
                return input;
            float heightTank;
            // The below is far too inaccurate for this duty - I will have to do it the old way
            //AIEPathMapper.GetAltitudeLoadedOnly(tank.rbody.velocity, out heightTank);
            if (tank.rbody != null)
                heightTank = tank.rbody.velocity.Clamp(-75 * Vector3.one, 75 * Vector3.one).y + tank.boundsCentreWorldNoCheck.y - (thisInst.lastTechExtents / 2);
            else
                heightTank = tank.boundsCentreWorldNoCheck.y - (thisInst.lastTechExtents / 2);
            Vector3 final = input;
            if (heightTank < KickStart.WaterHeight)// avoid sea pathing!
            {
                // Iterate closest terrain spots
                int stepxM = 3;
                int stepzM = 3;
                float highestHeight = KickStart.WaterHeight - thisInst.lastTechExtents * AIGlobals.WaterDepthTechHeightPercent;
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
                        if (!AIEPathMapper.GetAltitudeLoadedOnly(wow, out float heightC))
                            continue;
                        if (heightC > highestHeight)
                        {
                            highestHeight = heightC;
                            posBest = wow;
                            thisInst.Yield = true;
                        }
                    }
                }
                if (highestHeight > KickStart.WaterHeight)
                {
                    //DebugTAC_AI.Log("TACtical_AI: highest terrain  of depth " + highestHeight + " found at " + posBest);
                    if (thisInst.AdviseAwayCore)
                    { // Reverse
                        final = thisInst.tank.boundsCentreWorldNoCheck + (thisInst.tank.boundsCentreWorldNoCheck - posBest);
                    }
                    else
                        final = posBest;
                }
                else
                {
                    if (thisInst.AdviseAwayCore)
                    { // Reverse
                        final = thisInst.tank.boundsCentreWorldNoCheck + ((input - thisInst.tank.boundsCentreWorldNoCheck).normalized * thisInst.DodgeStrength);
                    }
                    else
                        final = thisInst.tank.boundsCentreWorldNoCheck - ((input - thisInst.tank.boundsCentreWorldNoCheck).normalized * thisInst.DodgeStrength);
                }
            }

            return final;
        }

        // Aux
        internal static Vector3 ModerateMaxAlt(Vector3 moderate, TankAIHelper thisInst)
        {
            if ((bool)Singleton.playerTank && !ManPlayerRTS.PlayerIsInRTS)
            {
                if (moderate.y > AIGlobals.AirWanderMaxHeight + Singleton.playerPos.y)
                {
                    return SnapOffsetFromGroundA(moderate, thisInst);
                }
            }
            else
            {
                try
                {
                    if (moderate.y > AIGlobals.AirWanderMaxHeight + TankAIManager.terrainHeight)
                    {
                        return SnapOffsetFromGroundA(moderate, thisInst);
                    }
                }
                catch { }
            }
            return moderate;
        }
        internal static bool IsUnderMaxAltPlayer(Vector3 Pos)
        {
            if ((bool)Singleton.playerTank && !ManPlayerRTS.PlayerIsInRTS)
            {
                if (Pos.y > AIGlobals.AirWanderMaxHeight + Singleton.playerPos.y)
                {
                    return false;
                }
            }
            else
            {
                try
                {
                    if (Pos.y > AIGlobals.AirWanderMaxHeight + TankAIManager.terrainHeight)
                    {
                        return false;
                    }
                }
                catch { }
            }
            return true;
        }

    }
}
