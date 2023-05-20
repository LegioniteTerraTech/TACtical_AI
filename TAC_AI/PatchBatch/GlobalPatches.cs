﻿using System;
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
        // GAME
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
        }

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
            static readonly FieldInfo beamRot = typeof(TankBeam).GetField("m_NudgeRotate", BindingFlags.NonPublic | BindingFlags.Instance);

            //PatchTankBeamToHelpAI - Give the AI some untangle help
            private static void OnUpdate_Postfix(TankBeam __instance)
            {
                //DebugTAC_AI.Log("TACtical_AI: Patched TankBeam Update(TankAIHelper)");
                if (__instance.IsActive && !ManNetwork.IsNetworked && !ManGameMode.inst.IsCurrent<ModeSumo>())
                {
                    var helper = __instance.GetComponent<AIECore.TankAIHelper>();
                    if (helper != null && (!helper.tank.PlayerFocused || (ManPlayerRTS.autopilotPlayer && ManPlayerRTS.PlayerIsInRTS)))
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
                            else if (helper.IsDirectedMovingAnyDest)
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
                    if (tAI.JustUnanchored && tAI.AIAlign == AIAlignment.Player)
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
                        SpecialAISpawner.OverrideSpawning(TSP, freeSpaceParams.m_CenterPos);
                    }
                }
            }
        }
    }
}

