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
    /// Handles NON-PLAYER Techs.  Needs to be reset and recollected each save reload.  Also does not maintain connections with Techs that are unloaded or destroyed.
    /// </summary>
    public class TechUnitGroup : ListHashSet<TankAIHelper>
    {
        public Func<int> Team;
        public bool PlaySFX;
        Func<Vector3, Vector3> PositionTweak = null;
        public TechUnitGroup(Func<Vector3, Vector3> coordCorrection, bool playSFX)
        {
            PositionTweak = coordCorrection;
            PlaySFX = playSFX;
        }

        /// <summary>
        /// true values move to the front
        /// </summary>
        /// <param name="func"></param>
        /// <returns></returns>
        public TechUnitGroup ReorderBy(Func<TankAIHelper, bool> func)
        {
            this.InsertionSort((x) => func(x) ? 1 : 0);
            return this;
        }
        /// <summary>
        /// true values move to the back
        /// </summary>
        /// <param name="func"></param>
        /// <returns></returns>
        public TechUnitGroup ReorderByDescending(Func<TankAIHelper, bool> func)
        {
            this.InsertionSort((x) => func(x) ? 0 : 1);
            return this;
        }
        public TechUnitGroup ReorderByDescending(Func<TankAIHelper, int> func)
        {
            this.InsertionSort((x) => -func(x));
            return this;
        }

        public bool StartControlling(TankAIHelper helper)
        {
            if (helper.tank.netTech?.NetPlayer)
            {
                if (helper.tank.netTech.NetPlayer != ManNetwork.inst.MyPlayer)
                    return false;// cannot grab other player tech
            }
            Add(helper);
            ManWorldRTS.dirtyLocalPlayer = true;
            return true;
        }
        public bool StopControlling(TankAIHelper helper)
        {
            if (helper.tank.netTech?.NetPlayer)
            {
                return false;// cannot grab other player tech
            }
            Remove(helper);
            ManWorldRTS.dirtyLocalPlayer = true;
            return true;
        }

        public bool HandleSelection(Vector3 cmdTargPoint, bool stackCommands)
        {
            return HandleSelection(cmdTargPoint, null, stackCommands);
        }
        public bool HandleSelection(Visible cmdTargVis, bool stackCommands)
        {
            if (cmdTargVis != null && cmdTargVis.isActive)
            {
                HandleSelection(cmdTargVis.centrePosition, cmdTargVis, stackCommands);
                return true;
            }
            return false;
        }
        public bool HandleSelection(Vector3 cmdTargPoint, Visible cmdTargVis, bool stackCommands)
        {
            if (cmdTargVis?.resdisp)
                return HandleSelectScenery(cmdTargPoint, cmdTargVis, stackCommands);
            else if (cmdTargVis?.block)
                return HandleSelectTargetTank(cmdTargPoint, cmdTargVis, stackCommands);
            else
                return HandleSelectTerrain(cmdTargPoint, stackCommands);
        }
        private bool HandleSelectTargetTank(Vector3 cmdTargPoint, Visible cmdTargVis, bool stackCommands)
        {
            //DebugTAC_AI.Log(KickStart.ModID + ": HandleSelectTargetTank.");
            Tank cmdTargTech = cmdTargVis.block.tank;
            if ((bool)cmdTargTech)
            {
                bool responded = false;
                if (cmdTargTech.IsEnemy(ManPlayer.inst.PlayerTeam))
                {   // Attack Move
                    foreach (TankAIHelper helper in this)
                    {
                        if (helper != null)
                        {
                            if (helper.lastAIType != AITreeType.AITypes.Escort)
                                helper.WakeAIForChange(true);
                            if (stackCommands)
                            {
                                ManWorldRTS.inst.QueueNextCommand(helper, cmdTargTech.visible, AIType.Null);
                            }
                            else
                            {
                                helper.RTSDestination = TankAIHelper.RTSDisabled;
                                helper.SetRTSState(true);
                                if (ManNetwork.IsNetworked)
                                    NetworkHandler.TryBroadcastRTSAttack(helper.tank.netTech.netId.Value, cmdTargTech.netTech.netId.Value);
                                helper.lastEnemy = cmdTargTech.visible;
                                ManWorldRTS.inst.TechMovementQueue.Remove(helper.tank.visible.ID);
                            }
                            responded = true;
                        }
                    }
                    if (PlaySFX && responded)
                        Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.LockOn);
                }
                else if (cmdTargTech.IsFriendly(ManPlayer.inst.PlayerTeam))
                {
                    if (cmdTargTech.IsPlayer)
                    {   // Reset to working order
                        foreach (TankAIHelper helper in this)
                        {
                            if (helper != null)
                            {
                                if (helper == cmdTargTech)
                                {// We are selecting ourselves, we just stay put 
                                    if (stackCommands)
                                    {
                                        ManWorldRTS.inst.QueueNextCommand(helper, PositionTweak(cmdTargPoint));
                                    }
                                    else
                                    {
                                        helper.RTSDestination = PositionTweak(cmdTargPoint);
                                        ManWorldRTS.inst.TechMovementQueue.Remove(helper.tank.visible.ID);
                                        if (helper.lastAIType != AITreeType.AITypes.Escort)
                                            helper.WakeAIForChange(true);
                                        helper.SetRTSState(true);
                                    }
                                }
                                else
                                {
                                    if (stackCommands)
                                    {
                                        ManWorldRTS.inst.QueueNextCommand(helper, cmdTargTech.visible, AIType.Escort);
                                    }
                                    else
                                    {
                                        ManWorldRTS.inst.TechMovementQueue.Remove(helper.tank.visible.ID);
                                        ManWorldRTS.inst.SetOptionAuto(helper, AIType.Escort);
                                        helper.RTSDestination = TankAIHelper.RTSDisabled;
                                        helper.SetRTSState(false);
                                        if (!ManNetwork.IsNetworked)
                                            helper.lastPlayer = cmdTargTech.visible;
                                    }
                                }
                                responded = true;
                            }
                        }
                    }
                    else
                    {   // Protect/Defend
                        try
                        {
                            if (cmdTargTech.IsAnchored)
                            {
                                foreach (TankAIHelper helper in this)
                                {
                                    if (helper != null)
                                    {
                                        if (helper.isAssassinAvail)
                                        {
                                            if (stackCommands)
                                            {
                                                ManWorldRTS.inst.QueueNextCommand(helper, cmdTargTech.visible, AIType.Assault);
                                            }
                                            else
                                            {
                                                ManWorldRTS.inst.SetOptionAuto(helper, AIType.Assault);
                                                helper.RTSDestination = TankAIHelper.RTSDisabled;
                                                helper.SetRTSState(false);
                                                if (!ManNetwork.IsNetworked)
                                                {
                                                    helper.foundBase = false;
                                                    helper.CollectedTarget = false;
                                                }
                                                ManWorldRTS.inst.TechMovementQueue.Remove(helper.tank.visible.ID);
                                            }
                                        }
                                        else
                                        {
                                            if (stackCommands)
                                            {
                                            }
                                            else
                                            {
                                                if (helper.lastAIType != AITreeType.AITypes.Escort)
                                                    helper.WakeAIForChange(true);
                                                helper.RTSDestination = cmdTargTech.boundsCentreWorldNoCheck;
                                                helper.SetRTSState(true);
                                                ManWorldRTS.inst.TechMovementQueue.Remove(helper.tank.visible.ID);
                                            }
                                        }
                                        responded = true;
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
                                        if (helper.isAegisAvail)// && LandAIAssigned)
                                        {
                                            if (stackCommands)
                                            {
                                                ManWorldRTS.inst.QueueNextCommand(helper, cmdTargTech.visible, AIType.Aegis);
                                            }
                                            else
                                            {
                                                ManWorldRTS.inst.SetOptionAuto(helper, AIType.Aegis);
                                                helper.RTSDestination = TankAIHelper.RTSDisabled;
                                                helper.SetRTSState(false);
                                                if (!ManNetwork.IsNetworked)
                                                {
                                                    helper.lastCloseAlly = cmdTargTech;
                                                    helper.theResource = cmdTargTech.visible;
                                                    helper.CollectedTarget = false;
                                                }
                                                ManWorldRTS.inst.TechMovementQueue.Remove(helper.tank.visible.ID);
                                            }
                                        }
                                        else
                                        {
                                            if (stackCommands)
                                            {
                                            }
                                            else
                                            {
                                                if (helper.lastAIType != AITreeType.AITypes.Escort)
                                                    helper.WakeAIForChange(true);
                                                helper.RTSDestination = cmdTargTech.boundsCentreWorldNoCheck;
                                                helper.SetRTSState(true);
                                                ManWorldRTS.inst.TechMovementQueue.Remove(helper.tank.visible.ID);
                                            }
                                        }
                                        responded = true;
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
                    if (PlaySFX && responded)
                        Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.AIFollow);
                }
                else
                    return HandleSelectTerrain(cmdTargPoint, stackCommands);
                return responded;
            }
            else
            {
                return HandleSelectBlock(cmdTargPoint, cmdTargVis, stackCommands);
            }
        }
        private bool HandleSelectTerrain(Vector3 cmdTargPoint, bool stackCommands)
        {
            //DebugTAC_AI.Log(KickStart.ModID + ": HandleSelectTerrain. - " + StackTraceUtility.ExtractStackTrace());
            Vector3 cmdTargPointTerrain = PositionTweak(cmdTargPoint);
            if (!stackCommands && this.Count == 1)
            {
                TankAIHelper helper = this.FirstOrDefault();
                if (helper != null)
                {
                    helper.RTSDestination = cmdTargPointTerrain;
                    ManWorldRTS.inst.TechMovementQueue.Remove(helper.tank.visible.ID);
                    ManWorldRTS.inst.QueueNextCommand(helper, cmdTargPointTerrain);// INSURE direct path to position
                    if (helper.lastAIType != AITreeType.AITypes.Escort)
                        helper.WakeAIForChange(true);
                    helper.SetRTSState(true);
                }
            }
            else
            {
                if (stackCommands)
                {
                    foreach (TankAIHelper helper in this)
                    {
                        if (helper != null)
                        {
                            ManWorldRTS.inst.QueueNextCommand(helper, cmdTargPointTerrain);
                        }
                    }
                }
                else
                {
                    foreach (TankAIHelper helper in this)
                    {
                        if (helper != null)
                        {
                            helper.RTSDestination = cmdTargPointTerrain;
                            ManWorldRTS.inst.TechMovementQueue.Remove(helper.tank.visible.ID);
                            if (helper.lastAIType != AITreeType.AITypes.Escort)
                                helper.WakeAIForChange(true);
                            helper.SetRTSState(true);
                        }
                    }
                }
            }
            //DebugTAC_AI.Log(KickStart.ModID + ": HandleSelectTerrain.");
            if (PlaySFX && this.Any())
                Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.AcceptMission);
            return this.Any();
        }
        private bool HandleSelectScenery(Vector3 cmdTargPoint, Visible cmdTargVis, bool stackCommands)
        {
            //DebugTAC_AI.Log(KickStart.ModID + ": HandleSelectScenery.");

            bool responded = false;
            if (cmdTargVis)
            {
                ResourceDispenser cmdTargNode = cmdTargVis.GetComponent<ResourceDispenser>();
                if ((bool)cmdTargNode)
                {
                    if (!cmdTargNode.GetComponent<Damageable>().Invulnerable)
                    {   // Mine Move
                        foreach (TankAIHelper helper in this)
                        {
                            if (helper != null)
                            {
                                bool LandAIAssigned = helper.DediAI < AIType.MTTurret;
                                if (helper.isProspectorAvail)
                                {
                                    if (stackCommands)
                                    {
                                        ManWorldRTS.inst.QueueNextCommand(helper, cmdTargVis, AIType.Prospector);
                                    }
                                    else
                                    {
                                        ManWorldRTS.inst.SetOptionAuto(helper, AIType.Prospector);
                                        helper.RTSDestination = TankAIHelper.RTSDisabled;
                                        helper.SetRTSState(false);
                                        if (!ManNetwork.IsNetworked)
                                        {
                                            helper.theResource = cmdTargVis;
                                            helper.CollectedTarget = false;
                                        }
                                        ManWorldRTS.inst.TechMovementQueue.Remove(helper.tank.visible.ID);
                                    }
                                }
                                else
                                {
                                    if (stackCommands)
                                    {
                                    }
                                    else
                                    {
                                        if (helper.lastAIType != AITreeType.AITypes.Escort)
                                            helper.WakeAIForChange(true);
                                        helper.RTSDestination = cmdTargNode.transform.position + (Vector3.up * 2);
                                        helper.SetRTSState(true);
                                        ManWorldRTS.inst.TechMovementQueue.Remove(helper.tank.visible.ID);
                                    }
                                }
                                responded = true;
                            }
                        }
                        if (PlaySFX && responded)
                            Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.Undo);
                    }
                    else
                    {   // Just issue a movement command, it's a flattened rock or "landmark"
                        HandleSelectTerrain(cmdTargPoint, stackCommands);
                    }
                    return responded;
                }
            }
            try
            {
                Vector3 terrainPoint = PositionTweak(cmdTargPoint);
                foreach (TankAIHelper helper in this)
                {
                    if (helper != null)
                    {
                        if (helper.lastAIType != AITreeType.AITypes.Escort)
                            helper.WakeAIForChange(true);
                        helper.RTSDestination = terrainPoint;
                        helper.SetRTSState(true);
                        responded = true;
                    }
                }
                if (PlaySFX && responded)
                    Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.AcceptMission);
            }
            catch { }
            return responded;
        }
        private bool HandleSelectBlock(Vector3 cmdTargPoint, Visible cmdTargVis, bool stackCommands)
        {
            //DebugTAC_AI.Log(KickStart.ModID + ": HandleSelectBlock.");

            bool responded = false;
            if (cmdTargVis)
            {
                TankBlock cmdTargBlock = cmdTargVis.GetComponent<TankBlock>();
                if ((bool)cmdTargBlock)
                {
                    foreach (TankAIHelper helper in this)
                    {
                        if (helper != null)
                        {
                            bool LandAIAssigned = helper.DediAI < AIType.MTTurret;
                            if (helper.isScrapperAvail)
                            {
                                if (stackCommands)
                                {
                                    ManWorldRTS.inst.QueueNextCommand(helper, cmdTargVis, AIType.Scrapper);
                                }
                                else
                                {
                                    ManWorldRTS.inst.SetOptionAuto(helper, AIType.Scrapper);
                                    helper.RTSDestination = TankAIHelper.RTSDisabled;
                                    helper.SetRTSState(false);
                                    if (!ManNetwork.IsNetworked)
                                    {
                                        helper.theResource = cmdTargVis;
                                        helper.CollectedTarget = false;
                                    }
                                    ManWorldRTS.inst.TechMovementQueue.Remove(helper.tank.visible.ID);
                                }
                            }
                            else
                            {
                                if (stackCommands)
                                {
                                }
                                else
                                {
                                    if (helper.lastAIType != AITreeType.AITypes.Escort)
                                        helper.WakeAIForChange(true);
                                    helper.RTSDestination = cmdTargBlock.transform.position + (Vector3.up * 2);
                                    helper.SetRTSState(true);
                                    ManWorldRTS.inst.TechMovementQueue.Remove(helper.tank.visible.ID);
                                }
                            }
                            responded = true;
                        }
                    }
                    if (PlaySFX && responded)
                        Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.Craft);
                    return responded;
                }
            }
            try
            {
                foreach (TankAIHelper helper in this)
                {
                    if (helper != null)
                    {
                        if (helper.lastAIType != AITreeType.AITypes.Escort)
                            helper.WakeAIForChange(true);
                        helper.RTSDestination = PositionTweak(cmdTargPoint);
                        helper.SetRTSState(true);
                        responded = true;
                    }
                }
                if (PlaySFX && responded)
                    Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.AcceptMission);
            }
            catch { }
            return responded;
        }/*

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
                            ManWorldRTS.inst.TechMovementQueue.Remove(helper.tank.visible.ID);
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
                                    ManWorldRTS.inst.TechMovementQueue.Remove(helper.tank.visible.ID);
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
                                    ManWorldRTS.inst.TechMovementQueue.Remove(helper.tank.visible.ID);
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
                    ManWorldRTS.inst.TechMovementQueue.Remove(helper.tank.visible.ID);
                    if (helper.lastAIType != AITreeType.AITypes.Escort)
                        helper.WakeAIForChange(true);
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
                            ManWorldRTS.inst.TechMovementQueue.Remove(helper.tank.visible.ID);
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
                        ManWorldRTS.inst.TechMovementQueue.Remove(helper.tank.visible.ID);
                    }
                }
            }
            return success;
        }
        */

    }
}
