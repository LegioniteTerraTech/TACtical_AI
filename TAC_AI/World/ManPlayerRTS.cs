using System;
using System.Collections.Generic;
using System.Linq;
using TAC_AI.AI;
using TAC_AI.Templates;
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

        private AIECore.TankAIHelper tech;
        private ParticleSystem ps;
        private GameObject circleInst;
        private float lastSize = 1;
        private RTSHaloState lastHalo = RTSHaloState.Default;
        private const float sizeMulti = 1.25f;
        private static Color Player = new Color(0f, 0f, 1f, 0.95f);
        private static Color Main = new Color(0f, 0.75f, 1f, 0.45f);
        private static Color NonMain = new Color(0f, 0.5f, 0.5f, 0.45f);
        private static Color Target = new Color(1f, 0.25f, 0.25f, 0.45f);
        private static Color Hovered = new Color(1f, 1f, 0.1f, 0.45f);
        internal static Dictionary<RTSHaloState, Material> halos = new Dictionary<RTSHaloState, Material>();

        public void Initiate(AIECore.TankAIHelper TechUnit)
        {
            if ((bool)circleInst)
            {
                circleInst.SetActive(true);
                return;
            }
            circleInst = Instantiate(SelectCirclePrefab, TechUnit.transform, false);
            circleInst.transform.position = TechUnit.tank.boundsCentreWorldNoCheck;
            circleInst.name = "SelectCircle";
            lastHalo = RTSHaloState.Default;
            tech = TechUnit;
            tech.tank.AttachEvent.Subscribe(OnSizeUpdate);
            tech.tank.DetachEvent.Subscribe(OnSizeUpdate);
            ps = circleInst.GetComponent<ParticleSystem>();
            var m = ps.main;
            m.startSize = TechUnit.lastTechExtents * sizeMulti;
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
                    psr.material = halos.First().Value;
                lastHalo = halo;
            }
        }
        private void Update()
        {
            try
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
                    if (ManPlayerRTS.inst.PlayerHovered == tech)
                        UpdateVisual(RTSHaloState.Hover);
                    else
                    {
                        if (ManPlayerRTS.autopilotPlayer && ManPlayerRTS.PlayerIsInRTS)
                            UpdateVisual(RTSHaloState.Select);
                        else
                            UpdateVisual(RTSHaloState.Default);
                    }
                }
                else if (ManPlayerRTS.inst.Leading == tech)
                {
                    m.startColor = Main;
                    if (ManPlayerRTS.inst.PlayerHovered == tech)
                        UpdateVisual(RTSHaloState.Hover);
                    else
                        UpdateVisual(RTSHaloState.Select);
                }
                else
                {
                    switch (tech.AIState)
                    {
                        case AIAlignment.Player:
                            m.startColor = NonMain;
                            if (ManPlayerRTS.inst.PlayerHovered == tech)
                                UpdateVisual(RTSHaloState.Hover);
                            else
                                UpdateVisual(RTSHaloState.Select);
                            break;
                        case AIAlignment.NonPlayer:
                            if (tech == ManPlayerRTS.inst.EnemyHovered)
                            {
                                m.startColor = Hovered;
                                UpdateVisual(RTSHaloState.Attack);
                            }
                            else
                            {
                                m.startColor = Target;
                                UpdateVisual(RTSHaloState.Attack);
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
                DebugTAC_AI.Log("TACtical_AI: SelectHalo - Removal failiure - was it edited but something else?!? " + e);
            }
        }
    }
    public class ManPlayerRTS : MonoBehaviour
    {
        public static ManPlayerRTS inst;
        public static int MaxCommandDistance = 9001;//500;
        public static int MaxAllowedSizeForHighlight = 3;
        public static bool PlayerIsInRTS = false;
        public static bool PlayerRTSOverlay = false;
        public static bool QueuedRelease = false;
        private static bool isDragging = false;
        private static bool isBoxSelecting = false;
        public static bool BoxSelecting => isBoxSelecting;
        public static bool GroupSelecting => Input.GetKey(KickStart.MultiSelect);

        private static Vector3 ScreenBoxStart = Vector3.zero;

        private static float SuccessBoxDiv = 16;

        public static RTSCursorState cursorState = RTSCursorState.Empty;
        private static bool ControlState = false;


        private bool dirty = false;

        private AIECore.TankAIHelper GrabbedThisFrame;
        public AIECore.TankAIHelper Leading => LocalPlayerTechsControlled.Count > 0 ? LocalPlayerTechsControlled.First() : null;
        public AIECore.TankAIHelper PlayerHovered { get; private set; }
        public List<AIECore.TankAIHelper> LocalPlayerTechsControlled { get; private set; } = new List<AIECore.TankAIHelper>();
        public AIECore.TankAIHelper EnemyHovered { get; private set; }
        public List<AIECore.TankAIHelper> EnemyTargets { get; private set; } = new List<AIECore.TankAIHelper>();

        public Dictionary<AIECore.TankAIHelper, Queue<WorldPosition>> TechMovementQueue = new Dictionary<AIECore.TankAIHelper, Queue<WorldPosition>>();

        public List<List<AIECore.TankAIHelper>> SavedGroups = new List<List<AIECore.TankAIHelper>> {
            {new List<AIECore.TankAIHelper>()},
            {new List<AIECore.TankAIHelper>()},
            {new List<AIECore.TankAIHelper>()},
            {new List<AIECore.TankAIHelper>()},
            {new List<AIECore.TankAIHelper>()},
            {new List<AIECore.TankAIHelper>()},
            {new List<AIECore.TankAIHelper>()},
            {new List<AIECore.TankAIHelper>()},
            {new List<AIECore.TankAIHelper>()},
            {new List<AIECore.TankAIHelper>()}
        };
        
        public static void Initiate()
        {
            if (!KickStart.AllowStrategicAI || inst)
                return;
            inst = new GameObject("PlayerRTSControl").AddComponent<ManPlayerRTS>();
            DebugTAC_AI.Log("TACtical_AI: Created PlayerRTSControl.");
            //ManPointer.inst.MouseEvent.Subscribe(OnMouseEvent); - Only updates when in active game, not spectator
            Singleton.Manager<ManGameMode>.inst.ModeSwitchEvent.Subscribe(OnWorldReset);
            Singleton.Manager<CameraManager>.inst.CameraSwitchEvent.Subscribe(OnCameraChange);
            AIECore.TankAIManager.TechRemovedEvent.Subscribe(ReleaseControl);
            PlayerRTSUI.Initiate();
        }
        public static void DeInit()
        {
            if (!inst)
                return;
            PlayerRTSUI.DeInit();
            inst.PurgeAllNull();
            inst.ClearList();
            AIECore.TankAIManager.TechRemovedEvent.Unsubscribe(ReleaseControl);
            Singleton.Manager<ManGameMode>.inst.ModeSwitchEvent.Unsubscribe(OnWorldReset);
            Singleton.Manager<CameraManager>.inst.CameraSwitchEvent.Unsubscribe(OnCameraChange);
            Destroy(SelectWindow);
            Destroy(autoWindow);
            Destroy(inst.gameObject);
            SelectWindow = null;
            autoWindow = null;
            inst = null;
            DebugTAC_AI.Log("TACtical_AI: Removed PlayerRTSControl.");

        }
        public static void DelayedInitiate()
        {
            if (!KickStart.AllowStrategicAI)
                return;
            if (SelectHalo.SelectCirclePrefab == null)
            {
                DebugTAC_AI.Log("TACtical_AI: Creating SelectCircle.");
                SelectHalo.SelectCirclePrefab = new GameObject("SelectCircle");
                SelectHalo.SelectCirclePrefab.AddComponent<SelectHalo>();
                Material[] mats = Resources.FindObjectsOfTypeAll<Material>();
                mats = mats.Where(cases => cases.name == "MAT_SFX_Explosion_01_Shockwave").ToArray();
                foreach (Material matcase in mats)
                {
                    DebugTAC_AI.Log("TACtical_AI: Getting " + matcase.name + "...");
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
                DebugTAC_AI.Log("TACtical_AI: Created SelectCircle.");
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
                    DebugTAC_AI.Log("TACtical AI: Player is in RTS view!");
                }
            }
            else
            {
                if (PlayerIsInRTS)
                {
                    PlayerIsInRTS = false;
                    PlayerRTSUI.SetActive(false);
                    RemovePlayerTech();
                }
            }
        }
        public static void OnTechRemoved(AIECore.TankAIHelper TechUnit)
        {
            if ((bool)inst)
            {
                if (TechUnit != null)
                {
                    inst.LocalPlayerTechsControlled.Remove(TechUnit);
                    int numOp = inst.SavedGroups.Count;
                    try
                    {
                        for (int step = 0; step < numOp; step++)
                        {
                            inst.SavedGroups.ElementAt(step).Remove(TechUnit);
                        }
                    }
                    catch { }
                    inst.TechMovementQueue.Remove(TechUnit);
                }
            }
        }
        public static void ReleaseControl(AIECore.TankAIHelper TechUnit)
        {
            if ((bool)inst)
            {
                if (TechUnit != null && !ControlState)
                {
                    try
                    {
                        SetSelectHalo(TechUnit, false);
                        TechUnit.SetRTSState(false);
                    }
                    catch
                    {
                        DebugTAC_AI.Log("TACtical_AI: ERROR ON SETTING ReleaseControl");
                    }
                    inst.LocalPlayerTechsControlled.Remove(TechUnit);
                    inst.TechMovementQueue.Remove(TechUnit);
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

        public static void OnRTSEvent(ManPointer.Event click, bool down)
        {
            if ((PlayerRTSOverlay || PlayerIsInRTS) && down && !ManPointer.inst.DraggingItem)
            {
                int layerMask = Globals.inst.layerTank.mask | Globals.inst.layerTankIgnoreTerrain.mask | Globals.inst.layerTerrain.mask | Globals.inst.layerLandmark.mask | Globals.inst.layerScenery.mask;
                Globals gInst = Globals.inst;

                if (click == ManPointer.Event.LMB)
                {
                    //Debug.Log("TACtical_AI: LEFT MOUSE BUTTON");

                    Vector3 pos = Camera.main.transform.position;
                    Vector3 posD = Singleton.camera.ScreenPointToRay(Input.mousePosition).direction.normalized;
                    RaycastHit rayman;

                    Physics.Raycast(new Ray(pos, posD), out rayman, MaxCommandDistance, layerMask);


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
                                inst.HandleSelectTank(rayman);
                            }
                        }
                        else
                        {
                            if (layer == gInst.layerTerrain || layer == gInst.layerLandmark)
                            {
                                if (QueuedRelease)
                                {
                                    inst.ClearList();
                                    //Debug.Log("TACtical_AI: Cleared Tech Selection.");
                                }
                                QueuedRelease = !QueuedRelease;
                            }
                            else
                            {
                                QueuedRelease = false;
                                inst.HandleSelectTank(rayman);
                            }
                        }
                    }
                    else
                    {   // We hit NOTHING
                        inst.ClearList();
                        return;
                    }
                }
                else if (click == ManPointer.Event.RMB)
                {
                    ControlState = true;
                    Vector3 pos = Camera.main.transform.position;
                    Vector3 posD = Singleton.camera.ScreenPointToRay(Input.mousePosition).direction.normalized;

                    RaycastHit rayman;
                    Physics.Raycast(pos, posD, out rayman, MaxCommandDistance, layerMask);
                    if ((bool)rayman.collider)
                    {
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
                        }
                    }
                    ControlState = false;
                }
            }
        }
        public void HandleBoxSelectUnits()
        {
            //Debug.Log("TACtical_AI: GROUP Select ACTIVATED");
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
                    if (SelectTank(GrabbedThisFrame))
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
                AIECore.TankAIHelper TechUnit = Tech.GetComponent<AIECore.TankAIHelper>();
                if (TechUnit != null && GrabbedThisFrame != TechUnit)
                {
                    if (Tech.Team == Singleton.Manager<ManPlayer>.inst.PlayerTeam)
                    {
                        if (!PlayerIsInRTS && Tech == Singleton.playerTank)
                            continue;
                        Vector3 camPos = Singleton.camera.WorldToScreenPoint(Tech.boundsCentreWorldNoCheck);
                        if (LowX <= camPos.x && camPos.x <= HighX && LowY <= camPos.y && camPos.y <= HighY)
                        {
                            Selects++;
                            if (KickStart.UseClassicRTSControls)
                            {
                                if (!LocalPlayerTechsControlled.Contains(TechUnit))
                                {
                                    if (SelectTank(TechUnit))
                                    {
                                        SetSelectHalo(TechUnit, true);
                                    }
                                }
                                else if (shift)
                                {
                                    if (UnselectTank(TechUnit))
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
                                    if (SelectTank(TechUnit))
                                    {
                                        SetSelectHalo(TechUnit, true);
                                    }
                                }
                                else if (!shift)
                                {
                                    if (UnselectTank(TechUnit))
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
            DebugTAC_AI.Log("TACtical_AI: GROUP Selected " + Selects);
            if (Selects > 0)
            {
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
                DebugTAC_AI.Log("TACtical_AI: GROUP SAVED " + groupNum + ".");
                working = true;
            }
            else
            {
                ClearList();
                DebugTAC_AI.Log("TACtical_AI: GROUP SELECTED " + groupNum + ".");
                foreach (AIECore.TankAIHelper TechUnit in SavedGroups[groupNum])
                {
                    if (!(bool)TechUnit)
                        continue;
                    try
                    {
                        if (!PlayerIsInRTS && TechUnit.tank == Singleton.playerTank)
                        {
                            continue;
                        }
                        if (!(PlayerIsInRTS && TechUnit.tank == Singleton.playerTank) && TechUnit.AIState != AIAlignment.Player)
                            continue;

                        if (!LocalPlayerTechsControlled.Contains(TechUnit))
                        {
                            if (SelectTank(TechUnit))
                            {
                                SetSelectHalo(TechUnit, true);
                                DebugTAC_AI.Log("TACtical_AI: Selected Tank " + TechUnit.tank.name + ".");
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

        public void HandleSelectTank(RaycastHit rayman)
        {
            Tank grabbedTech = rayman.collider.transform.root.GetComponent<Tank>();
            if ((bool)grabbedTech)
            {
                if (grabbedTech.Team == Singleton.Manager<ManPlayer>.inst.PlayerTeam)
                {
                    if (!PlayerIsInRTS && grabbedTech == Singleton.playerTank)
                    {
                        return;
                    }
                    var TechUnit = grabbedTech.GetComponent<AIECore.TankAIHelper>();
                    if (!(PlayerIsInRTS && grabbedTech == Singleton.playerTank) && TechUnit.AIState != AIAlignment.Player)
                        return;

                    if (KickStart.UseClassicRTSControls)
                    {
                        bool shift = GroupSelecting;
                        if (!LocalPlayerTechsControlled.Contains(TechUnit))
                        {
                            if (!shift)
                                ClearList();
                            if (SelectTank(TechUnit))
                            {
                                SetSelectHalo(TechUnit, true);
                                //Debug.Log("TACtical_AI: Selected Tank " + grabbedTech.name + ".");
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
                                    if (SelectTank(TechUnit))
                                    {
                                        SetSelectHalo(TechUnit, true);
                                        //Debug.Log("TACtical_AI: Selected Tank " + grabbedTech.name + ".");
                                        SelectUnitSFX();
                                    }
                                }
                                else
                                {
                                    if (UnselectTank(TechUnit))
                                    {
                                        GrabbedThisFrame = TechUnit;
                                        SetSelectHalo(TechUnit, false);
                                        //Debug.Log("TACtical_AI: Unselected Tank " + grabbedTech.name + ".");
                                        UnSelectUnitSFX();
                                    }
                                }
                                //Debug.Log("TACtical_AI: Selected Tank " + grabbedTech.name + ".");
                            }
                            QueuedRelease = !QueuedRelease;
                        }
                    }
                    else
                    {
                        bool shift = GroupSelecting;
                        if (LocalPlayerTechsControlled.Contains(TechUnit) && !shift)
                        {
                            if (UnselectTank(TechUnit))
                            {
                                GrabbedThisFrame = TechUnit;
                                SetSelectHalo(TechUnit, false);
                                //Debug.Log("TACtical_AI: Unselected Tank " + grabbedTech.name + ".");
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
                            if (SelectTank(TechUnit))
                            {
                                SetSelectHalo(TechUnit, true);
                                //Debug.Log("TACtical_AI: Selected Tank " + grabbedTech.name + ".");
                                SelectUnitSFX();
                            }
                        }
                    }
                }
                else if (AIGlobals.IsBaseTeam(grabbedTech.Team))
                {
                    GUIEvictionNotice.GetTank(grabbedTech);
                }
            }
        }
        public void HandleSelectTargetTank(RaycastHit rayman)
        {
            DebugTAC_AI.Log("TACtical_AI: HandleSelectTargetTank.");
            Tank grabbedTech = rayman.collider.transform.root.GetComponent<Tank>();
            if ((bool)grabbedTech)
            {
                bool responded = false;
                if (grabbedTech.IsEnemy(Singleton.Manager<ManPlayer>.inst.PlayerTeam))
                {   // Attack Move
                    foreach (AIECore.TankAIHelper help in LocalPlayerTechsControlled)
                    {
                        if (help != null)
                        {
                            if (help.lastAIType != AITreeType.AITypes.Escort)
                                help.ForceAllAIsToEscort(true);
                            if (GroupSelecting)
                            {
                                help.SetRTSState(true);
                                QueueNextDestination(help, rayman.point);
                                if (ManNetwork.IsNetworked)
                                    NetworkHandler.TryBroadcastRTSAttack(help.tank.netTech.netId.Value, grabbedTech.netTech.netId.Value);
                                help.lastEnemy = grabbedTech.visible;
                            }
                            else
                            {
                                help.RTSDestination = Vector3.zero;
                                help.SetRTSState(true);
                                if (ManNetwork.IsNetworked)
                                    NetworkHandler.TryBroadcastRTSAttack(help.tank.netTech.netId.Value, grabbedTech.netTech.netId.Value);
                                help.lastEnemy = grabbedTech.visible;
                                TechMovementQueue.Remove(help);
                            }
                            responded = true;
                        }
                    }
                    if (responded)
                        Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.LockOn);
                }
                else if (grabbedTech.IsFriendly(Singleton.Manager<ManPlayer>.inst.PlayerTeam))
                {
                    if (grabbedTech.IsPlayer)
                    {   // Reset to working order
                        foreach (AIECore.TankAIHelper help in LocalPlayerTechsControlled)
                        {
                            if (help != null)
                            {
                                TechMovementQueue.Remove(help);
                                SetOptionAuto(help, AIType.Escort);
                                help.RTSDestination = Vector3.zero;
                                help.SetRTSState(false);
                                if (!ManNetwork.IsNetworked)
                                    help.lastPlayer = grabbedTech.visible;
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
                                foreach (AIECore.TankAIHelper help in LocalPlayerTechsControlled)
                                {
                                    if (help != null)
                                    {
                                        if (help.isAegisAvail)
                                        {
                                            SetOptionAuto(help, AIType.Assault);
                                            help.RTSDestination = Vector3.zero;
                                            help.SetRTSState(false);
                                            if (!ManNetwork.IsNetworked)
                                            {
                                                help.foundBase = false;
                                                help.CollectedTarget = false;
                                            }
                                        }
                                        else
                                        {
                                            if (help.lastAIType != AITreeType.AITypes.Escort)
                                                help.ForceAllAIsToEscort(true);
                                            help.RTSDestination = grabbedTech.boundsCentreWorldNoCheck;
                                            help.SetRTSState(true);
                                        }
                                        responded = true;
                                        TechMovementQueue.Remove(help);
                                    }
                                }
                            }
                            else
                            {
                                foreach (AIECore.TankAIHelper help in LocalPlayerTechsControlled)
                                {
                                    if (help != null)
                                    {
                                        //bool LandAIAssigned = help.DediAI < AIType.MTTurret;
                                        if (help.isAegisAvail)// && LandAIAssigned)
                                        {
                                            SetOptionAuto(help, AIType.Aegis);
                                            help.RTSDestination = Vector3.zero;
                                            help.SetRTSState(false);
                                            if (!ManNetwork.IsNetworked)
                                            {
                                                help.lastCloseAlly = grabbedTech;
                                                help.theResource = grabbedTech.visible;
                                                help.CollectedTarget = false;
                                            }
                                        }
                                        else
                                        {
                                            if (help.lastAIType != AITreeType.AITypes.Escort)
                                                help.ForceAllAIsToEscort(true);
                                            help.RTSDestination = grabbedTech.boundsCentreWorldNoCheck;
                                            help.SetRTSState(true);
                                        }
                                        responded = true;
                                        TechMovementQueue.Remove(help);
                                    }
                                }
                            }
                        }
                        catch
                        {
                            DebugTAC_AI.Log("TACtical_AI: Error on Protect/Defend - Techs");
                            foreach (AIECore.TankAIHelper help in LocalPlayerTechsControlled)
                            {
                                DebugTAC_AI.Log("TACtical_AI: " + help.name);
                            }
                        }
                    }
                    if (responded)
                        Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.AIFollow);
                }
            }
            else
            {
                HandleSelectBlock(rayman);
            }
        }
        public void HandleSelectTerrain(RaycastHit rayman)
        {
            if (GroupSelecting)
            {
                foreach (AIECore.TankAIHelper help in LocalPlayerTechsControlled)
                {
                    if (help != null)
                    {
                        QueueNextDestination(help, GetPlayerTargetOffset(rayman.point));
                        if (help.lastAIType != AITreeType.AITypes.Escort)
                            help.ForceAllAIsToEscort(true);
                        help.SetRTSState(true);
                    }
                }
            }
            else
            {
                foreach (AIECore.TankAIHelper help in LocalPlayerTechsControlled)
                {
                    if (help != null)
                    {
                        help.RTSDestination = GetPlayerTargetOffset(rayman.point);
                        TechMovementQueue.Remove(help);
                        if (help.lastAIType != AITreeType.AITypes.Escort)
                            help.ForceAllAIsToEscort(true);
                        help.SetRTSState(true);
                    }
                }
            }
            DebugTAC_AI.Log("TACtical_AI: HandleSelectTerrain.");
            if (LocalPlayerTechsControlled.Count > 0)
                Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.AcceptMission);
        }
        public void HandleSelectScenery(RaycastHit rayman)
        {
            DebugTAC_AI.Log("TACtical_AI: HandleSelectScenery.");

            bool responded = false;
            Visible vis = Visible.FindVisibleUpwards(rayman.collider);
            if (vis)
            {
                ResourceDispenser node = vis.GetComponent<ResourceDispenser>();
                if ((bool)node)
                {
                    if (!node.GetComponent<Damageable>().Invulnerable)
                    {   // Mine Move
                        foreach (AIECore.TankAIHelper help in LocalPlayerTechsControlled)
                        {
                            if (help != null)
                            {
                                bool LandAIAssigned = help.DediAI < AIType.MTTurret;
                                if (help.isProspectorAvail)
                                {
                                    SetOptionAuto(help, AIType.Prospector);
                                    help.RTSDestination = Vector3.zero;
                                    help.SetRTSState(false);
                                    if (!ManNetwork.IsNetworked)
                                    {
                                        help.theResource = vis;
                                        help.CollectedTarget = false;
                                    }
                                }
                                else
                                {
                                    if (help.lastAIType != AITreeType.AITypes.Escort)
                                        help.ForceAllAIsToEscort(true);
                                    help.RTSDestination = node.transform.position + (Vector3.up * 2);
                                    help.SetRTSState(true);
                                }
                                responded = true;
                                TechMovementQueue.Remove(help);
                            }
                        }
                        if (responded)
                            Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.Undo);
                    }
                    else
                    {   // Just issue a movement command, it's a flattened rock or "landmark"
                        HandleSelectTerrain(rayman);
                    }
                    return;
                }
            }
            try
            {
                foreach (AIECore.TankAIHelper help in LocalPlayerTechsControlled)
                {
                    if (help != null)
                    {
                        if (help.lastAIType != AITreeType.AITypes.Escort)
                            help.ForceAllAIsToEscort(true);
                        help.RTSDestination = GetPlayerTargetOffset(rayman.point);
                        help.SetRTSState(true);
                        responded = true;
                    }
                }
                if (responded)
                    Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.AcceptMission);
            }
            catch { }
        }
        public void HandleSelectBlock(RaycastHit rayman)
        {
            DebugTAC_AI.Log("TACtical_AI: HandleSelectScenery.");

            bool responded = false;
            Visible vis = Visible.FindVisibleUpwards(rayman.collider);
            if (vis)
            {
                TankBlock block = vis.GetComponent<TankBlock>();
                if ((bool)block)
                {
                    foreach (AIECore.TankAIHelper help in LocalPlayerTechsControlled)
                    {
                        if (help != null)
                        {
                            bool LandAIAssigned = help.DediAI < AIType.MTTurret;
                            if (help.isScrapperAvail)
                            {
                                SetOptionAuto(help, AIType.Scrapper);
                                help.RTSDestination = Vector3.zero;
                                help.SetRTSState(false);
                                if (!ManNetwork.IsNetworked)
                                {
                                    help.theResource = vis;
                                    help.CollectedTarget = false;
                                }
                            }
                            else
                            {
                                if (help.lastAIType != AITreeType.AITypes.Escort)
                                    help.ForceAllAIsToEscort(true);
                                help.RTSDestination = block.transform.position + (Vector3.up * 2);
                                help.SetRTSState(true);
                            }
                            responded = true;
                            TechMovementQueue.Remove(help);
                        }
                    }
                    if (responded)
                        Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.Craft);
                    return;
                }
            }
            try
            {
                foreach (AIECore.TankAIHelper help in LocalPlayerTechsControlled)
                {
                    if (help != null)
                    {
                        if (help.lastAIType != AITreeType.AITypes.Escort)
                            help.ForceAllAIsToEscort(true);
                        help.RTSDestination = GetPlayerTargetOffset(rayman.point);
                        help.SetRTSState(true);
                        responded = true;
                    }
                }
                if (responded)
                    Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.AcceptMission);
            }
            catch { }
        }

        public bool SelectTank(AIECore.TankAIHelper TechUnit)
        {
            if (TechUnit.tank.netTech?.NetPlayer)
            {
                if (TechUnit.tank.netTech.NetPlayer != ManNetwork.inst.MyPlayer)
                    return false;// cannot grab other player tech
            }
            if (!TechUnit.ActuallyWorks)
                return false;
            if (GrabbedThisFrame == null)
                GrabbedThisFrame = TechUnit;
            LocalPlayerTechsControlled.Add(TechUnit);
            dirty = true;
            return true;
        }
        public bool UnselectTank(AIECore.TankAIHelper TechUnit)
        {
            if (TechUnit.tank.netTech?.NetPlayer)
            {
                return false;// cannot grab other player tech
            }
            LocalPlayerTechsControlled.Remove(TechUnit);
            dirty = true;
            return true;
        }
        public void SelectAllPlayer()
        {
            bool selected = false;
            ClearList();
            foreach (var tech in ManTechs.inst.IterateTechs())
            {
                if (!(bool)tech)
                    continue;
                try
                {
                    if (!PlayerIsInRTS && tech == Singleton.playerTank)
                    {
                        continue;
                    }
                    if (tech.Team != ManPlayer.inst.PlayerTeam)
                        continue;
                    AIECore.TankAIHelper TechUnit = tech.GetComponent<AIECore.TankAIHelper>();

                    if (!LocalPlayerTechsControlled.Contains(TechUnit))
                    {
                        if (SelectTank(TechUnit))
                        {
                            selected = true;
                            SetSelectHalo(TechUnit, true);
                            //TechUnit.SetRTSState(true);
                            DebugTAC_AI.Log("TACtical_AI: Selected Tank " + tech.name + ".");
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



        public static bool HasMovementQueue(AIECore.TankAIHelper TechUnit)
        {
            if (inst)
                return inst.TechMovementQueue.TryGetValue(TechUnit, out _);
            return false;
        }
        public static bool IsCloseEnough(Tank tank, Vector3 posScene)
        {
            return (tank.boundsCentreWorldNoCheck - posScene).WithinBox(SuccessBoxDiv);
        }
        public void QueueNextDestination(AIECore.TankAIHelper TechUnit, Vector3 posScene)
        {
            if (TechMovementQueue.TryGetValue(TechUnit, out Queue<WorldPosition> addTo))
            {
                addTo.Enqueue(WorldPosition.FromScenePosition(posScene));
            }
            else
            {
                Queue<WorldPosition> queue = new Queue<WorldPosition>();
                queue.Enqueue(WorldPosition.FromScenePosition(posScene));
                TechMovementQueue.Add(TechUnit, queue);
            }
        }
        public bool TestNextDestination(AIECore.TankAIHelper TechUnit, out Vector3 nextPosScene)
        {
            if (TechMovementQueue.TryGetValue(TechUnit, out Queue<WorldPosition> getFrom))
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


        public static void SetSelectHalo(AIECore.TankAIHelper TechUnit, bool selectedHalo)
        {
            if (!(bool)TechUnit)
                return;
            
            if (selectedHalo)
            {
                var halo = TechUnit.gameObject.GetOrAddComponent<SelectHalo>();
                halo.Initiate(TechUnit);
            }
            else
            {
                try
                {
                    var halo = TechUnit.gameObject.GetComponent<SelectHalo>();
                    if ((bool)halo)
                        TechUnit.GetComponent<SelectHalo>().Remove();
                }
                catch { }
            }
        }
        public void GrabAllSameName(AIECore.TankAIHelper techToFindNameOf)
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
                        AIECore.TankAIHelper TechUnit = tech.GetComponent<AIECore.TankAIHelper>();
                        if (!(PlayerIsInRTS && tech == Singleton.playerTank) && TechUnit.AIState != AIAlignment.Player)
                            continue;

                        if (!LocalPlayerTechsControlled.Contains(TechUnit))
                        {
                            if (SelectTank(TechUnit))
                            {
                                working = true;
                                SetSelectHalo(TechUnit, true);
                                //TechUnit.SetRTSState(true);
                                DebugTAC_AI.Log("TACtical_AI: Selected Tank " + tech.name + ".");
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
            foreach (AIECore.TankAIHelper help in LocalPlayerTechsControlled)
            {
                if (help != null)
                {
                    help.BoltsFired = true;
                    help.tank.control.ServerDetonateExplosiveBolt();
                    help.PendingDamageCheck = true;
                }
            }
            DebugTAC_AI.Log("TACtical_AI: HandleSelectTerrain.");
            if (LocalPlayerTechsControlled.Count > 0)
                Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.SendToInventory);
        }

        private bool visEnabled = true;
        public void SetVisOfAll(bool visibleSelect)
        {
            if (visEnabled != visibleSelect)
            {
                foreach (AIECore.TankAIHelper TechUnit in LocalPlayerTechsControlled)
                {
                    if (TechUnit != null)
                        SetSelectHalo(TechUnit, visibleSelect);
                }
                if (!visibleSelect)
                    ClearRTSVis();
                visEnabled = visibleSelect;
            }
        }

        public void SetOptionAuto(AIECore.TankAIHelper lastTank, AIType dediAI)
        {
            if (ManNetwork.IsNetworked)
            {
                try
                {
                    if (lastTank.lastAIType != AITreeType.AITypes.Escort)
                        lastTank.ForceAllAIsToEscort(true);
                    NetworkHandler.TryBroadcastNewAIState(lastTank.tank.netTech.netId.Value, dediAI, AIDriverType.AutoSet);
                    lastTank.OnSwitchAI(false);
                    if (lastTank.DediAI != dediAI)
                    {
                        WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(lastTank.tank.visible);
                        AIGlobals.PopupPlayerInfo(dediAI.ToString(), worPos);
                    }
                    lastTank.DediAI = dediAI;
                    lastTank.RecalibrateMovementAIController();

                    //TankDescriptionOverlay overlay = (TankDescriptionOverlay)GUIAIManager.bubble.GetValue(lastTank.tank);
                    //overlay.Update();
                }
                catch (Exception e)
                {
                    DebugTAC_AI.Log("TACtical_AI: Error on sending AI Option change!!!\n" + e);
                }
            }
            else
            {
                if (lastTank.lastAIType != AITreeType.AITypes.Escort)
                    lastTank.ForceAllAIsToEscort(true);
                lastTank.OnSwitchAI(false);
                if (lastTank.DediAI != dediAI)
                {
                    WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(lastTank.tank.visible);
                    AIGlobals.PopupPlayerInfo(dediAI.ToString(), worPos);
                }
                lastTank.DediAI = dediAI;
                lastTank.RecalibrateMovementAIController();

                //TankDescriptionOverlay overlay = (TankDescriptionOverlay)GUIAIManager.bubble.GetValue(lastTank.tank);
                //overlay.Update();
            }
        }
        
        public void ClearList()
        {
            if (EnemyHovered)
                SetSelectHalo(EnemyHovered, false);
            foreach (AIECore.TankAIHelper TechUnit in EnemyTargets)
            {
                if (TechUnit != null)
                    SetSelectHalo(TechUnit, false);
            }
            inst.EnemyTargets.Clear();
            foreach (AIECore.TankAIHelper TechUnit in LocalPlayerTechsControlled)
            {
                if (TechUnit != null)
                    SetSelectHalo(TechUnit, false);
            }
            inst.LocalPlayerTechsControlled.Clear();
            GUIAIManager.ResetInfo();
            UnSelectUnitSFX();
        }
        public static void RemovePlayerTech()
        {
            try
            {
                if ((bool)inst)
                {
                    inst.PurgeAllNull();
                    if ((bool)Singleton.playerTank)
                    {
                        var TechUnit = Singleton.playerTank.GetComponent<AIECore.TankAIHelper>();
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
                    AIECore.TankAIHelper help = LocalPlayerTechsControlled.ElementAt(step);
                    if (help == null)
                    {
                        LocalPlayerTechsControlled.RemoveAt(step);
                        numStep--;
                    }
                    else if (help.tank.blockman.blockCount == 0)
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
                    AIECore.TankAIHelper help = TechMovementQueue.ElementAt(step).Key;
                    if (help == null)
                    {
                        TechMovementQueue.Remove(help);
                        numStep--;
                    }
                    else if (help.tank.blockman.blockCount == 0)
                    {
                        TechMovementQueue.Remove(help);
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


        private void Update()
        {
            if (!KickStart.AllowStrategicAI)
            {
                if (LocalPlayerTechsControlled.Count > 0)
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
                if (!PlayerIsInRTS && Input.GetKeyDown(KickStart.CommandHotkey))
                {
                    PlayerRTSOverlay = !PlayerRTSOverlay;
                }
                SetVisOfAll(isRTSState);
                linesLastUsed = linesUsed;
                linesUsed = 0;
                if (isRTSState)
                {
                    bool notOverMenus = (!GUIAIManager.MouseIsOverSubMenu() && !PlayerRTSUI.MouseIsOverSubMenu()
                        && !GUIRectAuto.MouseIsOverSubMenu() && !DebugRawTechSpawner.IsOverMenu()) || BoxSelecting;

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
                            OnRTSEvent(ManPointer.Event.LMB, true);
                    }
                    if (notOverMenus && !isDragging && Input.GetMouseButtonUp(1))
                    {
                        OnRTSEvent(ManPointer.Event.RMB, true);
                    }

                    if (!notOverMenus || (!Input.GetMouseButton(0) && !Input.GetMouseButton(1)) || ManPointer.inst.DraggingItem)
                    {
                        ScreenBoxStart = Input.mousePosition;
                    }
                    isDragging = (Input.mousePosition - ScreenBoxStart).sqrMagnitude > 1024;
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
                    UpdateLines();

                    DelayedUpdateClock += Time.deltaTime;
                    if (DelayedUpdateClock >= DelayedUpdateClockInterval)
                    {
                        DelayedUpdate();
                        DelayedUpdateClock = 0;
                    }
                }

                RemoveUnusedLines();
            }
            else
            {
                isBoxSelecting = false;
            }
        }

        private void UpdateLines()
        {
            if (PlayerHovered && !LocalPlayerTechsControlled.Contains(PlayerHovered))
            {
                if (PlayerHovered.MovingAndOrHasTarget)
                {
                    DrawDirection(PlayerHovered, PlayerHovered.DriveTargetLocation, PlayerHovered == Leading);
                }
            }
            foreach (AIECore.TankAIHelper help in LocalPlayerTechsControlled)
            {
                if (help != null && help.MovingAndOrHasTarget)
                {
                    DrawDirection(help, help.DriveTargetLocation, help == Leading);
                }
            }
            foreach (KeyValuePair<AIECore.TankAIHelper, Queue<WorldPosition>> extended in TechMovementQueue)
            {
                AIECore.TankAIHelper helper = extended.Key;
                if (helper != null && (LocalPlayerTechsControlled.Contains(helper) || PlayerHovered == helper))
                {
                    Vector3 lastPoint = helper.RTSDestination;
                    foreach (var item in extended.Value)
                    {
                        Vector3 nextPoint = item.ScenePosition;
                        nextPoint.y += helper.lastTechExtents;
                        DrawDirection(helper, lastPoint, nextPoint, helper == Leading);
                        lastPoint = nextPoint;
                    }
                }
            }
        }
        int mask = Globals.inst.layerTank.mask | Globals.inst.layerTankIgnoreTerrain.mask | Globals.inst.layerTerrain.mask | Globals.inst.layerScenery.mask;
        private void UpdateCursor()
        {
            Ray toCast;
            RaycastHit hit;
            toCast = ManUI.inst.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(toCast, out hit, MaxCommandDistance, mask, QueryTriggerInteraction.Ignore))
            {
                Visible vis = Visible.FindVisibleUpwards(hit.collider);
                if (vis?.block)
                {
                    if (vis.block.tank)
                    {
                        if (vis.block.tank.Team == ManPlayer.inst.PlayerTeam)
                        {
                            var helper = vis.block.tank.GetComponent<AIECore.TankAIHelper>();
                            if (helper && helper.ActuallyWorks)
                            {
                                SetPlayerHovered(helper);
                                bool isAlreadySelected = LocalPlayerTechsControlled.Contains(helper);
                                if (LocalPlayerTechsControlled.Count == 0 ||
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
                            {
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
                            if (EnemyHovered)
                            {
                                if (!EnemyTargets.Contains(EnemyHovered))
                                    SetSelectHalo(EnemyHovered, false);
                                EnemyHovered = null;
                            }
                            return;
                        }
                        else if (Tank.IsEnemy(vis.block.tank.Team, ManPlayer.inst.PlayerTeam))
                        {   // Show Attack Cursor
                            cursorState = RTSCursorState.Attack;
                            var helper = vis.block.tank.GetComponent<AIECore.TankAIHelper>();
                            if (helper)
                            {
                                if (EnemyHovered != helper)
                                {
                                    if (EnemyHovered)
                                    {
                                        if (!EnemyTargets.Contains(EnemyHovered))
                                            SetSelectHalo(EnemyHovered, false);
                                    }
                                    EnemyHovered = helper;
                                    SetSelectHalo(EnemyHovered, true);
                                }
                            }
                            else
                            {
                                if (EnemyHovered)
                                {
                                    if (!EnemyTargets.Contains(EnemyHovered))
                                        SetSelectHalo(EnemyHovered, false);
                                    EnemyHovered = null;
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
            if (EnemyHovered)
            {
                if (!EnemyTargets.Contains(EnemyHovered))
                    SetSelectHalo(EnemyHovered, false);
                EnemyHovered = null;
            }
        }
        private void ClearRTSVis()
        {
            if (EnemyHovered)
            {
                if (!EnemyTargets.Contains(EnemyHovered))
                    SetSelectHalo(EnemyHovered, false);
                EnemyHovered = null;
            }
            if (PlayerHovered)
            {
                SetPlayerHovered(null);
            }
        }

        bool ignoreOneFrame = false;
        public void SetPlayerHovered(AIECore.TankAIHelper helper)
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
        private List<AIECore.TankAIHelper> finishedMoveQueue = new List<AIECore.TankAIHelper>();
        private List<AIECore.TankAIHelper> currentTargets = new List<AIECore.TankAIHelper>();

        private void DelayedUpdate()
        {
            foreach (var item in LocalPlayerTechsControlled)
            {
                if (item?.lastEnemy)
                {
                    var helper = item.lastEnemy.GetComponent<AIECore.TankAIHelper>();
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
                AIECore.TankAIHelper help = item.Key;
                //Debug.Log(" dist " + (help.tank.boundsCentreWorldNoCheck - help.RTSDestination).magnitude + " vs " + help.lastTechExtents * (1 + (help.recentSpeed / 12)));
                if ((help.tank.boundsCentreWorldNoCheck - help.RTSDestination).WithinSquareXZ(help.lastTechExtents * (1 + (help.recentSpeed / 12))))
                {
                    if (TestNextDestination(help, out Vector3 nextPosScene))
                    {
                        help.RTSDestination = nextPosScene;
                    }
                    else
                        finishedMoveQueue.Add(help);
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
                AIECore.TankAIHelper second = LocalPlayerTechsControlled.ElementAt(1);
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
        private int linesUsed = 0;
        private int linesLastUsed = 0;
        private List<GameObject> linesQueue = new List<GameObject>();
        private Material lineMat = null;

        private GameObject MakeNewLine()
        {
            GameObject gO = Instantiate(new GameObject("TechMovementLine(" + linesQueue.Count + ")"), Vector3.zero, Quaternion.identity);

            var lr = gO.GetComponent<LineRenderer>();
            if (!(bool)lr)
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
                lr = gO.AddComponent<LineRenderer>();
                lr.material = lineMat;
                lr.positionCount = 2;
                lr.startWidth = 4.4f;
                lr.endWidth = 4.4f;
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
        private void DrawDirection(AIECore.TankAIHelper help, Vector3 endPosScene, bool isLeading)
        {
            GameObject gO;
            if (linesQueue.Count <= linesUsed)
            {
                gO = MakeNewLine();
            }
            else
                gO = linesQueue[linesUsed];
            gO.SetActive(true);
            Vector3 pos = help.tank.boundsCentreWorldNoCheck;
            Vector3 vertoffset = help.lastTechExtents * Vector3.up;
            Vector3[] vecs = new Vector3[2] { endPosScene, pos + vertoffset };
            var lr = gO.GetComponent<LineRenderer>();
            lr.SetPositions(vecs);
            if (isLeading)
            {
                lr.startColor = colorLeading;
                lr.endColor = colorLeading;
            }
            else
            {
                lr.startColor = color;
                lr.endColor = color;
            }
            linesUsed++;
        }
        private void DrawDirection(AIECore.TankAIHelper help, Vector3 startPosScene, Vector3 endPosScene, bool isLeading)
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
            if (isLeading)
            {
                lr.startColor = colorLeading;
                lr.endColor = colorLeading;
            }
            else
            {
                lr.startColor = color;
                lr.endColor = color;
            }
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
            //Debug.Log("TACtical_AI: DrawSelectBox - " + startPosGlobal + " | " + endPosGlobal);
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
                        GUI.skin = AIGlobals.MenuGUI;
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
                    string DirectoryTarget = RawTechExporter.DLLDirectory + RawTechExporter.up + "AI_Icons" + RawTechExporter.up
                        + "AIOrderBox.png";
                    matRect = FileUtils.LoadTexture(DirectoryTarget);
                }
                catch
                {
                    DebugTAC_AI.Assert(true, "ManPlayerRTS: AddBoxSelect - failed to fetch selector texture");
                    Texture2D[] mats = Resources.FindObjectsOfTypeAll<Texture2D>();
                    mats = mats.Where(cases => cases.name == "UI_CHECKBOX_OFF").ToArray();//GUI_DottedSquare
                    foreach (Texture2D matcase in mats)
                    {
                        DebugTAC_AI.Log("TACtical_AI: Getting " + matcase.name + "...");
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
            HotWindow = new Rect(LowX - 25, highYCorrect - 25, HighX - LowX + 50, HighY - LowY + 50);
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
                if (PlayerIsInRTS && !ManPauseGame.inst.IsPaused && Singleton.playerTank?.GetComponent<AIECore.TankAIHelper>())
                {
                    if (inst.LocalPlayerTechsControlled.Contains(Singleton.playerTank.GetComponent<AIECore.TankAIHelper>()))
                    {
                        AIGlobals.FetchResourcesFromGame();
                        AIGlobals.StartUI();
                        autopilotMenu = GUI.Window(PlayerAutopilotID, autopilotMenu, GUIHandlerPlayerAutopilot, "", AIGlobals.MenuLeft);
                        AIGlobals.EndUI();
                    } 
                }
                else
                {
                    autopilotMenu.x = 0;
                    autopilotMenu.y = 0;
                }
            }
        }
        internal static bool autopilotPlayer = false;
        internal static bool DevLockToCam = false;
#if DEBUG
        private static Rect autopilotMenu = new Rect(0, 0, 160, 80);   // the "window"
#else
        private static Rect autopilotMenu = new Rect(0, 0, 160, 50);   // the "window"
#endif

        private static void GUIHandlerPlayerAutopilot(int ID)
        {
            if (GUI.Button(new Rect(10, 10, 140, 30), autopilotPlayer ? "<b>AUTOPILOT ON</b>" : "AUTOPILOT Off", autopilotPlayer ? AIGlobals.ButtonGreen : AIGlobals.ButtonBlue))
            {
                autopilotPlayer = !autopilotPlayer;
                if (Singleton.playerTank)
                {
                    var tankInst = Singleton.playerTank.GetComponent<AIECore.TankAIHelper>();
                    if (tankInst)
                    {
                        tankInst.ForceAllAIsToEscort();
                        tankInst.RecalibrateMovementAIController();
                    }
                }
            }
#if DEBUG
            if (GUI.Button(new Rect(10, 40, 140, 30), DevLockToCam ? "<b>LOCKED TO CAM</b>" : "Lock to Cam"))
            {
                DevLockToCam = !DevLockToCam;
            }
#endif
            GUI.DragWindow();
        }
    }
}
