using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TAC_AI.AI.Enemy
{
    /// <summary>
    /// How the AI handles it's Tech
    /// </summary>
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

    /// <summary>
    /// What the AI does when Idle
    /// </summary>
    public enum EnemyAttitude
    {
        Default,    // Wander around
        Homing,     // Beeline for the nearest enemy tech
        Miner,      // Attack resources
        Junker,     // Move towards patches of loose blocks
        OnRails,    // Follow set RTS destinations instead for MissionManager
        NPCBaseHost,// Build a base and manage it, plus go off and do "missions"
        Boss,       // Build a big base and show off your power on the off-world
        Invader,    // One. job.   INVADE      end the player  REALLY PAINFULLY
    }

    /// <summary>
    /// How the AI reacts to the Player
    /// </summary>
    public enum EnemyStanding
    {
        Enemy,      // Attack. Always
        Friendly,   // Fight on the player's side
        SubNeutral, // Don't attack unless attacked once -> Attack everyone
        Neutral,    // Don't attack unless a block falls off -> Attack attacker
        MissionControl,// Only follow mission requests
    }

    /// <summary>
    /// The AI's skill level and abilities
    /// </summary>
    public enum EnemySmarts
    {               // retroactive for each step lower on this, also meaning more lag lol
        Default,    // literally default AI
        Mild,       // can at least deal with obstructions
        Meh,        // pathfinds two objects at once
        Smrt,       // anchors when left alone
        IntAIligent // enemies nearby this ai ALLY with this AI! - (still planned but not functional yet)
    }

    /// <summary>
    /// The conditions of which the AI decides to press X
    /// </summary>
    public enum EnemyBolts
    {                   // Handler for how you want your bolts used
        Default,        // Explode IMMEDEATELY
        AtFull,         // Explode when tech is fully-built (requires smrt or above to utilize nicely)
        AtFullOnAggro,  // Explode when tech is fully-built and enemy in range (requires smrt or above to utilize nicely)
        Countdown,      // Explode after # of ingame FixedUpdate ticks
        MissionTrigger, // Hold until triggered by mission event
    }
}
