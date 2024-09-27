using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using TerraTechETCUtil;
using TAC_AI.AI;
using TAC_AI.World;

namespace TAC_AI
{
    /// <summary>
    /// Handles all NON-PLAYER Techs
    /// </summary>
    public class TechUnitGroup : ListHashSet<TankAIHelper>
    {
        public int Team;
        Func<Vector3, Vector3> PositionTweak = ManWorldRTS.GetPlayerTargetOffset;

        public void HandleSelection(Visible vis)
        {
            if (vis?.resdisp)
                HandleSelectScenery(vis.resdisp);
            else if (vis?.tank)
                HandleSelectTargetTank(vis.tank);
            else if (vis?.block)
                HandleSelectBlock(vis.block);
        }
        public bool HandleSelection(Vector3 point, Visible vis)
        {
            if (vis?.resdisp)
                return HandleSelectScenery(vis.resdisp);
            else if (vis?.tank)
                return HandleSelectTargetTank(vis.tank);
            else if (vis?.block)
                return HandleSelectBlock(vis.block);
            else
                return HandleSelectTerrain(point);
        }
        public bool HandleSelectTargetTank(Tank grabbedTech)
        {
            //DebugTAC_AI.Log(KickStart.ModID + ": HandleSelectTargetTank.");
            bool success = false;
            if ((bool)grabbedTech)
            {
                if (grabbedTech.IsEnemy(Team))
                {   // Attack Move
                    foreach (TankAIHelper helper in this)
                    {
                        if (helper != null)
                        {
                            helper.RTSDestination = TankAIHelper.RTSDisabled;
                            helper.SetRTSState(true);
                            if (ManNetwork.IsNetworked)
                                NetworkHandler.TryBroadcastRTSAttack(helper.tank.netTech.netId.Value, grabbedTech.netTech.netId.Value);
                            helper.lastEnemy = grabbedTech.visible;
                            ManWorldRTS.inst.TechMovementQueue.Remove(helper);
                            success = true;
                        }
                    }
                }
                else if (grabbedTech.IsFriendly(Team))
                {// Protect/Defend
                    try
                    {
                        if (grabbedTech.IsAnchored)
                        {
                            foreach (TankAIHelper helper in this)
                            {
                                if (helper != null)
                                {
                                    helper.RTSDestination = TankAIHelper.RTSDisabled;
                                    helper.SetRTSState(false);
                                    if (!ManNetwork.IsNetworked)
                                    {
                                        helper.foundBase = false;
                                        helper.CollectedTarget = false;
                                    }
                                    ManWorldRTS.inst.TechMovementQueue.Remove(helper);
                                    success = true;
                                }
                            }
                        }
                        else
                        {
                            foreach (TankAIHelper helper in this)
                            {
                                if (helper != null)
                                {
                                    //bool LandAIAssigned = help.DediAI < AIType.MTTurret;
                                    helper.RTSDestination = TankAIHelper.RTSDisabled;
                                    helper.SetRTSState(false);
                                    if (!ManNetwork.IsNetworked)
                                    {
                                        helper.lastCloseAlly = grabbedTech;
                                        helper.theResource = grabbedTech.visible;
                                        helper.CollectedTarget = false;
                                    }
                                    ManWorldRTS.inst.TechMovementQueue.Remove(helper);
                                    success = true;
                                }
                            }
                        }
                    }
                    catch
                    {
                        DebugTAC_AI.Log(KickStart.ModID + ": Error on Protect/Defend - Techs");
                        foreach (TankAIHelper helper in this)
                        {
                            DebugTAC_AI.Log(KickStart.ModID + ": " + helper.name);
                        }
                    }
                }
            }
            return success;
        }
        public bool HandleSelectTerrain(Vector3 point)
        {
            bool success = false;
            Vector3 terrainPoint = PositionTweak(point);
            foreach (TankAIHelper helper in this)
            {
                if (helper != null)
                {
                    helper.RTSDestination = terrainPoint;
                    ManWorldRTS.inst.TechMovementQueue.Remove(helper);
                    if (helper.lastAIType != AITreeType.AITypes.Escort)
                        helper.ForceAllAIsToEscort(true, false);
                    helper.SetRTSState(true);
                    success = true;
                }
            }
            //DebugTAC_AI.Log(KickStart.ModID + ": HandleSelectTerrain.");
            return success;
        }
        public bool HandleSelectScenery(ResourceDispenser node)
        {
            //DebugTAC_AI.Log(KickStart.ModID + ": HandleSelectScenery.");

            bool success = false;
            if ((bool)node)
            {
                if (!node.GetComponent<Damageable>().Invulnerable)
                {   // Mine Move
                    foreach (TankAIHelper helper in this)
                    {
                        if (helper != null)
                        {
                            ManWorldRTS.inst.SetOptionAuto(helper, AIType.Prospector);
                            helper.RTSDestination = TankAIHelper.RTSDisabled;
                            helper.SetRTSState(false);
                            if (!ManNetwork.IsNetworked)
                            {
                                helper.theResource = node.visible;
                                helper.CollectedTarget = false;
                            }
                            ManWorldRTS.inst.TechMovementQueue.Remove(helper);
                            success = true;
                        }
                    }
                }
            }
            return success;
        }
        public bool HandleSelectBlock(TankBlock block)
        {
            //DebugTAC_AI.Log(KickStart.ModID + ": HandleSelectBlock.");

            bool success = false;
            if ((bool)block)
            {
                foreach (TankAIHelper helper in this)
                {
                    if (helper != null)
                    {
                        ManWorldRTS.inst.SetOptionAuto(helper, AIType.Scrapper);
                        helper.RTSDestination = TankAIHelper.RTSDisabled;
                        helper.SetRTSState(false);
                        if (!ManNetwork.IsNetworked)
                        {
                            helper.theResource = block.visible;
                            helper.CollectedTarget = false;
                        }
                        success = true;
                        ManWorldRTS.inst.TechMovementQueue.Remove(helper);
                    }
                }
            }
            return success;
        }


        public bool StartControlling(TankAIHelper helper)
        {
            if (helper.tank.netTech?.NetPlayer)
            {
                if (helper.tank.netTech.NetPlayer != ManNetwork.inst.MyPlayer)
                    return false;// cannot grab other player tech
            }
            Add(helper);
            ManWorldRTS.dirty = true;
            return true;
        }
        public bool StopControlling(TankAIHelper helper)
        {
            if (helper.tank.netTech?.NetPlayer)
            {
                return false;// cannot grab other player tech
            }
            Remove(helper);
            ManWorldRTS.dirty = true;
            return true;
        }
    }
}
