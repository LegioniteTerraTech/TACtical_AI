using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TAC_AI
{
    public static class TrackedVisibleExtensions
    {
        public static bool TryGetStored(this TrackedVisible TV, bool checkJSONTiles, out ManSaveGame.StoredVisible vis)
        {
            if (TV == null)
            {
                WorldPosition WP = TV.GetWorldPosition();
                vis = AIGlobals.FindStoredTech(TV.ID, WP.TileCoord, checkJSONTiles);
                return vis != null;
            }
            vis = null;
            return false;
        }
    }
}
