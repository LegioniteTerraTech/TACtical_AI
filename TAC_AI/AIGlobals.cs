﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using TAC_AI.AI;
using TAC_AI.Templates;
using TerraTechETCUtil;

namespace TAC_AI
{
    /// <summary>
    /// Stores all global information for this mod. Edit at your own risk.
    /// </summary>
    public static class AIGlobals
    {
        public static bool AIAttract => ManGameMode.inst.GetCurrentGameType() != ManGameMode.GameType.Attract;

        private static FieldInfo getCamTank = typeof(TankCamera).GetField("m_hideHud", BindingFlags.NonPublic | BindingFlags.Instance);
        public static bool GetHideHud => (bool)getCamTank.GetValue(TankCamera.inst);
        public static bool HideHud = false;
        public static bool IsBlockAIAble(BlockTypes BT)
        {
            if (BT != BlockTypes.GSOAIController_111)
            {
                if (ManMods.inst.IsModdedBlock(BT))
                {
                    var block = ManSpawn.inst.GetBlockPrefab(BT);
                    if (block)
                    {
                        var AI = block.GetComponent<ModuleAIBot>();
                        if (AI && (AI.AITypesEnabled.Contains(TechAI.AITypes.Escort) ||
                            AI.AITypesEnabled.Contains(TechAI.AITypes.Guard)))
                            return true;
                    }
                }
                else
                {
                    var BA = ManSpawn.inst.GetBlockAttributes(BT);
                    if (BA.Contains(BlockAttributes.AI))
                        return true;
                }
            }
            return false;
        }
        public static bool IsBlockAIAble(string name)
        {
            return IsBlockAIAble(BlockIndexer.GetBlockIDLogFree(name));
        }
        public static bool IsTechAIAble(Tank tech)
        {
            if (tech != null)
            {
                return tech.GetHelperInsured().hasAI;
            }
            return false;
        }
        public static bool IsTechAIAble(ManSaveGame.StoredTech tech)
        {
            if (tech != null)
            {
                foreach (var item in tech.m_TechData.m_BlockSpecs)
                {
                    if (IsBlockAIAble(item.block))
                        return true;
                }
            }
            return false;
        }

        public const float SleepRangeSpacing = 16;
        public static bool IsInSleepRange(Vector3 posScene)
        {
            float sleepRange = (float)TankAIManager.rangeOverride.GetValue(ManTechs.inst);
            return !ManNetwork.IsNetworked &&
                (posScene - Singleton.cameraTrans.position).sqrMagnitude > 
                (sleepRange * sleepRange) - SleepRangeSpacing;
        }

        public const float EradicateEffectMaxDistanceSqr = 200 * 200;


        public static Rewired.Player controllerExt = null;

        public static bool PlayerFireCommand(int team)
        {
            if (ManNetwork.IsNetworked)
                return PlayerMPFireCommand(team);
            else
                return PlayerClientFireCommand();
        }
        public static bool PlayerClientFireCommand()
        {
            try
            {
                if (controllerExt == null)
                    controllerExt = Rewired.ReInput.players.GetPlayer(ManPlayer.inst.PlayerTeam);
                if (controllerExt != null && controllerExt.GetButton(2))
                    return true;
            }
            catch { }
            return false;
        }
        public static bool PlayerMPFireCommand(int Team)
        {
            if (ManNetwork.IsHost)
            {
                try
                {
                    for (int step = 0; step < ManNetwork.inst.GetNumPlayers(); step++)
                    {
                        NetPlayer NP = ManNetwork.inst.GetPlayer(step);
                        if (NP && NP.HasTech() && Team == NP.TechTeamID && NP.CurTech.tech.control.FireControl)
                            return NP.CurTech.tech.control.FireControl;
                    }
                }
                catch { }
            }
            return false;
        }
        public static bool TechIsMPPlayerControlled(this Tank tank)
        {
            try
            {
                if (ManNetwork.IsNetworked)
                    return ManNetwork.inst.GetAllPlayerTechs().Contains(tank);
            }
            catch { }
            return false;
        }


