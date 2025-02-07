using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using TAC_AI.World;

namespace TAC_AI.AI.Movement
{
    internal static class AIEPathing
    {
        /// <summary>  DO NOT EDIT OUTPUT </summary>
        internal static HashSet<Tank> AllyList(Tank tank)
        {
            HashSet<Tank> transfer = TankAIManager.GetNonEnemyTanks(tank.Team);
            if (transfer == null)
                throw new NullReferenceException("AllyList unable to secure HashSet<Tank> of Techs to target?");
            return transfer;
        }

        public const float ShipDepth = -3;
        public const float DefaultExtraSpacing = 2;


        //The default steering handles the ground steering

        //3-axis steering is handled in AIEDrive

        // OBSTICLE AVOIDENCE
        /// <summary> Keep as list, helps efficiency </summary>
        private static List<Visible> ObstList = new List<Visible>();
        internal static List<Visible> ObstructionAwareness(Vector3 posWorld, TankAIHelper helper, float radAdd = DefaultExtraSpacing, bool ignoreDestructable = false)
        {
            ObstList.Clear();
            try
            {
                if (ignoreDestructable)
                {
                    foreach (Visible vis in Singleton.Manager<ManVisible>.inst.VisiblesTouchingRadius(posWorld, helper.lastTechExtents + radAdd, AIGlobals.sceneryBitMask))
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
                    foreach (Visible vis in Singleton.Manager<ManVisible>.inst.VisiblesTouchingRadius(posWorld, helper.lastTechExtents + radAdd, AIGlobals.sceneryBitMask))
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
                DebugTAC_AI.Log(KickStart.ModID + ": Error on ObstructionAwareness");
                DebugTAC_AI.Log(e);
            }
            return ObstList;
        }
        public static bool ObstructionAwarenessAny(Vector3 posWorld, TankAIHelper helper, float radius)
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
                DebugTAC_AI.Log(KickStart.ModID + ": Error on ObstructionAwarenessAny");
                DebugTAC_AI.Log(e);
            }
            return false;
        }

