using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using TAC_AI.AI;
using TAC_AI.AI.Enemy;
using AnimeAI;

namespace TAC_AI
{
    public static class AnimeAI
    {   //Attempt hook with the AnimeAI sub-mod
        public static void TransmitStatus(Tank tech, string message)
        {
            if (!AnimeAIBackupValidation())
                return;

        }
        public static void OnPossibleCharacterSpawn(Tank tech, AIECore.TankAIHelper help, EnemyMind mind = null)
        {
            if (!AnimeAIBackupValidation())
                return;
            LatchEnemyToSpeechSystem(tech);
        }
        public static void OnEnemyCEOSpawn(Tank tech, AIECore.TankAIHelper help, EnemyMind mind)
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
            if (!KickStart.isAnimeAIPresent)
            {
                Debug.Log("TACtical_AI: Tried to run AnimeAI without proper validation \n" + StackTraceUtility.ExtractStackTrace());
            }
            return KickStart.isAnimeAIPresent;
        }
    }
}
