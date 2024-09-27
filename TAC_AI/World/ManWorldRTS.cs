using System;
using System.Collections.Generic;
using System.Linq;
using TAC_AI.AI;
using TAC_AI.AI.Enemy;
using TAC_AI.Templates;
using TerraTechETCUtil;
using UnityEngine;

namespace TAC_AI.World
{
    public enum RTSCursorState
    {
        Empty,
        Moving,
        Attack,
        Select,
        Fetch,
        Mine,
        Protect,
        Scout,
    }
    public enum RTSHaloState
    {
        Default,
        Hover,
        Select,
        Attack,
    }
    internal class SelectHalo : MonoBehaviour
    {
        public static GameObject SelectCirclePrefab;

        private TankAIHelper tech;
        private ParticleSystem ps;
        private GameObject circleInst;
        private float lastSize = 1;
        private RTSHaloState lastHalo = RTSHaloState.Default;
        private const float sizeMulti = 1.25f;
        private static Color Player = new Color(0f, 0.3f, 1f, 0.95f);
        private static Color Main = new Color(0f, 0.75f, 1f, 0.45f);
        private static Color NonMain = new Color(0f, 0.5f, 0.5f, 0.45f);
        private static Color Target = new Color(1f, 0.25f, 0.25f, 0.45f);
        private static Color Hovered = new Color(1f, 1f, 0.1f, 0.45f);
        private static Color Info = new Color(1f, 0.25f, 1f, 0.45f);
        private static Color Curious = new Color(1f, 0.8f, 1f, 0.45f);


        internal static Dictionary<RTSHaloState, Material> halos = new Dictionary<RTSHaloState, Material>();

        public void Initiate(TankAIHelper helper)
        {
            if ((bool)circleInst)
            {
                circleInst.SetActive(true);
                return;
            }
            circleInst = Instantiate(SelectCirclePrefab, helper.transform, false);
            circleInst.transform.position = helper.tank.boundsCentreWorldNoCheck;
            circleInst.name = "SelectCircle";
            lastHalo = RTSHaloState.Default;
            tech = helper;
            tech.tank.AttachEvent.Subscribe(OnSizeUpdate);
            tech.tank.DetachEvent.Subscribe(OnSizeUpdate);
            ps = circleInst.GetComponent<ParticleSystem>();
            var m = ps.main;
            m.startSize = helper.lastTechExtents * sizeMulti;
            ps.Play(false);
            circleInst.SetActive(true);
        }
        private void UpdateVisual(RTSHaloState halo)
        {
            if (lastHalo != halo)
            {
                var psr = ps.GetComponent<ParticleSystemRenderer>();
                if (halos.TryGetValue(halo, out Material vis))
                {
                    psr.material = vis;
                }
                else
                    psr.material = halos.FirstOrDefault().Value;
                lastHalo = halo;
            }
        }
        private void Update()
        {
            try
            {
                if (AIGlobals.HideHud == ps.isPlaying)
                {
                    if (ps.isPlaying)
                        ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    else
                        ps.Play(false);
                }
                if (ps.isPlaying)
                {
                    var m = ps.main;
                    if (lastSize != tech.lastTechExtents)
                    {
                        m.startSize = tech.lastTechExtents * sizeMulti;
                        lastSize = tech.lastTechExtents;
                    }
                    if (tech.tank.PlayerFocused)
                    {
                        m.startColor = Player;
                        if (ManWorldRTS.inst.PlayerHovered == tech)
                            UpdateVisual(RTSHaloState.Hover);
                        else
                        {
                            if (KickStart.AutopilotPlayer)
                                UpdateVisual(RTSHaloState.Select);
                            else
                                UpdateVisual(RTSHaloState.Default);
                        }
                    }
                    else if (ManWorldRTS.inst.Leading == tech)
                    {
                        m.startColor = Main;
                        if (ManWorldRTS.inst.PlayerHovered == tech)
                            UpdateVisual(RTSHaloState.Hover);
                        else
                            UpdateVisual(RTSHaloState.Select);
                    }
                    else
                    {
                        switch (tech.AIAlign)
                        {
                            case AIAlignment.Player:
                                m.startColor = NonMain;
                                if (ManWorldRTS.inst.PlayerHovered == tech)
                                    UpdateVisual(RTSHaloState.Hover);
                                else
                                    UpdateVisual(RTSHaloState.Select);
                                break;
                            case AIAlignment.NonPlayer:
                                if (!ManBaseTeams.IsUnattackable(tech.tank.Team, ManPlayer.inst.PlayerTeam))
                                {
                                    if (tech == ManWorldRTS.inst.OtherHovered)
                                    {
                                        m.startColor = Hovered;
                                        UpdateVisual(RTSHaloState.Attack);
                                    }
                                    else
                                    {
                                        m.startColor = Target;
                                        UpdateVisual(RTSHaloState.Attack);
                                    }
                                }
                                else
                                {
                                    if (tech == ManWorldRTS.inst.OtherHovered)
                                    {
                                        m.startColor = Curious;
                                        UpdateVisual(RTSHaloState.Default);
                                    }
                                    else
                                    {
                                        m.startColor = Info;
                                        UpdateVisual(RTSHaloState.Default);
                                    }
                                }
                                break;
                            case AIAlignment.Static:
                                m.startColor = Hovered;
                                UpdateVisual(RTSHaloState.Hover);
                                break;
                            case AIAlignment.Neutral:
                            default:
                                m.startColor = Hovered;
                                UpdateVisual(RTSHaloState.Default);
                                break;
                        }
                    }
                }
            }
            catch { }
        }
        public void OnSizeUpdate(TankBlock tb, Tank techCase)
        {
            try
            {/*
                if (techCase == tech.tank)
                {
                    if (techCase.blockman.blockCount == 0)
                    {
                        Remove();
                        return;
                    }
                    var m = ps.main;
                    m.startSize = tech.lastTechExtents * sizeMulti;
                }*/
            }
            catch { }
        }
        public void Remove()
        {
            try
            {
                if (!(bool)circleInst)
                    return;
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                circleInst.SetActive(false);

                tech.tank.AttachEvent.Unsubscribe(OnSizeUpdate);
                tech.tank.DetachEvent.Unsubscribe(OnSizeUpdate);
                Destroy(ps);
                Destroy(circleInst);
                tech = null;
                ps = null;
                circleInst = null;
            }
            catch (Exception e)
            {
                DebugTAC_AI.Log(KickStart.ModID + ": SelectHalo - Removal failiure - was it edited but something else?!? " + e);
            }
        }
    }
    internal class ManWorldRTS : MonoBehaviour
    {

        public static ManWorldRTS inst;
        public static int MaxCommandDistance = 9001;//500;
        public static int MaxAllowedSizeForHighlight = 3;
        /// <summary> Converted Photo Mode </summary>
        public static bool PlayerIsInRTS = false;
        /// <summary> The controlling Tech command hotkey </summary>
        public static bool PlayerRTSOverlay = false;
        public static bool RTSControl => PlayerIsInRTS || PlayerRTSOverlay;
        public static bool QueuedRelease = false;
        private static bool isDragging = false;
        private static bool isBoxSelecting = false;
        public static bool BoxSelecting => isBoxSelecting;
        public static bool GroupSelecting => Input.GetKey(KickStart.MultiSelect);

        private static Vector3 ScreenBoxStart = Vector3.zero;

        private static float SuccessBoxDiv = 16;

        public static RTSCursorState cursorState = RTSCursorState.Empty;
        private static bool ControlState = false;


        internal static bool dirty = false;

        internal static TankAIHelper GrabbedThisFrame;
        public TankAIHelper Leading => LocalPlayerTechsControlled.FirstOrDefault();
        public TankAIHelper PlayerHovered { get; private set; }
        public ListHashSet<TankAIHelper> LocalPlayerTechsControlled { get; private set; } = new ListHashSet<TankAIHelper>();
        public TankAIHelper OtherHovered { get; private set; }
        public List<TankAIHelper> EnemyTargets { get; private set; } = new List<TankAIHelper>();

        public Dictionary<TankAIHelper, Queue<WorldPosition>> TechMovementQueue = new Dictionary<TankAIHelper, Queue<WorldPosition>>();

        public List<List<TankAIHelper>> SavedGroups = new List<List<TankAIHelper>> {
            {new List<TankAIHelper>()},
            {new List<TankAIHelper>()},
            {new List<TankAIHelper>()},
            {new List<TankAIHelper>()},
            {new List<TankAIHelper>()},
            {new List<TankAIHelper>()},
            {new List<TankAIHelper>()},
            {new List<TankAIHelper>()},
            {new List<TankAIHelper>()},
            {new List<TankAIHelper>()}
        };
        
