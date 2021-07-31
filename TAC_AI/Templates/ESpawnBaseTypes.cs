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
        GCTerraBore,
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
        HECapitalDefense,



        // MOBILE TECHS
        GSOTonyTeam,
        // Hybridz - will spawn if player is at every max grade corp and they are PAIN
        AttractServo,
        // TAC lol
        TACInvaderAttract,
        TACInvaderAttract2,
        TACOutpostMissions,
        TACSentinelTurret,

        // World Gen (emergency)
        // Aircraft - from slow to fast sorted by corp
        GSOLiftOff,         // Unarmed, early
        GSOEpicTony,        // lotta lasers
        GSOAirdropSquad,    // GSO Dropship
        GSOFightOrFlight,   // Heavy GSO does-all

        VENLiftOff,         // Unarmed, early
        VENPuffPlane,       // Small Venture interceptor
        VENDraftPlane,      // Medum Venture interceptor
        VENZephrPlane,      // Heavy interceptor [missiles]
        VENHotWings,        // Racer dropship
        VENMachinator,      // gg toofast, tons of missiles

        HEPeanutJet,        // Intro cutscene enemy, has cruise missiles very early
        HEDropship,         // Wheeled Tank Dropship (Alien ref)
        HEBombsAway,        // Anti-base bomber
        HECyclone,          // Very fast and dangerous HE aircraft armed to the bone

        BFEchoAir,          // Small laser interceptor
        BFColossus,         // Large BF hover dropship
        BFOblivion,         // lotta beam lasors

        // Choppers
        GSOQuadGale,
        HESheertail,
        HEPocketApache,
        HESmashApache,
        BFeClipse,

        // Naval
        GSOShallowWaterGuard,
        GCPlasmaDredge,
        GCYukonCharleyDredge,
        VENNautilus,
        HESwaddleBoat,
        BFLuxYacht,

        //Player-helping naval crafts
        FTUEGSOGrade1Bote,
        FTUEGSOGrade2Bote,
        FTUEGSOGrade3Bote,
        FTUEGSOGrade4Bote,
        FTUEGSOGrade5Bote,
        FTUEGSOGrade1BoteFallBack,
        FTUEGSOGrade2BoteFallBack,
        FTUEGSOGrade3BoteFallBack,
        FTUEGSOGrade4BoteFallBack,
        FTUEGSOGrade5BoteFallBack,

        // Space
        GSOSpaceshipMini,
        GSOSpaceshipCompact,
    }

}
