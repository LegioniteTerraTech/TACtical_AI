﻿using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
//using Harmony;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TAC_AI.AI;
using TAC_AI.AI.Enemy;
using TAC_AI.Templates;
using TAC_AI.World;
using SafeSaves;
using UnityEditor;

namespace TAC_AI
{
    class PatchBatch
    {
    }

#if STEAM
    public class KickStartTAC_AI : ModBase
    {
        
        internal static KickStartTAC_AI oInst;

        bool isInit = false;
        public override bool HasEarlyInit()
        {
            return true;
        }

        // IDK what I should init here...
        public override void EarlyInit()
        {
            if (oInst == null)
            {
                oInst = this;
                try
                {
                    //ManSafeSaves.Init();
                }
                catch (Exception e) { Debug.LogError(e.ToString()); }
                if (!KickStart.hasPatched)
                {
                    try
                    {
                        KickStart.harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
                        KickStart.hasPatched = true;
                    }
                    catch (Exception e)
                    {
                        Debug.Log("TACtical_AI: Error on patch");
                        Debug.Log(e);
                    }
                }
            }
        }
        public override void Init()
        {
            // We do this check because this mod takes FOREVER to build, so we don't heed every reset
            //   request - the mod is already built to handle that because of Unofficial.
            KickStart.ShouldBeActive = true;
            if (!isInit)
            {
                if (oInst == null)
                    oInst = this;
                KickStart.GetActiveMods();
                KickStart.MainOfficialInit();
                try
                {
                    //ManSafeSaves.Init();
                }
                catch (Exception e) { Debug.LogError(e.ToString()); }
                isInit = true;
            }
            else
            {
                SpecialAISpawner.DetermineActiveOnModeType(ManGameMode.inst.GetCurrentGameType());
                AIECore.TankAIManager.inst.CorrectBlocksList();
            }
        }
        public override void DeInit()
        {
            KickStart.ShouldBeActive = false;
            if (isInit)
            {
                KickStart.DeInitCheck();
                isInit = false;
            }
        }

        public override void Update()
        {
        }
    }
#endif

    internal enum AttractType
    {
        Harvester,
        Invader,
        SpaceInvader,
        Dogfight,
        SpaceBattle,
        NavalWarfare,
        HQSiege,
        BaseVBase,
        Misc,
    }

    internal static class Patches
    {

        //static readonly FieldInfo panelData = typeof(FloatingTextOverlay).GetField("m_Data", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo textInput = typeof(FloatingTextPanel).GetField("m_AmountText", BindingFlags.NonPublic | BindingFlags.Instance);

        static readonly FieldInfo listOverlays = typeof(ManOverlay).GetField("m_ActiveOverlays", BindingFlags.NonPublic | BindingFlags.Instance);

        static readonly FieldInfo rects = typeof(FloatingTextPanel).GetField("m_Rect", BindingFlags.NonPublic | BindingFlags.Instance);

