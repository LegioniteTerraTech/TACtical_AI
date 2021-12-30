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

        private float Offset = 10;

        private List<AIECore.TankAIHelper> LocalPlayerTechsControlled = new List<AIECore.TankAIHelper>();
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
                inst.LocalPlayerTechsControlled.Clear();
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
            if ((PlayerRTSOverlay || PlayerIsInRTS) && down)
            {
                int layerMask = Globals.inst.layerTank.mask | Globals.inst.layerTankIgnoreTerrain.mask | Globals.inst.layerTerrain.mask;

                if (click == ManPointer.Event.LMB)
                {
                    //Debug.Log("TACtical_AI: LEFT MOUSE BUTTON");

                    Vector3 pos = Camera.main.transform.position;
                    Vector3 posD = Singleton.camera.ScreenPointToRay(Input.mousePosition).direction.normalized;
                    
                    RaycastHit rayman;

                    Physics.Raycast(new Ray(pos, posD), out rayman, MaxCommandDistance, layerMask);


                    if ((bool)rayman.collider)
                    {
                        if (KickStart.UseClassicRTSControls)
                        {
                            if (rayman.collider.gameObject.layer == Globals.inst.layerTerrain)
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
                            if (rayman.collider.gameObject.layer == Globals.inst.layerTerrain)
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
                        if (rayman.collider.gameObject.layer == Globals.inst.layerTerrain)
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

                        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                        if (!LocalPlayerTechsControlled.Contains(TechUnit))
                        {
                            if (!shift)
                                ClearList();
                            LocalPlayerTechsControlled.Add(TechUnit);
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
                                    ClearList();
                                LocalPlayerTechsControlled.Add(TechUnit);
                                SetSelectHalo(TechUnit, true);
                                TechUnit.SetRTSState(true);
                                //Debug.Log("TACtical_AI: Selected Tank " + grabbedTech.name + ".");
                            }
                            QueuedRelease = !QueuedRelease;
                        }
                    }
                    else
                    {
                        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                        if (LocalPlayerTechsControlled.Contains(TechUnit) && !shift)
                        {
                            LocalPlayerTechsControlled.Remove(TechUnit);
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

            Tank grabbedTech = rayman.collider.transform.root.GetComponent<Tank>();
            if ((bool)grabbedTech)
            {
                if (grabbedTech.IsEnemy(Singleton.Manager<ManPlayer>.inst.PlayerTeam))
                {   // Attack Move
                    foreach (AIECore.TankAIHelper help in LocalPlayerTechsControlled)
                    {
                        if (help != null)
                        {
                            help.RTSDestination = Vector3.zero;
                            help.lastEnemy = grabbedTech.visible;
                        }
                    }
                    Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.LockOn);
                }
                else
                {
                    if (grabbedTech.IsPlayer)
                    {   // Reset to working order
                        foreach (AIECore.TankAIHelper help in LocalPlayerTechsControlled)
                        {
                            if (help != null)
                            {
                                help.RTSDestination = Vector3.zero;
                                help.lastPlayer = grabbedTech.visible;
                                help.SetRTSState(false);
                            }
                        }
                    }
                    else
                    {   // Protect/Defend
                        foreach (AIECore.TankAIHelper help in LocalPlayerTechsControlled)
                        {
                            if (help != null)
                            {
                                if (help.isAegisAvail)
                                {
                                    help.RTSDestination = Vector3.zero;
                                    help.LastCloseAlly = grabbedTech;
                                    SetOptionAuto(help, AIType.Aegis);
                                    help.SetRTSState(false);
                                }
                                else
                                {
                                    help.RTSDestination = grabbedTech.boundsCentreWorldNoCheck;
                                }
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
        public void SetOptionAuto(AIECore.TankAIHelper lastTank, AIType dediAI)
        {
            if (ManNetwork.IsNetworked)
            {
                try
                {
                    NetworkHandler.TryBroadcastNewAIState(lastTank.tank.netTech.netId.Value, dediAI);
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
        public void HandleSelectTerrain(RaycastHit rayman)
        {
            foreach (AIECore.TankAIHelper help in LocalPlayerTechsControlled)
            {
                if (help != null)
                {
                    help.RTSDestination = rayman.point;
                }
            }
            Debug.Log("TACtical_AI: HandleSelectTerrain.");
            if (LocalPlayerTechsControlled.Count > 0)
                Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.AcceptMission);
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
                if (!PlayerIsInRTS && Input.GetKeyDown(KickStart.CommandHotkey))
                {
                    PlayerRTSOverlay = !PlayerRTSOverlay;
                }
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
                if (PlayerRTSOverlay || PlayerIsInRTS)
                {
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
