using System;
using System.Reflection;
using System.Collections.Generic;
using Harmony;
using UnityEngine;
using UnityEngine.UI;

namespace TAC_AI
{
    class PatchBatch
    {
    }

    internal static class Patches
    {
        [HarmonyPatch(typeof(ModuleTechController))]
        [HarmonyPatch("ExecuteControl")]//On Control
        private static class PatchControlSystem
        {
            private static bool Prefix(ModuleTechController __instance)
            {
                if (KickStart.EnableBetterAI)
                {
                    //Debug.Log("TACtical_AI: AIEnhanced enabled");
                    try
                    {
                        var aI = __instance.transform.root.GetComponent<Tank>().AI;
                        var tank = __instance.transform.root.GetComponent<Tank>();
                        if (!tank.PlayerFocused && aI.HasAIModules && tank.IsFriendly() && !Singleton.Manager<ManGameMode>.inst.IsCurrentModeMultiplayer())
                        {
                            //Debug.Log("TACtical_AI: AI Valid!");
                            //Debug.Log("TACtical_AI: (TankAIHelper) is " + tank.gameObject.GetComponent<AIEnhancedCore.TankAIHelper>().wasEscort);
                            var tankAIHelp = tank.gameObject.GetComponent<AI.AIECore.TankAIHelper>();
                            //tankAIHelp.AIState && 
                            if (tankAIHelp.lastAIType == AITreeType.AITypes.Escort)
                            {
                                //Debug.Log("TACtical_AI: Running BetterAI");
                                //Debug.Log("TACtical_AI: Patched Tank ExecuteControl(TankAIHelper)");
                                tankAIHelp.BetterAI(__instance.block.tank.control);
                                return false;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.Log("TACtical_AI: Failiure on handling AI addition!");
                        Debug.Log(e);
                    }
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Tank))]
        [HarmonyPatch("OnPool")]//On Creation
        private static class PatchTankToHelpAIAndClocks
        {
            private static void Postfix(Tank __instance)
            {
                //Debug.Log("TACtical_AI: Patched Tank OnPool(TankAIHelper & TimeTank)");
                var ModuleCheck = __instance.gameObject.GetComponent<AI.AIECore.TankAIHelper>();
                if (ModuleCheck.IsNull())
                {
                    __instance.gameObject.AddComponent<AI.AIECore.TankAIHelper>().Subscribe(__instance);
                }
            }
        }

        /*
        [HarmonyPatch(typeof(TankBeam))]
        [HarmonyPatch("Update")]//Give the AI some untangle help
        private class PatchTankBeamToHelpAI
        {
            private static void Postfix(TankBeam __instance)
            {
                //Debug.Log("TACtical_AI: Patched TankBeam Update(TankAIHelper)");
                var ModuleCheck = __instance.gameObject.GetComponent<AIEnhancedCore.TankAIHelper>();
                if (ModuleCheck != null)
                {
                }
            }
        }
        */


        [HarmonyPatch(typeof(ModuleAIBot))]
        [HarmonyPatch("OnPool")]//On Creation
        private static class ImproveAI
        {
            private static void Postfix(ModuleAIBot __instance)
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
                    try
                    {
                        var name = __instance.gameObject.name;
                        if (name == "GSO_AI_Module_Guard_111")
                        {
                            ModuleAdd.Aegis = true;
                            ModuleAdd.AidAI = true;
                            ModuleAdd.Prospector = true;//Temp until main intended function arrives
                        }
                        if (name == "GSO_AIAnchor_121")
                        {
                            ModuleAdd.Aegis = true;
                            ModuleAdd.AidAI = true;
                            ModuleAdd.Prospector = true;//Temp until main intended function arrives
                            ModuleAdd.MaxCombatRange = 150;
                        }
                        else if (name == "GC_AI_Module_Guard_222")
                        {
                            ModuleAdd.Prospector = true;
                            ModuleAdd.MTForAll = true;
                            ModuleAdd.MeleePreferred = true;
                        }
                        else if (name == "VEN_AI_Module_Guard_111")
                        {
                            ModuleAdd.Aviator = true;
                            ModuleAdd.Prospector = true;//Temp until main intended function arrives
                            ModuleAdd.SidePreferred = true;
                            ModuleAdd.MaxCombatRange = 300;
                        }
                        else if (name == "HE_AI_Module_Guard_112")
                        {
                            ModuleAdd.Assault = true;
                            ModuleAdd.Prospector = true;//Temp until main intended function arrives
                            ModuleAdd.AdvancedAI = true;
                            ModuleAdd.MinCombatRange = 50;
                            ModuleAdd.MaxCombatRange = 200;
                        }
                        else if (name == "HE_AI_Turret_111")
                        {
                            ModuleAdd.Assault = true;
                            ModuleAdd.Prospector = true;//Temp until main intended function arrives
                            ModuleAdd.AdvancedAI = true;
                            ModuleAdd.MinCombatRange = 50;
                            ModuleAdd.MaxCombatRange = 150;
                        }
                        else if (name == "BF_AI_Module_Guard_212")
                        {
                            ModuleAdd.Astrotech = true;
                            //ModuleAdd.Prospector = true;//Temp until main intended function arrives
                            ModuleAdd.AdvAvoidence = true;
                            ModuleAdd.MinCombatRange = 60;
                            ModuleAdd.MaxCombatRange = 180;
                        }
                        /*
                        else if (name == "RR_AI_Module_Guard_212")
                        {
                            ModuleAdd.Energizer = true;
                            ModuleAdd.AdvAvoidence = true;
                            ModuleAdd.MinCombatRange = 160;
                            ModuleAdd.MaxCombatRange = 220;
                        }
                        else if (name == "SJ_AI_Module_Guard_122")
                        {
                            ModuleAdd.Scrapper = true;
                            ModuleAdd.MTForAll = true;
                            ModuleAdd.MinCombatRange = 60;
                            ModuleAdd.MaxCombatRange = 120;
                        }
                        else if (name == "TSN_AI_Module_Guard_312")
                        {
                            ModuleAdd.Buccaneer = true;
                            ModuleAdd.AdvAvoidence = true;
                            ModuleAdd.MinCombatRange = 150;
                            ModuleAdd.MaxCombatRange = 250;
                        }
                        else if (name == "LEG_AI_Module_Guard_112")
                        {   //Incase Legion happens and the AI needs help lol
                            ModuleAdd.Assault = true;
                            ModuleAdd.Aegis = true;
                            ModuleAdd.Prospector = true;
                            ModuleAdd.Scrapper = true;
                            ModuleAdd.Energizer = true;
                            ModuleAdd.Assault = true;
                            ModuleAdd.Aviator = true;
                            ModuleAdd.Buccaneer = true;
                            ModuleAdd.Astrotech = true;
                            ModuleAdd.AidAI = true;
                            ModuleAdd.AdvancedAI = true;
                            ModuleAdd.AdvAvoidence = true;
                            ModuleAdd.SidePreferred = true;
                            ModuleAdd.MeleePreferred = true;
                            ModuleAdd.MaxCombatRange = 200;
                        }
                        else if (name == "TAC_AI_Module_Plex_323")
                        {
                            ModuleAdd.Aviator = true;
                            ModuleAdd.Buccaneer = true;
                            ModuleAdd.Astrotech = true;
                            ModuleAdd.AidAI = true;
                            ModuleAdd.AnimeAI = true;
                            ModuleAdd.AdvancedAI = true;
                            ModuleAdd.AdvAvoidence = true;
                            ModuleAdd.MinCombatRange = 100;
                            ModuleAdd.MaxCombatRange = 400;
                        }
                        */
                    }
                    catch (Exception e)
                    {
                        Debug.Log("TACtical_AI: CRASH ON HANDLING EXISTING AIS");
                        Debug.Log(e);
                    }
                }
            }
        }


