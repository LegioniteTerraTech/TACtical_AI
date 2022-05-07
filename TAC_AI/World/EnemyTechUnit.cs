using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TAC_AI.World
{
    public class EnemyTechUnit
    {
        public readonly ManSaveGame.StoredTech tech;
        public FactionTypesExt Faction = FactionTypesExt.GSO;
        public IntVector2 tilePos;

        public string Name
        {
            get
            {
                try
                {
                    return tech.m_TechData.Name;
                }
                catch
                {
                    return "NULL TECHDATA";
                }
            }
            set
            {
                try
                {
                    tech.m_TechData.Name = value;
                }
                catch
                {
                    DebugTAC_AI.LogError("TACtical_AI: EnemyTechUnit - " + Name + " failed to update name!");
                }
            }
        }

        public WorldPosition PosWorld
        {
            get
            {
                try { return tech.m_WorldPosition; }
                catch
                {
                    DebugTAC_AI.LogError("TACtical_AI: EnemyTechUnit - " + Name + " failed to fetch worldPosition!");
                    return WorldPosition.FromGameWorldPosition(Vector3.zero);
                }
            }
            set
            {
                try
                {
                    if (ManEnemyWorld.TryMoveTechIntoTile(this, Singleton.Manager<ManSaveGame>.inst.GetStoredTile(value.TileCoord, false), false))
                        tech.m_WorldPosition = value;
                }
                catch
                {
                    DebugTAC_AI.LogError("TACtical_AI: EnemyTechUnit - " + Name + " failed to update position!");
                }
            }
        }

        /// <summary>
        /// LOSSY AT FAR FROM ORIGIN
        /// </summary>
        public Vector3 PosOrigin
        {
            get
            {
                try
                {
                    try { return tech.m_WorldPosition.GameWorldPosition; }
                    catch { return tech.m_Position; }
                }
                catch
                {
                    DebugTAC_AI.LogError("TACtical_AI: EnemyTechUnit - " + Name + " failed to fetch posWorld!");
                    return Vector3.zero;
                }
            }
            set
            {
                try
                {
                    WorldPosition WP = WorldPosition.FromGameWorldPosition(value);
                    if (ManEnemyWorld.TryMoveTechIntoTile(this, Singleton.Manager<ManSaveGame>.inst.GetStoredTile(WP.TileCoord, false), false))
                        tech.m_WorldPosition = WP;
                }
                catch
                {
                    DebugTAC_AI.LogError("TACtical_AI: EnemyTechUnit - " + Name + " failed to update position!");
                }
            }
        }

        /// <summary>
        /// LOSSY AT FAR FROM CAMERA
        /// </summary>
        public virtual Vector3 PosScene
        {
            get
            {
                try
                {
                    try  { return tech.m_WorldPosition.ScenePosition; }
                    catch { return WorldPosition.FromGameWorldPosition(tech.m_Position).ScenePosition; }
                }
                catch
                {
                    return Vector3.zero;
                }
            }
            set
            {
                try
                {
                    if (ManEnemyWorld.TryMoveTechIntoTile(this, ManWorld.inst.TileManager.GetStoredTileIfNotSpawned(value), false))
                        tech.m_WorldPosition = WorldPosition.FromScenePosition(value);
                }
                catch
                {
                    DebugTAC_AI.LogError("TACtical_AI: EnemyBaseUnloaded - Base " + Name + " failed to update position!");
                }
            }
        }

        public long Health = 0;
        public long MaxHealth = 0;
        public float MoveSpeed = 0;
        public int AttackPower = 0;
        public bool isFounder = false;
        public bool isMoving = false;
        public bool isArmed = false;
        public bool canHarvest = false;

        public EnemyTechUnit(IntVector2 tilePosition, ManSaveGame.StoredTech techIn)
        {
            tilePos = tilePosition;
            tech = techIn;
        }

        public bool IsNullOrTechMissing()
        {
            return this == null || tech == null;
        }

        /// <summary>
        /// Deal damage to this Tech
        /// </summary>
        /// <param name="dealt"></param>
        /// <returns>If tech destroyed</returns>
        public bool TakeDamage(int Dealt)
        {
            ManEnemyWorld.GetTeam(tech.m_TeamID).SetEvent(tilePos);
            Health -= Dealt;
            EnemyPresence.ReportCombat("TACtical_AI: EnemyPresence - Enemy " + Name + " has received " + Dealt + " damage | Health " + Health);
            return Health < 0;
        }
    }
}
