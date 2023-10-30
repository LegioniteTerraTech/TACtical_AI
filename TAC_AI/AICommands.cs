using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DevCommands;
using TerraTechETCUtil;
using UnityEngine;
using TAC_AI.Templates;

namespace TAC_AI
{
    public static class AICommands
    {
        [DevCommand(Name = KickStart.ModCommandID + ".ForceSpawn", Access = Access.Public, Users = User.Host)]
        public static ManDevCommands.CommandReturn ForceEnemySpawn()
        {
            RawTechLoader.StartBaseAtPositionNoFounder(FactionSubTypes.GSO, ManPointer.inst.targetPosition, -9001, BasePurpose.AnyNonHQ);
            return new ManDevCommands.CommandReturn
            {
                message = "Spawned new Tech",
                success = true,
            };
        }
    }
}
