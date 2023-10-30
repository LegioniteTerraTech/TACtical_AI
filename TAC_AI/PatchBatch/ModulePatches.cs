using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Reflection;
using TerraTechETCUtil;
using TAC_AI.AI;
using TAC_AI.AI.Enemy;
using TAC_AI.Templates;
using TAC_AI.World;
using HarmonyLib;

namespace TAC_AI
{
    internal class ModulePatches
    {
        internal static class ModuleAIBotPatches
        {
            internal static Type target = typeof(ModuleAIBot);

            //ImproveAI
            private static void OnAttached_Postfix(ModuleAIBot __instance)
            {
                var valid = __instance.GetComponent<ModuleAIExtension>();
                if (valid)
                {
                    valid.OnPool();
                }
                else
                {
                    var ModuleAdd = __instance.gameObject.AddComponent<ModuleAIExtension>();
                    ModuleAdd.OnPool();
                    // Now retrofit AIs
                    ModuleAdd.AlterExisting();
                }
            }
        }
        internal static class ModuleWeaponPatches
        {
            internal static Type target = typeof(ModuleWeapon);
            static readonly FieldInfo targDeli = typeof(TargetAimer).GetField("AimDelegate", BindingFlags.NonPublic | BindingFlags.Instance);
            //AllowAIToAimAtScenery - On targeting
            private static bool UpdateAim_Prefix(ModuleWeapon __instance)
            {
                if (!KickStart.EnableBetterAI)
                    return true;
                try
                {
                    var AICommand = __instance.transform.root.GetComponent<TankAIHelper>();
                    if (AICommand)
                    {
                        if (AICommand.ActiveAimState == AIWeaponState.Obsticle && AICommand.Obst.IsNotNull())
                        {
                            Visible obstVis = AICommand.Obst.GetComponent<Visible>();
                            if (obstVis)
                            {
                                if (!obstVis.isActive)
                                {
                                    AICommand.Obst = null;
                                }
                            }
                            var ta = __instance.GetComponent<TargetAimer>();
                            if (ta)
                            {
                                Func<Vector3, Vector3> func = (Func<Vector3, Vector3>)targDeli.GetValue(ta);
                                if (func != null)
                                {
                                    ta.AimAtWorldPos(func(AICommand.Obst.position + (Vector3.up * 2)), __instance.RotateSpeed);
                                }
                                else
                                {
                                    ta.AimAtWorldPos(AICommand.Obst.position + (Vector3.up * 2), __instance.RotateSpeed);
                                }
                            }
                            return false;
                        }
                    }
                }
                catch { }
                return true;
            }

            static readonly FieldInfo aimers = typeof(ModuleWeapon).GetField("m_TargetAimer", BindingFlags.NonPublic | BindingFlags.Instance),
                aimerTargPos = typeof(TargetAimer).GetField("m_TargetPosition", BindingFlags.NonPublic | BindingFlags.Instance),
                WeaponTargPos = typeof(ModuleWeapon).GetField("m_TargetPosition", BindingFlags.NonPublic | BindingFlags.Instance);
            //PatchAimingSystemsToHelpAI
            private static void UpdateAutoAimBehaviour_Postfix(ModuleWeapon __instance)
            {
                if (!KickStart.EnableBetterAI)
                    return;
                if (!KickStart.isWeaponAimModPresent)
                {
                    TargetAimer thisAimer = (TargetAimer)aimers.GetValue(__instance);

                    if (thisAimer.HasTarget)
                    {
                        WeaponTargPos.SetValue(__instance, (Vector3)aimerTargPos.GetValue(thisAimer));
                    }
                }
            }
        }
        internal static class ModuleItemPickupPatches
        {
            internal static Type target = typeof(ModuleItemPickup);

            //MarkReceiver
            private static void OnAttached_Postfix(ModuleItemPickup __instance)
            {
                var valid = __instance.GetComponent<ModuleItemHolder>();
                if (valid)
                {
                    if (valid.IsFlag(ModuleItemHolder.Flags.Receiver))
                    {
                        var ModuleAdd = __instance.gameObject.GetComponent<ModuleHarvestReciever>();
                        if (!ModuleAdd)
                        {
                            ModuleAdd = __instance.gameObject.AddComponent<ModuleHarvestReciever>();
                            ModuleAdd.enabled = true;
                            ModuleAdd.OnPool();
                        }
                    }
                }
            }
        }
        internal static class ModuleRemoteChargerPatches
        {
            internal static Type target = typeof(ModuleRemoteCharger);

            //MarkChargers
            private static void OnAttached_Postfix(ModuleRemoteCharger __instance)
            {
                var ModuleAdd = __instance.gameObject.GetComponent<ModuleChargerTracker>();
                if (!ModuleAdd)
                {
                    ModuleAdd = __instance.gameObject.AddComponent<ModuleChargerTracker>();
                    ModuleAdd.OnPool();
                }
            }
        }
        internal static class ModuleItemConsumePatches
        {
            internal static Type target = typeof(ModuleItemConsume);

