using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TAC_AI.AI;
using TAC_AI.AI.Enemy;
using TAC_AI.Templates;

namespace TAC_AI.World
{
    // The enemy base in world-relations
    public class EnemyPresence
    {
        public int team = AIGlobals.EnemyBaseTeamsStart;
        public int Team => team;
        private float lastAttackedTimestep = 0;
        internal EnemyTechUnit teamFounder;
        private Tank teamFounderActive;
        public List<EnemyBaseUnit> EBUs = new List<EnemyBaseUnit>();
        public List<EnemyTechUnit> ETUs = new List<EnemyTechUnit>();
        private bool eventHappening = false;
        public bool eventStarted = false;
        private IntVector2 lastEventTile;
        public List<IntVector2> scannedPositions = new List<IntVector2>();


        public EnemyPresence(int Team)
        {
            team = Team;
        }

        /// <summary>
        /// Returns false if the team should be removed
        /// </summary>
        /// <returns></returns>
        public bool UpdateGrandCommand()
        {
            if (Team == SpecialAISpawner.trollTeam)
            {
                HandleTraderTrolls();
                return EBUs.Count > 0 || ETUs.Count > 0;
            }
            if (GlobalMakerBaseCount() == 0)
            {
                DebugTAC_AI.Log("TACtical_AI: UpdateGrandCommand - Team " + Team + " has no production bases");
                return false; // NO SUCH TEAM EXISTS (no base!!!)
            }
            PresenceDebug("TACtical_AI: UpdateGrandCommand - Turn for Team " + Team);
            eventStarted = false;
            //PresenceDebug("TACtical_AI: UpdateGrandCommand - Updating for team " + Team);
            UpdateRevenue();
            HandleUnitMoving();
            HandleCombat();
            UnloadedBases.TryUnloadedBaseOperations(this);
            HandleRepairs();
            EnemyBaseUnit mainBase = UnloadedBases.GetTeamFunder(this);
            if (mainBase != null)
            {   // To make sure little bases are not totally stagnant - the AI is presumed to be mining aand doing missions
                PresenceDebugDEV("TACtical_AI: UpdateGrandCommand - Team final funds " + mainBase.BuildBucks);
            }
            return GlobalMakerBaseCount() > 0;
        }


        internal void ChangeTeamOfAllTechsUnloaded(int newTeam)
        {
            team = newTeam;
            foreach (var item in EBUs)
            {
                if (item.tech != null)
                    item.tech.m_TeamID = team;
            }
            foreach (var item in ETUs)
            {
                if (item.tech != null)
                    item.tech.m_TeamID = team;
            }
        }


        public bool HasMobileETUs()
        {
            return ETUs.Exists(delegate (EnemyTechUnit cand) { return cand.MoveSpeed > 12; });
        }

