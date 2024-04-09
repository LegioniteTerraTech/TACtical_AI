using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TAC_AI.AI.Movement;
using TAC_AI.AI.Enemy;
using TAC_AI.AI.AlliedOperations;
using TAC_AI.Templates;


namespace TAC_AI.AI
{
    public class AIESplitHandler : MonoBehaviour
    {
        private Tank tank;
        private Tank mother;
        private bool initDelay = false;

        public void Setup(Tank ThisTank, Tank Mother)
        {
            tank = ThisTank;
            mother = Mother;
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
                tank.AI.SetBehaviorType(AITreeType.AITypes.Escort);
                BlockManager BM = tank.blockman;
                TankAIHelper help = tank.GetHelperInsured();
                if (BM.blockCount > 0)
                {
                    if (BM.IterateBlockComponents<ModuleWheels>().Count() > 0 || BM.IterateBlockComponents<ModuleHover>().Count() > 0)
                        help.DediAI = AIType.Escort;
                    else
                    {
                        if (BM.IterateBlockComponents<ModuleWeapon>().Count() > 0)
                            help.DediAI = AIType.MTTurret;
                        else
                            help.DediAI = AIType.MTStatic;
                    }
                }
                else
                {   // We assume flares or a drone/infantry to launch
                    help.DediAI = AIType.Escort;
                }
                help.SetDriverType(AIECore.HandlingDetermine(tank, help));
                help.lastCloseAlly = mother;
                DebugTAC_AI.Log(KickStart.ModID + ": AIESplitHandler - Set to " + help.DediAI + " for " + tank.name);
            }
            catch
            {
                DebugTAC_AI.Log(KickStart.ModID + ": AIESplitHandler - CRITICAL ERROR ON UPDATE");
            }
            Destroy(this);
        }
    }
}
