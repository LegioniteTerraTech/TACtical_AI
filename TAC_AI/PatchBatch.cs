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
        bool firstInit = false;
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
                catch (Exception e) { Debug.LogError(e); }
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
            if (isInit)
                return;
            if (oInst == null)
                oInst = this;
            KickStart.GetActiveMods();
            KickStart.MainOfficialInit();
            try
            {
                //ManSafeSaves.Init();
            }
            catch (Exception e) { Debug.LogError(e); }
            isInit = true;
        }
        public override void DeInit()
        {
            if (!isInit)
                return;
            KickStart.DeInitALL();
            isInit = false;
        }

        public override void Update()
        {
            if (!firstInit)
            {
                if (Singleton.Manager<ManTechs>.inst)
                {
                    SpecialAISpawner.Initiate();
                    EnemyWorldManager.LateInitiate();
                    firstInit = true;
                }
            }
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

        static readonly FieldInfo panelData = typeof(FloatingTextOverlay).GetField("m_Data", BindingFlags.NonPublic | BindingFlags.Instance);
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
                        var aI = __instance.transform.root.GetComponent<Tank>().AI;
                        if (ManNetwork.IsNetworked)
                        {
                            var tankAIHelp = tank.gameObject.GetComponent<AIECore.TankAIHelper>();
                            if (!tankAIHelp)
                            {
                                tankAIHelp = tank.gameObject.AddComponent<AIECore.TankAIHelper>();
                                tankAIHelp.Subscribe(tank);
                            }
                            if (ManNetwork.IsHost)
                            {
                                bool IsPlayerRemoteControlled = false;
                                try
                                {
                                    IsPlayerRemoteControlled = ManNetwork.inst.GetAllPlayerTechs().Contains(tank);
                                }
                                catch { }
                                if (IsPlayerRemoteControlled)
                                {
                                    if (Singleton.playerTank == tank && PlayerRTSControl.PlayerIsInRTS)
                                    {
                                        tankAIHelp.SetRTSState(true);
                                        tankAIHelp.BetterAI(__instance.block.tank.control);
                                        __result = true;
                                        return false;
                                    }
                                }
                                else
                                {
                                    if (tank.FirstUpdateAfterSpawn)
                                    {
                                        if (tank.GetComponent<RequestAnchored>())
                                        {
                                            if (!__instance.block.tank.IsAnchored)
                                                __instance.block.tank.FixupAnchors(true);
                                        }
                                        // let the icon update
                                    }
                                    else if ((aI.CheckAIAvailable() || tank.PlayerFocused) && ManSpawn.IsPlayerTeam(tank.Team))
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
                                        //return false;
                                    }
                                    else if (KickStart.enablePainMode && tank.IsEnemy() && !ManSpawn.IsPlayerTeam(tank.Team))
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
                            else
                            {
                                if (Singleton.playerTank == tank && PlayerRTSControl.PlayerIsInRTS)
                                {
                                    if (tank.PlayerFocused)
                                    {
                                        tankAIHelp.SetRTSState(true);
                                        tankAIHelp.BetterAI(__instance.block.tank.control);
                                        __result = true;
                                        return false;
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (!tank.PlayerFocused || PlayerRTSControl.PlayerIsInRTS)//&& !Singleton.Manager<ManGameMode>.inst.IsCurrentModeMultiplayer())
                            {
                                var tankAIHelp = tank.gameObject.GetComponent<AIECore.TankAIHelper>();
                                if (!tankAIHelp)
                                {
                                    tankAIHelp = tank.gameObject.AddComponent<AIECore.TankAIHelper>();
                                    tankAIHelp.Subscribe(tank);
                                }
                                if (tank.FirstUpdateAfterSpawn)
                                {
                                    if (tank.GetComponent<RequestAnchored>())
                                    {
                                        if (!__instance.block.tank.IsAnchored)
                                            __instance.block.tank.FixupAnchors(true);
                                    }
                                    // let the icon update
                                }
                                else if ((aI.CheckAIAvailable() || tank.PlayerFocused) && ManSpawn.IsPlayerTeam(tank.Team))
                                {
                                    //Debug.Log("TACtical_AI: AI Valid!");
                                    //Debug.Log("TACtical_AI: (TankAIHelper) is " + tank.gameObject.GetComponent<AIEnhancedCore.TankAIHelper>().wasEscort);
                                    //tankAIHelp.AIState && 
                                    if (tankAIHelp.JustUnanchored)
                                    {
                                        tankAIHelp.ForceAllAIsToEscort();
                                        tankAIHelp.JustUnanchored = false;
                                    }
                                    else if (tank.PlayerFocused)
                                    {
                                        tankAIHelp.SetRTSState(true);
                                        tankAIHelp.BetterAI(__instance.block.tank.control);
                                        __result = true;
                                        return false;
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
                                    //return false;
                                }
                                else if (KickStart.enablePainMode && tank.IsEnemy() && !ManSpawn.IsPlayerTeam(tank.Team))
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
            static FieldInfo tech = typeof(TankDescriptionOverlay).GetField("m_Tank", BindingFlags.NonPublic | BindingFlags.Instance);
            static FieldInfo panel = typeof(TankDescriptionOverlay).GetField("m_PanelInst", BindingFlags.NonPublic | BindingFlags.Instance);
            static FieldInfo icon = typeof(LocatorPanel).GetField("m_FactionIcon", BindingFlags.NonPublic | BindingFlags.Instance);
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
                                if (tank.IsAnchored)
                                {   // Use anchor icon

                                }
                                else if (lastTech.AIState == 1)
                                {   // Allied AI
                                    if (lastTech.lastAIType == AITreeType.AITypes.Escort)
                                    {
                                        if (RawTechExporter.aiIcons.TryGetValue(KickStart.GetLegacy(lastTech.DediAI, lastTech.DriverType), out Sprite sprite))
                                        {
                                            //Debug.Log("TACtical_AI: UpdateAIDisplay - Swapping sprite!");
                                            Image cache = (Image)icon.GetValue(Panel);
                                            cache.sprite = sprite;
                                            icon.SetValue(Panel, cache);
                                        }
                                    }
                                }
                                else if (lastTech.AIState == 2)
                                {   // Enemy AI
                                    if (KickStart.enablePainMode)
                                    {
                                        var mind = lastTech.GetComponent<EnemyMind>();
                                        if ((bool)mind)
                                        {
                                            if (mind.CommanderSmarts < EnemySmarts.Smrt)
                                            {
                                                switch (mind.EvilCommander)
                                                {
                                                    case EnemyHandling.Airplane:
                                                    case EnemyHandling.Chopper:
                                                        if (RawTechExporter.aiIcons.TryGetValue(AIType.Aviator, out Sprite sprite1))
                                                        {
                                                            //Debug.Log("TACtical_AI: UpdateAIDisplay - Swapping sprite!");
                                                            Image cache = (Image)icon.GetValue(Panel);
                                                            cache.sprite = sprite1;
                                                            icon.SetValue(Panel, cache);
                                                        }
                                                        break;
                                                    case EnemyHandling.Naval:
                                                        if (RawTechExporter.aiIcons.TryGetValue(AIType.Buccaneer, out Sprite sprite2))
                                                        {
                                                            //Debug.Log("TACtical_AI: UpdateAIDisplay - Swapping sprite!");
                                                            Image cache = (Image)icon.GetValue(Panel);
                                                            cache.sprite = sprite2;
                                                            icon.SetValue(Panel, cache);
                                                        }
                                                        break;
                                                    case EnemyHandling.Starship:
                                                        if (RawTechExporter.aiIcons.TryGetValue(AIType.Astrotech, out Sprite sprite3))
                                                        {
                                                            //Debug.Log("TACtical_AI: UpdateAIDisplay - Swapping sprite!");
                                                            Image cache = (Image)icon.GetValue(Panel);
                                                            cache.sprite = sprite3;
                                                            icon.SetValue(Panel, cache);
                                                        }
                                                        break;
                                                    default:
                                                        if (RawTechExporter.aiIconsEnemy.TryGetValue(mind.CommanderSmarts, out Sprite sprite))
                                                        {
                                                            //Debug.Log("TACtical_AI: UpdateAIDisplay - Swapping sprite!");
                                                            Image cache = (Image)icon.GetValue(Panel);
                                                            cache.sprite = sprite;
                                                            icon.SetValue(Panel, cache);
                                                        }
                                                        break;
                                                }
                                            }
                                            else
                                            {
                                                if (RawTechExporter.aiIconsEnemy.TryGetValue(mind.CommanderSmarts, out Sprite sprite))
                                                {
                                                    //Debug.Log("TACtical_AI: UpdateAIDisplay - Swapping sprite!");
                                                    Image cache = (Image)icon.GetValue(Panel);
                                                    cache.sprite = sprite;
                                                    icon.SetValue(Panel, cache);
                                                }
                                            }
                                        }
                                    }
                                }
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
#if STEAM
        /*
        [HarmonyPatch(typeof(ManSaveGame))]
        [HarmonyPatch("Save")]// SAAAAAVVE
        private static class SaveTheSaves
        {
            private static void Prefix(ref ManGameMode.GameType gameType, ref string saveName)
            {
                Debug.Log("SafeSaves: Saving!");
                ManSafeSaves.SaveData(saveName, ManGameMode.inst.GetCurrentGameMode());
            }
        }*/
#endif

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
                RawTechExporter.LateInitiate();
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
            static FieldInfo spawnNum = typeof(ModeAttract).GetField("spawnIndex", BindingFlags.NonPublic | BindingFlags.Instance);

            private static bool Prefix(ModeAttract __instance)
            {
                // Testing
                bool caseOverride = true;
                AttractType outNum = AttractType.Dogfight;

#if DEBUG
                caseOverride = true;
#else
                caseOverride = false;
#endif

                if (UnityEngine.Random.Range(1, 100) > 80 || KickStart.retryForBote == 1 || caseOverride)
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
                {
                    KickStart.SpecialAttract = false;
                }
                int spawnIndex = (int)spawnNum.GetValue(__instance);
                Singleton.Manager<ManWorld>.inst.SeedString = null;
                Singleton.Manager<ManGameMode>.inst.RegenerateWorld(__instance.spawns[spawnIndex].biomeMap, __instance.spawns[spawnIndex].cameraSpawn.position, __instance.spawns[spawnIndex].cameraSpawn.orientation);
                Singleton.Manager<ManTimeOfDay>.inst.EnableSkyDome(enable: true);
                Singleton.Manager<ManTimeOfDay>.inst.EnableTimeProgression(enable: false);
                Singleton.Manager<ManTimeOfDay>.inst.SetTimeOfDay(UnityEngine.Random.Range(0, 23), 0, 0);//11
                return false;
            }
        }

        [HarmonyPatch(typeof(ModeAttract))]
        [HarmonyPatch("SetupTechs")]// Setup main menu techs
        private static class ThrowCoolAIInAttract
        {
            static FieldInfo spawnNum = typeof(ModeAttract).GetField("spawnIndex", BindingFlags.NonPublic | BindingFlags.Instance);

            static FieldInfo rTime = typeof(ModeAttract).GetField("resetAtTime", BindingFlags.NonPublic | BindingFlags.Instance);
            private static bool Prefix(ModeAttract __instance)
            {
                try
                {
                    if (KickStart.SpecialAttract)
                    {
                        int spawnIndex = (int)spawnNum.GetValue(__instance);
                        Vector3 spawn = __instance.spawns[spawnIndex].vehicleSpawnCentre.position;
                        Singleton.Manager<ManWorld>.inst.GetTerrainHeight(spawn, out float height);
                        spawn.y = height;

                        List<Vector3> tanksToConsider = new List<Vector3>();

                        int numToSpawn = 3;
                        float rad = 360f / (float)numToSpawn;
                        for (int step = 0; step < numToSpawn; step++)
                        {
                            Quaternion rotation = Quaternion.AngleAxis(UnityEngine.Random.value * 360f, Vector3.up);
                            Vector3 offset = Quaternion.Euler(0f, (float)step * rad, 0f) * Vector3.forward * 16;
                            tanksToConsider.Add(__instance.spawns[spawnIndex].vehicleSpawnCentre.position + offset);
                        }

                        AttractType randNum = KickStart.SpecialAttractNum;
                        Debug.Log("TACtical_AI: Pre-Setup for attract type " + randNum.ToString());
                        switch (randNum)
                        {
                            case AttractType.SpaceInvader: // space invader
                                //Debug.Log("TACtical_AI: Throwing in TAC ref lol");
                                RawTechLoader.SpawnAttractTech(spawn, 749, Vector3.forward, BaseTerrain.Space);
                                break;

                            case AttractType.Harvester: // Peaceful harvesting
                                RawTechLoader.SpawnSpecificTypeTech(spawn, 1, Vector3.forward, new List<BasePurpose> { BasePurpose.HasReceivers }, silentFail: false);
                                RawTechLoader.SpawnSpecificTypeTech(tanksToConsider[0], 1, Vector3.forward, new List<BasePurpose> { BasePurpose.NotStationary, BasePurpose.Harvesting }, silentFail: false);
                                rTime.SetValue(__instance, Time.time + __instance.resetTime);
                                spawnIndex = (spawnIndex + 1) % __instance.spawns.Length;
                                return false;

                            case AttractType.Dogfight: // Aircraft fight
                                for (int step = 0; numToSpawn > step; step++)
                                {
                                    Vector3 position = tanksToConsider[step] + (Vector3.up * 10);
                                    if (!RawTechLoader.SpawnAttractTech(position, (int)(UnityEngine.Random.Range(1, 999) + 0.5f), -(spawn - tanksToConsider[step]).normalized, BaseTerrain.Air, silentFail: false))
                                        Debug.Log("TACtical_AI: ThrowCoolAIInAttract(Dogfight) - error ~ could not find Tech");
                                }
                                rTime.SetValue(__instance, Time.time + __instance.resetTime);
                                spawnIndex = (spawnIndex + 1) % __instance.spawns.Length;
                                return false;

                            case AttractType.SpaceBattle: // Airship assault
                                for (int step = 0; numToSpawn > step; step++)
                                {
                                    Vector3 position = tanksToConsider[step] + (Vector3.up * 14);
                                    if (RawTechLoader.SpawnAttractTech(position, (int)(UnityEngine.Random.Range(1, 999) + 0.5f), (spawn - tanksToConsider[step]).normalized, BaseTerrain.Space, silentFail: false))
                                        Debug.Log("TACtical_AI: ThrowCoolAIInAttract(SpaceBattle) - error ~ could not find Tech");
                                }
                                rTime.SetValue(__instance, Time.time + __instance.resetTime);
                                spawnIndex = (spawnIndex + 1) % __instance.spawns.Length;
                                return false;

                            case AttractType.NavalWarfare: // Naval Brawl
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
                                    for (int step = 0; numToSpawn > step; step++)
                                    {
                                        Vector3 offset = Quaternion.Euler(0f, (float)step * rad, 0f) * Vector3.forward * 16;
                                        Vector3 posSea = KickStart.SpecialAttractPos + offset;

                                        Vector3 forward = (KickStart.SpecialAttractPos - posSea).normalized;
                                        Vector3 position = posSea;// - (forward * 10);
                                        position = AI.Movement.AIEPathing.ForceOffsetToSea(position);

                                        if (!RawTechLoader.SpawnAttractTech(position, (int)(UnityEngine.Random.Range(1, 999) + 0.5f), forward, BaseTerrain.Sea, silentFail: false))
                                            RawTechLoader.SpawnAttractTech(position, (int)(UnityEngine.Random.Range(1, 999) + 0.5f), forward, BaseTerrain.Space, silentFail: false);
                                    }
                                    //Debug.Log("TACtical_AI: cam is at " + Singleton.Manager<CameraManager>.inst.ca);
                                    Singleton.Manager<CameraManager>.inst.ResetCamera(KickStart.SpecialAttractPos, Quaternion.LookRotation(Vector3.forward));
                                    Singleton.cameraTrans.position = KickStart.SpecialAttractPos;
                                    Singleton.cameraTrans.rotation = Quaternion.LookRotation(Vector3.forward);
                                    rTime.SetValue(__instance, Time.time + __instance.resetTime);
                                    spawnIndex = (spawnIndex + 1) % __instance.spawns.Length;
                                    return false;
                                }
                                else
                                    RawTechLoader.SpawnAttractTech(spawn, 749, Vector3.forward, BaseTerrain.Land);
                                break;

                            case AttractType.HQSiege: // HQ Siege
                                RawTechLoader.SpawnAttractTech(spawn, 916, Vector3.forward, BaseTerrain.Land, purpose: BasePurpose.Headquarters);
                                break;

                            case AttractType.BaseVBase: // BaseVBase - Broken ATM
                                /* 
                                RawTechLoader.SpawnAttractTech(spawn + (Vector3.forward * 50), 56, -Vector3.forward, BaseTerrain.Land, purpose: BasePurpose.TechProduction);
                                RawTechLoader.SpawnAttractTech(spawn + (Vector3.forward * 25), 56, -Vector3.forward, BaseTerrain.Land);
                                RawTechLoader.SpawnAttractTech(spawn - (Vector3.forward * 50), 12, Vector3.forward, BaseTerrain.Land, purpose: BasePurpose.TechProduction);
                                RawTechLoader.SpawnAttractTech(spawn - (Vector3.forward * 25), 12, Vector3.forward, BaseTerrain.Land);
                                rTime.SetValue(__instance, Time.time + __instance.resetTime);
                                spawnIndex = (spawnIndex + 1) % __instance.spawns.Length;
                                return false;*/

                            case AttractType.Misc: // pending
                                for (int step = 0; numToSpawn > step; step++)
                                {
                                    Vector3 position = tanksToConsider[step] + (Vector3.up * 10);

                                    if (RawTechLoader.SpawnAttractTech(position, (int)(UnityEngine.Random.Range(1, 999) + 0.5f), (spawn - tanksToConsider[step]).normalized, BaseTerrain.AnyNonSea))
                                        Debug.Log("TACtical_AI: ThrowCoolAIInAttract(Misc) - error ~ could not find Tech");
                                }
                                RawTechLoader.SpawnAttractTech(spawn, 749, Vector3.forward, BaseTerrain.Air);
                                rTime.SetValue(__instance, Time.time + __instance.resetTime);
                                spawnIndex = (spawnIndex + 1) % __instance.spawns.Length;
                                return false;

                            default: //AttractType.Invader: - Land battle invoker
                                RawTechLoader.SpawnAttractTech(spawn, 749, Vector3.forward, BaseTerrain.Land);
                                break;
                        }
                    }
                }
                catch { }
                return true;
            }
            private static void Postfix(ModeAttract __instance)
            {
                try
                {
                    if (KickStart.SpecialAttract)
                    {
                        int TechCount = Singleton.Manager<ManTechs>.inst.CurrentTechs.Count();
                        List<Tank> tanksToConsider = Singleton.Manager<ManTechs>.inst.CurrentTechs.ToList();

                        AttractType randNum = KickStart.SpecialAttractNum;
                        if (randNum == AttractType.Harvester)
                        {   // Peaceful harvesting
                            /*
                            for (int step = 2; TechCount > step; step++)
                            {
                                Tank tech = tanksToConsider.ElementAt(step);
                                tech.visible.RemoveFromGame();
                            }*/
                        }
                        else if (randNum == AttractType.Dogfight)
                        {   // Aircraft fight
                        }
                        else if (randNum == AttractType.SpaceBattle)
                        {   // Airship assault
                        }
                        else if (randNum == AttractType.NavalWarfare)
                        {   // Naval Brawl
                        }
                        else if (randNum == AttractType.HQSiege)
                        {   // HQ Siege
                            foreach (Tank tech in Singleton.Manager<ManTechs>.inst.CurrentTechs)
                            {
                                tech.SetTeam(4114);
                            }
                            Singleton.Manager<ManTechs>.inst.CurrentTechs.First().SetTeam(916);
                            Singleton.Manager<ManTechs>.inst.CurrentTechs.ElementAtOrDefault(1).SetTeam(916);
                        }
                        else if (randNum == AttractType.Misc)
                        {   // pending
                        }
                        else // AttractType.Invader
                        {   // Land battle invoker
                        }

                        //Debug.Log("TACtical_AI: Post-Setup for attract type " + KickStart.SpecialAttractNum.ToString());
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
            static FieldInfo targPos = typeof(TargetAimer).GetField("m_TargetPosition", BindingFlags.NonPublic | BindingFlags.Instance);
            private static void Postfix(TechWeapon __instance, ref Visible __result)
            {
                if (!KickStart.EnableBetterAI)
                    return;
                var AICommand = __instance.transform.root.GetComponent<AIECore.TankAIHelper>();
                if (AICommand.IsNotNull())
                {
                    var tank = AICommand.tank;
                    if (tank.IsNotNull())
                    {
                        if (!tank.PlayerFocused)
                        {
                            if (AICommand.OverrideAim == 1)
                            {
                                if (AICommand.lastEnemy.IsNotNull())
                                {   // Allow the enemy AI to finely select targets
                                    //Debug.Log("TACtical_AI: Overriding targeting to aim at " + AICommand.lastEnemy.name + "  pos " + AICommand.lastEnemy.tank.boundsCentreWorldNoCheck);

                                    __result = AICommand.lastEnemy;
                                }
                            }
                            else if (AICommand.OverrideAim == 2)
                            {
                                if (AICommand.Obst.IsNotNull())
                                {
                                    var resTarget = AICommand.Obst.GetComponent<Visible>();
                                    if (resTarget)
                                    {
                                        //Debug.Log("TACtical_AI: Overriding targeting to aim at obstruction");
                                        __result = resTarget;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        [HarmonyPatch(typeof(TargetAimer))]//
        [HarmonyPatch("UpdateTarget")]//On targeting
        private static class PatchAimingToHelpAI
        {
            static FieldInfo targPos = typeof(TargetAimer).GetField("m_TargetPosition", BindingFlags.NonPublic | BindingFlags.Instance);
            private static void Postfix(TargetAimer __instance)
            {
                if (!KickStart.EnableBetterAI && !KickStart.isWeaponAimModPresent)
                    return;
                var AICommand = __instance.transform.root.GetComponent<AIECore.TankAIHelper>();
                if (AICommand.IsNotNull())
                {
                    var tank = AICommand.GetComponent<Tank>();
                    if (tank.IsNotNull())
                    {
                        if (!tank.PlayerFocused)
                        {
                            if (AICommand.OverrideAim == 1)
                            {
                                if (AICommand.lastEnemy.IsNotNull())
                                {   // Allow the enemy AI to finely select targets
                                    //Debug.Log("TACtical_AI: Overriding targeting to aim at " + AICommand.lastEnemy.name + "  pos " + AICommand.lastEnemy.tank.boundsCentreWorldNoCheck);
                                    //FieldInfo targ = typeof(TargetAimer).GetField("Target", BindingFlags.NonPublic | BindingFlags.Instance);
                                    //targ.SetValue(__instance, AICommand.lastEnemy);

                                    //targPos.SetValue(__instance, tank.control.TargetPositionWorld);

                                    if (AICommand.lastPlayer.IsNotNull())
                                    {
                                        var playerTarg = AICommand.lastPlayer.tank.Weapons.GetManualTarget();
                                        if (playerTarg != null)
                                        {
                                            if ((bool)playerTarg.tank)
                                            {
                                                try
                                                {
                                                    if (playerTarg.tank.CentralBlock && playerTarg.isActive)
                                                    {   // Relay position from player to allow artillery support
                                                        targPos.SetValue(__instance, playerTarg.GetAimPoint(tank.boundsCentreWorldNoCheck));
                                                        return;
                                                    }
                                                }
                                                catch { }
                                            }
                                        }
                                    }
                                    if (AICommand.lastEnemy.tank?.CentralBlock && AICommand.lastEnemy.isActive)
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
        [HarmonyPatch("UpdateAim")]//On targeting
        private static class AllowAIToAimAtScenery
        {
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
                            var rotSped = __instance.RotateSpeed;
                            var ta = __instance.GetComponent<TargetAimer>();
                            if (ta)
                                ta.AimAtWorldPos(AICommand.Obst.position + (Vector3.up * 2), rotSped);
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
            static FieldInfo aimers = typeof(ModuleWeapon).GetField("m_TargetAimer", BindingFlags.NonPublic | BindingFlags.Instance);
            static FieldInfo aimerTargPos = typeof(TargetAimer).GetField("m_TargetPosition", BindingFlags.NonPublic | BindingFlags.Instance);
            static FieldInfo WeaponTargPos = typeof(ModuleWeapon).GetField("m_TargetPosition", BindingFlags.NonPublic | BindingFlags.Instance);
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
        private static class LetEnemiesSellStuff
        {
            static readonly FieldInfo progress = typeof(ModuleItemConsume).GetField("m_ConsumeProgress", BindingFlags.NonPublic | BindingFlags.Instance);
            static readonly FieldInfo sellStolen = typeof(ModuleItemConsume).GetField("m_OperateItemInterceptedBy", BindingFlags.NonPublic | BindingFlags.Instance);

            private static void Prefix(ModuleItemConsume __instance)
            {
                int team = 0;
                if (__instance.block.tank.IsNotNull())
                {
                    team = __instance.block.tank.Team;
                }
                if ((ManNetwork.IsHost || !ManNetwork.IsNetworked) && ManSpawn.IsEnemyTeam(team))
                {
                    ModuleItemConsume.Progress pog = (ModuleItemConsume.Progress)progress.GetValue(__instance);
                    if (pog.currentRecipe.m_OutputType == RecipeTable.Recipe.OutputType.Money && sellStolen.GetValue(__instance) == null)
                    {
                        int sellGain = (int)(pog.currentRecipe.m_MoneyOutput * KickStart.EnemySellGainModifier);
                        if (KickStart.DisplayEnemyEvents)
                        {
                            WorldPosition pos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(__instance.block.visible);
                            PopupEnemyInfo(Singleton.Manager<Localisation>.inst.GetMoneyStringWithSymbol(sellGain), pos);
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
                    }
                }
            }
            /* Legacy
            private static void Prefix(ModuleItemConsume __instance)
            {

                var valid = __instance.transform.root.GetComponent<RBases.EnemyBaseFunder>();
                if ((bool)valid && (ManNetwork.IsHost || !ManNetwork.IsNetworked))
                {
                    ModuleItemConsume.Progress pog = (ModuleItemConsume.Progress)progress.GetValue(__instance);
                    if (pog.currentRecipe.m_OutputType == RecipeTable.Recipe.OutputType.Money && sellStolen.GetValue(__instance) == null)
                    {
                        int sellGain = pog.currentRecipe.m_MoneyOutput;
                        if (KickStart.DisplayEnemyEvents)
                        {
                            WorldPosition pos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(__instance.block.visible);
                            PopupEnemyInfo(Singleton.Manager<Localisation>.inst.GetMoneyStringWithSymbol(sellGain), pos);
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
                        valid.AddBuildBucks((int)(sellGain * KickStart.EnemySellGainModifier));
                    }
                }
            }*/
        }

        [HarmonyPatch(typeof(ModuleHeart))]
        [HarmonyPatch("UpdatePickupTargets")]//On Creation
        private static class LetEnemiesSCUStuff
        {
            static FieldInfo PNR = typeof(ModuleHeart).GetField("m_EventHorizonRadius", BindingFlags.NonPublic | BindingFlags.Instance);
            private static void Prefix(ModuleHeart __instance)
            {
                var valid = __instance.GetComponent<ModuleItemHolder>();
                if (valid)
                {
                    int team = __instance.block.tank.Team;
                    if (ManSpawn.IsEnemyTeam(team))
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
                                        PopupEnemyInfo(Singleton.Manager<Localisation>.inst.GetMoneyStringWithSymbol(sellGain), pos);
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
                    EnemyWorldManager.LateInitiate();
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