        private void HandleTraderTrolls()
        {
            int count = ETUs.Count;
            for (int step = 0; step < count;)
            {
                try
                {
                    EnemyTechUnit ETUcase = ETUs.ElementAt(step);
                    if ((ETUcase.tilePos - WorldPosition.FromScenePosition(Singleton.playerPos).TileCoord).WithinBox(ManEnemyWorld.EnemyBaseCullingExtents))
                    {
                        UnloadedBases.RemoteRemove(ETUcase);
                        count--;
                    }
                    else
                        step++;
                }
                catch { }
            }
        }
        private void HandleUnitMoving()
        {
            scannedPositions.Clear();
            EnemyBaseUnit mainBase = UnloadedBases.GetTeamFunder(this);
            if (mainBase == null)
            {
                //PresenceDebug("Team " + Team + " does not have a base allocated yet");
                int count = ETUs.Count;
                for (int step = 0; step < count; step++)
                {
                    EnemyTechUnit ETU = ETUs[step];
                    if (ETU.MoveSpeed < 10)
                        continue;
                    if (!Singleton.Manager<ManWorld>.inst.CheckIsTileAtPositionLoaded(Singleton.Manager<ManWorld>.inst.TileManager.CalcTileCentreScene(ETU.tilePos)))
                    {
                        if (!ETU.isMoving)
                        {
                            ManEnemyWorld.StrategicMoveQueue(ETU, WorldPosition.FromGameWorldPosition(Singleton.cameraTrans.position).TileCoord, out bool fail);
                            if (fail)
                            {
                                ETUs.RemoveAt(step);
                                step--;
                                count--;
                            }
                        }
                        else
                            PresenceDebug("Unit " + ETU.Name + " is moving");
                    }
                }
            }
            else
            {
                UnloadedBases.NaviFind(mainBase); // This happens first - home defense is more important
                IntVector2 eventTile = mainBase.tilePos;
                if (eventHappening)
                    eventTile = lastEventTile;
                else
                {
                    if (mainBase != null && !ManEnemySiege.InProgress && AIGlobals.IsEnemyBaseTeam(Team) && UnloadedBases.IsPlayerWithinProvokeDist(mainBase.tilePos))
                    {
                        PresenceDebug("This team can attack your base!  Threshold: " + ETUs.Count + " / " + (KickStart.EnemyTeamTechLimit / 2f));
                        if (ManEnemySiege.LaunchSiege(this))
                            SetEvent(ManWorld.inst.TileManager.SceneToTileCoord(Singleton.playerTank.trans.position));
                    }
                }
                PresenceDebugDEV("Main Base is " + mainBase.Name + " at " + mainBase.tilePos + (eventHappening ? ", EventTile is " + eventTile : ""));
                if (AIECore.RetreatingTeams.Contains(team))
                {
                    if (MoveAllETUsToDest(mainBase.tilePos))
                        eventHappening = false;
                }
                else
                {
                    if (MoveAllETUsToDest(eventTile))
                        eventHappening = false;
                }
            }
        }
        private void HandleCombat()
        {
            List<IntVector2> tilesHasTechs = new List<IntVector2>();
            foreach (EnemyTechUnit ETU in ETUs)
            {
                try
                {
                    IntVector2 techTilePos = ETU.tilePos;
                    if (!tilesHasTechs.Exists(delegate (IntVector2 pos) { return techTilePos.x == pos.x && techTilePos.y == pos.y; }))
                    {
                        tilesHasTechs.Add(techTilePos);
                    }
                }
                catch { }
            }
            float damageTime = ManEnemyWorld.UpdateDelay / ManEnemyWorld.ExpectedDPSDelitime;
            //PresenceDebug("HandleCombat found " + tilesHasTechs.Count + " tiles with Techs");
            foreach (IntVector2 TT in tilesHasTechs)
            {
                try
                {
                    if (Singleton.Manager<ManWorld>.inst.CheckIsTileAtPositionLoaded(Singleton.Manager<ManWorld>.inst.TileManager.CalcTileCentreScene(TT)))
                    {
                        //PresenceDebug("HandleCombat found the tile to be active!?");
                        continue; // tile loaded
                    }
                    //PresenceDebug("HandleCombat Trying to test for combat");
                    List<EnemyTechUnit> techsCache = ManEnemyWorld.GetTechsInTile(TT);
                    if (techsCache.Count == 0)
                    {
                        //PresenceDebug("TACtical_AI: EnemyPresence(ASSERT) - HandleCombat called the tile, but THERE'S NO TECHS IN THE TILE!");
                        continue;
                    }
                    EnemyTechUnit target = techsCache.Find(delegate (EnemyTechUnit cand) { return Tank.IsEnemy(cand.tech.m_TeamID, Team); });
                    if (target != null)
                    {
                        List<EnemyTechUnit> Allies = techsCache.FindAll(delegate (EnemyTechUnit cand) { return Tank.IsFriendly(cand.tech.m_TeamID, Team); });

                        ReportCombat("Combat underway at " + TT + " | " + Allies.First().tech.m_TeamID + " vs " + target.tech.m_TeamID);
                        OnCombat();
                        int strikePower = 1;
                        foreach (EnemyTechUnit Ally in Allies)
                        {
                            if (Ally is EnemyBaseUnit)
                            {
                                strikePower += Math.Max(5, (int)(Ally.AttackPower * ManEnemyWorld.BaseCombatMulti));
                            }
                            else
                            {
                                strikePower += Math.Max(5, (int)(Ally.AttackPower * ManEnemyWorld.MobileCombatMulti));
                            }
                        }
                        int min = 5 * Allies.Count;
                        strikePower = (int)(UnityEngine.Random.Range(min, Math.Max(min, strikePower)) * damageTime);
                        if (target.TakeDamage(strikePower))
                        {
                            try
                            {
                                if (target is EnemyBaseUnit EBU)
                                {
                                    UnloadedBases.EmergencyMoveMoney(EBU);
                                }
                                ReportCombat("Enemy " + target.Name + " has been destroyed!");
                            }
                            catch { }
                            UnloadedBases.RemoteDestroy(target);
                        }
                    }
                    else
                    {
                        //PresenceDebug("404 target not found");
                        UnloadedBases.NaviFind(this, TT);
                    }

                }
                catch { }
            }
        }
        private void HandleRepairs()
        {
            EnemyBaseUnit funds = UnloadedBases.GetTeamFunder(this);

            float healMulti;
            if (WasInCombat())
                healMulti = ManEnemyWorld.UpdateDelay / Mathf.Max(AIERepair.eDelayCombat, 1);
            else
                healMulti = ManEnemyWorld.UpdateDelay / Mathf.Max(AIERepair.eDelaySafe, 1);

            int numHealed = 0;
            if (funds != null)
            {
                foreach (EnemyBaseUnit EBU in EBUs)
                {
                    try
                    {
                        if (EBU.Health < EBU.MaxHealth)
                        {
                            if (funds.BuildBucks > ManEnemyWorld.HealthRepairCost * healMulti)
                            {
                                funds.BuildBucks -= (int)(ManEnemyWorld.HealthRepairCost * healMulti);
                                EBU.Health = Math.Min(EBU.MaxHealth, EBU.Health + (int)(ManEnemyWorld.HealthRepairRate * healMulti));
                                numHealed++;
                            }
                        }
                    }
                    catch { }
                }
                foreach (EnemyTechUnit ETU in ETUs)
                {
                    try
                    {
                        if (ETU.Health < ETU.MaxHealth)
                        {
                            if (funds.BuildBucks > ManEnemyWorld.HealthRepairCost * healMulti)
                            {
                                funds.BuildBucks -= (int)(ManEnemyWorld.HealthRepairCost * healMulti);
                                ETU.Health = Math.Min(ETU.MaxHealth, ETU.Health + (int)(ManEnemyWorld.HealthRepairRate * healMulti));
                                numHealed++;
                            }
                        }
                    }
                    catch { }
                }
                if (numHealed > 0)
                {
                    //PresenceDebug("HandleRepairs Team " + Team + " repaired " + numHealed + "Techs");
                }
            }
        }
        public void UpdateRevenue()
        {
            foreach (EnemyBaseUnit EBU in EBUs)
            {
                if (Singleton.Manager<ManVisible>.inst.GetTrackedVisible(EBU.tech.m_ID) == null)
                {
                    EBU.BuildBucks += EBU.revenue + ManEnemyWorld.ExpansionIncome;
                }
            }
            EnemyBaseUnit mainBase = UnloadedBases.GetTeamFunder(this);

            if (mainBase != null)
            {   // To make sure little bases are not totally stagnant - the AI is presumed to be mining aand doing missions
                mainBase.BuildBucks += ManEnemyWorld.PassiveHQBonusIncome * ManEnemyWorld.UpdateDelay;
                if (AIGlobals.TurboAICheat)
                {
                    mainBase.BuildBucks += 25000 * ManEnemyWorld.UpdateDelay;
                }
            }
        }

