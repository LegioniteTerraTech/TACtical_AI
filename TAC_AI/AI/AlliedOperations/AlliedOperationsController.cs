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
                    // We move to victory
                    this.helper.IsMultiTech = false;
                    this.helper.lastPlayer = this.helper.GetPlayerTech();
                    this.helper.foundGoal = false;
                    //this.helper.IsMultiTech = false;// Disabled so that on tech split it can be set automatically
                    BEscort.MotivateMove(this.helper, this.helper.tank);
                    BGeneral.AidDefend(this.helper, this.helper.tank);
                    break;

                case AIType.Assault:
                    // Up your arsenal
                    this.helper.IsMultiTech = false;
                    BAssassin.MotivateKill(this.helper, this.helper.tank);
                    //BAssassin.ShootToDestroy(this.helper, this.helper.tank);
                    BGeneral.AimDefend(this.helper, this.helper.tank);
                    break;

                case AIType.Aegis:
                    // I fight for my friends (priority resource techs pending)
                    this.helper.lastPlayer = this.helper.GetPlayerTech();
                    this.helper.foundGoal = false;
                    this.helper.IsMultiTech = false;
                    BAegis.MotivateProtect(this.helper, this.helper.tank);
                    BGeneral.AidDefend(this.helper, this.helper.tank);
                    break;

                case AIType.Prospector:
                    // We back in the mine
                    this.helper.IsMultiTech = false;
                    BProspector.MotivateMine(this.helper, this.helper.tank);
                    BGeneral.SelfDefend(this.helper, this.helper.tank);
                    break;

                case AIType.Scrapper:
                    // Grab Scrape and sell
                    this.helper.IsMultiTech = false;
                    this.helper.foundGoal = false;
                    //BScrapper.MotivateFind(this.helper, this.helper.tank);
                    BGeneral.SelfDefend(this.helper, this.helper.tank);
                    Debug.Log("TACtical_AI: AI NOT READY YET!");
                    break;

                case AIType.Energizer:
                    // The thing that keeps going
                    this.helper.IsMultiTech = false;
                    BEnergizer.MotivateCharge(this.helper, this.helper.tank);
                    BGeneral.SelfDefend(this.helper, this.helper.tank);
                    break;

                case AIType.MTTurret:
                    // Load, Aim,    FIIIIIRRRRRRRRRRRRRRRRRRRRRRRRRRRE!!!
                    this.helper.lastPlayer = this.helper.GetPlayerTech();
                    this.helper.IsMultiTech = true;
                    BGeneral.ResetValues(this.helper);
                    //EMultiTech.FollowTurretBelow(this.helper, this.helper.tank);
                    BMultiTech.BeamLockWithinBounds(this.helper, this.helper.tank); //lock rigidbody with closest non-MT Tech on build beam
                    BMultiTech.MimicDefend(this.helper, this.helper.tank);
                    break;

                case AIType.MTSlave:
                    // Defend and sit like good guard dog
                    this.helper.lastPlayer = this.helper.GetPlayerTech();
                    this.helper.IsMultiTech = true;
                    BGeneral.ResetValues(this.helper);
                    BMultiTech.BeamLockWithinBounds(this.helper, this.helper.tank); //lock rigidbody with closest non-MT Tech on build beam
                    BMultiTech.MimicDefend(this.helper, this.helper.tank);
                    break;

                case AIType.MTMimic:
                    // Copycat
                    this.helper.lastPlayer = this.helper.GetPlayerTech();
                    this.helper.IsMultiTech = true;
                    this.helper.Attempt3DNavi = true;
                    BGeneral.ResetValues(this.helper);
                    BMultiTech.MimicClosestAlly(this.helper, this.helper.tank);
                    break;

                case AIType.Astrotech:
                    // Grace from Space
                    this.helper.lastPlayer = this.helper.GetPlayerTech();
                    this.helper.IsMultiTech = false;
                    this.helper.Attempt3DNavi = true;
                    BAstrotech.MotivateSpace(this.helper, this.helper.tank);
                    BGeneral.AidDefend(this.helper, this.helper.tank);
                    break;

                case AIType.Buccaneer:
                    // Yarr
                    this.helper.lastPlayer = this.helper.GetPlayerTech();
                    this.helper.IsMultiTech = false;
                    this.helper.Attempt3DNavi = true;
                    BBuccaneer.MotivateBote(this.helper, this.helper.tank);
                    BGeneral.AidDefend(this.helper, this.helper.tank);
                    break;

                case AIType.Aviator:
                    // Fly and doggyfight
                    this.helper.lastPlayer = this.helper.GetPlayerTech();
                    this.helper.IsMultiTech = false;
                    this.helper.Attempt3DNavi = false;
                    BAviator.MotivateFly(this.helper, this.helper.tank);
                    BAviator.Dogfighting(this.helper, this.helper.tank);
                    break;

                default:
                    // It's one of the other showboat AIs(VEN(Air) or TAC(Navy) or Legion(Star)).  Not yet dammit!
                    Debug.Log("TACtical_AI: AI NOT READY YET! - Tougher Enemies doesn't even exist yet hold your horses!");
                    break;
            }
        }
    }
}
