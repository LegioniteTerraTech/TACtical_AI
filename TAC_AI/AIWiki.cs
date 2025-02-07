using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TerraTechETCUtil;
using TAC_AI.AI;
using TAC_AI.Templates;
using TAC_AI.World;
using System.Diagnostics;
using RandomAdditions;
using System.IO;

namespace TAC_AI
{
    internal class AIWiki
    {
        private static string modID => KickStart.ModID;
        private static Sprite lineSPrite;
        private static Sprite nullSprite;
        private static WikiPageInfo RTSJump;
        private static WikiPageInfo Airborne;
        internal static HashSet<BlockTypes> AllValidAIs = new HashSet<BlockTypes>();

        internal static void InsureAllValidAIs()
        {
            AllValidAIs.Clear();
            AllValidAIs.Add(BlockTypes.GSOAIGuardController_111);
            AllValidAIs.Add(BlockTypes.GSOAnchorAI_121);
            AllValidAIs.Add(BlockTypes.GCAIModuleGuard_222);
            AllValidAIs.Add(BlockTypes.BF_AIModule_Guard_212);
            AllValidAIs.Add(BlockTypes.HE_AIModule_Guard_112);
            AllValidAIs.Add(BlockTypes.HE_AITurret_112);
            AllValidAIs.Add(BlockTypes.SJ_Cab_AI_122);
            AllValidAIs.Add(BlockTypes.VENAIGuardModule_111);
            foreach (var item in ManMods.inst.IterateModdedBlocks())
            {
                var prefab = ManSpawn.inst.GetBlockPrefab(item);
                if (prefab && prefab.GetComponent<ModuleAIExtension>())
                    AllValidAIs.Add(item);
            }
        }

        internal static void InitWiki()
        {
            var tex = (Texture2D)ManWorldRTS.GetLineMat().mainTexture;
            lineSPrite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                RawTechExporter.GuardAIIcon.pivot);
            nullSprite = ManUI.inst.GetSprite(ObjectTypes.Block, -1);
            InitMechanics();
            InitTypes();
            InitModes();
            InitHelpers();
        }
        private static void InitMechanics()
        {
            ManIngameWiki.WikiPageGroup AITypesGrouper = new ManIngameWiki.WikiPageGroup(modID,
               "Mechanics", RawTechExporter.aiIconsEnemy[AI.Enemy.EnemySmarts.Meh]);
            new WikiPageInfo(modID, "Upgraded Enemies", RawTechExporter.aiIconsEnemy[AI.Enemy.EnemySmarts.IntAIligent],
                PageEnemies, AITypesGrouper);
            new WikiPageInfo(modID, "Flying Enemies", RawTechExporter.aiIcons[AIType.Aviator],
                PageAirborne, AITypesGrouper);
            RTSJump = new WikiPageInfo(modID, "Commanding", lineSPrite, PageRTS, AITypesGrouper);
            new WikiPageInfo(modID, "Enemy Bases", RawTechExporter.aiIcons[AIType.Aegis],
                PageBases, AITypesGrouper);
            new WikiPageInfo(modID, "Enemy Sieges", RawTechExporter.aiIconsEnemy[AI.Enemy.EnemySmarts.Meh],
                PageSieges, AITypesGrouper);
        }

        private static void InitHelpers()
        {
            new WikiPageInfo(modID, "Tools", ManIngameWiki.ToolsSprite, PageTools);
        }
        internal static void OpenInExplorer(string directory)
        {
            switch (SystemInfo.operatingSystemFamily)
            {
                case OperatingSystemFamily.MacOSX:
                    Process.Start(new ProcessStartInfo("file://" + directory));
                    break;
                case OperatingSystemFamily.Linux:
                case OperatingSystemFamily.Windows:
                    Process.Start(new ProcessStartInfo("explorer.exe", directory));
                    break;
                default:
                    throw new Exception("This operating system is UNSUPPORTED by RandomAdditions");
            }
        }
        internal static void PageTools()
        {
            AltUI.Sprite(nullSprite, AltUI.TextfieldBorderedBlue, GUILayout.Height(128), GUILayout.Width(128));

            GUILayout.BeginVertical(AltUI.TextfieldBlackHuge);
            GUILayout.Label("RawTech Files", AltUI.LabelBlueTitle);

            if (DebugRawTechSpawner.CanOpenDebugSpawnMenu)
            {
                if (GUILayout.Button("Population Exporter", AltUI.ButtonBlueLarge))
                    DebugRawTechSpawner.LaunchSubMenuClickable();
            }
            else
            {
                if (GUILayout.Button("Population Exporter(Only In Creative/R&D)", AltUI.ButtonGreyLarge))
                { 
                }
            }
            if (GUILayout.Button("Reload ALL Raw Techs", AltUI.ButtonBlueLarge))
            {
                RawTechExporter.inst.ReloadRawTechLoader();
            }
            if (GUILayout.Button("Open Raw Techs", AltUI.ButtonOrangeLarge))
            {
                string path = Path.Combine(new DirectoryInfo(Application.dataPath).Parent.ToString(), "Raw Techs");
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                OpenInExplorer(path);
            }
            GUILayout.EndVertical();
        }

