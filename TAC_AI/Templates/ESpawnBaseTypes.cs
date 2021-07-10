using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TAC_AI.Templates
{
    class ESpawnBaseTypes
    {
    }
    public enum SpawnBaseTypes
    {
        // STATIONARY TECHS
        // error
        NotAvail,
        // GSO
        GSOSeller,
        GSOMidBase,
        GSOMilitaryBase,
        GSOAIMinerProduction,
        GSOTechFactory,
        GSOStarport,
        // GeoCorp
        GCMiningRig,
        GCProspectorHub,
        GCHeadquarters,
        GCMiningLaser,
        // Venture
        VENRallyHost,
        VENGasStation,
        VENTuningShop,
        VENGasSilo,
        // Hawkeye
        HECommandCentre,
        HECombatStation,
        HEComsat,
        HETankFactory,
        HEMunitionsDepot,
        HEAircraftGarrison,

        // MOBILE TECHS
        // Hybridz
        AttractServo,
        // TAC lol
        TACInvaderAttract,
        TACInvaderAttract2,
    }

}
