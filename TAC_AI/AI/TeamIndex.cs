using System;
using System.Collections.Generic;
using System.Linq;

namespace TAC_AI.AI
{
    public class TeamIndex
    {   // 
        /// <summary>
        /// What we should DEFEND
        /// </summary>
        public HashSet<Tank> Teammates = new HashSet<Tank>();
        /// <summary>
        /// What we should NOT attack
        /// </summary>
        public HashSet<Tank> NonHostile = new HashSet<Tank>();
        /// <summary>
        /// What we should and CAN attack
        /// </summary>
        public HashSet<Tank> Targets = new HashSet<Tank>();
    }
}
