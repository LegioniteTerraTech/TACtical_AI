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
    internal class AIControllerStatic : MonoBehaviour, IMovementAIController
    {
        private Tank _tank;
        public Tank Tank
        {
            get => _tank;
            internal set => _tank = value;
        }
        private TankAIHelper _helper;
        public TankAIHelper Helper
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
        public WorldPosition SceneStayPos = WorldPosition.FromGameWorldPosition(Vector3.zero);
        public float HoldHeight = 0;

        public Vector3 PathPoint => SceneStayPos.ScenePosition.SetY(HoldHeight);
        public Vector2 IdleFacingDirect = Vector2.up;
        public float GetDrive => _AI.GetDrive;

        public void Initiate(Tank tank, TankAIHelper helper, EnemyMind mind = null)
        {
            Tank = tank;
            Helper = helper;
            EnemyMind = mind;

            HoldHeight = tank.boundsCentreWorld.y;
            SceneStayPos = WorldPosition.FromScenePosition(tank.boundsCentreWorld);
            /*
            List<Tank> Techs = TankAIManager.GetNonEnemyTanks(Tank.Team);
            Techs.Remove(tank);
            if (Techs.Count > 0)
            {
                Vector3 PosWorld = Techs.OrderByDescending(x => x.IsAnchored).ThenBy(x => (x.boundsCentreWorld - tank.boundsCentreWorldNoCheck).sqrMagnitude).FirstOrDefault().boundsCentreWorld;
                IdleFacingDirect = (tank.boundsCentreWorldNoCheck - PosWorld).ToVector2XZ().normalized;
            }*/
            IdleFacingDirect = Vector3.forward;
            AICore = new StaticAICore();
            AICore.Initiate(tank, this);


            DebugTAC_AI.LogAISetup(KickStart.ModID + ": Added static (anchored) AI for " + Tank.name);
        }
        public void UpdateEnemyMind(EnemyMind mind)
        {
            EnemyMind = mind;
        }

        public void DriveDirector(ref EControlCoreSet core)
        {
            if (Helper == null)
            {
                string tankName = Tank.IsNotNull() ? Tank.name : "UNKNOWN_TANK";
                DebugTAC_AI.Assert(true, KickStart.ModID + ": AI " + tankName + ":  FIRED DriveDirector WITHOUT THE REQUIRED TankAIHelper MODULE!!!");
                return;
            }
            Helper.TryInsureAnchor();

            if (Helper.AIAlign == AIAlignment.Player)// Allied
            {
                if (AICore == null)
                {
                    string tankName = Tank.IsNotNull() ? Tank.name : "UNKNOWN_TANK";
                    DebugTAC_AI.Assert(true, KickStart.ModID + ": AI " + tankName + ":  FIRED DriveDirector WITHOUT ANY SET AICore!!!");
                    return;
                }
                AICore.DriveDirector(ref core);
            }
            else//ENEMY
            {
                AICore.DriveDirectorEnemy(EnemyMind, ref core);
            }
        }

        public void DriveDirectorRTS(ref EControlCoreSet core)
        {   // Ignore player movement commands but follow attack commands
            if (Helper == null)
            {
                string tankName = Tank.IsNotNull() ? Tank.name : "UNKNOWN_TANK";
                DebugTAC_AI.Assert(true, KickStart.ModID + ": AI " + tankName + ":  FIRED DriveDirectorRTS WITHOUT THE REQUIRED TankAIHelper MODULE!!!");
                return;
            }
            Helper.TryInsureAnchor();

            if (Helper.AIAlign == AIAlignment.Player)// Allied
            {
                if (AICore == null)
                {
                    string tankName = Tank.IsNotNull() ? Tank.name : "UNKNOWN_TANK";
                    DebugTAC_AI.Assert(true, KickStart.ModID + ": AI " + tankName + ":  FIRED DriveDirectorRTS WITHOUT ANY SET AICore!!!");
                    return;
                }
                AICore.DriveDirectorRTS(ref core);
            }
            else//ENEMY
            {
                AICore.DriveDirectorEnemy(EnemyMind, ref core);
            }
        }

        public void DriveMaintainer(ref EControlCoreSet core)
        {
            AICore.DriveMaintainer(Helper, Tank, ref core);
        }

        public void OnMoveWorldOrigin(IntVector3 move)
        {
        }
        public Vector3 GetDestination()
        {
            return PathPoint;
        }

        public void Recycle()
        {
            AICore = null;
            if (this.IsNotNull())
            {
                //DebugTAC_AI.Log(KickStart.ModID + ": Removed static AI from " + Tank.name);
                DestroyImmediate(this);
            }
        }

        public bool IsTurretable => !Tank.Anchors.Fixed;
        public bool IsSkyAnchoredOnly => !Tank.Anchors.Fixed && Tank.IsSkyAnchored;
       
    }
}
