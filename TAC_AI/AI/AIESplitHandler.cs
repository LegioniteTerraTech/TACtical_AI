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
