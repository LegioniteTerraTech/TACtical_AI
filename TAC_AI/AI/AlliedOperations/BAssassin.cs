using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TAC_AI.AI.AlliedOperations
{
    public static class BAssassin
    {
        public static void MotivateKill(AIECore.TankAIHelper thisInst, Tank tank)
        {
            //The Handler that tells the Tank (Assassin) what to do movement-wise
            thisInst.IsMultiTech = false;
            thisInst.Attempt3DNavi = (thisInst.DriverType == AIDriverType.Pilot || thisInst.DriverType == AIDriverType.Astronaut);

            float dist = (tank.boundsCentreWorldNoCheck - thisInst.lastDestination).magnitude;
            bool hasMessaged = false;
            thisInst.lastRange = dist;
            thisInst.AvoidStuff = true;

            BGeneral.ResetValues(thisInst);


            EnergyRegulator.EnergyState state = tank.EnergyRegulator.Energy(EnergyRegulator.EnergyType.Electric);
            if (thisInst.CollectedTarget)
            {
                if ((state.storageTotal - state.spareCapacity) / state.storageTotal < 0.4f)
                {
                    //Debug.Log("TACtical_AI: AI " + tank.name + ": Falling back to base! Charge " + (state.storageTotal - state.spareCapacity).ToString());
                    thisInst.CollectedTarget = false;
                }
            }
            else
            {
                if ((state.storageTotal - state.spareCapacity) / state.storageTotal > 0.95f)
                {
                    //Debug.Log("TACtical_AI: AI " + tank.name + ": Charged up and ready to attack!");
                    thisInst.CollectedTarget = true;
                    thisInst.ActionPause = AIGlobals.ReverseDelay;
                }
            }

            if (!thisInst.CollectedTarget || thisInst.Retreat)
            {
                thisInst.foundBase = AIECore.FetchChargedChargers(tank, tank.Radar.Range * 2.5f, out thisInst.lastBasePos, out thisInst.theBase, tank.Team);
                if (!thisInst.foundBase)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Searching for nearest charger!");
                    thisInst.EstTopSped = 1;//slow down the clock to reduce lagg
                    if (thisInst.theBase == null)
                        return; // There's no base!
                    thisInst.lastBaseExtremes = thisInst.theBase.GetCheapBounds();
                }
                thisInst.ForceSetDrive = true;
                thisInst.DriveVar = 1;

                if (dist < thisInst.lastBaseExtremes + thisInst.lastTechExtents + 3)
                {
                    thisInst.theBase.GetComponent<AIECore.TankAIHelper>().AllowApproach();
                    if (thisInst.recentSpeed == 1)
                    {
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Trying to unjam...");
                        thisInst.AvoidStuff = false;
                        thisInst.DriveVar = -1;
                        //thisInst.TryHandleObstruction(hasMessaged, dist, false, false);
                    }
                    else
                    {
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Arrived at nearest charger and recharging!");
                        thisInst.AvoidStuff = false;
                        thisInst.ActionPause -= KickStart.AIClockPeriod / 5;
                        thisInst.Yield = true;
                        thisInst.SettleDown();
                    }
                }
                else if (dist < thisInst.lastBaseExtremes + thisInst.lastTechExtents + 8)
                {
                    thisInst.theBase.GetComponent<AIECore.TankAIHelper>().AllowApproach();
                    if (thisInst.recentSpeed < 3)
                    {
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Trying to unjam...");
                        thisInst.AvoidStuff = false;
                        thisInst.TryHandleObstruction(hasMessaged, dist, false, false);
                    }
                    else if (thisInst.recentSpeed < 8)
                    {
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Rattling off resources...");
                        thisInst.AvoidStuff = false;
                        thisInst.Yield = true;
                    }
                    else
                    {
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Yielding base approach...");
                        thisInst.AvoidStuff = false;
                        thisInst.Yield = true;
                        thisInst.SettleDown();
                    }
                }
                else if (dist < thisInst.lastBaseExtremes + thisInst.lastTechExtents + 12)
                {
                    thisInst.theBase.GetComponent<AIECore.TankAIHelper>().AllowApproach();
                    if (thisInst.recentSpeed < 3)
                    {
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  unjamming from base...");
                        thisInst.TryHandleObstruction(hasMessaged, dist, false, true);
                    }
                    else
                    {
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Arrived at base!");
                        thisInst.ActionPause -= KickStart.AIClockPeriod / 5;
                        //thisInst.Yield = true;
                        thisInst.SettleDown();
                    }
                }
                else if (thisInst.recentSpeed < 3)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Removing obstruction on way to base...");
                    thisInst.TryHandleObstruction(hasMessaged, dist, false, true);
                }
                AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Heading back to base!");
                thisInst.ProceedToBase = true;
                thisInst.foundGoal = false;
            }
            else if (thisInst.ActionPause > 0)
            {
                AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Reversing from base...");
                thisInst.ForceSetDrive = true;
                thisInst.DriveVar = -1;
            }
            else
            {
                if (!thisInst.foundGoal)
                {
                    thisInst.EstTopSped = 1;//slow down the clock to reduce lagg
                    thisInst.foundGoal = AIECore.FindTarget(tank, thisInst, thisInst.theResource, out thisInst.theResource);
                    AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Scanning for enemies...");
                    if (!thisInst.foundGoal)
                    {
                        thisInst.foundBase = AIECore.FetchChargedChargers(tank, tank.Radar.Range * 2.5f, out thisInst.lastBasePos, out thisInst.theBase, tank.Team);
                        if (thisInst.theBase == null)
                            return; // There's no base!
                        thisInst.lastBaseExtremes = thisInst.theBase.GetCheapBounds();
                    }
                    return; // There's no enemies left!
                }
                else if (!thisInst.theResource?.tank?.visible || !thisInst.theResource.tank.visible.isActive || !thisInst.theResource.tank.IsEnemy(tank.Team))
                {
                    thisInst.foundGoal = false;
                    thisInst.CollectedTarget = false;
                    thisInst.theResource = null;
                    thisInst.SettleDown();
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Target destroyed or disbanded.");
                    return; // Enemy destroyed
                }
                thisInst.lastDestination = thisInst.theResource.tank.boundsCentreWorldNoCheck;

                if (dist < thisInst.lastTechExtents + thisInst.MinimumRad)
                {
                    if (!thisInst.FullMelee)
                        thisInst.PivotOnly = true;
                    thisInst.SettleDown();
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Close to enemy at " + thisInst.theResource.centrePosition);
                }
                else if (dist < thisInst.lastTechExtents + thisInst.RangeToChase)
                {
                    thisInst.AutoHandleObstruction();
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Engadging the enemy at " + thisInst.theResource.centrePosition);
                }
                else if (thisInst.recentSpeed < 2)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Removing obstruction at " + tank.transform.position);
                    thisInst.TryHandleObstruction(hasMessaged, dist, false, true);
                }
                /*
                else if (dist < thisInst.lastTechExtents + 12)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  In combat at " + thisInst.theResource.centrePosition);
                    thisInst.SettleDown();
                }*/
                AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Moving out to fight at " + thisInst.theResource.centrePosition + " |Tech is at " + tank.boundsCentreWorldNoCheck);
                thisInst.ProceedToMine = true;
                thisInst.foundBase = false;
            }
        }


        public static void ShootToDestroy(AIECore.TankAIHelper thisInst, Tank tank)
        {
            // Determines the weapons actions and aiming of the AI, this one is more fire-precise and used for turrets
            thisInst.DANGER = false;
            //thisInst.lastEnemy = tank.Vision.GetFirstVisibleTechIsEnemy(tank.Team);

            if (thisInst.theResource)
            {
                thisInst.lastEnemy = thisInst.theResource;
            }

            if (thisInst.lastEnemy != null)
            {
                Vector3 aimTo = (thisInst.lastEnemy.transform.position - tank.transform.position).normalized;
                thisInst.WeaponDelayClock++;
                if (thisInst.SideToThreat)
                {
                    if (Mathf.Abs((tank.rootBlockTrans.right - aimTo).magnitude) < 0.15f || Mathf.Abs((tank.rootBlockTrans.right - aimTo).magnitude) > -0.15f || thisInst.WeaponDelayClock >= 30)
                    {
                        thisInst.DANGER = true;
                        thisInst.WeaponDelayClock = 30;
                    }
                }
                else
                {
                    if (Mathf.Abs((tank.rootBlockTrans.forward - aimTo).magnitude) < 0.15f || thisInst.WeaponDelayClock >= 30)
                    {
                        thisInst.DANGER = true;
                        thisInst.WeaponDelayClock = 30;
                    }
                }
            }
            else
            {
                thisInst.WeaponDelayClock = 0;
                thisInst.DANGER = false;
            }
        }
    }
}
