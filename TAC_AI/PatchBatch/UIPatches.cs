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
    internal class UIPatches
    {
        internal static class CursorPatches
        {
            internal static Type target = typeof(GameCursor);
            /*
            // NEW
            AIOrderAttack
            AIOrderEmpty
            AIOrderMove
            AIOrderSelect
            */

            /// <summary>
            /// See CursorChanger for more information
            /// </summary>
            /// <param name="__result"></param>
            private static void GetCursorState_Postfix(ref GameCursor.CursorState __result)
            {
                if (!CursorChanger.AddedNewCursors)
                    return;
                if (ManPlayerRTS.PlayerIsInRTS)
                {
                    switch (ManPlayerRTS.cursorState)
                    {
                        case RTSCursorState.Empty:
                            //__result = (GameCursor.CursorState)CursorChanger.CursorIndexCache[1];
                            break;
                        case RTSCursorState.Moving:
                            __result = (GameCursor.CursorState)CursorChanger.CursorIndexCache[2];
                            break;
                        case RTSCursorState.Attack:
                            __result = (GameCursor.CursorState)CursorChanger.CursorIndexCache[0];
                            break;
                        case RTSCursorState.Select:
                            __result = (GameCursor.CursorState)CursorChanger.CursorIndexCache[3];
                            break;
                        case RTSCursorState.Fetch:
                            __result = (GameCursor.CursorState)CursorChanger.CursorIndexCache[4];
                            break;
                        case RTSCursorState.Mine:
                            __result = (GameCursor.CursorState)CursorChanger.CursorIndexCache[5];
                            break;
                        case RTSCursorState.Protect:
                            __result = (GameCursor.CursorState)CursorChanger.CursorIndexCache[6];
                            break;
                        case RTSCursorState.Scout:
                            __result = (GameCursor.CursorState)CursorChanger.CursorIndexCache[7];
                            break;
                        default:
                            break;
                    }
                }
                else if (ManPlayerRTS.PlayerRTSOverlay && __result == GameCursor.CursorState.Default)
                {
                    switch (ManPlayerRTS.cursorState)
                    {
                        case RTSCursorState.Empty:
                            //__result = (GameCursor.CursorState)CursorChanger.CursorIndexCache[1];
                            break;
                        case RTSCursorState.Moving:
                            __result = (GameCursor.CursorState)CursorChanger.CursorIndexCache[2];
                            break;
                        case RTSCursorState.Attack:
                            __result = (GameCursor.CursorState)CursorChanger.CursorIndexCache[0];
                            break;
                        case RTSCursorState.Select:
                            __result = (GameCursor.CursorState)CursorChanger.CursorIndexCache[3];
                            break;
                        case RTSCursorState.Fetch:
                            __result = (GameCursor.CursorState)CursorChanger.CursorIndexCache[4];
                            break;
                        case RTSCursorState.Mine:
                            __result = (GameCursor.CursorState)CursorChanger.CursorIndexCache[5];
                            break;
                        case RTSCursorState.Protect:
                            __result = (GameCursor.CursorState)CursorChanger.CursorIndexCache[6];
                            break;
                        case RTSCursorState.Scout:
                            __result = (GameCursor.CursorState)CursorChanger.CursorIndexCache[7];
                            break;
                        default:
                            break;
                    }
                }
            }

        }

        internal static class TankDescriptionOverlayPatches
        {
            internal static Type target = typeof(TankDescriptionOverlay);

            static readonly FieldInfo tech = typeof(TankDescriptionOverlay).GetField("m_Tank", BindingFlags.NonPublic | BindingFlags.Instance),
                panel = typeof(TankDescriptionOverlay).GetField("m_PanelInst", BindingFlags.NonPublic | BindingFlags.Instance),
                back = typeof(LocatorPanel).GetField("m_Pin", BindingFlags.NonPublic | BindingFlags.Instance),
                icon = typeof(LocatorPanel).GetField("m_FactionIcon", BindingFlags.NonPublic | BindingFlags.Instance),
                lowerName = typeof(LocatorPanel).GetField("m_BottomName", BindingFlags.NonPublic | BindingFlags.Instance);
            //SendUpdateAIDisp - Change the Icon to something more appropreate
            private static void RefreshMarker_Postfix(TankDescriptionOverlay __instance)
            {
                if (KickStart.EnableBetterAI)
                {
                    try
                    {
                        Tank tank = (Tank)tech.GetValue(__instance);
                        //RawTechExporter.lastTech = tank.GetComponent<AIECore.TankAIHelper>();

                        LocatorPanel Panel = (LocatorPanel)panel.GetValue(__instance);
                        if (KickStart.EnableBetterAI && tank.IsNotNull() && Panel.IsNotNull())
                        {
                            AIECore.TankAIHelper lastTech = tank.GetComponent<AIECore.TankAIHelper>();
                            if (lastTech.IsNotNull())
                            {
                                Image cache = (Image)icon.GetValue(Panel);
                                Image cacheB = (Image)back.GetValue(Panel);

                                int Team = lastTech.tank.Team;

                                Panel.BottomName = TeamNamer.GetTeamName(Team).ToString();
                                if (AIGlobals.IsFriendlyBaseTeam(Team))
                                {
                                    cache.color = AIGlobals.FriendlyColor;
                                    cacheB.color = AIGlobals.FriendlyColor;
                                    back.SetValue(Panel, cacheB);
                                }
                                else if (AIGlobals.IsNeutralBaseTeam(Team))
                                {
                                    cache.color = AIGlobals.NeutralColor;
                                    cacheB.color = AIGlobals.NeutralColor;
                                    back.SetValue(Panel, cacheB);
                                }
                                else if (AIGlobals.IsSubNeutralBaseTeam(Team))
                                {
                                    cache.color = AIGlobals.EnemyColor;
                                    cacheB.color = AIGlobals.EnemyColor;
                                    back.SetValue(Panel, cacheB);
                                }
                                if (tank.IsAnchored)
                                {   // Use anchor icon

                                }
                                else if (lastTech.AIState == AIAlignment.Player)
                                {   // Allied AI
                                    if (lastTech.SetToActive)
                                    {
                                        Sprite sprite;
                                        if (RawTechExporter.aiBackplates.TryGetValue(lastTech.DriverType, out sprite))
                                        {
                                            cacheB.sprite = sprite;
                                        }
                                        if (RawTechExporter.aiIcons.TryGetValue(KickStart.GetLegacy(lastTech.DediAI, lastTech.DriverType), out sprite))
                                        {
                                            //DebugTAC_AI.Log("TACtical_AI: UpdateAIDisplay - Swapping sprite!");
                                            cache.sprite = sprite;
                                        }
                                    }
                                }
                                else if (lastTech.AIState == AIAlignment.NonPlayer)
                                {   // Enemy AI
                                    if (KickStart.enablePainMode)
                                    {
                                        var mind = lastTech.GetComponent<EnemyMind>();
                                        if ((bool)mind)
                                        {
                                            Sprite sprite;
                                            if (RawTechExporter.aiBackplates.TryGetValue(lastTech.DriverType, out sprite))
                                            {
                                                cacheB.sprite = sprite;
                                            }
                                            if (mind.CommanderSmarts < EnemySmarts.Smrt)
                                            {
                                                switch (mind.CommanderMind)
                                                {
                                                    case EnemyAttitude.Homing:
                                                        if (RawTechExporter.aiIcons.TryGetValue(AIType.Assault, out sprite))
                                                        {
                                                            //DebugTAC_AI.Log("TACtical_AI: UpdateAIDisplay - Swapping sprite!");
                                                            cache.sprite = sprite;
                                                        }
                                                        break;
                                                    case EnemyAttitude.Junker:
                                                        if (RawTechExporter.aiIcons.TryGetValue(AIType.Scrapper, out sprite))
                                                        {
                                                            //DebugTAC_AI.Log("TACtical_AI: UpdateAIDisplay - Swapping sprite!");
                                                            cache.sprite = sprite;
                                                            icon.SetValue(Panel, cache);
                                                        }
                                                        break;
                                                    case EnemyAttitude.Miner:
                                                        if (RawTechExporter.aiIcons.TryGetValue(AIType.Prospector, out sprite))
                                                        {
                                                            //DebugTAC_AI.Log("TACtical_AI: UpdateAIDisplay - Swapping sprite!");
                                                            cache.sprite = sprite;
                                                        }
                                                        break;
                                                    case EnemyAttitude.NPCBaseHost:
                                                    case EnemyAttitude.Boss:
                                                        if (RawTechExporter.aiIcons.TryGetValue(AIType.Aegis, out sprite))
                                                        {
                                                            //DebugTAC_AI.Log("TACtical_AI: UpdateAIDisplay - Swapping sprite!");
                                                            cache.sprite = sprite;
                                                        }
                                                        break;
                                                    default:
                                                        switch (mind.EvilCommander)
                                                        {
                                                            case EnemyHandling.Airplane:
                                                            case EnemyHandling.Chopper:
                                                                if (RawTechExporter.aiIcons.TryGetValue(AIType.Aviator, out sprite))
                                                                {
                                                                    //DebugTAC_AI.Log("TACtical_AI: UpdateAIDisplay - Swapping sprite!");
                                                                    cache.sprite = sprite;
                                                                }
                                                                break;
                                                            case EnemyHandling.Naval:
                                                                if (RawTechExporter.aiIcons.TryGetValue(AIType.Buccaneer, out sprite))
                                                                {
                                                                    //DebugTAC_AI.Log("TACtical_AI: UpdateAIDisplay - Swapping sprite!");
                                                                    cache.sprite = sprite;
                                                                }
                                                                break;
                                                            case EnemyHandling.Starship:
                                                                if (RawTechExporter.aiIcons.TryGetValue(AIType.Astrotech, out sprite))
                                                                {
                                                                    //DebugTAC_AI.Log("TACtical_AI: UpdateAIDisplay - Swapping sprite!");
                                                                    cache.sprite = sprite;
                                                                }
                                                                break;
                                                            default:
                                                                if (RawTechExporter.aiIconsEnemy.TryGetValue(mind.CommanderSmarts, out sprite))
                                                                {
                                                                    //DebugTAC_AI.Log("TACtical_AI: UpdateAIDisplay - Swapping sprite!");
                                                                    cache.sprite = sprite;
                                                                }
                                                                break;
                                                        }
                                                        break;
                                                }
                                            }
                                            else
                                            {
                                                switch (mind.CommanderMind)
                                                {
                                                    case EnemyAttitude.NPCBaseHost:
                                                    case EnemyAttitude.Boss:
                                                        if (RawTechExporter.aiIcons.TryGetValue(AIType.Aegis, out sprite))
                                                        {
                                                            //DebugTAC_AI.Log("TACtical_AI: UpdateAIDisplay - Swapping sprite!");
                                                            cache.sprite = sprite;
                                                        }
                                                        break;
                                                    default:
                                                        if (RawTechExporter.aiIconsEnemy.TryGetValue(mind.CommanderSmarts, out sprite))
                                                        {
                                                            //DebugTAC_AI.Log("TACtical_AI: UpdateAIDisplay - Swapping sprite!");
                                                            cache.sprite = sprite;
                                                        }
                                                        break;
                                                }
                                            }
                                        }
                                    }
                                }

                                icon.SetValue(Panel, cache);
                            }
                        }
                        //Panel.Format(Singleton.Manager<ManUI>.inst.GetAICategoryIcon(AICategories.AIHostile), new Color(0.8f, 0.8f, 0.8f, 0.8f), Singleton.Manager<ManUI>.inst.GetAICategoryIcon(AICategories.AIHostile), new Color(0.8f, 0.8f, 0.8f, 0.8f), TechWeapon.ManualTargetingReticuleState.NotTargeted);
                        //DebugTAC_AI.Log("TACtical_AI: SendUpdateAIDisp - sent!");
                        //return false;
                    }
                    catch
                    {
                        DebugTAC_AI.Log("TACtical_AI: SendUpdateAIDisp - Player not close enough");
                    }
                }
            }

        }
        internal static class UIMiniMapLayerTechPatches
        {
            internal static Type target = typeof(UIMiniMapLayerTech);

            // AddInNewUIElements
            private static void TryGetIconForTrackedVisible_Postfix(ref TrackedVisible trackedVisible, ref Color iconColour)
            {
                switch (trackedVisible.RadarType)
                {
                    case RadarTypes.Base:
                    case RadarTypes.Vehicle:
                        if (AIGlobals.IsFriendlyBaseTeam(trackedVisible.RadarTeamID))
                        {
                            iconColour = AIGlobals.FriendlyColor;
                        }
                        else if (AIGlobals.IsNeutralBaseTeam(trackedVisible.RadarTeamID))
                        {
                            iconColour = AIGlobals.NeutralColor;
                        }
                        break;
                }
            }
        }

        internal static class UIRadialTechControlMenuPatches
        {
            internal static Type target = typeof(UIRadialTechControlMenu);
            //DetectAIRadialAction
            private static void Show_Prefix(ref object context)
            {
                OpenMenuEventData nabData = (OpenMenuEventData)context;
                TankBlock thisBlock = nabData.m_TargetTankBlock;
                if (thisBlock.tank.IsNotNull())
                {
                    DebugTAC_AI.Info("TACtical_AI: grabbed tank data = " + thisBlock.tank.name.ToString());
                    GUIAIManager.GetTank(thisBlock.tank);
                }
                else
                {
                    DebugTAC_AI.Log("TACtical_AI: TANK IS NULL!");
                }
            }
            //DetectAIRadialMenuAction
            private static void OnAIOptionSelected_Prefix(UIRadialTechControlMenu __instance, ref UIRadialTechControlMenu.PlayerCommands command)
            {
                //DebugTAC_AI.Log("TACtical_AI: click menu FIRED!!!  input = " + command.ToString() + " | num = " + (int)command);
                if ((int)command == 3)
                {
                    if (GUIAIManager.IsTankNull())
                    {
                        FieldInfo currentTreeActual = typeof(UIRadialTechControlMenu).GetField("m_TargetTank", BindingFlags.NonPublic | BindingFlags.Instance);
                        Tank tonk = (Tank)currentTreeActual.GetValue(__instance);
                        GUIAIManager.GetTank(tonk);
                        if (GUIAIManager.IsTankNull())
                        {
                            DebugTAC_AI.Log("TACtical_AI: TANK IS NULL AFTER SEVERAL ATTEMPTS!!!");
                        }
                    }
                    GUIAIManager.LaunchSubMenuClickable();
                }

                //DebugTAC_AI.Log("TACtical_AI: click menu " + __instance.gameObject.name);
                //DebugTAC_AI.Log("TACtical_AI: click menu host gameobject " + Nuterra.NativeOptions.UIUtilities.GetComponentTree(__instance.gameObject, __instance.gameObject.name));
            }
        }

        internal static class UIScreenPauseMenuPatches
        {
            internal static Type target = typeof(UIScreenPauseMenu);

            static readonly FieldInfo rtsCam = typeof(UIScreenPauseMenu).GetField("m_FreeCam", BindingFlags.NonPublic | BindingFlags.Instance);
            //AllowRTSInCampaign
            private static void Show_Postfix(UIScreenPauseMenu __instance)
            {
                Toggle cam = (Toggle)rtsCam.GetValue(__instance);
                if (ManGameMode.inst.IsCurrent<ModeMain>() || ManGameMode.inst.IsCurrent<ModeCoOpCampaign>())
                {
                    cam.gameObject.SetActive(true);
                    cam.SetValue(ManPauseGame.inst.PhotoCamToggle);
                }
            }
        }
    }
}
