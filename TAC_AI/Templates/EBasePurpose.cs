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
        Land,
        Sea,
        Air,
        Space
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
    }
}
