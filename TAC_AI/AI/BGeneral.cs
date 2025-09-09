using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TAC_AI.Templates;

namespace TAC_AI.AI
{
    internal static class BGeneral
    {
        public static void ResetValues(TankAIHelper helper, ref EControlOperatorSet direct)
        {
            helper.ThrottleState = AIThrottleState.FullSpeed;
            helper.FIRE_ALL = false;
            helper.FullBoost = false;
            helper.FirePROPS = false;
            helper.ForceSetBeam = false;
            helper.LightBoost = false;
            helper.DriveVar = 0;

            direct.FaceDest();
        }

        /// <summary>
        /// Defend like default
        /// </summary>
        /// <param name="helper"></param>
        /// <param name="tank"></param>
        public static bool AidDefend(TankAIHelper helper, Tank tank)
        {
            // Determines the weapons actions and aiming of the AI
            if (helper.lastEnemyGet != null)
            {
                helper.TryRefreshEnemyAllied();
                //Fire even when retreating - the AI's life depends on this!
                helper.AttackEnemy = true;
                return false;
            }
            else
            {
                helper.AttackEnemy = false;
                helper.TryRefreshEnemyAllied();
                return helper.lastEnemyGet;
            }
        }

        /// <summary>
        /// Hold fire until aiming at target cab-forwards or after some time
        /// </summary>
        /// <param name="helper"></param>
        /// <param name="tank"></param>
        public static void AimDefend(TankAIHelper helper, Tank tank)
        {
            // Determines the weapons actions and aiming of the AI, this one is more fire-precise and used for turrets
            helper.AttackEnemy = false;
            helper.TryRefreshEnemyAllied();
            if (helper.lastEnemyGet != null)
            {
                Vector3 aimTo = (helper.lastEnemyGet.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized;
                helper.WeaponDelayClock++;
                if (helper.Attempt3DNavi)
                {
                    if (helper.SideToThreat)
                    {
                        float dot = Vector3.Dot(tank.rootBlockTrans.right, aimTo);
                        if (dot > 0.45f || dot < -0.45f || helper.WeaponDelayClock >= 30)
                        {
                            helper.AttackEnemy = true;
                            helper.WeaponDelayClock = 30;
                        }
                    }
                    else
                    {
                        if (Vector3.Dot(tank.rootBlockTrans.forward, aimTo) > 0.45f || helper.WeaponDelayClock >= 30)
                        {
                            helper.AttackEnemy = true;
                            helper.WeaponDelayClock = 30;
                        }
                    }
                }
                else
                {
                    if (helper.SideToThreat)
                    {
                        float dot = Vector2.Dot(tank.rootBlockTrans.right.ToVector2XZ(), aimTo.ToVector2XZ());
                        if (dot > 0.45f || dot < -0.45f || helper.WeaponDelayClock >= 30)
                        {
                            helper.AttackEnemy = true;
                            helper.WeaponDelayClock = 30;
                        }
                    }
                    else
                    {
                        if (Vector2.Dot(tank.rootBlockTrans.forward.ToVector2XZ(), aimTo.ToVector2XZ()) > 0.45f || helper.WeaponDelayClock >= 30)
                        {
                            helper.AttackEnemy = true;
                            helper.WeaponDelayClock = 30;
                        }
                    }
                }
            }
            else
            {
                helper.WeaponDelayClock = 0;
                helper.AttackEnemy = false;
            }
        }

        public static void SelfDefend(TankAIHelper helper, Tank tank)
        {
            // Alternative of the above - does not aim at enemies while mining
            if (helper.Obst == null)
            {
                if (AidDefend(helper, tank))
                {
                    AIECore.RequestFocusFirePlayer(tank, helper.lastEnemyGet, RequestSeverity.ThinkMcFly);
                }
                else
                    helper.AttackEnemy = false;
            }
            else
                helper.AttackEnemy = true;
        }

        /// <summary>
        /// Stay focused on first target if the unit is order to focus-fire
        /// </summary>
        /// <param name="helper"></param>
        /// <param name="tank"></param>
        public static void RTSCombat(TankAIHelper helper, Tank tank)
        {
            // Determines the weapons actions and aiming of the AI
            if (helper.lastEnemyGet != null)
            {   // focus fire like Grudge
                helper.AttackEnemy = true;
                if (!helper.lastEnemyGet.isActive)
                    helper.TryRefreshEnemyAllied();
            }
            else
            {
                helper.AttackEnemy = false;
                helper.TryRefreshEnemyAllied();
            }
        }

        public static bool GetMineableScenery(TankAIHelper helper, Tank tank, bool includeTradingStations, ref float dist, ref bool hasMessaged, ref EControlOperatorSet direct)
        {
            helper.foundGoal = AIECore.FetchClosestResource(tank.rootBlockTrans.position, helper.JobSearchRange + 
                AIGlobals.FindItemScanRangeExtension, helper.lastTechExtents * AIGlobals.WaterDepthTechHeightPercent ,
                out helper.theResource);
            if (!helper.foundGoal)
            {
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Found a Resource Node...");
                direct.SetLastDest(helper.theResource.centrePosition);
                direct.STOP(helper);
                return true;
            }
            else
            { // We failed to find anything, so we just sit back and chill
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Scanning for resources...");
                StopByBase(helper, tank, includeTradingStations, ref dist, ref hasMessaged, ref direct);
                return false;
            }
        }

        public static bool GetBase(TankAIHelper helper, Tank tank, bool includeTradingStations, ref float dist, ref bool hasMessaged, ref EControlOperatorSet direct)
        {
            helper.foundBase = AIECore.FetchClosestChunkReceiver(tank.rootBlockTrans.position, helper.JobSearchRange +
                            AIGlobals.FindBaseScanRangeExtension, out helper.lastBasePos, out helper.theBase, tank.Team,
                            includeTradingStations);
            if (helper.foundBase && helper.theBase)
            {
                helper.lastBaseExtremes = helper.theBase.GetCheapBounds();
                direct.SetLastDest(helper.theBase.boundsCentreWorld);
                dist = (tank.boundsCentreWorldNoCheck - helper.lastDestinationCore).magnitude;
                return true;
            }
            else
            {
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Searching for nearest base!");
                helper.EstTopSped = 1;//slow down the clock to reduce lagg
                direct.STOP(helper);
                return false; // There's no base!
            }
        }
        public static void GetBaseIfNeeded(TankAIHelper helper, Tank tank, bool includeTradingStations, ref float dist, ref bool hasMessaged, ref EControlOperatorSet direct)
        {
            if (!helper.foundBase)
                GetBase(helper, tank, includeTradingStations, ref dist, ref hasMessaged, ref direct);
        }
        public static void StopByBase(TankAIHelper helper, Tank tank, bool includeTradingStations, ref float dist, ref bool hasMessaged, ref EControlOperatorSet direct)
        {
            GetBaseIfNeeded(helper, tank, includeTradingStations, ref dist, ref hasMessaged, ref direct);
            if (helper.theBase == null)
            {
                helper.foundBase = false;
                direct.STOP(helper);
                return; // There's no base! 
            }
            direct.DriveDest = EDriveDest.ToBase;
            float girth = helper.lastBaseExtremes + helper.lastTechExtents;
            helper.theBase.GetHelperInsured().SlowForApproacher(helper);
            if (dist < girth + 3)
            {   // We are at the base, too close so give some space
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Giving room to base... |Tech is at " + tank.boundsCentreWorldNoCheck);
                direct.DriveAwayFacingTowards();
                helper.AvoidStuff = false;
                helper.ThrottleState = AIThrottleState.ForceSpeed;
                helper.DriveVar = -1;
                helper.SettleDown();
            }
            else if (dist < girth + 7)
            {   // We are at the base, stop moving and hold pos
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Arrived at a base and applying brakes. |Tech is at " + tank.boundsCentreWorldNoCheck);
                direct.DriveToFacingTowards();
                helper.AvoidStuff = false;
                helper.ThrottleState = AIThrottleState.Yield;
                helper.ThrottleState = AIThrottleState.PivotOnly;
                helper.SettleDown();
            }
            else
            {   // Go to the place
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Going to base! |Tech is at " + tank.boundsCentreWorldNoCheck);
                direct.DriveToFacingTowards();
                helper.AvoidStuff = true;
            }
        }
        public static void StopByPosition(TankAIHelper helper, Tank tank, Vector3 position, float girth, ref EControlOperatorSet direct)
        {
            Vector3 veloFlat = Vector3.zero;
            if ((bool)tank.rbody)   // So that drifting is minimized
            {
                veloFlat = helper.SafeVelocity;
                veloFlat.y = 0;
            }
            direct.SetLastDest(position);
            float dist = (direct.lastDestination - tank.boundsCentreWorldNoCheck + veloFlat).magnitude;
            direct.DriveDest = EDriveDest.ToLastDestination;
            if (dist < girth + 3)
            {   // We are at the place, too close so give some space
                direct.DriveAwayFacingTowards();
                helper.AvoidStuff = false;
                helper.ThrottleState = AIThrottleState.ForceSpeed;
                helper.DriveVar = -1;
                helper.SettleDown();
            }
            else if (dist < girth + 7)
            {   // We are at the place, stop moving and hold pos
                direct.DriveToFacingTowards();
                helper.AvoidStuff = false;
                helper.ThrottleState = AIThrottleState.Yield;
                helper.ThrottleState = AIThrottleState.PivotOnly;
                helper.SettleDown();
            }
            else
            {   // Go to the place
                direct.DriveToFacingTowards();
                helper.AvoidStuff = true;
            }
        }
    }
}
