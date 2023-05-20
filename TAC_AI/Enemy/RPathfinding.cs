﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using TAC_AI.AI.Movement;

namespace TAC_AI.AI.Enemy
{
    public static class RPathfinding
    {
        /// <summary>
        /// Airborne Avoidence
        /// </summary>
        /// <param name="tank"></param>
        /// <param name="targetIn"></param>
        /// <param name="thisInst"></param>
        /// <param name="mind"></param>
        /// <returns></returns>
        public static Vector3 AvoidAssistEnemy(Tank tank, Vector3 targetIn, Vector3 predictionOffset, AIECore.TankAIHelper thisInst, EnemyMind mind)
        {   //WIP
            if (!thisInst.AvoidStuff || tank.IsAnchored)
                return targetIn;
            HashSet<Tank> Allies = AIEPathing.AllyList(tank);
            try
            {
                bool obst;
                Tank lastCloseAlly;
                float lastAllyDist;
                if (mind.CommanderSmarts >= EnemySmarts.Smrt && Allies.Count() > 1)// MORE processing power
                {
                    lastCloseAlly = SecondClosestAllyE(predictionOffset, out Tank lastCloseAlly2, out lastAllyDist, out float lastAuxVal, Allies);
                    if (lastAllyDist < thisInst.lastTechExtents + lastCloseAlly.GetCheapBounds() + 12 + (predictionOffset - tank.boundsCentreWorldNoCheck).magnitude)
                    {
                        if (lastAuxVal < thisInst.lastTechExtents + lastCloseAlly2.GetCheapBounds() + 12 + (predictionOffset - tank.boundsCentreWorldNoCheck).magnitude)
                        {
                            Vector3 ProccessedVal2 = thisInst.GetOtherDir(lastCloseAlly) + thisInst.GetOtherDir(lastCloseAlly2) + AIEPathing.ObstDodgeOffset(tank, thisInst, true, out obst, true);
                            if (obst)
                                return (targetIn + ProccessedVal2) / 4;
                            else
                                return (targetIn + ProccessedVal2) / 3;
                        }
                        Vector3 ProccessedVal = thisInst.GetOtherDir(lastCloseAlly) + AIEPathing.ObstDodgeOffset(tank, thisInst, true, out obst, true);
                        if (obst)
                            return (targetIn + ProccessedVal) / 3;
                        else
                            return (targetIn + ProccessedVal) / 2;
                    }
                    else
                    {
                        Vector3 ProccessedVal = AIEPathing.ObstDodgeOffset(tank, thisInst, true, out obst, true);
                        if (obst)
                            return (targetIn + ProccessedVal) / 2;
                        else
                            return targetIn;
                    }

                }
                lastCloseAlly = ClosestAllyE(predictionOffset, out lastAllyDist, Allies);
                if (lastCloseAlly == null)
                {
                    //DebugTAC_AI.Log("TACtical_AI: ALLY IS NULL");
                    Vector3 ProccessedVal = AIEPathing.ObstDodgeOffset(tank, thisInst, true, out obst, mind.CommanderSmarts >= EnemySmarts.Meh);
                    if (obst)
                        return (targetIn + ProccessedVal) / 2;
                    else
                        return targetIn;
                }
                else
                {
                    if (lastAllyDist < thisInst.lastTechExtents + lastCloseAlly.GetCheapBounds() + 12 + (predictionOffset - tank.boundsCentreWorldNoCheck).magnitude)
                    {
                        Vector3 ProccessedVal = thisInst.GetOtherDir(lastCloseAlly) + AIEPathing.ObstDodgeOffset(tank, thisInst, true, out obst, mind.CommanderSmarts >= EnemySmarts.Meh);
                        if (obst)
                            return (targetIn + ProccessedVal) / 3;
                        else
                            return (targetIn + ProccessedVal) / 2;
                    }
                }
            }
            catch (Exception e)
            {
                DebugTAC_AI.Log("TACtical_AI: Crash on AvoidAssistEnemy(AIR) " + e);
                return targetIn;
            }
            if (targetIn.IsNaN())
            {
                DebugTAC_AI.Log("TACtical_AI: AvoidAssistEnemy(2) IS NaN!!");
            }
            return targetIn;
        }



        public static Tank ClosestAllyE(Vector3 tankPos, out float bestValue, HashSet<Tank> Allies)
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
                    if (bestValue > temp && temp >= 10)
                    {
                        bestValue = temp;
                        bestStep = stepper;
                    }
                }
                bestValue = (Allies.ElementAt(bestStep).boundsCentreWorldNoCheck - tankPos).magnitude;
                closestTank = Allies.ElementAt(bestStep);
                //DebugTAC_AI.Log("TACtical_AI:ClosestAllyProcess " + closestTank.name);
            }
            catch //(Exception e)
            {
                //DebugTAC_AI.Log("TACtical_AI: Crash on ClosestAllyProcess " + e);
            }
            return closestTank;
        }
        public static Tank SecondClosestAllyE(Vector3 tankPos, out Tank secondTank, out float bestValue, out float auxBestValue, HashSet<Tank> Allies)
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
                    if (bestValue > temp && temp >= 10)
                    {
                        auxStep = bestStep;
                        bestStep = stepper;
                        auxBestValue = bestValue;
                        bestValue = temp;
                    }
                    else if (bestValue < temp && auxBestValue > temp && temp >= 10)
                    {
                        auxStep = stepper;
                        auxBestValue = temp;
                    }
                }
                secondTank = Allies.ElementAt(auxStep);
                closestTank = Allies.ElementAt(bestStep);
                auxBestValue = (Allies.ElementAt(auxStep).boundsCentreWorldNoCheck - tankPos).magnitude;
                bestValue = (Allies.ElementAt(bestStep).boundsCentreWorldNoCheck - tankPos).magnitude;
                //DebugTAC_AI.Log("TACtical_AI: ClosestAllyProcess " + closestTank.name);
                return closestTank;
            }
            catch //(Exception e)
            {
                //DebugTAC_AI.Log("TACtical_AI: Crash on SecondClosestAllyProcess " + e);
            }
            DebugTAC_AI.Log("TACtical_AI: SecondClosestAllyE - COULD NOT FETCH TANK");
            secondTank = null;
            return null;
        }

    }
}
