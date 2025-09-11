using System;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using TerraTechETCUtil;
using TAC_AI.AI;
using TAC_AI.Templates;
using TAC_AI.World;
using HarmonyLib;
using Snapshots;

namespace TAC_AI
{
    internal class GlobalPatches
    {
        // GAME
#if DEBUG
        internal static class SnapshotServiceDesktopPatches
        {
            internal static Type target = typeof(SnapshotServiceDesktop);

            //Redirect to our tech population
            private static void GetFilePath_Prefix(ref string relativePath)
            {
                if (relativePath == "Snapshots")
                    relativePath = "SnapshotsCommunity";
            }
        }
#endif
        /*
        internal static class ManSpawnPatches
        {
            internal static Type target = typeof(ManSpawn);

            static readonly FieldInfo teamC = typeof(ManSpawn).GetField("m_TeamCounter", BindingFlags.NonPublic | BindingFlags.Instance);
            //Startup - On very late update
            private static void GenerateAutomaticTeamID_Prefix(ref ManSpawn __instance, ref int __result)
            {
                if (__result == -1)
                {
                    if (AIGlobals.IsBaseTeam((int)teamC.GetValue(__instance)))
                    {
                        __result = AIGlobals.BaseTeamsEnd + 1;
                        teamC.SetValue(__instance, AIGlobals.BaseTeamsEnd + 2);
                    }
                }
            }
        }*/

        internal static class ManPlayerPatches
        {
            internal static Type target = typeof(ManPlayer);

            /// <summary> CatchPlayerInitCheats </summary>
            internal static void SetPlayerHasEnabledCheatCommands_Prefix(ManPlayer __instance)
            {
                DebugRawTechSpawner.CanOpenDebugSpawnMenu = true;
            }
        }

        internal static class SpawnTechDataPatches
        {
            internal static Type target = typeof(SpawnTechData);

            //Startup - On very late update
            private static void SpawnTechInEncounter_Postfix(SpawnTechData __instance,  
                ref Encounter encounterToSpawnInto, ref string nameOverride)
            {
                string text = !nameOverride.NullOrEmpty() ? nameOverride : __instance.UniqueName;
                if (encounterToSpawnInto.GetVisibleState(text, out var vis) == Encounter.EncounterVisibleState.AliveAndSpawned)
                    TankAIManager.RegisterMissionTechVisID(vis.ID);
            }
        }
        /*
        internal static class ModePatches
        {
            internal static Type target = typeof(Mode);

            //Startup - On very late update
            private static void EnterPreMode_Prefix()
            {
                if (!KickStart.firedAfterBlockInjector)//KickStart.isBlockInjectorPresent && 
                    KickStart.DelayedBaseLoader();
            }
        }*/
        internal static class ModeMainPatches
        {
            internal static Type target = typeof(ModeMain);

            //OverridePlayerTechOnWaterLanding
            private static void PlayerRespawned_Postfix()
            {
                DebugTAC_AI.Log(KickStart.ModID + ": Player respawned");
                if (!KickStart.isPopInjectorPresent && KickStart.isWaterModPresent)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Precheck validated");
                    if (AI.Movement.AIEPathing.AboveTheSeaForcedAccurate(Singleton.playerTank.boundsCentreWorld))
                    {
                        DebugTAC_AI.Log(KickStart.ModID + ": Attempting retrofit");
                        PlayerSpawnAid.TryBotePlayerSpawn();
                    }
                }
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
            /*
            private static void ApplyCollectedMovementInputs_Prefix(TankControl __instance, ref TankControl other)
            {
                try
                {
                }
                catch
                { }
            }*/
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
            static readonly FieldInfo beamRot = typeof(TankBeam).GetField("m_NudgeRotate", BindingFlags.NonPublic | BindingFlags.Instance);

