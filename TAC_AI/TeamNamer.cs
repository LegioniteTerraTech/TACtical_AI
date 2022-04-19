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
        public static StringBuilder GetTeamName(int Team)
        {
#if !STEAM
            if (KickStart.isAnimeAIPresent)
                return AnimeAI.Dialect.ManDialogDetail.TeamName(Team);
#endif
            StringBuilder build = new StringBuilder();
            int teamNameDetermine = Team;
            if (teamNameDetermine == ManSpawn.FirstEnemyTeam || teamNameDetermine == ManSpawn.NewEnemyTeam)
            {
                build.Append("Lone Prospector");
                return build;
            }
            else if (teamNameDetermine == ManPlayer.inst.PlayerTeam)
            {
                build.Append("Player Team");
                return build;
            }
            else if (teamNameDetermine == ManSpawn.NeutralTeam)
            {
                build.Append("Services Group");
                return build;
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

            return build;
        }
        public static StringBuilder EnemyTeamName(EnemyMind mind)
        {
#if !STEAM
            if (KickStart.isAnimeAIPresent)
                return AnimeAI.Dialect.ManDialogDetail.EnemyTeamName(mind);
#endif
            StringBuilder build = new StringBuilder();
            int teamNameDetermine = mind.AIControl.tank.Team;
            if (teamNameDetermine == -1)
            {
                build.Append("Lone Prospector");
                return build;
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

            return build;
        }



        // team name generator
        private static List<string> Adjective
        {
            get
            {
                return new List<string>
                {   // must be even
            {   "Furious"
            },{ "Crimson"
            },{ "Grand"
            },{ "Scarlet"
            },{ "Unrelenting"
            },{ "Philosphical"
            },{ "Conflictive"
            },{ "Overseeing"
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
            {   "Slayers"
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
            {   "Agents of"
            },{ "Foes of"
            },{ "People of"
            },{ "Wanderers of"
            },{ "The Gathering of"
            },{ "Followers of"
            },{ "Laberors of"
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
            },
        };
            }
        }

    }
}