        // AIERepair contains the self-repair stats
        // EnemyWorldManager contains the unloaded enemy stats

        //-------------------------------------
        //              CONSTANTS
        //-------------------------------------
        // SPAWNING
        public const int SmolTechBlockThreshold = 24;
        public const int DefenderWeaponCount = 12;
        public const int HomingWeaponCount = 25;
        public const int BossTechSize = 150;
        public const int LethalTechSize = 256;
        public const int MaxEradicatorTechs = 2;
        public const int MaxBlockLimitAttract = 128;

        // GENERAL AI PARAMETERS
        public const float DefaultMaxObjectiveRange = 750;
        public const float TargetVelocityLeadPredictionMulti = 0.01f; // for projectiles of speed 100
        public const float StationaryMoveDampening = 6;
        public const int TeamRangeStart = 256;
        public const short NetAIClockPeriod = 30;

        public const float TargetCacheRefreshInterval = 1.5f;  // Seconds until we try to gather enemy Techs within range

        internal static GUIButtonMadness ModularMenu;
        private const int IDGUI = 8037315;
        private static GUI_BM_Element[] MenuButtons = new GUI_BM_Element[]
        {
            new GUI_BM_Element_Simple()
            {
                Name = "Bribe",
                OnIcon = null,
                OnDesc = () => {
                    return "Buy them out";
                },
                ClampSteps = 0,
                LastVal = 0,
                OnSet = (float in1) => {
                    if (GUINPTInteraction.lastTank)
                    {
                        int techCost = Mathf.RoundToInt(RawTechTemplate.GetBBCost(GUINPTInteraction.lastTank) * BribeMulti);
                        GUINPTInteraction.TryLoneCommand(ManNetwork.inst.MyPlayer, GUINPTInteraction.lastTank, 0);
                        ModularMenu.CloseGUI();
                    }
                    return 0;
                },
            },
            new GUI_BM_Element_Simple()
            {
                Name = "Info",
                OnIcon = null,
                OnDesc = () => {
                    return "Open details pane";
                },
                ClampSteps = 0,
                LastVal = 0,
                OnSet = (float in1) => {
                    GUINPTInteraction.LaunchSubMenuClickable();
                    ModularMenu.CloseGUI();
                    return 0;
                },
            },
            new GUI_BM_Element_Simple()
            {
                Name = "Insult",
                OnIcon = null,
                OnDesc = () => {
                    return "Anger and annoy them"; 
                },
                ClampSteps = 0,
                LastVal = 0,
                OnSet = (float in1) => {
                    if (GUINPTInteraction.lastTank)
                    {
                        GUINPTInteraction.TryLoneCommand(ManNetwork.inst.MyPlayer, GUINPTInteraction.lastTank, 0);
                        ModularMenu.CloseGUI();
                    }
                    return 0; 
                },
            },
            new GUI_BM_Element_Simple()
            {
                Name = "Missions",
                OnIcon = null,
                OnDesc = () => {
                    return "See what they want";
                },
                ClampSteps = 0,
                LastVal = 0,
                OnSet = (float in1) => {
                    if (GUINPTInteraction.lastTank)
                    {
                        int techCost = Mathf.RoundToInt(RawTechTemplate.GetBBCost(GUINPTInteraction.lastTank) * BribeMulti);
                        GUINPTInteraction.TryLoneCommand(ManNetwork.inst.MyPlayer, GUINPTInteraction.lastTank, 0);
                        ModularMenu.CloseGUI();
                    }
                    return 0;
                },
            },
        };

        internal static void InitSharedMenu()
        {
            if (ModularMenu != null)
                return;
            DebugTAC_AI.Log("AIGlobals.InitSharedMenu()");
            ModularMenu = GUIButtonMadness.Initiate(IDGUI, "ERROR", MenuButtons);
        }

