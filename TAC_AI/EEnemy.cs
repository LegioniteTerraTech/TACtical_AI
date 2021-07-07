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
        Boss,           // end the player  REALLY PAINFULLY
    }
    public enum EnemyAttitude
    {
        Default,    // Wander around
        Homing,     // Beeline for the nearest enemy tech
        Miner,      // Attack resources
        Junker,     // Move towards patches of loose blocks
        OnRails,    // Follow set destinations instead for MissionManager
    }
    public enum EnemyAttack
    {
        Circle,     // Chase player on provoke, circle on hurt
        Grudge,     // Chase last assailant head-on until death regardless of range [default for SuicideMissile]
        Coward,     // Avoid danger
        Bully,      // Attack the weakest tech in range
        Pesterer,   // Attack random techs and back off when hit
        Spyper,     // Attack player from afar because we are a F^bro-fracker
    }
    public enum EnemySmarts
    {               // retroactive for each step lower on this, also meaning more lag lol
        Default,    // dumb as all heck
        Mild,       // can at least deal with obstructions
        Meh,        // pathfinds two objects at once
        Smrt,       // anchors when left alone
        IntAIligent // enemies nearby this ai ALLY with this AI!
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
