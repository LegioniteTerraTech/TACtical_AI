using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TAC_AI
{
    public enum AIAlignment
    {
        Static,
        PlayerNoAI,
        Player,
        NonPlayer,
        Neutral,
    }

    public enum AIWeaponState
    { // 0 is sleep, 1 is target, 2 is obsticle, 3 is mimic
        Normal,
        Enemy,
        Obsticle,
        Mimic,
    }

    /// <summary>
    /// Update later
    /// </summary>
    public enum AIDriveState
    {
        None,
        Driving,
        NonPlayer,
        Neutral,
    }

    public enum NP_Types
    {
        Player,
        NonNPT,
        Friendly,
        Neutral,
        NonAggressive,
        SubNeutral,
        Enemy,
    }
}
