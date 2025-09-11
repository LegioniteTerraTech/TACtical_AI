﻿
namespace TAC_AI.AI {
    public enum AIType
    {   //like the old plans, we make the AI do stuff
        Null = -1,

        // COMBAT
        Escort,     // Good ol' player defender                     (Classic player defense numbnut)
        Assault,    // Run off and attack the enemies on your radar (Runs off (beyond radar range!) to attack enemies)
        Aegis,      // Protects the nearest non-player allied Tech  (Follows nearest ally, will chase enemy some distance)

        // RESOURCES
        Prospector, // Harvest Chunks and return them to base       (Returns chunks when full to nearest receiver)
        Scrapper,   // Grab loose blocks but avoid combat           (Return to nearest base when threatened)
        Energizer,  // Charges up and/or heals other techs          (Return to nearest base when out of power)

        // MISC        (MultiTech) - BuildBeam disabled, will fire at any angle.
        MTTurret,   // Only turns to aim at enemy                   
        MTStatic,    // Does not move on own but does shoot back     
        MTMimic,    // Copies the actions of the closest non-MT Tech in relation     

        // LEGACY - These now have their own Enum now!
        // ADVANCED    (REQUIRES TOUGHER ENEMIES TO USE!)           (can't just do the same without the enemies attacking these ways as well...)
        Aviator,    // Flies aircraft, death from above, nuff said  (Flies above ground, by the player and keeps distance) [unload distance will break!]
        Buccaneer,  // Sails ships amongst ye seas~                 (Avoids terrain above water level)
        Astrotech,  // Flies hoverships and kicks Tech              (Follows player a certain distance above ground level and can follow into the sky)
    }
    //All of their operating ranges are ultimately determined by the Tech's biggest provided vision/radar range.

    public enum AIDriverType
    {   //like the old plans, we make the AI do stuff
        Null = -1,
        AutoSet,      // Requested auto-set

        // COMBAT
        Tank,       // Classic wheeled
        Pilot,      // Flies aircraft, death from above, nuff said  (Flies above ground, by the player and keeps distance) [unload distance will break!]
        Sailor,     // Sails ships amongst ye seas~                 (Avoids terrain above water level)
        Astronaut,  // Flies hoverships and kicks Tech              (Follows player a certain distance above ground level and can follow into the sky)

        // STATIC (bases) - forces AI to stay in "Guard"
        Stationary,
    }
}