        public static void Initiate()
        {
            if (!KickStart.AllowPlayerRTSHUD || inst)
                return;
            inst = new GameObject("PlayerRTSControl").AddComponent<ManWorldRTS>();
            DebugTAC_AI.Log(KickStart.ModID + ": Created PlayerRTSControl.");
            //ManPointer.inst.MouseEvent.Subscribe(OnMouseEvent); - Only updates when in active game, not spectator
            Singleton.Manager<ManGameMode>.inst.ModeSwitchEvent.Subscribe(OnWorldReset);
            Singleton.Manager<CameraManager>.inst.CameraSwitchEvent.Subscribe(OnCameraChange);
            TankAIManager.TechRemovedEvent.Subscribe(ReleaseControl);
            PlayerRTSUI.Initiate();
        }
        public static void DeInit()
        {
            if (!inst)
                return;
            PlayerRTSUI.DeInit();
            inst.PurgeAllNull();
            inst.ClearList();
            TankAIManager.TechRemovedEvent.Unsubscribe(ReleaseControl);
            Singleton.Manager<ManGameMode>.inst.ModeSwitchEvent.Unsubscribe(OnWorldReset);
            Singleton.Manager<CameraManager>.inst.CameraSwitchEvent.Unsubscribe(OnCameraChange);
            Destroy(SelectWindow);
            Destroy(autoWindow);
            Destroy(inst.gameObject);
            SelectWindow = null;
            autoWindow = null;
            inst = null;
            DebugTAC_AI.Log(KickStart.ModID + ": Removed PlayerRTSControl.");

        }
        public static void DelayedInitiate()
        {
            KickStart.MainOfficialInit();
            if (!KickStart.AllowPlayerRTSHUD)
                return;
            if (SelectHalo.SelectCirclePrefab == null)
            {
                DebugTAC_AI.Log(KickStart.ModID + ": Creating SelectCircle.");
                SelectHalo.SelectCirclePrefab = new GameObject("SelectCircle");
                SelectHalo.SelectCirclePrefab.AddComponent<SelectHalo>();
                Material[] mats = Resources.FindObjectsOfTypeAll<Material>();
                mats = mats.Where(cases => cases.name == "MAT_SFX_Explosion_01_Shockwave").ToArray();
                foreach (Material matcase in mats)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Getting " + matcase.name + "...");
                }
                Material mat = mats.ElementAt(0);
                SelectHalo.halos.Add(RTSHaloState.Default, mat);
                try
                {
                    SelectHalo.halos.Add(RTSHaloState.Attack, RawTechExporter.CreateMaterial("O_AttackTarg.png", mat));
                    SelectHalo.halos.Add(RTSHaloState.Hover, RawTechExporter.CreateMaterial("O_HoverSelect.png", mat));
                    SelectHalo.halos.Add(RTSHaloState.Select, RawTechExporter.CreateMaterial("O_AllySelect.png", mat));
                }
                catch { }

                //SelectHalo.SelectCirclePrefab.AddComponent<MeshRenderer>().material = mat;
                var ps = SelectHalo.SelectCirclePrefab.AddComponent<ParticleSystem>();
                var s = ps.shape;
                //s.texture = (Texture2D)mat.mainTexture;
                s.textureColorAffectsParticles = false;
                s.shapeType = ParticleSystemShapeType.Circle;
                s.radius = 0;
                s.sphericalDirectionAmount = 0;
                var m = ps.main;
                m.startColor = new Color(1f, 0.35f, 0.25f, 0.125f);
                m.startLifetime = 60;
                m.maxParticles = 1;
                m.startSpeed = 0;
                m.startSize = 1;
                m.startRotation = 0;
                var e = ps.emission;
                e.rateOverTime = 10;
                var r = ps.rotationOverLifetime;
                r.enabled = true;
                //r.separateAxes = false;
                r.z = new ParticleSystem.MinMaxCurve
                {
                    mode = ParticleSystemCurveMode.Constant,
                    constant = 24f,
                };
                r.zMultiplier = 1;
                var psr = SelectHalo.SelectCirclePrefab.GetComponent<ParticleSystemRenderer>();
                psr.renderMode = ParticleSystemRenderMode.Billboard;
                psr.material = mat;
                psr.maxParticleSize = 3000;
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                SelectHalo.SelectCirclePrefab.SetActive(false);
                DebugTAC_AI.Log(KickStart.ModID + ": Created SelectCircle.");
            }

            SelectWindow = Instantiate(new GameObject("TechSelectRect"));
            SelectWindow.AddComponent<GUIRectSelect>();
            autoWindow = Instantiate(new GameObject("AutoPilot"));
            autoWindow.AddComponent<GUIRectAuto>();
        }
        public static void OnWorldReset()
        {
            if ((bool)inst)
            {
                inst.LocalPlayerTechsControlled.Clear();
                int numOp = inst.SavedGroups.Count;
                try
                {
                    for (int step = 0; step < numOp; step++)
                    {
                        inst.SavedGroups.ElementAt(step).Clear();
                    }
                }
                catch { }
            }
        }
        public static void OnCameraChange(CameraManager.Camera camera1, CameraManager.Camera camera2)
        {
            if (camera2 is PlayerFreeCamera PFC)
            {
                if (!PlayerIsInRTS)
                {
                    PlayerIsInRTS = true;
                    PlayerRTSUI.ResetPos();
                    PlayerRTSUI.SetActive(true);
                    TankAIManager.toggleAuto.SetToggleState(KickStart.AutopilotPlayer);
                    UpdatePlayerTechControlOnSwitch();
                    DebugTAC_AI.Log(KickStart.ModID + ": Player is in RTS view!");
                }
            }
            else
            {
                if (PlayerIsInRTS)
                {
                    PlayerIsInRTS = false;
                    PlayerRTSUI.SetActive(false);
                    TankAIManager.toggleAuto.SetToggleState(KickStart.AutopilotPlayer);
                    UpdatePlayerTechControlOnSwitch();
                }
            }
        }
        public static void OnTechRemoved(TankAIHelper helper)
        {
            if ((bool)inst)
            {
                if (helper != null)
                {
                    inst.LocalPlayerTechsControlled.Remove(helper);
                    int numOp = inst.SavedGroups.Count;
                    try
                    {
                        for (int step = 0; step < numOp; step++)
                        {
                            inst.SavedGroups.ElementAt(step).Remove(helper);
                        }
                    }
                    catch { }
                    inst.TechMovementQueue.Remove(helper);
                }
            }
        }
        public static void ReleaseControl(TankAIHelper helper)
        {
            if ((bool)inst)
            {
                if (helper != null && !ControlState)
                {
                    try
                    {
                        SetSelectHalo(helper, false);
                        helper.SetRTSState(false);
                    }
                    catch
                    {
                        DebugTAC_AI.Log(KickStart.ModID + ": ERROR ON SETTING ReleaseControl");
                    }
                    inst.LocalPlayerTechsControlled.Remove(helper);
                    inst.TechMovementQueue.Remove(helper);
                }
            }
        }


        public static Vector3 GetPlayerTargetOffset(Vector3 target)
        {
            if (ManWorld.inst.GetTerrainHeight(target, out float height))
            {
                if (height + 12 < target.y)
                {
                    target.y = Singleton.playerPos.y - 12;
                }
            }
            return target;
        }