        [HarmonyPatch(typeof(ResourceDispenser))]
        [HarmonyPatch("OnSpawn")]//On World Spawn
        private static class PatchResourcesToHelpAI
        {
            private static void Prefix(ResourceDispenser __instance)
            {
                //Debug.Log("TACtical_AI: Added resource to list (OnSpawn)");
                if (!AI.AIECore.Minables.Contains(__instance.transform))
                    AI.AIECore.Minables.Add(__instance.transform);
                else
                    Debug.Log("TACtical_AI: RESOURCE WAS ALREADY ADDED! (OnSpawn)");
            }
        }

        [HarmonyPatch(typeof(ResourceDispenser))]
        [HarmonyPatch("Regrow")]//On World Spawn
        private static class PatchResourceRegrowToHelpAI
        {
            private static void Prefix(ResourceDispenser __instance)
            {
                //Debug.Log("TACtical_AI: Added resource to list (OnSpawn)");
                if (!AI.AIECore.Minables.Contains(__instance.transform))
                    AI.AIECore.Minables.Add(__instance.transform);
                //else
                //    Debug.Log("TACtical_AI: RESOURCE WAS ALREADY ADDED! (OnSpawn)");
            }
        }

        [HarmonyPatch(typeof(ResourceDispenser))]
        [HarmonyPatch("Die")]//On resource destruction
        private static class PatchResourceDeathToHelpAI
        {
            private static void Prefix(ResourceDispenser __instance)
            {
                //Debug.Log("TACtical_AI: Removed resource from list (Die)");
                if (AI.AIECore.Minables.Contains(__instance.transform))
                {
                    AI.AIECore.Minables.Remove(__instance.transform);
                }
                else
                    Debug.Log("TACtical_AI: RESOURCE WAS ALREADY REMOVED! (Die)");
            }
        }

