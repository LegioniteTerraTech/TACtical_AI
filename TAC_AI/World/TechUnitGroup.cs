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
        int Team;
        Func<Vector3, Vector3> PositionTweak = ManPlayerRTS.GetPlayerTargetOffset;

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
                    foreach (TankAIHelper help in this)
                    {
                        if (help != null)
                        {
                            help.RTSDestination = TankAIHelper.RTSDisabled;
                            help.SetRTSState(true);
                            if (ManNetwork.IsNetworked)
                                NetworkHandler.TryBroadcastRTSAttack(help.tank.netTech.netId.Value, grabbedTech.netTech.netId.Value);
                            help.lastEnemy = grabbedTech.visible;
                            ManPlayerRTS.inst.TechMovementQueue.Remove(help);
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
                            foreach (TankAIHelper help in this)
                            {
                                if (help != null)
                                {
                                    help.RTSDestination = TankAIHelper.RTSDisabled;
                                    help.SetRTSState(false);
                                    if (!ManNetwork.IsNetworked)
                                    {
                                        help.foundBase = false;
                                        help.CollectedTarget = false;
                                    }
                                    ManPlayerRTS.inst.TechMovementQueue.Remove(help);
                                    success = true;
                                }
                            }
                        }
                        else
                        {
                            foreach (TankAIHelper help in this)
                            {
                                if (help != null)
                                {
                                    //bool LandAIAssigned = help.DediAI < AIType.MTTurret;
                                    help.RTSDestination = TankAIHelper.RTSDisabled;
                                    help.SetRTSState(false);
                                    if (!ManNetwork.IsNetworked)
                                    {
                                        help.lastCloseAlly = grabbedTech;
                                        help.theResource = grabbedTech.visible;
                                        help.CollectedTarget = false;
                                    }
                                    ManPlayerRTS.inst.TechMovementQueue.Remove(help);
                                    success = true;
                                }
                            }
                        }
                    }
                    catch
                    {
                        DebugTAC_AI.Log(KickStart.ModID + ": Error on Protect/Defend - Techs");
                        foreach (TankAIHelper help in this)
                        {
                            DebugTAC_AI.Log(KickStart.ModID + ": " + help.name);
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
            foreach (TankAIHelper help in this)
            {
                if (help != null)
                {
                    help.RTSDestination = terrainPoint;
                    ManPlayerRTS.inst.TechMovementQueue.Remove(help);
                    if (help.lastAIType != AITreeType.AITypes.Escort)
                        help.ForceAllAIsToEscort(true, false);
                    help.SetRTSState(true);
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
                    foreach (TankAIHelper help in this)
                    {
                        if (help != null)
                        {
                            ManPlayerRTS.inst.SetOptionAuto(help, AIType.Prospector);
                            help.RTSDestination = TankAIHelper.RTSDisabled;
                            help.SetRTSState(false);
                            if (!ManNetwork.IsNetworked)
                            {
                                help.theResource = node.visible;
                                help.CollectedTarget = false;
                            }
                            ManPlayerRTS.inst.TechMovementQueue.Remove(help);
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
                foreach (TankAIHelper help in this)
                {
                    if (help != null)
                    {
                        ManPlayerRTS.inst.SetOptionAuto(help, AIType.Scrapper);
                        help.RTSDestination = TankAIHelper.RTSDisabled;
                        help.SetRTSState(false);
                        if (!ManNetwork.IsNetworked)
                        {
                            help.theResource = block.visible;
                            help.CollectedTarget = false;
                        }
                        success = true;
                        ManPlayerRTS.inst.TechMovementQueue.Remove(help);
                    }
                }
            }
            return success;
        }


        public bool StartControlling(TankAIHelper TechUnit)
        {
            if (TechUnit.tank.netTech?.NetPlayer)
            {
                if (TechUnit.tank.netTech.NetPlayer != ManNetwork.inst.MyPlayer)
                    return false;// cannot grab other player tech
            }
            Add(TechUnit);
            ManPlayerRTS.dirty = true;
            return true;
        }
        public bool StopControlling(TankAIHelper TechUnit)
        {
            if (TechUnit.tank.netTech?.NetPlayer)
            {
                return false;// cannot grab other player tech
            }
            Remove(TechUnit);
            ManPlayerRTS.dirty = true;
            return true;
        }
    }
}