        internal static Action<Vector3> CommandQueued = null;
        public static void OnRTSEvent(ManPointer.Event click, bool down)
        {
            if (RTSControl && down && !ManPointer.inst.DraggingItem)
            {
                Globals gInst = Globals.inst;

                if (click == ManPointer.Event.LMB)
                {
                    //DebugTAC_AI.Log(KickStart.ModID + ": LEFT MOUSE BUTTON");

                    Vector3 pos = Camera.main.transform.position;
                    RaycastHit rayman;

                    int layerMask = Globals.inst.layerTank.mask | Globals.inst.layerTerrain.mask | Globals.inst.layerLandmark.mask;
                    Physics.Raycast(ManUI.inst.ScreenPointToRay(Input.mousePosition), out rayman, MaxCommandDistance, layerMask, QueryTriggerInteraction.Ignore);


                    if (CommandQueued != null)
                    {
                        if ((bool)rayman.collider)
                        {
                            int layer = rayman.collider.gameObject.layer;
                            CommandQueued.Invoke(rayman.point);
                        }
                        CommandQueued = null;
                    }
                    else
                    {
                        if ((bool)rayman.collider)
                        {
                            int layer = rayman.collider.gameObject.layer;
                            if (KickStart.UseClassicRTSControls)
                            {
                                if (layer == gInst.layerTerrain || layer == gInst.layerLandmark)
                                {
                                    inst.ClearList();
                                }
                                else
                                {
                                    inst.SelectTankPlayer(ManVisible.inst.FindVisible(rayman.collider), inst.LocalPlayerTechsControlled);
                                }
                            }
                            else
                            {
                                if (layer == gInst.layerTerrain || layer == gInst.layerLandmark)
                                {
                                    if (QueuedRelease)
                                    {
                                        inst.ClearList();
                                        //DebugTAC_AI.Log(KickStart.ModID + ": Cleared Tech Selection.");
                                    }
                                    QueuedRelease = !QueuedRelease;
                                }
                                else
                                {
                                    QueuedRelease = false;
                                    inst.SelectTankPlayer(ManVisible.inst.FindVisible(rayman.collider), inst.LocalPlayerTechsControlled);
                                }
                            }
                        }
                        else
                        {   // We hit NOTHING
                            inst.ClearList();
                            return;
                        }
                    }
                }
                else if (click == ManPointer.Event.RMB)
                {
                    ControlState = true;

                    RaycastHit rayman;
                    int layerMask = Globals.inst.layerTank.mask | Globals.inst.layerTankIgnoreTerrain.mask | Globals.inst.layerTerrain.mask |
                        Globals.inst.layerLandmark.mask | Globals.inst.layerScenery.mask;
                    Physics.Raycast(ManUI.inst.ScreenPointToRay(Input.mousePosition), out rayman, MaxCommandDistance, layerMask, QueryTriggerInteraction.Ignore);
                    if ((bool)rayman.collider)
                    {
                        QueuedRelease = false;
                        inst.HandleSelection(rayman.point, ManVisible.inst.FindVisible(rayman.collider), inst.LocalPlayerTechsControlled);
                        /*
                        int layer = rayman.collider.gameObject.layer;
                        if (layer == gInst.layerTerrain || layer == gInst.layerLandmark)
                        {
                            QueuedRelease = false;
                            inst.HandleSelectTerrain(rayman);
                        }
                        else if (layer == gInst.layerScenery)
                        {
                            QueuedRelease = false;
                            inst.HandleSelectScenery(rayman);
                        }
                        else
                        {
                            QueuedRelease = false;
                            inst.HandleSelectTargetTank(rayman);
                        }*/
                    }
                    ControlState = false;
                }
            }
        }
        public void HandleBoxSelectUnits()
        {
            //DebugTAC_AI.Log(KickStart.ModID + ": GROUP Select ACTIVATED");
            Vector3 ScreenBoxEnd = Input.mousePosition;
            float HighX = ScreenBoxStart.x >= ScreenBoxEnd.x ? ScreenBoxStart.x : ScreenBoxEnd.x;
            float LowX = ScreenBoxStart.x < ScreenBoxEnd.x ? ScreenBoxStart.x : ScreenBoxEnd.x;
            float HighY = ScreenBoxStart.y >= ScreenBoxEnd.y ? ScreenBoxStart.y : ScreenBoxEnd.y;
            float LowY = ScreenBoxStart.y < ScreenBoxEnd.y ? ScreenBoxStart.y : ScreenBoxEnd.y;
            int Selects = 0;

            bool shift = GroupSelecting;
            if (!shift)
            {
                ClearList();
                if (GrabbedThisFrame.IsNotNull())
                {
                    if (StartControlling(GrabbedThisFrame, LocalPlayerTechsControlled))
                    {
                        SetSelectHalo(GrabbedThisFrame, true);
                        GrabbedThisFrame.SetRTSState(true);
                        SelectUnitSFX();
                    }
                }
            }
            bool unselect = false;
            foreach (Tank Tech in ManTechs.inst.CurrentTechs)
            {
                if (!(bool)Tech)
                    continue;
                var TechUnit = Tech.GetHelperInsured();
                if (TechUnit != null && GrabbedThisFrame != TechUnit)
                {
                    if (Tech.Team == ManPlayer.inst.PlayerTeam || DebugRawTechSpawner.CanCommandOtherTeams)
                    {
                        if (!PlayerIsInRTS && !KickStart.AutopilotPlayer && Tech == Singleton.playerTank)
                            continue;
                        Vector3 camPos = Singleton.camera.WorldToScreenPoint(Tech.boundsCentreWorldNoCheck);
                        if (LowX <= camPos.x && camPos.x <= HighX && LowY <= camPos.y && camPos.y <= HighY && camPos.z > 0)
                        {
                            Selects++;
                            if (KickStart.UseClassicRTSControls)
                            {
                                if (!LocalPlayerTechsControlled.Contains(TechUnit))
                                {
                                    if (StartControlling(TechUnit, LocalPlayerTechsControlled))
                                    {
                                        SetSelectHalo(TechUnit, true);
                                    }
                                }
                                else if (shift)
                                {
                                    if (StopControlling(TechUnit, LocalPlayerTechsControlled))
                                    {
                                        SetSelectHalo(TechUnit, false);
                                        unselect = true;
                                    }
                                }
                            }
                            else
                            {
                                if (!LocalPlayerTechsControlled.Contains(TechUnit))
                                {
                                    if (StartControlling(TechUnit, LocalPlayerTechsControlled))
                                    {
                                        SetSelectHalo(TechUnit, true);
                                    }
                                }
                                else if (!shift)
                                {
                                    if (StopControlling(TechUnit, LocalPlayerTechsControlled))
                                    {
                                        SetSelectHalo(TechUnit, false);
                                        unselect = true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            DebugTAC_AI.Info(KickStart.ModID + ": GROUP Selected " + Selects);
            if (Selects > 0)
            {
                LocalPlayerTechsControlled = LocalPlayerTechsControlled.
                    OrderBy(x => x.ActuallyWorks).ThenByDescending(x => x.tank.PlayerFocused).ToList();
                if (Leading)
                {
                    GUIAIManager.GetInfo(Leading);
                    SelectUnitSFX();
                    if (Selects > 1)
                        Invoke("SelectUnitSFXDelayed", 0.1f);
                }
            }
            else if (unselect)
                UnSelectUnitSFX();
            else
                Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.DropDown);

        }
        public void HandleGroups()
        {
            int groupNum = -1;
            if (KickStart.UseNumpadForGrouping)
            {
                for (int step = (int)KeyCode.Keypad0; step <= (int)KeyCode.Keypad9; step++)
                {
                    if (Input.GetKeyDown((KeyCode)step))
                    {
                        groupNum = step - (int)KeyCode.Keypad0;
                    }
                }
            }
            else
            {
                for (int step = (int)KeyCode.Alpha0; step <= (int)KeyCode.Alpha9; step++)
                {
                    if (Input.GetKeyDown((KeyCode)step))
                    {
                        groupNum = step - (int)KeyCode.Alpha0;
                    }
                }
            }
            if (groupNum < 0)
            {
                return;
            }
            bool working = false;
            if (LocalPlayerTechsControlled.Count > 0 && (GroupSelecting || SavedGroups[groupNum].Count == 0))
            {
                PurgeAllNull();
                SavedGroups[groupNum].Clear();
                SavedGroups[groupNum].AddRange(LocalPlayerTechsControlled);
                DebugTAC_AI.Log(KickStart.ModID + ": GROUP SAVED " + groupNum + ".");
                working = true;
            }
            else
            {
                ClearList();
                DebugTAC_AI.Log(KickStart.ModID + ": GROUP SELECTED " + groupNum + ".");
                foreach (TankAIHelper helper in SavedGroups[groupNum])
                {
                    if (!(bool)helper)
                        continue;
                    try
                    {
                        if (!PlayerIsInRTS && helper.tank == Singleton.playerTank)
                        {
                            continue;
                        }
                        if (!(PlayerIsInRTS && helper.tank == Singleton.playerTank) && helper.AIAlign != AIAlignment.Player)
                            continue;

                        if (!LocalPlayerTechsControlled.Contains(helper))
                        {
                            if (StartControlling(helper, LocalPlayerTechsControlled))
                            {
                                SetSelectHalo(helper, true);
                                DebugTAC_AI.Log(KickStart.ModID + ": Selected Tank " + helper.tank.name + ".");
                                working = true;
                            }
                        }
                    }
                    catch { }
                }
            }
            if (working)
                SelectUnitSFX();
        }

        private void SelectTankPlayer(Visible vis, ListHashSet<TankAIHelper> controlled)
        {
            Tank grabbedTech = vis?.tank;
            if ((bool)grabbedTech)
            {
                if (grabbedTech.Team == ManPlayer.inst.PlayerTeam || DebugRawTechSpawner.CanCommandOtherTeams)
                {
                    if (!KickStart.AutopilotPlayer && grabbedTech == Singleton.playerTank)
                        return;
                    var TechUnit = grabbedTech.GetHelperInsured();

                    if (KickStart.UseClassicRTSControls)
                    {
                        bool shift = GroupSelecting;
                        if (!controlled.Contains(TechUnit))
                        {
                            if (!shift)
                                ClearList();
                            if (StartControlling(TechUnit, controlled))
                            {
                                SetSelectHalo(TechUnit, true);
                                //DebugTAC_AI.Log(KickStart.ModID + ": Selected Tank " + grabbedTech.name + ".");
                                SelectUnitSFX();
                            }
                            QueuedRelease = false;
                        }
                        else
                        {
                            if (!QueuedRelease)
                            {
                                GrabAllSameName(TechUnit);
                            }
                            else
                            {
                                if (!shift)
                                {
                                    ClearList();
                                    if (StartControlling(TechUnit, controlled))
                                    {
                                        SetSelectHalo(TechUnit, true);
                                        //DebugTAC_AI.Log(KickStart.ModID + ": Selected Tank " + grabbedTech.name + ".");
                                        SelectUnitSFX();
                                    }
                                }
                                else
                                {
                                    if (StopControlling(TechUnit, controlled))
                                    {
                                        GrabbedThisFrame = TechUnit;
                                        SetSelectHalo(TechUnit, false);
                                        //DebugTAC_AI.Log(KickStart.ModID + ": Unselected Tank " + grabbedTech.name + ".");
                                        UnSelectUnitSFX();
                                    }
                                }
                                //DebugTAC_AI.Log(KickStart.ModID + ": Selected Tank " + grabbedTech.name + ".");
                            }
                            QueuedRelease = !QueuedRelease;
                        }
                    }
                    else
                    {
                        bool shift = GroupSelecting;
                        if (controlled.Contains(TechUnit) && !shift)
                        {
                            if (StopControlling(TechUnit, controlled))
                            {
                                GrabbedThisFrame = TechUnit;
                                SetSelectHalo(TechUnit, false);
                                //DebugTAC_AI.Log(KickStart.ModID + ": Unselected Tank " + grabbedTech.name + ".");
                                UnSelectUnitSFX();
                            }
                        }
                        else
                        {
                            if (shift)
                            {
                                GrabAllSameName(TechUnit);
                                return;
                            }
                            if (StartControlling(TechUnit, controlled))
                            {
                                SetSelectHalo(TechUnit, true);
                                //DebugTAC_AI.Log(KickStart.ModID + ": Selected Tank " + grabbedTech.name + ".");
                                SelectUnitSFX();
                            }
                        }
                    }
                }
                else if (AIGlobals.IsBaseTeamDynamic(grabbedTech.Team))
                {
                    GUINPTInteraction.GetTank(grabbedTech);
                }
            }
        }
        public void SelectTankAI(Tank Tech, ListHashSet<TankAIHelper> controlled)
        {
            if ((bool)Tech)
            {
                if (Tech.Team != ManPlayer.inst.PlayerTeam || DebugRawTechSpawner.CanCommandOtherTeams)
                {
                    if (!PlayerIsInRTS && Tech == Singleton.playerTank)
                        return;
                    var TechUnit = Tech.GetHelperInsured();

                    if (KickStart.UseClassicRTSControls)
                    {
                        bool shift = GroupSelecting;
                        if (!controlled.Contains(TechUnit))
                        {
                            if (!shift)
                                ClearList();
                            if (StartControlling(TechUnit, LocalPlayerTechsControlled))
                            {
                                SetSelectHalo(TechUnit, true);
                                //DebugTAC_AI.Log(KickStart.ModID + ": Selected Tank " + grabbedTech.name + ".");
                                SelectUnitSFX();
                            }
                            QueuedRelease = false;
                        }
                        else
                        {
                            if (!QueuedRelease)
                            {
                                GrabAllSameName(TechUnit);
                            }
                            else
                            {
                                if (!shift)
                                {
                                    ClearList();
                                    if (StartControlling(TechUnit, LocalPlayerTechsControlled))
                                    {
                                        SetSelectHalo(TechUnit, true);
                                        //DebugTAC_AI.Log(KickStart.ModID + ": Selected Tank " + grabbedTech.name + ".");
                                        SelectUnitSFX();
                                    }
                                }
                                else
                                {
                                    if (StopControlling(TechUnit, LocalPlayerTechsControlled))
                                    {
                                        GrabbedThisFrame = TechUnit;
                                        SetSelectHalo(TechUnit, false);
                                        //DebugTAC_AI.Log(KickStart.ModID + ": Unselected Tank " + grabbedTech.name + ".");
                                        UnSelectUnitSFX();
                                    }
                                }
                                //DebugTAC_AI.Log(KickStart.ModID + ": Selected Tank " + grabbedTech.name + ".");
                            }
                            QueuedRelease = !QueuedRelease;
                        }
                    }
                    else
                    {
                        bool shift = GroupSelecting;
                        if (controlled.Contains(TechUnit) && !shift)
                        {
                            if (StopControlling(TechUnit, LocalPlayerTechsControlled))
                            {
                                GrabbedThisFrame = TechUnit;
                                SetSelectHalo(TechUnit, false);
                                //DebugTAC_AI.Log(KickStart.ModID + ": Unselected Tank " + grabbedTech.name + ".");
                                UnSelectUnitSFX();
                            }
                        }
                        else
                        {
                            if (shift)
                            {
                                GrabAllSameName(TechUnit);
                                return;
                            }
                            if (StartControlling(TechUnit, LocalPlayerTechsControlled))
                            {
                                SetSelectHalo(TechUnit, true);
                                //DebugTAC_AI.Log(KickStart.ModID + ": Selected Tank " + grabbedTech.name + ".");
                                SelectUnitSFX();
                            }
                        }
                    }
                }
                else if (AIGlobals.IsBaseTeamDynamic(Tech.Team))
                {
                    GUINPTInteraction.GetTank(Tech);
                }
            }
        }

        public void HandleSelection(Vector3 point, Visible vis, ListHashSet<TankAIHelper> controlled)
        {
            if (vis?.resdisp)
                HandleSelectScenery(point, vis, controlled);
            else if (vis?.block)
                HandleSelectTargetTank(point, vis, controlled);
            else
                HandleSelectTerrain(point, controlled);
        }
        private void HandleSelectTargetTank(Vector3 point, Visible vis, ListHashSet<TankAIHelper> controlled)
        {
            //DebugTAC_AI.Log(KickStart.ModID + ": HandleSelectTargetTank.");
            Tank grabbedTech = vis.block.tank;
            if ((bool)grabbedTech)
            {
                bool responded = false;
                if (grabbedTech.IsEnemy(ManPlayer.inst.PlayerTeam))
                {   // Attack Move
                    foreach (TankAIHelper helper in controlled)
                    {
                        if (helper != null)
                        {
                            if (helper.lastAIType != AITreeType.AITypes.Escort)
                                helper.ForceAllAIsToEscort(true, false);
                            if (GroupSelecting)
                            {
                                helper.SetRTSState(true);
                                QueueNextDestination(helper, point);
                                if (ManNetwork.IsNetworked)
                                    NetworkHandler.TryBroadcastRTSAttack(helper.tank.netTech.netId.Value, grabbedTech.netTech.netId.Value);
                                helper.lastEnemy = grabbedTech.visible;
                            }
                            else
                            {
                                helper.RTSDestination = TankAIHelper.RTSDisabled;
                                helper.SetRTSState(true);
                                if (ManNetwork.IsNetworked)
                                    NetworkHandler.TryBroadcastRTSAttack(helper.tank.netTech.netId.Value, grabbedTech.netTech.netId.Value);
                                helper.lastEnemy = grabbedTech.visible;
                                TechMovementQueue.Remove(helper);
                            }
                            responded = true;
                        }
                    }
                    if (responded)
                        Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.LockOn);
                }
                else if (grabbedTech.IsFriendly(ManPlayer.inst.PlayerTeam))
                {
                    if (grabbedTech.IsPlayer)
                    {   // Reset to working order
                        foreach (TankAIHelper helper in controlled)
                        {
                            if (helper != null)
                            {
                                TechMovementQueue.Remove(helper);
                                SetOptionAuto(helper, AIType.Escort);
                                helper.RTSDestination = TankAIHelper.RTSDisabled;
                                helper.SetRTSState(false);
                                if (!ManNetwork.IsNetworked)
                                    helper.lastPlayer = grabbedTech.visible;
                                responded = true;
                            }
                        }
                    }
                    else
                    {   // Protect/Defend
                        try
                        {
                            if (grabbedTech.IsAnchored)
                            {
                                foreach (TankAIHelper helper in controlled)
                                {
                                    if (helper != null)
                                    {
                                        if (helper.isAegisAvail)
                                        {
                                            SetOptionAuto(helper, AIType.Assault);
                                            helper.RTSDestination = TankAIHelper.RTSDisabled;
                                            helper.SetRTSState(false);
                                            if (!ManNetwork.IsNetworked)
                                            {
                                                helper.foundBase = false;
                                                helper.CollectedTarget = false;
                                            }
                                        }
                                        else
                                        {
                                            if (helper.lastAIType != AITreeType.AITypes.Escort)
                                                helper.ForceAllAIsToEscort(true, false);
                                            helper.RTSDestination = grabbedTech.boundsCentreWorldNoCheck;
                                            helper.SetRTSState(true);
                                        }
                                        responded = true;
                                        TechMovementQueue.Remove(helper);
                                    }
                                }
                            }
                            else
                            {
                                foreach (TankAIHelper helper in controlled)
                                {
                                    if (helper != null)
                                    {
                                        //bool LandAIAssigned = help.DediAI < AIType.MTTurret;
                                        if (helper.isAegisAvail)// && LandAIAssigned)
                                        {
                                            SetOptionAuto(helper, AIType.Aegis);
                                            helper.RTSDestination = TankAIHelper.RTSDisabled;
                                            helper.SetRTSState(false);
                                            if (!ManNetwork.IsNetworked)
                                            {
                                                helper.lastCloseAlly = grabbedTech;
                                                helper.theResource = grabbedTech.visible;
                                                helper.CollectedTarget = false;
                                            }
                                        }
                                        else
                                        {
                                            if (helper.lastAIType != AITreeType.AITypes.Escort)
                                                helper.ForceAllAIsToEscort(true, false);
                                            helper.RTSDestination = grabbedTech.boundsCentreWorldNoCheck;
                                            helper.SetRTSState(true);
                                        }
                                        responded = true;
                                        TechMovementQueue.Remove(helper);
                                    }
                                }
                            }
                        }
                        catch
                        {
                            DebugTAC_AI.Log(KickStart.ModID + ": Error on Protect/Defend - Techs");
                            foreach (TankAIHelper helper in controlled)
                            {
                                DebugTAC_AI.Log(KickStart.ModID + ": " + helper.name);
                            }
                        }
                    }
                    if (responded)
                        Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.AIFollow);
                }
            }
            else
            {
                HandleSelectBlock(point, vis, controlled);
            }
        }
        private void HandleSelectTerrain(Vector3 point, ListHashSet<TankAIHelper> controlled)
        {
            //DebugTAC_AI.Log(KickStart.ModID + ": HandleSelectTerrain. - " + StackTraceUtility.ExtractStackTrace());
            Vector3 terrainPoint = GetPlayerTargetOffset(point);
            if (!GroupSelecting && controlled.Count == 1)
            {
                TankAIHelper helper = controlled.FirstOrDefault();
                if (helper != null)
                {
                    helper.RTSDestination = terrainPoint;
                    TechMovementQueue.Remove(helper);
                    QueueNextDestination(helper, terrainPoint);// INSURE direct path to position
                    if (helper.lastAIType != AITreeType.AITypes.Escort)
                        helper.ForceAllAIsToEscort(true, false);
                    helper.SetRTSState(true);
                }
            }
            else
            {
                if (GroupSelecting)
                {
                    foreach (TankAIHelper helper in controlled)
                    {
                        if (helper != null)
                        {
                            QueueNextDestination(helper, terrainPoint);
                            if (helper.lastAIType != AITreeType.AITypes.Escort)
                                helper.ForceAllAIsToEscort(true, false);
                            helper.SetRTSState(true);
                        }
                    }
                }
                else
                {
                    foreach (TankAIHelper helper in controlled)
                    {
                        if (helper != null)
                        {
                            helper.RTSDestination = terrainPoint;
                            TechMovementQueue.Remove(helper);
                            if (helper.lastAIType != AITreeType.AITypes.Escort)
                                helper.ForceAllAIsToEscort(true, false);
                            helper.SetRTSState(true);
                        }
                    }
                }
            }
            //DebugTAC_AI.Log(KickStart.ModID + ": HandleSelectTerrain.");
            if (controlled.Any())
                Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.AcceptMission);
        }
        private void HandleSelectScenery(Vector3 point, Visible vis, ListHashSet<TankAIHelper> controlled)
        {
            //DebugTAC_AI.Log(KickStart.ModID + ": HandleSelectScenery.");

            bool responded = false;
            if (vis)
            {
                ResourceDispenser node = vis.GetComponent<ResourceDispenser>();
                if ((bool)node)
                {
                    if (!node.GetComponent<Damageable>().Invulnerable)
                    {   // Mine Move
                        foreach (TankAIHelper helper in controlled)
                        {
                            if (helper != null)
                            {
                                bool LandAIAssigned = helper.DediAI < AIType.MTTurret;
                                if (helper.isProspectorAvail)
                                {
                                    SetOptionAuto(helper, AIType.Prospector);
                                    helper.RTSDestination = TankAIHelper.RTSDisabled;
                                    helper.SetRTSState(false);
                                    if (!ManNetwork.IsNetworked)
                                    {
                                        helper.theResource = vis;
                                        helper.CollectedTarget = false;
                                    }
                                }
                                else
                                {
                                    if (helper.lastAIType != AITreeType.AITypes.Escort)
                                        helper.ForceAllAIsToEscort(true, false);
                                    helper.RTSDestination = node.transform.position + (Vector3.up * 2);
                                    helper.SetRTSState(true);
                                }
                                responded = true;
                                TechMovementQueue.Remove(helper);
                            }
                        }
                        if (responded)
                            Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.Undo);
                    }
                    else
                    {   // Just issue a movement command, it's a flattened rock or "landmark"
                        HandleSelectTerrain(point, controlled);
                    }
                    return;
                }
            }
            try
            {
                Vector3 terrainPoint = GetPlayerTargetOffset(point);
                foreach (TankAIHelper helper in controlled)
                {
                    if (helper != null)
                    {
                        if (helper.lastAIType != AITreeType.AITypes.Escort)
                            helper.ForceAllAIsToEscort(true, false);
                        helper.RTSDestination = terrainPoint;
                        helper.SetRTSState(true);
                        responded = true;
                    }
                }
                if (responded)
                    Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.AcceptMission);
            }
            catch { }
        }
        private void HandleSelectBlock(Vector3 point, Visible vis, ListHashSet<TankAIHelper> controlled)
        {
            //DebugTAC_AI.Log(KickStart.ModID + ": HandleSelectBlock.");

            bool responded = false;
            if (vis)
            {
                TankBlock block = vis.GetComponent<TankBlock>();
                if ((bool)block)
                {
                    foreach (TankAIHelper helper in controlled)
                    {
                        if (helper != null)
                        {
                            bool LandAIAssigned = helper.DediAI < AIType.MTTurret;
                            if (helper.isScrapperAvail)
                            {
                                SetOptionAuto(helper, AIType.Scrapper);
                                helper.RTSDestination = TankAIHelper.RTSDisabled;
                                helper.SetRTSState(false);
                                if (!ManNetwork.IsNetworked)
                                {
                                    helper.theResource = vis;
                                    helper.CollectedTarget = false;
                                }
                            }
                            else
                            {
                                if (helper.lastAIType != AITreeType.AITypes.Escort)
                                    helper.ForceAllAIsToEscort(true, false);
                                helper.RTSDestination = block.transform.position + (Vector3.up * 2);
                                helper.SetRTSState(true);
                            }
                            responded = true;
                            TechMovementQueue.Remove(helper);
                        }
                    }
                    if (responded)
                        Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.Craft);
                    return;
                }
            }
            try
            {
                foreach (TankAIHelper helper in controlled)
                {
                    if (helper != null)
                    {
                        if (helper.lastAIType != AITreeType.AITypes.Escort)
                            helper.ForceAllAIsToEscort(true, false);
                        helper.RTSDestination = GetPlayerTargetOffset(point);
                        helper.SetRTSState(true);
                        responded = true;
                    }
                }
                if (responded)
                    Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.AcceptMission);
            }
            catch { }
        }


        public bool StartControlling(TankAIHelper helper, ListHashSet<TankAIHelper> controlled)
        {
            if (helper.tank.netTech?.NetPlayer)
            {
                if (helper.tank.netTech.NetPlayer != ManNetwork.inst.MyPlayer)
                    return false;// cannot grab other player tech
            }
            //if (!TechUnit.ActuallyWorks)
            //    return false;
            if (GrabbedThisFrame == null)
                GrabbedThisFrame = helper;
            controlled.Add(helper);
            dirty = true;
            return true;
        }
        public bool StopControlling(TankAIHelper helper, ListHashSet<TankAIHelper> controlled)
        {
            if (helper.tank.netTech?.NetPlayer)
            {
                return false;// cannot grab other player tech
            }
            controlled.Remove(helper);
            dirty = true;
            return true;
        }
        public void ControlAllPlayer()
        {
            bool selected = false;
            ClearList();
            foreach (var tech in ManTechs.inst.IterateTechs())
            {
                if (!(bool)tech)
                    continue;
                try
                {
                    if (tech.Team != ManPlayer.inst.PlayerTeam)
                        continue;
                    if (!PlayerIsInRTS && tech == Singleton.playerTank)
                        continue;
                    var TechUnit = tech.GetHelperInsured();

                    if (!LocalPlayerTechsControlled.Contains(TechUnit))
                    {
                        if (StartControlling(TechUnit, LocalPlayerTechsControlled))
                        {
                            selected = true;
                            SetSelectHalo(TechUnit, true);
                            //TechUnit.SetRTSState(true);
                            DebugTAC_AI.Log(KickStart.ModID + ": Selected Tank " + tech.name + ".");
                        }
                    }
                }
                catch { }
            }
            if (selected)
            {
                SelectUnitSFX();
                Invoke("SelectUnitSFXDelayed", 0.1f);
            }
        }



        public static bool HasMovementQueue(TankAIHelper helper)
        {
            if (inst)
                return inst.TechMovementQueue.ContainsKey(helper);
            return false;
        }
        public static bool IsCloseEnough(Tank tank, Vector3 posScene)
        {
            return (tank.boundsCentreWorldNoCheck - posScene).WithinBox(SuccessBoxDiv);
        }
        public void QueueNextDestination(TankAIHelper helper, Vector3 posScene)
        {
            if (TechMovementQueue.TryGetValue(helper, out Queue<WorldPosition> addTo))
            {
                addTo.Enqueue(WorldPosition.FromScenePosition(posScene));
            }
            else
            {
                Queue<WorldPosition> queue = new Queue<WorldPosition>();
                queue.Enqueue(WorldPosition.FromScenePosition(posScene));
                TechMovementQueue.Add(helper, queue);
            }
        }
        public bool TestNextDestination(TankAIHelper helper, out Vector3 nextPosScene)
        {
            if (TechMovementQueue.TryGetValue(helper, out Queue<WorldPosition> getFrom))
            {
                if (getFrom.Count > 0)
                {
                    nextPosScene = getFrom.Dequeue().ScenePosition;
                    return true;
                }
            }
            nextPosScene = Vector3.zero;
            return false;
        }


        public static void SetSelectHalo(TankAIHelper helper, bool selectedHalo)
        {
            if (!(bool)helper)
                return;
            
            if (selectedHalo)
            {
                var halo = helper.gameObject.GetOrAddComponent<SelectHalo>();
                halo.Initiate(helper);
            }
            else
            {
                try
                {
                    var halo = helper.gameObject.GetComponent<SelectHalo>();
                    if ((bool)halo)
                        helper.GetComponent<SelectHalo>().Remove();
                }
                catch { }
            }
        }
        public void GrabAllSameName(TankAIHelper techToFindNameOf)
        {
            bool working = false;
            foreach (Tank tech in ManTechs.inst.IteratePlayerTechs())
            {
                if (!(bool)tech)
                    continue;
                try
                {
                    if (tech.name == techToFindNameOf.tank.name)
                    {
                        if (!PlayerIsInRTS && tech == Singleton.playerTank)
                        {
                            continue;
                        }
                        TankAIHelper helper = tech.GetHelperInsured();
                        if (!(PlayerIsInRTS && tech == Singleton.playerTank) && helper.AIAlign != AIAlignment.Player)
                            continue;

                        if (!LocalPlayerTechsControlled.Contains(helper))
                        {
                            if (StartControlling(helper, LocalPlayerTechsControlled))
                            {
                                working = true;
                                SetSelectHalo(helper, true);
                                //TechUnit.SetRTSState(true);
                                DebugTAC_AI.Log(KickStart.ModID + ": Selected Tank " + tech.name + ".");
                            }
                        }
                    }
                }
                catch { }
            }
            if (working)
            {
                SelectUnitSFX();
                Invoke("SelectUnitSFXDelayed", 0.1f);
            }
        }
        public void ExplodeUnitBolts()
        {
            foreach (TankAIHelper helper in LocalPlayerTechsControlled)
            {
                if (helper != null)
                {
                    helper.BoltsFired = true;
                    helper.tank.control.ServerDetonateExplosiveBolt();
                    helper.PendingDamageCheck = true;
                }
            }
            DebugTAC_AI.Log(KickStart.ModID + ": HandleSelectTerrain.");
            if (LocalPlayerTechsControlled.Any())
                Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.SendToInventory);
        }

        private bool visEnabled = true;
        public void SetVisOfAll(bool visibleSelect)
        {
            if (visEnabled != visibleSelect)
            {
                foreach (TankAIHelper helper in LocalPlayerTechsControlled)
                {
                    if (helper != null)
                        SetSelectHalo(helper, visibleSelect);
                }
                if (!visibleSelect)
                    ClearRTSVis();
                visEnabled = visibleSelect;
            }
        }

        public void SetOptionAuto(TankAIHelper lastTank, AIType dediAI, bool force = false)
        {
            var enemy = lastTank.GetComponent<EnemyMind>();
            if (enemy)
            {
                switch (dediAI)
                {
                    case AIType.Assault:
                        enemy.CommanderMind = EnemyAttitude.Homing;
                        break;
                    case AIType.Aegis:
                        enemy.CommanderMind = EnemyAttitude.Guardian;
                        if (force)
                            enemy.MaxCombatRange = 0;
                        break;
                    case AIType.Prospector:
                        enemy.CommanderMind = EnemyAttitude.Miner;
                        if (force)
                            enemy.MaxCombatRange = 0;
                        break;
                    case AIType.Scrapper:
                        enemy.CommanderMind = EnemyAttitude.Junker;
                        if (force)
                            enemy.MaxCombatRange = 0;
                        break;
                    case AIType.Energizer:
                        throw new InvalidOperationException("Enemy equivalent for AIType.Energizer does not exist");
                    case AIType.MTTurret:
                        enemy.CommanderMind = EnemyAttitude.PartTurret;
                        break;
                    case AIType.MTStatic:
                        enemy.CommanderMind = EnemyAttitude.PartStatic;
                        break;
                    case AIType.MTMimic:
                        enemy.CommanderMind = EnemyAttitude.PartMimic;
                        break;
                    case AIType.Aviator:
                        throw new InvalidOperationException("Enemy equivalent for AIType.Aviator does not exist (too many outcomes)");
                    case AIType.Buccaneer:
                        enemy.EvilCommander = EnemyHandling.Naval;
                        break;
                    case AIType.Astrotech:
                        enemy.EvilCommander = EnemyHandling.Starship;
                        break;
                    default:
                        enemy.CommanderMind = EnemyAttitude.Default;
                        break;
                }
            }
            else
            if (ManNetwork.IsNetworked)
            {
                try
                {
                    if (lastTank.lastAIType != AITreeType.AITypes.Escort)
                        lastTank.ForceAllAIsToEscort(true, false);
                    NetworkHandler.TryBroadcastNewAIState(lastTank.tank.netTech.netId.Value, dediAI, AIDriverType.AutoSet);
                    lastTank.OnSwitchAI(false);
                    if (lastTank.DediAI != dediAI)
                    {
                        WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(lastTank.tank.visible);
                        AIGlobals.PopupPlayerInfo(dediAI.ToString(), worPos);
                    }
                    lastTank.DediAI = dediAI;

                    //TankDescriptionOverlay overlay = (TankDescriptionOverlay)GUIAIManager.bubble.GetValue(lastTank.tank);
                    //overlay.Update();
                }
                catch (Exception e)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Error on sending AI Option change!!!\n" + e);
                }
            }
            else
            {
                if (lastTank.lastAIType != AITreeType.AITypes.Escort)
                    lastTank.ForceAllAIsToEscort(true, false);
                lastTank.OnSwitchAI(false);
                if (lastTank.DediAI != dediAI)
                {
                    WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(lastTank.tank.visible);
                    AIGlobals.PopupPlayerInfo(dediAI.ToString(), worPos);
                }
                lastTank.DediAI = dediAI;

                //TankDescriptionOverlay overlay = (TankDescriptionOverlay)GUIAIManager.bubble.GetValue(lastTank.tank);
                //overlay.Update();
            }
        }
        
        public void ClearList()
        {
            if (OtherHovered)
                SetSelectHalo(OtherHovered, false);
            foreach (TankAIHelper helper in EnemyTargets)
            {
                if (helper != null)
                    SetSelectHalo(helper, false);
            }
            inst.EnemyTargets.Clear();
            foreach (TankAIHelper helper in LocalPlayerTechsControlled)
            {
                if (helper != null)
                    SetSelectHalo(helper, false);
            }
            inst.LocalPlayerTechsControlled.Clear();
            GUIAIManager.ResetInfo();
            UnSelectUnitSFX();
        }
        public static void UpdatePlayerTechControlOnSwitch()
        {
            try
            {
                if ((bool)inst)
                {
                    inst.PurgeAllNull();
                    if (!KickStart.AutopilotPlayer && (bool)Singleton.playerTank)
                    {
                        var TechUnit = Singleton.playerTank.GetHelperInsured();
                        SetSelectHalo(TechUnit, false);
                        inst.LocalPlayerTechsControlled.Remove(TechUnit);
                        TechUnit.SetRTSState(false);
                        UnSelectUnitSFX();
                    }
                }
            }
            catch 
            { 
            }
        }
        public void PurgeAllNull()
        {
            try
            {
                int numStep = LocalPlayerTechsControlled.Count;
                for (int step = 0; step < numStep;)
                {
                    TankAIHelper helper = LocalPlayerTechsControlled.ElementAt(step);
                    if (helper?.tank?.visible == null || !helper.tank.visible.isActive)
                    {
                        LocalPlayerTechsControlled.RemoveAt(step);
                        numStep--;
                    }
                    else if (helper.tank.blockman.blockCount == 0)
                    {
                        LocalPlayerTechsControlled.RemoveAt(step);
                        numStep--;
                    }
                    else
                        step++;
                }
            }
            catch
            {
            }
        }
        private void RemoveNullFromQueue()
        {
            try
            {
                int numStep = TechMovementQueue.Count;
                for (int step = 0; step < numStep;)
                {
                    TankAIHelper helper = TechMovementQueue.ElementAt(step).Key;
                    if (helper?.tank?.visible == null || !helper.tank.visible.isActive)
                    {
                        TechMovementQueue.Remove(helper);
                        numStep--;
                    }
                    else if (helper.tank.blockman.blockCount == 0)
                    {
                        TechMovementQueue.Remove(helper);
                        numStep--;
                    }
                    else
                        step++;
                }
            }
            catch
            {
            }
        }

        private static float lastClickTime = 0;
        private void Update()
        {
            try
            {
                if (!KickStart.AllowPlayerRTSHUD)
                {
                    if (LocalPlayerTechsControlled.Any())
                    {
                        PurgeAllNull();
                        ClearList();
                    }
                    SetVisOfAll(false);
                    return;
                }
                if (!ManPauseGame.inst.IsPaused && ManGameMode.inst.GetIsInPlayableMode())
                {
                    GrabbedThisFrame = null;
                    bool isRTSState = PlayerIsInRTS || PlayerRTSOverlay;
                    if (Input.GetKeyDown(KickStart.CommandHotkey))
                    {
                        PlayerRTSOverlay = !PlayerRTSOverlay;
                    }
                    SetVisOfAll(isRTSState);
                    linesLastUsed = linesUsed;
                    linesUsed = 0;
                    if (isRTSState)
                    {
                        bool notOverMenus = !ManModGUI.IsMouseOverModGUI || BoxSelecting;

                        UpdateCameraOverride();
                        UpdateCursor();


                        if (Input.GetMouseButtonUp(0))
                        {
                            if (isBoxSelecting)
                            {
                                if (!ManPointer.inst.DraggingItem)
                                {
                                    HandleBoxSelectUnits();
                                }
                            }
                            else if (notOverMenus)
                            {
                                OnRTSEvent(ManPointer.Event.LMB, true);
                                if (Time.realtimeSinceStartup - lastClickTime < 0.2f && Leading
                                    && Leading.tank.ControllableByLocalPlayer)
                                    ManTechs.inst.RequestSetPlayerTank(Leading.tank);
                                lastClickTime = Time.realtimeSinceStartup;
                            }
                        }
                        isDragging = (Input.mousePosition - ScreenBoxStart).sqrMagnitude > 32;//1024;
                        if (notOverMenus && !isDragging && Input.GetMouseButtonUp(1))
                        {
                            OnRTSEvent(ManPointer.Event.RMB, true);
                        }

                        if (!notOverMenus || (!Input.GetMouseButton(0) && !Input.GetMouseButton(1)) || ManPointer.inst.DraggingItem)
                        {
                            ScreenBoxStart = Input.mousePosition;
                        }
                        isBoxSelecting = isDragging && Input.GetMouseButton(0);


                        HandleGroups();
                        if (Input.GetKeyDown(KickStart.CommandBoltsHotkey))
                        {
                            ExplodeUnitBolts();
                        }

                        if (dirty)
                        {
                            PurgeAllNull();
                            LocalPlayerTechsControlled = LocalPlayerTechsControlled.OrderBy(x => x.tank.IsAnchored)
                                .ThenByDescending(x => x.tank.blockman.blockCount).ToList();
                        }

                        // Handle RTS unit guidence lines
                        if (!AIGlobals.HideHud)
                            UpdateLines();

                        DelayedUpdateClock += Time.deltaTime;
                        if (DelayedUpdateClock >= DelayedUpdateClockInterval)
                        {
                            DelayedUpdate();
                            DelayedUpdateClock = 0;
                        }
                    }
                    else
                        lastCameraPos = WorldPosition.FromScenePosition(Singleton.cameraTrans.position);
                    RemoveUnusedLines();
                }
                else
                {
                    isBoxSelecting = false;
                }
            }
            catch (Exception e)
            {
                DebugTAC_AI.LogWarnPlayerOnce("Critical Error in ManPlayerRTS.Update()", e);
            }
        }

        private static WorldPosition lastCameraPos;
        private const float followTime = 1.4f;
        private const float followTimeAim = 0.4f;
        private void UpdateCameraOverride()
        {
            if (DevCamLock == DebugCameraLock.LockCamToTech && Singleton.playerTank != null)
            {
                DebugTAC_AI.Assert("UpdateCameraOverride");
                var instH = Singleton.playerTank.GetHelperInsured();
                Vector3 tankPos = Singleton.playerTank.boundsCentreWorldNoCheck + new Vector3(0, instH.lastTechExtents * 0.75f, 0);
                Vector3 lookPos = Singleton.cameraTrans.position;
                Vector3 lookVec = tankPos - lookPos;
                if (instH.lastEnemy != null && instH.lastEnemy.isActive)
                {
                    float dist = Mathf.Clamp(lookVec.magnitude, 0, 9001);
                    float dividend = followTimeAim / Mathf.Clamp(Time.deltaTime, 0.05f, 100);
                    Vector3 enemyLookVec = (instH.lastEnemy.tank.boundsCentreWorldNoCheck - tankPos).normalized * (instH.lastTechExtents * 3);
                    lookPos = Vector3.MoveTowards(lastCameraPos.ScenePosition, tankPos - enemyLookVec, dist / dividend);
                    if (!Input.GetMouseButton(1))
                    {
                        Quaternion look = Quaternion.LookRotation(enemyLookVec);
                        Singleton.cameraTrans.rotation = Quaternion.Slerp(Singleton.cameraTrans.rotation, look, 1 / dividend);
                    }
                }
                else
                {
                    float dist = Mathf.Clamp(lookVec.magnitude - (instH.lastTechExtents * 3), 0, 9001);
                    float dividend = followTime / Mathf.Clamp(Time.deltaTime, 0.05f, 100);
                    lookPos = Vector3.MoveTowards(lastCameraPos.ScenePosition, tankPos, dist / dividend);
                    if (!Input.GetMouseButton(1))
                    {
                        Quaternion look = Quaternion.LookRotation(lookVec);
                        Singleton.cameraTrans.rotation = Quaternion.Slerp(Singleton.cameraTrans.rotation, look, 1 / dividend);
                    }
                }
                lastCameraPos = WorldPosition.FromScenePosition(lookPos);
                Singleton.cameraTrans.position = lookPos;
            }
            else
                lastCameraPos = WorldPosition.FromScenePosition(Singleton.cameraTrans.position);
        }

        private void UpdateLines()
        {
            if (DebugRawTechSpawner.ShowDebugFeedBack)
            {
                foreach (TankAIHelper helper in AIECore.IterateAllHelpers(x => x.MovingAndOrHasTarget))
                {
                    if (!UpdatePathfindingRouteVisualIfAny(helper))
                    {
                        Vector3 targLoc = helper.DriveTargetLocation;
                        targLoc.y += helper.lastTechExtents;
                        DrawDirection(helper, targLoc, color);
                    }
                }
            }
            else
            {
                if (PlayerHovered && !LocalPlayerTechsControlled.Contains(PlayerHovered))
                {
                    if (PlayerHovered.MovingAndOrHasTarget)
                    {
                        Vector3 targLoc = PlayerHovered.DriveTargetLocation;
                        targLoc.y += PlayerHovered.lastTechExtents;
                        DrawDirection(PlayerHovered, targLoc, (PlayerHovered == Leading) ? colorLeading : color);
                        UpdatePathfindingRouteVisualIfAny(PlayerHovered);
                    }
                }
                if (OtherHovered)
                {
                    if (OtherHovered.MovingAndOrHasTarget)
                    {
                        Vector3 targLoc = OtherHovered.DriveTargetLocation;
                        targLoc.y += OtherHovered.lastTechExtents;
                        DrawDirection(OtherHovered, targLoc, color);
                        UpdatePathfindingRouteVisualIfAny(OtherHovered);
                    }
                }
                if (ManNetwork.IsNetworked || !AIGlobals.PlayerClientFireCommand())
                {
                    foreach (TankAIHelper helper in LocalPlayerTechsControlled)
                    {
                        if (helper != null && helper.MovingAndOrHasTarget)
                        {
                            Vector3 targLoc = helper.DriveTargetLocation;
                            targLoc.y += helper.lastTechExtents;
                            DrawDirection(helper, targLoc, (helper == Leading) ? colorLeading : color);
                            if (Input.GetKey(KickStart.MultiSelect))
                                UpdatePathfindingRouteVisualIfAny(helper);
                        }
                    }
                }
                foreach (KeyValuePair<TankAIHelper, Queue<WorldPosition>> extended in TechMovementQueue)
                {
                    TankAIHelper helper = extended.Key;
                    if (helper != null && (LocalPlayerTechsControlled.Contains(helper) || PlayerHovered == helper))
                    {
                        Vector3 lastPoint = helper.RTSDestination;
                        foreach (var item in extended.Value)
                        {
                            Vector3 nextPoint = item.ScenePosition;
                            nextPoint.y += helper.lastTechExtents;
                            DrawDirection(lastPoint, nextPoint, (helper == Leading) ? colorLeading : color);
                            lastPoint = nextPoint;
                        }
                    }
                }
            }
        }
        private static List<WorldPosition> pathDispCache = new List<WorldPosition>();
        private bool UpdatePathfindingRouteVisualIfAny(TankAIHelper help)
        {
            if (help.UsingPathfinding)
            {
                if (help.MovementController is AIControllerDefault def && def.PathPlanned != null && def.PathPlanned.Count > 0)
                {
                    var path = new Queue<WorldPosition>(def.PathPlanned);
                    Vector3 lastPoint = help.tank.boundsCentreWorldNoCheck;
                    lastPoint.y += help.lastTechExtents;
                    foreach (var item in def.PathPlanned)
                    {
                        Vector3 nextPoint = item.ScenePosition;
                        nextPoint.y += help.lastTechExtents;
                        DrawDirection(lastPoint, nextPoint, colorPath);
                        lastPoint = nextPoint;
                    }
                    return true;
                }
                if (help.autoPather != null && help.autoPather.CanGetPath(1) && help.autoPather.IsRegistered)
                {
                    pathDispCache.Clear();
                    help.autoPather.GetPath(pathDispCache);
                    Vector3 lastPoint = pathDispCache.FirstOrDefault().ScenePosition;
                    for (int step = 1; step < pathDispCache.Count; step++)
                    {
                        Vector3 nextPoint = pathDispCache[step].ScenePosition;
                        nextPoint.y += help.lastTechExtents;
                        DrawDirection(lastPoint, nextPoint, colorPathing);
                        lastPoint = nextPoint;
                    }
                    return true;
                }
            }
            return false;
        }

        int mask = Globals.inst.layerTank.mask | Globals.inst.layerTankIgnoreTerrain.mask | Globals.inst.layerTerrain.mask | Globals.inst.layerScenery.mask;
        private void UpdateCursor()
        {
            Ray toCast;
            RaycastHit hit;
            toCast = ManUI.inst.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(toCast, out hit, MaxCommandDistance, mask, QueryTriggerInteraction.Collide))
            {
                Visible vis = Visible.FindVisibleUpwards(hit.collider);
                if (vis?.block)
                {
                    if (vis.block.tank)
                    {
                        if (vis.block.tank.Team == ManPlayer.inst.PlayerTeam || DebugRawTechSpawner.CanCommandOtherTeams)
                        {
                            var helper = vis.block.tank.GetHelperInsured();
                            if (helper && helper.ActuallyWorks && (!helper.tank.PlayerFocused || PlayerIsInRTS))
                            {
                                SetPlayerHovered(helper);
                                bool isAlreadySelected = LocalPlayerTechsControlled.Contains(helper);
                                if (!LocalPlayerTechsControlled.Any() ||
                                    (GroupSelecting && !isAlreadySelected))
                                {
                                    cursorState = RTSCursorState.Select; // Show Select Cursor
                                }
                                else
                                {
                                    if (GroupSelecting)
                                        cursorState = RTSCursorState.Moving; // Show Select Cursor
                                    else
                                    {   // Show Protect Cursor
                                        if (helper.tank.IsAnchored)
                                            cursorState = RTSCursorState.Scout;
                                        else
                                            cursorState = RTSCursorState.Protect;
                                    }
                                }
                            }
                            else
                            {   // Player IS NOT hovering over a valid target
                                if (PlayerHovered)
                                {
                                    SetPlayerHovered(null);
                                }
                                if (GroupSelecting)
                                    cursorState = RTSCursorState.Moving; // Show Select Cursor
                                else
                                {   // Show Protect Cursor
                                    if (vis.block.tank.IsAnchored)
                                        cursorState = RTSCursorState.Scout;
                                    else
                                        cursorState = RTSCursorState.Protect;
                                }
                            }
                            if (OtherHovered)
                            {
                                if (!EnemyTargets.Contains(OtherHovered))
                                    SetSelectHalo(OtherHovered, false);
                                OtherHovered = null;
                            }
                            return;
                        }
                        else if (vis.block.tank.Team != ManPlayer.inst.PlayerTeam)
                        {   // Show Attack Cursor
                            if (!ManBaseTeams.IsUnattackable(vis.block.tank.Team, ManPlayer.inst.PlayerTeam))
                                cursorState = RTSCursorState.Attack;
                            else
                                cursorState = RTSCursorState.Select;
                            var helper = vis.block.tank.GetHelperInsured();
                            if (helper)
                            {
                                if (OtherHovered != helper)
                                {
                                    if (OtherHovered)
                                    {
                                        if (!EnemyTargets.Contains(OtherHovered))
                                            SetSelectHalo(OtherHovered, false);
                                    }
                                    OtherHovered = helper;
                                    SetSelectHalo(OtherHovered, true);
                                }
                            }
                            else
                            {
                                if (OtherHovered)
                                {
                                    if (!EnemyTargets.Contains(OtherHovered))
                                        SetSelectHalo(OtherHovered, false);
                                    OtherHovered = null;
                                }
                            }
                            if (PlayerHovered)
                            {
                                SetPlayerHovered(null);
                            }
                            return;
                        }
                        cursorState = RTSCursorState.Empty; // Show Default Cursor
                    }
                    else
                    {
                        if (GroupSelecting)
                            cursorState = RTSCursorState.Moving; // Show Select Cursor
                        else
                        {
                            if (Leading)
                                cursorState = RTSCursorState.Fetch;
                            else
                                cursorState = RTSCursorState.Moving; // Show Default Cursor
                        }
                    }
                }
                else if (vis?.resdisp)
                {
                    if (GroupSelecting)
                        cursorState = RTSCursorState.Moving; // Show Select Cursor
                    else
                    {
                        if (Leading && !vis.resdisp.GetComponent<Damageable>().Invulnerable)
                            cursorState = RTSCursorState.Mine;
                        else
                            cursorState = RTSCursorState.Moving; // Show Default Cursor
                    }
                }
                else
                {
                    if (Leading && hit.collider.GetComponent<TerrainCollider>())
                        cursorState = RTSCursorState.Moving; // Show Move Cursor
                    else
                        cursorState = RTSCursorState.Empty; // Show Default Cursor
                }
            }
            else
                cursorState = RTSCursorState.Empty; // Show Default Cursor
            if (PlayerHovered)
            {
                SetPlayerHovered(null);
            }
            if (OtherHovered)
            {
                if (!EnemyTargets.Contains(OtherHovered))
                    SetSelectHalo(OtherHovered, false);
                OtherHovered = null;
            }
        }
        private void ClearRTSVis()
        {
            if (OtherHovered)
            {
                if (!EnemyTargets.Contains(OtherHovered))
                    SetSelectHalo(OtherHovered, false);
                OtherHovered = null;
            }
            if (PlayerHovered)
            {
                SetPlayerHovered(null);
            }
        }

        public void SetPlayerHovered(TankAIHelper helper)
        {
            if (helper)
            {
                if (PlayerHovered != helper)
                {
                    if (PlayerHovered)
                    {
                        if (!LocalPlayerTechsControlled.Contains(PlayerHovered))
                            SetSelectHalo(PlayerHovered, false);
                    }
                    PlayerHovered = helper;
                    SetSelectHalo(PlayerHovered, true);
                }
            }
            else
            {
                if (!LocalPlayerTechsControlled.Contains(PlayerHovered))
                    SetSelectHalo(PlayerHovered, false);
                PlayerHovered = null;
            }
        }

        private const float DelayedUpdateClockInterval = 1;
        private float DelayedUpdateClock = 0;
        private List<TankAIHelper> finishedMoveQueue = new List<TankAIHelper>();
        private List<TankAIHelper> currentTargets = new List<TankAIHelper>();

        private void DelayedUpdate()
        {
            foreach (var item in LocalPlayerTechsControlled)
            {
                if (item?.lastEnemyGet?.tank)
                {
                    var helper = item.lastEnemyGet.tank.GetHelperInsured();
                    SetSelectHalo(helper, true);
                    currentTargets.Add(helper);
                }
            }
            foreach (var item in EnemyTargets)
            {
                if (!currentTargets.Contains(item))
                    SetSelectHalo(item, false);
            }
            EnemyTargets.Clear();
            EnemyTargets.AddRange(currentTargets);
            currentTargets.Clear();

            RemoveNullFromQueue();
            foreach (var item in TechMovementQueue)
            {
                TankAIHelper helper = item.Key;
                //DebugTAC_AI.Log(" dist " + (help.tank.boundsCentreWorldNoCheck - help.RTSDestination).magnitude + " vs " + help.lastTechExtents * (1 + (help.recentSpeed / 12)));
                if ((helper.tank.boundsCentreWorldNoCheck - helper.RTSDestination).WithinSquareXZ(helper.lastTechExtents * (1 + (helper.recentSpeed / 12))))
                {
                    if (TestNextDestination(helper, out Vector3 nextPosScene))
                    {
                        helper.RTSDestination = nextPosScene;
                    }
                    else
                        finishedMoveQueue.Add(helper);
                }
            }
            foreach (var item in finishedMoveQueue)
            {
                TechMovementQueue.Remove(item);
            }
            finishedMoveQueue.Clear();
        }

        public void SelectUnitSFX()
        {
            if (Leading != null)
            {
                if (Leading.tank.IsAnchored)
                {
                    ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimHEPayTerminal);
                }
                else
                {
                    Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.AIIdle);
                    //Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.DropDown);
                }
            }
            else if (GrabbedThisFrame)
            {
                if (GrabbedThisFrame.tank.IsAnchored)
                {
                    ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimHEPayTerminal);
                }
                else
                {
                    Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.AIIdle);
                    //Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.DropDown);
                }
            }
        }
        public void SelectUnitSFXDelayed()
        {
            if (LocalPlayerTechsControlled.Count > 1)
            {
                TankAIHelper second = LocalPlayerTechsControlled.ElementAt(1);
                if (second)
                {
                    if (second.tank.IsAnchored)
                    {
                        ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimHEPayTerminal);
                    }
                    else
                    {
                        Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.AIIdle);
                        //Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.DropDown);
                    }
                }
            }
        }
        public static void UnSelectUnitSFX()
        {
            Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.Back);
        }

        private Color color = new Color(1f, 0.75f, 0.25f, 0.8f);//Color(0.25f, 1f, 0.25f, 0.75f);
        private Color colorLeading = new Color(1f, 0.4f, 0.75f, 0.65f);//Color(0.25f, 1f, 0.25f, 0.75f);
        private Color colorPath = new Color(0.4f, 1f, 0.75f, 0.65f);
        private Color colorPathing = new Color(0.0f, 1f, 1f, 0.65f);
        private int linesUsed = 0;
        private int linesLastUsed = 0;
        private List<GameObject> linesQueue = new List<GameObject>();
        private static Material lineMat = null;

        internal static Material GetLineMat()
        {
            if (lineMat == null)
            {
                try
                {
                    lineMat = RawTechExporter.CreateMaterial("M_MoveLine.png", new Material(Shader.Find("Sprites/Default")));
                }
                catch
                {
                    lineMat = new Material(Shader.Find("Sprites/Default"));
                }
            }
            return lineMat;
        }

        private GameObject MakeNewLine()
        {
            GameObject gO = Instantiate(new GameObject("TechMovementLine(" + linesQueue.Count + ")"), Vector3.zero, Quaternion.identity);

            var lr = gO.GetComponent<LineRenderer>();
            if (!(bool)lr)
            {
                GetLineMat();
                lr = gO.AddComponent<LineRenderer>();
                lr.material = lineMat;
                lr.positionCount = 2;
                lr.startWidth = 6.75f;
                lr.endWidth = 6.75f;
                lr.numCapVertices = 0;
            }
            lr.startColor = color;
            lr.endColor = color;
            lr.textureMode = LineTextureMode.Tile;
            Vector3[] vecs = new Vector3[2] { Vector3.zero, Vector3.one };
            lr.SetPositions(vecs);
            linesQueue.Add(gO);
            return gO;
        }
        private void DrawDirection(TankAIHelper helper, Vector3 endPosScene, Color refColor)
        {
            GameObject gO;
            if (linesQueue.Count <= linesUsed)
            {
                gO = MakeNewLine();
            }
            else
                gO = linesQueue[linesUsed];
            gO.SetActive(true);
            Vector3 pos = helper.tank.boundsCentreWorldNoCheck;
            Vector3 vertoffset = helper.lastTechExtents * Vector3.up;
            Vector3[] vecs = new Vector3[2] { endPosScene, pos + vertoffset };
            var lr = gO.GetComponent<LineRenderer>();
            lr.SetPositions(vecs);
            lr.startColor = refColor;
            lr.endColor = refColor;
            linesUsed++;
        }
        private void DrawDirection(Vector3 startPosScene, Vector3 endPosScene, Color refColor)
        {
            GameObject gO;
            if (linesQueue.Count <= linesUsed)
            {
                gO = MakeNewLine();
            }
            else
                gO = linesQueue[linesUsed];
            gO.SetActive(true);
            Vector3[] vecs = new Vector3[2] { endPosScene, startPosScene };
            var lr = gO.GetComponent<LineRenderer>();
            lr.SetPositions(vecs);
            lr.startColor = refColor;
            lr.endColor = refColor;
            linesUsed++;
        }
        private void RemoveUnusedLines()
        {
            for (int step = linesUsed; step < linesLastUsed; step++)
            {
                linesQueue[step].gameObject.SetActive(false);
            }
        }
        private void DrawSelectBox(Vector3 startPosGlobal, Vector3 endPosGlobal)
        {
            //DebugTAC_AI.Log(KickStart.ModID + ": DrawSelectBox - " + startPosGlobal + " | " + endPosGlobal);
            Vector3 sPos = startPosGlobal;
            Vector3 ePos = endPosGlobal;
            Vector3 ePosVert = ePos.SetY(sPos.y);
            Vector3 sPosVert = sPos.SetY(ePos.y);
            GameObject gO = Instantiate(new GameObject("TechSelectRect"), Vector3.zero, Quaternion.identity);

            var lr = gO.GetComponent<LineRenderer>();
            if (!(bool)lr)
            {
                lr = gO.AddComponent<LineRenderer>();
                lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.positionCount = 2;
                lr.startWidth = 0.25f;
                lr.endWidth = 0.25f;
                lr.numCapVertices = 4;
            }
            lr.startColor = color;
            lr.endColor = color;
            Vector3[] vecs = new Vector3[5] { sPos, ePosVert, ePos, sPosVert, sPos };
            lr.SetPositions(vecs);
            Destroy(gO, Time.deltaTime);
        }

        private static GameObject SelectWindow;
        private static Rect HotWindow = new Rect(0, 0, 200, 240);   // the "window"
        private static GUIStyle modifStyle;
        private static GUIStyleState modifStyleState;
        private const int AIBoxSelectID = 8006;
        private static Texture2D matRect;
        internal class GUIRectSelect : MonoBehaviour
        {
            private void OnGUI()
            {
                if (KickStart.IsIngame)
                {
                    if (isBoxSelecting)
                    {
                        GUISkin cache = GUI.skin;
                        GUI.skin = AltUI.MenuGUI;
                        if (modifStyle == null)
                            HotWindow = GUI.Window(AIBoxSelectID, HotWindow, GUIHandler, "");//"<b>BoxSelect</b>"
                        else
                            HotWindow = GUI.Window(AIBoxSelectID, HotWindow, GUIHandler, "", modifStyle);
                    }
                }
                else
                    isBoxSelecting = false;
            }
        }
        private static void GUIHandler(int ID)
        {
            if (modifStyle == null)
            {
                try
                {
                    //string DirectoryTarget = RawTechExporter.DLLDirectory + RawTechExporter.up + "AI_Icons" + RawTechExporter.up
                    //    + "AIOrderBox.png";
                    matRect = RawTechExporter.FetchTexture("AIOrderBox.png");
                }
                catch
                {
                    DebugTAC_AI.Assert(true, "ManPlayerRTS: AddBoxSelect - failed to fetch selector texture");
                    Texture2D[] mats = Resources.FindObjectsOfTypeAll<Texture2D>();
                    mats = mats.Where(cases => cases.name == "UI_CHECKBOX_OFF").ToArray();//GUI_DottedSquare
                    foreach (Texture2D matcase in mats)
                    {
                        DebugTAC_AI.Log(KickStart.ModID + ": Getting " + matcase.name + "...");
                    }
                    matRect = mats.ElementAt(0);
                }
                modifStyle = new GUIStyle(GUI.skin.window);
                modifStyleState = new GUIStyleState() { background = matRect, textColor = new Color(0, 0, 0, 1), };
                modifStyle.border = new RectOffset(matRect.width / 3, matRect.width / 3, matRect.height / 3, matRect.height / 3);
                modifStyle.normal = modifStyleState;
                modifStyle.hover = modifStyleState;
                modifStyle.active = modifStyleState;
                modifStyle.focused = modifStyleState;
                modifStyle.onNormal = modifStyleState;
                modifStyle.onHover = modifStyleState;
                modifStyle.onActive = modifStyleState;
                modifStyle.onFocused = modifStyleState;
            }
            Vector3 ScreenBoxEnd = Input.mousePosition;
            float HighX = ScreenBoxStart.x >= ScreenBoxEnd.x ? ScreenBoxStart.x : ScreenBoxEnd.x;
            float LowX = ScreenBoxStart.x < ScreenBoxEnd.x ? ScreenBoxStart.x : ScreenBoxEnd.x;
            float HighY = ScreenBoxStart.y >= ScreenBoxEnd.y ? ScreenBoxStart.y : ScreenBoxEnd.y;
            float LowY = ScreenBoxStart.y < ScreenBoxEnd.y ? ScreenBoxStart.y : ScreenBoxEnd.y;
            float highYCorrect = Display.main.renderingHeight - HighY;
            HotWindow = new Rect(LowX - 1, highYCorrect - 1, HighX - LowX + 2, HighY - LowY + 2);
        }


        // Player autopilot
        private static GameObject autoWindow;
        private const int PlayerAutopilotID = 8009;
        internal class GUIRectAuto : MonoBehaviour
        {
            public static bool MouseIsOverSubMenu()
            {
                if (!KickStart.EnableBetterAI)
                {
                    return false;
                }
                if (PlayerIsInRTS && !ManPauseGame.inst.IsPaused && Singleton.playerTank)
                {
                    Vector3 Mous = Input.mousePosition;
                    Mous.y = Display.main.renderingHeight - Mous.y;
                    return autopilotMenu.Contains(Mous);
                }
                return false;
            }
            private void OnGUI()
            {
                if (PlayerIsInRTS && !ManPauseGame.inst.IsPaused && !AIGlobals.HideHud && 
                    Singleton.playerTank?.GetHelperInsured())
                {
                    if (inst.LocalPlayerTechsControlled.Contains(Singleton.playerTank.GetHelperInsured()))
                    {
                        AltUI.StartUI();
                        autopilotMenu = GUI.Window(PlayerAutopilotID, autopilotMenu, GUIHandlerPlayerAutopilot, "", AltUI.MenuLeft);
                        if (UIHelpersExt.MouseIsOverSubMenu(autopilotMenu))
                            ManModGUI.IsMouseOverAnyModGUI = 2;
                        AltUI.EndUI();
                    } 
                }
                else
                {
                    autopilotMenu.x = 0;
                    autopilotMenu.y = 0;
                }
            }
        }
        internal static DebugCameraLock DevCamLock = DebugCameraLock.None;
