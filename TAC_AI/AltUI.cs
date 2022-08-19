using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace TerraTechETCUtil
{
    public static class AltUI
    {
        // INIT
        internal static GUISkin MenuGUI;
        internal static float UIAlpha = 0.65f;
        internal static string UIAlphaText = "<color=#454545ff>";

        internal static GUIStyle MenuLeft;
        private static Texture2D MenuTexRect;
        private static GUIStyleState MenuLeftStyle;

        internal static GUIStyle ButtonBlue;
        private static Texture2D ButtonTexMain;
        private static Texture2D ButtonTexHover;
        private static GUIStyleState ButtonStyle;
        private static GUIStyleState ButtonStyleHover;

        internal static GUIStyle ButtonGreen;
        private static Texture2D ButtonTexAccept;
        private static Texture2D ButtonTexAcceptHover;
        private static GUIStyleState ButtonStyleAccept;
        private static GUIStyleState ButtonStyleAcceptHover;

        internal static GUIStyle ButtonRed;
        private static Texture2D ButtonTexDisabled;
        private static Texture2D ButtonTexDisabledHover;
        private static GUIStyleState ButtonStyleDisabled;
        private static GUIStyleState ButtonStyleDisabledHover;


        internal static GUIStyle ButtonGrey;
        private static Texture2D ButtonTexInactive;
        private static GUIStyleState ButtonStyleInactive;

        internal static GUIStyle ButtonBlueActive;
        private static GUIStyleState ButtonStyleActive;
        private static Texture2D ButtonTexSelect;

        internal static GUIStyle ButtonGreenActive;
        private static GUIStyleState ButtonStyleGActive;
        private static Texture2D ButtonTexSelectGreen;

        internal static GUIStyle ButtonRedActive;
        private static GUIStyleState ButtonStyleRActive;
        private static Texture2D ButtonTexSelectRed;

        private static void FetchResourcesFromGame()
        {
            if (MenuLeft == null)
            {
                try
                {
                    Texture2D[] res = Resources.FindObjectsOfTypeAll<Texture2D>();
                    for (int step = 0; step < res.Length; step++)
                    {
                        Texture2D resCase = res[step];
                        if (resCase && !resCase.name.NullOrEmpty())
                        {
                            if (resCase.name == "ACTION_MENU_SHORT_BKG")
                                MenuTexRect = resCase;
                            else if (resCase.name == "Button_BLUE")       // HUD_Button_BG
                                ButtonTexMain = resCase;
                            else if (resCase.name == "Button_BLUE_Highlight")// HUD_Button_Highlight
                                ButtonTexHover = resCase;
                            else if (resCase.name == "Button_BLUE_Pressed") // HUD_Button_Selected
                                ButtonTexSelect = resCase;
                            else if (resCase.name == "Button_GREEN")        // ????
                                ButtonTexAccept = resCase;
                            else if (resCase.name == "Button_GREEN_Highlight")// ????
                                ButtonTexAcceptHover = resCase;
                            else if (resCase.name == "Button_GREEN_Pressed")// ????
                                ButtonTexSelectGreen = resCase;
                            else if (resCase.name == "Button_RED")          // HUD_Button_Disabled_BG
                                ButtonTexDisabled = resCase;
                            else if (resCase.name == "Button_RED_Highlight")        // ????
                                ButtonTexDisabledHover = resCase;
                            else if (resCase.name == "Button_RED_Pressed")        // ????
                                ButtonTexSelectRed = resCase;
                            else if (resCase.name == "HUD_Button_InActive") // HUD_Button_InActive
                                ButtonTexInactive = resCase;
                        }
                    }
                }
                catch
                {
                    Debug.Assert(true, "AltUI: failed to fetch textures");
                    return;
                }


                // Setup Menu
                MenuLeft = new GUIStyle(GUI.skin.window);
                try
                {
                    MenuLeft.font = Resources.FindObjectsOfTypeAll<Font>().ToList().Find(delegate (Font cand)
                    { return cand.name == "Exo-SemiBoldItalic"; });
                }
                catch { }
                MenuLeftStyle = new GUIStyleState() { background = MenuTexRect, textColor = new Color(0, 0, 0, 1), };
                MenuLeft.padding = new RectOffset(MenuTexRect.width / 6, MenuTexRect.width / 6, MenuTexRect.height / 12, MenuTexRect.height / 12);
                MenuLeft.border = new RectOffset(MenuTexRect.width / 3, MenuTexRect.width / 3, MenuTexRect.height / 6, MenuTexRect.height / 6);
                MenuLeft.normal = MenuLeftStyle;
                MenuLeft.hover = MenuLeftStyle;
                MenuLeft.active = MenuLeftStyle;
                MenuLeft.focused = MenuLeftStyle;
                MenuLeft.onNormal = MenuLeftStyle;
                MenuLeft.onHover = MenuLeftStyle;
                MenuLeft.onActive = MenuLeftStyle;
                MenuLeft.onFocused = MenuLeftStyle;

                GUIStyle ButtonBase = new GUIStyle(GUI.skin.button);
                ButtonBase.padding = new RectOffset(0, 0, 0, 0);
                ButtonBase.border = new RectOffset(ButtonTexMain.width / 4, ButtonTexMain.width / 4, ButtonTexMain.height / 4, ButtonTexMain.height / 4);

                try
                {
                    ButtonBase.font = Resources.FindObjectsOfTypeAll<Font>().ToList().Find(delegate (Font cand)
                    { return cand.name == "Exo-ExtraBold"; });
                }
                catch { }

                // Setup Button Default
                ButtonBlue = new GUIStyle(ButtonBase);
                ButtonStyle = new GUIStyleState() { background = ButtonTexMain, textColor = new Color(1, 1, 1, 1), };
                ButtonStyleHover = new GUIStyleState() { background = ButtonTexHover, textColor = new Color(1, 1, 1, 1), };
                ButtonBlue.normal = ButtonStyle;
                ButtonBlue.hover = ButtonStyleHover;
                ButtonBlue.active = ButtonStyle;
                ButtonBlue.focused = ButtonStyle;
                ButtonBlue.onNormal = ButtonStyle;
                ButtonBlue.onHover = ButtonStyleHover;
                ButtonBlue.onActive = ButtonStyle;
                ButtonBlue.onFocused = ButtonStyle;

                // Setup Button Accept
                ButtonGreen = new GUIStyle(ButtonBase);
                ButtonStyleAccept = new GUIStyleState() { background = ButtonTexAccept, textColor = new Color(1, 1, 1, 1), };
                ButtonStyleAcceptHover = new GUIStyleState() { background = ButtonTexAcceptHover, textColor = new Color(1, 1, 1, 1), };
                ButtonGreen.normal = ButtonStyleAccept;
                ButtonGreen.hover = ButtonStyleAcceptHover;
                ButtonGreen.active = ButtonStyleAccept;
                ButtonGreen.focused = ButtonStyleAccept;
                ButtonGreen.onNormal = ButtonStyleAccept;
                ButtonGreen.onHover = ButtonStyleAcceptHover;
                ButtonGreen.onActive = ButtonStyleAccept;
                ButtonGreen.onFocused = ButtonStyleAccept;

                // Setup Button Disabled
                ButtonRed = new GUIStyle(ButtonBase);
                ButtonStyleDisabled = new GUIStyleState() { background = ButtonTexDisabled, textColor = new Color(1, 1, 1, 1), };
                ButtonStyleDisabledHover = new GUIStyleState() { background = ButtonTexDisabledHover, textColor = new Color(1, 1, 1, 1), };
                ButtonRed.normal = ButtonStyleDisabled;
                ButtonRed.hover = ButtonStyleDisabledHover;
                ButtonRed.active = ButtonStyleDisabled;
                ButtonRed.focused = ButtonStyleDisabled;
                ButtonRed.onNormal = ButtonStyleDisabled;
                ButtonRed.onHover = ButtonStyleDisabledHover;
                ButtonRed.onActive = ButtonStyleDisabled;
                ButtonRed.onFocused = ButtonStyleDisabled;

                // Setup Button Not Active
                ButtonGrey = new GUIStyle(ButtonBase);
                ButtonStyleInactive = new GUIStyleState() { background = ButtonTexInactive, textColor = new Color(1, 1, 1, 1), };
                ButtonGrey.normal = ButtonStyleInactive;
                ButtonGrey.hover = ButtonStyleInactive;
                ButtonGrey.active = ButtonStyleInactive;
                ButtonGrey.focused = ButtonStyleInactive;
                ButtonGrey.onNormal = ButtonStyleInactive;
                ButtonGrey.onHover = ButtonStyleInactive;
                ButtonGrey.onActive = ButtonStyleInactive;
                ButtonGrey.onFocused = ButtonStyleInactive;


                // Setup Button Active
                ButtonBlueActive = new GUIStyle(ButtonBase);
                ButtonStyleActive = new GUIStyleState() { background = ButtonTexSelect, textColor = new Color(1, 1, 1, 1), };
                ButtonBlueActive.normal = ButtonStyleActive;
                ButtonBlueActive.hover = ButtonStyleActive;
                ButtonBlueActive.active = ButtonStyleActive;
                ButtonBlueActive.focused = ButtonStyleActive;
                ButtonBlueActive.onNormal = ButtonStyleActive;
                ButtonBlueActive.onHover = ButtonStyleActive;
                ButtonBlueActive.onActive = ButtonStyleActive;
                ButtonBlueActive.onFocused = ButtonStyleActive;

                // Setup Button Green Active
                ButtonGreenActive = new GUIStyle(ButtonBase);
                ButtonStyleGActive = new GUIStyleState() { background = ButtonTexSelectGreen, textColor = new Color(1, 1, 1, 1), };
                ButtonGreenActive.normal = ButtonStyleGActive;
                ButtonGreenActive.hover = ButtonStyleGActive;
                ButtonGreenActive.active = ButtonStyleGActive;
                ButtonGreenActive.focused = ButtonStyleGActive;
                ButtonGreenActive.onNormal = ButtonStyleGActive;
                ButtonGreenActive.onHover = ButtonStyleGActive;
                ButtonGreenActive.onActive = ButtonStyleGActive;
                ButtonGreenActive.onFocused = ButtonStyleGActive;

                // Setup Button Red Active
                ButtonRedActive = new GUIStyle(ButtonBase);
                ButtonStyleRActive = new GUIStyleState() { background = ButtonTexSelectRed, textColor = new Color(1, 1, 1, 1), };
                ButtonRedActive.normal = ButtonStyleRActive;
                ButtonRedActive.hover = ButtonStyleRActive;
                ButtonRedActive.active = ButtonStyleRActive;
                ButtonRedActive.focused = ButtonStyleRActive;
                ButtonRedActive.onNormal = ButtonStyleRActive;
                ButtonRedActive.onHover = ButtonStyleRActive;
                ButtonRedActive.onActive = ButtonStyleRActive;
                ButtonRedActive.onFocused = ButtonStyleRActive;

                Font idealFont = GUI.skin.font;
                try
                {
                    idealFont = Resources.FindObjectsOfTypeAll<Font>().ToList().Find(delegate (Font cand)
                    { return cand.name == "Exo-SemiBold"; });
                }
                catch { }
                MenuGUI = new GUISkin();
                MenuGUI.font = idealFont;
                MenuGUI.window = MenuLeft;

                MenuGUI.label = new GUIStyle(GUI.skin.label);
                MenuGUI.label.font = idealFont;
                MenuGUI.label.alignment = TextAnchor.MiddleLeft;
                MenuGUI.label.fontStyle = FontStyle.Normal;
                MenuGUI.label.normal.textColor = new Color(0.27f, 0.27f, 0.27f, 1);

                MenuGUI.button = ButtonBlue;
                MenuGUI.box = new GUIStyle(GUI.skin.box);
                MenuGUI.box.font = idealFont;
                MenuGUI.box.normal.textColor = new Color(0.27f, 0.27f, 0.27f, 1);
            }
        }

        private static GUISkin cache;
        private static Color cacheColor;
        private static Color GUIColor = new Color(1, 1, 1, UIAlpha);
        private static Color GUIColorSolid = new Color(1, 1, 1, 1);
        public static void StartUI()
        {
            FetchResourcesFromGame();
            cache = GUI.skin;
            cacheColor = GUI.color;
            GUI.skin = MenuGUI;
            GUI.color = GUIColor;
        }
        public static void StartUIOpaque()
        {
            FetchResourcesFromGame();
            cache = GUI.skin;
            cacheColor = GUI.color;
            GUI.skin = MenuGUI;
            GUI.color = GUIColorSolid;
        }
        public static void EndUI()
        {
            GUI.color = cacheColor;
            GUI.skin = cache;
        }



        /// <summary>
        /// For the Popups that appear like the BB sold thing
        /// </summary>
        static readonly FieldInfo 
            textInput = typeof(FloatingTextPanel).GetField("m_AmountText", BindingFlags.NonPublic | BindingFlags.Instance),
            listOverlays = typeof(ManOverlay).GetField("m_ActiveOverlays", BindingFlags.NonPublic | BindingFlags.Instance),
            rects = typeof(FloatingTextPanel).GetField("m_Rect", BindingFlags.NonPublic | BindingFlags.Instance),
            sScale = typeof(FloatingTextPanel).GetField("m_InitialScale", BindingFlags.NonPublic | BindingFlags.Instance),
            scale = typeof(FloatingTextPanel).GetField("m_scaler", BindingFlags.NonPublic | BindingFlags.Instance),
            canvas = typeof(FloatingTextPanel).GetField("m_CanvasGroup", BindingFlags.NonPublic | BindingFlags.Instance),
            CaseThis = typeof(ManOverlay).GetField("m_ConsumptionAddMoneyOverlayData", BindingFlags.NonPublic | BindingFlags.Instance);


        internal static GameObject CreateCustomPopupInfo(string name, Color colorToSet, out FloatingTextOverlayData CallToShow)
        {
            GameObject TextStor = new GameObject(name, typeof(RectTransform));
            RectTransform rTrans = TextStor.GetComponent<RectTransform>();
            Text texter = rTrans.gameObject.AddComponent<Text>();
            FloatingTextOverlayData refer = (FloatingTextOverlayData)CaseThis.GetValue(ManOverlay.inst);
            Text textRefer = (Text)textInput.GetValue(refer.m_PanelPrefab);

            texter.horizontalOverflow = HorizontalWrapMode.Overflow;
            texter.fontStyle = textRefer.fontStyle;
            texter.material = textRefer.material;
            texter.alignment = textRefer.alignment;
            texter.font = textRefer.font;
            texter.color = colorToSet;
            texter.fontSize = (int)((float)texter.fontSize * 2f);
            texter.SetAllDirty();

            FloatingTextPanel panel = TextStor.AddComponent<FloatingTextPanel>();
            CanvasGroup newCanvasG;
            try
            {
                CanvasGroup cG = (CanvasGroup)canvas.GetValue(refer.m_PanelPrefab);
                newCanvasG = rTrans.gameObject.AddComponent<CanvasGroup>();
                newCanvasG.alpha = 0.95f;
                newCanvasG.blocksRaycasts = false;
                newCanvasG.hideFlags = 0;
                newCanvasG.ignoreParentGroups = true;
                newCanvasG.interactable = false;
            }
            catch
            {
                Debug.Assert(true, "AltUI: FAILED to create modded PopupInfo extract!");
                CallToShow = null;
                return null;
            }

            canvas.SetValue(panel, newCanvasG);
            rects.SetValue(panel, rTrans);
            sScale.SetValue(panel, Vector3.one * 2.5f);
            scale.SetValue(panel, 2.5f);

            textInput.SetValue(panel, texter);

            CallToShow = TextStor.AddComponent<FloatingTextOverlayData>();
            CallToShow.m_HiddenInModes = new List<ManGameMode.GameType>
                {
                    ManGameMode.GameType.Attract,
                    ManGameMode.GameType.Gauntlet,
                    ManGameMode.GameType.SumoShowdown,
                };
            CallToShow.m_StayTime = refer.m_StayTime;
            CallToShow.m_FadeOutTime = refer.m_FadeOutTime;
            CallToShow.m_MaxCameraResizeDist = refer.m_MaxCameraResizeDist;
            CallToShow.m_HiddenInModes = refer.m_HiddenInModes;
            CallToShow.m_MinCameraResizeDist = refer.m_MinCameraResizeDist;
            CallToShow.m_CamResizeCurve = refer.m_CamResizeCurve;
            CallToShow.m_AboveDist = refer.m_AboveDist;
            CallToShow.m_PanelPrefab = panel;

            return TextStor;
        }

        internal static void PopupCustomInfo(string text, WorldPosition pos, FloatingTextOverlayData FTOD)
        {
            FloatingTextOverlay fOverlay = new FloatingTextOverlay(FTOD);

            fOverlay.Set(text, pos);

            FloatingTextOverlayData textCase = (FloatingTextOverlayData)CaseThis.GetValue(ManOverlay.inst);
            if (textCase.VisibleInCurrentMode && fOverlay != null)
            {
                List<Overlay> over = (List<Overlay>)listOverlays.GetValue(ManOverlay.inst);
                over.Add(fOverlay);
                listOverlays.SetValue(ManOverlay.inst, over);
            }
        }
    }
}
