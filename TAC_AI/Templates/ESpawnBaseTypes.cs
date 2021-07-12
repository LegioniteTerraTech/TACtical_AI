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
        // The Bases
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

        // The Defenses
        GSOTowerDefense,
        GSOMegaDefense,
        GCMiningLaserLite,
        VENDeputyTurret,
        HEPerimeterDefense,
        HEHeavyDefense,



        // MOBILE TECHS
        // Hybridz
        AttractServo,
        // TAC lol
        TACInvaderAttract,
        TACInvaderAttract2,
        TACOutpostMissions,
        TACSentinelTurret,

        // World Gen (emergency)
        // Aircraft - from slow to fast sorted by corp
        GSOEpicTony,
        GSOAirdropSquad,
        GSOFightOrFlight,
        VENZephr,
        VENMachinator,
        HEDropship,
        HEBombsAway,
        HECyclone,
        // Choppers
        GSOQuadGale,
        HEPocketApache,
        BFeClipse,
        // Naval
        GSOShallowWaterGuard,
        GCBuoyMiner,
        VENNautilus,
        HESwaddleBoat,
        BFLuxYacht,
        // Space
        GSOSpaceshipCompact
    }

}