#if DEBUG
        private static Rect autopilotMenu = new Rect(0, 0, 160, 110);   // the "window"
#else
        private static Rect autopilotMenu = new Rect(0, 0, 160, 80);   // the "window"
#endif

        private static void GUIHandlerPlayerAutopilot(int ID)
        {
            if (GUI.Button(new Rect(10, 10, 140, 30), KickStart.AutopilotPlayerRTS ? "<b>AUTOPILOT ON</b>" : "AUTOPILOT Off", KickStart.AutopilotPlayerRTS ? AltUI.ButtonGreen : AltUI.ButtonBlue))
            {
                KickStart.AutopilotPlayerRTS = !KickStart.AutopilotPlayerRTS;
                if (Singleton.playerTank)
                {
                    var tankInst = Singleton.playerTank.GetHelperInsured();
                    if (tankInst)
                    {
                        tankInst.ForceAllAIsToEscort(true, false);
                        tankInst.MovementAIControllerDirty = true;
                    }
                }
            }
            if (GUI.Button(new Rect(10, 40, 140, 30), DevCamLock == DebugCameraLock.LockCamToTech ? "<b>LOCKED TO TECH</b>" : "Lock to Tech"))
            {
                if (DevCamLock == DebugCameraLock.LockCamToTech)
                    DevCamLock = DebugCameraLock.None;
                else
                    DevCamLock = DebugCameraLock.LockCamToTech;
                lastCameraPos = WorldPosition.FromScenePosition(Singleton.cameraTrans.position);
            }
#if DEBUG
            if (GUI.Button(new Rect(10, 70, 140, 30), DevCamLock == DebugCameraLock.LockTechToCam ? "<b>LOCKED TO CAM</b>" : "Lock to Cam"))
            {
                if (DevCamLock == DebugCameraLock.LockTechToCam)
                    DevCamLock = DebugCameraLock.None;
                else
                    DevCamLock = DebugCameraLock.LockTechToCam;
            }
#endif
            GUI.DragWindow();
        }

        private static bool controlledDisp = false;
        private static bool typesDisp = false;
        public static void GUIGetTotalManaged()
        {
            if (inst == null)
            {
                GUILayout.Box("--- RTS Command [DISABLED] --- ");
                return;
            }
            GUILayout.Box("--- RTS Command --- ");
            GUILayout.Label("  Paths Queued: " + inst.TechMovementQueue.Count());
            int activeCount = 0;
            int baseCount = 0;
            Dictionary<AIAlignment, int> alignments = new Dictionary<AIAlignment, int>();
            foreach (AIAlignment item in Enum.GetValues(typeof(AIAlignment)))
            {
                alignments.Add(item, 0);
            }
            Dictionary<AIType, int> types = new Dictionary<AIType, int>();
            foreach (AIType item in Enum.GetValues(typeof(AIType)))
            {
                types.Add(item, 0);
            }
            foreach (var helper in inst.LocalPlayerTechsControlled)
            {
                if (helper != null && helper.isActiveAndEnabled)
                {
                    activeCount++;
                    alignments[helper.AIAlign]++;
                    types[helper.DediAI]++;
                    if (helper.tank.IsAnchored)
                        baseCount++;
                }
            }
            GUILayout.Label("  Num Bases: " + baseCount);
            if (GUILayout.Button("Total Controlled: " + inst.LocalPlayerTechsControlled.Count + " | Active: " + activeCount))
                controlledDisp = !controlledDisp;
            if (controlledDisp)
            {
                foreach (var item in alignments)
                {
                    GUILayout.Label("  Alignment: " + item.Key.ToString() + " - " + item.Value);
                }
            }
            if(GUILayout.Button("Types: " + types.Count))
                typesDisp = !typesDisp;
            if (typesDisp)
            {
                foreach (var item in types)
                {
                    GUILayout.Label("  Type: " + item.Key.ToString() + " - " + item.Value);
                }
            }
        }
    }
    internal enum DebugCameraLock
    {
        None,
        LockTechToCam,
        LockCamToTech,
    }
}
