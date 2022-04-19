using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TAC_AI
{
    /// <summary>
    /// Stores all global information for this mod. Edit at your own risk.
    /// </summary>
    public class AIGlobals
    {
        // AIERepair contains the self-repair stats
        // EnemyWorldManager contains the unloaded enemy stats

        //-------------------------------------
        //              CONSTANTS
        //-------------------------------------
        // SPAWNING
        public const int SmolTechBlockThreshold = 24;
        public const int HomingWeaponCount = 25;
        public const int BossTechSize = 150;
        public const int LethalTechSize = 256;
        public const int MaxEradicatorTechs = 2;
        public const int MaxBlockLimitAttract = 128;

        public const float MinimumBaseSpacing = 450;
        public const float MinimumMonitorSpacingSqr = 122500;

        // GENERAL AI PARAMETERS
        public const float SafeAnchorDist = 50f;     // enemy too close to anchor
        public const int TeamRangeStart = 256;
        public const short NetAIClockPeriod = 30;

        // Pathfinding
        public const float DodgeStrengthMultiplier = 1.75f;  // The motivation in trying to move away from a tech in the way
        public const float FindBaseExtension = 500;
        // Control the aircrafts and AI
        public const float AirMaxHeightOffset = 250;
        public const float AirMaxHeight = 150;
        public const float AirPromoteHeight = 200;


        // Item Handling
        public const float BlockAttachDelay = 0.75f;        // How long until we actually attach the block when playing the placement animation
        public const float MaxBlockGrabRange = 47.5f;       // Less than player range to compensate for precision
        public const float MaxBlockGrabRangeAlt = 5;        // Lowered range to allow scrap magnets to have a chance
        public const float ItemGrabStrength = 1750;         // The max acceleration to apply when holding an item
        public const float ItemThrowVelo = 115;             // The max velocity to apply when throwing an item
        public const float AircraftHailMaryRange = 65f;     // Try throw things this far away for aircraft 
        //  because we don't want to burn daylight trying to land and takeoff again

        // Charger Parameters
        public const float minimumChargeFractionToConsider = 0.75f;


        // ENEMY AI PARAMETERS
        // Active Enemy AI Techs
        public const int DefaultEnemyRange = 150;
        public const int TileFringeDist = 96;

        public const int ProvokeTime = 200;

        // Combat target switching
        public const int ScanDelay = 20;            // Frames until we try to find a appropreate target
        public const int PestererSwitchDelay = 500; // Frames before Pesterers find a new random target

        // Sight ranges
        public const float MaxRangeFireAll = 125;   // WEAPON AIMING RANGE
        public const int BaseFounderRange = 60;     // 
        public const int BossMaxRange = 250;        // 
        public const float SpyperMaxRange = 450;    // 

        // Combat Spacing Ranges
        public const float SpacingRange = 12;
        public const float SpacingRangeSpyper = 64;
        public const float SpacingRangeAircraft = 32;
        public const float SpacingRangeHoverer = 26;

        // Enemy Base Checks
        public const int MinimumBBRequired = 10000; // Before expanding
        public const int MinimumStoredBeforeTryBribe = 100000;
        public const float BribePenalty = 1.5f;
        public const int BaseExpandChance = 65;//18;
        public const int MinResourcesReqToCollect = 50;
        public const int EnemyBaseMiningMaxRange = 250;
        public const int EnemyExtendActionRange = 500 + 32; //the extra 32 to account for tech sizes

        public const int MPEachBaseProfits = 25000;

    }
}
