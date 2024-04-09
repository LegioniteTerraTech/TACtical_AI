using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using TAC_AI.Templates;

namespace TAC_AI.AI.Enemy
{
    public static class RBolts
    {
        internal static void ManageBolts(TankAIHelper thisInst, Tank tank, EnemyMind mind)
        {
            //if (tank.IsSleeping)
            //    return;
            switch (mind.CommanderBolts)
            {
                case EnemyBolts.MissionTrigger:  // do nothing - Mission tells us what to do!
                    break;
                //DO NOT CALL THE TWO BELOW WITHOUT EnemyMemory!!!  THEY WILL ACT LIKE DEFAULT BUT WORSE!!!
                case EnemyBolts.AtFull:         // Blow up passively at full health (or we are an area town base)
                    if (RLoadedBases.TeamGlobalMobileTechCount(tank.Team) < KickStart.EnemyTeamTechLimit && 
                        !thisInst.PendingDamageCheck && AIGlobals.CanSplitTech())
                        mind.BlowBolts();
                    break;
                case EnemyBolts.AtFullOnAggro:  // Blow up if enemy is in range and on full health
                    if (thisInst.lastEnemyGet.IsNotNull() && RLoadedBases.TeamGlobalMobileTechCount(tank.Team) < KickStart.EnemyTeamTechLimit && 
                        !thisInst.PendingDamageCheck && AIGlobals.CanSplitTech())
                        mind.BlowBolts();
                    break;
                case EnemyBolts.Default:        // Blow up like default - first enemy sighting
                default:                        // Unimplemented
                    if (thisInst.lastEnemyGet.IsNotNull() && AIGlobals.CanSplitTech())
                        mind.BlowBolts();
                    break;
            }
            if (mind.BoltsQueued > 0)
                mind.BoltsQueued--;
        }
        public static void BlowBolts(this EnemyMind mind)
        {
            if (AIGlobals.AtSceneTechMaxSpawnLimit())
                return; // world is too stressed to handle more
            if (mind.TechMemor)
            {
                mind.TechMemor.ReserveSuperGrabs = -256;
            }
            mind.BoltsQueued = 2;
            mind.AIControl.tank.control.ServerDetonateExplosiveBolt();
        }


        /*
        // Tech Accounting (OBSOLETE)
        private static readonly Dictionary<int, int> teamTechs = new Dictionary<int, int>();
        private static readonly List<int> teamUnfiltered = new List<int>();
        private static int lastTechsCount = 0;
        public static int AllyCostCountLegacy(int Team)
        {
            if (Singleton.Manager<ManTechs>.inst.CurrentTechs.Count() == lastTechsCount)
            {
                if (teamTechs.TryGetValue(Team, out int val))
                {
                    return val;
                }
            }
            return GetAllyCostCountsLegacy(Team);
        }
        public static int GetAllyCostCountsLegacy(int Team)
        {
            teamTechs.Clear();
            int AllyCount = 0;
            var allTechs = Singleton.Manager<ManTechs>.inst.CurrentTechs;
            int techCount = allTechs.Count();
            List<Tank> techs = allTechs.ToList();
            try
            {
                for (int stepper = 0; techCount > stepper; stepper++)
                {
                    Tank tech = techs.ElementAt(stepper);
                    teamUnfiltered.Add(Team);
                    if (!tech.IsAnchored)
                    {
                        teamUnfiltered.Add(Team);
                    }
                    if (tech.IsFriendly(Team))
                    {
                        AllyCount++;
                        if (!tech.IsAnchored)
                        {
                            AllyCount++;
                        }
                    }
                }
                foreach (int teamCase in teamUnfiltered)
                {
                    if (teamTechs.TryGetValue(teamCase, out int val))
                    {
                        continue;
                    }
                    int numOf = teamUnfiltered.FindAll(delegate (int cand) { return cand == teamCase; }).Count();
                    teamTechs.Add(teamCase, numOf);
                }
            }
            catch (Exception e)
            {
                DebugTAC_AI.Log(KickStart.ModID + ": AllyCostCount - Error on ally counting");
                DebugTAC_AI.Log(e);
            }
            lastTechsCount = techCount;
            teamUnfiltered.Clear();
            return AllyCount;
        }
        */
    }
}
