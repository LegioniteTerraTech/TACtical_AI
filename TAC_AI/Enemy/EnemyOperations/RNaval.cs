using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TerraTechETCUtil;

namespace TAC_AI.AI.Enemy.EnemyOperations
{
    internal static class RNaval
    {
        //Same as RWheeled but has terrain avoidence
        public static void AttackWhish(TankAIHelper thisInst, Tank tank, EnemyMind mind, ref EControlOperatorSet direct)
        {
            //The Handler that tells the Tank (Escort) what to do movement-wise
            BGeneral.ResetValues(thisInst, ref direct);
            thisInst.Attempt3DNavi = true;
            thisInst.AvoidStuff = true;

            if (mind.CommanderMind == EnemyAttitude.Homing && thisInst.lastEnemyGet.IsNotNull())
            {
                if ((thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).magnitude > mind.MaxCombatRange)
                {
                    bool isMending = RGeneral.LollyGag(thisInst, tank, mind, ref direct);
                    if (isMending)
                        return;
                }
            }
            if (thisInst.lastEnemyGet == null)
            {
                RGeneral.LollyGag(thisInst, tank, mind, ref direct);
                return;
            }
            RGeneral.Engadge(thisInst, tank, mind);

            float enemyExt = thisInst.lastEnemyGet.GetCheapBounds();
            float dist = thisInst.GetDistanceFromTask(thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck, enemyExt);
            float range = AIGlobals.MinCombatRangeDefault + thisInst.lastTechExtents;
            float spacer = thisInst.lastTechExtents + enemyExt;

            if (mind.MainFaction == FactionSubTypes.GC && mind.CommanderAttack != EAttackMode.Safety)
                spacer = -32;// ram no matter what, or get close for snipers

            direct.SetLastDest(thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck);
            if (mind.CommanderAttack == EAttackMode.Safety)
            {
                thisInst.AISetSettings.SideToThreat = false;
                thisInst.Retreat = true;
                direct.DriveAwayFacingAway();
                if (dist < spacer + (range / 4))
                {
                    if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                        thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                    else
                    {
                        thisInst.SettleDown();
                        thisInst.FullBoost = true;
                    }
                }
                else if (dist < spacer + range)
                {
                    if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                        thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                    else
                        thisInst.SettleDown();
                }
            }
            else if (mind.CommanderAttack == EAttackMode.Circle)
            {
                thisInst.AISetSettings.SideToThreat = true;
                thisInst.Retreat = RGeneral.CanRetreat(thisInst, tank, mind);
                if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                    thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                else
                {
                    thisInst.SettleDown();
                    if (dist < thisInst.lastTechExtents + enemyExt + 2)
                    {
                        direct.DriveAwayFacingPerp();
                        direct.SetLastDest(thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck);
                    }
                    else if (mind.MaxCombatRange < spacer + range)
                    {
                        direct.DriveToFacingPerp();
                        direct.SetLastDest(thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck);
                    }
                    else
                    {
                        direct.DriveToFacingPerp();
                        thisInst.FullBoost = true;
                    }
                }
            }
            else if (mind.CommanderAttack == EAttackMode.Ranged)
            {
                thisInst.AISetSettings.SideToThreat = true;
                thisInst.Retreat = RGeneral.CanRetreat(thisInst, tank, mind);
                if (dist < spacer + (range / 2))
                {
                    if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                        thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                    else
                    {
                        thisInst.SettleDown();
                        direct.DriveAwayFacingTowards();
                    }
                }
                else if (dist < spacer + range)
                {
                    thisInst.PivotOnly = true;
                }
                else if (dist < thisInst.lastTechExtents + enemyExt + (range * 2))
                {
                    if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                        thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                    else
                    {
                        thisInst.SettleDown();
                        direct.DriveToFacingPerp();
                    }
                }
                else
                {
                    if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                        thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                    else
                    {
                        thisInst.SettleDown();
                        thisInst.FullBoost = true;
                        direct.DriveToFacingPerp();
                    }
                }
            }
            else
            {
                thisInst.AISetSettings.SideToThreat = false;
                thisInst.Retreat = RGeneral.CanRetreat(thisInst, tank, mind);
                if (dist < spacer + 2)
                {
                    if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                        thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                    else
                    {
                        direct.DriveAwayFacingTowards();
                        thisInst.SettleDown();
                    }
                }
                else if (dist < spacer + range)
                {
                    thisInst.PivotOnly = true;
                    direct.DriveToFacingPerp();
                }
                else if (dist < spacer + (range * 1.25f))
                {
                    if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                        thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                    else
                    {
                        thisInst.SettleDown();
                        direct.DriveToFacingPerp();
                    }
                }
                else
                {
                    if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                        thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                    else
                    {
                        thisInst.SettleDown();
                        thisInst.FullBoost = true;
                        direct.DriveToFacingPerp();
                    }
                }
            }
        }
    }
}