            //PatchTankBeamToHelpAI - Give the AI some untangle help
            private static void OnUpdate_Postfix(TankBeam __instance)
            {
                //DebugTAC_AI.Log(KickStart.ModID + ": Patched TankBeam Update(TankAIHelper)");
                if (__instance.IsActive && !ManNetwork.IsNetworked && !ManGameMode.inst.IsCurrent<ModeSumo>())
                {
                    var helper = __instance.GetComponent<TankAIHelper>();
                    if (helper != null && (!helper.tank.PlayerFocused || KickStart.AutopilotPlayer))
                    {
                        if (helper.AIAlign != AIAlignment.Static && (helper.AIAlign != AIAlignment.Player || helper.ActuallyWorks))
                        {
                            bool ReversedMove;
                            switch (helper.DriveDestDirected)
                            {
                                case EDriveDest.FromLastDestination:
                                    ReversedMove = !helper.IsTryingToUnjam;
                                    break;
                                case EDriveDest.ToLastDestination:
                                case EDriveDest.ToMine:
                                default:
                                    ReversedMove = helper.IsTryingToUnjam;
                                    break;
                            }
                            Vector2 headingVec = (helper.lastDestinationCore - helper.tank.boundsCentreWorldNoCheck).ToVector2XZ();
                            if (headingVec.sqrMagnitude > 1)
                                headingVec = headingVec.normalized;

                            float turnControl;
                            if (helper.IsTryingToUnjam)
                            {
                                turnControl = Mathf.Sign(Vector2.Dot(helper.tank.rootBlockTrans.right.ToVector2XZ().normalized, headingVec.normalized)) * Vector2.Dot(helper.tank.rootBlockTrans.forward.ToVector2XZ().normalized, headingVec.normalized);
                            }
                            else
                            {
                                switch (helper.DriveDirDirected)
                                {
                                    case EDriveFacing.Perpendicular:
                                        turnControl = Mathf.Sign(Vector2.Dot(helper.tank.rootBlockTrans.right.ToVector2XZ().normalized, headingVec.normalized)) * Vector2.Dot(helper.tank.rootBlockTrans.forward.ToVector2XZ().normalized, headingVec.normalized);
                                        break;
                                    case EDriveFacing.Backwards:
                                        turnControl = -Vector2.Dot(helper.tank.rootBlockTrans.right.ToVector2XZ().normalized, headingVec.normalized);
                                        ReversedMove = !ReversedMove;
                                        break;
                                    case EDriveFacing.Forwards:
                                    default:
                                        turnControl = Vector2.Dot(helper.tank.rootBlockTrans.right.ToVector2XZ().normalized, headingVec.normalized);
                                        break;
                                }
                            }
                            beamRot.SetValue(__instance, -turnControl);
                            float forceVal = 0;
                            if (helper.DriveVar != 0)
                            {
                                forceVal = helper.DriveVar;
                                beamPush.SetValue(__instance,
                                    helper.tank.rootBlockTrans.InverseTransformDirection((new Vector2(1, 0) * forceVal).
                                    Clamp(-Vector2.one, Vector2.one)));
                                return;
                            }
                            else if (helper.IsDirectedMoving)
                                forceVal = 1.41f;
                            if (ReversedMove)
                                beamPush.SetValue(__instance,
                                    helper.tank.rootBlockTrans.InverseTransformDirection((-headingVec * forceVal).
                                    Clamp(-Vector2.one, Vector2.one)));
                            else
                                beamPush.SetValue(__instance,
                                    helper.tank.rootBlockTrans.InverseTransformDirection((headingVec * forceVal).
                                    Clamp(-Vector2.one, Vector2.one)));
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
                var AICommand = tankToFollow.GetHelperInsured();
                if (AICommand.lastLockOnTarget)
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

                var AICommand = __instance.transform.root.GetComponent<TankAIHelper>();
                if (AICommand.IsNotNull())
                {
                    if (__result == null)
                    {
                        if (AICommand.lastLockOnTarget)
                            __result = AICommand.lastLockOnTarget;
                    }
                    else
                    {
                        if (AICommand.lastLockOnTarget)
                            AICommand.lastLockOnTarget = null;
                    }
                }
            }
        }
        internal static class TechAIPatches
        {
            internal static Type target = typeof(TechAI);

            static readonly FieldInfo currentTreeActual = typeof(TechAI).GetField("m_CurrentAITreeType", BindingFlags.NonPublic | BindingFlags.Instance);

            private static bool ControlTech_Prefix(TechAI __instance)
            {
                var tAI = __instance.gameObject.GetComponent<AI.TankAIHelper>();
                if (tAI.IsNotNull())
                    return tAI.RunState == AIRunState.Default;
                return true;
            }
            
            //ForceAIToComplyAnchorCorrectly - (Allied AI state changing remotes) On Auto Setting Tech AI
            private static void UpdateAICategory_Postfix(TechAI __instance)
            {
                var tAI = __instance.gameObject.GetComponent<AI.TankAIHelper>();
                if (tAI.IsNotNull())
                {
                    if (tAI.AnchorStateAIInsure && tAI.AIAlign == AIAlignment.Player)
                    {   //Set the AI back to escort to continue operations if autoanchor is true
                        tAI.AnchorStateAIInsure = false;
                        __instance.SetBehaviorType(AITreeType.AITypes.Escort);
                        /*
                        if (!__instance.TryGetCurrentAIType(out AITreeType.AITypes type))
                        {
                            if (type != AITreeType.AITypes.Escort)
                            {
                                __instance.SetBehaviorType(AITreeType.AITypes.Escort);
                                AITreeType AISetting = (AITreeType)currentTreeActual.GetValue(__instance);

                                AISetting.m_TypeName = AITreeType.AITypes.Escort.ToString();

                                currentTreeActual.SetValue(__instance, AISetting);
                            }
                        }
                        else
                        {
                            AITreeType AISetting = (AITreeType)currentTreeActual.GetValue(__instance);

                            AISetting.m_TypeName = AITreeType.AITypes.Escort.ToString();

                            currentTreeActual.SetValue(__instance, AISetting);
                        }
                        */
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
                        try
                        {
                            DebugTAC_AI.Log("Spawning ");
                            SpecialAISpawner.OverrideSpawning(TSP, freeSpaceParams.m_CenterPos);
                            objectSpawnParams = TSP;
                        }
                        catch (Exception e)
                        {
                            DebugTAC_AI.FatalError("A serious crash occurred whilist Advanced AI was spawning a Tech!");
                            throw e;
                        }
                    }
                }
            }
        }
    }
}