        public static IntVector3 RTSDisabled => IntVector3.invalid;

        // Elevation
        public const float GroundOffsetGeneralAir = 10;
        public const float GroundOffsetRTSAir = 24;
        public const float GroundOffsetAircraft = 22;
        public const float GroundOffsetChopper = 13.5f;
        public const float GroundOffsetCrashWarnChopper = 11.5f;

        // Anchors
        public const float SafeAnchorDist = 50f;     // enemy too close to anchor
        /// <summary> How much do we dampen anchor movements by? </summary>
        public const int AnchorAimDampening = 45;
        public const short AlliedAnchorAttempts = 12;
        public const short NPTAnchorAttempts = 12;

        // Unjamming
        public const int UnjamUpdateStart = 120;
        public const int UnjamUpdateTicks = 120;
        public const int UnjamUpdateEndDelay = 20;

        public const int UnjamUpdateDrop = UnjamUpdateStart + UnjamUpdateTicks;
        public const int UnjamUpdateEnd = UnjamUpdateDrop + UnjamUpdateEndDelay;



        // Pathfinding
        internal static Bitfield<ObjectTypes> emptyBitMask = new Bitfield<ObjectTypes>();
        internal static Bitfield<ObjectTypes> blockBitMask = new Bitfield<ObjectTypes>(new ObjectTypes[1] { ObjectTypes.Block });
        internal static Bitfield<ObjectTypes> techBitMask = new Bitfield<ObjectTypes>(new ObjectTypes[1] { ObjectTypes.Vehicle });
        internal static Bitfield<ObjectTypes> sceneryBitMask = new Bitfield<ObjectTypes>(new ObjectTypes[1] { ObjectTypes.Scenery });
        internal static Bitfield<ObjectTypes> crashBitMask = new Bitfield<ObjectTypes>(new ObjectTypes[2] { ObjectTypes.Scenery, ObjectTypes.Vehicle });

        public const float AIPathingSuccessRad = 2.4f; // How far should the tech radius from the path point to consider finishing the path point?
        public const float AIPathingSuccessRadPrecise = 1.2f; // How far should the tech radius from the path point to consider finishing the path point?

        public const int PathfindingExtraSpace = 6;  // Extra pathfinding space
        public const float DefaultDodgeStrengthMultiplier = 1.75f;  // The motivation in trying to move away from a tech in the way
        public const float AirborneDodgeStrengthMultiplier = 0.4f;  // The motivation in trying to move away from a tech in the way
        public const float FindItemScanRangeExtension = 50;
        public const float FindBaseScanRangeExtension = 500;
        public const int ReverseDelay = 60;
        public const float PlayerAISpeedPanicDividend = 6;
        public const float EnemyAISpeedPanicDividend = 9;
        /// <summary>Depth that land Techs are able to drive into</summary>
        public const float WaterDepthTechHeightPercent = 0.35f;

        // Control the aircrafts and AI
        public const float AircraftPreCrashDetection = 1.6f;
        public const float AircraftDestSuccessRadius = 32;
        public const float AerofoilSluggishnessBaseValue = 30;
        public const float AircraftMaxDive = 0.6f;
        public const float AircraftDangerDive = 0.7f;
        public const float AircraftChillFactorMulti = 4.5f;         // More accuraccy, less responsiveness
        public const float LargeAircraftChillFactorMulti = 1.25f;   // More responsiveness, less accuraccy

        public const float AirNPTMaxHeightOffset = 125;     // How far the AI is allowed to go while in combat above the player
        public const float AirWanderMaxHeightIngame = 75;         // How far the AI is allowed to go while wandering randomly above the player
        public static float AirWanderMaxHeight => AIAttract ? 500 : AirWanderMaxHeightIngame;         // How far the AI is allowed to go while wandering randomly above the player
        public const float AirPromoteSpaceHeight = 150;     // The height the player, beyond passing, will encounter more spacecraft
        public const float AirMaxYaw = 0.2f; // 0 - 1 (float)
        public const float AirMaxYawBankOnly = 0.75f; // 0 - 1 (float)

