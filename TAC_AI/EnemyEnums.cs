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
        /// <summary>Drive like a Tank on the floor and avoid trees</summary>
        Wheeled,
        /// <summary>Keep thrusting up while floating above tree-line</summary>
        Chopper,
        /// <summary>Keep thrusting forwards while flying above tree-line</summary>
        Airplane,
        /// <summary>Float above tree-line</summary>
        Starship,
        /// <summary>Float above water-line and avoid terrain</summary>
        Naval,
        /// <summary>Fly vertically, make approx 90-degree turn, 
        /// then ram into the enemy by whatever means possible</summary>
        SuicideMissile, // set for bolt tech splitoffs with less than 2 weapons (excluding cabs)
        /// <summary>Stay still.  Avoid anchoring when possible</summary>
        Stationary,     // sit there like a good wingnut
    }

    /// <summary>
    /// What the AI does when Idle
    /// </summary>
    public enum EnemyAttitude
    {
        /// <summary>Wander around</summary>
        Default,
        /// <summary>Beeline for the nearest enemy tech</summary>
        Homing, 
        /// <summary>Harvest resources</summary>
        Miner,
        /// <summary>Collect loose blocks</summary>
        Junker,
        /// <summary>Follow set RTS destinations</summary>
        OnRails,
        /// <summary>Build a base and manage it, plus go off and do "missions" (WIP)</summary>
        NPCBaseHost,
        /// <summary>Build a big-S base and show off your power on the off-world</summary>
        Boss,
        /// <summary>One. job.<para>INVADE</para><para>end the player</para><para>REALLY PAINFULLY</para></summary>
        Invader,
        /// <summary>Protect other allies</summary>
        Guardian,

        /// <summary>Like allied equiv</summary>
        PartTurret,
        /// <summary>Like allied equiv</summary>
        PartStatic,
        /// <summary>Like allied equiv</summary>
        PartMimic,
    }

    /// <summary>
    /// How the AI interacts with the Player
    /// </summary>
    public enum EnemyStanding
    {
        /// <summary>My sole existance is to <c>D E S T R O Y</c></summary>
        Enemy,      // Attack. Always
        /// <summary>Fight on the player's side</summary>
        Friendly,
        /// <summary>Don't attack unless attacked once -> Attack everyone</summary>
        SubNeutral,
        /// <summary>Don't attack at all.  Usually indestructable.</summary>
        Neutral,
        /// <summary>Only follow mission requests</summary>
        MissionControl,
    }

    /// <summary>
    /// The AI's skill level and abilities.  Retroactive.
    /// </summary>
    public enum EnemySmarts
    {   // retroactive for each step lower on this.
        /// <summary>Literally default AI</summary>
        Default,
        /// <summary>Can at least deal with obstructions</summary>
        Mild,
        /// <summary>Pathfinds two objects at once</summary>
        Meh,
        /// <summary>Anchors when left alone</summary>
        Smrt,
        /// <summary>Enemies nearby this ai ALLY with this AI!</summary>
        IntAIligent
    }

    /// <summary>
    /// The conditions of which the AI decides to press X
    /// <list type="">EnemyBolts</list>
    /// <list type="bullet">Default        - Explode IMMEDEATELY</list>
    /// <list type="bullet">AtFull         - Explode when tech is fully-built (requires smrt or above to utilize nicely)</list>
    /// <list type="bullet">AtFullOnAggro  - Explode when tech is fully-built and enemy in range (requires smrt or above to utilize nicely)</list>
    /// <list type="bullet">Countdown      - Explode after # of ingame FixedUpdate ticks</list>
    /// <list type="bullet">MissionTrigger - Hold until triggered by mission event (basically no internal fire)</list>
    /// </summary>
    public enum EnemyBolts
    {                   // Handler for how you want your bolts used
        /// <summary>Explode IMMEDEATELY</summary>
        Default,
        /// <summary>Explode when tech is fully-built (requires smrt or above to utilize nicely)</summary>
        AtFull,
        /// <summary>Explode when tech is fully-built and enemy in range (requires smrt or above to utilize nicely)</summary>
        AtFullOnAggro,
        /// <summary>Explode after # of ingame FixedUpdate ticks</summary>
        Countdown,
        /// <summary>Hold until triggered by mission event</summary>
        MissionTrigger,
    }
}
