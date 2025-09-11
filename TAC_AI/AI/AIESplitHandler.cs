using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TAC_AI.AI.Enemy;
using TAC_AI.Templates;
using TerraTechETCUtil;


namespace TAC_AI.AI
{
    public class AIESplitHandler : MonoBehaviour
    {
        private static StringBuilder SB => RLoadedBases.SB;

        private static Dictionary<Tank, HashSet<Tank>> cachedTechsByParent = new Dictionary<Tank, HashSet<Tank>>();
        private static List<Tank> cachedTechs = new List<Tank>();
        internal static void BaseSplitPriorityHandler(Tank speculativePart)
        {
            InvokeHelper.InvokeSingle(BaseSplitPriorityCheck, 0.125f);
        }
        private static void BaseSplitPriorityCheck()
        {
            try
            {
                foreach (var parentAndChildren in cachedTechsByParent)
                {
                    Tank parent = parentAndChildren.Key;
                    if (parent == null)
                        continue;
                    try
                    {
                        int blockCount = 0;
                        int anchorCount = 0;
                        Tank largest = null;
                        Tank anchored = null;
                        string NameMain = parentAndChildren.Key.name;
                        if (blockCount < parent.blockman.blockCount)
                        {
                            blockCount = parent.blockman.blockCount;
                            largest = parent;
                        }
                        if (anchorCount < parent.Anchors.NumPossibleAnchors)
                        {
                            anchorCount = parent.Anchors.NumPossibleAnchors;
                            anchored = parent;
                        }
                        cachedTechs.Add(parent);
                        foreach (var item in parentAndChildren.Value)
                        {
                            if (item == null)
                                continue;
                            if (blockCount < item.blockman.blockCount)
                            {
                                blockCount = item.blockman.blockCount;
                                largest = item;
                            }
                            if (anchorCount < item.Anchors.NumPossibleAnchors)
                            {
                                anchorCount = item.Anchors.NumPossibleAnchors;
                                anchored = item;
                            }
                            cachedTechs.Add(item);
                        }
                        if (anchored == null)
                            anchored = largest;
                        if (anchored)
                        {
                            foreach (var tank in cachedTechs)
                            {
                                if (anchored == tank)
                                {
                                    tank.SetName(NameMain);
                                }
                                else
                                {
                                    //It's likely not a base
                                    if (tank.IsAnchored && !tank.blockman.IterateBlockComponents<ModuleTechController>().FirstOrDefault())
                                    {   // It's a fragment of the base - recycle it to prevent unwanted mess from getting in the way
                                        RLoadedBases.RecycleTechToTeam(tank);
                                        continue;
                                    }

                                    char lastIn = 'n';
                                    foreach (char ch in NameMain)
                                    {
                                        if (ch == '¥' && lastIn == '¥')
                                        {
                                            SB.Remove(SB.Length - 2, 2);
                                            break;
                                        }
                                        else
                                            SB.Append(ch);
                                        lastIn = ch;
                                    }
                                    SB.Append(" Minion");
                                    string nameNew = SB.ToString();
                                    SB.Clear();
                                    var helper = tank.GetHelperInsured();
                                    if (helper)
                                    {
                                        helper.ResetOnSwitchAlignments(tank);
                                        helper.GenerateEnemyAI(tank);
                                    }

                                    var mind = tank.GetComponent<EnemyMind>();
                                    if (mind)
                                    {
                                        // it's a minion of the base
                                        if (mind.CommanderAttack == EAttackMode.Safety)
                                            mind.CommanderAttack = EAttackMode.Chase;
                                    }

                                    // Charge the new Tech and send it on it's way!
                                    RawTechLoader.ChargeAndClean(tank);
                                    tank.visible.Teleport(tank.boundsCentreWorldNoCheck + (tank.rootBlockTrans.forward *
                                        tank.blockBounds.size.magnitude), tank.rootBlockTrans.rotation, false, false);
                                    if (!ManVisible.inst.AllTrackedVisibles.Any(delegate (TrackedVisible cand)
                                    { return cand.visible == tank.visible; }))
                                    {
                                        DebugTAC_AI.Assert(true, KickStart.ModID + ": ASSERT - " + tank.name +
                                            " was not properly inserted into the TrackedVisibles list and will not function (and network) properly!");
                                        RawTechLoader.InsureTrackingTank(tank, false, false);
                                    }

                                    TrackedVisible TV = ManVisible.inst.AllTrackedVisibles.FirstOrDefault(delegate (TrackedVisible cand)
                                    { return cand.visible == tank.visible; });
                                    tank.SetName(nameNew);
                                    ManTechs.inst.TankNameChangedEvent.Send(tank, TV);
                                }
                            }
                        }
                    }
                    finally
                    {
                        cachedTechs.Clear();
                    }
                }
            }
            finally
            {
                cachedTechsByParent.Clear();
            }
        }


        private Tank tank;
        private Tank mother;
        private bool initDelay = false;

        public void Setup(Tank ThisTank, Tank Mother)
        {
            tank = ThisTank;
            mother = Mother;
            HashSet<Tank> hashS;
            if (cachedTechsByParent.TryGetValue(mother, out hashS))
            {
                hashS.Add(ThisTank);
            }
            else
            {
                hashS = new HashSet<Tank>();
                hashS.Add(ThisTank);
                cachedTechsByParent.Add(Mother, hashS);
            }
        }

        public void Update()
        {
            if (!initDelay)
            {
                initDelay = true;
                return;
            }
            try
            {
                BlockManager BM = tank.blockman;
                TankAIHelper helper = tank.GetHelperInsured();
                helper.SetAIControl(AITreeType.AITypes.Escort);
                if (BM.blockCount > 0)
                {
                    if (BM.IterateBlockComponents<ModuleWheels>().Count() > 0 || BM.IterateBlockComponents<ModuleHover>().Count() > 0)
                        helper.DediAI = AIType.Escort;
                    else
                    {
                        if (BM.IterateBlockComponents<ModuleWeapon>().Count() > 0)
                            helper.DediAI = AIType.MTTurret;
                        else
                            helper.DediAI = AIType.MTStatic;
                    }
                }
                else
                {   // We assume flares or a drone/infantry to launch
                    helper.DediAI = AIType.Escort;
                }
                helper.SetDriverType(AIECore.HandlingDetermine(tank, helper));
                helper.lastCloseAlly = mother;
                DebugTAC_AI.Log(KickStart.ModID + ": AIESplitHandler - Set to " + helper.DediAI + " for " + tank.name);
            }
            catch
            {
                DebugTAC_AI.Log(KickStart.ModID + ": AIESplitHandler - CRITICAL ERROR ON UPDATE");
            }
            Destroy(this);
        }
    }
}
