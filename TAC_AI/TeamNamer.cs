using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TAC_AI.AI.Enemy;

namespace TAC_AI
{
    public static class TeamNamer
    {

        private static StringBuilder build = new StringBuilder();
        public static string GetTeamName(int Team)
        {
            //if (KickStart.isAnimeAIPresent)
            //    return AnimeAI.Dialect.ManDialogDetail.TeamName(Team);
            build.Clear();
            int teamNameDetermine = Team;
            if (teamNameDetermine == ManSpawn.FirstEnemyTeam || teamNameDetermine == ManSpawn.NewEnemyTeam)
            {
                build.Append("Lone Prospector");
                return build.ToString();
            }
            else if (teamNameDetermine == ManPlayer.inst.PlayerTeam)
            {
                build.Append("Player Team");
                return build.ToString();
            }
            else if (teamNameDetermine == ManSpawn.NeutralTeam)
            {
                build.Append("Services Group");
                return build.ToString();
            }
            else if (teamNameDetermine < 1075)
            {
                build.Append(Adjective.ElementAt((int)Mathf.Repeat((int)(teamNameDetermine + 0.5f), Adjective.Count)));
                build.Append(" ");
                build.Append(Noun.ElementAt((int)Mathf.Repeat((int)(teamNameDetermine + 0.5f), Noun.Count)));
            }
            else
            {
                build.Append(AdjectiveAlt.ElementAt((int)Mathf.Repeat((int)(teamNameDetermine + 0.5f), AdjectiveAlt.Count)));
                build.Append(" ");
                build.Append(NounAlt.ElementAt((int)Mathf.Repeat((int)(teamNameDetermine + 0.5f), NounAlt.Count)));
            }

            return build.ToString();
        }
        public static string EnemyTeamName(EnemyMind mind)
        {
            //if (KickStart.isAnimeAIPresent)
            //    return AnimeAI.Dialect.ManDialogDetail.EnemyTeamName(mind);

            build.Clear();
            int teamNameDetermine = mind.AIControl.tank.Team;
            if (teamNameDetermine == -1)
            {
                build.Append("Lone Prospector");
                return build.ToString();
            }
            else if (teamNameDetermine < 1075)
            {
                build.Append(Adjective.ElementAt((int)Mathf.Repeat((int)(teamNameDetermine + 0.5f), Adjective.Count)));
                build.Append(" ");
                build.Append(Noun.ElementAt((int)Mathf.Repeat((int)(teamNameDetermine + 0.5f), Noun.Count)));
            }
            else
            {
                build.Append(AdjectiveAlt.ElementAt((int)Mathf.Repeat((int)(teamNameDetermine + 0.5f), AdjectiveAlt.Count)));
                build.Append(" ");
                build.Append(NounAlt.ElementAt((int)Mathf.Repeat((int)(teamNameDetermine + 0.5f), NounAlt.Count)));
            }

            return build.ToString();
        }



        // team name generator
        private static List<string> Adjective
        {
            get
            {
                return new List<string>
                {   // must be even
                    /* // LEGACY
            {   "Furious"
            },{ "Crimson"
            },{ "Grand"
            },{ "Scarlet"
            },{ "Unrelenting"
            },{ "Philosphical"
            },{ "Conflictive"
            },{ "Overseeing"
            },*/
            {   "Old"
            },{ "Group"
            },{ "Grand"
            },{ "Core"
            },{ "GEO"
            },{ "Rock"
            },{ "Power"
            },{ "Turbo"
            },{ "Gold"
            },{ "Big Tony"
            },
                };
            }
        }
        private static List<string> Noun
        {
            get
            {
                return new List<string>
        {   // must be odd
                    /*// LEGACY
            {   "Prospectors"
            },{ "Shield"
            },{ "Halberd"
            },{ "Enclave"
            },{ "Avengers"
            },{ "Ravagers"
            },{ "Maulers"
            },{ "Infidor"
            },{ "Off-World"
            },{ "Cubes"
            },{ "Cult"
            },{ "Gourd"
            },{ "Griters"
            },{ "Beings"
            },{ "Entities"
            },*/
            {   "Prospectors"
            },{ "Organization"
            },{ "Chunks"
            },{ "Expedition"
            },{ "Moles"
            },{ "Spade"
            },{ "Rushers"
            },{ "Maulers"
            },{ "Crushers"
            },{ "Off-World"
            },{ "Cubes"
            },{ "Blocks"
            },{ "Drill"
            },{ "Grinders"
            },{ "Operations"
            },
        };
            }
        }
        private static List<string> AdjectiveAlt
        {
            get
            {
                return new List<string>
        {   // must be even
                    /* // LEGACY
            {   "Agents of"
            },{ "Foes of"
            },{ "People of"
            },{ "Wanderers of"
            },{ "The Gathering of"
            },{ "Followers of"
            },{ "Laberors of"
            },{ "Techs of"
            },*/
            {   "Miners of"
            },{ "Diggers of"
            },{ "Robots of"
            },{ "Explorers of"
            },{ "Masters of"
            },{ "Followers of"
            },{ "Scientests of"
            },{ "Techs of"
            },
        };
            }
        }
        private static List<string> NounAlt
        {
            get
            {
                return new List<string>
        {   // must be odd
                    /*// LEGACY
            {   "The Shield"
            },{ "The Halberd"
            },{ "The Earth Federation Forces"
            },{ "The Enclave"
            },{ "The Avengers"
            },{ "The Infidor"
            },{ "The Off-World"
            },{ "The Cube"
            },{ "The Gourd"
            },{ "The Grit"
            },{ "The Planet"
            },*/
            {   "Sacred Rock"
            },{ "Chrome"
            },{ "Rubble"
            },{ "Ore"
            },{ "F*bron"
            },{ "Rough Rubber"
            },{ "Plumbite"
            },{ "Titania"
            },{ "Erudite"
            },{ "Rodius"
            },{ "Luxite"
            },{ "Oleius"
            },{ "Ignite"
            },{ "Celestite"
            },{ "Carbite"
            },
        };
            }
        }

    }
}
