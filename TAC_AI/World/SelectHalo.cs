using System;
using System.Collections.Generic;
using System.Linq;
using TAC_AI.AI;
using UnityEngine;

namespace TAC_AI.World
{
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
}
