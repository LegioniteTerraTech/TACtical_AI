using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TAC_AI.Templates
{
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
        GSOCorvetteGantry, //
        GSOMacrocosmGantry,// To-Do [heavy spaceship spawner]
        //-HQs
        GSOMilitaryBase,// To-Do

        // GeoCorp
        GCMiningRig,
        GCTerraBore,
        GCLoaderLauncher,
        GCProspectorHub,// To-Do [Heavy Mining drone spawner]
        GCMiningLaser,
        GCDefTurret,
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
        HEArtyFactory,
        HEXLTankFactory,
        HEAircraftGarrison,
        HEXLAircraftGarrison,
        //-HQs
        HECommandCentre,

        // Better Future
        BFGains,
        BFExtractor,
        BFDrifter,
        BFShampoo,// To-Do [medium ground Tech spawner]
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
        GCDefenseTurret,
        VENDeputyTurret,
        HEPerimeterDefense,
        HECapitalDefense,
        BFOrbitarGuard, // To-Do



        // MOBILE TECHS
        GSOTonyTeam,
        GSOHarvesterTech,
        GCHarvesterTech,
        HEHarvesterTech,
        BFHarvesterTech,
        RRHarvesterTech,

        // Hybridz - will spawn if player is at every max grade corp and they are PAIN
        AttractServo,
        // TAC lol (WILL BECOME OBSOLETE)
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

        GCDigginFlyDual,
        GCDiggersBee,
        GCHelicross,
        GCMadMiner,

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

        // space arty
        GSOSpaceArty,
        VENSpaceArty,
        HESpaceArty,
        BFSpaceArty,

        //-----------
        // COMMUNITY
        //-----------
        // Land Techs
        VENHarvester,
        Chungmus,
        TidusJ,
        U1T1M4T3P4RTYBUSSS,
        Kickball2,

        AntiBomb,
        AntiSkyScience,

        // Air Techs
        Midge,
        Damselfly,

        // Space Techs
        Vette2,

        //-Eradicators
        Invulnerable2,
        Tyrant2,

        // Bases
        GSO0GExtractor,
        GSOPowerStation,
        GSOQuickBuck,

        //---------
        // CLUSTER
        //---------
        // Note: Need to label contributions
        // Vertu and (LOOK IT UP) contributed huge batches to this
        TINYTANKS,
        LDAInitiator,
        VertuDefartillery,
        Vertusolarnode,
        AntishieldtinyTank,
        antiprotons,
        TestlaLazeRR,
        ThermalRRat,
        V_LDAdrone,
        Armageddon,
        VertuLEAD,
        DefLRMSentry,
        LDADefBattleTower,
        PetesSecretStash,
        VertuDeffort,
        TestSubjectACDC73,
        Beta,
        Cosmic,
        Electron,
        ExpRamp,
        ExplodeTheory,
        EXPloder,
        LABRover,
        Meteorminer,
        Meterordriller,
        PlasmaTester,
        PlasmaTheory,
        Proton,
        RampTank,
        ResearchRamp,
        RocketCroc,
        ScienceExplorer,
        ScrapSpider,
        ShotDog,
        SoundRover,
        SoundScience,
        SpaceMiner,
        TESTGravity,
        TESTPlasma,
        TESTSound,
        TeslaHolder,
        Thebattleprototype,
        TheCrabparatus,
        TherocketBEE,
        ZAPPer,
        EXPHover,
        QueenBee,
        ThebRRokenHover,
        Vertupyrotech,
        VertuDefnode,
        ScienceTrack,
        VertuAIscout,
        Vertubiotech,
        Antimatter,
        Antimattercarrier,
        LDALaserdrone,
        Defbattletower,
        Defdisruptersentry,
        DefHeavyLaserSentry,
        DefSentryGun,
        DefSentryGunB,
        LDAcannontower,
        LDALightLaserHtank,
        MiniPebbles,
        Monolith,
        V_LDADefHMT,
        VertuBattleDrone,
        Vertucapitaltech,
        VertuDEFcannon,
        VertuENCV2,
        Vertumobilebastion,
        BigShotR,
        Geckette,
        LDABrawlerTank,
        LDAKothTankV2,
        LDAMissiledrone,
        LDATank,
        SY7_Staffer,
        V_LDAAITank,
        V_LDAbasiancedtank,
        V_LDAcommandtank,
        V_LDADroneTank,
        V_LDALightTank,
        V_LDAScouttank,
        Vertuangrytech,
        VertuAssaulttank,
        VertuenforcerMk2,
        VertuTerrorTech,
        Worker,
        Bunker,
        V_LDAbattletank,
        VertuassaultT1,
        Vertubehemothtank,
        VertuhardytankV2,
        Vertupaintrain,
        Vertureactortank,
        Vertusaigetech,
        Vertustormtank,
        VertuTitan,
        Vertusdoomtrain,
        Maikro,
        Scythe,
        Dinghy,
        Intrepid,
        LDAMicroBattleship,
        Piranha,
        Spyder,
        VertuexperimentalO,
        Vertulasercrusier,
        Brawler,
        DCZ5ScorpionMK2R,
        MaulV4,
        Stingray,
        TX98ShockwaveMk2R,
        V_LDAACSBSToaster,
        V_LDABattleCruiser,
        V_LDABattletech,
        V_LDABSIronWill,
        V_LDAEscortcruiser,
        V_LDAHeavycruiser,
        V_LDAHeavyTank,
        V_LDATeslatank,
        V_LDRhovertank,
        VertuAtmoBattleship,
        VertuLaserBattleship,
        Hatchet,
        V_LDADefPWatcher,
        VertuDefPOverwatch,
        VertumissileDEFP,
        MecanumSpider,
        Prototypedrone,
        Crescent,
        V_LDAResponcedrone,
        DiolysisC6WyvernR,
        Hornet,
        Hummingbird,
        StarN422281337,
        PreludeMP,
        V_LDAAssaultdrone,



        /*
        DefBattleTower,
        DefDisrupterSentry,
        DefHeavyLaserSentry,
        DefLRMSentry,
        DefSentryGun,
        DefSentryGunB,
        Hatchet,
        ComCannonTower,
        ComDefBattleTower,
        Monolith,
        PetesSecretStash,
        TestSubjectACDC73,
        ComDefHMT,
        ComDefPWatcher,
        ComBattleDrone,
        ComCapitalTech,
        ComDefArtillery,
        ComDefCannon,
        ComDefFort,
        ComDefNode,
        ComDefPOverwatch,
        ComENCV2,
        ComMissileDEFP,
        ComMobileBastion,
        ComSolarNode,
        AntiShieldTinyTank,
        Beta,
        Cosmic,
        Electron,
        ExpRamp,
        ExplodeTheory,
        EXPloder,
        Geckette,
        LABRover,
        ComBrawlerTank,
        ComKothTankV2,
        ComMissileDrone,
        MecanumSpider,
        MeteorMiner,
        MeteorDriller,
        PlasmaTester,
        PlasmaTheory,
        Proton,
        Prototypedrone,
        RampTank,
        ResearchRamp,
        RocketCroc,
        ScienceExplorer,
        ScienceTrack,
        ScrapSpider,
        ShotDog,
        SoundRover,
        SoundScience,
        SpaceMiner,
        TESTGravity,
        TESTPlasma,
        TESTSound,
        TeslaHolder,
        TestlaLazeRR,
        TheBattlePrototype,
        TheCrabparatus,
        TheRocketBEE,
        ThermalRRat,
        TINYTANKS,
        ComAITank,
        ComBasiancedTank,
        ComBattleTank,
        ComCommandtank,
        ComDrone,
        ComDroneTank,
        ComLightTank,
        ComScouttank,
        ComAIScout,
        ComAngryTech,
        ComAssaultT1,
        ComAssaultTank,
        ComEnforcerMk2,
        ComHardyTankV2,
        ComPainTrain,
        ComPyroTech,
        ComReactorTank,
        ComSiegeTech,
        ComStormTank,
        ComTerrorTech,
        ComTitan,
        ComDoomTrain,
        Worker,
        ZAPPer,
        Crescent,
        ComLaserDrone,
        Maikro,
        Scythe,
        ComResponseDrone,
        Armageddon,
        Brawler,
        Dinghy,
        EXPHover,
        Hornet,
        Hummingbird,
        ComMicroBattleship,
        Piranha,
        PreludeMP,
        QueenBee,
        Spyder,
        StarN422281337,
        Stingray,
        ThebRRokenHover,
        ComAssaultdrone,
        ComBattleCruiser,
        ComBSIronWill,
        ComEscortCruiser,
        ComHeavyCruiser,
        ComHeavyTank,
        ComTeslaTank,
        ComHoverTank,
        ComAtmoBattleship,
        ComExperimentalO,
        ComLaserCruiser,
        ComLEAD,
        ComLaserBattleship,
        */
    }

}