        static readonly FieldInfo sScale = typeof(FloatingTextPanel).GetField("m_InitialScale", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo scale = typeof(FloatingTextPanel).GetField("m_scaler", BindingFlags.NonPublic | BindingFlags.Instance);

        static readonly FieldInfo canvas = typeof(FloatingTextPanel).GetField("m_CanvasGroup", BindingFlags.NonPublic | BindingFlags.Instance);

        static readonly FieldInfo CaseThis = typeof(ManOverlay).GetField("m_ConsumptionAddMoneyOverlayData", BindingFlags.NonPublic | BindingFlags.Instance);


        private static bool savedOverlay = false;
        private static FloatingTextOverlayData overlayEdit;
        private static GameObject textStor;
        private static CanvasGroup canGroup;
        internal static void PopupEnemyInfo(string text, WorldPosition pos)
        {
            // Big mess trying to get some hard-locked code working

            if (!savedOverlay)
            {
                textStor = new GameObject("NewTextEnemy", typeof(RectTransform));

                RectTransform rTrans = textStor.GetComponent<RectTransform>();
                Text texter = rTrans.gameObject.AddComponent<Text>();
                FloatingTextOverlayData refer = (FloatingTextOverlayData)CaseThis.GetValue(ManOverlay.inst);
                Text textRefer = (Text)textInput.GetValue(refer.m_PanelPrefab);



                //texter = (Text)textInput.GetValue(refer.m_PanelPrefab);

                texter.horizontalOverflow = HorizontalWrapMode.Overflow;
                texter.fontStyle = textRefer.fontStyle;
                texter.material = textRefer.material;
                texter.alignment = textRefer.alignment;
                texter.font = textRefer.font;
                texter.color = new Color(0.95f, 0.1f, 0.1f, 0.95f);
                texter.fontSize = (int)((float)texter.fontSize * 2f);
                texter.SetAllDirty();

                FloatingTextPanel panel = textStor.AddComponent<FloatingTextPanel>();

                //panel = refer.m_PanelPrefab;
                //canGroup = (CanvasGroup)canvas.GetValue(refer.m_PanelPrefab);

                try
                {
                    CanvasGroup cG = (CanvasGroup)canvas.GetValue(refer.m_PanelPrefab);
                    canGroup = rTrans.gameObject.AddComponent<CanvasGroup>();
                    canGroup.alpha = 0.95f;
                    canGroup.blocksRaycasts = false;
                    canGroup.hideFlags = 0;
                    canGroup.ignoreParentGroups = true;
                    canGroup.interactable = false;
                }
                catch { }

                canvas.SetValue(panel, canGroup);
                rects.SetValue(panel, rTrans);
                sScale.SetValue(panel, Vector3.one * 2.5f);
                scale.SetValue(panel, 2.5f);

                textInput.SetValue(panel, texter);


                overlayEdit = textStor.AddComponent<FloatingTextOverlayData>();
                overlayEdit.m_HiddenInModes = new List<ManGameMode.GameType>
                {
                    ManGameMode.GameType.Attract,
                    ManGameMode.GameType.Gauntlet,
                    ManGameMode.GameType.SumoShowdown,
                };
                overlayEdit.m_StayTime = refer.m_StayTime;
                overlayEdit.m_FadeOutTime = refer.m_FadeOutTime;
                overlayEdit.m_MaxCameraResizeDist = refer.m_MaxCameraResizeDist;
                overlayEdit.m_HiddenInModes = refer.m_HiddenInModes;
                overlayEdit.m_MinCameraResizeDist = refer.m_MinCameraResizeDist;
                overlayEdit.m_CamResizeCurve = refer.m_CamResizeCurve;
                overlayEdit.m_AboveDist = refer.m_AboveDist;
                overlayEdit.m_PanelPrefab = panel;

                savedOverlay = true;
            }

            FloatingTextOverlay fOverlay = new FloatingTextOverlay(overlayEdit);

            fOverlay.Set(text, pos);

            FloatingTextOverlayData textCase = (FloatingTextOverlayData)CaseThis.GetValue(ManOverlay.inst);
            if (textCase.VisibleInCurrentMode && fOverlay != null)
            {
                List<Overlay> over = (List<Overlay>)listOverlays.GetValue(ManOverlay.inst);
                over.Add(fOverlay);
                listOverlays.SetValue(ManOverlay.inst, over);
                //Debug.Log("TACtical_AI: PopupEnemyInfo - Force inserted popup");
            }
            //Debug.Log("TACtical_AI: PopupEnemyInfo - Threw popup \"" + text + "\"");


            // ManOverlay.inst.AddFloatingTextOverlay(text, pos);
        }

        private static bool savedOverlayA = false;
        private static FloatingTextOverlayData overlayEditA;
        private static GameObject textStorA;
        private static CanvasGroup canGroupA;
        internal static void PopupAllyInfo(string text, WorldPosition pos)
        {
            // Big mess trying to get some hard-locked code working

            if (!savedOverlayA)
            {
                textStorA = new GameObject("NewTextAlly", typeof(RectTransform));

                RectTransform rTrans = textStorA.GetComponent<RectTransform>();
                Text texter = rTrans.gameObject.AddComponent<Text>();
                FloatingTextOverlayData refer = (FloatingTextOverlayData)CaseThis.GetValue(ManOverlay.inst);
                Text textRefer = (Text)textInput.GetValue(refer.m_PanelPrefab);



                //texter = (Text)textInput.GetValue(refer.m_PanelPrefab);

                texter.horizontalOverflow = HorizontalWrapMode.Overflow;
                texter.fontStyle = textRefer.fontStyle;
                texter.material = textRefer.material;
                texter.alignment = textRefer.alignment;
                texter.font = textRefer.font;
                texter.color = new Color(0.2f, 0.95f, 0.2f, 0.95f);
                texter.fontSize = (int)((float)texter.fontSize * 2f);
                texter.SetAllDirty();

                FloatingTextPanel panel = textStorA.AddComponent<FloatingTextPanel>();

                //panel = refer.m_PanelPrefab;
                //canGroup = (CanvasGroup)canvas.GetValue(refer.m_PanelPrefab);

                try
                {
                    CanvasGroup cG = (CanvasGroup)canvas.GetValue(refer.m_PanelPrefab);
                    canGroupA = rTrans.gameObject.AddComponent<CanvasGroup>();
                    canGroupA.alpha = 0.95f;
                    canGroupA.blocksRaycasts = false;
                    canGroupA.hideFlags = 0;
                    canGroupA.ignoreParentGroups = true;
                    canGroupA.interactable = false;
                }
                catch { }

                canvas.SetValue(panel, canGroupA);
                rects.SetValue(panel, rTrans);
                sScale.SetValue(panel, Vector3.one * 2.5f);
                scale.SetValue(panel, 2.5f);

                textInput.SetValue(panel, texter);


                overlayEditA = textStorA.AddComponent<FloatingTextOverlayData>();
                overlayEditA.m_HiddenInModes = new List<ManGameMode.GameType>
                {
                    ManGameMode.GameType.Attract,
                    ManGameMode.GameType.Gauntlet,
                    ManGameMode.GameType.SumoShowdown,
                };
                overlayEditA.m_StayTime = refer.m_StayTime;
                overlayEditA.m_FadeOutTime = refer.m_FadeOutTime;
                overlayEditA.m_MaxCameraResizeDist = refer.m_MaxCameraResizeDist;
                overlayEditA.m_HiddenInModes = refer.m_HiddenInModes;
                overlayEditA.m_MinCameraResizeDist = refer.m_MinCameraResizeDist;
                overlayEditA.m_CamResizeCurve = refer.m_CamResizeCurve;
                overlayEditA.m_AboveDist = refer.m_AboveDist;
                overlayEditA.m_PanelPrefab = panel;

                savedOverlayA = true;
            }

            FloatingTextOverlay fOverlay = new FloatingTextOverlay(overlayEditA);

            fOverlay.Set(text, pos);

            FloatingTextOverlayData textCase = (FloatingTextOverlayData)CaseThis.GetValue(ManOverlay.inst);
            if (textCase.VisibleInCurrentMode && fOverlay != null)
            {
                List<Overlay> over = (List<Overlay>)listOverlays.GetValue(ManOverlay.inst);
                over.Add(fOverlay);
                listOverlays.SetValue(ManOverlay.inst, over);
                //Debug.Log("TACtical_AI: PopupAllyInfo - Force inserted popup");
            }
            //Debug.Log("TACtical_AI: PopupAllyInfo - Threw popup \"" + text + "\"");


            // ManOverlay.inst.AddFloatingTextOverlay(text, pos);
        }

#if DEBUG
        // Leg testing
        [HarmonyPatch(typeof(Tank))]
        [HarmonyPatch("IsEnemy", typeof(int), typeof(int))]//On player base bomb landing
        private static class LetMeObserveAIForTestingPurposes
        {
            private static bool Prefix(ref bool __result, ref int teamID1, ref int teamID2)
            {
                int playerTeam;
                if (Singleton.playerTank)
                    playerTeam = Singleton.playerTank.Team;
                else
                    playerTeam = ManPlayer.inst.PlayerTeam;
                if ((teamID1 == playerTeam && RawTechLoader.IsFriendlyBaseTeam(teamID2)) || (teamID2 == playerTeam && RawTechLoader.IsFriendlyBaseTeam(teamID1)))
                {
                    __result = false;
                    return false;
                }
                if (DebugRawTechSpawner.DevCheatNoAttackPlayer && (teamID1 == playerTeam || teamID2 == playerTeam) && !ManNetwork.IsNetworked)
                {
                    __result = false;
                    return false;
                }
                return true;
            }
        }
        [HarmonyPatch(typeof(Tank))]
        [HarmonyPatch("IsFriendly", typeof(int), typeof(int))]//On player base bomb landing
        private static class LetMeObserveAIForTestingPurposesAllied
        {
            private static bool Prefix(ref bool __result, ref int teamID1, ref int teamID2)
            {
                int playerTeam;
                if (Singleton.playerTank)
                    playerTeam = Singleton.playerTank.Team;
                else
                    playerTeam = ManPlayer.inst.PlayerTeam;
                if (DebugRawTechSpawner.DevCheatPlayerEnemyBaseTeam && (teamID1 == playerTeam || teamID2 == playerTeam) && !ManNetwork.IsNetworked)
                {
                    __result = true;
                    return false;
                }
                return true;
            }
        }
#else
        [HarmonyPatch(typeof(Tank))]
        [HarmonyPatch("IsEnemy", typeof(int), typeof(int))]//On player base bomb landing
        private static class LetMeObserveAIForTestingPurposes
        {
            private static bool Prefix(ref bool __result, ref int teamID1, ref int teamID2)
            {
                int playerTeam;
                if (Singleton.playerTank)
                    playerTeam = Singleton.playerTank.Team;
                else
                    playerTeam = ManPlayer.inst.PlayerTeam;
                if ((teamID1 == playerTeam && RawTechLoader.IsFriendlyBaseTeam(teamID2)) || (teamID2 == playerTeam && RawTechLoader.IsFriendlyBaseTeam(teamID1)))
                {
                    __result = false;
                    return false;
                }
                return true;
            }
        }
#endif



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
                        var tank = __instance.transform.root.GetComponent<Tank>();
                        var tankAIHelp = tank.gameObject.GetComponent<AIECore.TankAIHelper>();
                        if (tankAIHelp)
                        {
                            bool ExertControl = tankAIHelp.ControlTech(__instance.block.tank.control);

                            if (ExertControl)
                            {
                                __result = true;
                                return false;
                            }
                        }
                        // else it's still initiating
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

        [HarmonyPatch(typeof(TankDescriptionOverlay))]
        [HarmonyPatch("RefreshMarker")]//Change the Icon to something more appropreate
        private static class SendUpdateAIDisp
        {
            static readonly FieldInfo tech = typeof(TankDescriptionOverlay).GetField("m_Tank", BindingFlags.NonPublic | BindingFlags.Instance),
                panel = typeof(TankDescriptionOverlay).GetField("m_PanelInst", BindingFlags.NonPublic | BindingFlags.Instance),
                back = typeof(LocatorPanel).GetField("m_Pin", BindingFlags.NonPublic | BindingFlags.Instance),
                icon = typeof(LocatorPanel).GetField("m_FactionIcon", BindingFlags.NonPublic | BindingFlags.Instance),
                lowerName = typeof(LocatorPanel).GetField("m_BottomName", BindingFlags.NonPublic | BindingFlags.Instance);

            private static void Postfix(TankDescriptionOverlay __instance)
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
                                if (RawTechLoader.IsFriendlyBaseTeam(Team))
                                {
                                    cache.color = new Color(0, 1, 0);
                                    cacheB.color = new Color(0, 1, 0);
                                    back.SetValue(Panel, cacheB);
                                }
                                else if (RawTechLoader.IsNeutralBaseTeam(Team))
                                {
                                    cache.color = new Color(0.5f, 0, 0.5f);
                                    cacheB.color = new Color(0.5f, 0, 0.5f);
                                    back.SetValue(Panel, cacheB);
                                }
                                else if (RawTechLoader.IsSubNeutralBaseTeam(Team))
                                {
                                    cache.color = new Color(1, 0.8f, 0);
                                    cacheB.color = new Color(1, 0.8f, 0);
                                    back.SetValue(Panel, cacheB);
                                }
                                if (tank.IsAnchored)
                                {   // Use anchor icon

                                }
                                else if (lastTech.AIState == AIAlignment.Player)
                                {   // Allied AI
                                    if (lastTech.lastAIType == AITreeType.AITypes.Escort)
                                    {
                                        if (RawTechExporter.aiIcons.TryGetValue(KickStart.GetLegacy(lastTech.DediAI, lastTech.DriverType), out Sprite sprite))
                                        {
                                            //Debug.Log("TACtical_AI: UpdateAIDisplay - Swapping sprite!");
                                            cache.sprite = sprite;
                                        }
                                    }
                                }
                                else if (lastTech.AIState == AIAlignment.NonPlayerTech)
                                {   // Enemy AI
                                    if (KickStart.enablePainMode)
                                    {

                                        var mind = lastTech.GetComponent<EnemyMind>();
                                        if ((bool)mind)
                                        {
                                            Sprite sprite;
                                            if (mind.CommanderSmarts < EnemySmarts.Smrt)
                                            {
                                                switch (mind.CommanderMind)
                                                {
                                                    case EnemyAttitude.Homing:
                                                        if (RawTechExporter.aiIcons.TryGetValue(AIType.Assault, out sprite))
                                                        {
                                                            //Debug.Log("TACtical_AI: UpdateAIDisplay - Swapping sprite!");
                                                            cache.sprite = sprite;
                                                        }
                                                        break;
                                                    case EnemyAttitude.Junker:
                                                        if (RawTechExporter.aiIcons.TryGetValue(AIType.Scrapper, out sprite))
                                                        {
                                                            //Debug.Log("TACtical_AI: UpdateAIDisplay - Swapping sprite!");
                                                            cache.sprite = sprite;
                                                            icon.SetValue(Panel, cache);
                                                        }
                                                        break;
                                                    case EnemyAttitude.Miner:
                                                        if (RawTechExporter.aiIcons.TryGetValue(AIType.Prospector, out sprite))
                                                        {
                                                            //Debug.Log("TACtical_AI: UpdateAIDisplay - Swapping sprite!");
                                                            cache.sprite = sprite;
                                                        }
                                                        break;
                                                    case EnemyAttitude.NPCBaseHost:
                                                    case EnemyAttitude.Boss:
                                                        if (RawTechExporter.aiIcons.TryGetValue(AIType.Aegis, out sprite))
                                                        {
                                                            //Debug.Log("TACtical_AI: UpdateAIDisplay - Swapping sprite!");
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
                                                                    //Debug.Log("TACtical_AI: UpdateAIDisplay - Swapping sprite!");
                                                                    cache.sprite = sprite;
                                                                }
                                                                break;
                                                            case EnemyHandling.Naval:
                                                                if (RawTechExporter.aiIcons.TryGetValue(AIType.Buccaneer, out sprite))
                                                                {
                                                                    //Debug.Log("TACtical_AI: UpdateAIDisplay - Swapping sprite!");
                                                                    cache.sprite = sprite;
                                                                }
                                                                break;
                                                            case EnemyHandling.Starship:
                                                                if (RawTechExporter.aiIcons.TryGetValue(AIType.Astrotech, out sprite))
                                                                {
                                                                    //Debug.Log("TACtical_AI: UpdateAIDisplay - Swapping sprite!");
                                                                    cache.sprite = sprite;
                                                                }
                                                                break;
                                                            default:
                                                                if (RawTechExporter.aiIconsEnemy.TryGetValue(mind.CommanderSmarts, out sprite))
                                                                {
                                                                    //Debug.Log("TACtical_AI: UpdateAIDisplay - Swapping sprite!");
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
                                                            //Debug.Log("TACtical_AI: UpdateAIDisplay - Swapping sprite!");
                                                            cache.sprite = sprite;
                                                        }
                                                        break;
                                                    default:
                                                        if (RawTechExporter.aiIconsEnemy.TryGetValue(mind.CommanderSmarts, out sprite))
                                                        {
                                                            //Debug.Log("TACtical_AI: UpdateAIDisplay - Swapping sprite!");
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
                        //Debug.Log("TACtical_AI: SendUpdateAIDisp - sent!");
                        //return false;
                    }
                    catch
                    {
                        Debug.Log("TACtical_AI: SendUpdateAIDisp - failiure on send!");
                    }
                }
            }
        }

        /*
        [HarmonyPatch(typeof(LocatorPanel))]
        [HarmonyPatch("Format")]//On icon update
        private static class SendUpdateAIDisp2
        {
            static FieldInfo icon = typeof(LocatorPanel).GetField("m_FactionIcon", BindingFlags.NonPublic | BindingFlags.Instance);
            private static bool Prefix(LocatorPanel __instance, ref Sprite iconSprite)
            {
                if (KickStart.EnableBetterAI)
                {
                    try
                    {

                        //Debug.Log("TACtical_AI: UpdateAIDisplay - Triggered!");
                        if (RawTechExporter.lastTech.IsNotNull())
                        {
                            if (RawTechExporter.lastTech.AIState == 1)
                            {
                                if (RawTechExporter.aiIcons.TryGetValue(RawTechExporter.lastTech.DediAI, out Sprite sprite))
                                {
                                    //Debug.Log("TACtical_AI: UpdateAIDisplay - Swapping sprite!");
                                    iconSprite = sprite;
                                    return false;
                                }
                            }
                        }
                        
                        //Image cache = (Image)icon.GetValue(__instance);
                        //cache.sprite = Singleton.Manager<ManUI>.inst.GetAICategoryIcon(AICategories.AIHostile);
                        //icon.SetValue(__instance, cache);

                        //Debug.Log("TACtical_AI: SendUpdateAIDisp2 - Caught Update!");
                    }
                    catch
                    {
                        Debug.Log("TACtical_AI: SendUpdateAIDisp - failiure on send!");
                    }
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(ManUI))]
        [HarmonyPatch("GetAICategoryIcon")]//On icon update
        private static class UpdateAIDisplay
        {
            static bool fired = false;
            private static bool Prefix(ManUI __instance, ref Sprite __result)
            {
                //Debug.Log("TACtical_AI: UpdateAIDisplay - Trigger");
                if (KickStart.EnableBetterAI)
                {
                    try
                    {
                        if (!fired)
                        {
                            Debug.Log("TACtical_AI: UpdateAIDisplay - snapping sprite!"); 
                            Sprite image = __instance.m_SpriteFetcher.GetAICategoryIcon(AICategories.AIHostile);
                            RenderTexture grabTex = RenderTexture.GetTemporary(
                                image.texture.width,
                                image.texture.height,
                                0,
                                RenderTextureFormat.Default,
                                RenderTextureReadWrite.Linear
                                );
                            Graphics.Blit(image.texture, grabTex);
                            RenderTexture grabTex2 = RenderTexture.active;
                            RenderTexture.active = grabTex;

                            Texture2D generated = new Texture2D((int)image.rect.width, (int)image.rect.height);
                            generated.ReadPixels(new Rect(0, 0, (int)grabTex.width, (int)grabTex.height), 0, 0);

                            Debug.Log("TACtical_AI: UpdateModeDisplay - deployed!");
                            FileUtils.SaveTexture(generated, RawTechExporter.BaseDirectory + up + "AI2.png");
                            fired = true;
                        }
                        //image.sprite = spride override
                    }
                    catch
                    {
                        Debug.Log("TACtical_AI: UpdateModeDisplay - failiure on update!");
                    }
                }
                return true;
            }
        }*/

        
        [HarmonyPatch(typeof(ManSpawn))]
        [HarmonyPatch("OnDLCLoadComplete")]//
        private class DelayedLoadRequest
        {
            private static void Postfix(ManSpawn __instance)
            {
                PlayerRTSControl.DelayedInitiate();
            }
        }

        [HarmonyPatch(typeof(NetTech))]
        [HarmonyPatch("SaveTechData")]//On very late update
        private static class DontSaveWhenNotNeeded
        {
            private static bool Prefix(NetTech __instance)
            {
                if (AIERepair.BulkAdding)
                {
                    __instance.QueueSaveTechData();
                    return false;
                }
                return true;
            }
        }


        [HarmonyPatch(typeof(ManLooseBlocks))]
        [HarmonyPatch("OnServerAttachBlockRequest")]//On very late update
        private static class AITechLivesMatter
        {
            private static bool Prefix(ManLooseBlocks __instance, ref NetworkMessage netMsg)
            {
                if (AIERepair.AIAttaching)
                {
                    BlockAttachedMessage BAM = netMsg.ReadMessage<BlockAttachedMessage>();
                    NetTech NetT = NetworkServer.FindLocalObject(BAM.m_TechNetId).GetComponent<NetTech>();
                    TankBlock canidate = ManLooseBlocks.inst.FindTankBlock(BAM.m_BlockPoolID);
                    bool attached;
                    if (NetT == null)
                    {
                        Debug.Log("TACtical_AI: BlockAttachNetworkOverrideServer - NetTech is NULL!");
                    }
                    else if (canidate == null)
                    {
                        Debug.Log("TACtical_AI: BlockAttachNetworkOverrideServer - BLOCK IS NULL!");
                    }
                    else
                    {
                        Tank tank = NetT.tech;
                        NetBlock netBlock = canidate.netBlock;
                        if (netBlock.IsNull())
                        {
                            Debug.Log("TACtical_AI: BlockAttachNetworkOverrideServer - NetBlock could not be found on AI block attach attempt!");
                        }
                        else
                        {
                            OrthoRotation OR = new OrthoRotation((OrthoRotation.r)BAM.m_BlockOrthoRotation);
                            attached = tank.blockman.AddBlockToTech(canidate, BAM.m_BlockPosition, OR);
                            if (attached)
                            {
                                Singleton.Manager<ManNetwork>.inst.ServerNetBlockAttachedToTech.Send(tank, netBlock, canidate);
                                tank.GetComponent<AIECore.TankAIHelper>().dirty = true;

                                //Debug.Log("TACtical_AI: BlockAttachNetworkOverrideServer - NetBlock " + canidate.transform.localPosition + " | " + canidate.transform.localRotation);
                                /*
                                BlockAttachedMessage message = new BlockAttachedMessage
                                {
                                    m_TechNetId = tank.netTech.netId,
                                    m_BlockPosition = BAM.m_BlockPosition,
                                    m_BlockOrthoRotation = BAM.m_BlockOrthoRotation,
                                    m_BlockPoolID = canidate.blockPoolID,
                                };*/
                                Singleton.Manager<ManNetwork>.inst.SendToAllExceptHost(TTMsgType.BlockAttach, BAM);
                                if (netBlock.block != null)
                                {
                                    netBlock.Disconnect();
                                }
                                if (Singleton.Manager<ManNetwork>.inst.IsServer)
                                {
                                    netBlock.RemoveClientAuthority();
                                    NetworkServer.UnSpawn(netBlock.gameObject);
                                }
                                netBlock.transform.Recycle(worldPosStays: false);
                            }
                        }
                    }
                    return false;
                }
                return true;
            }
        }

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

        // this is a VERY big mod
        //   we must make it look big like it is
        [HarmonyPatch(typeof(ModeAttract))]
        [HarmonyPatch("UpdateModeImpl")] // Checking title techs
        private static class RestartAttract
        {
            private static void Prefix(ModeAttract __instance)
            {
                CustomAttract.CheckShouldRestart(__instance);
            }
        }

        [HarmonyPatch(typeof(ModeAttract))]
        [HarmonyPatch("SetupTerrain")]// Setup main menu scene
        private static class SetupTerrainCustom
        {
            private static bool Prefix(ModeAttract __instance)
            {
                return CustomAttract.SetupTerrain(__instance);
            }
        }

        [HarmonyPatch(typeof(ModeAttract))]
        [HarmonyPatch("SetupTechs")]// Setup main menu techs
        private static class ThrowCoolAIInAttract
        {
            private static bool Prefix(ModeAttract __instance)
            {
                return CustomAttract.SetupTechsStart(__instance);
            }
            private static void Postfix(ModeAttract __instance)
            {
                CustomAttract.SetupTechsEnd(__instance);
            }
        }

        
        [HarmonyPatch(typeof(ManTechs))]
        [HarmonyPatch("RegisterTank")]//On Creation
        private static class PatchTankToHelpAI
        {
            private static void Postfix(ManTechs __instance, ref Tank t)
            {
                //Debug.Log("TACtical_AI: Patched Tank OnPool(TankAIHelper & TimeTank)");
                var ModuleCheck = t.GetComponent<AIECore.TankAIHelper>();
                if (ModuleCheck.IsNull())
                {
                    t.gameObject.AddComponent<AIECore.TankAIHelper>().Subscribe();
                }
            }
        }

        
        [HarmonyPatch(typeof(TankBeam))]
        [HarmonyPatch("Update")]//Give the AI some untangle help
        private class PatchTankBeamToHelpAI
        {
            static readonly FieldInfo beamPush = typeof(TankBeam).GetField("m_NudgeStrafe", BindingFlags.NonPublic | BindingFlags.Instance);

            private static void Postfix(TankBeam __instance)
            {
                //Debug.Log("TACtical_AI: Patched TankBeam Update(TankAIHelper)");
                if (__instance.IsActive && !ManNetwork.IsNetworked && !ManGameMode.inst.IsCurrent<ModeSumo>())
                {
                    var helper = __instance.GetComponent<AIECore.TankAIHelper>();
                    if (helper != null && (!helper.tank.PlayerFocused || (PlayerRTSControl.autopilotPlayer && PlayerRTSControl.PlayerIsInRTS)))
                    {
                        if (helper.AIState != AIAlignment.Static)
                        {
                            Vector2 headingSquare = (helper.lastDestination - helper.tank.boundsCentreWorldNoCheck).ToVector2XZ();
                            if (helper.ProceedToObjective)
                            {
                                beamPush.SetValue(__instance, Vector3.ClampMagnitude(helper.tank.rootBlockTrans.InverseTransformVector(headingSquare * helper.DriveVar), 1));
                            }
                            else if (helper.MoveFromObjective)
                            {
                                beamPush.SetValue(__instance, Vector3.ClampMagnitude(helper.tank.rootBlockTrans.InverseTransformVector(-headingSquare * helper.DriveVar), 1));
                            }
                        }
                    }
                }
            }
        }
        

        // Enemy AI's ability to "Lock On"
        [HarmonyPatch(typeof(ModuleAIBot))]
        [HarmonyPatch("OnAttach")]//On Creation
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
                        //Debug.Log("TACtical_AI: Init new AI for " + name);
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
                            ModuleAdd.Scrapper = true;  // Temp Testing
                            ModuleAdd.SelfRepairAI = true; // EXTREMELY POWERFUL
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
                            ModuleAdd.InventoryUser = true;
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


        [HarmonyPatch(typeof(TechWeapon))]//
        [HarmonyPatch("GetManualTarget")]//On targeting
        private static class PatchManualAimingToHelpAI
        {
            //static readonly FieldInfo targPos = typeof(TargetAimer).GetField("m_TargetPosition", BindingFlags.NonPublic | BindingFlags.Instance);
            private static void Postfix(TechWeapon __instance, ref Visible __result)
            {
                if (!KickStart.EnableBetterAI)
                    return;
                var AICommand = __instance.transform.root.GetComponent<AIECore.TankAIHelper>();
                if (AICommand.IsNotNull())
                {
                    if (AICommand.lastLockedTarget)
                        __result = AICommand.lastLockedTarget;
                }
            }
        }

        [HarmonyPatch(typeof(ModuleWeapon))]
        [HarmonyPatch("UpdateAim")]//On targeting
        private static class AllowAIToAimAtScenery
        {
            static readonly FieldInfo targDeli = typeof(TargetAimer).GetField("AimDelegate", BindingFlags.NonPublic | BindingFlags.Instance);
            private static bool Prefix(ModuleWeapon __instance)
            {
                if (!KickStart.EnableBetterAI)
                    return true;
                try
                {
                    var AICommand = __instance.transform.root.GetComponent<AIECore.TankAIHelper>();
                    if (AICommand)
                    {
                        if (AICommand.OverrideAim == 2 && AICommand.Obst.IsNotNull())
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

        }

        [HarmonyPatch(typeof(ModuleWeapon))]
        [HarmonyPatch("UpdateAutoAimBehaviour")]//On targeting
        private static class PatchAimingSystemsToHelpAI
        {
            static readonly FieldInfo aimers = typeof(ModuleWeapon).GetField("m_TargetAimer", BindingFlags.NonPublic | BindingFlags.Instance),
                aimerTargPos = typeof(TargetAimer).GetField("m_TargetPosition", BindingFlags.NonPublic | BindingFlags.Instance),
                WeaponTargPos = typeof(ModuleWeapon).GetField("m_TargetPosition", BindingFlags.NonPublic | BindingFlags.Instance);
            private static void Postfix(ModuleWeapon __instance)
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


        // Resources/Collection
        [HarmonyPatch(typeof(ResourceDispenser))]
        [HarmonyPatch("InitState")]//On World Spawn
        private static class PatchResourcesToHelpAI
        {
            private static void Prefix(ResourceDispenser __instance)
            {
                try
                { 
                //Debug.Log("TACtical_AI: Added resource to list (InitState)");
                if (!AIECore.Minables.Contains(__instance.visible))
                    AIECore.Minables.Add(__instance.visible);
                    //else
                    //    Debug.Log("TACtical_AI: RESOURCE WAS ALREADY ADDED! (InitState)");
                }
                catch { } // null call
            }
        }

        [HarmonyPatch(typeof(ResourceDispenser))]
        [HarmonyPatch("Restore")]//On World reload
        private static class PatchResourceRestoreToHelpAI
        {
            private static void Prefix(ResourceDispenser __instance, ref ResourceDispenser.PersistentState state)
            {
                try
                {
                    //Debug.Log("TACtical_AI: Added resource to list (Restore)");
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
                    //    Debug.Log("TACtical_AI: RESOURCE WAS ALREADY ADDED! (Restore)");
                }
                catch { } // null call
            }
        }


        [HarmonyPatch(typeof(ResourceDispenser))]
        [HarmonyPatch("Die")]//On resource destruction
        private static class PatchResourceDeathToHelpAI
        {
            private static void Prefix(ResourceDispenser __instance)
            {
                try
                {
                    //Debug.Log("TACtical_AI: Removed resource from list (Die)");
                    if (AI.AIECore.Minables.Contains(__instance.visible))
                    {
                        AI.AIECore.Minables.Remove(__instance.visible);
                    }
                    else
                        Debug.Log("TACtical_AI: RESOURCE WAS ALREADY REMOVED! (Die)");
                }
                catch { } // null call
            }
        }

        [HarmonyPatch(typeof(ResourceDispenser))]
        [HarmonyPatch("OnRecycle")]//On World Destruction
        private static class PatchResourceRecycleToHelpAI
        {
            private static void Prefix(ResourceDispenser __instance)
            {
                //Debug.Log("TACtical_AI: Removed resource from list (OnRecycle)");
                if (AIECore.Minables.Contains(__instance.visible))
                {
                    AIECore.Minables.Remove(__instance.visible);
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
                try
                { 
                //Debug.Log("TACtical_AI: Removed resource from list (Deactivate)");
                if (AIECore.Minables.Contains(__instance.visible))
                {
                    AIECore.Minables.Remove(__instance.visible);
                }
                    //else
                    //    Debug.Log("TACtical_AI: RESOURCE WAS ALREADY REMOVED! (Deactivate)");

                }
                catch { } // null call
            }
        }

        [HarmonyPatch(typeof(ModuleItemPickup))]
        [HarmonyPatch("OnAttach")]//On Creation
        private static class MarkReceiver
        {
            private static void Postfix(ModuleItemPickup __instance)
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
                            ModuleAdd.OnPool();
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(ModuleRemoteCharger))]
        [HarmonyPatch("OnAttach")]//On Creation
        private static class MarkChargers
        {
            private static void Postfix(ModuleRemoteCharger __instance)
            {
                var ModuleAdd = __instance.gameObject.GetComponent<ModuleChargerTracker>();
                if (!ModuleAdd)
                {
                    ModuleAdd = __instance.gameObject.AddComponent<ModuleChargerTracker>();
                    ModuleAdd.OnPool();
                }
            }
        }

        [HarmonyPatch(typeof(ModuleItemConsume))]
        [HarmonyPatch("InitRecipeOutput")]//On Creation
        private static class LetNPCsSellStuff
        {
            static readonly FieldInfo progress = typeof(ModuleItemConsume).GetField("m_ConsumeProgress", BindingFlags.NonPublic | BindingFlags.Instance);
            static readonly FieldInfo sellStolen = typeof(ModuleItemConsume).GetField("m_OperateItemInterceptedBy", BindingFlags.NonPublic | BindingFlags.Instance);

            private static bool Prefix(ModuleItemConsume __instance)
            {
                int team = 0;
                if (__instance.block?.tank)
                {
                    team = __instance.block.tank.Team;
                }
                if ((ManNetwork.IsHost || !ManNetwork.IsNetworked) && RawTechLoader.IsBaseTeam(team))
                {
                    ModuleItemConsume.Progress pog = (ModuleItemConsume.Progress)progress.GetValue(__instance);
                    if (pog.currentRecipe.m_OutputType == RecipeTable.Recipe.OutputType.Money && sellStolen.GetValue(__instance) == null)
                    {
                        WorldPosition pos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(__instance.block.visible);
                        int sellGain = (int)(pog.currentRecipe.m_MoneyOutput * KickStart.EnemySellGainModifier);
                        if (KickStart.DisplayEnemyEvents)
                        {
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
                        }
                        RBases.TryAddMoney(sellGain, __instance.block.tank.Team);
                        if (RawTechLoader.IsEnemyBaseTeam(team))
                            PopupEnemyInfo(Singleton.Manager<Localisation>.inst.GetMoneyStringWithSymbol(sellGain), pos);
                        else
                        {
                            PopupAllyInfo(Singleton.Manager<Localisation>.inst.GetMoneyStringWithSymbol(sellGain), pos);
                            return false;
                        }
                    }
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(ModuleHeart))]
        [HarmonyPatch("UpdatePickupTargets")]//On Creation
        private static class LetNPCsSCUStuff
        {
            static readonly FieldInfo PNR = typeof(ModuleHeart).GetField("m_EventHorizonRadius", BindingFlags.NonPublic | BindingFlags.Instance);
            private static void Prefix(ModuleHeart __instance)
            {
                var valid = __instance.GetComponent<ModuleItemHolder>();
                if (valid)
                {
                    int team = __instance.block.tank.Team;
                    if (RawTechLoader.IsBaseTeam(team))
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
                                    int sellGain = (int)(KickStart.EnemySellGainModifier * Singleton.Manager<RecipeManager>.inst.GetBlockSellPrice(vis.block.BlockType));
                                    if (KickStart.DisplayEnemyEvents)
                                    {
                                        WorldPosition pos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(__instance.block.visible);
                                        if (RawTechLoader.IsEnemyBaseTeam(team))
                                            PopupEnemyInfo(Singleton.Manager<Localisation>.inst.GetMoneyStringWithSymbol(sellGain), pos);
                                        else
                                            PopupAllyInfo(Singleton.Manager<Localisation>.inst.GetMoneyStringWithSymbol(sellGain), pos);
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
                                    }
                                    RBases.TryAddMoney(sellGain, team);
                                }
                            }
                        }
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
            static readonly FieldInfo currentTreeActual = typeof(TechAI).GetField("m_CurrentAITreeType", BindingFlags.NonPublic | BindingFlags.Instance);
            private static void Postfix(TechAI __instance)
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
                try
                {
                    other.gameObject.AddComponent<AIESplitHandler>().Setup(other.Tech, __instance.Tech);
                }
                catch
                { }
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

        [HarmonyPatch(typeof(ObjectSpawner))]
        [HarmonyPatch("TrySpawn")]// BEFORE enemy spawn
        private static class EmergencyOverrideOnTechLanding
        {

            private static void Prefix(ref ManSpawn.ObjectSpawnParams objectSpawnParams, ref ManFreeSpace.FreeSpaceParams freeSpaceParams)
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


                                        if (RawTechLoader.ShouldUseCustomTechs(out List<int> valid, FTE, BasePurpose.NotStationary, BaseTerrain.Sea, maxGrade: grade))
                                        {
                                            int randSelect = valid.GetRandomEntry();
                                            newTech = RawTechLoader.GetUnloadedTech(TempManager.ExternalEnemyTechsAll[randSelect], TSP.m_Team, out _);
                                            Debug.Log("TACtical_AI:  Tech " + TSP.m_TechToSpawn.Name + " landed in water and was likely not water-capable, naval Tech " + newTech.Name + " was substituted for the spawn instead");
                                            TSP.m_TechToSpawn = newTech;
                                        }
                                        else
                                        {
                                            SpawnBaseTypes type = RawTechLoader.GetEnemyBaseType(FTE, BasePurpose.NotStationary, BaseTerrain.Sea, maxGrade: grade);
                                            if (type != SpawnBaseTypes.NotAvail && !RawTechLoader.IsFallback(type))
                                            {
                                                newTech = RawTechLoader.GetUnloadedTech(type, TSP.m_Team, out _);
                                                Debug.Log("TACtical_AI:  Tech " + TSP.m_TechToSpawn.Name + " landed in water and was likely not water-capable, naval Tech " + newTech.Name + " was substituted for the spawn instead");
                                                TSP.m_TechToSpawn = newTech;
                                            }
                                            // Else we don't do anything.
                                        }
                                    }
                                    catch
                                    {
                                        Debug.Log("TACtical_AI:  Attempt to swap tech failed!");
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
                                        if (RawTechLoader.ShouldUseCustomTechs(out List<int> valid, FTE, BasePurpose.NotStationary, BaseTerrain.Land, maxGrade: grade, maxPrice: KickStart.EnemySpawnPriceMatching))
                                        {
                                            int randSelect = valid.GetRandomEntry();
                                            newTech = RawTechLoader.GetUnloadedTech(TempManager.ExternalEnemyTechsAll[randSelect], TSP.m_Team, out _);
                                            Debug.Log("TACtical_AI:  Tech " + TSP.m_TechToSpawn.Name + " has been swapped out for land tech " + newTech.Name + " instead");
                                            TSP.m_TechToSpawn = newTech;
                                        }
                                        else
                                        {
                                            SpawnBaseTypes type = RawTechLoader.GetEnemyBaseType(FTE, BasePurpose.NotStationary, BaseTerrain.Land, maxGrade: grade, maxPrice: KickStart.EnemySpawnPriceMatching);
                                            if (type != SpawnBaseTypes.NotAvail && !RawTechLoader.IsFallback(type))
                                            {
                                                newTech = RawTechLoader.GetUnloadedTech(type, TSP.m_Team, out _);
                                                Debug.Log("TACtical_AI:  Tech " + TSP.m_TechToSpawn.Name + " has been swapped out for land tech " + newTech.Name + " instead");
                                                TSP.m_TechToSpawn = newTech;
                                            }
                                            // Else we don't do anything.
                                        }
                                    }
                                    catch
                                    {
                                        Debug.Log("TACtical_AI: Attempt to swap Land tech failed!");
                                    }
                                }

                                RawTechLoader.UseFactionSubTypes = false;
                            }
                        }
                    }
                }
            }
        }

        /*
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
                        if (ManWorld.inst.CheckIsTileAtPositionLoaded(tv.Position))
                        {
                            if (tv.visible.tank != null)
                            {
                                return true;
                            }
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
                        RawTechLoader.UseFactionSubTypes = true;
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


                                if (RawTechLoader.ShouldUseCustomTechs(out List<int> valid, tv.visible.tank.GetMainCorpExt(), BasePurpose.NotStationary, BaseTerrain.Sea, maxGrade: grade))
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

                                    int newTech = valid.GetRandomEntry();
                                    Tank replacementBote = RawTechLoader.SpawnEnemyTechExt(pos, team, posF, TempManager.ExternalEnemyTechs[newTech], AutoTerrain: false);
                                    replacementBote.SetTeam(tv.TeamID, wasPop);

                                    Debug.Log("TACtical_AI:  Tech " + previousTechName + " landed in water and was likely not water-capable, naval Tech " + replacementBote.name + " was substituted for the spawn instead");
                                    tv = ManVisible.inst.GetTrackedVisible(replacementBote.visible.ID);
                                    if (tv == null)
                                        tv = new TrackedVisible(replacementBote.visible.ID, replacementBote.visible, ObjectTypes.Vehicle, inherit);
                                }
                                else
                                {
                                    SpawnBaseTypes type = RawTechLoader.GetEnemyBaseType(tv.visible.tank.GetMainCorpExt(), BasePurpose.NotStationary, BaseTerrain.Sea, maxGrade: grade);
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

                                try
                                {
                                    SpecialAISpawner.Eradicate(tv.visible.tank);
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
                                if (RawTechLoader.ShouldUseCustomTechs(out List<int> valid, tv.visible.tank.GetMainCorpExt(), BasePurpose.NotStationary, BaseTerrain.Land, maxGrade: grade, maxPrice: KickStart.EnemySpawnPriceMatching))
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

                                    int newType = valid.GetRandomEntry();
                                    Tank replacementTech = RawTechLoader.SpawnEnemyTechExt(pos, team, posF, TempManager.ExternalEnemyTechs[newType], AutoTerrain: false);
                                    replacementTech.SetTeam(tv.TeamID, wasPop);

                                    Debug.Log("TACtical_AI:  Tech " + previousTechName + " has been swapped out for land tech " + replacementTech.name + " instead");
                                    tv = ManVisible.inst.GetTrackedVisible(replacementTech.visible.ID);
                                    if (tv == null)
                                        tv = new TrackedVisible(replacementTech.visible.ID, replacementTech.visible, ObjectTypes.Vehicle, inherit);
                                }
                                else
                                {
                                    SpawnBaseTypes type = RawTechLoader.GetEnemyBaseType(tv.visible.tank.GetMainCorpExt(), BasePurpose.NotStationary, BaseTerrain.Land, maxGrade: grade, maxPrice: KickStart.EnemySpawnPriceMatching);
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
                                Debug.Log("TACtical_AI: Attempt to swap Land tech failed!");
                            }
                        }

                        RawTechLoader.UseFactionSubTypes = false;
                    }
                }
            }
        }
        */
#if !STEAM
        [HarmonyPatch(typeof(ManGameMode))]
        [HarmonyPatch("Awake")]//On Game start
        private static class StartupSpecialAISpawner
        {
            private static void Postfix()
            {
                // Setup aircraft if Population Injector is N/A
                if (!KickStart.isPopInjectorPresent)
                {
                    SpecialAISpawner.Initiate();
                    ManEnemyWorld.LateInitiate();
                }
            }
        }
#endif

        [HarmonyPatch(typeof(ModuleHeart))]
        [HarmonyPatch("OnAttach")]//On Game start
        private static class SpawnTraderTroll
        {
            private static void Postfix(ModuleHeart __instance)
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
                        AIECore.TankAIManager.inst.Invoke("WarnPlayers", 16);
                }
                catch{ }
            }
        }

        // Bases
        /*
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
                }
                catch { }
            }
        }*/
    }
}
