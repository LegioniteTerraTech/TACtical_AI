using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TAC_AI.AI
{

    /// <summary>
    /// We can only be going to one location at once!
    /// </summary>
    public enum EDriveDest
    {   //Control the AI drive direction
        None, // No target


        // Corrdinate-Based Targets
        /// <summary>
        /// Drive from target POINTING AT TARGET [in relation to DriveDir]
        /// </summary>
        FromLastDestination,

        /// <summary>
        /// Drive to target
        /// </summary>
        ToLastDestination,


        // Dynamically Changing Targets
        /// <summary>
        /// Counts also as [recharge home, block rally]
        /// </summary>
        ToBase,

        /// <summary>
        /// Counts also as [loose block, target enemy, target to charge]
        /// </summary>
        ToMine
    }

    public enum EDriveFacing
    {   //Control the AI drive firection
        Neutral,
        Forwards,
        Perpendicular,
        Backwards
    }
}
