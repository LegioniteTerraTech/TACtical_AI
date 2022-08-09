using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using TAC_AI.AI.Enemy;
using TAC_AI.AI.Movement.AICores;

namespace TAC_AI.AI
{
    /// <summary>
    /// Handles all anchored operations 
    /// </summary>
    public class AIControllerStatic : MonoBehaviour, IMovementAIController
    {
        private Tank _tank;
        public Tank Tank
        {
            get => _tank;
            internal set => _tank = value;
        }
        private AIECore.TankAIHelper _helper;
        public AIECore.TankAIHelper Helper
        {
            get => _helper;
            internal set => _helper = value;
        }
        private IMovementAICore _AI;
        public IMovementAICore AICore
        {
            get => _AI;
            internal set => _AI = value;
        }
        private EnemyMind _mind;
        public EnemyMind EnemyMind
        {
            get => _mind;
            internal set => _mind = value;
        }

        public Vector3 AimTarget = Vector3.zero;
        public Vector2 SceneStayPos = Vector2.zero;
        public float HoldHeight = 0;

        public Vector3 MovePosition => SceneStayPos.ToVector3XZ(HoldHeight);
        public Vector2 IdleFacingDirect = Vector2.up;

        public void Initiate(Tank tank, AIECore.TankAIHelper helper, EnemyMind mind = null)
        {
            Tank = tank;
            Helper = helper;
            EnemyMind = mind;

            SceneStayPos = tank.boundsCentreWorld.ToVector2XZ();
            HoldHeight = SceneStayPos.y;
            List<Tank> Techs = AIECore.TankAIManager.GetNonEnemyTanks(Tank.Team);
            Techs.Remove(tank);
            if (Techs.Count > 0)
            {
                Vector3 PosWorld = Techs.OrderByDescending(x => x.IsAnchored).ThenBy(x => (x.boundsCentreWorld - tank.boundsCentreWorldNoCheck).sqrMagnitude).First().boundsCentreWorld;
                IdleFacingDirect = (tank.boundsCentreWorldNoCheck - PosWorld).ToVector2XZ().normalized;
            }
            AICore = new StaticAICore();
            AICore.Initiate(tank, this);

            DebugTAC_AI.Log("TACtical_AI: Added static AI for " + Tank.name);
        }
        public void UpdateEnemyMind(EnemyMind mind)
        {
            EnemyMind = mind;
        }

        public void DriveDirector()
        {
            if (Helper == null)
            {
                string tankName = Tank.IsNotNull() ? Tank.name : "UNKNOWN_TANK";
                DebugTAC_AI.Assert(true, "TACtical_AI: AI " + tankName + ":  FIRED DriveDirector WITHOUT THE REQUIRED TankAIHelper MODULE!!!");
                return;
            }

            if (Helper.AIState == AIAlignment.Player)// Allied
            {
                if (AICore == null)
                {
                    string tankName = Tank.IsNotNull() ? Tank.name : "UNKNOWN_TANK";
                    DebugTAC_AI.Assert(true, "TACtical_AI: AI " + tankName + ":  FIRED DriveDirector WITHOUT ANY SET AICore!!!");
                    return;
                }
                AICore.DriveDirector();
            }
            else//ENEMY
            {
                AICore.DriveDirectorEnemy(EnemyMind);
            }
        }

        public void DriveDirectorRTS()
        {   // Ignore player movement commands but follow attack commands
            if (Helper == null)
            {
                string tankName = Tank.IsNotNull() ? Tank.name : "UNKNOWN_TANK";
                DebugTAC_AI.Assert(true, "TACtical_AI: AI " + tankName + ":  FIRED DriveDirectorRTS WITHOUT THE REQUIRED TankAIHelper MODULE!!!");
                return;
            }

            if (Helper.AIState == AIAlignment.Player)// Allied
            {
                if (AICore == null)
                {
                    string tankName = Tank.IsNotNull() ? Tank.name : "UNKNOWN_TANK";
                    DebugTAC_AI.Assert(true, "TACtical_AI: AI " + tankName + ":  FIRED DriveDirectorRTS WITHOUT ANY SET AICore!!!");
                    return;
                }
                AICore.DriveDirectorRTS();
            }
            else//ENEMY
            {
                AICore.DriveDirectorEnemy(EnemyMind);
            }
        }

        public void DriveMaintainer(TankControl thisControl)
        {
            thisControl.m_Movement.m_USE_AVOIDANCE = false;
            AICore.DriveMaintainer(thisControl, Helper, Tank);
        }

        public void OnMoveWorldOrigin(IntVector3 move)
        {
            SceneStayPos += (Vector2)move.ToVector2XZ();
        }
        public Vector3 GetDestination()
        {
            return SceneStayPos.ToVector3XZ(HoldHeight);
        }

        public void Recycle()
        {
            AICore = null;
            if (this.IsNotNull())
            {
                //Debug.Log("TACtical_AI: Removed static AI from " + Tank.name);
                DestroyImmediate(this);
            }
        }

        public bool IsTurretable => !Tank.Anchors.Fixed;
        public bool IsSkyAnchoredOnly => !Tank.Anchors.Fixed && Tank.IsSkyAnchored;
       
    }
}
