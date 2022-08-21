using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using TAC_AI.Templates;
using TerraTechETCUtil;

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
        public const float PlayerAISpeedPanicDividend = 6;
        public const float EnemyAISpeedPanicDividend = 9;

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

        internal static bool IsAttract => ManGameMode.inst.GetCurrentGameType() == ManGameMode.GameType.Attract;
        public static float BaseChanceGoodMulti => 1 - ((KickStart.difficulty + 50) / 200f); // 25%
        public static float NonHostileBaseChance => 0.5f * BaseChanceGoodMulti; // 50% at easiest
        public static float FriendlyBaseChance => 0.25f * BaseChanceGoodMulti;  // 12.5% at easiest

        internal static bool TurboAICheat
        {
            get { return SpecialAISpawner.CreativeMode && Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.Backspace); }
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




        private static bool playerSavedOver = false;
        private static FloatingTextOverlayData playerOverEdit;
        private static GameObject playerTextStor;
        //private static CanvasGroup playerCanGroup;
        internal static void PopupPlayerInfo(string text, WorldPosition pos)
        {
            // Big mess trying to get some hard-locked code working

            if (!playerSavedOver)
            {
                playerTextStor = AltUI.CreateCustomPopupInfo("NewTextPlayer", PlayerColor, out playerOverEdit);
                playerSavedOver = true;
            }

            AltUI.PopupCustomInfo(text, pos, playerOverEdit);
        }



        private static bool enemySavedOver = false;
        private static FloatingTextOverlayData enemyOverEdit;
        private static GameObject enemyTextStor;
        //private static CanvasGroup enemyCanGroup;
        internal static void PopupEnemyInfo(string text, WorldPosition pos)
        {
            if (!enemySavedOver)
            {
                enemyTextStor = AltUI.CreateCustomPopupInfo("NewTextEnemy", EnemyColor, out enemyOverEdit);
                enemySavedOver = true;
            }

            AltUI.PopupCustomInfo(text, pos, enemyOverEdit);
        }


        private static bool neutralSavedOver = false;
        private static FloatingTextOverlayData NeutralOverEdit;
        private static GameObject neutralTextStor;
        //private static CanvasGroup neutralCanGroup;
        internal static void PopupNeutralInfo(string text, WorldPosition pos)
        {
            if (!neutralSavedOver)
            {
                neutralTextStor = AltUI.CreateCustomPopupInfo("NewTextNeutral", NeutralColor, out NeutralOverEdit);
                neutralSavedOver = true;
            }
            AltUI.PopupCustomInfo(text, pos, NeutralOverEdit);
        }


        private static bool AllySavedOver = false;
        private static FloatingTextOverlayData AllyOverEdit;
        private static GameObject AllyTextStor;
        //private static CanvasGroup AllyCanGroup;
        internal static void PopupAllyInfo(string text, WorldPosition pos)
        {
            // Big mess trying to get some hard-locked code working

            if (!AllySavedOver)
            {
                AllyTextStor = AltUI.CreateCustomPopupInfo("NewTextAlly", FriendlyColor, out AllyOverEdit);
                AllySavedOver = true;
            }
            AltUI.PopupCustomInfo(text, pos, AllyOverEdit);
            //Debug.Log("TACtical_AI: PopupAllyInfo - Threw popup \"" + text + "\"");
        }
    }
}
