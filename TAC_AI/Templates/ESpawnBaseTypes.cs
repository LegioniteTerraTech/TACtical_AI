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

        // fallbacks
        GSO0Base,
        GCTotum,
        VENPipperoni,
        HERelay,
        BFCyberFlote,
        RRSideswipe,


        // The Bases
        // GSO
        GSOSeller,
        GSOMidBase,
        GSOMinerCluster,
        GSOAIMinerProduction,// To-Do [Mining drone spawner]
        GSOTechFactory,
        GSOHVYTankFactory,
        GSOAirbase,// To-Do [aircraft spawner]
        GSOStarport,// To-Do [Starfighter spawner]
        GSOMacrocosmGantry,// To-Do [heavy spaceship spawner]
        //-HQs
        GSOMilitaryBase,// To-Do

        // GeoCorp
        GCMiningRig,
        GCTerraBore,
        GCProspectorHub,// To-Do [Heavy Mining drone spawner]
        GCMiningLaser,// To-Do
        //-HQs
        GCHeadquarters,

        // Venture
        VENTuningShop,
        VENGasStation,
        VENPitStop,
        VENRallyHost,
        VENAviationCenter,// To-Do [Multi-Aircraft spawner]
        VENGasSilo,// To-Do
        //-HQs
        VENSpeedac,

        // Hawkeye
        HECombatStation,
        HEOilDerrick,
        HEComsat, // To-Do [sends allies to attack really far]
        HEMunitionsDepot,// To-Do [Bike factory]
        HETankFactory,// To-Do [Light tank spawner]
        HEAAFactory, // To-Do [AA spawner]
        HEXLTankFactory,
        HEAircraftGarrison,
        //-HQs
        HECommandCentre,

        // Better Future
        BFGains,
        BFExtractor,
        BFShampoo,// To-Do [small ground Tech spawner]
        BFHoverFactory,
        BFSporkFabricator,// To-Do [medium spaceship spawner]
        //-HQs
        BFNoleusAutoworks,

        // Reticule Research
        RRDecanter,
        RRSampler,
        RRFactory,// To-Do - RR Light tank spawner
        RRHeavyFactory,
        RRScienceVessel,// To-Do - RR spaceship spawner


        // The Defenses
        GSOLightDefense,
        GSOTowerDefense,
        GSOMegaDefense,
        GCMiningLaserLite,
        VENDeputyTurret,
        HEPerimeterDefense,
        HECapitalDefense,
        BFOrbitarGuard, // To-Do



        // MOBILE TECHS
        GSOTonyTeam,
        GSOHarvesterTech,
        GCHarvesterTech,
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
        GSOHammer,   // GSO Missile aircraft
        GSOFightOrFlight,   // Heavy GSO does-all

        VENLiftOff,         // Unarmed, early
        VENPuffPlane,       // Small Venture interceptor
        VENDraftPlane,      // Medum Venture interceptor
        VENZephrPlane,      // Heavy interceptor [missiles]
        VENHotWings,        // Racer dropship
        VENMachinator,      // gg toofast, tons of missiles

        HEPeanutJet,        // Intro cutscene enemy, has cruise missiles very early
        HEProx,             // Little fighter
        HEVengence,         // Railgun fighter
        HEDropship,         // Wheeled Tank Dropship (Alien ref)
        HEBombsAway,        // Anti-base bomber
        HECyclone,          // Very fast and dangerous HE aircraft armed to the bone

        BFEchoAir,          // Small laser interceptor
        BFColossus,         // Large BF hover dropship
        BFOblivion,         // lotta beam lasors

        // Choppers
        GSOQuadGale,
        GSOQuadGaleM,
        HESheertail,
        HEPocketApache,
        HESmashApache,
        BFeClipse,

        // Naval
        GSOShallowWaterGuard,
        GCPlasmaDredge,
        GCYukonCharleyDredge,
        VENScurry,
        VENNautilus,
        HESwaddleBoat,
        HEDreadnaught,
        HEBattleSwivels,
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
        GSOSpaceship,
        HEClatoe,
        HECroc,
        BFTrident,

        //-----------
        // COMMUNITY
        //-----------
        // Land Techs
        VENHarvester,
        Chungmus,
        TidusJ,
        U1T1M4T3P4RTYBUSSS,
        Kickball2,

        // Space Techs
        Vette2,

        //-Eradicators
        Invulnerable2,
        Tyrant2,

        // Bases
        GSO0GExtractor,
        GSOPowerStation,
        GSOQuickBuck,
    }

}