        /// <summary>
        /// INCOMPLETE
        /// </summary>
        private static void PageEnemies()
        {
            AltUI.Sprite(RawTechExporter.aiIconsEnemy[AI.Enemy.EnemySmarts.IntAIligent], AltUI.TextfieldBorderedBlue, GUILayout.Height(128), GUILayout.Width(128));

            GUILayout.BeginVertical(AltUI.TextfieldBlackHuge);
            GUILayout.Label(AltUI.UIObjectiveMarkerText +
                "Non-Player Prospectors" + AltUI.UIEndColor + " now think more intelligently, and can build and " +
                AltUI.UIEnemyText + "destroy" + AltUI.UIEndColor + " others, and maybe even you as well!" +
                "\n\n  Early-game they may be weak, but later on expect fierce competition!", AltUI.LabelWhite);

            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();

        }
        private static void PageAirborne()
        {
            GUILayout.BeginHorizontal();
            AltUI.Sprite(RawTechExporter.aiIcons[AIType.Aviator], AltUI.TextfieldBorderedBlue, GUILayout.Height(128), GUILayout.Width(128));
            GUILayout.BeginVertical(AltUI.TextfieldBlackHuge);

            float airborneSpawnChance = SpecialAISpawner.AirborneAISpawnOdds / SpecialAISpawner.AirborneSpawnChance;
            GUILayout.Label("Chances:", AltUI.LabelRedTitle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Appear: ", AltUI.LabelWhite);
            GUILayout.Label(airborneSpawnChance.ToString("P"), AltUI.LabelBlue);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Aircraft: ", AltUI.LabelWhite);
            GUILayout.Label((Mathf.Clamp01(1 - SpecialAISpawner.SpaceSpawnChance)).ToString("P"), AltUI.LabelBlue);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Spaceship: ", AltUI.LabelWhite);
            GUILayout.Label(SpecialAISpawner.SpaceSpawnChance.ToString("P"), AltUI.LabelBlue);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Only Spaceships Above: ", AltUI.LabelRed);
            GUILayout.Label(((int)SpecialAISpawner.SpaceBeginAltitude).ToString(), AltUI.LabelBlue);
            GUILayout.Label(" km", AltUI.LabelBlue);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(AltUI.TextfieldBlackHuge);
            GUILayout.Label(AltUI.UIObjectiveMarkerText +
                "Intrepid Prospectors" + AltUI.UIEndColor + " are very mobile prospectors that can " +
                AltUI.UIEnemyText + "attack" + AltUI.UIEndColor + " you by surpise." +
                "\n\n  Early-game they have no interest, but late-game they may begin to strike!", AltUI.LabelWhite);

            GUILayout.Label("Aggression Grades:", AltUI.LabelRedTitle);
            GUILayout.BeginVertical(AltUI.TextfieldBordered);
            foreach (var item in SpecialAISpawner.AirAggressionGrades)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(item.Key.ToString(), AltUI.LabelBlack);
                GUILayout.Label(": ", AltUI.LabelBlack);
                GUILayout.Label(item.Value.ToString(), AltUI.LabelBlack);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6);
            GUILayout.Label("All Corps Attack after", AltUI.LabelRed);
            GUILayout.BeginHorizontal();
            GUILayout.Label("GSO: 4", AltUI.LabelBlack);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();

        }
        private static void PageRTSGoHereDesc()
        {
            GUILayout.Label("AKA: Drive To, Move Command", AltUI.LabelBlackTitle);
            GUILayout.BeginHorizontal();
            AltUI.Sprite(lineSPrite, AltUI.TextfieldBorderedBlue, GUILayout.Height(128), GUILayout.Width(128));
            GUILayout.BeginVertical(AltUI.TextfieldBordered);
            GUILayout.Label("Needs:", AltUI.LabelRedTitle);
            GUILayout.Label("+ Alive Player as Commander", AltUI.TextfieldBlackHuge);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(AltUI.TextfieldBlackHuge);
            GUILayout.Label(AltUI.UIObjectiveMarkerText +
                "Go Here" + AltUI.UIEndColor + " lets the A.I. follow waypoints.  \n\nYou can set waypoints by" +
                "pressing your Command Hotkey " + AltUI.HighlightString("[" + KickStart.CommandHotkey + "]") + ", " +
                AltUI.UIObjectiveMarkerText + "[Left-Mouse]" + AltUI.UIEndColor + " on the " + AltUI.UIBlueText + "Tech" + AltUI.UIEndColor +
                " you want to command, then by pressing " + AltUI.UIObjectiveMarkerText + "[Right-Mouse]" + AltUI.UIEndColor +
                " on the object you want the A.I. to interact with.", AltUI.LabelWhite);
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
        }
        private static void PageRTS()
        {
            GUILayout.Label("Go Here", AltUI.LabelBlackTitle);
            PageRTSGoHereDesc();
        }

