using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using TAC_AI.AI;
using TAC_AI.AI.Enemy;

// Anime AI support is on hold until further notice
#if !STEAM
using AnimeAI;

namespace TAC_AI
{
    public enum AOffenseReact
    {   //Attempt hook with the AnimeAI sub-mod 
        Hurt,       // Taken damage
        Damaged,    // Block chipped off
        Destroyed   // Tech destroyed
    }
    public enum ALossReact
    {   //Attempt hook with the AnimeAI sub-mod 
        Land,     
        Sea,
        Air,
        Space,   
        Base,   
    }
    public static class AnimeAICompat
    {   //Attempt hook with the AnimeAI sub-mod
        //  WIP
        const bool UseErrorChecking = false;

        //private List<Tank> techsWithCharacters = new List<Tank>();

        public static bool PollShouldRetreat(Tank tech, TankAIHelper help, out bool verdict)
        {
            verdict = false;
            if (!AnimeAIBackupValidation())
                return false;

            if (Core.GetDriverOfTech(tech, out CharacterInst CI))
            {
                if (help.lastEnemy?.tank)
                    verdict = CI.ShouldRetreat(help.lastEnemy.tank);
                else
                    verdict = false;
                return true;
            }
            return false;
        }

        public static void RespondToOffense(Tank tech, Tank assailant, AOffenseReact damage)
        {
            if (!AnimeAIBackupValidation())
                return;

        }
        public static void RespondToLoss(Tank tech, ALossReact damage)
        {
            if (!AnimeAIBackupValidation())
                return;

        }

        public static void TransmitStatus(Tank tech, string message)
        {
            if (!AnimeAIBackupValidation())
                return;

        }
        public static void OnPossibleCharacterSpawn(Tank tech, TankAIHelper help, EnemyMind mind = null)
        {
            if (!AnimeAIBackupValidation())
                return;
            LatchEnemyToSpeechSystem(tech);
        }
        public static void OnEnemyCEOSpawn(Tank tech, TankAIHelper help, EnemyMind mind)
        {
            if (!AnimeAIBackupValidation())
                return;
            LatchEnemyCEOToSpeechSystem(tech);
        }

        private static void LatchEnemyToSpeechSystem(Tank tech)
        {
            //ManCharacterFetcher.che
        }
        private static void LatchEnemyCEOToSpeechSystem(Tank tech)
        {

        }


        private static bool AnimeAIBackupValidation()
        {
            if (UseErrorChecking && !KickStart.isAnimeAIPresent)
            {
                DebugTAC_AI.Log(KickStart.ModID + ": Tried to run AnimeAI \n" + StackTraceUtility.ExtractStackTrace());
            }
            return KickStart.isAnimeAIPresent;
        }
    }
}
#endif
