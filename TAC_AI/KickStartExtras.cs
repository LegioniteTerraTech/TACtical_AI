using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
//using Harmony;
using HarmonyLib;
using UnityEngine;
using TAC_AI.AI;
using TAC_AI.AI.Enemy;
using TAC_AI.Templates;
using TAC_AI.World;
using TAC_AI.AI.Movement;
using SafeSaves;
using TerraTechETCUtil;

#if !STEAM
using ModHelper.Config;
#else
using ModHelper;
#endif
using Nuterra.NativeOptions;


namespace TAC_AI
{
    public class KickStartConfigHelper
    {
        public static bool CullFarEnemyBases = false;
        internal static void PushExtModConfigHandlingConfigOnly()
        {
            KickStart.SavedDefaultEnemyFragility = Globals.inst.moduleDamageParams.detachMeterFillFactor;

            PushExtModConfigSetup();

            OverrideManPop.ChangeToRagnarokPop(KickStart.CommitDeathMode);
        }
        internal static ModConfig PushExtModConfigSetup()
        {
            ModConfig thisModConfig = new ModConfig();
            if (!thisModConfig.ReadConfigJsonFile())
            {
                LoadingHintsExt.MandatoryHints.Add(AltUI.ObjectiveString("Advanced AI") + " adds multiple layers of depth and " +
                AltUI.EnemyString("difficulty") + " to TerraTech.  " +
                AltUI.HintString("Best suited for prospecting veterans looking for a new challenge."));
            }
            thisModConfig.BindConfig<KickStart>(null, "EnableBetterAI");
            thisModConfig.BindConfig<KickStart>(null, "RetreatHotkeySav");
            thisModConfig.BindConfig<KickStart>(null, "AIDodgeCheapness");
            thisModConfig.BindConfig<KickStart>(null, "AIClockPeriodSet");
            thisModConfig.BindConfig<AIEPathMapper>(null, "PathRequestsToCalcPerFrame");
            thisModConfig.BindConfig<KickStart>(null, "MuteNonPlayerRacket");
            thisModConfig.BindConfig<KickStart>(null, "enablePainMode");
            thisModConfig.BindConfig<KickStart>(null, "DisplayEnemyEvents");
            thisModConfig.BindConfig<RawTechExporter>(null, "ExportJSONInsteadOfRAWTECH");
            thisModConfig.BindConfig<KickStart>(null, "difficulty");
            thisModConfig.BindConfig<KickStart>(null, "AllowAISelfRepair");
            thisModConfig.BindConfig<KickStart>(null, "AllowAISelfRepairInMP");
            thisModConfig.BindConfig<KickStart>(null, "LandEnemyOverrideChanceSav");
            thisModConfig.BindConfig<KickStart>(null, "EnemyBlockDropChance");
            thisModConfig.BindConfig<KickStart>(null, "EnemyEradicators");
            thisModConfig.BindConfig<KickStart>(null, "EnemiesHaveCreativeInventory");
            thisModConfig.BindConfig<AIGlobals>(null, "EnemyBaseMakerChance");
            thisModConfig.BindConfig<ManBaseTeams>(null, "PercentChanceExisting");
            thisModConfig.BindConfig<AIGlobals>(null, "AttackableNeutralBaseChanceMulti");
            thisModConfig.BindConfig<AIGlobals>(null, "FriendlyBaseChanceMulti");
            thisModConfig.BindConfig<KickStart>(null, "ActiveSpawnFoundersOffScene");
            thisModConfig.BindConfig<KickStart>(null, "SpawnFoundersPositional");
            thisModConfig.BindConfig<KickStart>(null, "MaxEnemyBaseLimit");
            thisModConfig.BindConfig<KickStart>(null, "MaxBasesPerTeam");
            thisModConfig.BindConfig<KickStart>(null, "AllowSeaEnemiesToSpawn");
            thisModConfig.BindConfig<KickStart>(null, "AirEnemiesSpawnRate");
            thisModConfig.BindConfig<KickStart>(null, "DesignsToLog");
            thisModConfig.BindConfig<KickStart>(null, "AIPopMaxLimit");
            thisModConfig.BindConfig<KickStart>(null, "TryForceOnlyPlayerSpawns");
            thisModConfig.BindConfig<KickStart>(null, "CommitDeathMode");
            thisModConfig.BindConfig<KickStart>(null, "CatMode");
            thisModConfig.BindConfig<KickStart>(null, "EnemySellGainModifier");
            thisModConfig.BindConfig<KickStartConfigHelper>(null, "CullFarEnemyBases");
            thisModConfig.BindConfig<KickStart>(null, "CullFarEnemyBasesMode");
            thisModConfig.BindConfig<KickStart>(null, "ForceRemoveOverEnemyMaxCap");
            thisModConfig.BindConfig<KickStart>(null, "EnemyBaseUpdateMode");

            // RTS
            thisModConfig.BindConfig<KickStart>(null, "AllowPlayerRTSHUD");
            thisModConfig.BindConfig<KickStart>(null, "AllowStrategicAI");
            thisModConfig.BindConfig<KickStart>(null, "UseClassicRTSControls");
            thisModConfig.BindConfig<KickStart>(null, "UseNumpadForGrouping");
            thisModConfig.BindConfig<KickStart>(null, "CommandHotkeySav");
            thisModConfig.BindConfig<KickStart>(null, "CommandBoltsHotkeySav");
            thisModConfig.BindConfig<KickStart>(null, "MultiSelectKeySav");
            thisModConfig.BindConfig<KickStart>(null, "ModeSelectKeySav");

            KickStart.RetreatHotkey = (KeyCode)KickStart.RetreatHotkeySav;
            KickStart.CommandHotkey = (KeyCode)KickStart.CommandHotkeySav;
            KickStart.CommandBoltsHotkey = (KeyCode)KickStart.CommandBoltsHotkeySav;
            KickStart.MultiSelect = (KeyCode)KickStart.MultiSelectKeySav;
            if (CullFarEnemyBases)
            {
                KickStart.CullFarEnemyBasesMode = 2;
                CullFarEnemyBases = false;
            }
            return thisModConfig;
        }
        internal static void PushExtModConfigHandling()
        {
            KickStart.SavedDefaultEnemyFragility = Globals.inst.moduleDamageParams.detachMeterFillFactor;

            var thisModConfig = PushExtModConfigSetup();
            try
            {
                KickStartNativeOptions.PushExtModOptionsHandling(thisModConfig);
            }
            catch (Exception e)
            {
                throw new Exception("PushExtModOptionsHandling within PushExtModConfigHandling hit exception - ", e);
            }
            finally
            {
                OverrideManPop.ChangeToRagnarokPop(KickStart.CommitDeathMode);
            }
        }
    }