        private static Vector3 ObstOtherDir(Tank tank, TankAIHelper helper, Visible vis)
        {
            //What actually does the avoidence
            Vector3 inputOffset = tank.transform.position - vis.centrePosition;
            float inputSpacing = vis.Radius + helper.lastTechExtents + helper.DodgeStrength;
            Vector3 Final = (inputOffset.normalized * inputSpacing) + tank.transform.position;
            return Final;
        }
        public static Vector3 ObstDodgeOffset(Tank tank, TankAIHelper helper, bool DoDodge, out bool worked, bool useTwo = false, bool ignoreDestructable = false)
        {
            if (helper.IsDirectedMovingFromDest)
                return ObstDodgeOffsetInv(tank, helper, DoDodge, out worked, useTwo, ignoreDestructable);
            worked = false;
            if (!DoDodge || KickStart.AIDodgeCheapness >= 75 || helper.DriveDestDirected == EDriveDest.ToMine || helper.DriveDestDirected == EDriveDest.ToBase)   // are we desperate for performance or going to mine
                return Vector3.zero;    // don't bother with this
            Vector3 Offset = Vector3.zero;

            if (tank.rbody == null)
                return Vector3.zero; // no need, we are stationary

            List<Visible> ObstList = ObstructionAwareness(tank.boundsCentreWorldNoCheck + helper.SafeVelocity, helper, 2, ignoreDestructable);
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
                helper.ThrottleState = AIThrottleState.Yield;
                worked = true;
                if (useTwo && moreThan2)
                {
                    if (ObstructionAwarenessSetPiece(tank.boundsCentreWorldNoCheck + helper.SafeVelocity, tank, helper, out Vector3 posMon))
                        Offset = (ObstOtherDir(tank, helper, ObstList.ElementAt(bestStep)) + ObstOtherDir(tank, helper, ObstList.ElementAt(auxStep)) + posMon) / 3;
                    else
                        Offset = (ObstOtherDir(tank, helper, ObstList.ElementAt(bestStep)) + ObstOtherDir(tank, helper, ObstList.ElementAt(auxStep))) / 2;
                }
                else
                {
                    if (ObstructionAwarenessSetPiece(tank.boundsCentreWorldNoCheck + helper.SafeVelocity, tank, helper, out Vector3 posMon))
                        Offset = (ObstOtherDir(tank, helper, ObstList.ElementAt(bestStep)) + posMon) / 2;
                    else
                        Offset = ObstOtherDir(tank, helper, ObstList.ElementAt(bestStep));
                }
            }
            catch (Exception e)
            {
                DebugTAC_AI.Log(KickStart.ModID + ": Error on ObstDodgeOffset");
                DebugTAC_AI.Log(e);
            }
            return Offset;
        }

        /// <summary>
        /// For inverted output
        /// </summary>
        /// <param name="tank"></param>
        /// <param name="helper"></param>
        /// <param name="vis"></param>
        /// <returns></returns>
        private static Vector3 ObstDir(Tank tank, TankAIHelper helper, Visible vis)
        {
            //What actually does the avoidence
            Vector3 inputOffset = tank.transform.position - vis.centrePosition;
            float inputSpacing = vis.Radius + helper.lastTechExtents + helper.DodgeStrength;
            Vector3 Final = -(inputOffset.normalized * inputSpacing) + tank.transform.position;
            return Final;
        }
        private static Vector3 ObstDodgeOffsetInv(Tank tank, TankAIHelper helper, bool DoDodge, out bool worked, bool useTwo = false, bool ignoreDestructable = false)
        {
            worked = false;
            if (!DoDodge || KickStart.AIDodgeCheapness >= 60 || helper.DriveDestDirected == EDriveDest.ToMine || helper.DriveDestDirected == EDriveDest.ToBase)   // are we desperate for performance or going to mine
                return Vector3.zero;    // don't bother with this
            Vector3 Offset = Vector3.zero;

            if (tank.rbody == null)
                return Vector3.zero; // no need, we are stationary

            List<Visible> ObstList = ObstructionAwareness(tank.boundsCentreWorldNoCheck + helper.SafeVelocity, helper, 2, ignoreDestructable);
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
                helper.ThrottleState = AIThrottleState.Yield;
                worked = true;
                if (useTwo && moreThan2)
                {
                    if (ObstructionAwarenessSetPiece(tank.boundsCentreWorldNoCheck + helper.SafeVelocity, tank, helper, out Vector3 posMon, true))
                        Offset = (ObstDir(tank, helper, ObstList.ElementAt(bestStep)) + ObstDir(tank, helper, ObstList.ElementAt(auxStep)) + posMon) / 3;
                    else
                        Offset = (ObstDir(tank, helper, ObstList.ElementAt(bestStep)) + ObstDir(tank, helper, ObstList.ElementAt(auxStep))) / 2;
                }
                else
                {
                    if (ObstructionAwarenessSetPiece(tank.boundsCentreWorldNoCheck + helper.SafeVelocity, tank, helper, out Vector3 posMon, true))
                        Offset = (ObstDir(tank, helper, ObstList.ElementAt(bestStep)) + posMon) / 2;
                    else
                        Offset = ObstDir(tank, helper, ObstList.ElementAt(bestStep));
                }
            }
            catch (Exception e)
            {
                DebugTAC_AI.Log(KickStart.ModID + ": Error on ObstDodgeOffset");
                DebugTAC_AI.Log(e);
            }
            return Offset;
        }

        /// <summary>
        /// also handles sceneryblockers
        /// </summary>
        /// <param name="posScene"></param>
        /// <param name="tank"></param>
        /// <param name="helper"></param>
        /// <param name="pos"></param>
        /// <param name="invert"></param>
        /// <returns></returns>
        public static bool ObstructionAwarenessSetPiece(Vector3 posScene, Tank tank, TankAIHelper helper, out Vector3 pos, bool invert = false)
        {
            pos = Vector3.zero;
            ManWorld world = Singleton.Manager<ManWorld>.inst;
            if (!helper.tank.IsAnchored && TankAIManager.SetPieces.Count > 0)
            {
                List<ManWorld.TerrainSetPiecePlacement> ObstList = TankAIManager.SetPieces;
                float inRange = 270;
                bool isInRange = false;
                foreach (var item in ObstList)
                {
                    if ((item.m_WorldPosition.ScenePosition - posScene).WithinSquareXZ(inRange))
                    {
                        isInRange = true;
                        break;
                    }
                }
                if (!isInRange)
                {
                    if (world.CheckIfInsideSceneryBlocker(SceneryBlocker.BlockMode.Spawn, posScene, helper.lastTechExtents + 12))
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
                    Ray ray = new Ray(posScene, helper.tank.rootBlockTrans.forward);
                    Physics.Raycast(ray, out RaycastHit hitInfo, world.TileSize, monuments, QueryTriggerInteraction.Collide);
                    if ((bool)hitInfo.collider)
                    {
                        if (hitInfo.collider.GetComponent<TerrainSetPiece>())
                        {
                            TerrainSetPiece piece = hitInfo.collider.GetComponent<TerrainSetPiece>();
                            if (invert)
                            {
                                pos = ObstDirSetPiece(tank, helper, posScene, piece);
                            }
                            else
                            {
                                pos = ObstOtherDirSetPiece(tank, helper, posScene, piece);
                            }
                            return true;
                        }
                    }
                }
                catch (Exception e)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Error on ObstructionAwarenessMonument");
                    DebugTAC_AI.Log(e);
                }
            }
            return false;
        }
        public static bool ObstructionAwarenessSetPieceAny(Vector3 posScene, TankAIHelper helper, float radius)
        {
            if (!helper.tank.IsAnchored && ManWorld.inst.GetSetPiecePlacement().Count > 0)
            {
                if (ManWorld.inst.CheckIfInsideSceneryBlocker(SceneryBlocker.BlockMode.Spawn, posScene, radius))
                {
                    return true;
                }
            }
            return false;
        }
        public static bool ObstructionAwarenessTerrain(Vector3 posScene, TankAIHelper helper, float radius)
        {
            if (!helper.tank.IsAnchored)
            {
                float height = AIEPathMapper.GetHighestAltInRadius(posScene, radius, false);
                if (height > posScene.y - radius)
                    return true;
            }
            return false;
        }
        public static Vector3 ObstOtherDirSetPiece(Tank tank, TankAIHelper helper, Vector3 pos, TerrainSetPiece vis)
        {   //What actually does the avoidence
            Vector3 inputOffset = tank.transform.position - pos;
            float inputSpacing = vis.GetApproxCellRadius() + helper.lastTechExtents + helper.DodgeStrength;
            Vector3 Final = (inputOffset.normalized * inputSpacing) + tank.transform.position;
            return Final;
        }
        public static Vector3 ObstDirSetPiece(Tank tank, TankAIHelper helper, Vector3 pos, TerrainSetPiece vis)
        {   //What actually does the avoidence
            Vector3 inputOffset = tank.transform.position - pos;
            float inputSpacing = vis.GetApproxCellRadius() + helper.lastTechExtents + helper.DodgeStrength;
            Vector3 Final = -(inputOffset.normalized * inputSpacing) + tank.transform.position;
            return Final;
        }





        // ALLY COLLISION AVOIDENCE
        private static bool AvoidInvalidOrIgnoreable(Tank tank)
        {
            if (tank != null && tank.visible.isActive)
            {
                TankAIHelper help = tank.GetHelperInsured();
                return (help.IsMultiTech && tank.IsAnchored) || help.DediAI == AIType.Aegis;
            }
            return true;
        }
        private static bool FollowInvalidOrIgnoreable(Tank tank)
        {
            if (tank != null && tank.visible.isActive)
            {
                TankAIHelper help = tank.GetHelperInsured();
                return help.IsMultiTech || help.DediAI == AIType.Aegis;
            }
            return true;
        }
        public static Tank ClosestAlly(IEnumerable<Tank> AlliesAlt, Vector3 tankPos, out float bestValue, TankAIHelper thisTank)
        {
            // Finds the closest ally and outputs their respective distance as well as their being
            bestValue = 500;
            Tank closestTank = null;
            try
            {
                foreach (Tank otherTech in AlliesAlt)
                {
                    if (AvoidInvalidOrIgnoreable(otherTech) || thisTank.tank == otherTech || 
                        thisTank.MultiTechsAffiliated.Contains(otherTech))
                        continue;
                    float temp = (otherTech.boundsCentreWorldNoCheck - tankPos).sqrMagnitude;
                    if (bestValue > temp)
                    {
                        bestValue = temp;
                        closestTank = otherTech;
                    }
                }
                if (closestTank != null)
                    bestValue = (closestTank.boundsCentreWorldNoCheck - tankPos).magnitude;
                //DebugTAC_AI.Log(KickStart.ModID + ":ClosestAllyProcess " + closestTank.name);
            }
            catch //(Exception e)
            {
                //DebugTAC_AI.Log(KickStart.ModID + ": Crash on ClosestAllyProcess " + e);
            }
            return closestTank;
        }
        public static Tank ClosestAllyPrecision(IEnumerable<Tank> AlliesAlt, Vector3 tankPos, out float bestValue, TankAIHelper thisTank)
        {
            // Finds the closest ally and outputs their respective distance as well as their being
            //  For when the size matters of the object to dodge
            //  DEMANDS MORE PROCESSING THAN THE ABOVE
            bestValue = 500;
            Tank closestTank = null;
            try
            {
                foreach (Tank otherTech in AlliesAlt)
                {
                    if (AvoidInvalidOrIgnoreable(otherTech) || thisTank.tank == otherTech || 
                        thisTank.MultiTechsAffiliated.Contains(otherTech))
                        continue;
                    float temp = (otherTech.boundsCentreWorldNoCheck - tankPos).sqrMagnitude - otherTech.GetCheapBounds();
                    if (bestValue > temp)
                    {
                        bestValue = temp;
                        closestTank = otherTech;
                    }
                }
                if (closestTank != null)
                    bestValue = (closestTank.boundsCentreWorldNoCheck - tankPos).magnitude;
                //DebugTAC_AI.Log(KickStart.ModID + ": ClosestAllyProcess " + closestTank.name);
            }
            catch //(Exception e)
            {
                //DebugTAC_AI.Log(KickStart.ModID + ": Crash on ClosestAllyPrecisionProcess " + e);
            }
            return closestTank;
        }

        public static Tank SecondClosestAlly(IEnumerable<Tank> AlliesAlt, Vector3 tankPos, out Tank secondTank, out float bestValue, out float auxBestValue, TankAIHelper thisTank)
        {
            // Finds the two closest allies and outputs their respective distances as well as their beings
            bestValue = 500;
            auxBestValue = 500;
            secondTank = null;
            Tank closestTank = null;
            try
            {
                foreach (Tank otherTech in AlliesAlt)
                {
                    if (AvoidInvalidOrIgnoreable(otherTech) || thisTank.tank == otherTech || 
                        thisTank.MultiTechsAffiliated.Contains(otherTech))
                        continue;
                    float temp = (otherTech.boundsCentreWorldNoCheck - tankPos).sqrMagnitude;
                    if (bestValue > temp)
                    {
                        secondTank = otherTech;
                        closestTank = otherTech;
                        auxBestValue = bestValue;
                        bestValue = temp;
                    }
                    else if (bestValue < temp && auxBestValue > temp)
                    {
                        secondTank = otherTech;
                        auxBestValue = temp;
                    }
                }
                if (secondTank != null)
                    auxBestValue = (secondTank.boundsCentreWorldNoCheck - tankPos).magnitude;
                if (closestTank != null)
                    bestValue = (closestTank.boundsCentreWorldNoCheck - tankPos).magnitude;
                //DebugTAC_AI.Log(KickStart.ModID + ": ClosestAllyProcess " + closestTank.name);
                return closestTank;
            }
            catch (Exception e)
            {
                DebugTAC_AI.Log(KickStart.ModID + ": Crash on SecondClosestAlly " + e);
            }
            DebugTAC_AI.Log(KickStart.ModID + ": SecondClosestAlly - COULD NOT FETCH TANK");
            secondTank = null;
            return null;
        }
        public static Tank SecondClosestAllyPrecision(IEnumerable<Tank> AlliesAlt, Vector3 tankPos, out Tank secondTank, out float bestValue, out float auxBestValue, TankAIHelper thisTank)
        {
            // Finds the two closest allies and outputs their respective distances as well as their beings
            //  For when the size matters of the object to dodge
            //  DEMANDS MORE PROCESSING THAN THE ABOVE
            bestValue = 500;
            auxBestValue = 500;
            secondTank = null;
            Tank closestTank = null;
            try
            {
                foreach (Tank otherTech in AlliesAlt)
                {
                    if (AvoidInvalidOrIgnoreable(otherTech) || thisTank.tank == otherTech || 
                        thisTank.MultiTechsAffiliated.Contains(otherTech))
                        continue;
                    float temp = (otherTech.boundsCentreWorldNoCheck - tankPos).sqrMagnitude - otherTech.GetCheapBounds();
                    if (bestValue > temp)
                    {
                        secondTank = otherTech;
                        closestTank = otherTech;
                        auxBestValue = bestValue;
                        bestValue = temp;
                    }
                    else if (bestValue < temp && auxBestValue > temp)
                    {
                        secondTank = otherTech;
                        auxBestValue = temp;
                    }
                }
                if (secondTank != null)
                    auxBestValue = (secondTank.boundsCentreWorldNoCheck - tankPos).magnitude;
                if (closestTank != null)
                    bestValue = (closestTank.boundsCentreWorldNoCheck - tankPos).magnitude;
                //DebugTAC_AI.Log(KickStart.ModID + ": ClosestAllyProcess " + closestTank.name);
                return closestTank;
            }
            catch //(Exception e)
            {
                //DebugTAC_AI.Log(KickStart.ModID + ": Crash on SecondClosestAllyPrecisionProcess " + e);
            }
            DebugTAC_AI.Log(KickStart.ModID + ": SecondClosestAllyPrecision - COULD NOT FETCH TANK");
            secondTank = null;
            return null;
        }
        
        public static Tank ClosestUnanchoredAllyAegis(IEnumerable<Tank> AlliesAlt, Vector3 tankPos, float rangeSqr, out float bestValue, TankAIHelper thisTank)
        {
            // Finds the closest ally and outputs their respective distance as well as their being
            bestValue = rangeSqr;
            Tank closestTank = null;
            try
            {
                foreach (Tank otherTech in AlliesAlt)
                {
                    if (FollowInvalidOrIgnoreable(otherTech) || thisTank.tank == otherTech || otherTech.IsAnchored)
                        continue;
                    float temp = (otherTech.boundsCentreWorldNoCheck - tankPos).sqrMagnitude;
                    if (bestValue > temp)
                    {
                        bestValue = temp;
                        closestTank = otherTech;
                    }
                }
                if (closestTank == null)
                    return null;
                bestValue = (closestTank.boundsCentreWorldNoCheck - tankPos).magnitude;
                //DebugTAC_AI.Log(KickStart.ModID + ":ClosestAllyProcess " + closestTank.name);
            }
            catch //(Exception e)
            {
                //DebugTAC_AI.Log(KickStart.ModID + ": Crash on ClosestAllyProcess " + e);
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
            //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ": Host Steering " + controlOverload.m_State.m_InputRotation);
            // Generate a rough tangent
            Vector3 MoveDirectionUnthrottled = ((Quaternion.Euler(RAdjuster.x, RAdjuster.y, RAdjuster.z) * offsetTo) - offsetTo).normalized * (1000 * AIHelp.lastTechExtents);

            Vector3 posToGo = MoveDirectionUnthrottled + DAdjuster;


            //Anchor handling
            if (AIHelp.AutoAnchor)
            {
                if (tankToCopy.IsAnchored)
                {
                    if (AIHelp.CanAutoAnchor)
                        AIHelp.TryInsureAnchor();
                }
                else
                {
                    if (AIHelp.CanAutoUnanchor)
                        AIHelp.Unanchor();
                }
            }

            // Then we pack it all up nicely in the end
            end = tankToCopy.trans.TransformPoint(posToGo + tankToCopy.blockBounds.center);
            //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ": Drive Mimic " + (end - centerThis));
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
            //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ": Host Steering " + controlOverload.m_State.m_InputRotation);
            // Generate a rough tangent
            Vector3 MoveDirectionUnthrottled = ((Quaternion.Euler(RAdjuster.x, RAdjuster.y, RAdjuster.z) * offsetTo) - offsetTo).normalized * (1000 * AIHelp.lastTechExtents);

            Vector3 posToGo = MoveDirectionUnthrottled + DAdjuster;

            //Run ETC copies
            AIHelp.ProcessControl(Vector3.zero, Vector3.zero, Vector3.zero, 
                controlCopyTarget.m_BoostProps, controlCopyTarget.m_BoostJets);

            //Anchor handling
            if (AIHelp.AutoAnchor)
            {
                if (tankToCopy.IsAnchored)
                {
                    if (AIHelp.CanAutoAnchor)
                        AIHelp.TryInsureAnchor();
                }
                else
                {
                    if (AIHelp.CanAutoUnanchor)
                        AIHelp.Unanchor();
                }
            }

            // Then we pack it all up nicely in the end
            end = tankToCopy.trans.TransformPoint(posToGo + tankToCopy.blockBounds.center);
            //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ": Drive Mimic " + (end - centerThis));
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
        public static bool AboveTheSeaForcedAccurate(Vector3 posScene)
        {
            if (!KickStart.isWaterModPresent)
                return false;
            float height = ManWorld.inst.TileManager.GetTerrainHeightAtPosition(posScene, out _);
            if (height < KickStart.WaterHeight)
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
        /// <param name="helper"></param>
        /// <param name="groundOffset"></param>
        /// <returns></returns>
        public static Vector3 OffsetFromGround(Vector3 input, TankAIHelper helper, float groundOffset = 0)
        {
            float final_y;
            Vector3 final = input;
            bool terrain = AIEPathMapper.GetAltitudeLoadedOnly(input, out float height);
            if (groundOffset == 0) groundOffset = helper.GroundOffsetHeight;
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
        /// <param name="helper"></param>
        /// <param name="groundOffset"></param>
        /// <returns></returns>
        public static Vector3 OffsetFromGroundH(Vector3 input, TankAIHelper helper, float groundOffset = 0)
        {
            float final_y;
            Vector3 final = input;
            bool terrain = AIEPathMapper.GetAltitudeLoadedOnly(input, out float height);
            if (groundOffset == 0) groundOffset = helper.GroundOffsetHeight;
            if (terrain)
                final_y = height + groundOffset;
            else
                final_y = 50 + groundOffset;
            if (helper.AdviseAwayCore)// && helper.lastEnemy.IsNull()
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
                        final.x = helper.tank.boundsCentreWorldNoCheck.x;
                        final.z = helper.tank.boundsCentreWorldNoCheck.z;
                        final.y = height;
                    }
                    else
                    {
                        final.y = helper.tank.boundsCentreWorldNoCheck.y;
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
        /// <param name="helper"></param>
        /// <param name="groundOffset"></param>
        /// <returns></returns>
        public static Vector3 OffsetFromGroundA(Vector3 input, TankAIHelper helper, float groundOffset = 0)
        {
            float final_y;
            Vector3 final = input;
            bool terrain = AIEPathMapper.GetAltitudeLoadedOnly(input, out float height);
            if (groundOffset == 0) groundOffset = helper.GroundOffsetHeight;
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
        public static Vector3 SnapOffsetFromGroundA(Vector3 input, TankAIHelper helper, float groundOffset = 0)
        {
            float final_y;
            Vector3 final = input;
            bool terrain = AIEPathMapper.GetAltitudeLoadedOnly(input, out float height);
            if (groundOffset == 0) groundOffset = helper.GroundOffsetHeight;
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
        public static Vector3 OffsetToSea(Vector3 input, Tank tank, TankAIHelper helper)
        {
            Vector3 final = input;
            float heightTank;
            if (tank.rbody != null)
                AIEPathMapper.GetAltitudeLoadedOnly(tank.boundsCentreWorldNoCheck + helper.SafeVelocity.Clamp(-75 * Vector3.one, 75 * Vector3.one), out heightTank);
            else
                AIEPathMapper.GetAltitudeLoadedOnly(tank.boundsCentreWorldNoCheck, out heightTank);
            bool terrain = AIEPathMapper.GetAltitudeLoadedOnly(input, out float height);
            if (terrain)
            {
                float operatingDepth = tank.boundsCentreWorldNoCheck.y + helper.LowestPointOnTech;
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
                                helper.ThrottleState = AIThrottleState.Yield;
                            }
                        }
                    }
                    if (vecCount == 25)
                    {
                        //helper.ThrottleState = AIThrottleState.Yield;
                        //DebugTAC_AI.Log(KickStart.ModID + ": Tech " + helper.tank.name + " is jammed on land!");
                        if (helper.AdviseAwayCore)
                        { // Reverse
                            final = helper.tank.boundsCentreWorldNoCheck + ((input - helper.tank.boundsCentreWorldNoCheck).normalized * helper.DodgeStrength);
                        }
                        else
                            final = helper.tank.boundsCentreWorldNoCheck - ((input - helper.tank.boundsCentreWorldNoCheck).normalized * helper.DodgeStrength);
                    }
                    else if (vecCount > 0)
                    {
                        //DebugTAC_AI.Log(KickStart.ModID + ": Tech " + helper.tank.name + " is trying to avoid terrain");
                        if (helper.AdviseAwayCore)
                        { // Reverse
                            final = helper.tank.boundsCentreWorldNoCheck - ((tank.boundsCentreWorldNoCheck - (posAll / vecCount)).normalized * helper.DodgeStrength);
                        }
                        else
                            final = helper.tank.boundsCentreWorldNoCheck + ((tank.boundsCentreWorldNoCheck - (posAll / vecCount)).normalized * helper.DodgeStrength);
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
        public static Vector3 OffsetFromSea(Vector3 input, Tank tank, TankAIHelper helper)
        {
            if (!KickStart.isWaterModPresent)
                return input;
            float heightTank;
            // The below is far too inaccurate for this duty - I will have to do it the old way
            //AIEPathMapper.GetAltitudeLoadedOnly(helper.SafeVelocity, out heightTank);
            if (tank.rbody != null)
                heightTank = helper.SafeVelocity.Clamp(-75 * Vector3.one, 75 * Vector3.one).y + tank.boundsCentreWorldNoCheck.y - (helper.lastTechExtents / 2);
            else
                heightTank = tank.boundsCentreWorldNoCheck.y - (helper.lastTechExtents / 2);
            Vector3 final = input;
            if (heightTank < KickStart.WaterHeight)// avoid sea pathing!
            {
                // Iterate closest terrain spots
                int stepxM = 3;
                int stepzM = 3;
                float highestHeight = KickStart.WaterHeight - helper.lastTechExtents * AIGlobals.WaterDepthTechHeightPercent;
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
                            helper.ThrottleState = AIThrottleState.Yield;
                        }
                    }
                }
                if (highestHeight > KickStart.WaterHeight)
                {
                    //DebugTAC_AI.Log(KickStart.ModID + ": highest terrain  of depth " + highestHeight + " found at " + posBest);
                    if (helper.AdviseAwayCore)
                    { // Reverse
                        final = helper.tank.boundsCentreWorldNoCheck + (helper.tank.boundsCentreWorldNoCheck - posBest);
                    }
                    else
                        final = posBest;
                }
                else
                {
                    if (helper.AdviseAwayCore)
                    { // Reverse
                        final = helper.tank.boundsCentreWorldNoCheck + ((input - helper.tank.boundsCentreWorldNoCheck).normalized * helper.DodgeStrength);
                    }
                    else
                        final = helper.tank.boundsCentreWorldNoCheck - ((input - helper.tank.boundsCentreWorldNoCheck).normalized * helper.DodgeStrength);
                }
            }

            return final;
        }

        // Aux
        internal static Vector3 ModerateMaxAlt(Vector3 moderate, TankAIHelper helper)
        {
            if ((bool)Singleton.playerTank && !ManWorldRTS.PlayerIsInRTS)
            {
                if (moderate.y > AIGlobals.AirWanderMaxHeight + Singleton.playerPos.y)
                {
                    return SnapOffsetFromGroundA(moderate, helper);
                }
            }
            else
            {
                try
                {
                    if (moderate.y > AIGlobals.AirWanderMaxHeight + TankAIManager.terrainHeight)
                    {
                        return SnapOffsetFromGroundA(moderate, helper);
                    }
                }
                catch { }
            }
            return moderate;
        }
        internal static bool IsUnderMaxAltPlayer(float height)
        {
            if ((bool)Singleton.playerTank && !ManWorldRTS.PlayerIsInRTS)
            {
                if (height > AIGlobals.AirWanderMaxHeight + Singleton.playerPos.y)
                    return false;
            }
            else
            {
                try
                {
                    if (height > AIGlobals.AirWanderMaxHeight + TankAIManager.terrainHeight)
                        return false;
                }
                catch { }
            }
            return true;
        }

    }
}