            static readonly FieldInfo progress = typeof(ModuleItemConsume).GetField("m_ConsumeProgress", BindingFlags.NonPublic | BindingFlags.Instance);
            static readonly FieldInfo sellStolen = typeof(ModuleItemConsume).GetField("m_OperateItemInterceptedBy", BindingFlags.NonPublic | BindingFlags.Instance);

            //LetNPCsSellStuff
            private static bool InitRecipeOutput_Prefix(ModuleItemConsume __instance)
            {
                int team = 0;
                if (__instance.block?.tank)
                {
                    team = __instance.block.tank.Team;
                }
                if (AIGlobals.IsBaseTeam(team))
                {
                    ModuleItemConsume.Progress pog = (ModuleItemConsume.Progress)progress.GetValue(__instance);
                    if (pog.currentRecipe.m_OutputType == RecipeTable.Recipe.OutputType.Money && sellStolen.GetValue(__instance) == null)
                    {
                        WorldPosition pos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(__instance.block.visible);
                        int sellGain = (int)(pog.currentRecipe.m_MoneyOutput * KickStart.EnemySellGainModifier);

                        string moneyGain = Singleton.Manager<Localisation>.inst.GetMoneyStringWithSymbol(sellGain);
                        if (AIGlobals.IsNeutralBaseTeam(team))
                        {
                            if (KickStart.DisplayEnemyEvents)
                                AIGlobals.PopupNeutralInfo(moneyGain, pos);
                            RLoadedBases.TryAddMoney(sellGain, __instance.block.tank.Team);
                            return false;
                        }
                        else if (AIGlobals.IsFriendlyBaseTeam(team))
                        {
                            if (KickStart.DisplayEnemyEvents)
                                AIGlobals.PopupAllyInfo(moneyGain, pos);
                            RLoadedBases.TryAddMoney(sellGain, __instance.block.tank.Team);
                            return false;
                        }
                        else
                        {
                            if (KickStart.DisplayEnemyEvents)
                                AIGlobals.PopupEnemyInfo(moneyGain, pos);
                            RLoadedBases.TryAddMoney(sellGain, __instance.block.tank.Team);
                        }
                    }
                }
                return true;
            }
        }
        internal static class ModuleHeartPatches
        {
            internal static Type target = typeof(ModuleHeart);

