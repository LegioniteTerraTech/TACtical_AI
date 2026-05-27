using System;
using System.Collections.Generic;
using System.Linq;
//using Harmony;
using HarmonyLib;
using UnityEngine;
using TAC_AI.AI;
using TAC_AI.AI.Enemy;
using TAC_AI.Templates;
using TAC_AI.World;
using TAC_AI.AI.Movement;
using TerraTechETCUtil;

#if !STEAM
using ModHelper.Config;
#endif


namespace TAC_AI
{

    public struct BaseTeamsUpdateRate
    {
        public string name;
        public BaseTeamsUpdateRate(string name)
        {
            this.name = name;
        }
        public override string ToString() => name;
    }
    public class KickStartConfigHelper
    {
        public static bool CullFarEnemyBasesDevStartup = false;
        private static ModHelper.ModConfig modConfig;
        internal static void Save()
        {
            modConfig.WriteConfigJsonFile();
        }
        internal static void PushExtModConfigHandlingConfigOnly()
        {
            KickStart.SavedDefaultEnemyFragility = Globals.inst.moduleDamageParams.detachMeterFillFactor;

            PushExtModConfigSetup();

            OverrideManPop.ChangeToRagnarokPop(KickStart.CommitDeathMode);
        }
        internal static ModHelper.ModConfig PushExtModConfigSetup()
        {
            ModHelper.ModConfig thisModConfig = new ModHelper.ModConfig();
            if (!thisModConfig.ReadConfigJsonFile())
            {
                LoadingHintsExt.MandatoryHints.Add(new LocExtStringNonReg(AltUI.ObjectiveString("Advanced AI") + 
                    " adds multiple layers of depth and " + AltUI.EnemyString("difficulty") + " to TerraTech.  " +
                AltUI.HintString("Best suited for prospecting veterans looking for a new challenge.")));
            }
            KickStart.EnemyBlockDetachChance = Mathf.CeilToInt(Globals.inst.moduleDamageParams.detachMeterFillFactor * 25.0f);
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
            thisModConfig.BindConfig<AIGlobals>(null, "AllowWeaponsDisarm2");
            thisModConfig.BindConfig<KickStart>(null, "LandEnemyOverrideChanceSav");
            thisModConfig.BindConfig<KickStart>(null, "EnemyBlockDetachChance");
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
            thisModConfig.BindConfig<KickStart>(null, "CullFarEnemyBasesMode");
            thisModConfig.BindConfig<KickStart>(null, "ForceRemoveOverEnemyMaxCap");
            thisModConfig.BindConfig<KickStart>(null, "EnemyBaseUpdateMode");

            // RTS
            thisModConfig.BindConfig<KickStart>(null, "AllowPlayerRTSHUD");
            thisModConfig.BindConfig<KickStart>(null, "AllowStrategicAI");
            thisModConfig.BindConfig<ManWorldRTS>(null, "RTSUIScale");
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
            if (CullFarEnemyBasesDevStartup)
            {
                KickStart.CullFarEnemyBasesMode = 2;
                CullFarEnemyBasesDevStartup = false;
            }
            modConfig = thisModConfig;
            return thisModConfig;
        }
        internal static void PushExtModConfigHandling()
        {
            KickStart.SavedDefaultEnemyRecoveryRate = Globals.inst.moduleDamageParams.detachMeterFillFactor;
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
    public class KickStartNativeOptions
    {
        // NativeOptions Parameters
        public static Nuterra.NativeOptions.OptionKey retreatHotkey;
        public static Nuterra.NativeOptions.OptionKey commandHotKey;
        public static Nuterra.NativeOptions.OptionKey commandBoltsHotKey;
        public static Nuterra.NativeOptions.OptionKey groupSelectKey;
        public static Nuterra.NativeOptions.OptionKey modeSelectKey;
        public static Nuterra.NativeOptions.OptionKey nptInteractKey;
        public static Nuterra.NativeOptions.OptionToggle commandClassic;
        public static Nuterra.NativeOptions.OptionToggle betterAI;
        public static Nuterra.NativeOptions.OptionToggle aiSelfRepair;
        public static Nuterra.NativeOptions.OptionToggle aiSelfRepair2;
        public static Nuterra.NativeOptions.OptionRange dodgePeriod;
        public static Nuterra.NativeOptions.OptionRange aiUpkeepRefresh; //AIClockPeriod
        public static Nuterra.NativeOptions.OptionRange aiPathing;
        public static Nuterra.NativeOptions.OptionToggle muteNonPlayerBuildRacket;
        public static Nuterra.NativeOptions.OptionToggle allowOverLevelBlocksDrop;
        public static Nuterra.NativeOptions.OptionToggle exportReadableRAW;
        public static Nuterra.NativeOptions.OptionToggle displayEvents;
        public static Nuterra.NativeOptions.OptionToggle painfulEnemies;
        public static Nuterra.NativeOptions.OptionRange diff;
        public static Nuterra.NativeOptions.OptionRange landEnemyChangeChance;
        public static Nuterra.NativeOptions.OptionRange blockDetachChance;
        public static Nuterra.NativeOptions.OptionRange blockRecoveryChance;
        public static Nuterra.NativeOptions.OptionToggle permitEradication;
        public static Nuterra.NativeOptions.OptionToggle infEnemySupplies;
        public static Nuterra.NativeOptions.OptionRange founderOnScene;
        public static Nuterra.NativeOptions.OptionToggle founderAlwaysUnloaded;
        public static Nuterra.NativeOptions.OptionRange founderWorldGen;
        public static Nuterra.NativeOptions.OptionRange enemyExpandLim;
        public static Nuterra.NativeOptions.OptionRange enemyAirSpawn;
        public static Nuterra.NativeOptions.OptionToggle enemySeaSpawn;
        public static Nuterra.NativeOptions.OptionToggle playerMadeTechsOnly;
        public static Nuterra.NativeOptions.OptionToggle localPlayerMadeTechsOnly;
        public static Nuterra.NativeOptions.OptionRange enemyBaseCount;
        public static Nuterra.NativeOptions.OptionRange enemyMaxCount;
        public static Nuterra.NativeOptions.OptionRange enemyMaxCountForceLim;
        public static Nuterra.NativeOptions.OptionRange existingTeamBaseChance;
        public static Nuterra.NativeOptions.OptionRange nonHostileBaseChance;
        public static Nuterra.NativeOptions.OptionRange nonHostileAttackableBaseChance;
        public static Nuterra.NativeOptions.OptionRange nonHostileAlliedBaseChance;
        public static Nuterra.NativeOptions.OptionToggle ragnarok;
        public static Nuterra.NativeOptions.OptionToggle copycat;

        public static Nuterra.NativeOptions.OptionToggle playerStrategic;
        public static Nuterra.NativeOptions.OptionToggle enemyStrategic;
        public static Nuterra.NativeOptions.OptionRange guiScaler;
        public static Nuterra.NativeOptions.OptionToggle enemyMiners;
        public static Nuterra.NativeOptions.OptionToggle useKeypadForGroups;

        public static Nuterra.NativeOptions.OptionToggle WarnEnemyLock;
        public static Nuterra.NativeOptions.OptionToggle HoldFireOnNeutral;
        public static bool ShownRebootWarning = false;


        public static void EnoughLocalTechsSanityCheck()
        {
            if (KickStart.TryForceOnlyPlayerLocalSpawns && ModTechsDatabase.DoWeNotHaveEnoughLocalTechs())
            {
                ManModGUI.ShowErrorPopup("Advanced AI - \"Try Only Use Local Spawns\" does not have enough Local Techs to insure random spawning!\n  You only have " +
                    ModTechsDatabase.ExtPopTechsAllCount() + " Techs, while at least " + ModTechsDatabase.MinimumLocalTechsToTriggerWarning +
                    " Techs in your local enemies folder is suggested.  This automatic check does not cover edge-cases caused by a lack of diversity in your Tech selection.\n" +
                    AltUI.ObjectiveString("  Press \"Fix\" to disable \"Try Only Use Local Spawns\""), false, () =>
                    {
                        localPlayerMadeTechsOnly.Value = false;
                        if (KickStart.isConfigHelperPresent)
                        {
                            try
                            {
                                KickStart.ENCAPSULATEErrorSaveConfig();
                            }
                            catch (Exception e)
                            {
                                DebugTAC_AI.Log(KickStart.ModID + ": Error on Option & Config saving");
                                DebugTAC_AI.Log(e);
                                DebugTAC_AI.ErrorReport("Error on hooks with ConfigHelper");
                            }
                        }
                    });
            }
        }

        internal static void PushExtModOptionsHandling()
        {
            var TACAI = KickStart.ModID + " - A.I. General";
#if !STEAM  // Because this toggle is reserved for the loading and unloading of the mod in STEAM release
            betterAI = new OptionToggle("<b>Enable Mod</b>", TACAI, KickStart.EnableBetterAI);
            betterAI.onValueSaved.AddListener(() => { KickStart.EnableBetterAI = betterAI.SavedValue; });
#endif
            Nuterra.NativeOptions.OptionToggle togTest = new Nuterra.NativeOptions.OptionToggle("Flying Enemy Warnings", TACAI, KickStart.WarnOnEnemyLock);
            togTest.onValueSaved.AddListener(() => { KickStart.WarnOnEnemyLock = togTest.SavedValue; });
            WarnEnemyLock = togTest;
            muteNonPlayerBuildRacket = new Nuterra.NativeOptions.OptionToggle("Mute Non-Player Build Racket", TACAI, KickStart.MuteNonPlayerRacket);
            muteNonPlayerBuildRacket.onValueSaved.AddListener(() => { KickStart.MuteNonPlayerRacket = muteNonPlayerBuildRacket.SavedValue; });
            exportReadableRAW = new Nuterra.NativeOptions.OptionToggle("Export .JSON instead of .RAWTECH", TACAI, RawTechExporter.ExportJSONInsteadOfRAWTECH);
            exportReadableRAW.onValueSaved.AddListener(() => { RawTechExporter.ExportJSONInsteadOfRAWTECH = exportReadableRAW.SavedValue; });

            retreatHotkey = new Nuterra.NativeOptions.OptionKey("Retreat Button", TACAI, KickStart.RetreatHotkey);
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
            HoldFireOnNeutral = new Nuterra.NativeOptions.OptionToggle("Don't auto-attack neutrals", TACAI, AIGlobals.AllowWeaponsDisarm2);
            HoldFireOnNeutral.onValueSaved.AddListener(() => { AIGlobals.AllowWeaponsDisarm2 = HoldFireOnNeutral.SavedValue; });

            var TACAISP = KickStart.ModID + " - A.I. Single Player";
            aiUpkeepRefresh = SuperNativeOptions.OptionRangeAutoDisplay("A.I. Awareness Update Laziness",
                TACAISP, KickStart.AIClockPeriodSet, 5, 50, 5, (float value) => {
                    if (value == 5)
                        return "Not Lazy";
                    return Mathf.RoundToInt((value - 5) * (100f / 45f)) + "%";
                });
            aiUpkeepRefresh.onValueSaved.AddListener(() => { KickStart.AIClockPeriodSet = (short)aiUpkeepRefresh.SavedValue; });
            aiSelfRepair = new Nuterra.NativeOptions.OptionToggle("Allow Mobile A.I.s to Build", TACAISP, KickStart.AllowAISelfRepair);
            aiSelfRepair.onValueSaved.AddListener(() => { KickStart.AllowAISelfRepair = aiSelfRepair.SavedValue; });

            var TACAIMP = KickStart.ModID + " - A.I. MP Host";
            aiSelfRepair2 = new Nuterra.NativeOptions.OptionToggle("Mobile A.I. Building - Requires Beefy Networking", TACAIMP, KickStart.AllowAISelfRepairInMP);
            aiSelfRepair2.onValueSaved.AddListener(() => { KickStart.AllowAISelfRepairInMP = aiSelfRepair2.SavedValue; });


            var TACAIRTS = KickStart.ModID + " - Real-Time Strategy [RTS] Mode";
            playerStrategic = new Nuterra.NativeOptions.OptionToggle("Enable A.I. Click-based Control", TACAIRTS, KickStart.AllowPlayerRTSHUD);//\nRandomAdditions and TweakTech highly advised for best experience
            playerStrategic.onValueSaved.AddListener(() => { 
                KickStart.AllowPlayerRTSHUD = playerStrategic.SavedValue;
                if (KickStart.AllowPlayerRTSHUD)
                {
                    PlayerRTSUI.Initiate();
                    //ModStatusChecker.EncapsulateSafeInit("Advanced AI", ManWorldRTS.DelayedInitiate, KickStart.DeInitALL); // tf is this here for?
                }
                else
                {
                    PlayerRTSUI.DeInit();
                    // ManUI.inst.ShowErrorPopup("A game restart is required to let the changes take effect"); // causes settings fail
                }
            });
            enemyStrategic = new Nuterra.NativeOptions.OptionToggle("Allow A.I. to Act When Out of View", TACAIRTS, KickStart.AllowStrategicAI);//\nRandomAdditions and TweakTech highly advised for best experience
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
            guiScaler = SuperNativeOptions.OptionRangeAutoDisplay("RTS GUI Scaling", TACAIRTS, ManWorldRTS.RTSUIScale, 
                0.5f, 1.5f, 0.05f, (float inVal) => inVal.ToString("P"));
            guiScaler.onValueSaved.AddListener(delegate
            {
                ManWorldRTS.RTSUIScale = guiScaler.SavedValue;
                ManWorldRTS.RecalcUIScale();
            });
            commandHotKey = new Nuterra.NativeOptions.OptionKey("Enable RTS Overlay Hotkey", TACAIRTS, KickStart.CommandHotkey);
            commandHotKey.onValueSaved.AddListener(() =>
            {
                KickStart.CommandHotkey = commandHotKey.SavedValue;
                KickStart.CommandHotkeySav = (int)KickStart.CommandHotkey;
            });
            groupSelectKey = new Nuterra.NativeOptions.OptionKey("Multi-Select Hotkey", TACAIRTS, KickStart.MultiSelect);
            groupSelectKey.onValueSaved.AddListener(() =>
            {
                KickStart.MultiSelect = groupSelectKey.SavedValue;
                KickStart.MultiSelectKeySav = (int)KickStart.MultiSelect;
            });
            commandBoltsHotKey = new Nuterra.NativeOptions.OptionKey("Detonate Bolts Hotkey", TACAIRTS, KickStart.CommandBoltsHotkey);
            commandBoltsHotKey.onValueSaved.AddListener(() =>
            {
                KickStart.CommandBoltsHotkey = commandBoltsHotKey.SavedValue;
                KickStart.CommandBoltsHotkeySav = (int)KickStart.CommandBoltsHotkey;
            });
            modeSelectKey = new Nuterra.NativeOptions.OptionKey("A.I. Menu Hotkey", TACAIRTS, KickStart.ModeSelect);
            modeSelectKey.onValueSaved.AddListener(() =>
            {
                KickStart.ModeSelect = modeSelectKey.SavedValue;
                KickStart.ModeSelectKeySav = (int)KickStart.ModeSelect;
            });
            nptInteractKey = new Nuterra.NativeOptions.OptionKey("NPT Interact Hotkey", TACAIRTS, KickStart.NPTInteract);
            nptInteractKey.onValueSaved.AddListener(() =>
            {
                KickStart.NPTInteract = nptInteractKey.SavedValue;
                KickStart.NPTInteractKeySav = (int)KickStart.NPTInteract;
            });
            commandClassic = new Nuterra.NativeOptions.OptionToggle("Classic RTS Controls", TACAIRTS, KickStart.UseClassicRTSControls);
            commandClassic.onValueSaved.AddListener(() => { KickStart.UseClassicRTSControls = commandClassic.SavedValue; });
            useKeypadForGroups = new Nuterra.NativeOptions.OptionToggle("Enable Keypad for Grouping - Check Num Lock", TACAIRTS, KickStart.UseNumpadForGrouping);
            useKeypadForGroups.onValueSaved.AddListener(() => { KickStart.UseNumpadForGrouping = useKeypadForGroups.SavedValue; });

            var enemyBaseTeamsUpdateLaziness = new Nuterra.NativeOptions.OptionList<BaseTeamsUpdateRate>("Unloaded Base Update Rate", TACAIRTS,
                KickStart.limitAIBaseRate, (int)KickStart.EnemyBaseUpdateMode);
            enemyBaseTeamsUpdateLaziness.onValueSaved.AddListener(() =>
            {
                KickStart.EnemyBaseUpdateMode = enemyBaseTeamsUpdateLaziness.SavedValue;
            });


            var TACAIEnemies = KickStart.ModID + " - Non-Player Techs (NPT) General";
            painfulEnemies = new Nuterra.NativeOptions.OptionToggle("<b>Enable Advanced NPTs</b>", TACAIEnemies, KickStart.enablePainMode);
            painfulEnemies.onValueSaved.AddListener(() => {
                if (KickStart.enablePainMode != painfulEnemies.SavedValue)
                {
                    foreach (var item in ManTechs.inst.IterateTechs())
                    {
                        if (item != null && item.IsEnemy())
                        {
                            TankAIHelper help = item.GetHelperInsured();
                            help.ForceRebuildAlignment();
                        }
                    }
                }
                KickStart.enablePainMode = painfulEnemies.SavedValue;
                //DebugRawTechSpawner.CanOpenDebugSpawnMenu = DebugRawTechSpawner.CheckValidMode();
            });
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
                        return pre + "% Easy";
                    if (value >= 75)
                        return pre + "% Hard";
                    return pre + "% Medium";
                });
            diff.onValueSaved.AddListener(() =>
            {
                KickStart.difficulty = (int)diff.SavedValue;
                AIERepair.RefreshDelays();
            });
            displayEvents = new Nuterra.NativeOptions.OptionToggle("Show NPT Events", TACAIEnemies, KickStart.DisplayEnemyEvents);
            displayEvents.onValueSaved.AddListener(() => { KickStart.DisplayEnemyEvents = displayEvents.SavedValue; });
            enemyMiners = new Nuterra.NativeOptions.OptionToggle("NPTs Can Mine", TACAIEnemies, KickStart.AllowEnemiesToMine);
            enemyMiners.onValueSaved.AddListener(() => { KickStart.AllowEnemiesToMine = enemyMiners.SavedValue; });

            blockDetachChance = SuperNativeOptions.OptionRangeAutoDisplay("NPT Block Detach Chance",
                TACAIEnemies, KickStart.EnemyBlockDetachChance, 0, 100, 10, (float value) => {
                    if (value == 0)
                        return "Never";
                    if (value == 100)
                        return "Highest";
                    return Mathf.RoundToInt(value) + "%";
                });
            blockDetachChance.onValueSaved.AddListener(() => {
                KickStart.EnemyBlockDetachChance = (int)blockDetachChance.SavedValue;
                Globals.inst.moduleDamageParams.detachMeterFillFactor = (float)((float)KickStart.EnemyBlockDetachChance / 25.0f);
            }); 
            Globals.inst.moduleDamageParams.detachMeterFillFactor = (float)((float)KickStart.EnemyBlockDetachChance / 25.0f);
            blockRecoveryChance = SuperNativeOptions.OptionRangeAutoDisplay("NPT Block Scavenge Chance", 
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
            });
            Globals.inst.m_BlockSurvivalChance = (float)((float)KickStart.EnemyBlockDropChance / 100.0f);

