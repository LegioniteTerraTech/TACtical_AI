using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TAC_AI
{
    public enum AIRunState
    {
        /// <summary> Nothing at all, for external use </summary>
        Off,
        /// <summary> Use Vanilla AI </summary>
        Default,
        /// <summary> Use this mod's AI </summary>
        Advanced,
    }
    public enum AIAlignment
    {
        Static,
        PlayerNoAI,
        Player,
        NonPlayer,
        Neutral,
    }

    public enum AIWeaponType
    {
        /// <summary>  We don't know yet</summary>
        Unknown, 
        /// <summary>  We need line of sight to fire </summary>
        Direct,
        /// <summary>  We can fire from anywhere </summary>
        Indirect,
    }
    public enum AIWeaponState
    { // 0 is sleep, 1 is target, 2 is obsticle, 3 is mimic
        Normal,
        Enemy,
        HoldFire,
        Obsticle,
        Mimic,
    }
    public enum AIAnchorState
    {
        None,
        Anchored,
        Anchor,
        AnchorStaticAI,
        ForceAnchor,
        Unanchor,
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
    public enum AIThrottleState
    {
        /// <summary>  Only aim at target </summary>
        PivotOnly,
        /// <summary>  Slow down and moderate top speed. For aircraft, we perform dodge manuvers. </summary>
        Yield,
        /// <summary>  Dynamically adjust speed, prefer top </summary>
        FullSpeed,
        /// <summary>  Force the drive (cab forwards!) to a specific set value </summary>
        ForceSpeed
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
