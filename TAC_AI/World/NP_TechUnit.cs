using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TerraTechETCUtil;
using TAC_AI.Templates;

namespace TAC_AI.World
{
    public abstract class NP_TechUnit
    {
        public readonly NP_Presence_Automatic teamInst;
        public int Team => tech.m_TeamID;
        public ManSaveGame.StoredTech tech { get; private set; }
        public TrackedVisible trackedVis { get; private set; }
        public FactionSubTypes Faction = FactionSubTypes.GSO;
        public IntVector2 tilePos => WorldPos.TileCoord;
        public int ID => trackedVis.ID;

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
                    DebugTAC_AI.LogError(KickStart.ModID + ": EnemyTechUnit - " + Name + " failed to update name!");
                }
            }
        }

        public WorldPosition WorldPos
        {
            get
            {
                try { return AIGlobals.GetWorldPos(tech); }
                catch
                {
                    DebugTAC_AI.LogError(KickStart.ModID + ": EnemyTechUnit - " + Name + " failed to fetch worldPosition!");
                    return WorldPosition.FromGameWorldPosition(Vector3.zero);
                }
            }
        }

        /// <summary>
        /// LOSSY AT FAR FROM ORIGIN
        /// </summary>
        public Vector3 PosWorld => WorldPos.GameWorldPosition;

        /// <summary>
        /// LOSSY AT FAR FROM CAMERA
        /// </summary>
        public virtual Vector3 PosScene => WorldPos.ScenePosition;

        public long Health;
        public long Shield;
        public bool isMoving => ManEnemyWorld.QueuedUnitMoves.ContainsKey(this);

        public long MaxHealth;
        public long MaxShield;
        internal int BaseAttackPower;
        public int AttackPower => Mathf.CeilToInt(BaseAttackPower * ((float)Health / MaxHealth));
        public bool isArmed = false;
        public bool handlesChunks = false;

        protected NP_TechUnit(ManSaveGame.StoredTech techIn, NP_Presence_Automatic team, FactionSubTypes faction)
        {
            if (techIn == null)
                throw new NullReferenceException("techIn cannot be null!");
            tech = techIn;
            Health = -1;
            if (team == null)
                throw new NullReferenceException("team cannot be null!");
            teamInst = team;
            Faction = faction;
        }


        public abstract float GetSpeed();
        public abstract float GetEvasion();
        public abstract bool Exists();

        /*
        public bool SetPositionWorld(WorldPosition newPos)
        {
            try
            {
                if (ManEnemyWorld.TryMoveTechIntoTile(this, Singleton.Manager<ManSaveGame>.inst.GetStoredTile(newPos.TileCoord, false), false))
                {
                    tech.m_WorldPosition = newPos;
                }
            }
            catch
            {
                DebugTAC_AI.LogError(KickStart.ModID + ": EnemyTechUnit - " + Name + " failed to update position!");
            }
            return false;
        }*/
        internal void SetTracked(TrackedVisible TV)
        {
            trackedVis = TV;
        }
        internal void SetTech(ManSaveGame.StoredTech tech)
        {
            this.tech = tech;
        }


        internal abstract void MovementSceneDelta(float timeDelta);
        internal void SetFakeTVLocation(Vector3 posScene)
        {
            trackedVis.StopTracking();
            trackedVis.SetPos(posScene);
        }
        internal void UpdateTVLocation()
        {
            trackedVis.SetPos(WorldPos);
        }

        public Tank GetActiveTech()
        {
            var iterit = ManTechs.inst.IterateTechsWhere(x => x.visible.ID == ID);
            if (iterit.Any())
                return iterit.FirstOrDefault();
            return null;
        }

        public bool IsNullOrTechMissing()
        {
            return this == null || tech == null;
        }
        public void Recharge(ref long Recharge)
        {
            long Needs = MaxShield - Shield;
            Recharge -= Needs;
            if (Recharge > 0)
                Shield = MaxShield;
            else
            {
                Shield = MaxShield - Needs;
            }
        }

        /// <summary>
        /// Deal damage to this Tech
        /// </summary>
        /// <param name="dealt"></param>
        /// <returns>True if tech destroyed</returns>
        public abstract bool RecieveDamage(int Dealt);
        internal void ApplyDamage()
        {
            if (Health == MaxHealth)
                return;
            float damagePercent = (float)Health / MaxHealth;
            SpecialAISpawner.InflictPercentDamage(tech.m_TechData, 1 - damagePercent);
            BaseAttackPower = AttackPower;
            MaxHealth = Health;
        }
        internal bool ShouldApplyShields()
        {
            if (MaxShield > 0)
            {
                foreach (TankPreset.BlockSpec spec in tech.m_TechData.m_BlockSpecs)
                {
                    try
                    {
                        var BT = spec.GetBlockType();
                        if (ManSpawn.inst.GetBlockAttributes(BT).Contains(BlockAttributes.PowerStorage))
                        {
                            return spec.saveState.Count == 0;
                        }
                    }
                    catch { }
                }
            }
            return false;
        }
        internal void DoApplyShields(Tank techActive)
        {
            if (techActive == null)
                return;
            float Shields = (float)Shield / MaxShield;
            RawTechLoader.ChargeAndClean(techActive, Shields);
            DebugTAC_AI.Log("First time spawn for " + Name + " with shields");
        }
    }
}