        /// <summary>
        /// WORK IN PROGRESS
        /// </summary>
        /// <param name="ETU"></param>
        /// <param name="techsMoving"></param>
        private void HandleFounderActions(EnemyTechUnit ETU, ref bool techsMoving)
        {
            //ETU.
        }

        public bool AddBuildBucks(int add)
        {
            EnemyBaseUnit EBU = UnloadedBases.GetTeamFunder(this);
            if (EBU != null)
            {
                EBU.BuildBucks += add;
                return true;
            }
            return false;
        }

        public void SetEvent(IntVector2 tilePos)
        {
            //PresenceDebug("Enemy team " + Team + " has found target");
            eventStarted = true;
            eventHappening = true;
            lastEventTile = tilePos;
        }
        public void ResetEvent()
        {
            eventHappening = false;
            EnemyBaseUnit mainBase = UnloadedBases.GetTeamFunder(this);
            if (mainBase != null)
            {
                lastEventTile = mainBase.tilePos;
            }
        }

        // Movement
        /// <summary>
        /// 
        /// </summary>
        /// <param name="eventTile"></param>
        /// <returns>True if a tech is moving</returns>
        private bool MoveAllETUsToDest(IntVector2 eventTile)
        {
            bool techsMoving = false;
            int count = ETUs.Count;
            for (int step = 0; step < count; step++)
            {
                EnemyTechUnit ETU = ETUs[step];
                if (ETU.tilePos == eventTile)
                    continue;
                if (!ETU.isMoving)
                {
                    if (ETU.isFounder)
                    {
                        if (eventHappening)
                        {   // Base is under attack
                            if (ManEnemyWorld.StrategicMoveQueue(ETU, eventTile, out bool fail))
                            {
                                //techsMoving = true;
                            }
                            if (fail)
                            {
                                ETUs.RemoveAt(step);
                                step--;
                                count--;
                            }
                            techsMoving = true;
                        }
                        else
                        {   // Do random things
                            //HandleFounderActions();
                        }
                    }
                    else
                    {
                        if (ManEnemyWorld.StrategicMoveQueue(ETU, eventTile, out bool fail))
                            techsMoving = true;
                        if (fail)
                        {
                            ETUs.RemoveAt(step);
                            step--;
                            count--;
                        }
                    }
                }
                else
                {
                    PresenceDebug("Unit " + ETU.Name + " is moving!");
                    techsMoving = true;
                }
            }
            return techsMoving;
        }



