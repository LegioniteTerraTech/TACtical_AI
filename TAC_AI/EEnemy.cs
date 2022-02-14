using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TAC_AI.AI.Enemy
{
    //class EEnemy
    //{
    //}
    public enum EnemyHandling
    {
        Wheeled,
        Chopper,
        Airplane,
        Starship,
        Naval,
        SuicideMissile, // set for bolt tech splitoffs with less than 2 weapons (excluding cabs)
        Stationary,     // sit there like a good wingnut
    }
    public enum EnemyAttitude
    {
        Default,    // Wander around
        Homing,     // Beeline for the nearest enemy tech
        Miner,      // Attack resources
        Junker,     // Move towards patches of loose blocks
        OnRails,    // Follow set destinations instead for MissionManager
        Boss,       // Build a big base and show off your power on the off-world
        Invader,    // One. job.   INVADE      end the player  REALLY PAINFULLY
        SubNeutral, // Don't attack unless attacked
    }
    public enum EnemyAttack
    {
        Circle,     // Circle the enemy while shooting at them
        // !! Only active with TweakTech or WeaponAimMod (because no target leading) !!
        // Use for: Skirmishers, Mid-long-ranged units with fast turrets [GSO Gigaton, VEN Rapid Cannon, HE HG Cannon, BF Arc Missiles, RR Sonic Blaster TAC Terminator]
        Grudge,     // Chase last assailant head-on until death regardless of range [default for SuicideMissile]
        // Use for: Homing missiles, Dualling units, Eradicators hellbent on removing the player from existance
        Coward,     // Avoid danger
        // Use for: Any Non-Combat Tech
        Bully,      // Attack the weakest tech in range
        // Use for: Riots, Area Denial
        Pesterer,   // Attack random techs
        // Use for: Interceptors, Raiders
        Spyper,     // Attack player from afar because we are a F^bro-fracker
        // Use for: Artillery, Motherships
    }
    public enum EnemySmarts
    {               // retroactive for each step lower on this, also meaning more lag lol
        Default,    // literally default AI
        Mild,       // can at least deal with obstructions
        Meh,        // pathfinds two objects at once
        Smrt,       // anchors when left alone
        IntAIligent // enemies nearby this ai ALLY with this AI! - (still planned but not functional yet)
    }
    public enum EnemyBolts
    {                   // Handler for how you want your bolts used
        Default,        // Explode IMMEDEATELY
        AtFull,         // Explode when tech is fully-built (requires smrt or above to utilize nicely)
        AtFullOnAggro,  // Explode when tech is fully-built and enemy in range (requires smrt or above to utilize nicely)
        Countdown,      // Explode after # of ingame FixedUpdate ticks
        MissionTrigger, // Hold until triggered by mission event
    }
}
