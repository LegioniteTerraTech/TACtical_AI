using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
//using Harmony;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using TAC_AI.AI;
using TAC_AI.AI.Enemy;
using TAC_AI.Templates;

namespace TAC_AI
{
    class PatchBatch
    {
    }

    internal enum AttractType
    {
        Harvester,
        Invader,
        SpaceInvader,
        Dogfight,
        SpaceBattle,
        NavalWarfare,
        HQSiege,
        Misc,
    }

    internal static class Patches
    {
        // Where it all happens
        [HarmonyPatch(typeof(ModuleTechController))]
        [HarmonyPatch("ExecuteControl")]//On Control
        private static class PatchControlSystem
        {
            private static bool Prefix(ModuleTechController __instance, ref bool __result)
            {
                if (KickStart.EnableBetterAI)
                {
                    //Debug.Log("TACtical_AI: AIEnhanced enabled");
                    try
                    {
                        var aI = __instance.transform.root.GetComponent<Tank>().AI;
                        var tank = __instance.transform.root.GetComponent<Tank>();
                        bool IsPlayerRemoteControlled = false;
                        try
                        {
                            IsPlayerRemoteControlled = (bool)tank.netTech.NetPlayer;
                        }
                        catch { }
                        if (!tank.PlayerFocused && !IsPlayerRemoteControlled)//&& !Singleton.Manager<ManGameMode>.inst.IsCurrentModeMultiplayer())
                        {
                            var tankAIHelp = tank.gameObject.GetComponent<AIECore.TankAIHelper>();

                            if (tank.FirstUpdateAfterSpawn)
                            {
                                // let the icon update
                            }
                            else if (aI.CheckAIAvailable() && tank.IsFriendly())
                            {
                                //Debug.Log("TACtical_AI: AI Valid!");
                                //Debug.Log("TACtical_AI: (TankAIHelper) is " + tank.gameObject.GetComponent<AIEnhancedCore.TankAIHelper>().wasEscort);
                                //tankAIHelp.AIState && 
                                if (tankAIHelp.JustUnanchored)
                                {
                                    tankAIHelp.ForceAllAIsToEscort();
                                    tankAIHelp.JustUnanchored = false;
                                }
                                else if (tankAIHelp.lastAIType == AITreeType.AITypes.Escort)
                                {
                                    //Debug.Log("TACtical_AI: Running BetterAI");
                                    //Debug.Log("TACtical_AI: Patched Tank ExecuteControl(TankAIHelper)");
                                    tankAIHelp.BetterAI(__instance.block.tank.control);
                                    __result = true;
                                    return false;
                                }
                            }
                            else if (tankAIHelp.OverrideAllControls)
                            {   // override EVERYTHING
                                if (__instance.block.tank.Anchors.NumIsAnchored > 0)
                                    __instance.block.tank.Anchors.UnanchorAll(true);
                                __instance.block.tank.control.BoostControlJets = true;
                                __result = true;
                                return false;
                            }
                            else if ((KickStart.testEnemyAI || KickStart.isTougherEnemiesPresent) && KickStart.enablePainMode && tank.IsEnemy() && !ManSpawn.IsPlayerTeam(tank.Team))
                            {
                                if (!tankAIHelp.Hibernate)
                                {
                                    tankAIHelp.BetterAI(__instance.block.tank.control);
                                    __result = true;
                                    return false;
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.Log("TACtical_AI: Failure on handling AI addition!");
                        Debug.Log(e);
                    }
                }
                return true;
            }
        }

        // this is a VERY big mod
        //   we must make it look big like it is
        [HarmonyPatch(typeof(Mode))]
        [HarmonyPatch("EnterPreMode")]//On very late update
        private static class Startup
        {
            private static void Prefix()
            {
                if (!KickStart.firedAfterBlockInjector)//KickStart.isBlockInjectorPresent && 
                    KickStart.DelayedBaseLoader();
            }
        }

        [HarmonyPatch(typeof(ModeAttract))]
        [HarmonyPatch("UpdateModeImpl")] // Checking title techs
        private static class RestartAttract
        {
            private static void Prefix(ModeAttract __instance)
            {
                FieldInfo state = typeof(ModeAttract).GetField("m_State", BindingFlags.NonPublic | BindingFlags.Instance);
                int mode = (int)state.GetValue(__instance);
                if (mode == 2)
                {
                    if (KickStart.SpecialAttractNum == AttractType.Harvester)
                    {
                        bool restart = false;
                        List<Tank> active = Singleton.Manager<ManTechs>.inst.CurrentTechs.ToList();
                        foreach (Tank tonk in active)
                        {
                            if ((tonk.boundsCentreWorldNoCheck - Singleton.cameraTrans.position).magnitude > 125)
                                restart = true;
                        }
                        if (restart == true)
                        {
                            UILoadingScreenHints.SuppressNextHint = true;
                            Singleton.Manager<ManUI>.inst.FadeToBlack();
                            state.SetValue(__instance, 3);
                        }
                    }
                    else
                    {
                        bool restart = true;
                        List<Tank> active = Singleton.Manager<ManTechs>.inst.CurrentTechs.ToList();
                        foreach (Tank tonk in active)
                        {
                            if (tonk.Weapons.GetFirstWeapon().IsNotNull())
                            {
                                foreach (Tank tonk2 in active)
                                {
                                    if (tonk.IsEnemy(tonk2.Team))
                                        restart = false;
                                }
                            }
                            if (tonk.IsSleeping)
                            {
                                foreach (TankBlock block in tonk.blockman.IterateBlocks())
                                {
                                    block.damage.SelfDestruct(0.5f);
                                }
                                tonk.blockman.Disintegrate(true, false);
                            }
                        }
                        if (restart == true)
                        {
                            UILoadingScreenHints.SuppressNextHint = true;
                            Singleton.Manager<ManUI>.inst.FadeToBlack();
                            state.SetValue(__instance, 3);
                        }
                    }
                }

            }
        }

        [HarmonyPatch(typeof(ModeAttract))]
        [HarmonyPatch("SetupTerrain")]// Setup main menu scene
        private static class SetupTerrainCustom
        {
            private static bool Prefix(ModeAttract __instance)
            {
                // Testing
                bool caseOverride = true;
                AttractType outNum = AttractType.Harvester;


                if (UnityEngine.Random.Range(1, 100) > 20 || KickStart.retryForBote == 1 || caseOverride)
                {
                    Debug.Log("TACtical_AI: Ooop - the special threshold has been met");
                    KickStart.SpecialAttract = true;
                    if (KickStart.retryForBote == 1)
                        outNum = AttractType.NavalWarfare;
                    else if (!caseOverride)
                        outNum = (AttractType)UnityEngine.Random.Range(0, Enum.GetValues(typeof(AttractType)).Length);
                    KickStart.SpecialAttractNum = outNum;

                    if (KickStart.SpecialAttractNum == AttractType.NavalWarfare)
                    {   // Naval Brawl
                        if (KickStart.isWaterModPresent)
                        {
                            KickStart.retryForBote++;
                            Singleton.cameraTrans.position = KickStart.SpecialAttractPos;
                            Singleton.cameraTrans.rotation = Quaternion.LookRotation(Vector3.forward);
                            Vector3 offset = Vector3.zero;
                            offset.x = -50.0f;
                            offset.z = 100.0f;
                            //offset.x = -240.0f;
                            //offset.z = 442.0f;
                            BiomeMap edited = __instance.spawns[0].biomeMap;
                            Singleton.Manager<ManWorld>.inst.SeedString = "68unRTyXMrX93DH";
                            Singleton.Manager<ManGameMode>.inst.RegenerateWorld(edited, __instance.spawns[1].cameraSpawn.forward, Quaternion.LookRotation(__instance.spawns[1].cameraSpawn.forward, Vector3.up));
                            Singleton.Manager<ManTimeOfDay>.inst.EnableSkyDome(enable: true);
                            Singleton.Manager<ManTimeOfDay>.inst.EnableTimeProgression(enable: false);
                            Singleton.Manager<ManTimeOfDay>.inst.SetTimeOfDay(UnityEngine.Random.Range(8, 18), 0, 0);
                            KickStart.SpecialAttractPos = offset;
                            Singleton.cameraTrans.position = KickStart.SpecialAttractPos;
                            Singleton.cameraTrans.rotation = Quaternion.LookRotation(Vector3.forward);

                            return false;
                        }
                    }
                }
                else
                    KickStart.SpecialAttract = false;
                return true;
            }
        }

        [HarmonyPatch(typeof(ModeAttract))]
        [HarmonyPatch("SetupTerrain")]// Setup main menu scene
        private static class RandomTime
        {
            private static void Postfix()
            {
                try
                {
                    if (KickStart.SpecialAttract)
                    {
                        Singleton.Manager<ManTimeOfDay>.inst.SetTimeOfDay(UnityEngine.Random.Range(8, 18), 0, 0);
                    }
                }
                catch { }
            }
        }

        [HarmonyPatch(typeof(ModeAttract))]
        [HarmonyPatch("SetupTechs")]// Setup main menu techs
        private static class ThrowCoolAIInAttract
        {
            private static void Postfix()
            {
                try
                {
                    if (KickStart.SpecialAttract)
                    {
                        Tank tankPos = Singleton.Manager<ManTechs>.inst.CurrentTechs.First();
                        Vector3 spawn = tankPos.boundsCentreWorld + (tankPos.rootBlockTrans.forward * 20);
                        Singleton.Manager<ManWorld>.inst.GetTerrainHeight(spawn, out float height);
                        spawn.y = height;

                        int TechCount = Singleton.Manager<ManTechs>.inst.CurrentTechs.Count();
                        List<Tank> tanksToConsider = Singleton.Manager<ManTechs>.inst.CurrentTechs.ToList();


                        AttractType randNum = KickStart.SpecialAttractNum;
                        if (randNum == AttractType.SpaceInvader)
                        {   // space invader
                            //Debug.Log("TACtical_AI: Throwing in TAC ref lol");
                            RawTechLoader.SpawnAttractTech(spawn, 749, Vector3.forward, BaseTerrain.Space);
                        }
                        else if (randNum == AttractType.Harvester)
                        {   // Peaceful harvesting
                            for (int step = 0; TechCount > step; step++)
                            {
                                Tank tech = tanksToConsider.ElementAt(step);
                                Vector3 position = tech.boundsCentreWorld;// - (tech.rootBlockTrans.forward * 32);

                                if (step == 0)
                                {
                                    RawTechLoader.SpawnSpecificTypeTech(spawn, 1, -tech.rootBlockTrans.forward, new List<BasePurpose> { BasePurpose.HasReceivers }, silentFail: false);
                                    RawTechLoader.SpawnSpecificTypeTech(position, 1, -tech.rootBlockTrans.forward, new List<BasePurpose> { BasePurpose.NotStationary, BasePurpose.Harvesting }, silentFail: false);
                                }
                                //if (RawTechLoader.SpawnSpecificTypeTech(position, 1, -tech.rootBlockTrans.forward, new List<BasePurpose> { BasePurpose.NotStationary, BasePurpose.Harvesting }, silentFail: false))
                                    tech.visible.RemoveFromGame();
                            }
                        }
                        else if (randNum == AttractType.Dogfight)
                        {   // Aircraft fight
                            for (int step = 0; TechCount > step; step++)
                            {
                                Tank tech = tanksToConsider.ElementAt(step);
                                Vector3 position = tech.boundsCentreWorld;// - (tech.rootBlockTrans.forward * 32);
                                position.y += 64;

                                if (RawTechLoader.SpawnAttractTech(position, (int)(UnityEngine.Random.Range(1, 999) + 0.5f), -tech.rootBlockTrans.forward, BaseTerrain.Air, silentFail: false))
                                    tech.visible.RemoveFromGame();
                            }
                        }
                        else if (randNum == AttractType.SpaceBattle)
                        {   // Airship assault
                            for (int step = 0; TechCount > step; step++)
                            {
                                Tank tech = tanksToConsider.ElementAt(step);
                                Vector3 position = tech.boundsCentreWorld;// - (tech.rootBlockTrans.forward * 48);
                                position.y += 64;

                                if (RawTechLoader.SpawnAttractTech(position, (int)(UnityEngine.Random.Range(1, 999) + 0.5f), tech.rootBlockTrans.forward, BaseTerrain.Space, silentFail: false))
                                    tech.visible.RemoveFromGame();
                            }
                        }
                        else if (randNum == AttractType.NavalWarfare)
                        {   // Naval Brawl
                            if (KickStart.isWaterModPresent)
                            {
                                Camera.main.transform.position = KickStart.SpecialAttractPos;
                                int removed = 0;
                                foreach (Visible vis in Singleton.Manager<ManVisible>.inst.VisiblesTouchingRadius(KickStart.SpecialAttractPos, 2500, new Bitfield<ObjectTypes>(new ObjectTypes[1] { ObjectTypes.Scenery })))
                                {
                                    if (vis.resdisp.IsNotNull() && vis.centrePosition.y < -25)
                                    {
                                        vis.resdisp.RemoveFromWorld(false, true, true, true);
                                        removed++;
                                    }
                                }
                                Debug.Log("TACtical_AI: removed " + removed);
                                for (int step = 0; TechCount > step; step++)
                                {
                                    Tank tech = tanksToConsider.ElementAt(step);
                                    tech.transform.position = KickStart.SpecialAttractPos;
                                    Vector3 forward = Quaternion.AngleAxis((360 / TechCount) * (step + 1), Vector3.up) * Vector3.forward;
                                    Vector3 position = tech.boundsCentreWorld - (forward * 10);
                                    position = AI.Movement.AIEPathing.ForceOffsetToSea(position);
                                    tech.visible.RemoveFromGame();

                                    if (!RawTechLoader.SpawnAttractTech(position, (int)(UnityEngine.Random.Range(1, 999) + 0.5f), forward, Templates.BaseTerrain.Sea, silentFail: false))
                                        RawTechLoader.SpawnAttractTech(position, (int)(UnityEngine.Random.Range(1, 999) + 0.5f), forward, Templates.BaseTerrain.Space, silentFail: false);
                                }
                                //Debug.Log("TACtical_AI: cam is at " + Singleton.Manager<CameraManager>.inst.ca);
                                Singleton.Manager<CameraManager>.inst.ResetCamera(KickStart.SpecialAttractPos, Quaternion.LookRotation(Vector3.forward));
                                Singleton.cameraTrans.position = KickStart.SpecialAttractPos;
                                Singleton.cameraTrans.rotation = Quaternion.LookRotation(Vector3.forward);
                            }
                            else
                                RawTechLoader.SpawnAttractTech(spawn, 749, Vector3.forward, Templates.BaseTerrain.Land);
                        }
                        else if (randNum == AttractType.HQSiege)
                        {   // HQ Siege
                            foreach (Tank tech in Singleton.Manager<ManTechs>.inst.CurrentTechs)
                            {
                                tech.SetTeam(4114);
                            }
                            tankPos.SetTeam(916);
                            RawTechLoader.SpawnAttractTech(spawn, tankPos.Team, Vector3.forward, Templates.BaseTerrain.Land, tankPos.GetMainCorp(), Templates.BasePurpose.Headquarters);
                        }
                        else if (randNum == AttractType.Misc)
                        {   // pending
                            for (int step = 0; TechCount > step; step++)
                            {
                                Tank tech = tanksToConsider.ElementAt(step);
                                Vector3 position = tech.boundsCentreWorld; //- (tech.rootBlockTrans.forward * 10);
                                position.y += 10;

                                if (RawTechLoader.SpawnAttractTech(position, (int)(UnityEngine.Random.Range(1, 999) + 0.5f), tech.rootBlockTrans.forward, BaseTerrain.AnyNonSea))
                                    tech.visible.RemoveFromGame();
                            }
                            RawTechLoader.SpawnAttractTech(spawn, 749, Vector3.forward, BaseTerrain.Air);
                        }
                        else // AttractType.Invader
                        {   // Land battle invoker
                            RawTechLoader.SpawnAttractTech(spawn, 749, Vector3.forward, BaseTerrain.Land);
                        }

                        Debug.Log("TACtical_AI: Setup for attract type " + KickStart.SpecialAttractNum.ToString());
                    }
                }
                catch { }
            }
        }

        [HarmonyPatch(typeof(Tank))]
        [HarmonyPatch("OnPool")]//On Creation
        private static class PatchTankToHelpAI
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

        // Enemy AI's ability to "Lock On"
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
                            ModuleAdd.Prospector = true;
                            ModuleAdd.Buccaneer = true;
                            ModuleAdd.AidAI = true;
                            //ModuleAdd.SelfRepairAI = true; // testing
                        }
                        else if (name == "GSO_AIAnchor_121")
                        {
                            ModuleAdd.Aegis = true;
                            ModuleAdd.AidAI = true;
                            ModuleAdd.MaxCombatRange = 150;
                        }
                        else if (name == "GC_AI_Module_Guard_222")
                        {
                            //ModuleAdd.AutoAnchor = true; // temp testing
                            ModuleAdd.Prospector = true;
                            ModuleAdd.Energizer = true;
                            ModuleAdd.MTForAll = true;
                            ModuleAdd.MeleePreferred = true;
                        }
                        else if (name == "VEN_AI_Module_Guard_111")
                        {
                            ModuleAdd.Aviator = true;
                            ModuleAdd.Buccaneer = true;
                            ModuleAdd.SidePreferred = true;
                            ModuleAdd.MaxCombatRange = 300;
                        }
                        else if (name == "HE_AI_Module_Guard_112")
                        {
                            ModuleAdd.Assault = true;
                            ModuleAdd.Aviator = true;
                            ModuleAdd.Astrotech = true;
                            ModuleAdd.AdvancedAI = true;
                            ModuleAdd.MinCombatRange = 50;
                            ModuleAdd.MaxCombatRange = 200;
                        }
                        else if (name == "HE_AI_Turret_111")
                        {
                            ModuleAdd.Assault = true;
                            ModuleAdd.AdvancedAI = true;
                            ModuleAdd.MinCombatRange = 50;
                            ModuleAdd.MaxCombatRange = 225;
                        }
                        else if (name == "BF_AI_Module_Guard_212")
                        {
                            ModuleAdd.Astrotech = true;
                            ModuleAdd.AdvAvoidence = true;
                            ModuleAdd.SelfRepairAI = true; // EXTREMELY POWERFUL
                            ModuleAdd.MinCombatRange = 60;
                            ModuleAdd.MaxCombatRange = 250;
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
                            ModuleAdd.Prospector = true;
                            ModuleAdd.Scrapper = true;
                            ModuleAdd.MTForAll = true;
                            ModuleAdd.MinCombatRange = 60;
                            ModuleAdd.MaxCombatRange = 120;
                        }
                        else if (name == "TSN_AI_Module_Guard_312")
                        {
                            ModuleAdd.AutoAnchor = true;
                            ModuleAdd.Buccaneer = true;
                            ModuleAdd.AdvAvoidence = true;
                            ModuleAdd.MinCombatRange = 150;
                            ModuleAdd.MaxCombatRange = 250;
                        }
                        else if (name == "LEG_AI_Module_Guard_112")
                        {   //Incase Legion happens and the AI needs help lol
                            ModuleAdd.AutoAnchor = true;
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
                            ModuleAdd.AutoAnchor = true;
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

        /* // Can't make this work - there's too many random checks prohibiting this
        [HarmonyPatch(typeof(TargetAimer))]//
        [HarmonyPatch("UpdateTarget")]//On targeting
        private static class PatchAimingToHelpPlasmaCutter_Auto
        {
            static FieldInfo targPos = typeof(TargetAimer).GetField("m_TargetPosition", BindingFlags.NonPublic | BindingFlags.Instance);
            static FieldInfo targ = typeof(TargetAimer).GetField("Target", BindingFlags.NonPublic | BindingFlags.Instance);
            private static bool Prefix(TargetAimer __instance)
            {
                if (__instance.gameObject.name == "GC_PlasmaCutter_Auto_434")
                {
                    var AICommand = __instance.transform.root.GetComponent<AIECore.TankAIHelper>();
                    if (AICommand.IsNotNull() && !KickStart.isWeaponAimModPresent)
                    {
                        var tank = __instance.transform.root.GetComponent<Tank>();
                        if (tank.IsNotNull())
                        {// give that gimbal cutter the ability to mine resources!
                            if (AIECore.FetchClosestResource(__instance.GetComponent<TankBlock>().centreOfMassWorld, 75, out Visible theResource))
                            {
                                //Debug.Log("TACtical_AI: Overriding PlasmaCutter_Auto to aim at resources");
                                try
                                {
                                    targ.SetValue(__instance, theResource);
                                }
                                catch { }
                                targPos.SetValue(__instance, theResource.centrePosition);
                                return false;
                            }
                        }
                    }
                }
                return true;
            }
        }*/

        [HarmonyPatch(typeof(TargetAimer))]//
        [HarmonyPatch("UpdateTarget")]//On targeting
        private static class PatchAimingToHelpAI
        {
            static FieldInfo targPos = typeof(TargetAimer).GetField("m_TargetPosition", BindingFlags.NonPublic | BindingFlags.Instance);
            private static void Postfix(TargetAimer __instance)
            {
                var AICommand = __instance.transform.root.GetComponent<AIECore.TankAIHelper>();
                if (AICommand.IsNotNull() && !KickStart.isWeaponAimModPresent)
                {
                    var tank = __instance.transform.root.GetComponent<Tank>();
                    if (tank.IsNotNull())
                    {
                        if (!tank.PlayerFocused)
                        {
                            if (AICommand.OverrideAim == 1)
                            {
                                if (AICommand.lastEnemy.IsNotNull())
                                {
                                    //Debug.Log("TACtical_AI: Overriding targeting to aim at " + AICommand.lastEnemy.name + "  pos " + AICommand.lastEnemy.tank.boundsCentreWorldNoCheck);
                                    //FieldInfo targ = typeof(TargetAimer).GetField("Target", BindingFlags.NonPublic | BindingFlags.Instance);
                                    //targ.SetValue(__instance, AICommand.lastEnemy);

                                    //targPos.SetValue(__instance, tank.control.TargetPositionWorld);
                                    if ((bool)AICommand.lastEnemy.tank.CentralBlock && AICommand.lastEnemy.isActive)
                                    {
                                        targPos.SetValue(__instance, AICommand.lastEnemy.GetAimPoint(__instance.transform.position));
                                    }
                                    else
                                        targPos.SetValue(__instance, tank.control.TargetPositionWorld);
                                    //Debug.Log("TACtical_AI: final aim is " + targPos.GetValue(__instance));

                                }
                            }
                            else if (AICommand.OverrideAim == 2)
                            {
                                if (AICommand.Obst.IsNotNull())
                                {
                                    //Debug.Log("TACtical_AI: Overriding targeting to aim at obstruction");

                                    targPos.SetValue(__instance, AICommand.Obst.position + (Vector3.up * 2));

                                }
                            }
                            else if (AICommand.OverrideAim == 3)
                            {
                                if (AICommand.LastCloseAlly.IsNotNull())
                                {
                                    //Debug.Log("TACtical_AI: Overriding targeting to aim at player's target");

                                    targPos.SetValue(__instance, AICommand.LastCloseAlly.control.TargetPositionWorld);

                                }
                            }
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(ModuleWeapon))]
        [HarmonyPatch("UpdateAutoAimBehaviour")]//On targeting
        private static class PatchAimingSystemsToHelpAI
        {
            static FieldInfo aimers = typeof(ModuleWeapon).GetField("m_TargetAimer", BindingFlags.NonPublic | BindingFlags.Instance);
            static FieldInfo aimerTargPos = typeof(TargetAimer).GetField("m_TargetPosition", BindingFlags.NonPublic | BindingFlags.Instance);
            static FieldInfo WeaponTargPos = typeof(ModuleWeapon).GetField("m_TargetPosition", BindingFlags.NonPublic | BindingFlags.Instance);
            private static void Postfix(ModuleWeapon __instance)
            {
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


        // Resources/Collection
        [HarmonyPatch(typeof(ResourceDispenser))]
        [HarmonyPatch("OnSpawn")]//On World Spawn
        private static class PatchResourcesToHelpAI
        {
            private static void Prefix(ResourceDispenser __instance)
            {
                //Debug.Log("TACtical_AI: Added resource to list (OnSpawn)");
                if (!AI.AIECore.Minables.Contains(__instance.visible))
                    AI.AIECore.Minables.Add(__instance.visible);
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
                if (!AI.AIECore.Minables.Contains(__instance.visible))
                    AI.AIECore.Minables.Add(__instance.visible);
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
                if (AI.AIECore.Minables.Contains(__instance.visible))
                {
                    AI.AIECore.Minables.Remove(__instance.visible);
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
                if (AI.AIECore.Minables.Contains(__instance.visible))
                {
                    AI.AIECore.Minables.Remove(__instance.visible);
                }
                //else
                //    Debug.Log("TACtical_AI: RESOURCE WAS ALREADY REMOVED! (OnRecycle)");

            }
        }

        [HarmonyPatch(typeof(ResourceDispenser))]
        [HarmonyPatch("Deactivate")]//On instant remove
        private static class PatchResourceDeactivateToHelpAI
        {
            private static void Prefix(ResourceDispenser __instance)
            {
                //Debug.Log("TACtical_AI: Removed resource from list (Deactivate)");
                if (AIECore.Minables.Contains(__instance.visible))
                {
                    AIECore.Minables.Remove(__instance.visible);
                }
                //else
                //    Debug.Log("TACtical_AI: RESOURCE WAS ALREADY REMOVED! (Deactivate)");

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

        [HarmonyPatch(typeof(ModuleRemoteCharger))]
        [HarmonyPatch("OnPool")]//On Creation
        private static class MarkChargers
        {
            private static void Postfix(ModuleRemoteCharger __instance)
            {
                var ModuleAdd = __instance.gameObject.AddComponent<ModuleChargerTracker>();
                ModuleAdd.OnPool();
            }
        }

        [HarmonyPatch(typeof(ModuleItemConsume))]
        [HarmonyPatch("InitRecipeOutput")]//On Creation
        private static class LetEnemiesSellStuff
        {
            static readonly FieldInfo progress = typeof(ModuleItemConsume).GetField("m_ConsumeProgress", BindingFlags.NonPublic | BindingFlags.Instance);
            static readonly FieldInfo sellStolen = typeof(ModuleItemConsume).GetField("m_OperateItemInterceptedBy", BindingFlags.NonPublic | BindingFlags.Instance);
            
            private static void Prefix(ModuleItemConsume __instance)
            {
                var valid = __instance.transform.root.GetComponent<RBases.EnemyBaseFunder>();
                if ((bool)valid && (ManNetwork.IsHost || !ManNetwork.IsNetworked))
                {
                    ModuleItemConsume.Progress pog = (ModuleItemConsume.Progress)progress.GetValue(__instance);
                    if (pog.currentRecipe.m_OutputType == RecipeTable.Recipe.OutputType.Money && sellStolen.GetValue(__instance) == null)
                    {
                        int sellGain = pog.currentRecipe.m_MoneyOutput;

                        WorldPosition pos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(__instance.block.visible);
                        Singleton.Manager<ManOverlay>.inst.AddFloatingTextOverlay(Singleton.Manager<Localisation>.inst.GetMoneyStringWithSymbol(sellGain), pos);
                        if (Singleton.Manager<ManNetwork>.inst.IsServer)
                        {
                            PopupNumberMessage message = new PopupNumberMessage
                            {
                                m_Type = PopupNumberMessage.Type.Money,
                                m_Number = sellGain,
                                m_Position = pos
                            };
                            Singleton.Manager<ManNetwork>.inst.SendToAllExceptHost(TTMsgType.AddFloatingNumberPopupMessage, message);
                        }
                        valid.AddBuildBucks(sellGain);
                    }
                }
            }
        }


        // Allied AI state changing remotes
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
            static FieldInfo currentTreeActual = typeof(TechAI).GetField("m_CurrentAITreeType", BindingFlags.NonPublic | BindingFlags.Instance);
            private static void Postfix(TechAI __instance)
            {
                var tAI = __instance.gameObject.GetComponent<AI.AIECore.TankAIHelper>();
                if (tAI.IsNotNull())
                {
                    if (tAI.JustUnanchored && tAI.AIState == 1)
                    {   //Set the AI back to escort to continue operations if autoanchor is true
                        __instance.SetBehaviorType(AITreeType.AITypes.Escort);
                        if (!__instance.TryGetCurrentAIType(out AITreeType.AITypes type))
                        {
                            if (type != AITreeType.AITypes.Escort)
                            {
                                AITreeType AISetting = (AITreeType)currentTreeActual.GetValue(__instance);

                                AISetting.m_TypeName = AITreeType.AITypes.Escort.ToString();

                                currentTreeActual.SetValue(__instance, AISetting);
                                tAI.JustUnanchored = false;
                            }
                            else
                                tAI.JustUnanchored = false;
                        }
                        else
                        {
                            AITreeType AISetting = (AITreeType)currentTreeActual.GetValue(__instance);

                            AISetting.m_TypeName = AITreeType.AITypes.Escort.ToString();

                            currentTreeActual.SetValue(__instance, AISetting);
                            tAI.JustUnanchored = false;
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(UIRadialTechControlMenu))]
        [HarmonyPatch("Show")]//On popup
        private static class DetectAIRadialAction
        {
            private static void Prefix(ref object context)
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

        [HarmonyPatch(typeof(UIRadialTechControlMenu))]
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
                    __instance.gameObject.GetComponent<AI.AIECore.TankAIHelper>().DediAI = AIType.Escort;
                else
                {
                    if (__instance.Tech.blockman.IterateBlockComponents<ModuleWeapon>().Count() > 0)
                        __instance.gameObject.GetComponent<AI.AIECore.TankAIHelper>().DediAI = AIType.MTTurret;
                    else
                        __instance.gameObject.GetComponent<AI.AIECore.TankAIHelper>().DediAI = AIType.MTSlave;
                }
            }
        }


        // CampaignAutohandling
        [HarmonyPatch(typeof(ModeMain))]
        [HarmonyPatch("PlayerRespawned")]//On player base bomb landing
        private static class OverridePlayerTechOnWaterLanding
        {
            private static void Postfix()
            {
                Debug.Log("TACtical_AI: Player respawned");
                if (!KickStart.isPopInjectorPresent && KickStart.isWaterModPresent)
                {
                    Debug.Log("TACtical_AI: Precheck validated");
                    if (AI.Movement.AIEPathing.AboveTheSea(Singleton.playerTank.boundsCentreWorld))
                    {
                        Debug.Log("TACtical_AI: Attempting retrofit");
                        PlayerSpawnAid.TryBotePlayerSpawn();
                    }
                }
            }
        }

        [HarmonyPatch(typeof(ManPop))]
        [HarmonyPatch("OnSpawned")]//On enemy base bomb landing
        private static class EmergencyOverrideOnTechLanding
        {
            private static bool TankExists(TrackedVisible tv)
            {
                if (tv != null)
                {
                    if (tv.visible != null)
                    {
                        if (tv.visible.tank != null)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            private static void Prefix(ref TrackedVisible tv)
            {
                if (!KickStart.isPopInjectorPresent && KickStart.EnableBetterAI && (ManNetwork.IsHost || !ManNetwork.IsNetworked))
                {
                    if (!TankExists(tv))
                        return;
                    if (tv.visible.tank.IsPopulation)
                    {
                        if (KickStart.AllowSeaEnemiesToSpawn && KickStart.isWaterModPresent && AI.Movement.AIEPathing.AboveTheSea(tv.visible.tank.boundsCentreWorld) && RCore.EnemyHandlingDetermine(tv.visible.tank) != EnemyHandling.Naval)
                        {
                            // OVERRIDE TO SHIP
                            try
                            {
                                int grade = 99;
                                try
                                {
                                    if (!SpecialAISpawner.CreativeMode)
                                        grade = ManLicenses.inst.GetCurrentLevel(tv.visible.tank.GetMainCorp());
                                }
                                catch { }


                                if (RawTechLoader.ShouldUseCustomTechs(tv.visible.tank.GetMainCorp(), BasePurpose.NotStationary, BaseTerrain.Sea, maxGrade: grade))
                                {
                                    RadarTypes inherit = tv.RadarType;
                                    string previousTechName = tv.visible.tank.name;
                                    Vector3 pos = tv.Position;
                                    Vector3 posF = tv.visible.tank.rootBlockTrans.forward;
                                    int team = tv.TeamID;
                                    bool wasPop = tv.visible.tank.IsPopulation;

                                    RawTechLoader.TryRemoveFromPop(tv.visible.tank);
                                    SpecialAISpawner.Purge(tv.visible.tank);
                                    pos = AI.Movement.AIEPathing.ForceOffsetToSea(pos);

                                    Tank replacementBote = RawTechLoader.SpawnEnemyTechExternal(pos, team, posF, TempManager.ExternalEnemyTechs[RawTechLoader.GetExternalIndex(tv.visible.tank.GetMainCorp(), BasePurpose.NotStationary, BaseTerrain.Sea, maxGrade: grade)], AutoTerrain: false);
                                    replacementBote.SetTeam(tv.TeamID, wasPop);

                                    Debug.Log("TACtical_AI:  Tech " + previousTechName + " landed in water and was likely not water-capable, naval Tech " + replacementBote.name + " was substituted for the spawn instead");
                                    tv = ManVisible.inst.GetTrackedVisible(replacementBote.visible.ID);
                                    if (tv == null)
                                        tv = new TrackedVisible(replacementBote.visible.ID, replacementBote.visible, ObjectTypes.Vehicle, inherit);
                                }
                                else
                                {
                                    SpawnBaseTypes type = RawTechLoader.GetEnemyBaseType(tv.visible.tank.GetMainCorp(), BasePurpose.NotStationary, BaseTerrain.Sea, maxGrade: grade);
                                    if (type != SpawnBaseTypes.NotAvail && !RawTechLoader.IsFallback(type))
                                    {
                                        RadarTypes inherit = tv.RadarType;
                                        string previousTechName = tv.visible.tank.name;
                                        Vector3 pos = tv.Position;
                                        Vector3 posF = tv.visible.tank.rootBlockTrans.forward;
                                        int team = tv.TeamID;
                                        bool wasPop = tv.visible.tank.IsPopulation;

                                        RawTechLoader.TryRemoveFromPop(tv.visible.tank);
                                        SpecialAISpawner.Purge(tv.visible.tank);
                                        pos = AI.Movement.AIEPathing.ForceOffsetToSea(pos);

                                        Tank replacementBote = RawTechLoader.SpawnMobileTech(pos, posF, team, type, AutoTerrain: false);
                                        replacementBote.SetTeam(tv.TeamID, wasPop);

                                        Debug.Log("TACtical_AI:  Tech " + previousTechName + " landed in water and was likely not water-capable, naval Tech " + replacementBote.name + " was substituted for the spawn instead");
                                        tv = ManVisible.inst.GetTrackedVisible(replacementBote.visible.ID);
                                        if (tv == null)
                                            tv = new TrackedVisible(replacementBote.visible.ID, replacementBote.visible, ObjectTypes.Vehicle, inherit);
                                    }
                                    // Else we don't do anything.
                                }
                            }
                            catch
                            {
                                Debug.Log("TACtical_AI:  attempt to swap tech failed, blowing up tech due to water landing");

                                for (int fire = 0; fire < 25; fire++)
                                {
                                    TankBlock boom = RawTechLoader.SpawnBlockS(BlockTypes.VENFuelTank_212, tv.Position, Quaternion.LookRotation(Vector3.forward), out bool worked);
                                    if (!worked)
                                    {
                                        boom.visible.SetInteractionTimeout(20);
                                        boom.damage.SelfDestruct(0.5f);
                                    }
                                }
                                try
                                {
                                    SpecialAISpawner.Eradicate(tv.visible.tank);

                                    /*
                                    foreach (TankBlock block in tv.visible.tank.blockman.IterateBlocks())
                                    {
                                        block.visible.SetInteractionTimeout(20);
                                        block.damage.SelfDestruct(0.5f);
                                        block.damage.Explode(true);
                                    }
                                    tv.visible.tank.blockman.Disintegrate(true, false);
                                    if (tv.visible.IsNotNull())
                                        tv.visible.trans.Recycle();
                                    */
                                }
                                catch { }
                            }
                        }
                        else if (UnityEngine.Random.Range(0, 100) < KickStart.LandEnemyOverrideChance) // Override for normal Tech spawns
                        {
                            // OVERRIDE TECH SPAWN
                            try
                            {
                                int grade = 99;
                                try
                                {
                                    if (!SpecialAISpawner.CreativeMode)
                                        grade = ManLicenses.inst.GetCurrentLevel(tv.visible.tank.GetMainCorp());
                                }
                                catch { }
                                if (RawTechLoader.ShouldUseCustomTechs(tv.visible.tank.GetMainCorp(), BasePurpose.NotStationary, BaseTerrain.Land, maxGrade: grade, maxPrice: KickStart.EnemySpawnPriceMatching))
                                {
                                    RadarTypes inherit = tv.RadarType;
                                    string previousTechName = tv.visible.tank.name;
                                    Vector3 pos = tv.Position;
                                    Vector3 posF = tv.visible.tank.rootBlockTrans.forward;
                                    int team = tv.TeamID;
                                    bool wasPop = tv.visible.tank.IsPopulation;

                                    RawTechLoader.TryRemoveFromPop(tv.visible.tank);
                                    SpecialAISpawner.Purge(tv.visible.tank);
                                    pos = AI.Movement.AIEPathing.ForceOffsetToSea(pos);

                                    Tank replacementTech = RawTechLoader.SpawnEnemyTechExternal(pos, team, posF, TempManager.ExternalEnemyTechs[RawTechLoader.GetExternalIndex(tv.visible.tank.GetMainCorp(), BasePurpose.NotStationary, BaseTerrain.Land, maxGrade: grade, maxPrice: KickStart.EnemySpawnPriceMatching)], AutoTerrain: false);
                                    replacementTech.SetTeam(tv.TeamID, wasPop);

                                    Debug.Log("TACtical_AI:  Tech " + previousTechName + " has been swapped out for land tech " + replacementTech.name + " instead");
                                    tv = ManVisible.inst.GetTrackedVisible(replacementTech.visible.ID);
                                    if (tv == null)
                                        tv = new TrackedVisible(replacementTech.visible.ID, replacementTech.visible, ObjectTypes.Vehicle, inherit);
                                }
                                else
                                {
                                    SpawnBaseTypes type = RawTechLoader.GetEnemyBaseType(tv.visible.tank.GetMainCorp(), BasePurpose.NotStationary, BaseTerrain.Land, maxGrade: grade, maxPrice: KickStart.EnemySpawnPriceMatching);
                                    if (type != SpawnBaseTypes.NotAvail && !RawTechLoader.IsFallback(type))
                                    {
                                        RadarTypes inherit = tv.RadarType;
                                        string previousTechName = tv.visible.tank.name;
                                        Vector3 pos = tv.Position;
                                        Vector3 posF = tv.visible.tank.rootBlockTrans.forward;
                                        int team = tv.TeamID;
                                        bool wasPop = tv.visible.tank.IsPopulation;

                                        RawTechLoader.TryRemoveFromPop(tv.visible.tank);
                                        SpecialAISpawner.Purge(tv.visible.tank);
                                        pos = AI.Movement.AIEPathing.ForceOffsetFromGroundA(pos);

                                        Tank replacementTank = RawTechLoader.SpawnMobileTech(pos, posF, team, type, AutoTerrain: false);
                                        replacementTank.SetTeam(tv.TeamID, wasPop);

                                        Debug.Log("TACtical_AI:  Tech " + previousTechName + " has been swapped out for land tech " + replacementTank.name + " instead");
                                        tv = ManVisible.inst.GetTrackedVisible(replacementTank.visible.ID);
                                        if (tv == null)
                                            tv = new TrackedVisible(replacementTank.visible.ID, replacementTank.visible, ObjectTypes.Vehicle, inherit);
                                    }
                                    // Else we don't do anything.
                                }
                            }
                            catch
                            {
                            }
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(ManGameMode))]
        [HarmonyPatch("Awake")]//On Game start
        private static class StartupSpecialAISpawner
        {
            private static void Postfix()
            {
                // Setup aircraft if Population Injector is N/A
                if (!KickStart.isPopInjectorPresent)
                    SpecialAISpawner.Initiate();
            }
        }


        // Multi-Player
        [HarmonyPatch(typeof(ManNetwork))]
        [HarmonyPatch("AddPlayer")]//On Game start
        private static class WarnJoiningPlayersOfScaryAI
        {
            private static void Postfix(ManNetwork __instance)
            {
                // Setup aircraft if Population Injector is N/A
                try
                {
                    if (ManNetwork.IsHost && KickStart.EnableBetterAI)
                        AIECore.TankAIManager.inst.Invoke("WarnPlayers", 2);
                }
                catch{ }
            }
        }

        // Bases
        [HarmonyPatch(typeof(TankBlock))]
        [HarmonyPatch("OnAttach")]//On Game start
        private static class InsureResetEnemyMiners
        {
            private static void Prefix(TankBlock __instance)
            {
                try
                {
                    if ((bool)__instance.GetComponent<ReverseCache>())
                    {
                        __instance.GetComponent<ReverseCache>().LoadNow();
                        //Debug.Log("TACtical_AI: Destroyed " + __instance.name);
                    }
                    /*
                    else if (__instance.GetComponent<ModuleItemProducer>() && __instance.tank.IsEnemy())
                    {
                        __instance.gameObject.GetOrAddComponent<ReverseCache>().SaveComponents();
                    }*/
                }
                catch { }
            }
        }
    }
}
