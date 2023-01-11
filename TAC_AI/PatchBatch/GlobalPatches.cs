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
    internal class GlobalPatches
    {

        internal static class ModePatches
        {
            internal static Type target = typeof(Mode);

            //Startup - On very late update
            private static void EnterPreMode_Prefix()
            {
                if (!KickStart.firedAfterBlockInjector)//KickStart.isBlockInjectorPresent && 
                    KickStart.DelayedBaseLoader();
            }
        }
        internal static class ModeMainPatches
        {
            internal static Type target = typeof(ModeMain);

            //OverridePlayerTechOnWaterLanding
            private static void PlayerRespawned_Postfix()
            {
                DebugTAC_AI.Log("TACtical_AI: Player respawned");
                if (!KickStart.isPopInjectorPresent && KickStart.isWaterModPresent)
                {
                    DebugTAC_AI.Log("TACtical_AI: Precheck validated");
                    if (AI.Movement.AIEPathing.AboveTheSea(Singleton.playerTank.boundsCentreWorld))
                    {
                        DebugTAC_AI.Log("TACtical_AI: Attempting retrofit");
                        PlayerSpawnAid.TryBotePlayerSpawn();
                    }
                }
            }
        }
        internal static class ModeAttractPatches
        {
            internal static Type target = typeof(ModeAttract);

            // this is a VERY big mod
            //   we must make it look big like it is
            //RestartAttract - Checking title techs
            private static void UpdateModeImpl_Prefix(ModeAttract __instance)
            {
                CustomAttract.CheckShouldRestart(__instance);
            }

            //SetupTerrainCustom -  Setup main menu scene
            private static bool SetupTerrain_Prefix(ModeAttract __instance)
            {
                return CustomAttract.SetupTerrain(__instance);
            }

            //ThrowCoolAIInAttract - Setup main menu techs
            private static bool SetupTechs_Prefix(ModeAttract __instance)
            {
                return CustomAttract.SetupTechsStart(__instance);
            }
            private static void SetupTechs_Postfix(ModeAttract __instance)
            {
                CustomAttract.SetupTechsEnd(__instance);
            }

        }


        internal static class NetTechPatches
        {
            internal static Type target = typeof(NetTech);

            //DontSaveWhenNotNeeded
            private static bool SaveTechData_Prefix(NetTech __instance)
            {
                if (AIERepair.BulkAdding)
                {
                    __instance.QueueSaveTechData();
                    return false;
                }
                return true;
            }
        }
        internal static class TankControlPatches
        {
            internal static Type target = typeof(TankControl);
            //SetMTAIAuto
            private static void CopySchemesFrom_Prefix(TankControl __instance, ref TankControl other)
            {
                try
                {
                    other.gameObject.AddComponent<AIESplitHandler>().Setup(other.Tech, __instance.Tech);
                }
                catch
                { }
            }
        }
        internal static class TankBeamPatches
        {
            internal static Type target = typeof(TankBeam);

            static readonly FieldInfo beamPush = typeof(TankBeam).GetField("m_NudgeStrafe", BindingFlags.NonPublic | BindingFlags.Instance);

            //PatchTankBeamToHelpAI - Give the AI some untangle help
            private static void OnUpdate_Postfix(TankBeam __instance)
            {
                //DebugTAC_AI.Log("TACtical_AI: Patched TankBeam Update(TankAIHelper)");
                if (__instance.IsActive && !ManNetwork.IsNetworked && !ManGameMode.inst.IsCurrent<ModeSumo>())
                {
                    var helper = __instance.GetComponent<AIECore.TankAIHelper>();
                    if (helper != null && (!helper.tank.PlayerFocused || (ManPlayerRTS.autopilotPlayer && ManPlayerRTS.PlayerIsInRTS)))
                    {
                        if (helper.AIState != AIAlignment.Static)
                        {
                            Vector2 headingSquare = (helper.lastDestination - helper.tank.boundsCentreWorldNoCheck).ToVector2XZ();
                            if (helper.DriveDest == EDriveDest.ToLastDestination)
                            {
                                beamPush.SetValue(__instance, helper.tank.rootBlockTrans.InverseTransformDirection(headingSquare * helper.DriveVar * Time.deltaTime));
                            }
                            else if (helper.DriveDest == EDriveDest.FromLastDestination)
                            {
                                beamPush.SetValue(__instance, helper.tank.rootBlockTrans.InverseTransformDirection(-headingSquare * helper.DriveVar * Time.deltaTime));
                            }
                        }
                    }
                }
            }
        }
        internal static class TankCameraPatches
        {
            internal static Type target = typeof(TankCamera);

            //MakeCameraIgnoreAutopilotLockOn
            //static readonly FieldInfo targPos = typeof(TargetAimer).GetField("m_TargetPosition", BindingFlags.NonPublic | BindingFlags.Instance);
            private static void TryKeepManualTargetInView_Postfix(ref Tank tankToFollow, ref bool __result)
            {
                if (!KickStart.EnableBetterAI || !tankToFollow)
                    return;
                var AICommand = tankToFollow.GetComponent<AIECore.TankAIHelper>();
                if (AICommand.lastLockedTarget)
                    __result = false;
            }
        }
        internal static class TechWeaponPatches
        {
            internal static Type target = typeof(TechWeapon);

            //static readonly FieldInfo targPos = typeof(TargetAimer).GetField("m_TargetPosition", BindingFlags.NonPublic | BindingFlags.Instance);
            private static void GetManualTarget_Postfix(TechWeapon __instance, ref Visible __result)
            {
                if (!KickStart.EnableBetterAI)
                    return;

                var AICommand = __instance.transform.root.GetComponent<AIECore.TankAIHelper>();
                if (AICommand.IsNotNull())
                {
                    if (__result == null)
                    {
                        if (AICommand.lastLockedTarget)
                            __result = AICommand.lastLockedTarget;
                    }
                    else
                    {
                        if (AICommand.lastLockedTarget)
                            AICommand.lastLockedTarget = null;
                    }
                }
            }
        }
        internal static class TechAIPatches
        {
            internal static Type target = typeof(TechAI);

            static readonly FieldInfo currentTreeActual = typeof(TechAI).GetField("m_CurrentAITreeType", BindingFlags.NonPublic | BindingFlags.Instance);
            //ForceAIToComplyAnchorCorrectly - (Allied AI state changing remotes) On Auto Setting Tech AI
            private static void UpdateAICategory_Postfix(TechAI __instance)
            {
                var tAI = __instance.gameObject.GetComponent<AI.AIECore.TankAIHelper>();
                if (tAI.IsNotNull())
                {
                    if (tAI.JustUnanchored && tAI.AIState == AIAlignment.Player)
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
        internal static class ObjectSpawnerPatches
        {
            internal static Type target = typeof(ObjectSpawner);

            //EmergencyOverrideOnTechLanding - BEFORE enemy spawn
            private static void TrySpawn_Prefix(ref ManSpawn.ObjectSpawnParams objectSpawnParams, ref ManFreeSpace.FreeSpaceParams freeSpaceParams)
            {
                if (objectSpawnParams != null)
                {
                    if (objectSpawnParams is ManSpawn.TechSpawnParams TSP)
                    {
                        if (TSP.m_IsPopulation)
                        {
                            if (!KickStart.isPopInjectorPresent && KickStart.EnableBetterAI && (ManNetwork.IsHost || !ManNetwork.IsNetworked))
                            {
                                RawTechLoader.UseFactionSubTypes = true;
                                TechData newTech;
                                FactionTypesExt FTE = TSP.m_TechToSpawn.GetMainCorpExt();
                                FactionSubTypes FST = KickStart.CorpExtToCorp(FTE);
                                FactionLevel lvl = RawTechLoader.TryGetPlayerLicenceLevel();
                                if (KickStart.AllowSeaEnemiesToSpawn && KickStart.isWaterModPresent && AI.Movement.AIEPathing.AboveTheSea(freeSpaceParams.m_CenterPos) && RawTechExporter.GetBaseTerrain(TSP.m_TechToSpawn, TSP.m_TechToSpawn.CheckIsAnchored()) == BaseTerrain.Land)
                                {
                                    // OVERRIDE TO SHIP
                                    try
                                    {
                                        int grade = 99;
                                        try
                                        {
                                            if (!SpecialAISpawner.CreativeMode)
                                                grade = ManLicenses.inst.GetCurrentLevel(FST);
                                        }
                                        catch { }


                                        if (RawTechLoader.ShouldUseCustomTechs(out List<int> valid, FTE, lvl, BasePurpose.NotStationary, BaseTerrain.Sea, maxGrade: grade))
                                        {
                                            int randSelect = valid.GetRandomEntry();
                                            newTech = RawTechLoader.GetUnloadedTech(TempManager.ExternalEnemyTechsAll[randSelect], TSP.m_Team, out _);

                                            if (newTech == null)
                                            {
                                                DebugTAC_AI.Exception("Water Tech spawning override failed as fetched TechData is null.  Please report this.");
                                                return;
                                            }
                                            if (newTech.m_BlockSpecs == null)
                                            {
                                                DebugTAC_AI.Exception("Water Tech spawning override failed as fetched TechData's block info is null.  Please report this.");
                                                return;
                                            }
                                            if (newTech.m_BlockSpecs.Count == 0)
                                            {
                                                DebugTAC_AI.Exception("Water Tech spawning override failed as no blocks are present on modified spawning Tech.  Please report this.");
                                                return;
                                            }
                                            DebugTAC_AI.Log("TACtical_AI:  Tech " + TSP.m_TechToSpawn.Name + " landed in water and was likely not water-capable, naval Tech " + newTech.Name + " was substituted for the spawn instead");
                                            TSP.m_TechToSpawn = newTech;
                                        }
                                        else
                                        {
                                            SpawnBaseTypes type = RawTechLoader.GetEnemyBaseType(FTE, lvl, BasePurpose.NotStationary, BaseTerrain.Sea, maxGrade: grade);
                                            if (type != SpawnBaseTypes.NotAvail && !RawTechLoader.IsFallback(type))
                                            {
                                                newTech = RawTechLoader.GetUnloadedTech(type, TSP.m_Team, out _);
                                                if (newTech == null)
                                                {
                                                    DebugTAC_AI.Exception("Water Tech spawning override(PREFAB) failed as fetched TechData is null.  Please report this.");
                                                    return;
                                                }
                                                if (newTech.m_BlockSpecs == null)
                                                {
                                                    DebugTAC_AI.Exception("Water Tech spawning override(PREFAB) failed as fetched TechData's block info is null.  Please report this.");
                                                    return;
                                                }
                                                if (newTech.m_BlockSpecs.Count == 0)
                                                {
                                                    DebugTAC_AI.Exception("Water Tech spawning override(PREFAB) failed as no blocks are present on modified spawning Tech.  Please report this.");
                                                    return;
                                                }
                                                DebugTAC_AI.Log("TACtical_AI:  Tech " + TSP.m_TechToSpawn.Name + " landed in water and was likely not water-capable, naval Tech " + newTech.Name + " was substituted for the spawn instead");

                                                TSP.m_TechToSpawn = newTech;
                                            }
                                            // Else we don't do anything.
                                        }
                                    }
                                    catch
                                    {
                                        DebugTAC_AI.Assert(true, "TACtical_AI:  Attempt to swap sea tech failed!");
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
                                                grade = ManLicenses.inst.GetCurrentLevel(FST);
                                        }
                                        catch { }
                                        if (RawTechLoader.ShouldUseCustomTechs(out List<int> valid, FTE, lvl, BasePurpose.NotStationary, BaseTerrain.Land, maxGrade: grade, maxPrice: KickStart.EnemySpawnPriceMatching))
                                        {
                                            int randSelect = valid.GetRandomEntry();
                                            newTech = RawTechLoader.GetUnloadedTech(TempManager.ExternalEnemyTechsAll[randSelect], TSP.m_Team, out _);

                                            if (newTech == null)
                                            {
                                                DebugTAC_AI.Exception("Land Tech spawning override failed as fetched TechData is null.  Please report this.");
                                                return;
                                            }
                                            if (newTech.m_BlockSpecs == null)
                                            {
                                                DebugTAC_AI.Exception("Land Tech spawning override failed as fetched TechData's block info is null.  Please report this.");
                                                return;
                                            }
                                            if (newTech.m_BlockSpecs.Count == 0)
                                            {
                                                DebugTAC_AI.Exception("Land Tech spawning override failed as no blocks are present on modified spawning Tech.  Please report this.");
                                                return;
                                            }
                                            DebugTAC_AI.Log("TACtical_AI:  Tech " + TSP.m_TechToSpawn.Name + " has been swapped out for land tech " + newTech.Name + " instead");
                                            TSP.m_TechToSpawn = newTech;
                                        }
                                        else
                                        {
                                            SpawnBaseTypes type = RawTechLoader.GetEnemyBaseType(FTE, lvl, BasePurpose.NotStationary, BaseTerrain.Land, maxGrade: grade, maxPrice: KickStart.EnemySpawnPriceMatching);
                                            if (type != SpawnBaseTypes.NotAvail && !RawTechLoader.IsFallback(type))
                                            {
                                                newTech = RawTechLoader.GetUnloadedTech(type, TSP.m_Team, out _);
                                                if (newTech == null)
                                                {
                                                    DebugTAC_AI.Exception("Land Tech spawning override(PREFAB) failed as fetched TechData is null.  Please report this.");
                                                    return;
                                                }
                                                if (newTech.m_BlockSpecs == null)
                                                {
                                                    DebugTAC_AI.Exception("Land Tech spawning override(PREFAB) failed as fetched TechData's block info is null.  Please report this.");
                                                    return;
                                                }
                                                if (newTech.m_BlockSpecs.Count == 0)
                                                {
                                                    DebugTAC_AI.Exception("Land Tech spawning override(PREFAB) failed as no blocks are present on modified spawning Tech.  Please report this.");
                                                    return;
                                                }

                                                DebugTAC_AI.Log("TACtical_AI:  Tech " + TSP.m_TechToSpawn.Name + " has been swapped out for land tech " + newTech.Name + " instead");
                                                TSP.m_TechToSpawn = newTech;
                                            }
                                            // Else we don't do anything.
                                        }
                                    }
                                    catch
                                    {
                                        DebugTAC_AI.Assert(true, "TACtical_AI: Attempt to swap Land tech failed!");
                                    }
                                }

                                RawTechLoader.UseFactionSubTypes = false;
                            }
                        }
                    }
                }
            }
        }
    }
}