    public struct BaseTeamsUpdateRate
    {
        public string name;
        public BaseTeamsUpdateRate(string name)
        {
            this.name = name;
        }
        public override string ToString() => name;
    }
    public class KickStartNativeOptions
    {
        // NativeOptions Parameters
        public static OptionKey retreatHotkey;
        public static OptionKey commandHotKey;
        public static OptionKey commandBoltsHotKey;
        public static OptionKey groupSelectKey;
        public static OptionKey modeSelectKey;
        public static OptionKey nptInteractKey;
        public static OptionToggle commandClassic;
        public static OptionToggle betterAI;
        public static OptionToggle aiSelfRepair;
        public static OptionToggle aiSelfRepair2;
        public static OptionRange dodgePeriod;
        public static OptionRange aiUpkeepRefresh; //AIClockPeriod
        public static OptionRange aiPathing;
        public static OptionToggle muteNonPlayerBuildRacket;
        public static OptionToggle allowOverLevelBlocksDrop;
        public static OptionToggle exportReadableRAW;
        public static OptionToggle displayEvents;
        public static OptionToggle painfulEnemies;
        public static OptionRange diff;
        public static OptionRange landEnemyChangeChance;
        public static OptionRange blockRecoveryChance;
        public static OptionToggle permitEradication;
        public static OptionToggle infEnemySupplies;
        public static OptionRange founderOnScene;
        public static OptionToggle founderAlwaysUnloaded;
        public static OptionRange founderWorldGen;
        public static OptionRange enemyExpandLim;
        public static OptionRange enemyAirSpawn;
        public static OptionToggle enemySeaSpawn;
        public static OptionToggle playerMadeTechsOnly;
        public static OptionToggle localPlayerMadeTechsOnly;
        public static OptionRange enemyBaseCount;
        public static OptionRange enemyMaxCount;
        public static OptionRange enemyMaxCountForceLim;
        public static OptionRange existingTeamBaseChance;
        public static OptionRange nonHostileBaseChance;
        public static OptionRange nonHostileAttackableBaseChance;
        public static OptionRange nonHostileAlliedBaseChance;
        public static OptionToggle ragnarok;
        public static OptionToggle copycat;
        public static OptionToggle playerStrategic;
        public static OptionToggle enemyStrategic;
        public static OptionToggle enemyMiners;
        public static OptionToggle useKeypadForGroups;
        public static OptionToggle enemyBaseCulling;