        /// <summary>
        /// INCOMPLETE
        /// </summary>
        private static void PageBases()
        {
            GUILayout.Label("AKA: Founders, Siegers", AltUI.LabelBlackTitle);
            GUILayout.BeginHorizontal();
            AltUI.Sprite(RawTechExporter.aiIcons[AIType.Aegis], AltUI.TextfieldBorderedBlue, GUILayout.Height(128), GUILayout.Width(128));
            GUILayout.BeginVertical(AltUI.TextfieldBlackHuge);

            GUILayout.Label("Chances:", AltUI.LabelRedTitle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Appear:", AltUI.LabelWhite);
            GUILayout.Label((AIGlobals.EnemyBaseMakerChance / 100).ToString("P"), AltUI.LabelBlue);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            float friendlyBaseChance = AIGlobals.NonHostileBaseChance * AIGlobals.FriendlyBaseChance;

            GUILayout.BeginHorizontal();
            GUILayout.Label("Enemy:", AltUI.LabelWhite);
            GUILayout.Label((Mathf.Clamp01(1 - friendlyBaseChance - AIGlobals.NonHostileBaseChance)).ToString("P"), AltUI.LabelBlue);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Neutral:", AltUI.LabelWhite);
            GUILayout.Label(AIGlobals.NonHostileBaseChance.ToString("P"), AltUI.LabelBlue);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Friendly:", AltUI.LabelWhite);
            GUILayout.Label(friendlyBaseChance.ToString("P"), AltUI.LabelBlue);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(AltUI.TextfieldBlackHuge);
            GUILayout.Label(AltUI.UIObjectiveMarkerText +
                "Rival Prospectors" + AltUI.UIEndColor + " can build " +
                AltUI.UIEnemyText + "bases" + AltUI.UIEndColor + " around your turf." +
                " They will then continue to expand, " +
                (KickStart.AllowEnemiesToMine ? "mining out your area  " : "attacking your " + AltUI.UIBlueText + "Techs" + AltUI.UIEndColor) +
                " to stake YOUR claim as their own.  Beware, even if you run away, they can still launch attacks from afar.\n\n" +
                "The best course of action is to take them out as soon as possible, if you can that is.  Keep them" +
                " under control with some well-prepared A.I.s set to " + AltUI.UIObjectiveMarkerText +
                "Scout" + AltUI.UIEndColor, AltUI.LabelWhite);
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
        }
        private static void PageSieges()
        {
            GUILayout.Label("AKA: White Banner, \"Invasion\"", AltUI.LabelBlackTitle);
            GUILayout.BeginHorizontal();
            AltUI.Sprite(RawTechExporter.aiIconsEnemy[AI.Enemy.EnemySmarts.Meh], AltUI.TextfieldBorderedBlue, GUILayout.Height(128), GUILayout.Width(128));
            GUILayout.BeginVertical(AltUI.TextfieldBlackHuge);

            GUILayout.Label("Chances:", AltUI.LabelRedTitle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Approximate Range:", AltUI.LabelWhite);
            GUILayout.Label((ManEnemyWorld.BaseSightRadius * ManWorld.inst.TileSize).ToString(), AltUI.LabelBlue);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(AltUI.TextfieldBlackHuge);
            GUILayout.Label(AltUI.UIObjectiveMarkerText +
                "Rival Prospectors" + AltUI.UIEndColor + " will appear every now and then to " +
                AltUI.UIEnemyText + "challenge" + AltUI.UIEndColor + " your turf by setting up close or within it." +
                " They will then continue to expand, " +
                (KickStart.AllowEnemiesToMine ? "mining out your area  " : "attacking your " + AltUI.UIBlueText + "Techs" + AltUI.UIEndColor) +
                " to stake YOUR claim as their own.  Beware, even if you run away, they can still launch attacks from afar.\n\n" +
                "The best course of action is to take them out as soon as possible, if you can that is.  Keep them" +
                " under control with some well-prepared A.I.s set to " + AltUI.UIObjectiveMarkerText +
                "Scout" + AltUI.UIEndColor + ".", AltUI.LabelWhite);
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
        }


        private static void InitTypes()
        {
            ManIngameWiki.WikiPageGroup AITypesGrouper = new ManIngameWiki.WikiPageGroup(modID,
               "A.I. Drivers", RawTechExporter.aiIcons[AIType.Escort]);
            new WikiPageInfo(modID, "Tank", RawTechExporter.aiIcons[AIType.Escort], PageTank, AITypesGrouper);
            Airborne = new WikiPageInfo(modID, "Airborne", RawTechExporter.aiIcons[AIType.Aviator], PageAir, AITypesGrouper);
            new WikiPageInfo(modID, "Rocket", RawTechExporter.aiIcons[AIType.Astrotech], PageSpace, AITypesGrouper);
            new WikiPageInfo(modID, "Ship", RawTechExporter.aiIcons[AIType.Buccaneer], PageSea, AITypesGrouper);
        }
        private static void PageTank()
        {
            GUILayout.Label("AKA: Car, Vehicle", AltUI.LabelBlackTitle);
            GUILayout.BeginHorizontal();
            AltUI.Sprite(RawTechExporter.aiIcons[AIType.Escort], AltUI.TextfieldBorderedBlue, GUILayout.Height(128), GUILayout.Width(128));
            GUILayout.BeginVertical(AltUI.TextfieldBlackHuge);
            GUILayout.Label("Needs:", AltUI.LabelRedTitle);
            GUILayout.Label("+ Land Drive", AltUI.TextfieldBlackHuge);
            ManIngameWiki.Tooltip.GUITooltip("Working Wheels, Tank Tracks, and/or Legs");
            GUILayout.Label("- OPTIONAL: Drills to ignore trees", AltUI.TextfieldBlackHuge);
            ManIngameWiki.Tooltip.GUITooltip("Trees can pop-up infront of moving Techs");
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(AltUI.TextfieldBordered);
            GUILayout.Label("Capable:", AltUI.LabelBlueTitle);
            foreach (var item2 in AllValidAIs)
            {
                var prefab = ManSpawn.inst.GetBlockPrefab(item2)?.GetComponent<ModuleAIExtension>();
                if (prefab)
                {
                    var wikiLink = new ManIngameWiki.WikiLink(ManIngameWiki.GetBlockPage(prefab.name));
                    if (wikiLink.OnGUI(AltUI.LabelBlue))
                        wikiLink.linked.GoHere();
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(AltUI.TextfieldBlackHuge);
            GUILayout.Label(AltUI.UIObjectiveMarkerText +
                "Cars & Tanks" + AltUI.UIEndColor + " are the bread and butter of any prospecting operation.  \n\nThe standard land " + AltUI.UIBlueText +
                "Tech" + AltUI.UIEndColor + " serves as the cheapest way to get many things done.\n\n" +
                "However they can't do much as they can't float over trees, or drive into water.", AltUI.LabelWhite);
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
        }
        private static void PageAir()
        {
            GUILayout.Label("AKA: Plane, Rotorcraft", AltUI.LabelBlackTitle);
            GUILayout.BeginHorizontal();
            AltUI.Sprite(RawTechExporter.aiIcons[AIType.Aviator], AltUI.TextfieldBorderedBlue, GUILayout.Height(128), GUILayout.Width(128));
            GUILayout.BeginVertical(AltUI.TextfieldBlackHuge);
            GUILayout.Label("Needs:", AltUI.LabelRedTitle);
            GUILayout.Label("PLANE:", AltUI.LabelBlue);
            GUILayout.Label("+ Wings", AltUI.TextfieldBlackHuge);
            ManIngameWiki.Tooltip.GUITooltip("Spoilers don't count!");
            GUILayout.Label("+ Forwards Thrust", AltUI.TextfieldBlackHuge);
            ManIngameWiki.Tooltip.GUITooltip("Propellers, Steering Hovers, and/or Boosters with Fuel Tanks.");
            GUILayout.Label("- OPTIONAL: Crash protection", AltUI.TextfieldBlackHuge);
            ManIngameWiki.Tooltip.GUITooltip("Emergency landings caused by damage");
            GUILayout.Label("HELICOPTER:", AltUI.LabelBlue);
            GUILayout.Label("+ Upwards Thrust", AltUI.TextfieldBlackHuge);
            ManIngameWiki.Tooltip.GUITooltip("Propellers, Steering Hovers, and/or Boosters with Fuel Tanks.");
            GUILayout.Label("- OPTIONAL: Landing gear", AltUI.TextfieldBlackHuge);
            ManIngameWiki.Tooltip.GUITooltip("Emergency landings caused by damage");
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(AltUI.TextfieldBordered);
            GUILayout.Label("Capable:", AltUI.LabelWhiteTitle);
            foreach (var item2 in AllValidAIs)
            {
                var prefab = ManSpawn.inst.GetBlockPrefab(item2)?.GetComponent<ModuleAIExtension>();
                if (prefab && prefab.Aviator)
                {
                    var wikiLink = new ManIngameWiki.WikiLink(ManIngameWiki.GetBlockPage(prefab.name));
                    if (wikiLink.OnGUI(AltUI.LabelBlue))
                        wikiLink.linked.GoHere();
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(AltUI.TextfieldBlackHuge);
            GUILayout.Label(AltUI.UIObjectiveMarkerText +
                "Aircraft" + AltUI.UIEndColor + " are expensive but make the fastest " + AltUI.UIBlueText +
                "Techs" + AltUI.UIEndColor + " that are usually unchallenged by ground dangers. \n\nAirborne " + AltUI.UIBlueText +
                "Techs" + AltUI.UIEndColor + " also have no problems going over Trees and Rocks, permitting" +
                " a means of escape when dealing with enemies.  \n\n" +
                "However they tend to be frail and attacking with them head-on isn't safe.", AltUI.LabelWhite);
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
        }
        private static void PageSpace()
        {
            GUILayout.Label("AKA: Spaceship, Antigrav", AltUI.LabelBlackTitle);
            GUILayout.BeginHorizontal();
            AltUI.Sprite(RawTechExporter.aiIcons[AIType.Astrotech], AltUI.TextfieldBorderedBlue, GUILayout.Height(128), GUILayout.Width(128));
            GUILayout.BeginVertical(AltUI.TextfieldBlackHuge);
            GUILayout.Label("Needs:", AltUI.LabelRedTitle);
            GUILayout.Label("+ Hovering Capabilities", AltUI.TextfieldBlackHuge);
            ManIngameWiki.Tooltip.GUITooltip("Antigravity Engines, Hoverbug, and/or lots of Steering Hovers");
            GUILayout.Label("- OPTIONAL: Gyroscopes", AltUI.TextfieldBlackHuge);
            ManIngameWiki.Tooltip.GUITooltip("AI already self-aligns but Gyros can stop roll when turning");
            GUILayout.Label("- OPTIONAL: Bombs", AltUI.TextfieldBlackHuge);
            ManIngameWiki.Tooltip.GUITooltip("To ruin someone's day, you can use a ton of them");
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(AltUI.TextfieldBordered);
            GUILayout.Label("Capable:", AltUI.LabelWhiteTitle);
            foreach (var item2 in AllValidAIs)
            {
                var prefab = ManSpawn.inst.GetBlockPrefab(item2)?.GetComponent<ModuleAIExtension>();
                if (prefab && prefab.Astrotech)
                {
                    var wikiLink = new ManIngameWiki.WikiLink(ManIngameWiki.GetBlockPage(prefab.name));
                    if (wikiLink.OnGUI(AltUI.LabelBlue))
                        wikiLink.linked.GoHere();
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(AltUI.TextfieldBlackHuge);
            GUILayout.Label(AltUI.UIObjectiveMarkerText +
                "Spacecraft" + AltUI.UIEndColor + " meander in the late-game as the ultimate mode of transportation.  " +
                "\n\n  Space-fairing " + AltUI.UIBlueText +
                "Techs" + AltUI.UIEndColor + " can float freely in all directions and can stop and remain mostly and neatly stationary in the sky, " +
                "raining down heck-fire on anything below.  They are usually quite bulky and very difficult to take down.\n\n" +
                "However they cost a lot to make, and tend to be sluggish against the more agile " + AltUI.UIObjectiveMarkerText +
                "Aircraft" + AltUI.UIEndColor + ".", AltUI.LabelWhite);
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
        }
        private static void PageSea()
        {
            GUILayout.Label("AKA: Warship, Floater", AltUI.LabelBlackTitle);
            GUILayout.BeginHorizontal();
            AltUI.Sprite(RawTechExporter.aiIcons[AIType.Buccaneer], AltUI.TextfieldBorderedBlue, GUILayout.Height(128), GUILayout.Width(128));
            GUILayout.BeginVertical(AltUI.TextfieldBlackHuge);
            GUILayout.Label("Needs:", AltUI.LabelRedTitle);
            if (GUILayout.Button("+ The Water Mod", AltUI.TextfieldBlackHuge))
                ManSteamworks.inst.OpenOverlayURL("https://steamcommunity.com/sharedfiles/filedetails/?id=2757919307");
            ManIngameWiki.Tooltip.GUITooltip("On the Steam Workshop [LINK]");
            GUILayout.Label("+ Floatation", AltUI.TextfieldBlackHuge);
            ManIngameWiki.Tooltip.GUITooltip("Enough lightweight blocks, Antigravity Engines, Hoverbug, and/or Steering Hovers");
            GUILayout.Label("+ Forwards Thrust", AltUI.TextfieldBlackHuge);
            ManIngameWiki.Tooltip.GUITooltip("Propellers and/or Steering Hovers.");
            GUILayout.Label("- OPTIONAL: Rudders", AltUI.TextfieldBlackHuge);
            ManIngameWiki.Tooltip.GUITooltip("Wings as rudders at the back to reduce drifting");
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(AltUI.TextfieldBordered);
            GUILayout.Label("Capable:", AltUI.LabelWhiteTitle);
            foreach (var item2 in AllValidAIs)
            {
                var prefab = ManSpawn.inst.GetBlockPrefab(item2)?.GetComponent<ModuleAIExtension>();
                if (prefab && prefab.Buccaneer)
                {
                    var wikiLink = new ManIngameWiki.WikiLink(ManIngameWiki.GetBlockPage(prefab.name));
                    if (wikiLink.OnGUI(AltUI.LabelBlue))
                        wikiLink.linked.GoHere();
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(AltUI.TextfieldBlackHuge);
            GUILayout.Label(AltUI.UIObjectiveMarkerText +
                "Ships" + AltUI.UIEndColor + " float in the water, where all other types of Tech seem to struggle in.  " +
                "\n\nFloating " + AltUI.UIBlueText +
                "Techs" + AltUI.UIEndColor + " can go anywhere in the sea, effortlessly picking off whomever dares to enter the sea.\n\n" +
                "However, it can only go where the sea can reach.", AltUI.LabelWhite);
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
        }

        private static void InitModes()
        {
            ManIngameWiki.WikiPageGroup AITypesGrouper = new ManIngameWiki.WikiPageGroup(modID,
               "A.I. Modes", RawTechExporter.GuardAIIcon);
            new WikiPageInfo(modID, "Guard Player", RawTechExporter.GuardAIIcon, PageEscort, AITypesGrouper);
            new WikiPageInfo(modID, "Go Here", lineSPrite, PageGoHere, AITypesGrouper);
            new WikiPageInfo(modID, "Harvest", RawTechExporter.aiIcons[AIType.Prospector], PageMine, AITypesGrouper);
            new WikiPageInfo(modID, "Protect", RawTechExporter.aiIcons[AIType.Aegis], PageAegis, AITypesGrouper);
            new WikiPageInfo(modID, "Scout", RawTechExporter.aiIcons[AIType.Assault], PageAssault, AITypesGrouper);
            new WikiPageInfo(modID, "Fetch", RawTechExporter.aiIcons[AIType.Scrapper], PageScrapper, AITypesGrouper);
            new WikiPageInfo(modID, "Charger", RawTechExporter.aiIcons[AIType.Energizer], PageEnergizer, AITypesGrouper);
            new WikiPageInfo(modID, "Part", RawTechExporter.aiIcons[AIType.MTStatic], PageStatic, AITypesGrouper);
            new WikiPageInfo(modID, "Turret", RawTechExporter.aiIcons[AIType.MTTurret], PageTurret, AITypesGrouper);
            new WikiPageInfo(modID, "Mimic", RawTechExporter.aiIcons[AIType.MTMimic], PageMimic, AITypesGrouper);
        }
        private static void PageEscort()
        {
            GUILayout.Label("AKA: Follow, Guard", AltUI.LabelBlackTitle);
            GUILayout.BeginHorizontal();
            AltUI.Sprite(RawTechExporter.GuardAIIcon, AltUI.TextfieldBorderedBlue, GUILayout.Height(128), GUILayout.Width(128));
            GUILayout.BeginVertical(AltUI.TextfieldBordered);
            GUILayout.Label("Needs:", AltUI.LabelRedTitle);
            GUILayout.Label("+ Ability to move\n+ Alive Player to Guard", AltUI.TextfieldBlackHuge);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(AltUI.TextfieldBordered);
            GUILayout.Label("Capable:", AltUI.LabelWhiteTitle);
            foreach (var item2 in AllValidAIs)
            {
                var prefab = ManSpawn.inst.GetBlockPrefab(item2)?.GetComponent<ModuleAIExtension>();
                if (prefab)
                {
                    var wikiLink = new ManIngameWiki.WikiLink(ManIngameWiki.GetBlockPage(prefab.name));
                    if (wikiLink.OnGUI(AltUI.LabelBlue))
                        wikiLink.linked.GoHere();
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(AltUI.TextfieldBlackHuge);
            GUILayout.Label(AltUI.UIObjectiveMarkerText +
                "Escorts" + AltUI.UIEndColor + " defend you.  " +
                "\n\nMost useful for coordinated attacks against a large enemy.  Make sure to use your Retreat Hotkey " +
                AltUI.HighlightString("[" + KickStart.RetreatHotkey + "]") +
                " to keep them by your side while pressing on the attack if you don't want them wandering off!", AltUI.LabelWhite);
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
        }
        private static void PageGoHere()
        {
            PageRTSGoHereDesc();
            if (GUILayout.Button("Jump To RTS Section", AltUI.ButtonOrangeLarge, GUILayout.Height(40)))
            {
                RTSJump.GoHere();
            }
        }
        private static void PageMine()
        {
            GUILayout.Label("AKA: Mine, Prospector", AltUI.LabelBlackTitle);
            GUILayout.BeginHorizontal();
            AltUI.Sprite(RawTechExporter.aiIcons[AIType.Prospector], AltUI.TextfieldBorderedBlue, GUILayout.Height(128), GUILayout.Width(128));
            GUILayout.BeginVertical(AltUI.TextfieldBordered);
            GUILayout.Label("Needs:", AltUI.LabelRedTitle);
            GUILayout.Label("+ Base with Receivers\n+ Own Collectors\n+ Resources Nearby", AltUI.TextfieldBlackHuge);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(AltUI.TextfieldBordered);
            GUILayout.Label("Capable:", AltUI.LabelWhiteTitle);
            foreach (var item2 in AllValidAIs)
            {
                var prefab = ManSpawn.inst.GetBlockPrefab(item2)?.GetComponent<ModuleAIExtension>();
                if (prefab && prefab.Prospector)
                {
                    var wikiLink = new ManIngameWiki.WikiLink(ManIngameWiki.GetBlockPage(prefab.name));
                    if (wikiLink.OnGUI(AltUI.LabelBlue))
                        wikiLink.linked.GoHere();
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(AltUI.TextfieldBlackHuge);
            GUILayout.Label(AltUI.UIObjectiveMarkerText + "Harvesters" + AltUI.UIEndColor +
                " mine out everything in sight to gather " + AltUI.UIBuyText +
                "Resources" + AltUI.UIEndColor + " as well as make it easier to drive around on land.", AltUI.LabelWhite);
            if (GUILayout.Button(AltUI.UIObjectiveMarkerText + "Aircraft A.I." + AltUI.UIEndColor +
                " can also harvest, although their usefulness is quite limited.", AltUI.LabelWhite))
                Airborne.GoHere();
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
        }
        private static void PageAegis()
        {
            GUILayout.Label("AKA: Defend Others, Watchdog", AltUI.LabelBlackTitle);
            GUILayout.BeginHorizontal();
            AltUI.Sprite(RawTechExporter.aiIcons[AIType.Aegis], AltUI.TextfieldBorderedBlue, GUILayout.Height(128), GUILayout.Width(128));
            GUILayout.BeginVertical(AltUI.TextfieldBordered);
            GUILayout.Label("Needs:", AltUI.LabelRedTitle);
            GUILayout.Label("+ Teammates to Protect", AltUI.TextfieldBlackHuge);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(AltUI.TextfieldBlackHuge);
            GUILayout.Label(AltUI.UIObjectiveMarkerText + "Protectors" + AltUI.UIEndColor +
                " defend your " + AltUI.UIBlueText +
                "Techs" + AltUI.UIEndColor + " and distract " + AltUI.UIBlueText +
                "Enemies" + AltUI.UIEndColor + " from attacking your more valuable " + 
                AltUI.UIObjectiveMarkerText + "Bases" + AltUI.UIEndColor + ", " +
                AltUI.UIObjectiveMarkerText + "Harvesters" + AltUI.UIEndColor + ", " +
                AltUI.UIObjectiveMarkerText + "Fetchers" + AltUI.UIEndColor + ", and " +
                AltUI.UIObjectiveMarkerText + "Chargers" + AltUI.UIEndColor +
                ".", AltUI.LabelWhite);
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
        }
        private static void PageAssault()
        {
            GUILayout.Label("AKA: Assault, Raider", AltUI.LabelBlackTitle);
            GUILayout.BeginHorizontal();
            AltUI.Sprite(RawTechExporter.aiIcons[AIType.Assault], AltUI.TextfieldBorderedBlue, GUILayout.Height(128), GUILayout.Width(128));
            GUILayout.BeginVertical(AltUI.TextfieldBordered);
            GUILayout.Label("Needs:", AltUI.LabelRedTitle);
            GUILayout.Label("+ Base with Generators and Wireless Chargers\n+ Own Batteries and Shields", AltUI.TextfieldBlackHuge);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(AltUI.TextfieldBordered);
            GUILayout.Label("Capable:", AltUI.LabelWhiteTitle);
            foreach (var item2 in AllValidAIs)
            {
                var prefab = ManSpawn.inst.GetBlockPrefab(item2)?.GetComponent<ModuleAIExtension>();
                if (prefab && prefab.Assault)
                {
                    var wikiLink = new ManIngameWiki.WikiLink(ManIngameWiki.GetBlockPage(prefab.name));
                    if (wikiLink.OnGUI(AltUI.LabelBlue))
                        wikiLink.linked.GoHere();
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(AltUI.TextfieldBlackHuge);
            GUILayout.Label(AltUI.UIObjectiveMarkerText + "Scouts" + AltUI.UIEndColor +
                " automatically ward off " + AltUI.UIEnemyText + "Enemies" + AltUI.UIEndColor + 
                " to prevent them from building " + AltUI.UIObjectiveMarkerText +
                "Bases" + AltUI.UIEndColor + " on your turf.", AltUI.LabelWhite);
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
        }
        private static void PageScrapper()
        {
            GUILayout.Label("AKA: Collect blocks, Scrapper", AltUI.LabelBlackTitle);
            GUILayout.BeginHorizontal();
            AltUI.Sprite(RawTechExporter.aiIcons[AIType.Scrapper], AltUI.TextfieldBorderedBlue, GUILayout.Height(128), GUILayout.Width(128));
            GUILayout.BeginVertical(AltUI.TextfieldBordered);
            GUILayout.Label("Needs:", AltUI.LabelRedTitle);
            GUILayout.Label("+ Base with Scrapper or SCU \n- OPTIONAL: Scrap Magnet", AltUI.TextfieldBlackHuge);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(AltUI.TextfieldBordered);
            GUILayout.Label("Capable:", AltUI.LabelWhiteTitle);
            foreach (var item2 in AllValidAIs)
            {
                var prefab = ManSpawn.inst.GetBlockPrefab(item2)?.GetComponent<ModuleAIExtension>();
                if (prefab && prefab.Scrapper)
                {
                    var wikiLink = new ManIngameWiki.WikiLink(ManIngameWiki.GetBlockPage(prefab.name));
                    if (wikiLink.OnGUI(AltUI.LabelBlue))
                        wikiLink.linked.GoHere();
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(AltUI.TextfieldBlackHuge);
            GUILayout.Label(AltUI.UIObjectiveMarkerText + "Fetchers" + AltUI.UIEndColor +
                " collect any stray " + AltUI.UIBlueText +
                "Blocks" + AltUI.UIEndColor + " from the floor and return them to your " + AltUI.UIBlueText +
                "Scrappers & SCUs" + AltUI.UIEndColor + ", keeping your turf neat and tidy.", AltUI.LabelWhite);
            if (GUILayout.Button(AltUI.UIObjectiveMarkerText + "Aircraft A.I." + AltUI.UIEndColor +
                " can also fetch, although their usefulness is quite limited.", AltUI.LabelWhite))
                Airborne.GoHere();
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
        }
        private static void PageEnergizer()
        {
            GUILayout.Label("AKA: Tech Recharger, Energizer", AltUI.LabelBlackTitle);
            GUILayout.BeginHorizontal();
            AltUI.Sprite(RawTechExporter.aiIcons[AIType.Energizer], AltUI.TextfieldBorderedBlue, GUILayout.Height(128), GUILayout.Width(128));
            GUILayout.BeginVertical(AltUI.TextfieldBordered);
            GUILayout.Label("Needs:", AltUI.LabelRedTitle);
            GUILayout.Label("+ Base with Generators and Wireless Chargers\n+ Own Batteries and Wireless Charger", AltUI.TextfieldBlackHuge);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(AltUI.TextfieldBordered);
            GUILayout.Label("Capable:", AltUI.LabelWhiteTitle);
            foreach (var item2 in AllValidAIs)
            {
                var prefab = ManSpawn.inst.GetBlockPrefab(item2)?.GetComponent<ModuleAIExtension>();
                if (prefab && prefab.Energizer)
                {
                    var wikiLink = new ManIngameWiki.WikiLink(ManIngameWiki.GetBlockPage(prefab.name));
                    if (wikiLink.OnGUI(AltUI.LabelBlue))
                        wikiLink.linked.GoHere();
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(AltUI.TextfieldBlackHuge);
            GUILayout.Label(AltUI.UIObjectiveMarkerText + "Chargers" + AltUI.UIEndColor +
                " keep your " + AltUI.UIBlueText +
                "Techs" + AltUI.UIEndColor + " topped off with " + AltUI.UIBlueText +
                "Energy" + AltUI.UIEndColor + " and keep them ready for action.", AltUI.LabelWhite);
            if (GUILayout.Button(AltUI.UIObjectiveMarkerText + "Aircraft A.I." + AltUI.UIEndColor +
                " cannot charge Techs effectively and should never be used as chargers", AltUI.LabelWhite))
                Airborne.GoHere();
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
        }
        private static void PageMultiTech()
        {
            GUILayout.Label("The universal " + AltUI.UIObjectiveMarkerText + "Attach Point System" + AltUI.UIEndColor +
                " has a 64 length-width-height limit due to structual issues.\n\n  " + AltUI.UIObjectiveMarkerText +
                "Multi-Techs" + AltUI.UIEndColor + " are the solution to this issue.   " +
                "\n  A " + AltUI.UIObjectiveMarkerText + "Multi-Tech" + AltUI.UIEndColor + 
                " is composed of two or more " + AltUI.UIBlueText + "Techs" + AltUI.UIEndColor + 
                " that work as one.\n\nMaking two loops out of blocks on two seperate Techs to entangle them " +
                "together is a good way to start.  Later on it can involve entangling various parts like wings, " +
                "wheels, boosters, " + AltUI.UIObjectiveMarkerText + "Circuits & Systems" + AltUI.UIEndColor + 
                " and much, much more.  The possibilities really become endless with two Techs working together!\n" +
                 AltUI.UIObjectiveMarkerText + "Multi-Techs" + AltUI.UIEndColor + " are the future of bigger Techs. ", 
                 AltUI.LabelWhite);
            var wikiLink = new ManIngameWiki.WikiLink(ManIngameWiki.GetPage("Part"));
            if (wikiLink.OnGUI(AltUI.LabelBlue))
                wikiLink.linked.GoHere();

            wikiLink = new ManIngameWiki.WikiLink(ManIngameWiki.GetPage("Turret"));
            if (wikiLink.OnGUI(AltUI.LabelBlue))
                wikiLink.linked.GoHere();

            wikiLink = new ManIngameWiki.WikiLink(ManIngameWiki.GetPage("Mimic"));
            if (wikiLink.OnGUI(AltUI.LabelBlue))
                wikiLink.linked.GoHere();
        }
        private static void PageStatic()
        {
            GUILayout.Label("AKA: Multi-Tech Segment", AltUI.LabelBlackTitle);
            GUILayout.BeginHorizontal();
            AltUI.Sprite(RawTechExporter.aiIcons[AIType.MTStatic], AltUI.TextfieldBorderedBlue, GUILayout.Height(128), GUILayout.Width(128));
            GUILayout.BeginVertical(AltUI.TextfieldBordered);
            GUILayout.Label("Upgrade: Multi+", AltUI.LabelBlueTitle);
            GUILayout.Label("Allows this to talk with non-Player allied, controlled Techs", AltUI.TextfieldBlackHuge);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(AltUI.TextfieldBordered);
            GUILayout.Label("Capable:", AltUI.LabelWhiteTitle);
            foreach (var item2 in AllValidAIs)
            {
                var prefab = ManSpawn.inst.GetBlockPrefab(item2)?.GetComponent<ModuleAIExtension>();
                if (prefab)
                {
                    var wikiLink = new ManIngameWiki.WikiLink(ManIngameWiki.GetBlockPage(prefab.name));
                    if (wikiLink.OnGUI(AltUI.LabelBlue))
                        wikiLink.linked.GoHere();
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(AltUI.TextfieldBlackHuge);
            PageMultiTech();
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
        }
        private static void PageTurret()
        {
            GUILayout.Label("AKA: Multi-Tech Turret", AltUI.LabelBlackTitle);
            GUILayout.BeginHorizontal();
            AltUI.Sprite(RawTechExporter.aiIcons[AIType.MTTurret], AltUI.TextfieldBorderedBlue, GUILayout.Height(128), GUILayout.Width(128));
            GUILayout.BeginVertical(AltUI.TextfieldBordered);
            GUILayout.Label("Upgrade: Multi+", AltUI.LabelBlueTitle);
            GUILayout.Label("Allows this to talk with non-Player allied, controlled Techs", AltUI.TextfieldBlackHuge);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(AltUI.TextfieldBordered);
            GUILayout.Label("Capable:", AltUI.LabelWhiteTitle);
            foreach (var item2 in AllValidAIs)
            {
                var prefab = ManSpawn.inst.GetBlockPrefab(item2)?.GetComponent<ModuleAIExtension>();
                if (prefab)
                {
                    var wikiLink = new ManIngameWiki.WikiLink(ManIngameWiki.GetBlockPage(prefab.name));
                    if (wikiLink.OnGUI(AltUI.LabelBlue))
                        wikiLink.linked.GoHere();
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(AltUI.TextfieldBlackHuge);
            PageMultiTech();
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
        }
        private static void PageMimic()
        {
            GUILayout.Label("AKA: Multi-Tech Relay", AltUI.LabelBlackTitle);
            GUILayout.BeginHorizontal();
            AltUI.Sprite(RawTechExporter.aiIcons[AIType.MTMimic], AltUI.TextfieldBorderedBlue, GUILayout.Height(128), GUILayout.Width(128));
            GUILayout.BeginVertical(AltUI.TextfieldBordered);
            GUILayout.Label("Upgrade: Multi+", AltUI.LabelBlueTitle);
            GUILayout.Label("Allows this to talk with non-Player allied, controlled Techs", AltUI.TextfieldBlackHuge);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(AltUI.TextfieldBordered);
            GUILayout.Label("Capable:", AltUI.LabelWhiteTitle);
            foreach (var item2 in AllValidAIs)
            {
                var prefab = ManSpawn.inst.GetBlockPrefab(item2)?.GetComponent<ModuleAIExtension>();
                if (prefab)
                {
                    var wikiLink = new ManIngameWiki.WikiLink(ManIngameWiki.GetBlockPage(prefab.name));
                    if (wikiLink.OnGUI(AltUI.LabelBlue))
                        wikiLink.linked.GoHere();
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(AltUI.TextfieldBlackHuge);
            PageMultiTech();
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
        }


        internal static void ShowTeamInfoFirstTime(int Team)
        {
            switch (ManBaseTeams.GetRelationsWritablePriority(ManPlayer.inst.PlayerTeam, Team, TeamRelations.Enemy))
            {
                case TeamRelations.SubNeutral:
                case TeamRelations.Neutral:
                    hintRival.Show();
                    break;
                case TeamRelations.Friendly:
                    hintAllied.Show();
                    break;
            }
        }



        internal static LoadingHintsExt.LoadingHint loadHint1 = new LoadingHintsExt.LoadingHint(KickStart.ModID, "ADVANCED AI HINT",
            "Talk to prospectors with " + AltUI.HighlightString("T - Left-Click") + ".\nYou can't talk to " +
            AltUI.SideCharacterString("Prospectors") + " without bases or " + AltUI.EnemyString("Mission Objectives") +
            ".\nBuild Bucks are on the line!");

        internal static LoadingHintsExt.LoadingHint loadHint2 = new LoadingHintsExt.LoadingHint(KickStart.ModID, "ADVANCED AI HINT",
            "Don't buy out every " + AltUI.SideCharacterString("Prospector") + " you come across!" +
            "\nSave your money for later - " +
            AltUI.HighlightString("Bribing Techs costs at least <b>2 times more</b> than buying direct!"));

        internal static LoadingHintsExt.LoadingHint loadHint3 = new LoadingHintsExt.LoadingHint(KickStart.ModID, "ADVANCED AI HINT",
            "Try out the different AI modules and their modes in " + AltUI.ObjectiveString("Creative") + " first.\n" +
            AltUI.HighlightString("You will probably want to get to know them first."));

        internal static LoadingHintsExt.LoadingHint loadHint4 = new LoadingHintsExt.LoadingHint(KickStart.ModID, "CREATIVE MODE HINT",
            "You can experiment with the " + AltUI.ObjectiveString("Enemy Population") + ".\n" +
            AltUI.HighlightString("Hold Left <b>Ctrl</b>") + " and press " +
            AltUI.HighlightString("[<b>-</b>]") + " to access the special spawner.\nPress " +
            AltUI.HighlightString("[<b>=</b>]") + " instead to see local and other mod Enemy Techs.");

        internal static LoadingHintsExt.LoadingHint loadHint5 = new LoadingHintsExt.LoadingHint(KickStart.ModID, "ADVANCED AI HINT",
            "The "+ AltUI.EnemyString("Missile") + " knows where it is at all times.  It knows this because it  " + AltUI.ObjectiveString("knows where it isn't") + 
            ".\n" + AltUI.EnemyString("Huge Enemy Missiles") + " very rarely appear distant late-game, and will always take the most direct arc to their target." +
            "\nThe best means of defense is to stay mobile or small, because these rammers deal pain against " + AltUI.BlueString("Big Techs") + ".");

        internal static LoadingHintsExt.LoadingHint loadHint6 = new LoadingHintsExt.LoadingHint(KickStart.ModID, "CREATIVE MODE HINT",
            "At the upper-right corner when paused, you can add " + AltUI.HighlightString("<b>your own Techs</b>") +
            " to the " + AltUI.ObjectiveString("Enemy Population") + ".\nThey will then appear as " +
            AltUI.EnemyString("Enemies") + " in your campaign saves.");

        internal static LoadingHintsExt.LoadingHint loadHint7 = new LoadingHintsExt.LoadingHint(KickStart.ModID, "COMBAT HINT",
             AltUI.EnemyString("Smart Prospectors") + " will try to run away if they feel weak.  If you think you can, pursue " +
            "them and maybe you might find " + AltUI.HighlightString("their base") + " as well as some goodies.");

        internal static LoadingHintsExt.LoadingHint loadHint8 = new LoadingHintsExt.LoadingHint(KickStart.ModID, "ADVANCED AI HINT",
             AltUI.EnemyString("Intepid Prospectors") + " or " + AltUI.HighlightString("Spaceships") + 
            " appear more often the higher you are.  Shooting down from above " + 
            AltUI.HintString("will attract unwanted attention!"));

        internal static LoadingHintsExt.LoadingHint loadHint9 = new LoadingHintsExt.LoadingHint(KickStart.ModID, "ADVANCED AI HINT",
            "Your " + AltUI.HighlightString("Airborne A.I.") + " can harvest resources and pickup blocks!");
        internal static LoadingHintsExt.LoadingHint loadHint10 = new LoadingHintsExt.LoadingHint(KickStart.ModID, "ADVANCED AI HINT",
            "You can control fleets of AI with " + AltUI.HighlightString(KickStart.CommandHotkey.ToString()) + 
            " or through Camera mode, just drag a box around them!");
        internal static LoadingHintsExt.LoadingHint loadHint11 = new LoadingHintsExt.LoadingHint(KickStart.ModID, "ADVANCED AI HINT",
            "When the fight gets dicey you can order your minions to your side by pressing " + 
            AltUI.HighlightString(KickStart.RetreatHotkey.ToString()));
        internal static LoadingHintsExt.LoadingHint loadHint12 = new LoadingHintsExt.LoadingHint(KickStart.ModID, "ADVANCED AI HINT",
            "You can also open the AI Controls by pressing " +
            AltUI.HighlightString(KickStart.ModeSelect.ToString()));


        // Others
        internal static ExtUsageHint.UsageHint hintUpgrades = new ExtUsageHint.UsageHint(KickStart.ModID, "AIGlobals.hintUpgrades",
            "You A.I. can also be mouse-controlled through " + AltUI.ObjectiveString("RTS Mode") + " by toggling key " + 
            AltUI.HighlightString(KickStart.CommandHotkey.ToString()) + " or through Camera Mode.  To command, press " +
             AltUI.HighlightString(KickStart.ModeSelect.ToString()), 14);
        internal static ExtUsageHint.UsageHint hintADV = new ExtUsageHint.UsageHint(KickStart.ModID, "AIGlobals.hintADV",
            AltUI.HighlightString("Other Prospectors") + " have done their research, and are much more " +
            AltUI.EnemyString("scary") + " this time.  Be careful as they may also " + AltUI.ObjectiveString("gang up") + 
            " on you, or maybe they might be " +  AIGlobals.FriendlyColor.ToRGBA255().ColorString("Friendly") + "?  " + 
            AltUI.ThinkString("Who knows?"), 14);

        internal static ExtUsageHint.UsageHint hintAir = new ExtUsageHint.UsageHint(KickStart.ModID, "AIGlobals.hintAir",
            AltUI.EnemyString("Radical Prospectors") + " will fly " + AltUI.BlueString("Aircraft") +
            " for geographic survey commissions.  Whether or not that includes " + AltUI.ObjectiveString("airstrikes") +
            " is up to them.  " + AltUI.HintString("Plan on some AA or upwards shields."), 14);

        internal static ExtUsageHint.UsageHint hintSpace = new ExtUsageHint.UsageHint(KickStart.ModID, "AIGlobals.hintSpace",
            AltUI.EnemyString("Intrepid Prospectors") + " pilot " + AltUI.BlueString("Space Ships") +
            " for freight missions.  There's also a good chance they are here for " + AltUI.EnemyString("trouble") +
            ".  Sometimes outrunning them is simply not an option.  So play it safe or go " +
            AltUI.HighlightString("all in!"), 14);

        internal static ExtUsageHint.UsageHint hintShip = new ExtUsageHint.UsageHint(KickStart.ModID, "AIGlobals.hintShip",
            AltUI.HighlightString("Other Prospectors") + " may also appear on the water.  They are far more " +
            AltUI.EnemyString("dangerous") + " than your typical enemy.  Avoid the high seas or sink them, " +
            "the risk is worth the rewards.", 14);

        internal static ExtUsageHint.UsageHint hintTroll = new ExtUsageHint.UsageHint(KickStart.ModID, "AIGlobals.hintTroll",
            AltUI.EnemyString("Trader Trolls") +
            " may appear from time to time to give budding prospectors a rough time.", 14);


        internal static bool TooHighed = false;
        internal static ExtUsageHint.UsageHint hintSpaceTooHigh = new ExtUsageHint.UsageHint(KickStart.ModID, 
            "AIGlobals.hintSpaceTooHigh",
            "You have flown so high, you can almost see space!  " + AltUI.WhisperString("Wait a minute...") +
            "  There's more " + AltUI.EnemyString("Spaceships") + " up here!", 14);


        internal static ExtUsageHint.UsageHint hintBase = new ExtUsageHint.UsageHint(KickStart.ModID, "AIGlobals.hintBase",
            AltUI.HighlightString("Other Prospectors") + " will appear to stake their claim.  The " +
            AltUI.BlueString("Off-World") + " is a lawless frontier with blurred borders.  Stake your claim or go nomad, " +
            "no matter what, " + AltUI.HintString("they will come."), 14);
        internal static ExtUsageHint.UsageHint hintBaseInteract = new ExtUsageHint.UsageHint(KickStart.ModID, "AIGlobals.hintBaseInteract",
            "To interact with " + AltUI.SideCharacterString("Other Prospectors") + ", " + AltUI.HighlightString("hold T and Right-Mouse") +
            ".  " + AltUI.HintString("This may help you later on."), 12);


        internal static ExtUsageHint.UsageHint hintInvader = new ExtUsageHint.UsageHint(KickStart.ModID, "AIGlobals.hintRival",
            AltUI.EnemyString("Rival Prospectors") + " have a " + AltUI.EnemyString("Red") + " eye icon above themselves.  " +
            "They will attack YOUR turf for resources if they need to!  " + AltUI.HintString("Send them packing!"), 12);
        internal static ExtUsageHint.UsageHint hintRival = new ExtUsageHint.UsageHint(KickStart.ModID, "AIGlobals.hintSubNeutral",
            "<color=purple>Neutral Prospectors</color> have <color=purple>Purple</color> icons above them.  " +
            "They will neither attack you or your enemies.  They will watch over passerby, but " +
            AltUI.HintString("feel free to guard your lands from them."), 10);
        internal static ExtUsageHint.UsageHint hintAllied = new ExtUsageHint.UsageHint(KickStart.ModID, "AIGlobals.hintAllied",
            "<color=green>Allied Prospectors</color> have <color=green>Green</color> icons above them.  " +
            "They will help you " + AltUI.HighlightString("defend your turf") + " and will make no fuss.", 10);
        internal static ExtUsageHint.UsageHint hintSameTeam = new ExtUsageHint.UsageHint(KickStart.ModID, "AIGlobals.hintSameTeam",
            AltUI.HighlightString("Automated AI") + " on your team will automatically harvest and manage their resources.  " +
            "You can control them by hovering and selecting them with key " + AltUI.HighlightString(KickStart.NPTInteract.ToString()) + "", 12);

        internal static ExtUsageHint.UsageHint hintNPTSiege = new ExtUsageHint.UsageHint(KickStart.ModID, "AIGlobals.hintNPTSiege",
             AltUI.HighlightString("Sieges") + " happen when " + AltUI.EnemyString("Rival Prospectors") +
            " think the best option to manifest destiny is a " + AltUI.EnemyString("full on attack") +
            ".  Hold your ground or retreat, the choice is yours kiddo.", 10);
        internal static ExtUsageHint.UsageHint hintNPTRetreat = new ExtUsageHint.UsageHint(KickStart.ModID, "AIGlobals.hintNPTRetreat",
            AltUI.EnemyString("Rival Prospectors") + " when weak may " + AltUI.HighlightString("Fall Back") +
            " and regroup.  If you think you can take them out, go wild!", 10);



        internal static ExtUsageHint.UsageHint hintMissileWarning = new ExtUsageHint.UsageHint(KickStart.ModID, "SpecialAISpawner(A)",
            AltUI.HighlightString("ALERT! ") + AltUI.EnemyString("Huge Missile Detected"), 3.5f, true);
        internal static ExtUsageHint.UsageHint hintMissileDanger = new ExtUsageHint.UsageHint(KickStart.ModID, "SpecialAISpawner(B)",
            AltUI.EnemyString("HUGE MISSILE INBOUND"), 3.5f, true);

        internal static ExtUsageHint.UsageHint hintAirSafe = new ExtUsageHint.UsageHint(KickStart.ModID, "SpecialAISpawner(1)",
            AltUI.EnemyString("Hostile Aircraft") + " sighted!  It's not dangerous though.  " +
            AltUI.WhisperString("Phew!"));
        internal static ExtUsageHint.UsageHint hintAirWarning = new ExtUsageHint.UsageHint(KickStart.ModID, "SpecialAISpawner(2)",
            AltUI.EnemyString("Hostile Aircraft") + " sighted!  It's armed, so prepare yourself!");
        internal static ExtUsageHint.UsageHint hintAirDanger = new ExtUsageHint.UsageHint(KickStart.ModID, "SpecialAISpawner(3)",
            AltUI.EnemyString("Hostile Aircraft") + " inbound!", 3.5f, true);

        internal static ExtUsageHint.UsageHint hintSpaceSafe = new ExtUsageHint.UsageHint(KickStart.ModID, "SpecialAISpawner(4)",
            AltUI.EnemyString("Hostile Spaceship") + " has appeared!.  It's not interested in fighting though.  " +
            AltUI.WhisperString("hmmmm..."));
        internal static ExtUsageHint.UsageHint hintSpaceWarning = new ExtUsageHint.UsageHint(KickStart.ModID, "SpecialAISpawner(5)",
            AltUI.EnemyString("Hostile Spaceship") + " has appeared!.  It's armed, so prepare yourself!");
        internal static ExtUsageHint.UsageHint hintSpaceDanger = new ExtUsageHint.UsageHint(KickStart.ModID, "SpecialAISpawner(6)",
            AltUI.EnemyString("Hostile Spaceship inbound!"), 3.5f, true);
    }
}
