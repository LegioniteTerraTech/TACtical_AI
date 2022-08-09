using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using TAC_AI.Templates;

namespace TAC_AI
{
    /// <summary>
    /// Stores all global information for this mod. Edit at your own risk.
    /// </summary>
    public class AIGlobals
    {
        // AIERepair contains the self-repair stats
        // EnemyWorldManager contains the unloaded enemy stats

        //-------------------------------------
        //              CONSTANTS
        //-------------------------------------
        // SPAWNING
        public const int SmolTechBlockThreshold = 24;
        public const int HomingWeaponCount = 25;
        public const int BossTechSize = 150;
        public const int LethalTechSize = 256;
        public const int MaxEradicatorTechs = 2;
        public const int MaxBlockLimitAttract = 128;

        public const float MinimumBaseSpacing = 450;
        public const float MinimumMonitorSpacingSqr = 30625;//175

        // GENERAL AI PARAMETERS
        public const float RTSAirGroundOffset = 24;
        public const float GeneralAirGroundOffset = 10;
        public const float AircraftGroundOffset = 22;
        public const float ChopperGroundOffset = 12;
        public const float StationaryMoveDampening = 6;
        public const float SafeAnchorDist = 50f;     // enemy too close to anchor
        public const int TeamRangeStart = 256;
        public const short NetAIClockPeriod = 30;

        public const short AlliedAnchorAttempts = 12;
        public const short NPTAnchorAttempts = 12;


        // Pathfinding
        public const int ExtraSpace = 6;  // Extra pathfinding space
        public const float DefaultDodgeStrengthMultiplier = 1.75f;  // The motivation in trying to move away from a tech in the way
        public const float AirborneDodgeStrengthMultiplier = 0.4f;  // The motivation in trying to move away from a tech in the way
        public const float FindItemExtension = 50;
        public const float FindBaseExtension = 500;
        public const int ReverseDelay = 60;

        // Control the aircrafts and AI
        public const float AircraftPreCrashDetection = 1.6f;
        public const float AircraftDestSuccessRadius = 32;
        public const float AerofoilSluggishnessBaseValue = 30;
        public const float AircraftMaxDive = 0.6f;
        public const float AircraftDangerDive = 0.7f;
        public const float AircraftChillFactorMulti = 4.5f;         // More accuraccy, less responsiveness
        public const float LargeAircraftChillFactorMulti = 1.25f;   // More responsiveness, less accuraccy

        public const float AirNPTMaxHeightOffset = 275;
        public const float AirWanderMaxHeight = 225;
        public const float AirPromoteSpaceHeight = 200;
        public const float AirMaxYaw = 0.2f; // 0 - 1 (float)
        public const float AirMaxYawBankOnly = 0.75f; // 0 - 1 (float)

        public const float ChopperOperatingExtraHeight = 0.38f;
        public const float ChopperChillFactorMulti = 30f;


        /// <summary> IN m/s !!!</summary>
        public const int LargeAircraftSize = 15;            // The size of which we count an aircraft as large
        public const float AirStallSpeed = 42;//25          // The speed of which most wings begin to stall at
        public const float GroundAttackStagingDist = 225;   // Distance to fly (in meters!) before turning back


        // Item Handling
        public const float BlockAttachDelay = 0.75f;        // How long until we actually attach the block when playing the placement animation
        public const float MaxBlockGrabRange = 47.5f;       // Less than player range to compensate for precision
        public const float MaxBlockGrabRangeAlt = 5;        // Lowered range to allow scrap magnets to have a chance
        public const float ItemGrabStrength = 1750;         // The max acceleration to apply when holding an item
        public const float ItemThrowVelo = 115;             // The max velocity to apply when throwing an item
        public const float AircraftHailMaryRange = 65f;     // Try throw things this far away for aircraft 
        //  because we don't want to burn daylight trying to land and takeoff again

        // Charger Parameters
        public const float minimumChargeFractionToConsider = 0.75f;


        // ENEMY AI PARAMETERS
        // Active Enemy AI Techs
        public const int DefaultEnemyRange = 150;
        public const int TileFringeDist = 96;

        public const int ProvokeTime = 200;

        // Combat target switching
        public const int ScanDelay = 20;            // Frames until we try to find a appropreate target
        public const int PestererSwitchDelay = 500; // Frames before Pesterers find a new random target

        // Sight ranges
        public const float MaxRangeFireAll = 125;   // WEAPON AIMING RANGE
        public const int BaseFounderRange = 60;     // 
        public const int BossMaxRange = 250;        // 
        public const float SpyperMaxRange = 450;    // 

