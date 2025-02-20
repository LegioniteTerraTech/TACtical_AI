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
            
            private static bool UpdateAim_Prefix(ModuleWeapon __instance, IModuleWeapon ___m_WeaponComponent, ref TargetAimer ___m_TargetAimer)
            {
                if (!KickStart.EnableBetterAI)
                    return true;
                try
                {
                    var AICommand = __instance.block.tank.GetHelperInsured();
                    if (AICommand)
                    {
                        switch (AICommand.ActiveAimState)
                        {
                            case AIWeaponState.Normal:
                                break;
                            case AIWeaponState.Enemy:
                                break;
                            case AIWeaponState.HoldFire:
                                ___m_TargetAimer.AimAtWorldPos(___m_WeaponComponent.GetFireTransform().position +
                                    __instance.block.trans.TransformDirection(new Vector3(0, -0.5f, 1)), __instance.RotateSpeed);
                                return false;
                            case AIWeaponState.Obsticle:
                                Visible obstVis = AICommand.Obst.GetComponent<Visible>();
                                if (obstVis && !obstVis.isActive)
                                    AICommand.Obst = null;
                                if (___m_TargetAimer)
                                {
                                    Func<Vector3, Vector3> func = (Func<Vector3, Vector3>)targDeli.GetValue(___m_TargetAimer);
                                    if (func != null)
                                    {
                                        ___m_TargetAimer.AimAtWorldPos(func(AICommand.Obst.position + (Vector3.up * 2)), __instance.RotateSpeed);
                                    }
                                    else
                                    {
                                        ___m_TargetAimer.AimAtWorldPos(AICommand.Obst.position + (Vector3.up * 2), __instance.RotateSpeed);
                                    }
                                }
                                return false;
                            case AIWeaponState.Mimic:
                                break;
                            default:
                                break;
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
            private static void UpdateAutoAimBehaviour_Postfix(ModuleWeapon __instance, ref TargetAimer ___m_TargetAimer, ref Vector3 ___m_TargetPosition)
            {
                if (!KickStart.EnableBetterAI)
                    return;
                if (!KickStart.isWeaponAimModPresent)
                {
                    if (___m_TargetAimer.HasTarget)
                    {
                        ___m_TargetPosition = (Vector3)aimerTargPos.GetValue(___m_TargetAimer);
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
            internal static void OnAttached_Postfix(ModuleRemoteCharger __instance)
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
            private static Dictionary<ModuleItemConsume, int> ReservedSell = new Dictionary<ModuleItemConsume, int>();
            //LetNPCsSellStuff
            internal static bool InitRecipeOutput_Prefix(ModuleItemConsume __instance)
            {
                int team = 0;
                if (ReservedSell.TryGetValue(__instance, out int TeamOwner))
                    team = TeamOwner;
                else if (__instance.block?.tank)
                    team = __instance.block.tank.Team;
                if (ManNetwork.IsHost && AIGlobals.IsBaseTeamDynamic(team))
                {
                    ModuleItemConsume.Progress pog = (ModuleItemConsume.Progress)progress.GetValue(__instance);
                    if (pog.currentRecipe.m_OutputType == RecipeTable.Recipe.OutputType.Money && sellStolen.GetValue(__instance) == null)
                    {
                        WorldPosition pos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(__instance.block.visible);
                        int sellGain = (int)(pog.currentRecipe.m_MoneyOutput * KickStart.EnemySellGainModifier);

                        string moneyGain = Singleton.Manager<Localisation>.inst.GetMoneyStringWithSymbol(sellGain);

                        if (KickStart.DisplayEnemyEvents && AIGlobals.PopupColored(moneyGain, team, pos))
                        {
                            RLoadedBases.TryAddMoney(sellGain, __instance.block.tank.Team);
                            return false;
                        }
                    }
                }
                return true;
            }
            internal static void DestroyItem_Prefix(ModuleItemConsume __instance, ref Visible item)
            {
                if (__instance.block?.tank)
                {
                    int team = __instance.block.tank.Team;
                    if (team == ManSpawn.NeutralTeam && ManBaseTeams.inst.TradingSellOffers.TryGetValue(item.ID, out int teamOwner))
                    {
                        item.RecycledEvent.Unsubscribe(ManBaseTeams.PickupRecycled);
                        ManBaseTeams.inst.TradingSellOffers.Remove(item.ID);
                        ReservedSell[__instance] = teamOwner;
                    }
                }
            }
        }
        internal static class ModuleHeartPatches
        {
            internal static Type target = typeof(ModuleHeart);

            static readonly FieldInfo PNR = typeof(ModuleHeart).GetField("m_EventHorizonRadius", BindingFlags.NonPublic | BindingFlags.Instance);
            //LetNPCsSCUStuff
            internal static void UpdatePickupTargets_Prefix(ModuleHeart __instance)
            {
                var valid = __instance.GetComponent<ModuleItemHolder>();
                if (ManNetwork.IsHost && valid)
                {
                    int team = __instance.block.tank.Team;
                    if (AIGlobals.IsBaseTeamDynamic(team))
                    {
                        ModuleItemHolder.Stack stack = valid.SingleStack;
                        Vector3 vec = stack.BasePosWorld();
                        for (int num = stack.items.Count - 1; num >= 0; num--)
                        {
                            Visible vis = stack.items[num];
                            if (!vis.IsPrePickup && vis.block)
                            {
                                float magnitude = (vis.centrePosition - vec).magnitude;
                                if (magnitude <= (float)PNR.GetValue(__instance) && ManPointer.inst.DraggingItem != vis)
                                {
                                    WorldPosition pos = ManOverlay.inst.WorldPositionForFloatingText(__instance.block.visible);
                                    int sellGain = (int)(KickStart.EnemySellGainModifier * RecipeManager.inst.GetBlockSellPrice(vis.block.BlockType));

                                    string moneyGain = Localisation.inst.GetMoneyStringWithSymbol(sellGain);
                                    if (KickStart.DisplayEnemyEvents && AIGlobals.PopupColored(moneyGain, team, pos))
                                        RLoadedBases.TryAddMoney(sellGain, __instance.block.tank.Team);
                                }
                            }
                        }
                    }
                }
            }

            //SpawnTraderTroll
            internal static void OnAttached_Postfix(ModuleHeart __instance)
            {
                if (__instance.block.tank.IsNull())
                    return;
                // Setup trolls if Population Injector is N/A
                if (KickStart.enablePainMode && KickStart.AllowEnemiesToStartBases && SpecialAISpawner.thisActive && 
                    ManPop.inst.IsSpawningEnabled && 
                    ManWorld.inst.Vendors.IsVendorSCU(__instance.block.BlockType))
                {
                    if (ManWorld.inst.GetTerrainHeight(__instance.transform.position, out _))
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
            internal static bool ExecuteControl_Prefix(ModuleTechController __instance, ref bool __result)
            {
                if (KickStart.EnableBetterAI)
                {
                    //DebugTAC_AI.Log(KickStart.ModID + ": AIEnhanced enabled");
                    try
                    {
                        var tank = __instance.block.tank;
                        if (tank)
                        {
                            var helper = tank.gameObject.GetComponent<TankAIHelper>();
                            if (helper)
                            {
                                if (helper.ControlTech(__instance.block.tank.control))
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
                        DebugTAC_AI.Log(KickStart.ModID + ": TankAIHelper.ControlTech() - Failure on handling AI!");
                        DebugTAC_AI.Log(e);
                    }
                }
                return true;
            }
        }


        // Resources/Collection
        internal static class ResourceDispenserPatches
        {
            internal static Type target = typeof(ResourceDispenser);

            private static void OnSpawn_Postfix(ResourceDispenser __instance)
            {
                try
                {
                    //DebugTAC_AI.Log(KickStart.ModID + ": Added resource to list (InitState)");
                    var dmg = __instance.GetComponent<Damageable>();
                    if (dmg && !dmg.Invulnerable && !AIECore.Minables.Contains(__instance.visible))
                        AIECore.Minables.Add(__instance.visible);
                    //else
                    //    DebugTAC_AI.Log(KickStart.ModID + ": RESOURCE WAS ALREADY ADDED! (InitState)");
                }
                catch { } // null call
            }
            private static void InitState_Postfix(ResourceDispenser __instance)
            {
                try
                {
                    //DebugTAC_AI.Log(KickStart.ModID + ": Added resource to list (InitState)");
                    var dmg = __instance.GetComponent<Damageable>();
                    if (dmg && !dmg.Invulnerable && !AIECore.Minables.Contains(__instance.visible))
                        AIECore.Minables.Add(__instance.visible);
                    //else
                    //    DebugTAC_AI.Log(KickStart.ModID + ": RESOURCE WAS ALREADY ADDED! (InitState)");
                }
                catch { } // null call
            }
            private static void Restore_Postfix(ResourceDispenser __instance, ref ResourceDispenser.PersistentState state)
            {
                try
                {
                    //DebugTAC_AI.Log(KickStart.ModID + ": Added resource to list (Restore)");
                    if (!state.removedFromWorld && state.health > 0)
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
                    //    DebugTAC_AI.Log(KickStart.ModID + ": RESOURCE WAS ALREADY ADDED! (Restore)");
                }
                catch { } // null call
            }
            private static void Die_Prefix(ResourceDispenser __instance)
            {
                try
                {
                    //DebugTAC_AI.Log(KickStart.ModID + ": Removed resource from list (Die)");
                    if (AI.AIECore.Minables.Contains(__instance.visible))
                    {
                        AI.AIECore.Minables.Remove(__instance.visible);
                    }
                    else
                        DebugTAC_AI.Log(KickStart.ModID + ": RESOURCE WAS ALREADY REMOVED! (Die)");
                }
                catch { } // null call
            }
            private static void OnRecycle_Prefix(ResourceDispenser __instance)
            {
                //DebugTAC_AI.Log(KickStart.ModID + ": Removed resource from list (OnRecycle)");
                if (AIECore.Minables.Contains(__instance.visible))
                {
                    AIECore.Minables.Remove(__instance.visible);
                }
                //else
                //    DebugTAC_AI.Log(KickStart.ModID + ": RESOURCE WAS ALREADY REMOVED! (OnRecycle)");

            }
            private static void Deactivate_Prefix(ResourceDispenser __instance)
            {
                try
                {
                    //DebugTAC_AI.Log(KickStart.ModID + ": Removed resource from list (Deactivate)");
                    if (AIECore.Minables.Contains(__instance.visible))
                    {
                        AIECore.Minables.Remove(__instance.visible);
                    }
                    //else
                    //    DebugTAC_AI.Log(KickStart.ModID + ": RESOURCE WAS ALREADY REMOVED! (Deactivate)");

                }
                catch { } // null call
            }
        }
    }
}
