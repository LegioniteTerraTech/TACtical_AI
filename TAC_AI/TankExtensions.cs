using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using TAC_AI.AI;
using TAC_AI.Templates;
using TAC_AI.World;

namespace TAC_AI
{
    public static class TankExtentions
    {
        public static bool CommandInteract(this Tank tank, Visible vis)
        {
            if (vis?.resdisp)
                return tank.CommandMine(vis.resdisp);
            else if (vis?.tank)
                return tank.CommandTarget(vis.tank);
            else if (vis?.block)
                return tank.CommandCollect(vis.block);
            return false;
        }
        public static bool CommandTarget(this Tank tank, Tank grabbedTech)
        {
            var help = tank.GetHelperInsured();
            //DebugTAC_AI.Log(KickStart.ModID + ": HandleSelectTargetTank.");
            bool success = false;
            if ((bool)grabbedTech)
            {
                if (grabbedTech.IsEnemy(tank.Team))
                {   // Attack Move
                    help.RTSDestination = TankAIHelper.RTSDisabled;
                    help.SetRTSState(true);
                    if (ManNetwork.IsNetworked)
                        NetworkHandler.TryBroadcastRTSAttack(help.tank.netTech.netId.Value, grabbedTech.netTech.netId.Value);
                    help.lastEnemy = grabbedTech.visible;
                    ManPlayerRTS.inst.TechMovementQueue.Remove(help);
                    success = true;
                }
                else if (grabbedTech.IsFriendly(tank.Team))
                {// Protect/Defend
                    try
                    {
                        if (grabbedTech.IsAnchored)
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
                        else
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
                    catch
                    {
                        DebugTAC_AI.Log(KickStart.ModID + ": Error on Protect/Defend - Tech");
                        DebugTAC_AI.Log(KickStart.ModID + ": " + help.name);
                    }
                }
            }
            return success;
        }
        public static bool CommandMine(this Tank tank, ResourceDispenser node)
        {
            //DebugTAC_AI.Log(KickStart.ModID + ": HandleSelectScenery.");

            var help = tank.GetHelperInsured();
            bool success = false;
            if ((bool)node)
            {
                if (!node.GetComponent<Damageable>().Invulnerable)
                {   // Mine Move
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
            return success;
        }
        public static bool CommandCollect(this Tank tank, TankBlock block)
        {
            //DebugTAC_AI.Log(KickStart.ModID + ": HandleSelectBlock.");
            var help = tank.GetHelperInsured();

            bool success = false;
            if ((bool)block)
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
            return success;
        }

        public static void CommandMove(this Tank tank, Vector3 pos, bool enqueue = false)
        {
            var helper = tank.GetHelperInsured();
            if (enqueue)
            {
                helper.SetRTSState(true);
                ManPlayerRTS.inst.QueueNextDestination(helper, pos);
            }
            else
            {
                ManPlayerRTS.inst.TechMovementQueue.Remove(helper);
                helper.RTSDestination = pos;
                helper.SetRTSState(true);
            }
        }
        public static void CommandBlowBolts(this Tank tank)
        {
            var helper = tank.GetHelperInsured();
            helper.BoltsFired = true;
            tank.control.ServerDetonateExplosiveBolt();
        }

        public static void CommandRelease(this Tank tank)
        {
            var helper = tank.GetHelperInsured();
            helper.RTSDestination = TankAIHelper.RTSDisabled;
            helper.SetRTSState(false);
        }



        public static bool IsTeamFounder(this TechData tank)
        {
            if (tank == null)
            {
                DebugTAC_AI.LogError(KickStart.ModID + ": IsTeamFounder - CALLED ON NULL OBJECT");
                return false;
            }
            return tank.Name.Contains('Ω') || tank.Name.Contains('⦲');
        }
        public static bool IsTeamFounder(this Tank tank)
        {
            if (!tank)
            {
                DebugTAC_AI.LogError(KickStart.ModID + ": IsTeamFounder - CALLED ON NULL OBJECT");
                return false;
            }
            return tank.name.Contains('Ω') || tank.name.Contains('⦲');
        }
        public static bool IsBase(this TechData tank)
        {
            if (tank == null)
            {
                DebugTAC_AI.LogError(KickStart.ModID + ": IsBase - CALLED ON NULL OBJECT");
                return false;
            }
            return tank.CheckIsAnchored() || tank.Name.Contains('¥') || tank.Name.Contains(RawTechLoader.turretChar);
        }
        public static bool IsBase(this Tank tank)
        {
            if (!tank)
            {
                DebugTAC_AI.LogError(KickStart.ModID + ": IsBase - CALLED ON NULL OBJECT");
                return false;
            }
            return tank.IsAnchored || tank.name.Contains('¥') || tank.name.Contains(RawTechLoader.turretChar);
        }
        public static TankAIHelper GetHelperInsured(this Tank tank)
        {
            TankAIHelper help = tank.GetComponent<TankAIHelper>();
            if (!help)
            {
                help = tank.gameObject.AddComponent<TankAIHelper>().Subscribe();
            }
            return help;
        }
        public static float GetCheapBounds(this Visible vis)
        {
            if (!vis)
            {
                DebugTAC_AI.LogError(KickStart.ModID + ": GetCheapBounds - CALLED ON NULL OBJECT");
                return 1;
            }
            if (!vis.tank)
                return vis.Radius;
            TankAIHelper help = vis.GetComponent<TankAIHelper>();
            if (!help)
            {
                help = vis.gameObject.AddComponent<TankAIHelper>().Subscribe();
            }
            return help.lastTechExtents;
        }
        public static float GetCheapBounds(this Tank tank)
        {
            return tank.GetHelperInsured().lastTechExtents;
        }
        private static List<FactionSubTypes> toSortCache = new List<FactionSubTypes>();
        public static FactionSubTypes GetMainCorp(this Tank tank)
        {
            try
            {
                foreach (TankBlock BlocS in tank.blockman.IterateBlocks())
                {
                    toSortCache.Add(ManSpawn.inst.GetCorporation(BlocS.BlockType));
                }
                toSortCache = SortCorps(toSortCache);
                FactionSubTypes final = toSortCache.FirstOrDefault();
                //DebugTAC_AI.Log(KickStart.ModID + ": GetMainCorpExt - Selected " + final + " for main corp")
                return final;//(FactionSubTypes)tank.GetMainCorporations().FirstOrDefault();
            }
            finally
            {
                toSortCache.Clear();
            }
        }
        public static FactionSubTypes GetMainCorp(this TechData tank)
        {
            try
            {
                foreach (TankPreset.BlockSpec BlocS in tank.m_BlockSpecs)
                {
                    toSortCache.Add(ManSpawn.inst.GetCorporation(BlocS.m_BlockType));
                }
                toSortCache = SortCorps(toSortCache);
                var first = toSortCache.FirstOrDefault();
                return first;//(FactionSubTypes)tank.GetMainCorporations().FirstOrDefault();
            }
            finally
            {
                toSortCache.Clear();
            }
        }

        private static List<KeyValuePair<int, FactionSubTypes>> sorter = new List<KeyValuePair<int, FactionSubTypes>>();
        private static List<FactionSubTypes> distinct = new List<FactionSubTypes>();
        private static List<FactionSubTypes> SortCorps(List<FactionSubTypes> unsorted)
        {
            foreach (FactionSubTypes FTE in unsorted.Distinct())
            {
                int countOut = unsorted.Count(delegate (FactionSubTypes cand) { return cand == FTE; });
                sorter.Add(new KeyValuePair<int, FactionSubTypes>(countOut, FTE));
            }
            distinct.Clear();
            foreach (KeyValuePair<int, FactionSubTypes> intBT in sorter.OrderByDescending(x => x.Key))
            {
                distinct.Add(intBT.Value);
            }
            sorter.Clear();
            return distinct;
        }

        private const ModuleItemHolder.AcceptFlags flagB = ModuleItemHolder.AcceptFlags.Blocks;
        internal static bool BlockLoaded(this ModuleItemHolder MIH)
        {
            ModuleItemHolderMagnet mag = MIH.GetComponent<ModuleItemHolderMagnet>();
            if (mag)
            {
                if (!mag.IsOperating)
                    return false;
            }
            else
            {
                if (!MIH.IsEmpty && MIH.Acceptance == flagB && MIH.IsFlag(ModuleItemHolder.Flags.Collector))
                {
                    return true;
                }
            }
            return false;
        }
        internal static bool BlockNotFullAndAvail(this ModuleItemHolder MIH)
        {
            ModuleItemHolderMagnet mag = MIH.GetComponent<ModuleItemHolderMagnet>();
            if (MIH.GetComponent<ModuleItemHolderMagnet>())
            {
                if (!mag.IsOperating)
                    return false;

            }
            else
            {
                if (!MIH.IsFull && MIH.Acceptance == flagB && MIH.IsFlag(ModuleItemHolder.Flags.Collector))
                {
                    return true;
                }
            }
            return false;
        }


        private static readonly List<BlockManager.BlockAttachment> tempCache = new List<BlockManager.BlockAttachment>(64);
        internal static bool CanAttachBlock(this Tank tank, TankBlock TB, IntVector3 posOnTechGrid, OrthoRotation rotOnTech)
        {
            if (tank.blockman.blockCount != 0)
            {
                tempCache.Clear();
                tank.blockman.TryGetBlockAttachments(TB, posOnTechGrid, rotOnTech, tempCache);
                return tempCache.Count != 0;
            }
            return false;
        }
    }
}