        // Combat Spacing Ranges
        public const float SpacingRange = 12;
        public const float SpacingRangeSpyper = 64;
        public const float SpacingRangeAircraft = 24;
        public const float SpacingRangeChopper = 12;
        public const float SpacingRangeHoverer = 18;

        // Enemy Base Checks
        public static bool AllowInfAutominers = true;
        public const int MinimumBBRequired = 10000; // Before expanding
        public const int MinimumStoredBeforeTryBribe = 100000;
        public const float BribePenalty = 1.5f;
        public const int BaseExpandChance = 65;//18;
        public const int MinResourcesReqToCollect = 50;
        public const int EnemyBaseMiningMaxRange = 250;
        public const int EnemyExtendActionRange = 500 + 32; //the extra 32 to account for tech sizes

        public const int MPEachBaseProfits = 250;
        public const float RaidCooldownTimeSecs = 1200;
        public const int IgnoreBaseCullingTilesFromOrigin = 8388607;


        internal static Color PlayerColor = new Color(0.5f, 0.75f, 0.95f, 1);
        // ENEMY BASE TEAMS
        internal static Color EnemyColor = new Color(0.95f, 0.1f, 0.1f, 1);

        public const int EnemyBaseTeamsStart = 256;
        public const int EnemyBaseTeamsEnd = 356;

        public const int SubNeutralBaseTeamsStart = 357;
        public const int SubNeutralBaseTeamsEnd = 406;

        internal static Color NeutralColor = new Color(0.5f, 0, 0.5f, 1);
        public const int NeutralBaseTeamsStart = 407;
        public const int NeutralBaseTeamsEnd = 456;

        internal static Color FriendlyColor = new Color(0.2f, 0.95f, 0.2f, 1);
        public const int FriendlyBaseTeamsStart = 457;
        public const int FriendlyBaseTeamsEnd = 506;

        public const int BaseTeamsStart = 256;
        public const int BaseTeamsEnd = 506;

        internal static bool IsAttract => ManGameMode.inst.IsCurrent<ModeAttract>();
        public static float BaseChanceGoodMulti => 1 - ((KickStart.difficulty + 50) / 200f); // 25%
        public static float NonHostileBaseChance => 0.5f * BaseChanceGoodMulti; // 50% at easiest
        public static float FriendlyBaseChance => 0.25f * BaseChanceGoodMulti;  // 12.5% at easiest

        internal static bool TurboAICheat
        {
            get { return SpecialAISpawner.CreativeMode && Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.Backspace); }
        }


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

        public static void FetchResourcesFromGame()
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
                    DebugTAC_AI.Assert(true, "TACtical_AI: AIGlobals - failed to fetch textures");
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
                MenuLeftStyle = new GUIStyleState() { background = MenuTexRect, textColor = new Color(0, 0, 0, 1),}; 
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

