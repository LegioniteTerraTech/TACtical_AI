using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TAC_AI.Templates
{
    class EBasePurpose
    {
    }
    public enum BaseTypeLevel
    {                   
        Basic,      //basic base
        Advanced,
        Headquarters,
        Overkill,
        InvaderSpecific,
    }
    public enum BaseTerrain
    {
        Any,
        AnyNonSea,
        Land,   // is anchored
        Sea,    // floats on water
        Air,    // doubles as airplane
        Chopper,// relies on props to stay airborne
        Space   // mobile base that flies beyond
    }
    public enum BasePurpose
    {
        AnyNonHQ,       // Any base that's not an HQ
        HarvestingNoHQ, // Any harvesting base that's not an HQ
        Defense,        // Strictly defensive base element
        Harvesting,     // Has Delivery cannons
        Autominer,      // Can mine unlimited BB (DO NOT ATTACH THIS TAG TO HQs!!!)
        TechProduction, // Base with Explosive Bolts attached
        Headquarters,   // Calls in techs from orbit using funds
        MPSafe,         // Has no MP blocked crafting blocks
        HasReceivers,   // Has receivers
        NotStationary,  // Mobile Tech
        NoAutoSearch,   // Reserved for Attract
        NoWeapons,      // unarmed
        Fallback,       // run out of other options
    }
}
