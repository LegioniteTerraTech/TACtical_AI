using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Sub_Missions
{
    internal class TerrainOperations
    {
        internal const float RescaleFactor = 4;
        internal static void AmplifyTerrain(Terrain Terra)
        {
            Debug_SMissions.Info("SubMissions: Amplifying Terrain....");
            TerrainData TD = Terra.terrainData;
            TD.size = new Vector3(TD.size.x, TD.size.y * RescaleFactor, TD.size.z);
            float[,] floats = TD.GetHeights(0, 0, 129, 129);
            for (int stepX = 0; stepX < 129; stepX++)
                for (int stepY = 0; stepY < 129; stepY++)
                    floats.SetValue(floats[stepX,stepY] / RescaleFactor, stepX, stepY);
            TD.SetHeights(0, 0, floats);
            Terra.terrainData = TD;
            Terra.Flush();
            Debug_SMissions.Info("SubMissions: Amplifying Terrain complete!");
        }

        internal static void LevelTerrain(WorldTile WT)
        {
            Debug_SMissions.Log("SubMissions: Leveling terrain....");
            TerrainData TD = WT.Terrain.terrainData;
            TD.size = new Vector3(TD.size.x, TD.size.y * RescaleFactor, TD.size.z);
            float[,] floats = TD.GetHeights(0, 0, 129, 129);
            double totalheight = 0;
            foreach (float flo in floats)
                totalheight += flo;
            totalheight /= floats.Length;
            float th = (float)totalheight;
            for (int stepX = 1; stepX < 129; stepX++)
                for (int stepY = 1; stepY < 129; stepY++)
                    floats.SetValue(th, stepX, stepY);
            TD.SetHeights(0, 0, floats);
            WT.Terrain.terrainData = TD;
            WT.Terrain.Flush();
            Debug_SMissions.Log("SubMissions: Leveling terrain complete!");
        }
    }
}
