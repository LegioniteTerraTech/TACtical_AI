using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TAC_AI.AI;
using TAC_AI.Templates;

namespace TAC_AI.World
{
    internal class SelectHalo : MonoBehaviour
    {
        public static GameObject SelectCirclePrefab;

        private AIECore.TankAIHelper tech;
        private ParticleSystem ps;
        private GameObject circleInst;
        private readonly float rescale = 5;

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
            tech = TechUnit;
            tech.tank.AttachEvent.Subscribe(OnSizeUpdate);
            tech.tank.DetachEvent.Subscribe(OnSizeUpdate);
            ps = circleInst.GetComponent<ParticleSystem>();
            var m = ps.main;
            m.startSize = AIECore.Extremes(TechUnit.tank.blockBounds.extents) * rescale;
            ps.Play(false);
            circleInst.SetActive(true);
        }
        public void OnSizeUpdate(TankBlock tb, Tank techCase)
        {
            try
            {
                if (techCase == tech.tank)
                {
                    var m = ps.main;
                    m.startSize = AIECore.Extremes(tech.tank.blockBounds.extents) * rescale;
                }
            }
            catch { }
        }
        public void Remove()
        {
            if (!(bool)circleInst)
                return;
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            circleInst.SetActive(false);
            /*
            tech = null;
            ps = null;
            circleInst = null;
            tech.tank.AttachEvent.Unsubscribe(OnSizeUpdate);
            tech.tank.DetachEvent.Unsubscribe(OnSizeUpdate);
            Destroy(ps);
            Destroy(circleInst);*/
        }
    }
    public class PlayerRTSControl : MonoBehaviour
    {
        public static PlayerRTSControl inst;
        public static int MaxCommandDistance = 9001;//500;
        public static int MaxAllowedSizeForHighlight = 7;
        public static bool PlayerIsInRTS = false;
        public static bool PlayerRTSOverlay = false;
        public static bool QueuedRelease = false;
        private static bool isBoxSelecting = false;
        private static Vector3 ScreenBoxStart = Vector3.zero;

        private float Offset = 10;

        private AIECore.TankAIHelper GrabbedThisFrame;
        public List<AIECore.TankAIHelper> LocalPlayerTechsControlled { get; private set; } = new List<AIECore.TankAIHelper>();

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
            if (!KickStart.AllowStrategicAI)
                return;
            inst = new GameObject("PlayerRTSControl").AddComponent<PlayerRTSControl>();
            Debug.Log("TACtical_AI: Created PlayerRTSControl.");
            //ManPointer.inst.MouseEvent.Subscribe(OnMouseEvent); - Only updates when in active game, not spectator
            Singleton.Manager<ManGameMode>.inst.ModeSwitchEvent.Subscribe(OnWorldReset);
            Singleton.Manager<CameraManager>.inst.CameraSwitchEvent.Subscribe(OnCameraChange);

        }
        public static void DelayedInitiate()
        {
            if (!KickStart.AllowStrategicAI)
                return;
            Debug.Log("TACtical_AI: Creating SelectCircle.");
            SelectHalo.SelectCirclePrefab = new GameObject("SelectCircle");
            SelectHalo.SelectCirclePrefab.AddComponent<SelectHalo>();
            Material[] mats = Resources.FindObjectsOfTypeAll<Material>();
            mats = mats.Where(cases => cases.name == "MAT_SFX_Explosion_01_Shockwave").ToArray();
            foreach (Material matcase in mats)
            {
                Debug.Log("TACtical_AI: Getting " + matcase.name + "...");
            }
            Material mat = mats.ElementAt(0);
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
            m.startLifetime = 3;
            m.maxParticles = 1;
            m.startSpeed = 0;
            m.startSize = 1;
            var e = ps.emission;
            e.rateOverTime = 10;
            var psr = SelectHalo.SelectCirclePrefab.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            psr.material = mat;
            psr.maxParticleSize = 3000;
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            SelectHalo.SelectCirclePrefab.SetActive(false);
            Debug.Log("TACtical_AI: Created SelectCircle.");
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
                    Debug.Log("TACtical AI: Player is in RTS view!");
                }
            }
            else
            {
                if (PlayerIsInRTS)
                {
                    PlayerIsInRTS = false;
                    RemovePlayerTech();
                }
            }
        }
        public static void ReleaseControl(AIECore.TankAIHelper TechUnit)
        {
            if ((bool)inst)
            {
                if (TechUnit != null)
                {
                    TechUnit.SetRTSState(false);
                    try
                    {
                        SetSelectHalo(TechUnit, false);
                    }
                    catch
                    {
                        Debug.Log("TACtical_AI: ERROR ON SETTING ReleaseControl");
                    }
                    inst.LocalPlayerTechsControlled.Remove(TechUnit);
                }
            }
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
                    {
                        //Debug.Log("TACtical_AI: FAILED TO HIT ANYTHING");
                        return;
                    }
                }
                else if (click == ManPointer.Event.RMB)
                {
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
                        if (layer == gInst.layerScenery)
                        {
                            QueuedRelease = false;
                            inst.HandleSelectTerrain(rayman);
                        }
                        else
                        {
                            QueuedRelease = false;
                            inst.HandleSelectTargetTank(rayman);
                        }
                    }
                }
            }
        }
        public void StartBoxSelectUnits()
        {
            ScreenBoxStart = Input.mousePosition;
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

            bool shift = Input.GetKey(KickStart.MultiSelect);
            if (!shift)
            {
                ClearList();
                if (GrabbedThisFrame.IsNotNull())
                {
                    LocalPlayerTechsControlled.Add(GrabbedThisFrame);
                    SetSelectHalo(GrabbedThisFrame, true);
                    GrabbedThisFrame.SetRTSState(true);
                }
            }
            foreach (Tank Tech in ManTechs.inst.CurrentTechs)
            {
                if (!(bool)Tech)
                    continue;
                AIECore.TankAIHelper TechUnit = Tech.GetComponent<AIECore.TankAIHelper>();
                if (TechUnit != null && GrabbedThisFrame != TechUnit)
                {
                    if (Tech.Team == Singleton.Manager<ManPlayer>.inst.PlayerTeam)
                    {
                        if (!(PlayerIsInRTS && Tech == Singleton.playerTank) && TechUnit.AIState != 1)
                            continue;
                        Vector3 camPos = Singleton.camera.WorldToScreenPoint(Tech.boundsCentreWorldNoCheck);
                        if (LowX <= camPos.x && camPos.x <= HighX && LowY <= camPos.y && camPos.y <= HighY)
                        {
                            Selects++;
                            if (KickStart.UseClassicRTSControls)
                            {
                                if (!LocalPlayerTechsControlled.Contains(TechUnit))
                                {
                                    LocalPlayerTechsControlled.Add(TechUnit);
                                    SetSelectHalo(TechUnit, true);
                                    TechUnit.SetRTSState(true);
                                }
                                else if (shift)
                                {
                                    LocalPlayerTechsControlled.Remove(TechUnit);
                                    SetSelectHalo(TechUnit, false);
                                    UnSelectUnitSFX();
                                }
                            }
                            else
                            {
                                if (!LocalPlayerTechsControlled.Contains(TechUnit))
                                {
                                    LocalPlayerTechsControlled.Add(TechUnit);
                                    SetSelectHalo(TechUnit, true);
                                    TechUnit.SetRTSState(true);
                                }
                                else if (!shift)
                                {
                                    LocalPlayerTechsControlled.Remove(TechUnit);
                                    SetSelectHalo(TechUnit, false);
                                    UnSelectUnitSFX();
                                }
                            }
                        }
                    }
                }
            }
            Debug.Log("TACtical_AI: GROUP Selected " + Selects);
            if (Selects > 0)
            {
                SelectUnitSFX();
            }
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
            if (LocalPlayerTechsControlled.Count > 0 && (Input.GetKey(KickStart.MultiSelect) || SavedGroups[groupNum].Count == 0))
            {
                PurgeAllNull();
                SavedGroups[groupNum].Clear();
                SavedGroups[groupNum].AddRange(LocalPlayerTechsControlled);
                Debug.Log("TACtical_AI: GROUP SAVED " + groupNum + ".");
                working = true;
            }
            else
            {
                ClearList();
                Debug.Log("TACtical_AI: GROUP SELECTED " + groupNum + ".");
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
                        if (!(PlayerIsInRTS && TechUnit.tank == Singleton.playerTank) && TechUnit.AIState != 1)
                            continue;

                        if (!LocalPlayerTechsControlled.Contains(TechUnit))
                        {
                            working = true;
                            LocalPlayerTechsControlled.Add(TechUnit);
                            SetSelectHalo(TechUnit, true);
                            TechUnit.SetRTSState(true);
                            Debug.Log("TACtical_AI: Selected Tank " + TechUnit.tank.name + ".");
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
                    if (!(PlayerIsInRTS && grabbedTech == Singleton.playerTank) && TechUnit.AIState != 1)
                        return;

                    if (KickStart.UseClassicRTSControls)
                    {
                        bool shift = Input.GetKey(KickStart.MultiSelect);
                        if (!LocalPlayerTechsControlled.Contains(TechUnit))
                        {
                            if (!shift)
                                ClearList();
                            LocalPlayerTechsControlled.Add(TechUnit);
                            GrabbedThisFrame = TechUnit;
                            SetSelectHalo(TechUnit, true);
                            TechUnit.SetRTSState(true);
                            //Debug.Log("TACtical_AI: Selected Tank " + grabbedTech.name + ".");
                            SelectUnitSFX();
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
                                    LocalPlayerTechsControlled.Add(TechUnit);
                                    GrabbedThisFrame = TechUnit;
                                    SetSelectHalo(TechUnit, true);
                                    TechUnit.SetRTSState(true);
                                    //Debug.Log("TACtical_AI: Selected Tank " + grabbedTech.name + ".");
                                    SelectUnitSFX();
                                }
                                else
                                {
                                    LocalPlayerTechsControlled.Remove(TechUnit);
                                    GrabbedThisFrame = TechUnit;
                                    SetSelectHalo(TechUnit, false);
                                    //Debug.Log("TACtical_AI: Unselected Tank " + grabbedTech.name + ".");
                                    UnSelectUnitSFX();
                                }
                                //Debug.Log("TACtical_AI: Selected Tank " + grabbedTech.name + ".");
                            }
                            QueuedRelease = !QueuedRelease;
                        }
                    }
                    else
                    {
                        bool shift = Input.GetKey(KickStart.MultiSelect);
                        if (LocalPlayerTechsControlled.Contains(TechUnit) && !shift)
                        {
                            LocalPlayerTechsControlled.Remove(TechUnit);
                            GrabbedThisFrame = TechUnit;
                            SetSelectHalo(TechUnit, false);
                            //Debug.Log("TACtical_AI: Unselected Tank " + grabbedTech.name + ".");
                            UnSelectUnitSFX();
                        }
                        else
                        {
                            if (shift)
                            {
                                GrabAllSameName(TechUnit);
                                return;
                            }
                            LocalPlayerTechsControlled.Add(TechUnit);
                            GrabbedThisFrame = TechUnit;
                            SetSelectHalo(TechUnit, true);
                            TechUnit.SetRTSState(true);
                            //Debug.Log("TACtical_AI: Selected Tank " + grabbedTech.name + ".");
                            SelectUnitSFX();
                        }
                    }
                }
            }
        }
        public void HandleSelectTargetTank(RaycastHit rayman)
        {
            Debug.Log("TACtical_AI: HandleSelectTargetTank.");
            PurgeAllNull();

            Tank grabbedTech = rayman.collider.transform.root.GetComponent<Tank>();
            if ((bool)grabbedTech)
            {
                if (grabbedTech.IsEnemy(Singleton.Manager<ManPlayer>.inst.PlayerTeam))
                {   // Attack Move
                    foreach (AIECore.TankAIHelper help in LocalPlayerTechsControlled)
                    {
                        if (help != null)
                        {
                            help.SetRTSState(true);
                            if (Input.GetKey(KickStart.MultiSelect))
                                help.RTSDestination = Vector3.zero;
                            if (ManNetwork.IsNetworked)
                                NetworkHandler.TryBroadcastRTSAttack(help.tank.netTech.netId.Value, grabbedTech.netTech.netId.Value);
                            help.lastEnemy = grabbedTech.visible;
                        }
                    }
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
                                help.RTSDestination = Vector3.zero;
                                if (!ManNetwork.IsNetworked)
                                    help.lastPlayer = grabbedTech.visible;
                                help.SetRTSState(false);
                            }
                        }
                    }
                    else
                    {   // Protect/Defend
                        try
                        {
                            foreach (AIECore.TankAIHelper help in LocalPlayerTechsControlled)
                            {
                                if (help != null)
                                {
                                    if (help.isAegisAvail)
                                    {
                                        help.RTSDestination = Vector3.zero;
                                        if (!ManNetwork.IsNetworked)
                                            help.LastCloseAlly = grabbedTech;
                                        SetOptionAuto(help, AIType.Aegis);
                                        help.SetRTSState(false);
                                    }
                                    else
                                    {
                                        help.RTSDestination = grabbedTech.boundsCentreWorldNoCheck;
                                        help.SetRTSState(true);
                                    }
                                }
                            }
                        }
                        catch
                        {
                            Debug.Log("TACtical_AI: Error on Protect/Defend - Techs");
                            foreach (AIECore.TankAIHelper help in LocalPlayerTechsControlled)
                            {
                                Debug.Log("TACtical_AI: " + help.name);
                            }
                        }
                    }
                    Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.AIFollow);
                }
            }
            else
            {
                try
                {
                    foreach (AIECore.TankAIHelper help in LocalPlayerTechsControlled)
                    {
                        if (help != null)
                        {
                            help.RTSDestination = rayman.point;
                            help.SetRTSState(true);
                        }
                    }
                }
                catch { }
            }
        }
        public void HandleSelectTerrain(RaycastHit rayman)
        {
            foreach (AIECore.TankAIHelper help in LocalPlayerTechsControlled)
            {
                if (help != null)
                {
                    help.RTSDestination = rayman.point;
                    help.SetRTSState(true);
                }
            }
            Debug.Log("TACtical_AI: HandleSelectTerrain.");
            if (LocalPlayerTechsControlled.Count > 0)
                Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.AcceptMission);
        }
        public void HandleSelectScenery(RaycastHit rayman)
        {
            Debug.Log("TACtical_AI: HandleSelectScenery.");

            ResourceDispenser node = rayman.collider.transform.root.GetComponent<ResourceDispenser>();
            if ((bool)node)
            {
                if (!node.GetComponent<Damageable>().Invulnerable)
                {   // Mine Move
                    foreach (AIECore.TankAIHelper help in LocalPlayerTechsControlled)
                    {
                        if (help != null)
                        {
                            if (help.isProspectorAvail)
                            {
                                help.RTSDestination = Vector3.zero;
                                if (!ManNetwork.IsNetworked)
                                {
                                    help.theResource = node.visible;
                                    help.areWeFull = false;
                                }
                                SetOptionAuto(help, AIType.Prospector);
                                help.SetRTSState(false);
                            }
                            else
                            {
                                help.RTSDestination = node.transform.position + (Vector3.up * 2);
                                help.SetRTSState(true);
                            }
                        }
                    }
                    Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.SendToInventory);
                }
                else
                {   // Just issue a movement command, it's a flattened rock or "landmark"
                    HandleSelectTerrain(rayman);
                }
            }
            else
            {
                try
                {
                    foreach (AIECore.TankAIHelper help in LocalPlayerTechsControlled)
                    {
                        if (help != null)
                        {
                            help.RTSDestination = rayman.point;
                            help.SetRTSState(true);
                        }
                    }
                }
                catch { }
            }
        }


        public static void SetSelectHalo(AIECore.TankAIHelper TechUnit, bool selectedHalo)
        {
            if (!(bool)TechUnit)
                return;
            if (AIECore.Extremes(TechUnit.tank.blockBounds.extents) <= MaxAllowedSizeForHighlight)
            {
                TechUnit.tank.visible.EnableOutlineGlow(selectedHalo, cakeslice.Outline.OutlineEnableReason.ScriptHighlight);

                var halo = TechUnit.gameObject.GetComponent<SelectHalo>();
                if ((bool)halo)
                    TechUnit.GetComponent<SelectHalo>().Remove();
            }
            else if (selectedHalo)
            {
                var halo = TechUnit.gameObject.GetComponent<SelectHalo>();
                if (!(bool)halo)
                    halo = TechUnit.gameObject.AddComponent<SelectHalo>();
                halo.Initiate(TechUnit);
            }
            else
            {
                try
                {
                    TechUnit.tank.visible.EnableOutlineGlow(false, cakeslice.Outline.OutlineEnableReason.ScriptHighlight);

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
            foreach (Tank tech in ManTechs.inst.CurrentTechs)
            {
                if (!(bool)tech)
                    continue;
                try
                {
                    if (tech.Team == ManPlayer.inst.PlayerTeam)
                    {
                        if (tech.name == techToFindNameOf.tank.name)
                        {
                            if (!PlayerIsInRTS && tech == Singleton.playerTank)
                            {
                                continue;
                            }
                            AIECore.TankAIHelper TechUnit = tech.GetComponent<AIECore.TankAIHelper>();
                            if (!(PlayerIsInRTS && tech == Singleton.playerTank) && TechUnit.AIState != 1)
                                continue;

                            if (!LocalPlayerTechsControlled.Contains(TechUnit))
                            {
                                working = true;
                                LocalPlayerTechsControlled.Add(TechUnit);
                                SetSelectHalo(TechUnit, true);
                                TechUnit.SetRTSState(true);
                                Debug.Log("TACtical_AI: Selected Tank " + tech.name + ".");
                            }
                        }
                    }
                }
                catch { }
            }
            if (working)
                SelectUnitSFX();
        }
        public void ExplodeUnitBolts()
        {
            foreach (AIECore.TankAIHelper help in LocalPlayerTechsControlled)
            {
                if (help != null)
                {
                    help.BoltsFired = true;
                    help.tank.control.DetonateExplosiveBolt();
                    help.PendingSystemsCheck = true;
                }
            }
            Debug.Log("TACtical_AI: HandleSelectTerrain.");
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
                visEnabled = visibleSelect;
            }
        }

        public void SetOptionAuto(AIECore.TankAIHelper lastTank, AIType dediAI)
        {
            if (ManNetwork.IsNetworked)
            {
                try
                {
                    NetworkHandler.TryBroadcastNewAIState(lastTank.tank.netTech.netId.Value, dediAI);
                    lastTank.OnSwitchAI();
                    lastTank.DediAI = dediAI;
                    lastTank.TestForFlyingAIRequirement();

                    TankDescriptionOverlay overlay = (TankDescriptionOverlay)GUIAIManager.bubble.GetValue(lastTank.tank);
                    overlay.Update();
                }
                catch (Exception e)
                {
                    Debug.Log("TACtical_AI: Error on sending AI Option change!!!\n" + e);
                }
            }
            else
            {
                lastTank.OnSwitchAI();
                lastTank.DediAI = dediAI;
                lastTank.TestForFlyingAIRequirement();

                TankDescriptionOverlay overlay = (TankDescriptionOverlay)GUIAIManager.bubble.GetValue(lastTank.tank);
                overlay.Update();
            }
        }
        
        public void ClearList()
        {
            foreach (AIECore.TankAIHelper TechUnit in LocalPlayerTechsControlled)
            {
                if (TechUnit != null)
                    SetSelectHalo(TechUnit, false);
            }
            inst.LocalPlayerTechsControlled.Clear();
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
                for (int step = 0;  step < numStep; )
                {
                    AIECore.TankAIHelper help = LocalPlayerTechsControlled.ElementAt(step);
                    if (help == null)
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


        private int LastClickFrameTimer = 0;
        public void Update()
        {
            if (!ManPauseGame.inst.IsPaused)
            {
                GrabbedThisFrame = null;
                bool isRTSState = PlayerIsInRTS || PlayerRTSOverlay;
                if (!PlayerIsInRTS && Input.GetKeyDown(KickStart.CommandHotkey))
                {
                    PlayerRTSOverlay = !PlayerRTSOverlay;
                }
                SetVisOfAll(isRTSState);
                if (isRTSState)
                {
                    if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
                        LastClickFrameTimer = 8;
                    if (LastClickFrameTimer > 0)
                    {
                        if (Input.GetMouseButtonUp(0))
                        {
                            OnRTSEvent(ManPointer.Event.LMB, true);
                        }
                        else if (Input.GetMouseButtonUp(1))
                        {
                            OnRTSEvent(ManPointer.Event.RMB, true);
                        }
                        LastClickFrameTimer--;
                    }
                    if (Input.GetMouseButtonDown(0) && !ManPointer.inst.DraggingItem)
                    {
                        isBoxSelecting = true;
                        StartBoxSelectUnits();
                    }
                    else if (isBoxSelecting && Input.GetMouseButtonUp(0))
                    {
                        isBoxSelecting = false;
                        if (!ManPointer.inst.DraggingItem)
                        {
                            HandleBoxSelectUnits();
                        }
                    }
                    HandleGroups();
                    if (Input.GetKeyDown(KickStart.CommandBoltsHotkey))
                    {
                        ExplodeUnitBolts();
                    }
                    foreach (AIECore.TankAIHelper help in LocalPlayerTechsControlled)
                    {
                        if (help != null)
                            DrawDirection(help.tank, help.RTSDestination);
                    }
                }
            }
        }


        public static void SelectUnitSFX()
        {
            Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.DropDown);
        }
        public static void UnSelectUnitSFX()
        {
            Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.Back);
        }

        private Color color = new Color(1f, 0.6f, 0.25f, 0.6f);//Color(0.25f, 1f, 0.25f, 0.75f);
        private void DrawDirection(Tank tech, Vector3 endPosGlobal)
        {
            GameObject gO = Instantiate(new GameObject("TechMovementLine"), Vector3.zero, Quaternion.identity);

            var lr = gO.GetComponent<LineRenderer>();
            if (!(bool)lr)
            {
                lr = gO.AddComponent<LineRenderer>();
                lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.positionCount = 2;
                lr.startWidth = 2.25f;
                lr.endWidth = 0.25f;
                lr.numCapVertices = 4;
            }
            lr.startColor = color;
            lr.endColor = color;
            Vector3 pos = tech.boundsCentreWorldNoCheck;
            Vector3 vertoffset = Offset * Vector3.up;
            Vector3[] vecs = new Vector3[2] { pos + vertoffset, endPosGlobal };
            lr.SetPositions(vecs);
            Destroy(gO, Time.deltaTime);
        }
    }
}
