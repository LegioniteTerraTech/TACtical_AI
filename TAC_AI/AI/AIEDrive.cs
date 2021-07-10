using UnityEngine;
using System.Reflection;

namespace TAC_AI.AI
{
    public static class AIEDrive
    {
        


        //Combat handlers for DriveDirector
        


        /// <summary>
        /// For allied AI to determine combat readiness
        /// </summary>
        /// <param name="thisInst"></param>
        public static void DetermineCombat(AIECore.TankAIHelper thisInst)
        {
            bool DoNotEngage = false;
            if (thisInst.lastPlayer.IsNotNull())
            {
                if (thisInst.lastBasePos.IsNotNull())
                {
                    if (thisInst.IdealRangeCombat * 2 < (thisInst.lastBasePos.position - thisInst.tank.boundsCentreWorldNoCheck).magnitude && thisInst.DediAI == AIECore.DediAIType.Assault)
                        DoNotEngage = true;
                }
                if (thisInst.IdealRangeCombat < (thisInst.lastPlayer.tank.boundsCentreWorldNoCheck - thisInst.tank.boundsCentreWorldNoCheck).magnitude && thisInst.DediAI != AIECore.DediAIType.Assault)
                    DoNotEngage = true;
                else if (thisInst.AdvancedAI)
                {
                    //WIP
                    if (thisInst.DamageThreshold > 30)
                    {
                        DoNotEngage = true;
                    }
                }
            }
            thisInst.Retreat = DoNotEngage;
        }
    }
}