            infEnemySupplies = new Nuterra.NativeOptions.OptionToggle("All NPTechs Cheat Blocks", TACAIEnemies, KickStart.EnemiesHaveCreativeInventory);
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
            founderAlwaysUnloaded = new Nuterra.NativeOptions.OptionToggle("Natural Bases Respawn", TACAIEnemies, KickStart.ActiveSpawnFoundersOffScene);
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
            var SpawnLimiterSettings = new Nuterra.NativeOptions.OptionList<EnemyMaxDistLimit>("Remove Far NPT Bases", TACAIEnemies, KickStart.limitTypes, (int)KickStart.CullFarEnemyBasesMode);
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
                enemySeaSpawn = new Nuterra.NativeOptions.OptionToggle("NPT Sea Spawning (Needs Water Mod)", TACAIEnemiesPop, KickStart.AllowSeaEnemiesToSpawn);
                enemySeaSpawn.onValueSaved.AddListener(() => { KickStart.AllowSeaEnemiesToSpawn = enemySeaSpawn.SavedValue; });
                playerMadeTechsOnly = new Nuterra.NativeOptions.OptionToggle("Try Exclude Vanilla Spawns", TACAIEnemiesPop, KickStart.TryForceOnlyPlayerSpawns);
                playerMadeTechsOnly.onValueSaved.AddListener(() => {
                    if (KickStart.TryForceOnlyPlayerSpawns != playerMadeTechsOnly.SavedValue)
                    {
                        KickStart.TryForceOnlyPlayerSpawns = playerMadeTechsOnly.SavedValue;
                        ModTechsDatabase.ValidateAndAddAllInternalTechs();
                        if (KickStart.TryForceOnlyPlayerSpawns)
                        {
                            ManModGUI.ShowErrorPopup("Advanced AI - \"Try Exclude Vanilla Spawns\" does not have enough community spawns to " +
                                "insure that each spawn is random enough. \n  Use this option at your own risk.  " +
                                AltUI.ObjectiveString("Press \"Fix\" to disable \"Try Exclude Vanilla Spawns\"."), false, () =>
                                {
                                    playerMadeTechsOnly.Value = false;
                                    if (KickStart.isConfigHelperPresent)
                                    {
                                        try
                                        {
                                            KickStart.ENCAPSULATEErrorSaveConfig();
                                        }
                                        catch (Exception e)
                                        {
                                            DebugTAC_AI.Log(KickStart.ModID + ": Error on Option & Config saving");
                                            DebugTAC_AI.Log(e);
                                            DebugTAC_AI.ErrorReport("Error on hooks with ConfigHelper");
                                        }
                                    }
                                });
                        }
                    }
                });
                localPlayerMadeTechsOnly = new Nuterra.NativeOptions.OptionToggle("Try Only Use Local Spawns", TACAIEnemiesPop, KickStart.TryForceOnlyPlayerLocalSpawns);
                localPlayerMadeTechsOnly.onValueSaved.AddListener(() => {
                    if (KickStart.TryForceOnlyPlayerLocalSpawns != localPlayerMadeTechsOnly.SavedValue)
                    {
                        KickStart.TryForceOnlyPlayerLocalSpawns = localPlayerMadeTechsOnly.SavedValue;
                        ModTechsDatabase.ValidateAndAddAllInternalTechs();
                        EnoughLocalTechsSanityCheck();
                    }
                });
                permitEradication = new Nuterra.NativeOptions.OptionToggle("<b>Eradicators</b> - Huge NPT Tech Spawns - Requires Beefy Computer", TACAIEnemiesPop, KickStart.EnemyEradicators);
                permitEradication.onValueSaved.AddListener(() => { KickStart.EnemyEradicators = permitEradication.SavedValue; });
                ragnarok = new Nuterra.NativeOptions.OptionToggle("<b>Ragnarok</b> - Death To All (Anything Can Spawn) - Requires Beefy Computer", TACAIEnemiesPop, KickStart.CommitDeathMode);
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
                EnoughLocalTechsSanityCheck();
            }

            if (KickStart.AirEnemiesSpawnRate == 0)
                KickStart.AllowAirEnemiesToSpawn = false;
            else
            {
                SpecialAISpawner.AirSpawnInterval = 60 / KickStart.AirEnemiesSpawnRate;
                KickStart.AllowAirEnemiesToSpawn = true;
            }
        }

        internal static void PushExtModOptionsHandling(ModHelper.ModConfig thisModConfig)
        {
            PushExtModOptionsHandling();
            if (thisModConfig != null)
                Nuterra.NativeOptions.NativeOptionsMod.onOptionsSaved.AddListener(() => { thisModConfig.WriteConfigJsonFile(); });
        }
    }
}
