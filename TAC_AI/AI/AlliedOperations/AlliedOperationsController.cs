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
                            this.helper.IsMultiTech = false;
                            this.helper.lastPlayer = this.helper.GetPlayerTech();
                            this.helper.foundGoal = false;
                            this.helper.Attempt3DNavi = false;
                            //this.helper.IsMultiTech = false;// Disabled so that on tech split it can be set automatically
                            BEscort.MotivateMove(this.helper, this.helper.tank);
                            BGeneral.AidDefend(this.helper, this.helper.tank);
                            break;

                        case AIDriverType.Astronaut:
                            // Grace from Space
                            this.helper.lastPlayer = this.helper.GetPlayerTech();
                            this.helper.IsMultiTech = false;
                            this.helper.Attempt3DNavi = true;
                            BAstrotech.MotivateSpace(this.helper, this.helper.tank);
                            BGeneral.AidDefend(this.helper, this.helper.tank);
                            break;

                        case AIDriverType.Sailor:
                            // Yarr
                            this.helper.lastPlayer = this.helper.GetPlayerTech();
                            this.helper.IsMultiTech = false;
                            this.helper.Attempt3DNavi = true;
                            BBuccaneer.MotivateBote(this.helper, this.helper.tank);
                            BGeneral.AidDefend(this.helper, this.helper.tank);
                            break;

                        case AIDriverType.Pilot:
                            // Fly and doggyfight
                            this.helper.lastPlayer = this.helper.GetPlayerTech();
                            this.helper.IsMultiTech = false;
                            BAviator.MotivateFly(this.helper, this.helper.tank);
                            BAviator.Dogfighting(this.helper, this.helper.tank);
                            break;

                        default:
                            Debug.Log("TACtical_AI: AIDriver is set to an invalid state - " + this.helper.DriverType);
                            Debug.Log("TACtical_AI: RESETTING TO DEFAULTS");
                            this.helper.DriverType = AIDriverType.Tank;
                            break;
                    }
                    break;
                case AIType.Assault:
                    // Up your arsenal
                    this.helper.IsMultiTech = false; 
                    this.helper.Attempt3DNavi = (this.helper.DriverType == AIDriverType.Pilot || this.helper.DriverType == AIDriverType.Astronaut);
                    BAssassin.MotivateKill(this.helper, this.helper.tank);
                    BAssassin.ShootToDestroy(this.helper, this.helper.tank);
                    break;

                case AIType.Aegis:
                    // I fight for my friends (priority resource techs pending)
                    this.helper.lastPlayer = this.helper.GetPlayerTech();
                    this.helper.foundGoal = false;
                    this.helper.IsMultiTech = false;
                    this.helper.Attempt3DNavi = (this.helper.DriverType == AIDriverType.Pilot || this.helper.DriverType == AIDriverType.Astronaut);
                    BAegis.MotivateProtect(this.helper, this.helper.tank);
                    BGeneral.AidDefend(this.helper, this.helper.tank);
                    break;

                case AIType.Prospector:
                    // We back in the mine
                    this.helper.IsMultiTech = false;
                    this.helper.Attempt3DNavi = (this.helper.DriverType == AIDriverType.Pilot || this.helper.DriverType == AIDriverType.Astronaut);
                    BProspector.MotivateMine(this.helper, this.helper.tank);
                    BGeneral.SelfDefend(this.helper, this.helper.tank);
                    break;

                case AIType.Scrapper:
                    // Grab Scrape and sell
                    this.helper.IsMultiTech = false;
                    this.helper.foundGoal = false;
                    this.helper.Attempt3DNavi = (this.helper.DriverType == AIDriverType.Pilot || this.helper.DriverType == AIDriverType.Astronaut);
                    BScrapper.MotivateFind(this.helper, this.helper.tank);
                    BGeneral.SelfDefend(this.helper, this.helper.tank);
                    Debug.Log("TACtical_AI: AI NOT READY YET! - " + this.helper.DediAI.ToString());
                    break;

                case AIType.Energizer:
                    // The thing that keeps going
                    this.helper.IsMultiTech = false;
                    this.helper.Attempt3DNavi = (this.helper.DriverType == AIDriverType.Pilot || this.helper.DriverType == AIDriverType.Astronaut);
                    BEnergizer.MotivateCharge(this.helper, this.helper.tank);
                    BGeneral.SelfDefend(this.helper, this.helper.tank);
                    break;

                case AIType.MTTurret:
                    // Load, Aim,    FIIIIIRRRRRRRRRRRRRRRRRRRRRRRRRRRE!!!
                    this.helper.lastPlayer = this.helper.GetPlayerTech();
                    this.helper.IsMultiTech = true;
                    BMultiTech.MTStatic(this.helper, this.helper.tank);
                    //EMultiTech.FollowTurretBelow(this.helper, this.helper.tank);
                    BMultiTech.BeamLockWithinBounds(this.helper, this.helper.tank); //lock rigidbody with closest non-MT Tech on build beam
                    BMultiTech.MimicDefend(this.helper, this.helper.tank);
                    break;

                case AIType.MTSlave:
                    // Defend and sit like good guard dog
                    this.helper.lastPlayer = this.helper.GetPlayerTech();
                    this.helper.IsMultiTech = true;
                    BMultiTech.MTStatic(this.helper, this.helper.tank);
                    BMultiTech.BeamLockWithinBounds(this.helper, this.helper.tank); //lock rigidbody with closest non-MT Tech on build beam
                    BMultiTech.MimicDefend(this.helper, this.helper.tank);
                    break;

                case AIType.MTMimic:
                    // Copycat
                    this.helper.lastPlayer = this.helper.GetPlayerTech();
                    this.helper.IsMultiTech = true;
                    this.helper.Attempt3DNavi = true;
                    BMultiTech.MimicAllClosestAlly(this.helper, this.helper.tank);
                    break;

                default:
                    Debug.Log("TACtical_AI: AIType is set to an invalid state - " + this.helper.DediAI);
                    Debug.Log("TACtical_AI: RESETTING TO DEFAULTS");
                    this.helper.DediAI = AIType.Escort;
                    break;
            }
        }

    }
}