                MenuGUI.button = ButtonBlue;
                MenuGUI.box = new GUIStyle(GUI.skin.box);
                MenuGUI.box.font = idealFont;
            }
        }

        private static GUISkin cache;
        private static Color cacheColor;
        private static Color GUIColor = new Color(1,1,1, UIAlpha);
        public static void StartUI()
        {
            cache = GUI.skin;
            cacheColor = GUI.color;
            GUI.skin = MenuGUI;
            GUI.color = GUIColor;
        }
        public static void EndUI()
        {
            GUI.color = cacheColor;
            GUI.skin = cache;
        }

        // Utilities
        public static bool AtSceneTechMax()
        {
            int Counter = 0;
            try
            {
                foreach (var tech in Singleton.Manager<ManTechs>.inst.IterateTechs())
                {
                    if (IsBaseTeam(tech.Team) || tech.Team == -1 || (tech.Team >= 1 && tech.Team <= 24))
                        Counter++;
                }
            }
            catch (Exception e)
            {
                DebugTAC_AI.Log("TACtical_AI: AtSceneTechMax - Error on IterateTechs Fetch");
                DebugTAC_AI.Log(e);
            }
            return Counter >= KickStart.MaxEnemyWorldCapacity;
        }

        public static bool IsBaseTeam(int team)
        {
            return (team >= BaseTeamsStart && team <= BaseTeamsEnd) || team == SpecialAISpawner.trollTeam;
        }

        public static bool IsEnemyBaseTeam(int team)
        {
            return (team >= EnemyBaseTeamsStart && team <= EnemyBaseTeamsEnd) || team == SpecialAISpawner.trollTeam;
        }
        public static bool IsNonAggressiveTeam(int team)
        {
            return team >= SubNeutralBaseTeamsStart && team <= NeutralBaseTeamsEnd;
        }
        public static bool IsSubNeutralBaseTeam(int team)
        {
            return team >= SubNeutralBaseTeamsStart && team <= SubNeutralBaseTeamsEnd;
        }
        public static bool IsNeutralBaseTeam(int team)
        {
            return team >= NeutralBaseTeamsStart && team <= NeutralBaseTeamsEnd;
        }
        public static bool IsFriendlyBaseTeam(int team)
        {
            return team >= FriendlyBaseTeamsStart && team <= FriendlyBaseTeamsEnd;
        }

        public static int GetRandomBaseTeam()
        {
            if (DebugRawTechSpawner.IsCurrentlyEnabled)
            {
                bool shift = Input.GetKey(KeyCode.LeftShift);
                bool ctrl = Input.GetKey(KeyCode.LeftControl);
                if (ctrl)
                {
                    if (shift)
                        return ManSpawn.FirstEnemyTeam;
                    else
                        return GetRandomAllyBaseTeam();
                }
                else if (shift)
                    return GetRandomNeutralBaseTeam();
            }

            if (UnityEngine.Random.Range(0f, 1f) <= NonHostileBaseChance)
            {
                if (UnityEngine.Random.Range(0f, 1f) <= FriendlyBaseChance)
                    return GetRandomAllyBaseTeam();
                else
                    return GetRandomNeutralBaseTeam();
            }
            return GetRandomEnemyBaseTeam();
        }
        public static int GetRandomEnemyBaseTeam()
        {
            return UnityEngine.Random.Range(EnemyBaseTeamsStart, EnemyBaseTeamsEnd);
        }
        public static int GetRandomSubNeutralBaseTeam()
        {
            return UnityEngine.Random.Range(SubNeutralBaseTeamsStart, SubNeutralBaseTeamsEnd);
        }
        public static int GetRandomNeutralBaseTeam()
        {
            return UnityEngine.Random.Range(NeutralBaseTeamsStart, NeutralBaseTeamsEnd);
        }
        public static int GetRandomAllyBaseTeam()
        {
            return UnityEngine.Random.Range(FriendlyBaseTeamsStart, FriendlyBaseTeamsEnd);
        }



        //static readonly FieldInfo panelData = typeof(FloatingTextOverlay).GetField("m_Data", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo textInput = typeof(FloatingTextPanel).GetField("m_AmountText", BindingFlags.NonPublic | BindingFlags.Instance);

        static readonly FieldInfo listOverlays = typeof(ManOverlay).GetField("m_ActiveOverlays", BindingFlags.NonPublic | BindingFlags.Instance);

        static readonly FieldInfo rects = typeof(FloatingTextPanel).GetField("m_Rect", BindingFlags.NonPublic | BindingFlags.Instance);

        static readonly FieldInfo sScale = typeof(FloatingTextPanel).GetField("m_InitialScale", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo scale = typeof(FloatingTextPanel).GetField("m_scaler", BindingFlags.NonPublic | BindingFlags.Instance);

        static readonly FieldInfo canvas = typeof(FloatingTextPanel).GetField("m_CanvasGroup", BindingFlags.NonPublic | BindingFlags.Instance);

        static readonly FieldInfo CaseThis = typeof(ManOverlay).GetField("m_ConsumptionAddMoneyOverlayData", BindingFlags.NonPublic | BindingFlags.Instance);


        private static bool playerSavedOver = false;
        private static FloatingTextOverlayData playerOverEdit;
        private static GameObject playerTextStor;
        private static CanvasGroup playerCanGroup;
        internal static void PopupPlayerInfo(string text, WorldPosition pos)
        {
            // Big mess trying to get some hard-locked code working

            if (!playerSavedOver)
            {
                playerTextStor = new GameObject("NewTextPlayer", typeof(RectTransform));
                RectTransform rTrans = playerTextStor.GetComponent<RectTransform>();
                Text texter = rTrans.gameObject.AddComponent<Text>();
                FloatingTextOverlayData refer = (FloatingTextOverlayData)CaseThis.GetValue(ManOverlay.inst);
                Text textRefer = (Text)textInput.GetValue(refer.m_PanelPrefab);

                texter.horizontalOverflow = HorizontalWrapMode.Overflow;
                texter.fontStyle = textRefer.fontStyle;
                texter.material = textRefer.material;
                texter.alignment = textRefer.alignment;
                texter.font = textRefer.font;
                texter.color = PlayerColor;
                texter.fontSize = (int)((float)texter.fontSize * 2f);
                texter.SetAllDirty();

                FloatingTextPanel panel = playerTextStor.AddComponent<FloatingTextPanel>();

                try
                {
                    CanvasGroup cG = (CanvasGroup)canvas.GetValue(refer.m_PanelPrefab);
                    playerCanGroup = rTrans.gameObject.AddComponent<CanvasGroup>();
                    playerCanGroup.alpha = 0.95f;
                    playerCanGroup.blocksRaycasts = false;
                    playerCanGroup.hideFlags = 0;
                    playerCanGroup.ignoreParentGroups = true;
                    playerCanGroup.interactable = false;
                }
                catch { }

                canvas.SetValue(panel, playerCanGroup);
                rects.SetValue(panel, rTrans);
                sScale.SetValue(panel, Vector3.one * 2.5f);
                scale.SetValue(panel, 2.5f);

                textInput.SetValue(panel, texter);

                playerOverEdit = playerTextStor.AddComponent<FloatingTextOverlayData>();
                playerOverEdit.m_HiddenInModes = new List<ManGameMode.GameType>
                {
                    ManGameMode.GameType.Attract,
                    ManGameMode.GameType.Gauntlet,
                    ManGameMode.GameType.SumoShowdown,
                };
                playerOverEdit.m_StayTime = refer.m_StayTime;
                playerOverEdit.m_FadeOutTime = refer.m_FadeOutTime;
                playerOverEdit.m_MaxCameraResizeDist = refer.m_MaxCameraResizeDist;
                playerOverEdit.m_HiddenInModes = refer.m_HiddenInModes;
                playerOverEdit.m_MinCameraResizeDist = refer.m_MinCameraResizeDist;
                playerOverEdit.m_CamResizeCurve = refer.m_CamResizeCurve;
                playerOverEdit.m_AboveDist = refer.m_AboveDist;
                playerOverEdit.m_PanelPrefab = panel;

                playerSavedOver = true;
            }

            FloatingTextOverlay fOverlay = new FloatingTextOverlay(playerOverEdit);

            fOverlay.Set(text, pos);

            FloatingTextOverlayData textCase = (FloatingTextOverlayData)CaseThis.GetValue(ManOverlay.inst);
            if (textCase.VisibleInCurrentMode && fOverlay != null)
            {
                List<Overlay> over = (List<Overlay>)listOverlays.GetValue(ManOverlay.inst);
                over.Add(fOverlay);
                listOverlays.SetValue(ManOverlay.inst, over);
            }
        }



        private static bool enemySavedOver = false;
        private static FloatingTextOverlayData enemyOverEdit;
        private static GameObject enemyTextStor;
        private static CanvasGroup enemyCanGroup;
        internal static void PopupEnemyInfo(string text, WorldPosition pos)
        {
            // Big mess trying to get some hard-locked code working

            if (!enemySavedOver)
            {
                enemyTextStor = new GameObject("NewTextEnemy", typeof(RectTransform));

                RectTransform rTrans = enemyTextStor.GetComponent<RectTransform>();
                Text texter = rTrans.gameObject.AddComponent<Text>();
                FloatingTextOverlayData refer = (FloatingTextOverlayData)CaseThis.GetValue(ManOverlay.inst);
                Text textRefer = (Text)textInput.GetValue(refer.m_PanelPrefab);

                texter.horizontalOverflow = HorizontalWrapMode.Overflow;
                texter.fontStyle = textRefer.fontStyle;
                texter.material = textRefer.material;
                texter.alignment = textRefer.alignment;
                texter.font = textRefer.font;
                texter.color = EnemyColor;
                texter.fontSize = (int)((float)texter.fontSize * 2f);
                texter.SetAllDirty();

                FloatingTextPanel panel = enemyTextStor.AddComponent<FloatingTextPanel>();

                try
                {
                    CanvasGroup cG = (CanvasGroup)canvas.GetValue(refer.m_PanelPrefab);
                    enemyCanGroup = rTrans.gameObject.AddComponent<CanvasGroup>();
                    enemyCanGroup.alpha = 0.95f;
                    enemyCanGroup.blocksRaycasts = false;
                    enemyCanGroup.hideFlags = 0;
                    enemyCanGroup.ignoreParentGroups = true;
                    enemyCanGroup.interactable = false;
                }
                catch { }

                canvas.SetValue(panel, enemyCanGroup);
                rects.SetValue(panel, rTrans);
                sScale.SetValue(panel, Vector3.one * 2.5f);
                scale.SetValue(panel, 2.5f);

                textInput.SetValue(panel, texter);

                enemyOverEdit = enemyTextStor.AddComponent<FloatingTextOverlayData>();
                enemyOverEdit.m_HiddenInModes = new List<ManGameMode.GameType>
                {
                    ManGameMode.GameType.Attract,
                    ManGameMode.GameType.Gauntlet,
                    ManGameMode.GameType.SumoShowdown,
                };
                enemyOverEdit.m_StayTime = refer.m_StayTime;
                enemyOverEdit.m_FadeOutTime = refer.m_FadeOutTime;
                enemyOverEdit.m_MaxCameraResizeDist = refer.m_MaxCameraResizeDist;
                enemyOverEdit.m_HiddenInModes = refer.m_HiddenInModes;
                enemyOverEdit.m_MinCameraResizeDist = refer.m_MinCameraResizeDist;
                enemyOverEdit.m_CamResizeCurve = refer.m_CamResizeCurve;
                enemyOverEdit.m_AboveDist = refer.m_AboveDist;
                enemyOverEdit.m_PanelPrefab = panel;

                enemySavedOver = true;
            }

            FloatingTextOverlay fOverlay = new FloatingTextOverlay(enemyOverEdit);

            fOverlay.Set(text, pos);

            FloatingTextOverlayData textCase = (FloatingTextOverlayData)CaseThis.GetValue(ManOverlay.inst);
            if (textCase.VisibleInCurrentMode && fOverlay != null)
            {
                List<Overlay> over = (List<Overlay>)listOverlays.GetValue(ManOverlay.inst);
                over.Add(fOverlay);
                listOverlays.SetValue(ManOverlay.inst, over);
            }
        }


        private static bool neutralSavedOver = false;
        private static FloatingTextOverlayData NeutralOverEdit;
        private static GameObject neutralTextStor;
        private static CanvasGroup neutralCanGroup;
        internal static void PopupNeutralInfo(string text, WorldPosition pos)
        {
            // Big mess trying to get some hard-locked code working

            if (!neutralSavedOver)
            {
                neutralTextStor = new GameObject("NewTextNeutral", typeof(RectTransform));

                RectTransform rTrans = neutralTextStor.GetComponent<RectTransform>();
                Text texter = rTrans.gameObject.AddComponent<Text>();
                FloatingTextOverlayData refer = (FloatingTextOverlayData)CaseThis.GetValue(ManOverlay.inst);
                Text textRefer = (Text)textInput.GetValue(refer.m_PanelPrefab);

                texter.horizontalOverflow = HorizontalWrapMode.Overflow;
                texter.fontStyle = textRefer.fontStyle;
                texter.material = textRefer.material;
                texter.alignment = textRefer.alignment;
                texter.font = textRefer.font;
                texter.color = NeutralColor;
                texter.fontSize = (int)((float)texter.fontSize * 2f);
                texter.SetAllDirty();

                FloatingTextPanel panel = neutralTextStor.AddComponent<FloatingTextPanel>();

                try
                {
                    CanvasGroup cG = (CanvasGroup)canvas.GetValue(refer.m_PanelPrefab);
                    neutralCanGroup = rTrans.gameObject.AddComponent<CanvasGroup>();
                    neutralCanGroup.alpha = 0.95f;
                    neutralCanGroup.blocksRaycasts = false;
                    neutralCanGroup.hideFlags = 0;
                    neutralCanGroup.ignoreParentGroups = true;
                    neutralCanGroup.interactable = false;
                }
                catch { }

                canvas.SetValue(panel, neutralCanGroup);
                rects.SetValue(panel, rTrans);
                sScale.SetValue(panel, Vector3.one * 2.5f);
                scale.SetValue(panel, 2.5f);

                textInput.SetValue(panel, texter);

                NeutralOverEdit = neutralTextStor.AddComponent<FloatingTextOverlayData>();
                NeutralOverEdit.m_HiddenInModes = new List<ManGameMode.GameType>
                {
                    ManGameMode.GameType.Attract,
                    ManGameMode.GameType.Gauntlet,
                    ManGameMode.GameType.SumoShowdown,
                };
                NeutralOverEdit.m_StayTime = refer.m_StayTime;
                NeutralOverEdit.m_FadeOutTime = refer.m_FadeOutTime;
                NeutralOverEdit.m_MaxCameraResizeDist = refer.m_MaxCameraResizeDist;
                NeutralOverEdit.m_HiddenInModes = refer.m_HiddenInModes;
                NeutralOverEdit.m_MinCameraResizeDist = refer.m_MinCameraResizeDist;
                NeutralOverEdit.m_CamResizeCurve = refer.m_CamResizeCurve;
                NeutralOverEdit.m_AboveDist = refer.m_AboveDist;
                NeutralOverEdit.m_PanelPrefab = panel;

                neutralSavedOver = true;
            }

            FloatingTextOverlay fOverlay = new FloatingTextOverlay(NeutralOverEdit);

            fOverlay.Set(text, pos);

            FloatingTextOverlayData textCase = (FloatingTextOverlayData)CaseThis.GetValue(ManOverlay.inst);
            if (textCase.VisibleInCurrentMode && fOverlay != null)
            {
                List<Overlay> over = (List<Overlay>)listOverlays.GetValue(ManOverlay.inst);
                over.Add(fOverlay);
                listOverlays.SetValue(ManOverlay.inst, over);
            }
        }


        private static bool AllySavedOver = false;
        private static FloatingTextOverlayData AllyOverEdit;
        private static GameObject AllyTextStor;
        private static CanvasGroup AllyCanGroup;
        internal static void PopupAllyInfo(string text, WorldPosition pos)
        {
            // Big mess trying to get some hard-locked code working

            if (!AllySavedOver)
            {
                AllyTextStor = new GameObject("NewTextAlly", typeof(RectTransform));

                RectTransform rTrans = AllyTextStor.GetComponent<RectTransform>();
                Text texter = rTrans.gameObject.AddComponent<Text>();
                FloatingTextOverlayData refer = (FloatingTextOverlayData)CaseThis.GetValue(ManOverlay.inst);
                Text textRefer = (Text)textInput.GetValue(refer.m_PanelPrefab);

                texter.horizontalOverflow = HorizontalWrapMode.Overflow;
                texter.fontStyle = textRefer.fontStyle;
                texter.material = textRefer.material;
                texter.alignment = textRefer.alignment;
                texter.font = textRefer.font;
                texter.color = FriendlyColor;
                texter.fontSize = (int)((float)texter.fontSize * 2f);
                texter.SetAllDirty();

                FloatingTextPanel panel = AllyTextStor.AddComponent<FloatingTextPanel>();

                try
                {
                    CanvasGroup cG = (CanvasGroup)canvas.GetValue(refer.m_PanelPrefab);
                    AllyCanGroup = rTrans.gameObject.AddComponent<CanvasGroup>();
                    AllyCanGroup.alpha = 0.95f;
                    AllyCanGroup.blocksRaycasts = false;
                    AllyCanGroup.hideFlags = 0;
                    AllyCanGroup.ignoreParentGroups = true;
                    AllyCanGroup.interactable = false;
                }
                catch { }

                canvas.SetValue(panel, AllyCanGroup);
                rects.SetValue(panel, rTrans);
                sScale.SetValue(panel, Vector3.one * 2.5f);
                scale.SetValue(panel, 2.5f);

                textInput.SetValue(panel, texter);

                AllyOverEdit = AllyTextStor.AddComponent<FloatingTextOverlayData>();
                AllyOverEdit.m_HiddenInModes = new List<ManGameMode.GameType>
                {
                    ManGameMode.GameType.Attract,
                    ManGameMode.GameType.Gauntlet,
                    ManGameMode.GameType.SumoShowdown,
                };
                AllyOverEdit.m_StayTime = refer.m_StayTime;
                AllyOverEdit.m_FadeOutTime = refer.m_FadeOutTime;
                AllyOverEdit.m_MaxCameraResizeDist = refer.m_MaxCameraResizeDist;
                AllyOverEdit.m_HiddenInModes = refer.m_HiddenInModes;
                AllyOverEdit.m_MinCameraResizeDist = refer.m_MinCameraResizeDist;
                AllyOverEdit.m_CamResizeCurve = refer.m_CamResizeCurve;
                AllyOverEdit.m_AboveDist = refer.m_AboveDist;
                AllyOverEdit.m_PanelPrefab = panel;

                AllySavedOver = true;
            }

            FloatingTextOverlay fOverlay = new FloatingTextOverlay(AllyOverEdit);

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
    }
}
