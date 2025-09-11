using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using TAC_AI.AI;
using TAC_AI.World;
using TAC_AI.Templates;
using TerraTechETCUtil;

namespace TAC_AI
{
    internal class GUIAIManager : MonoBehaviour
    {
        //Handles the display that's triggered on AI change 
        //  Circle hud wheel when the player assigns a new AI state
        //  TODO - add the hook needed to get the UI to pop up on Guard selection
        // NOTE: HANDLES RTS SELECTED AIS AS WELL
        private static GUIAIManager inst;
        public static Vector3 PlayerLoc = Vector3.zero;
        public static bool isCurrentlyOpen = false;
        private static AIType fetchAI = AIType.Escort;
        private static AIDriverType AIDriver = AIDriverType.Tank;
        private static AIType changeAI = AIType.Escort;
        internal static TankAIHelper lastTank;


        // Mode - Setting
        private static GameObject GUIWindow;
        private static Rect HotWindow = new Rect(0, 0, 200, 380);   // the "window"
        private static float xMenu = 0;
        private static float yMenu = 0;
        private static int SelfDestruct = 5;
        private static bool AdvancedToggles = false;

        // Tech Tracker
        private static float windowTimer = 0;
        private const int AIManagerID = 8001;


        internal static void Initiate()
        {
            if (inst)
                return;
            inst = Instantiate(new GameObject()).AddComponent<GUIAIManager>();
            Singleton.Manager<ManTechs>.inst.TankDriverChangedEvent.Subscribe(OnPlayerSwap);
            if (GUIWindow == null)
            {
                GUIWindow = new GameObject();
                GUIWindow.AddComponent<GUIDisplay>();
                GUIWindow.SetActive(false);
                Vector3 Mous = Input.mousePosition;
                xMenu = 0;
                yMenu = 0;
            }
        }
        internal static void DeInit()
        {
            if (!inst)
                return;
            Singleton.Manager<ManTechs>.inst.TankDriverChangedEvent.Unsubscribe(OnPlayerSwap);
            GUIWindow.SetActive(false);
            inst.enabled = false;
            Destroy(inst.gameObject);
            inst = null;
        }

        public static void OnPlayerSwap(Tank tonk)
        {
            CloseSubMenuClickable();
        }
        public static void GetTank(Tank tank)
        {
            ResetInfo();
            lastTank = tank.GetHelperInsured();
            Vector3 Mous = Input.mousePosition;
            xMenu = Mous.x - 225;
            yMenu = Display.main.renderingHeight - Mous.y + 25;
            if (lastTank)
                GetInfo(lastTank);
        }
        public static bool IsTankNull()
        {
            return lastTank.IsNull();
        }

        internal class GUIDisplay : MonoBehaviour
        {
            internal void OnGUI()
            {
                if (isCurrentlyOpen && KickStart.CanUseMenu)
                {
                    AltUI.StartUI();
                    var title = AltUI.UIAlphaText + (!lastTank.name.NullOrEmpty() ? lastTank.name == "recycled tech" ? "None Selected" : lastTank.name : "NO NAME") + "</color>";
                    //"AI Mode Select"
                    HotWindow = GUI.Window(AIManagerID, HotWindow, GUIHandler, title, AltUI.MenuLeft);
                    if (UIHelpersExt.MouseIsOverSubMenu(HotWindow))
                        ManModGUI.IsMouseOverAnyModGUI = 2;
                    AltUI.EndUI();
                }
            }
        }

