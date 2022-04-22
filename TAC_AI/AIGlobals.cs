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
        public const float MinimumMonitorSpacingSqr = 122500;

        // GENERAL AI PARAMETERS
        public const float GeneralAirGroundOffset = 10;
        public const float AircraftGroundOffset = 18;
        public const float ChopperGroundOffset = 10;
        public const float SafeAnchorDist = 50f;     // enemy too close to anchor
        public const int TeamRangeStart = 256;
        public const short NetAIClockPeriod = 30;

        // Pathfinding
        public const float DodgeStrengthMultiplier = 1.75f;  // The motivation in trying to move away from a tech in the way
        public const float FindBaseExtension = 500;
        // Control the aircrafts and AI
        public const float AirMaxHeightOffset = 250;
        public const float AirMaxHeight = 150;
        public const float AirPromoteHeight = 200;


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
        public const float SpacingRangeHoverer = 18;

        // Enemy Base Checks
        public const int MinimumBBRequired = 10000; // Before expanding
        public const int MinimumStoredBeforeTryBribe = 100000;
        public const float BribePenalty = 1.5f;
        public const int BaseExpandChance = 65;//18;
        public const int MinResourcesReqToCollect = 50;
        public const int EnemyBaseMiningMaxRange = 250;
        public const int EnemyExtendActionRange = 500 + 32; //the extra 32 to account for tech sizes

        public const int MPEachBaseProfits = 25000;


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

        public static float BaseChanceGoodMulti => 1 - ((KickStart.difficulty + 50) / 200f); // 25%
        public static float NonHostileBaseChance => 0.5f * BaseChanceGoodMulti; // 50% at easiest
        public static float FriendlyBaseChance => 0.25f * BaseChanceGoodMulti;  // 12.5% at easiest




        // Utilities
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
