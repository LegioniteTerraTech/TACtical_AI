using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TAC_AI.AI.Movement.AICores
{
    internal static class MultiTechUtils
    {
        /// <summary>
        /// THIS IS A DIRECTOR
        /// </summary>
        /// <param name="help"></param>
        /// <param name="tank"></param>
        internal static Vector3 HandleMultiTech(AIECore.TankAIHelper help, Tank tank)
        {
            if (help.DediAI == AIType.MTStatic && help.lastEnemy != null)
            {   // act like a trailer
                help.DriveDir = EDriveFacing.Neutral;
                help.Steer = false;
                help.lastDestination = tank.boundsCentreWorldNoCheck;
                help.MinimumRad = 0;
            }
            else if (help.DediAI == AIType.MTTurret && help.lastEnemy != null)
            {
                help.Steer = true;
                help.PivotOnly = true;
                help.lastDestination = help.lastEnemy.transform.position;
                help.MinimumRad = 0;
            }
            else if (help.DediAI == AIType.MTMimic && help.MTMimicHostAvail)
            {
                if (help.lastCloseAlly != null)
                {
                    try
                    {
                        help.lastDestination = AIEPathing.GetDriveApproxAirDirector(help.lastCloseAlly, help, out bool IsMoving);
                        if (IsMoving)//!(help.lastDestination - this.controller.Tank.boundsCentreWorld).Approximately(Vector3.zero, 0.75f)
                        {
                            //Debug.Log("TACtical_AI: MTMimic - AI " + this.controller.Tank.name + ": In range of " + help.lastCloseAlly.name + " and idle.");
                            help.MinimumRad = 0.1f;
                            if (Vector3.Dot(tank.rootBlockTrans.forward, (help.lastDestination - tank.boundsCentreWorldNoCheck).normalized) >= 0)
                            {
                                //Debug.Log("TACtical_AI:AI " + this.controller.Tank.name + ": Forwards");
                                help.Steer = true;
                                help.DriveDir = EDriveFacing.Forwards;
                                help.PivotOnly = false;
                                Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 5, help.lastDestination - tank.boundsCentreWorldNoCheck, new Color(1, 1, 0, 1));
                            }
                            else
                            {
                                help.Steer = true;
                                help.DriveDir = EDriveFacing.Backwards;
                                help.PivotOnly = false;
                                Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 5, help.lastDestination - tank.boundsCentreWorldNoCheck, new Color(1, 0, 1, 1));
                            }
                        }
                        else
                        {
                            //Debug.Log("TACtical_AI: MTMimic - AI " + this.controller.Tank.name + ": In range of " + help.lastCloseAlly.name + " and idle.");
                            help.MinimumRad = 0f;
                            help.lastDestination = tank.boundsCentreWorldNoCheck;
                            help.ForceSetDrive = true;
                            help.DriveVar = 0;
                            help.Steer = false;
                            help.PivotOnly = true;
                        }
                    }
                    catch { }
                }
                else
                {
                    //Debug.Log("TACtical_AI: MTMimic - AI " + this.controller.Tank.name + ": Out of range of any possible target");
                    help.MinimumRad = 0f;
                    help.lastDestination = tank.boundsCentreWorldNoCheck;
                    help.ForceSetDrive = false;
                    help.DriveVar = 0;
                    help.Steer = false;
                    help.PivotOnly = true;
                }
            }
            return help.lastDestination;
        }

    }
}
