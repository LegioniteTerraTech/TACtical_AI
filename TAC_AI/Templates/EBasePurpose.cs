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
        AnyNonSea,
        Land,   // is anchored
        Sea,    // floats on water
        Air,    // doubles as airplane
        Chopper,// relies on props to stay airborne
        Space   // mobile base that flies beyond
    }
    public enum BasePurpose
    {
        AnyNonHQ,
        Defense,
        Harvesting,
        TechProduction,
        Headquarters,
        NotABase,
        NoAutoSearch,
        NoWeapons,
    }
}
