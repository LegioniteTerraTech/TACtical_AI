using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TerraTechETCUtil;
using System.Runtime.Remoting.Messaging;

namespace TAC_AI.AI.Movement.AICores
{
    internal static class MultiTechUtils
    {
        /// <summary>
        /// THIS IS A DIRECTOR
        /// </summary>
        /// <param name="help"></param>
        /// <param name="tank"></param>
        internal static Vector3 HandleMultiTech(TankAIHelper help, Tank tank, ref EControlCoreSet core)
        {
            Vector3 targPos;
            if (help.lastEnemyGet)
                help.UpdateEnemyDistance(help.lastEnemyGet.tank.boundsCentreWorldNoCheck);
            else
                help.IgnoreEnemyDistance();
            core.DriveDest = EDriveDest.None;
            if (help.DediAI == AIType.MTTurret && help.lastEnemyGet)
            {
                core.DriveToFacingTowards();
                help.ThrottleState = AIThrottleState.PivotOnly;
                targPos = help.lastEnemyGet.tank.boundsCentreWorldNoCheck;
                help.AutoSpacing = 0;
            }
            else if (help.DediAI == AIType.MTMimic && help.MTMimicHostAvail)
            {
                if (help.theResource != null && help.theResource.tank)
                {
                    targPos = AIEPathing.GetDriveApproxAirDirector(help.theResource.tank, help, out bool IsMoving);
                    if (IsMoving)//!(help.lastDestination - this.controller.Tank.boundsCentreWorld).Approximately(Vector3.zero, 0.75f)
                    {
                        //DebugTAC_AI.Log(KickStart.ModID + ": MTMimic - AI " + this.controller.Tank.name + ": In range of " + help.lastCloseAlly.name + " and idle.");
                        help.AutoSpacing = 0;//0.1f;
                        core.TurningStrictness = ESteeringStrength.MaxSteering;
                        if (Vector3.Dot(tank.rootBlockTrans.forward, (targPos - tank.boundsCentreWorldNoCheck).normalized) >= 0)
                        {
                            //DebugTAC_AI.Log(KickStart.ModID + ":AI " + this.controller.Tank.name + ": Forwards");
                            core.DriveToFacingTowards();
                            help.ThrottleState = AIThrottleState.FullSpeed;
                            //help.ThrottleState = AIThrottleState.ForceSpeed;
                            //help.DriveVar = 1;
                            if (Templates.DebugRawTechSpawner.ShowDebugFeedBack)
                                DebugExtUtilities.DrawDirIndicator(tank.gameObject, 5, targPos - tank.boundsCentreWorldNoCheck, new Color(1, 1, 0, 1));
                        }
                        else
                        {
                            core.DriveToFacingBackwards();
                            help.ThrottleState = AIThrottleState.FullSpeed;
                            //help.ThrottleState = AIThrottleState.ForceSpeed;
                            //help.DriveVar = -1;
                            if (Templates.DebugRawTechSpawner.ShowDebugFeedBack)
                                DebugExtUtilities.DrawDirIndicator(tank.gameObject, 5, targPos - tank.boundsCentreWorldNoCheck, new Color(1, 0, 1, 1));
                        }
                    }
                    else
                    {
                        //DebugTAC_AI.Log(KickStart.ModID + ": MTMimic - AI " + this.controller.Tank.name + ": In range of " + help.lastCloseAlly.name + " and idle.");
                        help.AutoSpacing = 0f;
                        targPos = tank.boundsCentreWorldNoCheck;
                        /*
                        help.ThrottleState = AIThrottleState.ForceSpeed;
                        help.DriveVar = 0;
                        // */
                        core.Stop();
                        help.ThrottleState = AIThrottleState.PivotOnly;
                    }
                }
                else
                {
                    //DebugTAC_AI.Log(KickStart.ModID + ": MTMimic - AI " + this.controller.Tank.name + ": Out of range of any possible target");
                    help.AutoSpacing = 0f;
                    targPos = tank.boundsCentreWorldNoCheck;
                    core.Stop();
                    help.ThrottleState = AIThrottleState.PivotOnly;
                }
            }
            else
            {   // act like a trailer
                core.DriveDir = EDriveFacing.Neutral;
                targPos = tank.boundsCentreWorldNoCheck;
                help.AutoSpacing = 0;
            }
            return targPos;
        }

    }
}
