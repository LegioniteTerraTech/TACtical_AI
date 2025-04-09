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
        [DevCommand(Name = KickStart.ModCommandID + ".ForceSpawnBase", Access = Access.Cheat, Users = User.Host)]
        public static CommandReturn ForceEnemyBaseSpawn()
        {
            RawTechLoader.FORCESpawnBaseAtPositionNoFounder(FactionSubTypes.NULL, ManPointer.inst.targetPosition, -9001, BasePurpose.AnyNonHQ);
            return new CommandReturn
            {
                message = "Spawned new Tech",
                success = true,
            };
        }
        [DevCommand(Name = KickStart.ModCommandID + ".OpenAIDebug", Access = Access.Cheat, Users = User.Host)]
        public static CommandReturn OpenDebugMenu()
        {
            DebugRawTechSpawner.LaunchSubMenuClickable(DebugMenus.DebugLog); 
            return new CommandReturn
            {
                message = "Opened AI Debug Menu",
                success = true,
            };
        }
        [DevCommand(Name = KickStart.ModCommandID + ".SpawnAIDebug", Access = Access.Cheat, Users = User.Host)]
        public static CommandReturn OpenSpawnMenu()
        {
            DebugRawTechSpawner.LaunchSubMenuClickable(DebugMenus.Prefabs);
            return new CommandReturn
            {
                message = "Opened AI Prefab Spawn Menu",
                success = true,
            };
        }
        [DevCommand(Name = KickStart.ModCommandID + ".LocalSpawnAIDebug", Access = Access.Cheat, Users = User.Host)]
        public static CommandReturn OpenSpawnLocalMenu()
        {
            DebugRawTechSpawner.LaunchSubMenuClickable(DebugMenus.Local);
            return new CommandReturn
            {
                message = "Opened AI Local Spawn Menu",
                success = true,
            };
        }
    }
}
