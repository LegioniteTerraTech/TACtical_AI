using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TAC_AI.AI.AlliedOperations
{
    public class AlliedOperationsController
    {
        private AIECore.TankAIHelper helper;

        public AlliedOperationsController(AIECore.TankAIHelper helper)
        {
            this.helper = helper;
        }

        public void Execute()
        {
            switch (this.helper.DediAI)
            {
                case AIType.Escort:
                    switch (this.helper.DriverType)
                    {
                        case AIDriverType.Tank:
                            // We move to victory
                            BEscort.MotivateMove(this.helper, this.helper.tank);
                            BGeneral.AidDefend(this.helper, this.helper.tank);
                            break;

                        case AIDriverType.Astronaut:
                            // Grace from Space
                            BAstrotech.MotivateSpace(this.helper, this.helper.tank);
                            BGeneral.AidDefend(this.helper, this.helper.tank);
                            break;

                        case AIDriverType.Sailor:
                            // Yarr
                            BBuccaneer.MotivateBote(this.helper, this.helper.tank);
                            BGeneral.AidDefend(this.helper, this.helper.tank);
                            break;

                        case AIDriverType.Pilot:
                            // Fly and doggyfight
                            BAviator.MotivateFly(this.helper, this.helper.tank);
                            BAviator.Dogfighting(this.helper, this.helper.tank);
                            break;

                        case AIDriverType.Stationary:
                            // Fly and doggyfight
                            BBase.HoldPosition(this.helper, this.helper.tank);
                            BGeneral.AimDefend(this.helper, this.helper.tank);
                            break;

                        default:
                            DebugTAC_AI.Log("TACtical_AI: AIDriver is set to an invalid state - " + this.helper.DriverType);
                            DebugTAC_AI.Log("TACtical_AI: RESETTING TO DEFAULTS");
                            this.helper.DriverType = AIDriverType.Tank;
                            break;
                    }
                    break;
                case AIType.Assault:
                    // Up your arsenal
                    BAssassin.MotivateKill(this.helper, this.helper.tank);
                    BAssassin.ShootToDestroy(this.helper, this.helper.tank);
                    break;

                case AIType.Aegis:
                    // I fight for my friends (priority resource techs pending)
                    BAegis.MotivateProtect(this.helper, this.helper.tank);
                    BGeneral.AidDefend(this.helper, this.helper.tank);
                    break;

                case AIType.Prospector:
                    // We back in the mine
                    BProspector.MotivateMine(this.helper, this.helper.tank);
                    BGeneral.SelfDefend(this.helper, this.helper.tank);
                    break;

                case AIType.Scrapper:
                    // Grab Scrape and sell
                    BScrapper.MotivateFind(this.helper, this.helper.tank);
                    BGeneral.SelfDefend(this.helper, this.helper.tank);
                    break;

                case AIType.Energizer:
                    // The thing that keeps going
                    BEnergizer.MotivateCharge(this.helper, this.helper.tank);
                    BGeneral.SelfDefend(this.helper, this.helper.tank);
                    break;

                case AIType.MTTurret:
                    // Load, Aim,    FIIIIIRRRRRRRRRRRRRRRRRRRRRRRRRRRE!!!
                    BMultiTech.MTStatic(this.helper, this.helper.tank);
                    //EMultiTech.FollowTurretBelow(this.helper, this.helper.tank);
                    BMultiTech.BeamLockWithinBounds(this.helper, this.helper.tank); //lock rigidbody with closest non-MT Tech on build beam
                    BMultiTech.MimicDefend(this.helper, this.helper.tank);
                    break;

                case AIType.MTStatic:
                    // Defend and sit like good guard dog
                    BMultiTech.MTStatic(this.helper, this.helper.tank);
                    BMultiTech.BeamLockWithinBounds(this.helper, this.helper.tank); //lock rigidbody with closest non-MT Tech on build beam
                    BMultiTech.MimicDefend(this.helper, this.helper.tank);
                    break;

                case AIType.MTMimic:
                    // Copycat
                    BMultiTech.MimicAllClosestAlly(this.helper, this.helper.tank);
                    break;

                default:
                    DebugTAC_AI.Log("TACtical_AI: AIType is set to an invalid state - " + this.helper.DediAI);
                    DebugTAC_AI.Log("TACtical_AI: RESETTING TO DEFAULTS");
                    this.helper.DediAI = AIType.Escort;
                    break;
            }
        }

    }
}