        public const float ChopperOperatingExtraPower = 1.38f;
        public const float ChopperChillFactorMulti = 30f;

        public const float HovershipHorizontalDriveMulti = 1.25f;
        public const float HovershipUpDriveMulti = 1f;
        public const float HovershipDownDriveMulti = 0.6f;


        /// <summary> IN m/s !!!</summary>
        public const int LargeAircraftSize = 15;            // The size of which we count an aircraft as large
        public const float AirStallSpeed = 42;//25          // The speed of which most wings begin to stall at
        public const float GroundAttackStagingDistMain = 275;
        public static float GroundAttackStagingDist => AIAttract ? 120 : GroundAttackStagingDistMain;   // Distance to fly (in meters!) before turning back
        public const float TechSplitDelay = 0.5f;


        // Item Handling
        public const float MinimumCloseInSpeed = 1.6f;      // If we are closing in on our target slower than this (with wrong heading), we drive slowly
        public const float BlockAttachDelay = 0.75f;        // How long until we actually attach the block when playing the placement animation
        public const float MaxBlockGrabRange = 47.5f;       // Less than player range to compensate for precision
        public const float MaxBlockGrabRangeAlt = 5;        // Lowered range to allow scrap magnets to have a chance
        public const float ItemGrabStrength = 1750;         // The max acceleration to apply when holding an item
        public const float ItemThrowVelo = 115;             // The max velocity to apply when throwing an item
        public const float AircraftHailMaryRange = 65f;     // Try throw things this far away for aircraft 
        //  because we don't want to burn daylight trying to land and takeoff again

        // Charger Parameters
        public const float minimumChargeFractionToConsider = 0.75f;

        // Combat Parameters
        public const int DefaultMaxTargetingRange = 150;
        public const float MaxRangeFireAll = 125;   // WEAPON AIMING RANGE

        // Combat target switching
        public const int ProvokeTime = 200;         // Roughly around 200/40 = 5 seconds
        public const int ProvokeTimeShort = 80;
        public const int DamageAlertThreshold = 45;// Above this damage we react to the threat
        public const float ScanDelay = 0.5f;        // Seconds until we try to find a appropreate target
        public const float PestererSwitchDelay = 12.5f; // Seconds before Pesterers find a new random target



        // ENEMY AI PARAMETERS
        // Active Enemy AI Techs
        public const int DefaultEnemyScanRange = 150;
        public const int TileFringeDist = 96;
        public const float BatteryRetreatPercent = 0.25f;

        // Attack Detection/Chase ranges
        public const int DefaultEnemyMaxCombatRange = 150;
        public const int PassiveMaxCombatRange = 75;
        public const int BaseFounderMaxCombatRange = 60;     // 
        public const int BossMaxCombatRange = 250;        // 
        public const int InvaderMaxCombatRange = 250;        // 
        public const float SpyperMaxCombatRange = 175;    // 

        // Combat Minimum Spacing Ranges
        public const float MinCombatRangeDefault = 12;
        public const float MinCombatRangeSpyper = 60;
        public const float SpacingRangeSpyperAir = 72;
        public const float SpacingRangeAircraft = 24;
        public const float SpacingRangeChopper = 12;
        public const float SpacingRangeHoverer = 18;