            static readonly FieldInfo PNR = typeof(ModuleHeart).GetField("m_EventHorizonRadius", BindingFlags.NonPublic | BindingFlags.Instance);
            //LetNPCsSCUStuff
            private static void UpdatePickupTargets_Prefix(ModuleHeart __instance)
            {
                var valid = __instance.GetComponent<ModuleItemHolder>();
                if (valid)
                {
                    int team = __instance.block.tank.Team;
                    if (ManNetwork.IsHost && AIGlobals.IsBaseTeam(team))
                    {
                        ModuleItemHolder.Stack stack = valid.SingleStack;
                        Vector3 vec = stack.BasePosWorld();
                        for (int num = stack.items.Count - 1; num >= 0; num--)
                        {
                            Visible vis = stack.items[num];
                            if (!vis.IsPrePickup && vis.block)
                            {
                                float magnitude = (vis.centrePosition - vec).magnitude;
                                if (magnitude <= (float)PNR.GetValue(__instance) && Singleton.Manager<ManPointer>.inst.DraggingItem != vis)
                                {
                                    WorldPosition pos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(__instance.block.visible);
                                    int sellGain = (int)(KickStart.EnemySellGainModifier * Singleton.Manager<RecipeManager>.inst.GetBlockSellPrice(vis.block.BlockType));

                                    string moneyGain = Singleton.Manager<Localisation>.inst.GetMoneyStringWithSymbol(sellGain);
                                    if (AIGlobals.IsNeutralBaseTeam(team))
                                    {
                                        if (KickStart.DisplayEnemyEvents)
                                            AIGlobals.PopupNeutralInfo(moneyGain, pos);
                                        RLoadedBases.TryAddMoney(sellGain, __instance.block.tank.Team);
                                    }
                                    else if (AIGlobals.IsFriendlyBaseTeam(team))
                                    {
                                        if (KickStart.DisplayEnemyEvents)
                                            AIGlobals.PopupAllyInfo(moneyGain, pos);
                                        RLoadedBases.TryAddMoney(sellGain, __instance.block.tank.Team);
                                    }
                                    else
                                    {
                                        if (KickStart.DisplayEnemyEvents)
                                            AIGlobals.PopupEnemyInfo(moneyGain, pos);
                                        RLoadedBases.TryAddMoney(sellGain, __instance.block.tank.Team);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            //SpawnTraderTroll
            private static void OnAttached_Postfix(ModuleHeart __instance)
            {
                if (__instance.block.tank.IsNull())
                    return;
                // Setup trolls if Population Injector is N/A
                if (KickStart.enablePainMode && KickStart.AllowEnemiesToStartBases && SpecialAISpawner.thisActive && Singleton.Manager<ManPop>.inst.IsSpawningEnabled && Singleton.Manager<ManWorld>.inst.Vendors.IsVendorSCU(__instance.block.BlockType))
                {
                    if (Singleton.Manager<ManWorld>.inst.GetTerrainHeight(__instance.transform.position, out _))
                    {
                        SpecialAISpawner.TrySpawnTraderTroll(__instance.transform.position);
                    }
                }
            }
        }

        internal static class ModuleTechControllerPatches
        {
            internal static Type target = typeof(ModuleTechController);

            // Where it all happens
            //PatchControlSystem
            private static bool ExecuteControl_Prefix(ModuleTechController __instance, ref bool __result)
            {
                if (KickStart.EnableBetterAI)
                {
                    //DebugTAC_AI.Log("TACtical_AI: AIEnhanced enabled");
                    try
                    {
                        var tank = __instance.block.tank;
                        if (tank)
                        {
                            var tankAIHelp = tank.gameObject.GetComponent<TankAIHelper>();
                            if (tankAIHelp)
                            {
                                if (tankAIHelp.ControlTech(__instance.block.tank.control))
                                {
                                    __result = true;
                                    return false;
                                }
                            }
                        }
                        // else it's still initiating
                    }
                    catch (Exception e)
                    {
                        DebugTAC_AI.Log("TACtical_AI: TankAIHelper.ControlTech() - Failure on handling AI!");
                        DebugTAC_AI.Log(e);
                    }
                }
                return true;
            }
        }


        // Resources/Collection
        internal static class ModuleItemHolderBeamPatches
        {
            internal static Type target = typeof(ResourceDispenser);

            private static void InitState_Prefix(ResourceDispenser __instance)
            {
                try
                {
                    //DebugTAC_AI.Log("TACtical_AI: Added resource to list (InitState)");
                    if (!AIECore.Minables.Contains(__instance.visible))
                        AIECore.Minables.Add(__instance.visible);
                    //else
                    //    DebugTAC_AI.Log("TACtical_AI: RESOURCE WAS ALREADY ADDED! (InitState)");
                }
                catch { } // null call
            }
            private static void Restore_Prefix(ResourceDispenser __instance, ref ResourceDispenser.PersistentState state)
            {
                try
                {
                    //DebugTAC_AI.Log("TACtical_AI: Added resource to list (Restore)");
                    if (!state.removedFromWorld)
                    {
                        if (!AIECore.Minables.Contains(__instance.visible))
                            AIECore.Minables.Add(__instance.visible);
                    }
                    else
                    {
                        if (AIECore.Minables.Contains(__instance.visible))
                            AIECore.Minables.Remove(__instance.visible);
                    }
                    //else
                    //    DebugTAC_AI.Log("TACtical_AI: RESOURCE WAS ALREADY ADDED! (Restore)");
                }
                catch { } // null call
            }
            private static void Die_Prefix(ResourceDispenser __instance)
            {
                try
                {
                    //DebugTAC_AI.Log("TACtical_AI: Removed resource from list (Die)");
                    if (AI.AIECore.Minables.Contains(__instance.visible))
                    {
                        AI.AIECore.Minables.Remove(__instance.visible);
                    }
                    else
                        DebugTAC_AI.Log("TACtical_AI: RESOURCE WAS ALREADY REMOVED! (Die)");
                }
                catch { } // null call
            }
            private static void OnRecycle_Prefix(ResourceDispenser __instance)
            {
                //DebugTAC_AI.Log("TACtical_AI: Removed resource from list (OnRecycle)");
                if (AIECore.Minables.Contains(__instance.visible))
                {
                    AIECore.Minables.Remove(__instance.visible);
                }
                //else
                //    DebugTAC_AI.Log("TACtical_AI: RESOURCE WAS ALREADY REMOVED! (OnRecycle)");

            }
            private static void Deactivate_Prefix(ResourceDispenser __instance)
            {
                try
                {
                    //DebugTAC_AI.Log("TACtical_AI: Removed resource from list (Deactivate)");
                    if (AIECore.Minables.Contains(__instance.visible))
                    {
                        AIECore.Minables.Remove(__instance.visible);
                    }
                    //else
                    //    DebugTAC_AI.Log("TACtical_AI: RESOURCE WAS ALREADY REMOVED! (Deactivate)");

                }
                catch { } // null call
            }
        }
    }
}