        public static OptionToggle WarnEnemyLock;
        public static OptionToggle HoldFireOnNeutral;
        public static bool ShownRebootWarning = false;


        internal static void PushExtModOptionsHandling()
        {
            var TACAI = KickStart.ModID + " - A.I. General";
#if !STEAM  // Because this toggle is reserved for the loading and unloading of the mod in STEAM release
            betterAI = new OptionToggle("<b>Enable Mod</b>", TACAI, KickStart.EnableBetterAI);
            betterAI.onValueSaved.AddListener(() => { KickStart.EnableBetterAI = betterAI.SavedValue; });
#endif
            OptionToggle togTest = new OptionToggle("Flying Enemy Warnings", TACAI, KickStart.WarnOnEnemyLock);
            togTest.onValueSaved.AddListener(() => { KickStart.WarnOnEnemyLock = togTest.SavedValue; });
            WarnEnemyLock = togTest;
            muteNonPlayerBuildRacket = new OptionToggle("Mute Non-Player Build Racket", TACAI, KickStart.MuteNonPlayerRacket);
            muteNonPlayerBuildRacket.onValueSaved.AddListener(() => { KickStart.MuteNonPlayerRacket = muteNonPlayerBuildRacket.SavedValue; });
            exportReadableRAW = new OptionToggle("Export .JSON instead of .RAWTECH", TACAI, RawTechExporter.ExportJSONInsteadOfRAWTECH);
            exportReadableRAW.onValueSaved.AddListener(() => { RawTechExporter.ExportJSONInsteadOfRAWTECH = exportReadableRAW.SavedValue; });

            retreatHotkey = new OptionKey("Retreat Button", TACAI, KickStart.RetreatHotkey);
            retreatHotkey.onValueSaved.AddListener(() =>
            {
                KickStart.RetreatHotkey = retreatHotkey.SavedValue;
                KickStart.RetreatHotkeySav = (int)KickStart.RetreatHotkey;
            });
            dodgePeriod = SuperNativeOptions.OptionRangeAutoDisplay("A.I. Dodge Processing Laziness",
                TACAI, KickStart.AIDodgeCheapness - 1, 0, 60, 5, (float value) => {
                    if (value == 0)
                        return "Not Lazy";
                    return Mathf.RoundToInt(value * (10f / 6f)) + "%";
                });
            dodgePeriod.onValueSaved.AddListener(() => { KickStart.AIDodgeCheapness = (int)dodgePeriod.SavedValue + 1; });
            aiPathing = SuperNativeOptions.OptionRangeAutoDisplay("A.I. Pathfinding Speed", TACAI, 
                AIEPathMapper.PathRequestsToCalcPerFrame, 0, 6, 1, (float value) => {
                    if (value == 0)
                        return "Off";
                    return Mathf.RoundToInt(value * (100f / 6f)) + "%";
                });
            aiPathing.onValueSaved.AddListener(() => {
                AIEPathMapper.PathRequestsToCalcPerFrame = (int)aiPathing.SavedValue;
            });
            HoldFireOnNeutral = new OptionToggle("Don't auto-attack neutrals", TACAI, AIGlobals.AllowWeaponsDisarm);
            HoldFireOnNeutral.onValueSaved.AddListener(() => { AIGlobals.AllowWeaponsDisarm = HoldFireOnNeutral.SavedValue; });

            var TACAISP = KickStart.ModID + " - A.I. Single Player";
            aiUpkeepRefresh = SuperNativeOptions.OptionRangeAutoDisplay("A.I. Awareness Update Laziness",
                TACAISP, KickStart.AIClockPeriodSet, 5, 50, 5, (float value) => {
                    if (value == 5)
                        return "Not Lazy";
                    return Mathf.RoundToInt((value - 5) * (100f / 45f)) + "%";
                });
            aiUpkeepRefresh.onValueSaved.AddListener(() => { KickStart.AIClockPeriodSet = (short)aiUpkeepRefresh.SavedValue; });
            aiSelfRepair = new OptionToggle("Allow Mobile A.I.s to Build", TACAISP, KickStart.AllowAISelfRepair);
            aiSelfRepair.onValueSaved.AddListener(() => { KickStart.AllowAISelfRepair = aiSelfRepair.SavedValue; });

            var TACAIMP = KickStart.ModID + " - A.I. MP Host";
            aiSelfRepair2 = new OptionToggle("Mobile A.I. Building - Requires Beefy Networking", TACAIMP, KickStart.AllowAISelfRepairInMP);
            aiSelfRepair2.onValueSaved.AddListener(() => { KickStart.AllowAISelfRepairInMP = aiSelfRepair2.SavedValue; });


            var TACAIRTS = KickStart.ModID + " - Real-Time Strategy [RTS] Mode";
            playerStrategic = new OptionToggle("Enable Player RTS HUD", TACAIRTS, KickStart.AllowPlayerRTSHUD);//\nRandomAdditions and TweakTech highly advised for best experience
            playerStrategic.onValueSaved.AddListener(() => { 
                KickStart.AllowPlayerRTSHUD = playerStrategic.SavedValue;
                if (KickStart.AllowPlayerRTSHUD)
                {
                    ManWorldRTS.Initiate();
                    //ModStatusChecker.EncapsulateSafeInit("Advanced AI", ManWorldRTS.DelayedInitiate, KickStart.DeInitALL); // tf is this here for?
                }
                else
                {
                    ManWorldRTS.DeInit();
                    // ManUI.inst.ShowErrorPopup("A game restart is required to let the changes take effect"); // causes settings fail
                }
            });
            enemyStrategic = new OptionToggle("Enemy RTS A.I.", TACAIRTS, KickStart.AllowStrategicAI);//\nRandomAdditions and TweakTech highly advised for best experience
            enemyStrategic.onValueSaved.AddListener(() => {
                KickStart.AllowStrategicAI = enemyStrategic.SavedValue;
                if (KickStart.AllowStrategicAI)
                {
                    ManEnemyWorld.Initiate();
                    //ModStatusChecker.EncapsulateSafeInit("Advanced AI", ManWorldRTS.DelayedInitiate, KickStart.DeInitALL); // tf is this here for?
                }
                else
                {
                    ManEnemyWorld.DeInit();
                    // ManUI.inst.ShowErrorPopup("A game restart is required to let the changes take effect"); // causes settings fail
                }
            });
            commandHotKey = new OptionKey("Enable RTS Overlay Hotkey", TACAIRTS, KickStart.CommandHotkey);
            commandHotKey.onValueSaved.AddListener(() =>
            {
                KickStart.CommandHotkey = commandHotKey.SavedValue;
                KickStart.CommandHotkeySav = (int)KickStart.CommandHotkey;
            });
            groupSelectKey = new OptionKey("Multi-Select Hotkey", TACAIRTS, KickStart.MultiSelect);
            groupSelectKey.onValueSaved.AddListener(() =>
            {
                KickStart.MultiSelect = groupSelectKey.SavedValue;
                KickStart.MultiSelectKeySav = (int)KickStart.MultiSelect;
            });
            commandBoltsHotKey = new OptionKey("Detonate Bolts Hotkey", TACAIRTS, KickStart.CommandBoltsHotkey);
            commandBoltsHotKey.onValueSaved.AddListener(() =>
            {
                KickStart.CommandBoltsHotkey = commandBoltsHotKey.SavedValue;
                KickStart.CommandBoltsHotkeySav = (int)KickStart.CommandBoltsHotkey;
            });
            modeSelectKey = new OptionKey("A.I. Menu Hotkey", TACAIRTS, KickStart.ModeSelect);
            modeSelectKey.onValueSaved.AddListener(() =>
            {
                KickStart.ModeSelect = modeSelectKey.SavedValue;
                KickStart.ModeSelectKeySav = (int)KickStart.ModeSelect;
            });
            nptInteractKey = new OptionKey("NPT Interact Hotkey", TACAIRTS, KickStart.NPTInteract);
            nptInteractKey.onValueSaved.AddListener(() =>
            {
                KickStart.NPTInteract = nptInteractKey.SavedValue;
                KickStart.NPTInteractKeySav = (int)KickStart.NPTInteract;
            });
            commandClassic = new OptionToggle("Classic RTS Controls", TACAIRTS, KickStart.UseClassicRTSControls);
            commandClassic.onValueSaved.AddListener(() => { KickStart.UseClassicRTSControls = commandClassic.SavedValue; });
            useKeypadForGroups = new OptionToggle("Enable Keypad for Grouping - Check Num Lock", TACAIRTS, KickStart.UseNumpadForGrouping);
            useKeypadForGroups.onValueSaved.AddListener(() => { KickStart.UseNumpadForGrouping = useKeypadForGroups.SavedValue; });

            var enemyBaseTeamsUpdateLaziness = new OptionList<BaseTeamsUpdateRate>("Unloaded Base Update Rate", TACAIRTS,
                KickStart.limitAIBaseRate, (int)KickStart.EnemyBaseUpdateMode);
            enemyBaseTeamsUpdateLaziness.onValueSaved.AddListener(() =>
            {
                KickStart.EnemyBaseUpdateMode = enemyBaseTeamsUpdateLaziness.SavedValue;
            });


            var TACAIEnemies = KickStart.ModID + " - Non-Player Techs (NPT) General";
            painfulEnemies = new OptionToggle("<b>Enable Advanced NPTs</b>", TACAIEnemies, KickStart.enablePainMode);
            painfulEnemies.onValueSaved.AddListener(() => { KickStart.enablePainMode = painfulEnemies.SavedValue; });
            diff = SuperNativeOptions.OptionRangeAutoDisplay("NPT Difficulty", 
                TACAIEnemies, KickStart.difficulty, -50, 150, 25, (float value) => {
                    string pre = Mathf.RoundToInt((value + 50) / 2).ToString();
                    if (value.Approximately(-50))
                        return pre + "% Vanilla AI";
                    if (value.Approximately(0))
                        return pre + "% Standard";
                    if (value.Approximately(150))
                        return pre + "% Insanity";
                    if (value < 0)
                        return pre +"% Easy";
                    if (value >= 75)
                        return pre + "% Hard";
                    return pre + "% Medium";
                });
            diff.onValueSaved.AddListener(() =>
            {
                KickStart.difficulty = (int)diff.SavedValue;
                AIERepair.RefreshDelays();
            });
            displayEvents = new OptionToggle("Show NPT Events", TACAIEnemies, KickStart.DisplayEnemyEvents);
            displayEvents.onValueSaved.AddListener(() => { KickStart.DisplayEnemyEvents = displayEvents.SavedValue; });
            enemyMiners = new OptionToggle("NPTs Can Mine", TACAIEnemies, KickStart.AllowEnemiesToMine);
            enemyMiners.onValueSaved.AddListener(() => { KickStart.AllowEnemiesToMine = enemyMiners.SavedValue; });
            blockRecoveryChance = SuperNativeOptions.OptionRangeAutoDisplay("NPT Block Drop Chance", 
                TACAIEnemies, KickStart.EnemyBlockDropChance, 0, 100, 10, (float value) => {
                    if (value == 0)
                        return "Never";
                    if (value == 100)
                        return "Highest";
                    return Mathf.RoundToInt(value) + "%";
                });
            blockRecoveryChance.onValueSaved.AddListener(() => {
                KickStart.EnemyBlockDropChance = (int)blockRecoveryChance.SavedValue;
                Globals.inst.m_BlockSurvivalChance = (float)((float)KickStart.EnemyBlockDropChance / 100.0f);

                if (KickStart.EnemyBlockDropChance == 0)
                {
                    Globals.inst.moduleDamageParams.detachMeterFillFactor = 0;// Make enemies drop no blocks!
                }
                else
                    Globals.inst.moduleDamageParams.detachMeterFillFactor = KickStart.SavedDefaultEnemyFragility;
            });
            infEnemySupplies = new OptionToggle("All NPTechs Cheat Blocks", TACAIEnemies, KickStart.EnemiesHaveCreativeInventory);
            infEnemySupplies.onValueSaved.AddListener(() => { KickStart.EnemiesHaveCreativeInventory = infEnemySupplies.SavedValue; });

            founderOnScene = SuperNativeOptions.OptionRangeAutoDisplay("Base Invader Spawn Chance", 
                TACAIEnemies, AIGlobals.EnemyBaseMakerChance, 0, 100, 1, (float value) => {
                    if (value == 0)
                        return "Never";
                    if (value == 100)
                        return "Maximum";
                    return Mathf.RoundToInt(value) + "%";
                });
            founderOnScene.onValueSaved.AddListener(() => { AIGlobals.EnemyBaseMakerChance = founderOnScene.SavedValue; });
            founderWorldGen = SuperNativeOptions.OptionRangeAutoDisplay("Natural Base Chance", 
                TACAIEnemies, KickStart.SpawnFoundersPositional, 0, 1f, 0.05f, (float value) => {
                    if (value == 0)
                        return "Never";
                    if (value == 1)
                        return "Maximum";
                    return Mathf.RoundToInt(value * 100) + "%";
                });
            founderWorldGen.onValueSaved.AddListener(() => { KickStart.SpawnFoundersPositional = founderWorldGen.SavedValue; });
            founderAlwaysUnloaded = new OptionToggle("Natural Bases Respawn", TACAIEnemies, KickStart.ActiveSpawnFoundersOffScene);
            founderAlwaysUnloaded.onValueSaved.AddListener(() => { KickStart.ActiveSpawnFoundersOffScene = founderAlwaysUnloaded.SavedValue; });

            existingTeamBaseChance = SuperNativeOptions.OptionRangeAutoDisplay("Team Growth Chance", 
                TACAIEnemies, ManBaseTeams.PercentChanceExisting, 0, 1f, 0.05f, (float value) => {
                    if (value == 0)
                        return "Never";
                    if (value == 1)
                        return "Highest";
                    return Mathf.RoundToInt(value * 100) + "%";
                });
            existingTeamBaseChance.onValueSaved.AddListener(() => { ManBaseTeams.PercentChanceExisting = existingTeamBaseChance.SavedValue; });
            nonHostileBaseChance = SuperNativeOptions.OptionRangeAutoDisplay("Non-Hostile Base Chance", 
                TACAIEnemies, AIGlobals.AttackableNeutralBaseChanceMulti, 0, 1f, 0.05f, (float value) => {
                    if (value == 0)
                        return "Never";
                    if (value == 1)
                        return "Maximum";
                    return Mathf.RoundToInt(value * 100) + "%";
                });
            nonHostileBaseChance.onValueSaved.AddListener(() => { AIGlobals.AttackableNeutralBaseChanceMulti = nonHostileBaseChance.SavedValue; });
            nonHostileAttackableBaseChance = SuperNativeOptions.OptionRangeAutoDisplay("Rival Base Chance",
                TACAIEnemies, AIGlobals.AttackableNeutralBaseChanceMulti, 0, 1f, 0.05f, (float value) => {
                    if (value == 0)
                        return "Never";
                    if (value == 1)
                        return "Maximum";
                    return Mathf.RoundToInt(value * 100) + "%";
                });
            nonHostileAttackableBaseChance.onValueSaved.AddListener(() => { AIGlobals.AttackableNeutralBaseChanceMulti = nonHostileAttackableBaseChance.SavedValue; });
            nonHostileAlliedBaseChance = SuperNativeOptions.OptionRangeAutoDisplay("Friendly Base Chance", 
                TACAIEnemies, AIGlobals.FriendlyBaseChanceMulti, 0, 1f, 0.05f, (float value) => {
                    if (value == 0)
                        return "Never";
                    if (value == 1)
                        return "Maximum";
                    return Mathf.RoundToInt(value * 100) + "%";
                });
            nonHostileAlliedBaseChance.onValueSaved.AddListener(() => { AIGlobals.FriendlyBaseChanceMulti = nonHostileAlliedBaseChance.SavedValue; });

            enemyBaseCount = SuperNativeOptions.OptionRangeAutoDisplay("Max Unique NPT Base Teams Active",
                TACAIEnemies, KickStart.MaxEnemyBaseLimit, 0, 6, 1, (float value) => {
                    if (value == 0)
                        return "Off";
                    return Mathf.RoundToInt(value).ToString();
                });
            enemyBaseCount.onValueSaved.AddListener(() => { KickStart.MaxEnemyBaseLimit = (int)enemyBaseCount.SavedValue; });
            enemyExpandLim = SuperNativeOptions.OptionRangeAutoDisplay("Max NPT Anchored Techs", 
                TACAIEnemies, KickStart.MaxBasesPerTeam, 0, 12, 3, (float value) => {
                    if (value == 0)
                        return "Off";
                    return Mathf.RoundToInt(value).ToString();
                });
            enemyExpandLim.onValueSaved.AddListener(() => { KickStart.MaxBasesPerTeam = (int)enemyExpandLim.SavedValue; });
            enemyBaseCulling = new OptionToggle("Remove Far NPT Bases", TACAIEnemies, KickStart.CullFarEnemyBases);
            var SpawnLimiterSettings = new OptionList<EnemyMaxDistLimit>("Remove Far NPT Bases", TACAIEnemies, KickStart.limitTypes, (int)KickStart.CullFarEnemyBasesMode);
            SpawnLimiterSettings.onValueSaved.AddListener(() =>
            {
                KickStart.CullFarEnemyBasesMode = SpawnLimiterSettings.SavedValue;
                KickStart.UpdateCullDist();
            }); 

            if (!KickStart.isPopInjectorPresent)
            {
                var TACAIEnemiesPop = KickStart.ModID + " - Non-Player Techs (NPT) Spawning";
                enemyMaxCount = SuperNativeOptions.OptionRangeAutoDisplay("NPTechs Spawn Limit", 
                    TACAIEnemiesPop, KickStart.AIPopMaxLimit, 6, 32, 1, (float value) => {
                        if (value == 6)
                            return "Default";
                        if (value > 24)
                            return Mathf.RoundToInt(value).ToString() + " VERY Laggy!";
                        if (value > 12)
                            return Mathf.RoundToInt(value).ToString() + " Laggy!";
                        return Mathf.RoundToInt(value).ToString();
                    });
                enemyMaxCount.onValueSaved.AddListener(() =>
                {
                    KickStart.AIPopMaxLimit = (int)enemyMaxCount.SavedValue;
                    KickStart.OverrideEnemyMax();
                });
                enemyMaxCountForceLim = SuperNativeOptions.OptionRangeAutoDisplay("NPTechs Limit Overflow",
                    TACAIEnemiesPop, KickStart.ForceRemoveOverEnemyMaxCap, 1, 12, 1, (float value) => {
                        if (value == 1)
                            return "Default";
                        return Mathf.RoundToInt(value).ToString();
                    });
                enemyMaxCountForceLim.onValueSaved.AddListener(() =>
                {
                    KickStart.ForceRemoveOverEnemyMaxCap = (int)enemyMaxCountForceLim.SavedValue;
                });

                landEnemyChangeChance = SuperNativeOptions.OptionRangeAutoDisplay("NPT Land RawTechs Chance",
                    TACAIEnemiesPop, KickStart.LandEnemyOverrideChance, 0, 100, 5, (float value) => {
                        if (value == 0)
                            return "Off";
                        if (value == 100)
                            return "Highest";
                        return Mathf.RoundToInt(value) + "%";
                    });
                landEnemyChangeChance.onValueSaved.AddListener(() => { KickStart.LandEnemyOverrideChance = (int)landEnemyChangeChance.SavedValue; });
                enemyAirSpawn = SuperNativeOptions.OptionRangeAutoDisplay("NPT Aircraft Frequency",
                    TACAIEnemiesPop, KickStart.AirEnemiesSpawnRate, 0, 4, 0.5f, (float value) => {
                        if (value == 0)
                            return "Off";
                        if (value == 1)
                            return "Default";
                        return Mathf.RoundToInt(value * 100) + "%";
                    });
                enemyAirSpawn.onValueSaved.AddListener(() => {
                    KickStart.AirEnemiesSpawnRate = (int)enemyAirSpawn.SavedValue;
                    if (KickStart.AirEnemiesSpawnRate == 0)
                        KickStart.AllowAirEnemiesToSpawn = false;
                    else
                    {
                        SpecialAISpawner.AirSpawnInterval = 60 / KickStart.AirEnemiesSpawnRate;
                        KickStart.AllowAirEnemiesToSpawn = true;
                    }
                });
                enemySeaSpawn = new OptionToggle("NPT Sea Spawning (Needs Water Mod)", TACAIEnemiesPop, KickStart.AllowSeaEnemiesToSpawn);
                enemySeaSpawn.onValueSaved.AddListener(() => { KickStart.AllowSeaEnemiesToSpawn = enemySeaSpawn.SavedValue; });
                playerMadeTechsOnly = new OptionToggle("Try Exclude Vanilla Spawns", TACAIEnemiesPop, KickStart.TryForceOnlyPlayerSpawns);
                playerMadeTechsOnly.onValueSaved.AddListener(() => {
                    if (KickStart.TryForceOnlyPlayerSpawns != playerMadeTechsOnly.SavedValue)
                    {
                        KickStart.TryForceOnlyPlayerSpawns = playerMadeTechsOnly.SavedValue;
                        ModTechsDatabase.ValidateAndAddAllInternalTechs();
                    }
                });
                localPlayerMadeTechsOnly = new OptionToggle("Try Only Use Local Spawns", TACAIEnemiesPop, KickStart.TryForceOnlyPlayerLocalSpawns);
                localPlayerMadeTechsOnly.onValueSaved.AddListener(() => {
                    if (KickStart.TryForceOnlyPlayerLocalSpawns != localPlayerMadeTechsOnly.SavedValue)
                    {
                        KickStart.TryForceOnlyPlayerLocalSpawns = localPlayerMadeTechsOnly.SavedValue;
                        ModTechsDatabase.ValidateAndAddAllInternalTechs();
                    }
                });
                permitEradication = new OptionToggle("<b>Eradicators</b> - Huge NPT Tech Spawns - Requires Beefy Computer", TACAIEnemiesPop, KickStart.EnemyEradicators);
                permitEradication.onValueSaved.AddListener(() => { KickStart.EnemyEradicators = permitEradication.SavedValue; });
                ragnarok = new OptionToggle("<b>Ragnarok</b> - Death To All (Anything Can Spawn) - Requires Beefy Computer", TACAIEnemiesPop, KickStart.CommitDeathMode);
                ragnarok.onValueSaved.AddListener(() =>
                {
                    KickStart.CommitDeathMode = ragnarok.SavedValue;
                    OverrideManPop.ChangeToRagnarokPop(KickStart.CommitDeathMode);
                });
                /*
                copycat = new OptionToggle("<b>Copy Cat</b> - Enemy can use your local Tech snaps!", TACAIEnemiesPop, KickStart.CatMode);
                copycat.onValueSaved.AddListener(() =>
                {
                    KickStart.CatMode = copycat.SavedValue;
                });*/
            }
            if (KickStart.EnemyBlockDropChance == 0)
            {
                Globals.inst.moduleDamageParams.detachMeterFillFactor = 0;// Make enemies drop no blocks!
            }

            if (KickStart.AirEnemiesSpawnRate == 0)
                KickStart.AllowAirEnemiesToSpawn = false;
            else
            {
                SpecialAISpawner.AirSpawnInterval = 60 / KickStart.AirEnemiesSpawnRate;
                KickStart.AllowAirEnemiesToSpawn = true;
            }
        }

        internal static void PushExtModOptionsHandling(ModConfig thisModConfig)
        {
            PushExtModOptionsHandling();
            if (thisModConfig != null)
                NativeOptionsMod.onOptionsSaved.AddListener(() => { thisModConfig.WriteConfigJsonFile(); });
        }
    }
}