        // Non-Player Base Checks
        public static bool StartingBasesAreAirdropped = false;
        public const float EnemyBaseMakerChance = 25;
        public const float StartBaseMinSpacing = 450;
        public static bool AllowInfAutominers = true;
        public static bool NoBuildWhileInCombat = true;
        public const int MinimumBBToTryExpand = 10000; // Before expanding
        public const int MinimumBBToTryBribe = 100000;
        public const float BribeMulti = 1.5f;
        public const int BaseExpandChance = 65;//18;
        public const int MinResourcesReqToCollect = 12;
        public const int EnemyBaseMiningMaxRange = 250;
        public const int EnemyExtendActionRangeShort = 500;
        public const int EnemyExtendActionRange = EnemyExtendActionRangeShort + 32; //the extra 32 to account for tech sizes
        public const float RetreatBelowTechDamageThreshold = 50;
        public const float RetreatBelowTeamDamageThreshold = 30;

        public const int MPEachBaseProfits = 250;
        public const float RaidCooldownTimeSecs = 1200;
        public const int IgnoreBaseCullingTilesFromOrigin = 8388607;

        public const float MaximumNeutralMonitorSqr = 75 ^ 2;//175

        // Colors
        internal static Color PlayerColor = new Color(0.5f, 0.75f, 0.95f, 1);
        // ENEMY BASE TEAMS
        internal static Color EnemyColor = new Color(0.95f, 0.1f, 0.1f, 1);

        internal static Color NeutralColor = new Color(0.5f, 0, 0.5f, 1);
        internal static Color FriendlyColor = new Color(0.2f, 0.95f, 0.2f, 1);


        /// <summary> increments NEGATIVELY </summary>
        public const int EnemyTeamsRangeStart = -1073741828;
                                               //2147483647 
        internal static bool IsAttract => ManGameMode.inst.GetCurrentGameType() == ManGameMode.GameType.Attract;
        public static float BaseChanceGoodMulti => 1 - ((KickStart.difficulty + 50) / 200f); // 25%
        public static float NonHostileBaseChance => 0.5f * BaseChanceGoodMulti; // 50% at easiest
        public static float FriendlyBaseChance => 0.25f * BaseChanceGoodMulti;  // 12.5% at easiest

        internal static bool TurboAICheat
        {
            get { return SpecialAISpawner.CreativeMode && Input.GetKey(KeyCode.RightControl) && Input.GetKey(KeyCode.Slash); }
        }


        // Utilities
        public static bool TechIsSafelyRemoveable(Tank tech)
        {
            if (!tech)
                return false;
            int team = tech.Team;
            return !IsPlayerTeam(team) && ManSpawn.NeutralTeam != team && !TankAIManager.MissionTechs.Contains(tech.visible.ID);
        }
        private static bool techSpawned = false;
        public static bool CanSplitTech(float delay = TechSplitDelay)
        {
            if (techSpawned)
                return false;
            techSpawned = true;
            InvokeHelper.InvokeSingle(ReAllowSplitTech, delay);
            return true;
        }
        private static void ReAllowSplitTech()
        {
            techSpawned = false;
        }
        internal static int SceneTechCount = -1;
        public static bool AtSceneTechMaxSpawnLimit()
        {
            if (SceneTechCount == -1)
            {
                try
                {
                    SceneTechCount = ManTechs.inst.IterateTechsWhere(x => TechIsSafelyRemoveable(x)).Count();
                }
                catch (Exception e)
                {
                    SceneTechCount = 0;
                    DebugTAC_AI.Log(KickStart.ModID + ": AtSceneTechMax() - Error on IterateTechs Fetch");
                    DebugTAC_AI.Log(e);
                }
            }
            return SceneTechCount >= KickStart.MaxEnemyWorldCapacity;
        }
        public static bool SceneTechMaxNeedsRemoval(out int needsRemovalCount)
        {
            if (SceneTechCount == -1)
            {
                try
                {
                    SceneTechCount = ManTechs.inst.IterateTechsWhere(x => TechIsSafelyRemoveable(x)).Count();
                }
                catch (Exception e)
                {
                    SceneTechCount = 0;
                    DebugTAC_AI.Log(KickStart.ModID + ": BeyondSceneTechMax() - Error on IterateTechs Fetch");
                    DebugTAC_AI.Log(e);
                }
            }
            int threshold = KickStart.MaxEnemyWorldCapacity + KickStart.ForceRemoveOverEnemyMaxCap;
            needsRemovalCount = Mathf.Max(0, SceneTechCount - threshold);
            return SceneTechCount >= threshold;
        }

