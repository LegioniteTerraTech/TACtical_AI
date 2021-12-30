using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TAC_AI.AI;

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

        public bool UpdateGrandCommand()
        {
            if (EBUs.Count < 0)
                return false; // NO SUCH TEAM EXISTS (no base!!!)
            eventStarted = false;
            //Debug.Log("TACtical_AI: UpdateGrandCommand - Updating for team " + Team);
            UpdateRevenue();
            HandleUnitMoving();
            HandleCombat();
            EnemyBaseWorld.TryUnloadedBaseOperations(this);
            HandleRepairs();
            return EBUs.Count > 0 || ETUs.Count > 0;
        }
        private void HandleUnitMoving()
        {
            scannedPositions.Clear();
            EnemyBaseUnloaded mainBase = EnemyBaseWorld.GetTeamFunder(this);
            if (mainBase == null)
            {
                //Debug.Log("TACtical_AI: EnemyPresence - Team " + Team + " does not have a base allocated yet");
                foreach (EnemyTechUnit ETU in ETUs)
                {
                    if (ETU.MoveSpeed < 10)
                        continue;
                    if (!Singleton.Manager<ManWorld>.inst.CheckIsTileAtPositionLoaded(Singleton.Manager<ManWorld>.inst.TileManager.CalcTileOriginScene(ETU.tilePos)))
                    {
                        if (!ETU.isMoving)
                        {
                            EnemyWorldManager.StrategicMoveQueue(ETU, WorldPosition.FromGameWorldPosition(Singleton.cameraTrans.position).TileCoord);
                        }
                    }
                }
            }
            else
            {
                EnemyBaseWorld.NaviFind(mainBase); // This happens first - home defense is more important
                IntVector2 eventTile = mainBase.tilePos;
                if (eventHappening)
                    eventTile = lastEventTile;
                foreach (EnemyTechUnit ETU in ETUs)
                {
                    if (!Singleton.Manager<ManWorld>.inst.CheckIsTileAtPositionLoaded(Singleton.Manager<ManWorld>.inst.TileManager.CalcTileOriginScene(ETU.tilePos)))
                    {
                        if (!ETU.isMoving)
                        {
                            EnemyWorldManager.StrategicMoveQueue(ETU, eventTile);
                        }
                    }
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
            float damageTime = EnemyWorldManager.UpdateDelay / EnemyWorldManager.ExpectedDPSDelitime;
            //Debug.Log("TACtical_AI: EnemyPresence - HandleCombat found " + tilesHasTechs.Count + " tiles with Techs");
            foreach (IntVector2 TT in tilesHasTechs)
            {
                try
                {
                    if (Singleton.Manager<ManWorld>.inst.CheckIsTileAtPositionLoaded(Singleton.Manager<ManWorld>.inst.TileManager.CalcTileOriginScene(TT)))
                    {
                        //Debug.Log("TACtical_AI: EnemyPresence - HandleCombat found the tile to be active!?");
                        continue; // tile loaded
                    }
                    //Debug.Log("TACtical_AI: EnemyPresence - HandleCombat Trying to test for combat");
                    List<EnemyTechUnit> techsCache = EnemyWorldManager.GetTechsInTile(TT);
                    if (techsCache.Count == 0)
                    {
                        //Debug.Log("TACtical_AI: EnemyPresence(ASSERT) - HandleCombat called the tile, but THERE'S NO TECHS IN THE TILE!");
                        continue;
                    }
                    EnemyTechUnit target = techsCache.Find(delegate (EnemyTechUnit cand) { return Tank.IsEnemy(cand.tech.m_TeamID, Team); });
                    if (target != null)
                    {
                        List<EnemyTechUnit> Allies = techsCache.FindAll(delegate (EnemyTechUnit cand) { return Tank.IsFriendly(cand.tech.m_TeamID, Team); });

                        Debug.Log("TACtical_AI: EnemyPresence - Combat underway at " + TT + " | " + Allies.First().tech.m_TeamID + " vs " + target.tech.m_TeamID);
                        OnCombat();
                        int strikePower = 1;
                        foreach (EnemyTechUnit Ally in Allies)
                        {
                            if (Ally is EnemyBaseUnloaded)
                            {
                                strikePower += Math.Max(5, Ally.AttackPower / 2);
                            }
                            else
                            {
                                strikePower += Math.Max(5, Ally.AttackPower);
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
                                Debug.Log("TACtical_AI: EnemyPresence - Enemy " + target.tech.m_TechData.Name + " has been destroyed");
                            }
                            catch { }
                            EnemyBaseWorld.RemoteRemove(target);
                        }
                    }
                    else
                    {
                        //Debug.Log("TACtical_AI: EnemyPresence - 404 target not found");
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
                    //Debug.Log("TACtical_AI: EnemyPresence - HandleRepairs Team " + Team + " repaired " + numHealed + "Techs");
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
            //Debug.Log("TACtical_AI: EnemyPresence - Enemy team " + Team + " has found target");
            eventStarted = true;
            eventHappening = true;
            lastEventTile = tilePos;
        }

        // MISC
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
