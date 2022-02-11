using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TAC_AI.AI;
using TAC_AI.Templates;

namespace TAC_AI.World
{
    // The enemy base in world-relations
    public class EnemyPresence
    {
        public int Team = 3;
        private float lastAttackedTimestep = 0;
        public List<EnemyBaseUnloaded> EBUs = new List<EnemyBaseUnloaded>();
        public List<EnemyTechUnit> ETUs = new List<EnemyTechUnit>();
        private bool eventHappening = false;
        public bool eventStarted = false;
        private IntVector2 lastEventTile;
        public List<IntVector2> scannedPositions = new List<IntVector2>();


        public EnemyPresence(int team)
        {
            Team = team;
        }

        public bool HasMobileETUs()
        {
            return ETUs.Exists(delegate (EnemyTechUnit cand) { return cand.MoveSpeed > 12; });
        }
        public bool UpdateGrandCommand()
        {
            if (Team == SpecialAISpawner.trollTeam)
            {
                HandleTraderTrolls();
                return EBUs.Count > 0 || ETUs.Count > 0;
            }
            if (EBUs.Count == 0)
            {
                Debug.Log("TACtical_AI: UpdateGrandCommand - Team " + Team + " has no bases");
                return false; // NO SUCH TEAM EXISTS (no base!!!)
            }
            PresenceDebug("TACtical_AI: UpdateGrandCommand - Turn for Team " + Team);
            eventStarted = false;
            //PresenceDebug("TACtical_AI: UpdateGrandCommand - Updating for team " + Team);
            UpdateRevenue();
            HandleUnitMoving();
            HandleCombat();
            EnemyBaseWorld.TryUnloadedBaseOperations(this);
            HandleRepairs();
            return EBUs.Count > 0 || ETUs.Count > 0;
        }
        private void HandleTraderTrolls()
        {
            int count = ETUs.Count;
            for (int step = 0; step < count;)
            {
                try
                {
                    EnemyTechUnit ETUcase = ETUs.ElementAt(step);
                    if ((ETUcase.tech.GetBackwardsCompatiblePosition() - Singleton.playerPos).sqrMagnitude > EnemyWorldManager.EnemyBaseCullingRangeSq)
                    {
                        EnemyBaseWorld.RemoteRemove(ETUcase);
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
            EnemyBaseUnloaded mainBase = EnemyBaseWorld.GetTeamFunder(this);
            if (mainBase == null)
            {
                //PresenceDebug("Team " + Team + " does not have a base allocated yet");
                foreach (EnemyTechUnit ETU in ETUs)
                {
                    if (ETU.MoveSpeed < 10)
                        continue;
                    if (!Singleton.Manager<ManWorld>.inst.CheckIsTileAtPositionLoaded(Singleton.Manager<ManWorld>.inst.TileManager.CalcTileCentreScene(ETU.tilePos)))
                    {
                        if (!ETU.isMoving)
                        {
                            EnemyWorldManager.StrategicMoveQueue(ETU, WorldPosition.FromGameWorldPosition(Singleton.cameraTrans.position).TileCoord);
                        }
                        else
                            PresenceDebug("Unit " + ETU.tech.m_TechData.Name + " is moving");
                    }
                }
            }
            else
            {
                EnemyBaseWorld.NaviFind(mainBase); // This happens first - home defense is more important
                IntVector2 eventTile = mainBase.tilePos;
                if (eventHappening)
                    eventTile = lastEventTile;
                else
                {
                    if (mainBase != null && !EnemySiege.InProgress && EnemyBaseWorld.IsPlayerWithinProvokeDist(mainBase.tilePos))
                    {
                        PresenceDebug("This team can attack the player!  Threshold: " + ETUs.Count + " / " + (KickStart.EnemyTeamTechLimit / 2f));
                        if (EnemySiege.LaunchSiege(this))
                            SetEvent(ManWorld.inst.TileManager.SceneToTileCoord(Singleton.playerTank.trans.position));
                    }
                }
                PresenceDebug("Main Base is " + mainBase.tech.m_TechData.Name + " at " + mainBase.tilePos + (eventHappening ? ", EventTile is " + eventTile : ""));
                bool techsMoving = false;
                foreach (EnemyTechUnit ETU in ETUs)
                {
                    //if (Singleton.Manager<ManWorld>.inst.TileManager.LookupTile(Singleton.Manager<ManWorld>.inst.TileManager.CalcTileCentreScene(ETU.tilePos)).m_LoadStep < WorldTile.LoadStep.Populated)
                    //{
                    if (!ETU.isMoving)
                    {
                        if (EnemyWorldManager.StrategicMoveQueue(ETU, eventTile))
                            techsMoving = true;
                    }
                    else
                    {
                        PresenceDebug("Unit " + ETU.tech.m_TechData.Name + " is moving");
                        techsMoving = true;
                    }
                    //}
                }
                if (!techsMoving)
                    eventHappening = false;
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
            float damageTime = EnemyWorldManager.UpdateDelay / EnemyWorldManager.ExpectedDPSDelitime;
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
                    List<EnemyTechUnit> techsCache = EnemyWorldManager.GetTechsInTile(TT);
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
                            if (Ally is EnemyBaseUnloaded)
                            {
                                strikePower += Math.Max(5, (int)(Ally.AttackPower * EnemyWorldManager.BaseCombatMulti));
                            }
                            else
                            {
                                strikePower += Math.Max(5, (int)(Ally.AttackPower * EnemyWorldManager.MobileCombatMulti));
                            }
                        }
                        int min = 5 * Allies.Count;
                        strikePower = (int)(UnityEngine.Random.Range(min, Math.Max(min, strikePower)) * damageTime);
                        if (target.TakeDamage(strikePower))
                        {
                            try
                            {
                                if (target is EnemyBaseUnloaded EBU)
                                {
                                    EnemyBaseWorld.EmergencyMoveMoney(EBU);
                                }
                                ReportCombat("Enemy " + target.tech.m_TechData.Name + " has been destroyed");
                            }
                            catch { }
                            EnemyBaseWorld.RemoteRemove(target);
                        }
                    }
                    else
                    {
                        //PresenceDebug("404 target not found");
                        EnemyBaseWorld.NaviFind(this, TT);
                    }

                }
                catch { }
            }
        }
        private void HandleRepairs()
        {
            EnemyBaseUnloaded funds = EnemyBaseWorld.GetTeamFunder(this);

            float healMulti;
            if (WasInCombat())
                healMulti = EnemyWorldManager.UpdateDelay / Mathf.Max(AIERepair.eDelayCombat, 1);
            else
                healMulti = EnemyWorldManager.UpdateDelay / Mathf.Max(AIERepair.eDelaySafe, 1);

            int numHealed = 0;
            if (funds != null)
            {
                foreach (EnemyBaseUnloaded EBU in EBUs)
                {
                    try
                    {
                        if (EBU.Health < EBU.MaxHealth)
                        {
                            if (funds.Funds > EnemyWorldManager.HealthRepairCost * healMulti)
                            {
                                funds.Funds = funds.Funds - (int)(EnemyWorldManager.HealthRepairCost * healMulti);
                                EBU.Health = Math.Min(EBU.MaxHealth, EBU.Health + (int)(EnemyWorldManager.HealthRepairRate * healMulti));
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
                            if (funds.Funds > EnemyWorldManager.HealthRepairCost * healMulti)
                            {
                                funds.Funds = funds.Funds - (int)(EnemyWorldManager.HealthRepairCost * healMulti);
                                ETU.Health = Math.Min(ETU.MaxHealth, ETU.Health + (int)(EnemyWorldManager.HealthRepairRate * healMulti));
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
            foreach (EnemyBaseUnloaded EBU in EBUs)
            {
                if (Singleton.Manager<ManVisible>.inst.GetTrackedVisible(EBU.tech.m_ID) == null)
                {
                    EBU.Funds += EBU.revenue;
                }
            }
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
            EnemyBaseUnloaded mainBase = EnemyBaseWorld.GetTeamFunder(this);
            if (mainBase != null)
            {
                lastEventTile = mainBase.tilePos;
            }
        }

        // MISC
        public static bool ignoreOut = false;
        public void PresenceDebug(string thing)
        {
            if (ignoreOut)
                return;
            Debug.Log(thing);
        }
        public static void ReportCombat(string thing)
        {
            Debug.Log("TACtical_AI: EnemyPresence - " + thing);
        }
        public bool WasInCombat()
        {
            return lastAttackedTimestep > Time.time;
        }
        public void OnCombat()
        {
            lastAttackedTimestep = Time.time + (EnemyWorldManager.UpdateDelay * 2);
        }
        public int GetBaseCount()
        {
            return EBUs.Count;
        }
        public int BuildBucks()
        {
            int count = 0;
            foreach (EnemyBaseUnloaded EBU in EBUs)
            {
                count += EBU.Funds;
            }
            return count;
        }
    }
}