        public static bool IsPlayerTeam(int team)
        {
            return ManNetwork.IsNetworked ? IsMPPlayerTeam(team) : ManPlayer.inst.PlayerTeam == team;
        }

        public static bool IsMPPlayerTeam(int team)
        {
            return ManSpawn.LobbyTeamIDFromTechTeamID(team) != int.MaxValue;
        }


        public static bool IsBaseTeam(int team)
        {
            return ManBaseTeams.IsBaseTeam(team);
        }
        public static NP_Types GetNPTTeamType(int team)
        {
            if (team == ManPlayer.inst.PlayerTeam)
                return NP_Types.Player;
            else if (IsBaseTeam(team))
            {
                if (IsEnemyBaseTeam(team))
                    return NP_Types.Enemy;
                else if (IsNeutralBaseTeam(team))
                    return NP_Types.Neutral;
                else if (IsNonAggressiveTeam(team))
                    return NP_Types.NonAggressive;
                else if (IsSubNeutralBaseTeam(team))
                    return NP_Types.SubNeutral;
                else
                    return NP_Types.Friendly;
            }
            else
                return NP_Types.NonNPT;
        }

        public static Func<int, bool> IsEnemyBaseTeam => ManBaseTeams.IsEnemyBaseTeam;
        public static Func<int, bool> IsNonAggressiveTeam => ManBaseTeams.IsNonAggressiveTeam;
        public static Func<int, bool> IsSubNeutralBaseTeam => ManBaseTeams.IsSubNeutralBaseTeam;
        public static Func<int, bool> IsNeutralBaseTeam => ManBaseTeams.IsNeutralBaseTeam;
        public static Func<int, bool> IsFriendlyBaseTeam => ManBaseTeams.IsFriendlyBaseTeam;


        public static int GetRandomBaseTeam(bool forceValidTeam = false)
        {
            if (DebugRawTechSpawner.CanOpenDebugSpawnMenu && !forceValidTeam)
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

            if (ManBaseTeams.inst.teams.Any() && UnityEngine.Random.Range(0, 1f) <= ManBaseTeams.percentChanceExisting)
                return ManBaseTeams.GetRandomExistingBaseTeam().teamID;
            else
            {
                if (UnityEngine.Random.Range(0f, 1f) <= NonHostileBaseChance)
                {
                    if (UnityEngine.Random.Range(0f, 1f) <= FriendlyBaseChance)
                        return GetRandomAllyBaseTeam();
                    else
                        return GetRandomNeutralBaseTeam();
                }
                return GetRandomEnemyBaseTeam();
            }
        }
        public static int GetRandomEnemyBaseTeam()
        {
            var teamInst = ManBaseTeams.GetNewBaseTeam();
            teamInst.relations = TeamRelations.Enemy;
            return teamInst.teamID;
        }
        public static int GetRandomSubNeutralBaseTeam()
        {
            var teamInst = ManBaseTeams.GetNewBaseTeam();
            teamInst.relations = TeamRelations.HoldFire;
            return teamInst.teamID;
        }
        public static int GetRandomNeutralBaseTeam()
        {
            var teamInst = ManBaseTeams.GetNewBaseTeam();
            teamInst.relations = TeamRelations.Neutral;
            return teamInst.teamID;
        }
        public static int GetRandomAllyBaseTeam()
        {
            var teamInst = ManBaseTeams.GetNewBaseTeam();
            teamInst.SetFriendly(ManPlayer.inst.PlayerTeam);
            return teamInst.teamID;
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
            //DebugTAC_AI.Log(KickStart.ModID + ": PopupAllyInfo - Threw popup \"" + text + "\"");
        }
    }
}