        private static LocExtStringMod LOC_Back = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
               "Back" },
            { LocalisationEnums.Languages.Japanese,
                "メインに戻る"},
        });
        private static LocExtStringMod LOC_Back_desc = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
               "Return to Main Control" },
            { LocalisationEnums.Languages.Japanese,
                "メインコントロールに戻る"},
        });

        private static LocExtStringMod LOC_ToAdv = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
               "Adv. Settings" },
            { LocalisationEnums.Languages.Japanese,
                "詳細設定"},
        });
        private static LocExtStringMod LOC_ToAdv_desc = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
               "Set the AI operating ranges and behavior" },
            { LocalisationEnums.Languages.Japanese,
                "AIの動作範囲と考え方を設定する"},
        });
        private static void GUIHandler(int ID)
        {
            if (IsTankNull())
            {
                CloseSubMenuClickable();
                return;
            }

            changeAI = fetchAI;
            if (lastTank != null)
            {
                bool CantPerformActions;
                GUI.tooltip = lastTank.GetActionStatus(out CantPerformActions);

                /*
                 * Legacy colored text:
                 * <color=#f23d3dff> - Active Color
                 * (none) - Selectable
                 * <color=#808080ff> - Inactive
                 */

                bool stuckAnchored = lastTank.tank.IsAnchored && !lastTank.PlayerAllowAutoAnchoring;

                if (AdvancedToggles)
                {
                    GUIOptionsDisplay(stuckAnchored, CantPerformActions);
                    if (GUI.Button(new Rect(20, HotWindow.height - 85, 160, 30), new GUIContent(LOC_Back, LOC_Back_desc), AltUI.ButtonBlue))
                    {
                        AdvancedToggles = false;
                    }
                }
                else
                {
                    GUIMainDisplay(stuckAnchored, CantPerformActions);
                    if (GUI.Button(new Rect(20, HotWindow.height - 85, 160, 30), new GUIContent(LOC_ToAdv, LOC_ToAdv_desc), AltUI.ButtonBlue))
                    {
                        AdvancedToggles = true;
                    }
                }
                

                GUI.Label(new Rect(20, HotWindow.height - 55, 160, 50), AltUI.UIAlphaText + GUI.tooltip + "</color>");
            }
            else
            {
                DebugTAC_AI.Log(KickStart.ModID + ": SELECTED TANK IS NULL!");
                //lastTank = Singleton.Manager<ManPointer>.inst.targetVisible.transform.root.gameObject.GetComponent<AI.AIEnhancedCore.TankAIHelper>();
                CloseSubMenuClickable();
            }
            //GUI.DragWindow();
        }

        private static void GUIMainDisplay(bool stuckAnchored, bool CantPerformActions)
        {
            //GUI.Label(new Rect(20, 90, 160, 25), AltUI.UIAlphaText + (!lastTank.name.NullOrEmpty() ? lastTank.name == "recycled tech" ? "None Selected" : lastTank.name : "NO NAME") + "</color>");

            if (stuckAnchored)
            {
                GUIAnchored();
                GUIAnchorButton();
            }
            else if (!lastTank.ActuallyWorks)
            {
                GUINoAI();
            }
            else
            {
                //GUIMobileLegacy(CantPerformActions);
                //GUIAnchorButton();
                GUIMobile(CantPerformActions);
                GUIAnchorButtonLayout();
            }
        }
        private static LocExtStringMod LOC_NullAI = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
               "No AI!" },
            { LocalisationEnums.Languages.Japanese,
                "AIなし"},
        });
        private static LocExtStringMod LOC_CannotCommand = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
               "Cannot Control!" },
            { LocalisationEnums.Languages.Japanese,
                "制御できない"},
        });
        internal static LocExtStringMod LOC_Dismantle = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
               "SELF\nDESTRUCT"},
            { LocalisationEnums.Languages.Japanese,
                "崩壊"},
        });
        internal static LocExtStringMod LOC_DismantleStep_desc = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
               "Click to dismantle Tech!"},
            { LocalisationEnums.Languages.Japanese,
                "解体するには押す"},
        });
        internal static LocExtStringMod LOC_Dismantle_desc = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
               "DISMANTLE NOW"},
            { LocalisationEnums.Languages.Japanese,
                "分解する"},
        });
        internal static LocExtStringMod LOC_Dismantle_disabled = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
               "Cannot SELF\nDESTRUCT"},
            { LocalisationEnums.Languages.Japanese,
                "自己破壊が無効"},
        });
        internal static LocExtStringMod LOC_Dismantle_disabled_desc = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
               "Cannot self-destruct in Multi-Player"},
            { LocalisationEnums.Languages.Japanese,
                "マルチプレイで自爆できない"},
        });
        private static LocExtStringMod LOC_HasNoControl = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
               "No Control" },
            { LocalisationEnums.Languages.Japanese,
                "AIなし"},
        });
        private static LocExtStringMod LOC_HasNoControl_desc = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
               "Needs working AI Module" },
            { LocalisationEnums.Languages.Japanese,
                "動作するAIモジュールが必要"},
        });
        private static void GUINoAI()
        {
            HotWindow = new Rect(HotWindow.x, HotWindow.y, 200, 380);
            string textAnchor = LOC_NullAI.ToString();
            if (GUI.Button(new Rect(20, 30, 160, 60), new GUIContent(textAnchor, LOC_CannotCommand.ToString()), AltUI.ButtonRed))
            {
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
            }

            string textError;
            if (SelfDestruct == 0)
            {
                textError = "<color=#ffffffff>" + LOC_Dismantle + "</color>";
                if (GUI.Button(new Rect(20, 115, 160, 150), new GUIContent(textError, LOC_Dismantle_desc.ToString()), AltUI.ButtonRed))
                {
                    if (!ManNetwork.IsNetworked)
                    {
                        if (lastTank.tank.visible.isActive)
                            ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimCrateOpen);
                        lastTank.tank.blockman.Disintegrate();
                        return;
                    }
                }
            }
            else
            {
                textError = "<color=#ffffffff>" + LOC_Dismantle + SelfDestruct + "</color>";
                if (GUI.Button(new Rect(20, 115, 160, 150), new GUIContent(textError, LOC_DismantleStep_desc.ToString()), AltUI.ButtonRed))
                {
                    if (!ManNetwork.IsNetworked)
                    {
                        SelfDestruct--;
                    }
                }
            }
            if (GUI.Button(new Rect(20, 265, 160, 30), new GUIContent(LOC_HasNoControl.ToString(), LOC_HasNoControl_desc.ToString()), AltUI.ButtonGrey))
            {
            }
        }
        private static LocExtStringMod LOC_BaseTech = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
               "Base" },
            { LocalisationEnums.Languages.Japanese,
                "ベース"},
        });
        private static LocExtStringMod LOC_BaseTech_desc = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
               "Stationary builder" },
            { LocalisationEnums.Languages.Japanese,
                "固定ビルダー"},
        });
        private static LocExtStringMod LOC_GuardBase = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
               "<color=#ffffffff>Guard\nBase</color>" },
            { LocalisationEnums.Languages.Japanese,
                "<color=#ffffffff>警備基地</color>"},
        });
        private static LocExtStringMod LOC_GuardBase_desc = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
               "Anchored base defender" },
            { LocalisationEnums.Languages.Japanese,
                "固定防御"},
        });
        internal static LocExtStringMod LOC_BuildBase = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
               "Build" },
            { LocalisationEnums.Languages.Japanese,
                "建てる"},
        });
        internal static LocExtStringMod LOC_BuildBase_desc = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
               "Build a Tech" },
            { LocalisationEnums.Languages.Japanese,
                "テクノロジーを構築する"},
        });
        private static void GUIAnchored()
        {
            HotWindow = new Rect(HotWindow.x, HotWindow.y, 200, 380);
            string textAnchor = LOC_BaseTech;
            if (GUI.Button(new Rect(20, 30, 160, 60), new GUIContent(textAnchor, LOC_BaseTech_desc), AltUI.ButtonGreen))
            {
                ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimHEPayTerminal);
            }

            string textDefend = LOC_GuardBase;
            if (GUI.Button(new Rect(20, 115, 160, 80), new GUIContent(textDefend, LOC_GuardBase_desc), AltUI.ButtonBlueActive))
            {
                ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimGCCab);
            }
            string textBuild = "<color=#ffffffff>" + LOC_BuildBase + "</color>";
            if (GUI.Button(new Rect(20, 200, 160, 60), new GUIContent(textBuild, LOC_BuildBase_desc), AltUI.ButtonBlueActive))
            {
                ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimGCFabricator);
                Debug_TTExt.LogAll = true;
                PlayerRTSUI.ShowTechPlacementUI(lastTank);
            }
        }

        private static void GUIMobileLegacy(bool CantPerformActions)
        {
            HotWindow = new Rect(HotWindow.x, HotWindow.y, 200, 380);
            bool clicked = false;
            bool clickedDriver = false;
            string textTank = "<color=#ffffffff>Tank</color>";
            if (GUI.Button(new Rect(20, 30, 80, 30), AIDriver == AIDriverType.Tank ? new GUIContent(textTank, "ACTIVE") : new GUIContent(textTank, "Drive on Wheels or Treads"),
                AIDriver == AIDriverType.Tank ? AltUI.ButtonBlueActive : AltUI.ButtonBlue))
            {
                AIDriver = AIDriverType.Tank;
                clickedDriver = true;
            }

            string textAir = "<color=#ffffffff>Pilot</color>";
            if (isAviatorAvail)
            {
                if (GUI.Button(new Rect(100, 30, 80, 30), AIDriver == AIDriverType.Pilot ? new GUIContent(textAir, "ACTIVE") : new GUIContent(textAir, "Fly Planes or Helicopters"),
                    AIDriver == AIDriverType.Pilot ? AltUI.ButtonBlueActive : AltUI.ButtonBlue))
                {
                    AIDriver = AIDriverType.Pilot;
                    clickedDriver = true;
                }
            }
            else if (GUI.Button(new Rect(100, 30, 80, 30), new GUIContent(textAir, "Need HE or VEN AI"), AltUI.ButtonGrey))
            {
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
            }

            string textWater = "<color=#ffffffff>Ship</color>";
            if (isBuccaneerAvail && KickStart.isWaterModPresent)
            {
                if (GUI.Button(new Rect(20, 60, 80, 30), AIDriver == AIDriverType.Sailor ? new GUIContent(textWater, "ACTIVE") : new GUIContent(textWater, "Stay in Water"),
                    AIDriver == AIDriverType.Sailor ? AltUI.ButtonBlueActive : AltUI.ButtonBlue))
                {
                    AIDriver = AIDriverType.Sailor;
                    clickedDriver = true;
                }
            }
            else if (GUI.Button(new Rect(20, 60, 80, 30), !KickStart.isWaterModPresent ? new GUIContent(textWater, "Need Water Mod") : new GUIContent(textWater, "Need GSO or VEN AI"), AltUI.ButtonGrey))
            {
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
            }

            string textSpace = "<color=#ffffffff>Space</color>";
            if (isAstrotechAvail)
            {
                if (GUI.Button(new Rect(100, 60, 80, 30), AIDriver == AIDriverType.Astronaut ? new GUIContent(textSpace, "ACTIVE") : new GUIContent(textSpace, "Fly with Antigravity or Hoverbug"),
                    AIDriver == AIDriverType.Astronaut ? AltUI.ButtonBlueActive : AltUI.ButtonBlue))
                {
                    AIDriver = AIDriverType.Astronaut;
                    clickedDriver = true;
                }
            }
            else if (GUI.Button(new Rect(100, 60, 80, 30), new GUIContent(textSpace, "Need BF or HE AI"), AltUI.ButtonGrey))
            {
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
            }

            // Tasks
            // top - Escort
            string textEscort = "<color=#ffffffff>Escort</color>";
            if (GUI.Button(new Rect(20, 115, 80, 30), fetchAI == AIType.Escort ? new GUIContent(textEscort, "ACTIVE") : new GUIContent(textEscort, "Follows player"),
                fetchAI == AIType.Escort ? AltUI.ButtonBlueActive : AltUI.ButtonBlue))
            {
                changeAI = AIType.Escort;
                clicked = true;
            }

            string textRTS = "<color=#ffffffff>Order</color>";
            if (KickStart.AllowPlayerRTSHUD)
            {
                if (GUI.Button(new Rect(100, 115, 80, 30), lastTank.RTSControlled ? new GUIContent(textRTS, "ACTIVE") : new GUIContent(textRTS, "Go to last target"),
                    lastTank.RTSControlled ? AltUI.ButtonGreenActive : AltUI.ButtonGreen))
                {
                    bool toTog = !lastTank.RTSControlled;
                    lastTank.SetRTSState(toTog);
                    int select = 0;
                    int amount = 0;
                    foreach (TankAIHelper helper in ManWorldRTS.IterateControlledTechs())
                    {
                        if (helper != lastTank)
                        {
                            select++;
                            helper.SetRTSState(toTog);
                        }
                        amount++;
                    }
                    if (select > 0)
                    {
                        DebugTAC_AI.Log(KickStart.ModID + ": GUIAIManager - Set " + select + " Techs to RTSMode " + toTog);
                        inst.Invoke("DelayedExtraNoise", 0.15f);
                    }
                    Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.Enter);
                    if (!lastTank)
                        return;
                }
            }
            else if (GUI.Button(new Rect(100, 115, 80, 30), new GUIContent(textRTS, "RTS Mode Disabled"), AltUI.ButtonGrey))
            {
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
            }


            // upper right - MT
            string textStation = "<color=#ffffffff>Static</color>";
            if (GUI.Button(new Rect(100, 145, 80, 30), CantPerformActions ? !lastTank.AllMT ? new GUIContent(textStation, "Player not in range") : new GUIContent(textStation, "Ally not in range") :
                fetchAI == AIType.MTStatic ? new GUIContent(textStation, "ACTIVE") : new GUIContent(textStation, "Mobile Tech Hardpoint"),
                fetchAI == AIType.MTStatic ? CantPerformActions ? AltUI.ButtonRedActive : AltUI.ButtonBlueActive : AltUI.ButtonBlue))
            {
                changeAI = AIType.MTStatic;
                clicked = true;
            }
            string textTurret = "<color=#ffffffff>Turret</color>";
            if (GUI.Button(new Rect(100, 175, 80, 30), CantPerformActions ? !lastTank.AllMT ? new GUIContent(textTurret, "Player not in range") : new GUIContent(textTurret, "Ally not in range") :
                fetchAI == AIType.MTTurret ? new GUIContent(textTurret, "ACTIVE") : new GUIContent(textTurret, "Mobile Tech Turret"),
                fetchAI == AIType.MTTurret ? CantPerformActions ? AltUI.ButtonRedActive : AltUI.ButtonBlueActive : AltUI.ButtonBlue))
            {
                changeAI = AIType.MTTurret;
                clicked = true;
            }
            string textMimic = "<color=#ffffffff>Mimic</color>";
            if (GUI.Button(new Rect(100, 205, 80, 30), CantPerformActions ? !lastTank.AllMT ? new GUIContent(textMimic, "Player not in range") : new GUIContent(textMimic, "Ally not in range")
                : fetchAI == AIType.MTMimic ? new GUIContent(textMimic, "ACTIVE") : new GUIContent(textMimic, "Mobile Tech Copycat"),
                fetchAI == AIType.MTMimic ? CantPerformActions ? AltUI.ButtonRedActive : AltUI.ButtonBlueActive : AltUI.ButtonBlue))
            {
                changeAI = AIType.MTMimic;
                clicked = true;
            }


            // upper left, bottom - Aux modes
            string textMiner = "<color=#ffffffff>Miner</color>";
            if (isProspectorAvail)
            {
                if (GUI.Button(new Rect(20, 145, 80, 30), fetchAI == AIType.Prospector && !CantPerformActions ? new GUIContent(textMiner, "ACTIVE") : new GUIContent(textMiner, "Needs Receiver Base"),
                  fetchAI == AIType.Prospector ? CantPerformActions ? AltUI.ButtonRedActive : AltUI.ButtonBlueActive : AltUI.ButtonBlue))
                {
                    changeAI = AIType.Prospector;
                    clicked = true;
                }
            }
            else if (GUI.Button(new Rect(20, 145, 80, 30), new GUIContent(textMiner, "Need GSO or GC AI"),
                AltUI.ButtonGrey))
            {
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
            }

            string textAttack = "<color=#ffffffff>Scout</color>";
            if (isAssassinAvail)
            {
                if (GUI.Button(new Rect(20, 175, 80, 30), fetchAI == AIType.Assault && !CantPerformActions ? new GUIContent(textAttack, "ACTIVE") : new GUIContent(textAttack, "Needs Charging Base"),
                    fetchAI == AIType.Assault ? CantPerformActions ? AltUI.ButtonRedActive : AltUI.ButtonBlueActive : AltUI.ButtonBlue))
                {
                    changeAI = AIType.Assault;
                    clicked = true;

                }
            }
            else if (GUI.Button(new Rect(20, 175, 80, 30), new GUIContent(textAttack, "Need HE AI"), AltUI.ButtonGrey))
            {
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
            }

            string textProtect = "<color=#ffffffff>Protect</color>";
            if (isAegisAvail)
            {
                if (GUI.Button(new Rect(20, 205, 80, 30), CantPerformActions ? new GUIContent(textProtect, "No Allies Nearby") : fetchAI == AIType.Aegis ? new GUIContent(textProtect, "ACTIVE") : new GUIContent(textProtect, "Follow Closest Ally"),
                    fetchAI == AIType.Aegis ? CantPerformActions ? AltUI.ButtonRedActive : AltUI.ButtonBlueActive : AltUI.ButtonBlue))
                {
                    changeAI = AIType.Aegis;
                    clicked = true;
                }
            }
            else if (GUI.Button(new Rect(20, 205, 80, 30), new GUIContent(textProtect, "Need GSO AI"), AltUI.ButtonGrey))
            {
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
            }

            string textCharge = "<color=#ffffffff>Charger</color>";
            if (isEnergizerAvail)
            {
                if (GUI.Button(new Rect(20, 235, 80, 30), fetchAI == AIType.Energizer && !CantPerformActions ? new GUIContent(textCharge, "ACTIVE") : new GUIContent(textCharge, "Need Charge Base & Charger"),
                    fetchAI == AIType.Energizer ? CantPerformActions ? AltUI.ButtonRedActive : AltUI.ButtonBlueActive : AltUI.ButtonBlue))
                {
                    changeAI = AIType.Energizer;
                    clicked = true;
                }
            }
            else if (GUI.Button(new Rect(20, 235, 80, 30), new GUIContent(textCharge, "Need GC AI"), AltUI.ButtonGrey))
            {
            }

            string textScrap = "<color=#ffffffff>Fetch</color>";
            if (isScrapperAvail)
            {
                if (GUI.Button(new Rect(100, 235, 80, 30), fetchAI == AIType.Scrapper && !CantPerformActions ? new GUIContent(textScrap, "ACTIVE") : new GUIContent(textScrap, "Need Block Receiving Base"),
                    fetchAI == AIType.Scrapper ? CantPerformActions ? AltUI.ButtonRedActive : AltUI.ButtonBlueActive : AltUI.ButtonBlue))
                {
                    changeAI = AIType.Scrapper;
                    clicked = true;
                }
            }
            else if (GUI.Button(new Rect(100, 235, 80, 30), new GUIContent(textScrap, "Need GC AI"), AltUI.ButtonGrey))
            {
            }

            if (clickedDriver)
            {
                SetDriver(AIDriver);
            }
            if (clicked)
            {
                SetAIType(changeAI);
            }
        }

        private static LocExtStringMod LOC_Tank = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
               "<color=#ffffffff>Tank</color>" },
            { LocalisationEnums.Languages.Japanese,
                "<color=#ffffffff>タンク</color>"},
        }); 
        private static LocExtStringMod LOC_Tank_desc = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
               "Drive on Wheels or Treads" },
            { LocalisationEnums.Languages.Japanese,
                "車輪または履帯で走行する"},
        });
        private static LocExtStringMod LOC_Air_desc = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
               "Fly Planes or Helicopters" },
            { LocalisationEnums.Languages.Japanese,
                "車輪または履帯で走行する"},
        });
        private static LocExtStringMod LOC_Water_desc = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
               "Stay within the water" },
            { LocalisationEnums.Languages.Japanese,
                "車輪または履帯で走行する"},
        });
        private static LocExtStringMod LOC_Water_req = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
              "You need to subscribe to the \"Water Mod\"" },
            { LocalisationEnums.Languages.Japanese,
                "500メートル以内の資源を採掘する"},
        });
        private static LocExtStringMod LOC_Space_desc = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
              "Fly with Antigravity or Hoverbug" },
            { LocalisationEnums.Languages.Japanese,
                "車輪または履帯で走行する"},
        });


        internal static LocExtStringMod LOC_FindTheAI = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
              "Incorrect A.I. installed.  Try other AI modules!" },
            { LocalisationEnums.Languages.Japanese,
                "現在のAIはこの機能と互換性がありません"},
        });
        private static LocExtStringMod LOC_Active = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
              "ACTIVE" },
            { LocalisationEnums.Languages.Japanese,
                "アクティブ"},
        });
        private static void GUIDriverSetter(bool CantPerformActions, GUILayoutOption GLO, GUILayoutOption GLH, ref bool clickedDriver)
        {
            Sprite sprite;
            GUIContent tankI;
            if (RawTechExporter.aiIcons.TryGetValue(AIType.Escort, out sprite))
            {
                tankI = AIDriver == AIDriverType.Tank ? new GUIContent(sprite.texture, LOC_Active)
                    : new GUIContent(sprite.texture, LOC_Tank_desc);
            }
            else
            {
                string textTank = LOC_Tank;
                tankI = AIDriver == AIDriverType.Tank ? new GUIContent(textTank, LOC_Active)
                    : new GUIContent(textTank, LOC_Tank_desc);
            }
            GUILayout.BeginHorizontal(GLH);
            if (GUILayout.Button(tankI, AIDriver == AIDriverType.Tank ? AltUI.ButtonBlueActive : AltUI.ButtonBlue, GLO, GLH))
            {
                AIDriver = AIDriverType.Tank;
                clickedDriver = true;
            }

            DriverButton("Pilot", AIDriverType.Pilot, AIType.Aviator, isAviatorAvail,
                LOC_Air_desc, LOC_FindTheAI, ref clickedDriver);//"Need HE or VEN AI"
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GLH);
            DriverButton("Ship", AIDriverType.Sailor, AIType.Buccaneer, isBuccaneerAvail && KickStart.isWaterModPresent,
                LOC_Water_desc, KickStart.isWaterModPresent ? LOC_FindTheAI : LOC_Water_req, ref clickedDriver);//"Need GSO or VEN AI"

            DriverButton("Space", AIDriverType.Astronaut, AIType.Astrotech, isAstrotechAvail, LOC_Space_desc,
                LOC_FindTheAI, ref clickedDriver);//"Need BF or HE AI"
            GUILayout.EndHorizontal();
            GUILayout.Box("", GUILayout.Height(10));
        }

        internal static LocExtStringMod LOC_GoTo = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
              "Go to" },
            { LocalisationEnums.Languages.Japanese,
                "に行く"},
        }); 
        internal static LocExtStringMod LOC_Attack = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
              "Attack" },
            { LocalisationEnums.Languages.Japanese,
                "攻撃"},
        });
        internal static LocExtStringMod LOC_Stop = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
              "Stop" },
            { LocalisationEnums.Languages.Japanese,
                "攻撃"},
        });
        internal static LocExtStringMod LOC_SelectAll = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
              "Select All" },
            { LocalisationEnums.Languages.Japanese,
                "すべて選択"},
        });
        internal static LocExtStringMod LOC_SelectAllSplit = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
              "Select\nAll" },
            { LocalisationEnums.Languages.Japanese,
                "すべて\n選択"},
        });
        private static LocExtStringMod LOC_RTSDisabled = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
              "RTS Mode is disabled" },
            { LocalisationEnums.Languages.Japanese,
                "RTSモードが無効になっています"},
        });
        private static void GUIRTSButton(bool CantPerformActions, GUILayoutOption GLO, GUILayoutOption GLH)
        {
            Texture textRTS = ManWorldRTS.GetLineMat().mainTexture;
            //string textRTS = "<color=#ffffffff>Order</color>";
            if (KickStart.AllowPlayerRTSHUD)
            {
                if (GUILayout.Button(lastTank.RTSControlled ? new GUIContent(textRTS, LOC_Active) : new GUIContent(textRTS, LOC_GoTo),
                    lastTank.RTSControlled ? AltUI.ButtonGreenActive : AltUI.ButtonGreen, GLO, GLH))
                {
                    bool toTog = !lastTank.RTSControlled;
                    lastTank.SetRTSState(toTog);
                    int select = 0;
                    int amount = 0;
                    foreach (TankAIHelper helper in ManWorldRTS.IterateControlledTechs())
                    {
                        if (helper != lastTank)
                        {
                            select++;
                            helper.SetRTSState(toTog);
                        }
                        amount++;
                    }
                    if (select > 0)
                    {
                        DebugTAC_AI.Log(KickStart.ModID + ": GUIAIManager - Set " + select + " Techs to RTSMode " + toTog);
                        inst.Invoke("DelayedExtraNoise", 0.15f);
                    }
                    Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.Enter);
                    if (!lastTank)
                        return;
                }
            }
            else if (GUILayout.Button(new GUIContent(textRTS, LOC_RTSDisabled), AltUI.ButtonGrey, GLO, GLH))
            {
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
            }
        }

        private static LocExtStringMod LOC_MTPlayer_req = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
              "Player not in range" },
            { LocalisationEnums.Languages.Japanese,
                "プレイヤーが範囲内にいません"},
        });
        private static LocExtStringMod LOC_MTAlly_req = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
              "Ally not in range" },
            { LocalisationEnums.Languages.Japanese,
                "味方が範囲内にいません"},
        });
        private static void GUIMTButton(string name, AIType type, string desc, bool CantPerformActions, GUILayoutOption GLO, GUILayoutOption GLH, ref bool clicked)
        {
            if (RawTechExporter.aiIcons.TryGetValue(type, out Sprite sprite))
            {
                if (GUILayout.Button(CantPerformActions ? !lastTank.AllMT ?
                new GUIContent(sprite.texture, LOC_MTPlayer_req) : new GUIContent(sprite.texture, LOC_MTAlly_req) :
                fetchAI == type ? new GUIContent(sprite.texture, LOC_Active) : new GUIContent(sprite.texture, desc),
                    fetchAI == type ? CantPerformActions ? AltUI.ButtonRedActive : AltUI.ButtonBlueActive : AltUI.ButtonBlue, GLO, GLH))
                {
                    changeAI = type;
                    clicked = true;
                }
            }
            else
            {
                string textStation = "<color=#ffffffff>" + name + "</color>";
                if (GUILayout.Button(CantPerformActions ? !lastTank.AllMT ?
                    new GUIContent(textStation, LOC_MTPlayer_req) : new GUIContent(textStation, LOC_MTAlly_req) :
                    fetchAI == type ? new GUIContent(textStation, LOC_Active) : new GUIContent(textStation, desc),
                    fetchAI == type ? CantPerformActions ? AltUI.ButtonRedActive : AltUI.ButtonBlueActive : AltUI.ButtonBlue, GLO, GLH))
                {
                    changeAI = type;
                    clicked = true;
                }
            }
        }

        private static LocExtStringMod LOC_Escort_desc = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
              "Escort allied players" },
            { LocalisationEnums.Languages.Japanese,
                "味方プレイヤーを護衛する"},
        });
        private static LocExtStringMod LOC_Miner_desc = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
              "Mine resources within 500 meters" },
            { LocalisationEnums.Languages.Japanese,
                "500メートル以内の資源を採掘する"},
        });
        private static LocExtStringMod LOC_Miner_req = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
              "Needs a Tech with resource receivers" },
            { LocalisationEnums.Languages.Japanese,
                "リソースレシーバーを備えたベースが必要です"},
        });
        private static LocExtStringMod LOC_Static_desc = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
              "Mobile Tech Hardpoint"},
            { LocalisationEnums.Languages.Japanese,
                "モバイルハードポイント"},
        });
        private static LocExtStringMod LOC_Turret_desc = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
              "Mobile Tech Turret" },
            { LocalisationEnums.Languages.Japanese,
                "移動式砲塔"},
        });
        private static LocExtStringMod LOC_Mimic_desc = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
              "Mobile Tech Copycat" },
            { LocalisationEnums.Languages.Japanese,
                "リレー制御"},
        });
        private static LocExtStringMod LOC_Scout_desc = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
              "Attack distant enemies"},
            { LocalisationEnums.Languages.Japanese,
                "遠方の敵を巡回して攻撃する"},
        });
        private static LocExtStringMod LOC_Battery_req = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
              "Needs other Tech with wireless chargers" },
            { LocalisationEnums.Languages.Japanese,
                "ワイヤレス充電器付きの別のベースが必要"},
        });
        private static LocExtStringMod LOC_Protect_desc = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
              "Follow closest mobile ally"},
            { LocalisationEnums.Languages.Japanese,
                "最も近いモバイル仲間をフォロー"},
        });
        private static LocExtStringMod LOC_Protect_req = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
              "No Allies in range of 500 meters" },
            { LocalisationEnums.Languages.Japanese,
                "500メートル以内に味方がいない"},
        });
        private static LocExtStringMod LOC_Charger_desc = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
              "Recharges the closest allies"},
            { LocalisationEnums.Languages.Japanese,
                "最も近い味方を再充電する"},
        });
        private static LocExtStringMod LOC_Collect_desc = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
              "Collect loose blocks within 500 meters"},
            { LocalisationEnums.Languages.Japanese,
                "500メートル以内の緩いブロックを集める"},
        });
        private static LocExtStringMod LOC_Collect_req = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
              "Need other Tech with SCU or Scrappers" },
            { LocalisationEnums.Languages.Japanese,
                "SCUまたはスクラッパーを備えた別の基地が必要"},
        });
        private static void GUIMobile(bool CantPerformActions)
        {
            HotWindow = new Rect(HotWindow.x, HotWindow.y, 200, 420);
            bool clicked = false;
            bool clickedDriver = false;
            Sprite sprite;
            GUIContent tankI;
            GUILayoutOption GLO = GUILayout.MinWidth(HotWindow.width / 2.5f);
            GUILayoutOption GLH = GUILayout.Height(HotWindow.width / 6f);

            GUIDriverSetter(CantPerformActions, GLO, GLH, ref clickedDriver);

            // Tasks
            // top - Escort
            GUILayout.BeginHorizontal(GLH);
            if (GUILayout.Button(fetchAI == AIType.Escort ?
                new GUIContent(RawTechExporter.GuardAIIcon.texture, LOC_Active) :
                new GUIContent(RawTechExporter.GuardAIIcon.texture, LOC_Escort_desc),
                fetchAI == AIType.Escort ? AltUI.ButtonBlueActive : AltUI.ButtonBlue, GLO, GLH))
            {
                changeAI = AIType.Escort;
                clicked = true;
            }
            GUIRTSButton(CantPerformActions, GLO, GLH);

            GUILayout.EndHorizontal();

            // upper right - MT
            GUILayout.BeginHorizontal(GLH);
            // upper left, bottom - Aux modes
            AuxButton("Miner", AIType.Prospector, isProspectorAvail, LOC_Miner_req, LOC_Miner_desc,
                LOC_FindTheAI, ref CantPerformActions, ref clicked);//"Need GSO or GC AI"
            GUIMTButton("Static", AIType.MTStatic, LOC_Static_desc, CantPerformActions, GLO, GLH, ref clicked);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GLH);
            AuxButton("Scout", AIType.Assault, isAssassinAvail, LOC_Battery_req,  LOC_Scout_desc,
                 LOC_FindTheAI, ref CantPerformActions, ref clicked);//"Need HE AI"

            GUIMTButton("Turret", AIType.MTTurret, LOC_Turret_desc, CantPerformActions, GLO, GLH, ref clicked);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GLH);
            AuxButton("Protect", AIType.Aegis, isAegisAvail, LOC_Protect_req, LOC_Protect_desc,
                 LOC_FindTheAI, ref CantPerformActions, ref clicked);//"Need GSO AI"

            GUIMTButton("Mimic", AIType.MTMimic, LOC_Mimic_desc, CantPerformActions, GLO, GLH, ref clicked);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GLH);
            AuxButton("Charger", AIType.Energizer, isEnergizerAvail, LOC_Battery_req,  LOC_Charger_desc,
                 LOC_FindTheAI, ref CantPerformActions, ref clicked);//"Need GC AI"

            AuxButton("Fetch", AIType.Scrapper, isScrapperAvail, LOC_Collect_req, LOC_Collect_desc,
                 LOC_FindTheAI, ref CantPerformActions, ref clicked);//"Need GC AI"
            GUILayout.EndHorizontal();

            if (clickedDriver)
            {
                SetDriver(AIDriver);
            }
            if (clicked)
            {
                SetAIType(changeAI);
            }
        }
        internal static LocExtStringMod LOC_AnchorNone = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
              "No Anchors" },
            { LocalisationEnums.Languages.Japanese,
                "アンカーなし"},
        });
        internal static LocExtStringMod LOC_AnchorNone_desc = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
              "Needs working anchors" },
            { LocalisationEnums.Languages.Japanese,
                "作動するアンカーが必要です"},
        });
        internal static LocExtStringMod LOC_AnchorEnemy = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
              "Enemy Jammed" },
            { LocalisationEnums.Languages.Japanese,
                "敵が干渉している"},
        });
        internal static LocExtStringMod LOC_AnchorEnemy_desc = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
              "Enemy too close!" },
            { LocalisationEnums.Languages.Japanese,
                "敵が近すぎます!"},
        });
        internal static LocExtStringMod LOC_AnchorRough = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
              "Rough Terrain" },
            { LocalisationEnums.Languages.Japanese,
                "起伏の多い地形"},
        });
        internal static LocExtStringMod LOC_AnchorRough_desc = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
              "Too rough to deploy anchors. Try somewhere else." },
            { LocalisationEnums.Languages.Japanese,
                "地形が悪すぎます。別の場所でもう一度お試しください。"},
        });
        internal static LocExtStringMod LOC_Anchor = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
             "Stop & Anchor" },
            { LocalisationEnums.Languages.Japanese,
                "停止とアンカー"},
        });
        internal static LocExtStringMod LOC_Anchor_desc = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
                "Fixate to ground" },
            { LocalisationEnums.Languages.Japanese,
                "アンカーを停止して発射する"},
        });
        internal static LocExtStringMod LOC_UnAnchor = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
             "Mobilize" },
            { LocalisationEnums.Languages.Japanese,
                "動員する"},
        });
        internal static LocExtStringMod LOC_UnAnchor_desc = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
                "Detach from ground" },
            { LocalisationEnums.Languages.Japanese,
                "アンカーを外して移動する"},
        });
        private static void GUIAnchorButton()
        {
            if (!lastTank.tank.IsAnchored)//(lastTank.PlayerAllowAutoAnchoring)
            {
                if (lastTank.tank.Anchors.NumPossibleAnchors < 1)
                    GUI.Button(new Rect(20, 265, 160, 30), new GUIContent(LOC_AnchorNone, LOC_AnchorNone_desc), AltUI.ButtonGrey);
                else if (!lastTank.CanAnchorSafely)
                    GUI.Button(new Rect(20, 265, 160, 30), new GUIContent(LOC_AnchorEnemy, LOC_AnchorEnemy_desc), AltUI.ButtonRed);
                else if (!lastTank.CanAttemptAnchor)
                    GUI.Button(new Rect(20, 265, 160, 30), new GUIContent(LOC_AnchorRough, LOC_AnchorRough_desc), AltUI.ButtonRed);
                else
                {
                    if (GUI.Button(new Rect(20, 265, 160, 30), new GUIContent(LOC_Anchor, LOC_Anchor_desc), AltUI.ButtonGreen))
                    {
                        ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimSolarGen);
                        if (ManNetwork.IsHost)
                        {
                            lastTank.PlayerAllowAutoAnchoring = false;
                            lastTank.TryInsureAnchor();
                        }
                        AIDriver = AIDriverType.Stationary;
                        changeAI = AIType.Escort;
                        SetDriver(AIDriver);
                        SetAIType(changeAI);
                    }
                }
            }
            else if (GUI.Button(new Rect(20, 265, 160, 30), new GUIContent(LOC_UnAnchor, LOC_UnAnchor_desc), AltUI.ButtonGreen))
            {
                ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimCrateUnlock);
                if (ManNetwork.IsHost)
                {
                    lastTank.Unanchor();
                    lastTank.PlayerAllowAutoAnchoring = true;
                }
                AIDriver = AIDriverType.AutoSet;
                SetDriver(AIDriver);
            }
        }
        private static void GUIAnchorButtonLayout()
        {
            GUILayoutOption GLH = GUILayout.Height(HotWindow.width / 6.25f);
            if (lastTank.PlayerAllowAutoAnchoring)
            {
                if (lastTank.tank.Anchors.NumPossibleAnchors < 1)
                    GUI.Button(new Rect(20, 265, 160, 30), new GUIContent(LOC_AnchorNone, LOC_AnchorNone_desc), AltUI.ButtonGrey);
                else if (!lastTank.CanAnchorSafely)
                    GUI.Button(new Rect(20, 265, 160, 30), new GUIContent(LOC_AnchorEnemy, LOC_AnchorEnemy_desc), AltUI.ButtonRed);
                else if (!lastTank.CanAttemptAnchor)
                    GUI.Button(new Rect(20, 265, 160, 30), new GUIContent(LOC_AnchorRough, LOC_AnchorRough_desc), AltUI.ButtonRed);
                else
                {
                    if (GUILayout.Button(new GUIContent(LOC_Anchor, LOC_Anchor_desc), AltUI.ButtonGreen, GLH))
                    {
                        ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimSolarGen);
                        if (ManNetwork.IsHost)
                        {
                            lastTank.PlayerAllowAutoAnchoring = false;
                            lastTank.TryInsureAnchor();
                        }
                        AIDriver = AIDriverType.Stationary;
                        changeAI = AIType.Escort;
                        SetDriver(AIDriver);
                        SetAIType(changeAI);
                    }
                }
            }
            else if (GUILayout.Button(new GUIContent(LOC_UnAnchor, LOC_UnAnchor_desc), AltUI.ButtonGreen, GLH))
            {
                ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimCrateUnlock);
                if (ManNetwork.IsHost)
                {
                    lastTank.Unanchor();
                    lastTank.PlayerAllowAutoAnchoring = true;
                }
                if (AIDriver == AIDriverType.Stationary)
                {
                    AIDriver = AIDriverType.AutoSet;
                    SetDriver(AIDriver);
                }
            }
        }


        static float selectedOnceTime = 0;
        static float selectedUIDisplayTime = 0;
        static bool releasedOnce = false;
        static bool handoffControl = false;
        static Vector2 selectedOncePos = default;
        //private static FieldInfo delay = typeof(ManHUD).GetField("m_RadialShowDelay", BindingFlags.NonPublic | BindingFlags.Instance);
        //private static float delaySelect = (float)delay.GetValue(ManHUD.inst);
        private static string SelectedFieldControlName = string.Empty;
        private static string SelectedFieldControlValue = string.Empty;
        const int defaultState = -1;
        private static bool DisplayFieldSettableSliderProto(string Label, float LabelVal, string LabelDesc, int heightPos, float lastSetting, float limit, out float outVal)
        {
            int setValInt;
            string labelSet = "<color=#ffffffff>" + Label + (LabelVal == 5000f ? "Max" : LabelVal.ToString()) + "</color>";
            if (SelectedFieldControlName == Label)
            {
                setValInt = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(25, heightPos, 150, 30), defaultState,
                    defaultState, limit, AltUI.ScrollHorizontalTransparent, AltUI.ScrollThumbTransparent));
                GUI.SetNextControlName(SelectedFieldControlName);
                SelectedFieldControlValue = GUI.TextField(new Rect(20, heightPos, 160, 30), SelectedFieldControlValue, 12, AltUI.ButtonBlue);
                if (handoffControl)
                {
                    handoffControl = false;
                    GUI.FocusControl(SelectedFieldControlName);
                    /*
                    if (GUI.GetNameOfFocusedControl() != SelectedFieldControlName)
                    {
                        DebugTAC_AI.Assert("gues ill die");
                        throw new InvalidOperationException("gues ill die");
                    }*/
                }

                if (GUI.GetNameOfFocusedControl() != SelectedFieldControlName && selectedUIDisplayTime <= 0)
                {
                    if (int.TryParse(SelectedFieldControlValue, out int result))
                        setValInt = result;
                    SelectedFieldControlName = string.Empty;
                    SelectedFieldControlValue = string.Empty;
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Close);
                }
            }
            else if (selectedOnceTime > 0 && selectedOncePos.x.Approximately(Input.mousePosition.x, 9f) && 
                selectedOncePos.y.Approximately(Input.mousePosition.y, 9f))
            {
                GUI.Label(new Rect(20, heightPos, 160, 30), new GUIContent(labelSet, LabelDesc), AltUI.ButtonBlue);

                setValInt = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(25, heightPos, 150, 30), defaultState,
                    defaultState, limit, AltUI.ScrollHorizontalTransparent, AltUI.ScrollThumbTransparent));
                if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1))
                    releasedOnce = true;
                if (setValInt != defaultState && releasedOnce)
                {
                    selectedOnceTime = 0;
                    selectedUIDisplayTime = Globals.inst.doubleTapDelay;

                    SelectedFieldControlName = Label;
                    GUI.SetNextControlName(SelectedFieldControlName);
                    SelectedFieldControlValue = GUI.TextField(new Rect(20, heightPos, 160, 30), lastSetting.ToString(), 12, AltUI.ButtonBlue);
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Open);
                    handoffControl = true;
                    GUI.FocusControl(SelectedFieldControlName);
                }
            }
            else
            {
                GUI.Label(new Rect(20, heightPos, 160, 30), new GUIContent(labelSet, LabelDesc), AltUI.ButtonBlue);
                GUI.SetNextControlName(Label);
                setValInt = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(25, heightPos, 150, 30), defaultState,
                    defaultState, limit, AltUI.ScrollHorizontalTransparent, AltUI.ScrollThumbTransparent));
                releasedOnce = false;
                if (setValInt != defaultState)
                {
                    selectedOnceTime = Globals.inst.doubleTapDelay;
                    selectedOncePos = Input.mousePosition;
                    //ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Select);
                    //DebugTAC_AI.Log("SL - FocusedControlName: " + GUI.GetNameOfFocusedControl());
                }
                else
                {
                    //DebugTAC_AI.Log("FocusedControlName: " + GUI.GetNameOfFocusedControl());
                }
            }
            if (setValInt == defaultState)
                outVal = lastSetting;
            else
                outVal = setValInt;
            return !lastSetting.Approximately(outVal);
        }

        private static LocExtStringMod LOC_MaxCombatRange = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
              "Maximum combat range" },
            { LocalisationEnums.Languages.Japanese,
                "最大戦闘範囲"},
        });
        private static LocExtStringMod LOC_SpaceRange = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
              "Spacing from target" },
            { LocalisationEnums.Languages.Japanese,
                "ターゲットからの間隔"},
        });
        private static LocExtStringMod LOC_AttackMethod = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
              "Attack method" },
            { LocalisationEnums.Languages.Japanese,
                "攻撃戦略"},
        });
        private static LocExtStringMod LOC_SecondAvoidence_desc = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
              "Smarter pathing" },
            { LocalisationEnums.Languages.Japanese,
                "よりスマートなパス"},
        });
        private static LocExtStringMod LOC_AutoAnchor_desc = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
              "Anchor when idle" },
            { LocalisationEnums.Languages.Japanese,
                "アイドル時にアンカー"},
        });
        /// <summary>
        /// Pending - allow toggling of AI special operations
        /// </summary>
        private static void GUIOptionsDisplay(bool stuckAnchored, bool CantPerformActions)
        {
            bool delta = false;

            var lim = lastTank.AILimitSettings;
            var set = lastTank.AISetSettings;

            if (DisplayFieldSettableSliderProto("Range: ", lastTank.MaxCombatRange, LOC_MaxCombatRange,
                30, lastTank.MaxCombatRange, lim.CombatChase, out float setCombatChase))
            {
                set.CombatChase = setCombatChase;
                delta = true;
            }

            if (DisplayFieldSettableSliderProto("Spacing: ", lastTank.MinCombatRange, LOC_SpaceRange,
                60, lastTank.MinCombatRange, lim.CombatSpacing, out float setCombatRange))
            {
                set.CombatSpacing = setCombatRange;
                delta = true;
            }


            StatusLabelButton(new Rect(20, 115, 80, 30), "Aware", lastTank.SecondAvoidence, 
                LOC_SecondAvoidence_desc, LOC_FindTheAI, ref delta);//"Need Non-Anchor AI"
            StatusLabelButton(new Rect(100, 115, 80, 30), "Crafty", lastTank.AutoAnchor, 
                LOC_AutoAnchor_desc, LOC_FindTheAI, ref delta);//"Need GC AI"

            set.GUIDisplay(lim, ref delta);

            lastTank.AttackMode = (EAttackMode)Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(25, 235, 150, 30), (int)lastTank.AttackMode, 0, (int)EAttackMode.Ranged));
            StatusLabel(new Rect(20, 235, 160, 30), "Mode: " + lastTank.AttackMode, LOC_AttackMethod);

            if (delta)
            {
                set.Sync(lastTank, lim);
                lastTank.AISetSettings = set;
            }
        }

        private static void StatusLabel(Rect pos, string title, string desc)
        {
            string label = "<color=#ffffffff>" + title + "</color>";
            GUI.Label(pos, new GUIContent(label, desc), AltUI.ButtonBlue);
        }
        private static void StatusLabelButton(Rect pos, string title, bool status, string desc, 
            string requirement, ref bool delta)
        {
            string label = "<color=#ffffffff>" + title + "</color>";
            if (status)
            {
                if (GUI.Button(pos, new GUIContent(label, desc), AltUI.ButtonBlue))
                {
                }
            }
            else if (GUI.Button(pos, new GUIContent(label, requirement), AltUI.ButtonGrey))
            {
            }
        }
        internal static void StatusLabelButtonToggle(Rect pos, string title, bool can, ref bool CurState, 
            string desc, string requirement, ref bool delta)
        {
            string label = "<color=#ffffffff>" + title + "</color>";
            if (can)
            {
                if (CurState)
                {
                    if (GUI.Button(pos, new GUIContent(label, desc), AltUI.ButtonGreen))
                    {
                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.CheckBox);
                        CurState = false;
                        delta = true;
                    }
                }
                else
                {
                    if (GUI.Button(pos, new GUIContent(label, desc), AltUI.ButtonRed))
                    {
                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.CheckBox);
                        CurState = true;
                        delta = true;
                    }
                }
            }
            else if (GUI.Button(pos, new GUIContent(label, requirement), AltUI.ButtonGrey))
            {
            }
        }

        private static void DriverButton(string title, AIDriverType type, AIType displayType, bool isAvail, 
            string Desc, string reqAI, ref bool clickedDriver)
        {
            GUILayoutOption GLO = GUILayout.MinWidth(HotWindow.width / 2.5f);
            GUILayoutOption GLH = GUILayout.Height(HotWindow.width / 6f);
            if (RawTechExporter.aiIcons.TryGetValue(displayType, out Sprite sprite))
            {
                if (isAvail)
                {
                    if (GUILayout.Button(AIDriver == type ? new GUIContent(sprite.texture, LOC_Active) : new GUIContent(sprite.texture, Desc),
                      AIDriver == type ? AltUI.ButtonBlueActive : AltUI.ButtonBlue, GLO, GLH))
                    {
                        AIDriver = type;
                        clickedDriver = true;
                    }
                }
                else if (GUILayout.Button(new GUIContent(sprite.texture, reqAI),
                    AltUI.ButtonGrey, GLO, GLH))
                {
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                }
            }
            else
            {
                string textTitle = "<color=#ffffffff>" + title + "</color>";
                if (isAvail)
                {
                    if (GUILayout.Button(AIDriver == type ? new GUIContent(textTitle, LOC_Active) : new GUIContent(textTitle, Desc),
                      AIDriver == type ? AltUI.ButtonBlueActive : AltUI.ButtonBlue, GLO, GLH))
                    {
                        AIDriver = type;
                        clickedDriver = true;
                    }
                }
                else if (GUILayout.Button(new GUIContent(textTitle, reqAI),
                    AltUI.ButtonGrey, GLO, GLH))
                {
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                }
            }
        }

        private static void AuxButton(string title, AIType type, bool isAvail, string runReq, string desc, string reqAI, ref bool CantPerformActions, ref bool clicked)
        {
            GUILayoutOption GLO = GUILayout.MinWidth(HotWindow.width / 2.5f);
            GUILayoutOption GLH = GUILayout.Height(HotWindow.width / 6f);
            if (RawTechExporter.aiIcons.TryGetValue(type, out Sprite sprite))
            {
                if (isAvail)
                {
                    if (GUILayout.Button(fetchAI == type && 
                        CantPerformActions ? new GUIContent(sprite.texture, runReq) 
                        : fetchAI == AIType.Aegis ? new GUIContent(sprite.texture, LOC_Active) : new GUIContent(sprite.texture, desc),
                      fetchAI == type ? CantPerformActions ? AltUI.ButtonRedActive : AltUI.ButtonBlueActive : AltUI.ButtonBlue, GLO, GLH))
                    {
                        changeAI = type;
                        clicked = true;
                    }
                }
                else if (GUILayout.Button(new GUIContent(sprite.texture, reqAI),
                    AltUI.ButtonGrey, GLO, GLH))
                {
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                }
            }
            else
            {
                string textTitle = "<color=#ffffffff>" + title + "</color>";
                if (isAvail)
                {
                    if (GUILayout.Button(fetchAI == type &&
                        CantPerformActions ? new GUIContent(textTitle, runReq)
                        : fetchAI == type ? new GUIContent(textTitle, LOC_Active) : new GUIContent(textTitle, desc),
                      fetchAI == type ? CantPerformActions ? AltUI.ButtonRedActive : AltUI.ButtonBlueActive : AltUI.ButtonBlue, GLO, GLH))
                    {
                        changeAI = type;
                        clicked = true;
                    }
                }
                else if (GUILayout.Button(new GUIContent(textTitle, reqAI),
                    AltUI.ButtonGrey, GLO, GLH))
                {
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                }
            }
        }


        internal static FieldInfo bubble = typeof(Tank).GetField("m_Overlay", BindingFlags.NonPublic | BindingFlags.Instance);

        private static void SetDriver(AIDriverType driver, bool playSFX = true)
        {
            SetDriver(lastTank, driver, playSFX);
            inst.TrySetDriverAllRTSControlled(driver);
        }
        public static void SetDriver(TankAIHelper lastTank, AIDriverType driver, bool playSFX = true)
        {
            try
            {
                if (!lastTank)
                    return;
                if (!lastTank.tank)
                    return;
                SetDriverCase(lastTank, driver, ManWorldRTS.inst.LocalPlayerTechsControlled.Count == 1);
                if (playSFX)
                {
                    switch (driver)
                    {
                        case AIDriverType.Tank:
                            ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimGCCab);
                            break;
                        case AIDriverType.Pilot:
                            ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimBFSmallSkidClose);
                            break;
                        case AIDriverType.Sailor:
                            ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimGSODeliCannonMob);
                            break;
                        case AIDriverType.Astronaut:
                            ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimBFAntiGravEco);
                            break;
                        case AIDriverType.Stationary:
                            ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimGCDeliCannon);
                            break;
                        default:
                            Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.CheckBox);
                            break;
                    }
                }
                //Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.AIFollow);
            }
            catch { }
        }
        private void TrySetDriverAllRTSControlled(AIDriverType driver)
        {
            if (!(bool)ManWorldRTS.inst)
                return;
            if (ManWorldRTS.PlayerIsInRTS || ManWorldRTS.PlayerRTSOverlay)
            {
                int select = 0;
                int amount = 0;
                foreach (TankAIHelper helper in ManWorldRTS.IterateControlledTechs())
                {
                    if (helper != lastTank)
                    {
                        select++;
                        SetDriverCase(helper, driver, false);
                    }
                    amount++;
                }
                DebugTAC_AI.Log(KickStart.ModID + ": TrySetOptionDriverRTS - Set " + amount + " Techs to drive " + driver);
                if (select > 0)
                    Invoke("DelayedExtraNoise", 0.15f);
            }
        }
        private static void SetDriverCase(TankAIHelper helper, AIDriverType driver, bool allowSetMultiTech)
        {
            if (helper.IsNull())
                return;
            if (helper.IsMultiTech && !allowSetMultiTech)
                return;
            bool guess = driver == AIDriverType.AutoSet;
            /*
            if (guess)
                DebugTAC_AI.Info(KickStart.ModID + ": Given " + lastTank.name + " set to driver " + driver);
            else
                DebugTAC_AI.Assert(KickStart.ModID + ": Set " + lastTank.name + " to driver " + driver);
            */
            AIDriverType locDediAI = AIDriverType.Tank;
            switch (driver)
            {
                case AIDriverType.Astronaut:
                    if (!helper.isAstrotechAvail)
                        return;
                    locDediAI = driver;
                    break;
                case AIDriverType.Pilot:
                    if (!helper.isAviatorAvail)
                        return;
                    locDediAI = driver;
                    break;
                case AIDriverType.Sailor:
                    if (!helper.isBuccaneerAvail)
                        return;
                    locDediAI = driver;
                    break;
                case AIDriverType.Stationary:
                    if (helper.tank.Anchors.NumPossibleAnchors < 1 || !helper.CanAnchorNow)
                        return;
                    locDediAI = driver;
                    break;
                case AIDriverType.Tank:
                    locDediAI = driver;
                    break;
                case AIDriverType.AutoSet:
                    break;
                default:
                    DebugTAC_AI.LogError(KickStart.ModID + ": Encountered illegal AIDriverType on AI Driver switch!");
                    break;
            }


            if (ManNetwork.IsNetworked)
            {
                try
                {
                    NetworkHandler.TryBroadcastNewAIState(helper.tank.netTech.netId.Value, AIType.Null, locDediAI);
                    helper.OnSwitchAI(false);
                    if (guess)
                        helper.ExecuteAutoSetNoCalibrate();
                    else
                        helper.SetDriverType(driver);
                    helper.WakeAIForChange(true);
                    if (helper.DriverType != driver)
                    {
                        WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(helper.tank.visible);
                        AIGlobals.PopupPlayerInfo(driver.ToString(), worPos);
                    }
                }
                catch (Exception e)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Error on sending AI Option change!!!\n" + e);
                }
            }
            else
            {
                helper.OnSwitchAI(false);
                if (driver == AIDriverType.AutoSet)
                    helper.ExecuteAutoSetNoCalibrate();
                else
                    helper.SetDriverType(driver);
                helper.WakeAIForChange();
                /*
                //DebugTAC_AI.Log(KickStart.ModID + ": 1");
                helper.ForceAllAIsToEscort(true, true);
                //DebugTAC_AI.Log(KickStart.ModID + ": 2");
                helper.ForceRebuildAlignment();
                //DebugTAC_AI.Log(KickStart.ModID + ": 3");
                */
                if (helper.DriverType != driver)
                {
                    WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(helper.tank.visible);
                    AIGlobals.PopupPlayerInfo(driver.ToString(), worPos);
                }
                //DebugTAC_AI.Log(KickStart.ModID + ": 41");
            }
            if (guess)
                DebugTAC_AI.Assert(KickStart.ModID + ": Set " + lastTank.name + " to driver " + driver);
        }

        private static void SetAIType(AIType dediAI, bool playSFX = true)
        {
            SetAIType(lastTank, dediAI, playSFX);
            inst.TrySetAITypeAllRTSControlled(dediAI);
        }
        public static void SetAIType(TankAIHelper lastTank, AIType dediAI, bool playSFX = true)
        {
            if (lastTank.IsNull())
                return;
            if (ManNetwork.IsNetworked)
            {
                try
                {
                    NetworkHandler.TryBroadcastNewAIState(lastTank.tank.netTech.netId.Value, dediAI, AIDriverType.Null);
                    lastTank.OnSwitchAI(true);
                    if (lastTank.DediAI != dediAI)
                    {
                        WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(lastTank.tank.visible);
                        AIGlobals.PopupPlayerInfo(dediAI.ToString(), worPos);
                    }
                    lastTank.DediAI = dediAI;
                    fetchAI = dediAI;
                    lastTank.WakeAIForChange(true);

                }
                catch (Exception e)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Error on sending AI Option change!!!\n" + e);
                }
            }
            else
            {
                lastTank.OnSwitchAI(true);
                if (lastTank.DediAI != dediAI)
                {
                    WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(lastTank.tank.visible);
                    AIGlobals.PopupPlayerInfo(dediAI.ToString(), worPos);
                }
                lastTank.DediAI = dediAI;
                fetchAI = dediAI;
                lastTank.WakeAIForChange(true);

            }
            if (playSFX)
            {
                switch (dediAI)
                {
                    case AIType.Escort:
                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AIFollow);
                        break;
                    case AIType.Assault:
                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.LockOn);
                        break;
                    case AIType.Aegis:
                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AIGuard);
                        break;
                    case AIType.Prospector:
                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.RadarOn);
                        break;
                    case AIType.Scrapper:
                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Craft);
                        break;
                    case AIType.Energizer:
                        ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimBFAirReceiver);
                        break;
                    case AIType.MTTurret:
                        ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimHEPayTerminal);
                        break;
                    case AIType.MTStatic:
                        ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimSolarGen);
                        break;
                    case AIType.MTMimic:
                        ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimGCPayTerminal);
                        break;
                    case AIType.Aviator:
                        ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimBFSmallSkidClose);
                        break;
                    case AIType.Buccaneer:
                        ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimGSODeliCannonMob);
                        break;
                    case AIType.Astrotech:
                        ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimBFAntiGravEco);
                        break;
                    default:
                        Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.Enter);
                        break;
                }
            }
            //Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.AIFollow);
            //CloseSubMenuClickable();
        }
        private void TrySetAITypeAllRTSControlled(AIType dediAI)
        {
            if (!(bool)ManWorldRTS.inst)
                return;
            if (ManWorldRTS.PlayerIsInRTS || ManWorldRTS.PlayerRTSOverlay)
            {
                int select = 0;
                int amount = 0;
                foreach (TankAIHelper helper in ManWorldRTS.IterateControlledTechs())
                {
                    if (helper != lastTank)
                    {
                        select++;
                        SetAITypeCase(helper, dediAI);
                    }
                    amount++;
                }
                DebugTAC_AI.Log(KickStart.ModID + ": TrySetOptionRTS - Set " + amount + " Techs to mode " + dediAI);
                if (select > 1)
                    Invoke("DelayedExtraNoise", 0.15f);
            }
        }
        private static void SetAITypeCase(TankAIHelper helper, AIType dediAI)
        {
            if (helper.IsNull())
                return;
            AIType locDediAI;
            switch (dediAI)
            {
                case AIType.Assault:
                    if (helper.isAssassinAvail)
                        locDediAI = dediAI;
                    else
                        locDediAI = AIType.Null;
                    break;
                case AIType.Aegis:
                    if (helper.isAegisAvail)
                        locDediAI = dediAI;
                    else
                        locDediAI = AIType.Null;
                    break;
                case AIType.Aviator:
                    if (helper.isAviatorAvail)
                        locDediAI = dediAI;
                    else
                        locDediAI = AIType.Null;
                    break;
                case AIType.Buccaneer:
                    if (helper.isBuccaneerAvail)
                        locDediAI = dediAI;
                    else
                        locDediAI = AIType.Null;
                    break;
                case AIType.Astrotech:
                    if (helper.isAstrotechAvail)
                        locDediAI = dediAI;
                    else
                        locDediAI = AIType.Null;
                    break;
                case AIType.Energizer:
                    if (helper.isEnergizerAvail)
                        locDediAI = dediAI;
                    else
                        locDediAI = AIType.Null;
                    break;
                case AIType.Prospector:
                    if (helper.isProspectorAvail)
                        locDediAI = dediAI;
                    else
                        locDediAI = AIType.Null;
                    break;
                case AIType.Scrapper:
                    if (helper.isScrapperAvail)
                        locDediAI = dediAI;
                    else
                        locDediAI = AIType.Null;
                    break;
                case AIType.Null:
                case AIType.Escort:
                case AIType.MTTurret:
                case AIType.MTStatic:
                case AIType.MTMimic:
                default:
                    locDediAI = dediAI;
                    break;
            }

            if (locDediAI == AIType.Null)
                return; // DO NOT CHANGE on conflict

            if (ManNetwork.IsNetworked)
            {
                try
                {
                    NetworkHandler.TryBroadcastNewAIState(helper.tank.netTech.netId.Value, locDediAI, AIDriverType.Null);
                    helper.OnSwitchAI(true);
                    if (helper.DediAI != dediAI)
                    {
                        WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(helper.tank.visible);
                        AIGlobals.PopupPlayerInfo(dediAI.ToString(), worPos);
                    }
                    helper.DediAI = locDediAI;
                    helper.WakeAIForChange(true);

                }
                catch (Exception e)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Error on sending AI Option change!!!\n" + e);
                }
            }
            else
            {
                helper.OnSwitchAI(false);
                if (helper.DediAI != dediAI)
                {
                    WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(helper.tank.visible);
                    AIGlobals.PopupPlayerInfo(dediAI.ToString(), worPos);
                }
                helper.DediAI = locDediAI;
                helper.WakeAIForChange(true);
            }
        }
        internal void DelayedExtraNoise()
        {
            Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.Enter);
        }


        private static bool isAssassinAvail = false;    //Is there an Assassin-enabled AI on this tech?
        private static bool isAegisAvail = false;       //Is there an Aegis-enabled AI on this tech?

        private static bool isProspectorAvail = false;  //Is there a Prospector-enabled AI on this tech?
        private static bool isScrapperAvail = false;    //Is there a Scrapper-enabled AI on this tech?
        private static bool isEnergizerAvail = false;   //Is there a Energizer-enabled AI on this tech?

        private static bool isAviatorAvail = false;
        private static bool isAstrotechAvail = false;
        private static bool isBuccaneerAvail = false;

        internal static void ResetInfo()
        {
            isAegisAvail = false;
            isAssassinAvail = false;

            isProspectorAvail = false;
            isScrapperAvail = false;
            isEnergizerAvail = false;

            isAstrotechAvail = false;
            isAviatorAvail = false;
            isBuccaneerAvail = false;
        }
        internal static void GetInfo(TankAIHelper helper)
        {
            if (helper.isAegisAvail)
                isAegisAvail = true;
            if (helper.isAssassinAvail)
                isAssassinAvail = true;

            // Collectors
            if (helper.isProspectorAvail)
                isProspectorAvail = true;
            if (helper.isScrapperAvail)
                isScrapperAvail = true;
            if (helper.isEnergizerAvail)
                isEnergizerAvail = true;

            // Pilots
            if (helper.isAviatorAvail)
                isAviatorAvail = true;
            if (helper.isBuccaneerAvail)
                isBuccaneerAvail = true;
            if (helper.isAstrotechAvail)
                isAstrotechAvail = true;
        }


        internal static void LaunchSubMenuClickableRTS()
        {
            if (!KickStart.EnableBetterAI || !ManWorldRTS.inst.IsNotNull())
            {
                return;
            }
            if (ManWorldRTS.inst.Leading)
            {
                try
                {
                    if (ManWorldRTS.PlayerIsInRTS || ManWorldRTS.PlayerRTSOverlay)
                    {
                        if (ManWorldRTS.inst.LocalPlayerTechsControlled.Count > 0)
                        {
                            Vector3 Mous = Input.mousePosition;
                            xMenu = Mous.x - (10 + HotWindow.width);
                            yMenu = Display.main.renderingHeight - Mous.y + 10;
                            lastTank = ManWorldRTS.inst.Leading;
                        }
                        else
                        {
                            DebugTAC_AI.Log(KickStart.ModID + ": No techs selected!");
                            Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                            return;
                        }
                    }
                    else
                    {
                        DebugTAC_AI.Log(KickStart.ModID + ": No techs selected!");
                        return;
                    }
                }
                catch
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": TANK IS NULL!");
                    return;
                }
            }
            if (!lastTank)
                return;

            lastTank.RefreshAI();
            GetInfo(lastTank);
            DebugTAC_AI.Log(KickStart.ModID + ": Opened AI menu!");
            AIDriver = lastTank.DriverType;
            fetchAI = lastTank.DediAI;
            MoveMenuToCursor(true);
            isCurrentlyOpen = true;
            windowTimer = 0.25f;
            GUIWindow.SetActive(true);
        }
        internal static void LaunchSubMenuClickable(bool centerOnMouse = false)
        {
            if (!KickStart.EnableBetterAI)
            {
                return;
            }
            if (IsTankNull())
            {
                DebugTAC_AI.Assert(true, KickStart.ModID + ": TANK IS NULL!");
                return;
            }
            lastTank.RefreshAI();
            GetInfo(lastTank);
            AdvancedToggles = false;
            SelfDestruct = 5;
            DebugTAC_AI.Log(KickStart.ModID + ": Opened AI menu!");
            AIDriver = lastTank.DriverType;
            fetchAI = lastTank.DediAI;
            MoveMenuToCursor(centerOnMouse);
            isCurrentlyOpen = true;
            if (centerOnMouse)
                windowTimer = 0.25f;
            else
                windowTimer = 2.25f;
            GUIWindow.SetActive(true);
        }
        internal static void CloseSubMenuClickable()
        {
            if (isCurrentlyOpen)
            {
                lastTank = null;
                ResetInfo();
                isCurrentlyOpen = false;
                GUIWindow.SetActive(false);
                KickStart.ReleaseControl();
                DebugTAC_AI.Log(KickStart.ModID + ": Closed AI menu!");
            }
        }

        internal static void MoveMenuToCursor(bool centerOnMouse)
        {
            if (centerOnMouse)
            {
                Vector3 Mous = Input.mousePosition;
                xMenu = Mous.x - (HotWindow.width / 2);
                yMenu = Display.main.renderingHeight - Mous.y - 90;
            }
            xMenu = Mathf.Clamp(xMenu, 0, Display.main.renderingWidth - HotWindow.width);
            yMenu = Mathf.Clamp(yMenu, 0, Display.main.renderingHeight - HotWindow.height);
            HotWindow.x = xMenu;
            HotWindow.y = yMenu;
        }
       

        private void Update()
        {
            if (selectedOnceTime > 0)
                selectedOnceTime -= Time.deltaTime;
            if (selectedUIDisplayTime > 0)
                selectedUIDisplayTime -= Time.deltaTime;

            if (windowTimer > 0)
            {
                windowTimer -= Time.deltaTime;
            }
            if (windowTimer < 0 && !UIHelpersExt.MouseIsOverSubMenu(HotWindow))
            {
                CloseSubMenuClickable();
                windowTimer = 0;
            }
            if (ManPauseGame.inst.IsPaused)
            {
                if (windowTimer >= 0)
                {
                    CloseSubMenuClickable();
                    windowTimer = 0;
                }
            }
            else
            {
                if (Input.GetKeyDown(KickStart.ModeSelect))
                {
                    if (!isCurrentlyOpen)
                        LaunchSubMenuClickableRTS();
                    else
                    {
                        CloseSubMenuClickable();
                        windowTimer = 0;
                    }
                }
            }
        }
    }
}