        [HarmonyPatch(typeof(ResourceDispenser))]
        [HarmonyPatch("OnRecycle")]//On World Destruction
        private static class PatchResourceRecycleToHelpAI
        {
            private static void Prefix(ResourceDispenser __instance)
            {
                //Debug.Log("TACtical_AI: Removed resource from list (OnRecycle)");
                if (AI.AIECore.Minables.Contains(__instance.transform))
                {
                    AI.AIECore.Minables.Remove(__instance.transform);
                }
                //else
                //    Debug.Log("TACtical_AI: RESOURCE WAS ALREADY REMOVED! (OnRecycle)");

            }
        }

        [HarmonyPatch(typeof(ModuleItemPickup))]
        [HarmonyPatch("OnPool")]//On Creation
        private static class MarkReceiver
        {
            private static void Postfix(ModuleItemPickup __instance)
            {
                var valid = __instance.GetComponent<ModuleItemHolder>();
                if (valid)
                {
                    if (valid.IsFlag(ModuleItemHolder.Flags.Receiver))
                    {
                        var ModuleAdd = __instance.gameObject.AddComponent<ModuleHarvestReciever>();
                        ModuleAdd.OnPool();
                    }
                }
            }
        }

        /*
        [HarmonyPatch(typeof(TechAI))]
        [HarmonyPatch("SetCurrentTree")]//On SettingTechAI
        private class DetectAIChangePatch
        {
            private static void Prefix(TechAI __instance, ref AITreeType aiTreeType)
            {
                if (aiTreeType != null)
                {
                    FieldInfo currentTreeActual = typeof(TechAI).GetField("m_CurrentAITreeType", BindingFlags.NonPublic | BindingFlags.Instance);
                    if ((AITreeType)currentTreeActual.GetValue(__instance) != aiTreeType)
                    {
                        //
                    }
                }
            }
        }
        */
        [HarmonyPatch(typeof(TechAI))]
        [HarmonyPatch("UpdateAICategory")]//On Auto Setting Tech AI
        private class ForceAIToComplyAnchorCorrectly
        {
            private static void Postfix(TechAI __instance)
            {
                var tAI = __instance.gameObject.GetComponent<AI.AIECore.TankAIHelper>();
                if (tAI.IsNotNull())
                {
                    if (tAI.JustUnanchored)
                    {   //Set the AI back to escort to continue operations if autoanchor is true
                        FieldInfo currentTreeActual = typeof(TechAI).GetField("m_CurrentAITreeType", BindingFlags.NonPublic | BindingFlags.Instance);
                        AITreeType AISetting = (AITreeType)currentTreeActual.GetValue(__instance);

                        AISetting.m_TypeName = AITreeType.AITypes.Escort.ToString();

                        currentTreeActual.SetValue(__instance, AISetting);
                        tAI.JustUnanchored = false;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(UIRadialTechControlMenu))]
        [HarmonyPatch("Show")]//On popup
        private static class DetectAIRadialAction
        {
            private static void Prefix(UIRadialTechControlMenu __instance, ref object context)
            {
                OpenMenuEventData nabData = (OpenMenuEventData)context;
                TankBlock thisBlock = nabData.m_TargetTankBlock;
                if (thisBlock.tank.IsNotNull())
                {
                    Debug.Log("TACtical_AI: grabbed tank data = " + thisBlock.tank.name.ToString());
                    GUIAIManager.GetTank(thisBlock.tank);
                }
                else
                {
                    Debug.Log("TACtical_AI: TANK IS NULL!");
                }
            }
        }


        [HarmonyPatch(typeof(UIRadialTechControlMenu))]//UIRadialMenuOptionWithWarning
        [HarmonyPatch("OnAIOptionSelected")]//On AI option
        private static class DetectAIRadialMenuAction
        {
            private static void Prefix(UIRadialTechControlMenu __instance, ref UIRadialTechControlMenu.PlayerCommands command)
            {
                //Debug.Log("TACtical_AI: click menu FIRED!!!  input = " + command.ToString() + " | num = " + (int)command);
                if ((int)command == 3)
                {
                    if (GUIAIManager.IsTankNull())
                    {
                        FieldInfo currentTreeActual = typeof(UIRadialTechControlMenu).GetField("m_TargetTank", BindingFlags.NonPublic | BindingFlags.Instance);
                        Tank tonk = (Tank)currentTreeActual.GetValue(__instance);
                        GUIAIManager.GetTank(tonk);
                        if (GUIAIManager.IsTankNull())
                        {
                            Debug.Log("TACtical_AI: TANK IS NULL AFTER SEVERAL ATTEMPTS!!!");
                        }
                    }
                    GUIAIManager.LaunchSubMenuClickable();
                }

                //Debug.Log("TACtical_AI: click menu " + __instance.gameObject.name);
                //Debug.Log("TACtical_AI: click menu host gameobject " + Nuterra.NativeOptions.UIUtilities.GetComponentTree(__instance.gameObject, __instance.gameObject.name));
            }
        }

        [HarmonyPatch(typeof(TankControl))]
        [HarmonyPatch("CopySchemesFrom")]//On Split
        private static class SetMTAIAuto
        {
            private static void Prefix(TankControl __instance, ref TankControl other)
            {
                if (__instance.Tech.blockman.IterateBlockComponents<ModuleWheels>().Count() > 0 || __instance.Tech.blockman.IterateBlockComponents<ModuleHover>().Count() > 0)
                    __instance.gameObject.GetComponent<AI.AIECore.TankAIHelper>().DediAI = AI.AIECore.DediAIType.Escort;
                else
                {
                    if (__instance.Tech.blockman.IterateBlockComponents<ModuleWeapon>().Count() > 0)
                        __instance.gameObject.GetComponent<AI.AIECore.TankAIHelper>().DediAI = AI.AIECore.DediAIType.MTTurret;
                    else
                        __instance.gameObject.GetComponent<AI.AIECore.TankAIHelper>().DediAI = AI.AIECore.DediAIType.MTSlave;
                }
            }
        }
    }
}