        /*
        // TEAMS
        /// <summary>
        /// Changes all Techs in a team (non-vanilla ranges)
        /// </summary>
        /// <param name="newTeamID"></param>
        public void ChangeEntireTeam(int newTeamID)
        {
            if (newTeamID < KickStart.TeamRangeStart && newTeamID > -KickStart.TeamRangeStart)
                return;
            //foreach
        }*/

        // MISC
        public void PresenceDebug(string thing)
        {
//#if DEBUG
            Debug.Log(thing);
//#endif
        }
        public void PresenceDebugDEV(string thing)
        {
//#if DEBUG
            Debug.Log(thing);
//#endif
        }
        public static void ReportCombat(string thing)
        {
            DebugTAC_AI.Log("TACtical_AI: EnemyPresence - " + thing);
        }
        public bool WasInCombat()
        {
            return lastAttackedTimestep > Time.time;
        }
        public void OnCombat()
        {
            lastAttackedTimestep = Time.time + (ManEnemyWorld.UpdateDelay * 2);
        }

        public int BuildBucks()
        {
            int count = 0;
            foreach (EnemyBaseUnit EBU in EBUs)
            {
                count += EBU.BuildBucks;
            }
            return count;
        }

        public bool TryGetFounderUnloaded(out EnemyTechUnit ETUFounder)
        {
            ETUFounder = null;
            if (!teamFounder.IsNullOrTechMissing())
            {
                ETUFounder = teamFounder;
                return true;
            }
            else
            {
                foreach (var item in ETUs)
                {
                    if (!item.IsNullOrTechMissing())
                    {
                        TechData tech = item.tech.m_TechData;
                        if (tech.IsTeamFounder())
                        {
                            teamFounder = item;
                            ETUFounder = item;
                            break;
                        }
                    }
                }
                if (!teamFounder.IsNullOrTechMissing())
                {
                    ETUs.Remove(teamFounder);
                    return true;
                }
                return false;
            }
        }
        public bool TryGetFounderActive(out Tank founderActive)
        {
            founderActive = null;
            if (teamFounderActive != null)
            {
                founderActive = teamFounderActive;
                return true;
            }
            else
            {
                foreach (var item in ManTechs.inst.IterateTechs())
                {
                    if (item.Team == Team && item.IsTeamFounder())
                    {
                        teamFounderActive = item;
                        founderActive = item;
                        return true;
                    }
                }
                return false;
            }
        }

        public bool HasANYTechs()
        {
            return EBUs.Count > 0 || ETUs.Count > 0 || RBases.TeamActiveMobileTechCount(Team) > 0 || RBases.TeamActiveAnyBaseCount(Team) > 0;
        }
        public int GlobalTotalTechCount()
        {
            return GlobalMakerBaseCount() + GlobalMobileTechCount();
        }
        public int GlobalMakerBaseCount()
        {
            return EBUs.Count + RBases.TeamActiveMakerBaseCount(Team);
        }
        public int GlobalMobileTechCount()
        {
            return ETUs.Count + RBases.TeamActiveMobileTechCount(Team);
        }
    }
}
