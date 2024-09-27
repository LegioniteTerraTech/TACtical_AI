using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TerraTechETCUtil;

namespace TAC_AI.Templates
{
    public enum RawTechOffset
    {
        OnGround,
        RaycastTerrainAndScenery,
        OffGround60Meters,
        Exact,
    }
    public class RawTechPopParams
    {
        public static RawTechPopParams Default => new RawTechPopParams();
        public RawTechPopParams()
        {
            _purposes = new HashSet<BasePurpose>();

            Faction = FactionSubTypes.NULL;
            Progression = RawTechLoader.TryGetPlayerLicenceLevel();
            Terrain = BaseTerrain.Any;
            Offset = RawTechOffset.Exact;
            MaxGrade = 99;
            MaxPrice = 0;
            RandSkins = true;
            TeamSkins = true;
            ForceAnchor = false;
            IsPopulation = true;
            SpawnCharged = true;
            //AllowAutominers = AIGlobals.AllowInfAutominers,
            BlockConveyors = false;
            SearchAttract = AIGlobals.IsAttract;
            ExcludeErad = !KickStart.EnemyEradicators || SpecialAISpawner.Eradicators.Count >= AIGlobals.MaxEradicatorTechs;
            Disarmed = false;
            ForceCompleted = false;
        }
        /// <summary>
        /// WARNING OVERWRITES Purposes!!!
        /// </summary>
        /// <param name="tech"></param>
        public RawTechPopParams(RawTech tech)
        {
            _purposes = tech.purposes;

            Faction = FactionSubTypes.NULL;
            Progression = RawTechLoader.TryGetPlayerLicenceLevel();
            Terrain = BaseTerrain.Any;
            Offset = RawTechOffset.Exact;
            MaxGrade = 99;
            MaxPrice = 0;
            RandSkins = true;
            TeamSkins = true;
            IsPopulation = true;
            SpawnCharged = true;
            ForceCompleted = false;
        }

        public FactionSubTypes Faction;
        public FactionLevel Progression;
        public BaseTerrain Terrain;
        private HashSet<BasePurpose> _purposes;
        /// <summary>
        /// WARNING: DO NOT ASSIGN A HASHSET THAT IS ALREADY USED ELSEWHERE
        /// </summary>
        public HashSet<BasePurpose> Purposes
        {
            get => _purposes;
            set
            {
                if (value == null)
                    throw new NullReferenceException("purposes cannot be null"); 
                _purposes = value;
            }
        }
        public BasePurpose Purpose
        {
            get => _purposes.First();
            set
            {
                _purposes.Clear();
                _purposes.Add(value);
            }
        }
        public RawTechOffset Offset;
        public int MaxGrade;
        public int MaxPrice;
        public bool ForceAnchor
        {
            get => !_purposes.Contains(BasePurpose.NotStationary);
            set
            {
                if (value)
                    _purposes.Remove(BasePurpose.NotStationary);
                else
                    _purposes.Add(BasePurpose.NotStationary);
            }
        }
        public bool SnapTerrain => Offset == RawTechOffset.OnGround || 
            Offset == RawTechOffset.RaycastTerrainAndScenery || ForceAnchor;
        public bool IsPopulation;
        public bool SpawnCharged;
        public bool RandSkins;
        public bool TeamSkins;
        public bool ExcludeErad
        {
            get => !_purposes.Contains(BasePurpose.NANI);
            set
            {
                if (value)
                    _purposes.Remove(BasePurpose.NANI);
                else
                    _purposes.Add(BasePurpose.NANI);
            }
        }
        public bool Disarmed
        {
            get => _purposes.Contains(BasePurpose.NoWeapons);
            set
            {
                if (value)
                    _purposes.Add(BasePurpose.NoWeapons);
                else
                    _purposes.Remove(BasePurpose.NoWeapons);
            }
        }
        public bool SearchAttract
        {
            get => _purposes.Contains(BasePurpose.AttractTech);
            set
            {
                if (value)
                    _purposes.Add(BasePurpose.AttractTech);
                else
                    _purposes.Remove(BasePurpose.AttractTech);
            }
        }
        public bool BlockConveyors
        {
            get => !_purposes.Contains(BasePurpose.MPUnsafe);
            set
            {
                if (value)
                    _purposes.Remove(BasePurpose.MPUnsafe);
                else
                    _purposes.Add(BasePurpose.MPUnsafe);
            }
        }
        public bool AllowAutominers
        {
            get => _purposes.Contains(BasePurpose.Autominer);
            set
            {
                if (value)
                    _purposes.Add(BasePurpose.Autominer);
                else
                    _purposes.Remove(BasePurpose.Autominer);
            }
        }
        public bool AllowSnipers
        {
            get => _purposes.Contains(BasePurpose.Sniper);
            set
            {
                if (value)
                    _purposes.Add(BasePurpose.Sniper);
                else
                    _purposes.Remove(BasePurpose.Sniper);
            }
        }
        public bool ForceCompleted;
    }
}
