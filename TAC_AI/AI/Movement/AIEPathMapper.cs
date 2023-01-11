using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TAC_AI.AI.Movement
{
    /// <summary>
    /// Evaluates world tiles (in smaller tiles) to figure out how path-findable they are.
    ///   Hosted in AIECore.TankAIManager.
    /// </summary>
    public class AIEPathMapper : MonoBehaviour
    {
        internal static AIEPathMapper inst;
        public const int chunksPerTileWH = 64;
        public const int AutoPathersToCalcPerFrame = 2;
        /// <summary>
        /// TerrainHeightVarianceMaxDifference
        /// </summary>
        public const float THVMD = 250;
        public const byte maxAltByte = 128;
        private static float Delta = 1f / chunksPerTileWH;
        private static float EvalRad = 1.76f * Delta * ManWorld.inst.TileSize;
        private static bool sub = false;

        private static readonly Dictionary<IntVector2, AIPathTileCached> tilesMapped = new Dictionary<IntVector2, AIPathTileCached>();

        public void Update()
        {
            AutoPathUpdate();
            HandlePathingRequests();
        }


        private static int autoPathStep = 0;
        internal static readonly List<IPathfindable> autoPathers = new List<IPathfindable>();
        private void AutoPathUpdate()
        {
            int frameCalcs = Mathf.Max(0, AutoPathersToCalcPerFrame - autoPathers.Count);
            while (frameCalcs < AutoPathersToCalcPerFrame)
            {
                if (autoPathStep >= autoPathers.Count)
                    autoPathStep = 0;
                IPathfindable pather = autoPathers[autoPathStep];
                if (pather != null)
                    pather.StartPathfind();
                else
                    autoPathers.RemoveAt(autoPathStep);

                autoPathStep++;
                frameCalcs++;
            }
        }


        /// <summary>
        /// Destination, Pathers
        /// </summary>
        internal static readonly Dictionary<IntVector2, List<AIEAutoPather2D>> pathRequests = new Dictionary<IntVector2, List<AIEAutoPather2D>>();
        private void HandlePathingRequests()
        {
            for (int step = 0; step < pathRequests.Count; step++)
            {
                var pair = pathRequests.ElementAt(step);
                List<AIEAutoPather2D> paths = pair.Value;
                for (int step2 = 0; step2 < paths.Count; step2++)
                {
                    var path = paths[step2];
                    if (!path.CalcRoute())
                        paths.RemoveAt(step2);
                }
                if (paths.Count == 0)
                {
                    pathRequests.Remove(pair.Key);
                }
            }
        }
        private void ImmedeatelyPathAll()
        {
            for (int step = 0; step < pathRequests.Count;)
            {
                var pair = pathRequests.ElementAt(step);
                List<AIEAutoPather2D> paths = pair.Value;
                for (int step2 = 0; step2 < paths.Count;)
                {
                    var path = paths[step2];
                    if (!path.CalcRoute())
                        paths.RemoveAt(step2);
                }
                if (paths.Count == 0)
                {
                    pathRequests.Remove(pair.Key);
                }
            }
        }

        public static void RegisterPather(IntVector2 dest, AIEAutoPather2D pather)
        {
            if (pathRequests.TryGetValue(dest, out List<AIEAutoPather2D> paths))
            {
                paths.Add(pather);
            }
            else
            {
                paths = new List<AIEAutoPather2D>();
                paths.Add(pather);
                pathRequests.Add(dest, paths);
            }
        }
        public static void RegisterTile(WorldTile tile)
        {
            if (!sub)
            {
                sub = true;
                inst = new GameObject("PathMapper").AddComponent<AIEPathMapper>();
                ManWorld.inst.TileManager.TileDestroyedEvent.Subscribe(UnregisterTile);
            }
            if (tilesMapped.TryGetValue(tile.Coord, out _))
                throw new Exception("AIPathMapper(RegisterTile) - Tried to add a WorldTile that is already present");
            DebugTAC_AI.Log("AIEPatchMapper: Registered tile " + tile.Coord.ToString());
            tilesMapped.Add(tile.Coord, new AIPathTileCached(tile));
        }
        public static void UnregisterTile(WorldTile tile)
        {
            if (tile == null)
                return;
            IntVector2 coord = tile.Coord;
            if (tilesMapped.TryGetValue(coord, out _))
                tilesMapped.Remove(coord);
        }
        public static void ResetAll()
        {
            tilesMapped.Clear();
        }

        public static byte GetDifficulty(Vector3 scenePos, AIEAutoPather2D pather)
        {
            IntVector2 coord = WorldPosition.FromScenePosition(scenePos).TileCoord;
            if (tilesMapped.TryGetValue(coord, out AIPathTileCached tile))
            {
                return pather.GetDiff(tile.GetAltitude(scenePos - ManWorld.inst.TileManager.CalcTileOriginScene(coord)));
            }
            else
            {
                var wTile = ManWorld.inst.TileManager.LookupTile(scenePos, false);
                if (wTile == null)
                    return maxAltByte;
                RegisterTile(wTile);
                if (tilesMapped.TryGetValue(coord, out AIPathTileCached tile2))
                    return pather.GetDiff(tile2.GetAltitude(scenePos - ManWorld.inst.TileManager.CalcTileOriginScene(coord)));
                throw new Exception("AIPathMapper(GetDifficulty) - Could not register a AIPathTile properly.  \nAll AIPathMapper operations will cascade fail!");
            }
        }

        public static byte GetDifficultyWater(Vector3 scenePos, AIEAutoPather2D pather)
        {
            IntVector2 coord = WorldPosition.FromScenePosition(scenePos).TileCoord;
            if (tilesMapped.TryGetValue(coord, out AIPathTileCached tile))
            {
                if (tile.IsWater(scenePos - ManWorld.inst.TileManager.CalcTileOriginScene(coord)))
                    return maxAltByte;
                return pather.GetDiff(tile.GetAltitude(scenePos - ManWorld.inst.TileManager.CalcTileOriginScene(coord)));
            }
            else
            {
                var wTile = ManWorld.inst.TileManager.LookupTile(scenePos, false);
                if (wTile == null)
                    return maxAltByte;
                RegisterTile(wTile);
                if (tilesMapped.TryGetValue(coord, out AIPathTileCached tile2))
                    return pather.GetDiff(tile2.GetAltitude(scenePos - ManWorld.inst.TileManager.CalcTileOriginScene(coord)));
                throw new Exception("AIPathMapper(GetDifficulty) - Could not register a AIPathTile properly.  \nAll AIPathMapper operations will cascade fail!");
            }
        }
        public static byte GetDifficultyWaterInv(Vector3 scenePos, AIEAutoPather2D pather)
        {
            IntVector2 coord = WorldPosition.FromScenePosition(scenePos).TileCoord;
            if (tilesMapped.TryGetValue(coord, out AIPathTileCached tile))
            {
                if (!tile.IsWater(scenePos - ManWorld.inst.TileManager.CalcTileOriginScene(coord)))
                    return maxAltByte;
                return pather.GetDiff(tile.GetAltitude(scenePos - ManWorld.inst.TileManager.CalcTileOriginScene(coord)));
            }
            else
            {
                var wTile = ManWorld.inst.TileManager.LookupTile(scenePos, false);
                if (wTile == null)
                    return maxAltByte;
                RegisterTile(wTile);
                if (tilesMapped.TryGetValue(coord, out AIPathTileCached tile2))
                    return pather.GetDiff(tile2.GetAltitude(scenePos - ManWorld.inst.TileManager.CalcTileOriginScene(coord)));
                throw new Exception("AIPathMapper(GetDifficulty) - Could not register a AIPathTile properly.  \nAll AIPathMapper operations will cascade fail!");
            }
        }

        public static bool IsWaterlogged(Vector3 scenePos)
        {
            IntVector2 coord = WorldPosition.FromScenePosition(scenePos).TileCoord;
            if (tilesMapped.TryGetValue(coord, out AIPathTileCached tile))
            {
                return tile.IsWater(scenePos - ManWorld.inst.TileManager.CalcTileOriginScene(coord));
            }
            else
            {
                var wTile = ManWorld.inst.TileManager.LookupTile(scenePos, false);
                if (wTile == null)
                    return false;
                RegisterTile(wTile);
                if (tilesMapped.TryGetValue(coord, out AIPathTileCached tile2))
                    return tile2.IsWater(scenePos - ManWorld.inst.TileManager.CalcTileOriginScene(coord));
                throw new Exception("AIPathMapper(IsWater) - Could not register a AIPathTile properly.  \nAll AIPathMapper operations will cascade fail!");
            }
        }

        private class AIPathTileCached
        {
            private readonly WorldTile tile;
            private readonly byte[] chunkBytes;

            internal AIPathTileCached(WorldTile tile)
            {
                if (tile == null)
                    throw new Exception("new AIPathTile() - The WorldTile given was null!?!  \nAll AIPathMapper operations will cascade fail!");
                chunkBytes = new byte[chunksPerTileWH * chunksPerTileWH];
                this.tile = tile;
            }

            internal bool IsWater(Vector3 inTile)
            {
                return (EvalChunk(inTile) & 1) == 1;
            }
            internal byte GetAltitude(Vector3 inTile)
            {
                return (byte)(EvalChunk(inTile) >> 1);
            }

            private static byte GetChunkByte(byte diff, bool isWater)
            {
                return (byte)((diff << 1) | (isWater ? 1 : 0));
            }

            private static float EvalTerrainDelta(Vector3 scenePos, float evalRad)
            {
                Vector3 scenePos1 = ManWorld.inst.ProjectToGround(scenePos + Vector3.forward * evalRad, true);
                Vector3 scenePos2 = ManWorld.inst.ProjectToGround(scenePos + new Vector3(-0.5f, 0, -0.25f) * evalRad, true);
                Vector3 scenePos3 = ManWorld.inst.ProjectToGround(scenePos + new Vector3(0.5f, 0, -0.25f) * evalRad, true);

                float low = Mathf.Max(scenePos1.y, scenePos2.y, scenePos3.y);
                float high = Mathf.Min(scenePos1.y, scenePos2.y, scenePos3.y);
                return high - low;
            }

            private byte EvalChunk(Vector3 inTile)
            {
                int x = Mathf.FloorToInt((inTile.x / ManWorld.inst.TileSize) * chunksPerTileWH);
                int z = Mathf.FloorToInt((inTile.z / ManWorld.inst.TileSize) * chunksPerTileWH);

                /*
                if (x > chunksPerTileWH)
                    throw new Exception("EvalChunk expects x to be within [0-" + chunksPerTileWH + "] but was given " + x + " instead");
                if (z > chunksPerTileWH)
                    throw new Exception("EvalChunk expects x to be within [0-" + chunksPerTileWH + "] but was given " + z + " instead");

                x = Mathf.Clamp(x, 0, chunksPerTileWH);
                z = Mathf.Clamp(z, 0, chunksPerTileWH);*/

                byte chunk = chunkBytes[x + (z * chunksPerTileWH)];
                if (chunk == 0)
                {
                    float scenePosX = (x * Delta * ManWorld.inst.TileSize) + tile.CalcSceneOrigin().x;
                    float scenePosZ = (z * Delta * ManWorld.inst.TileSize) + tile.CalcSceneOrigin().z;
                    Vector3 scenePos = new Vector3(scenePosX, 0, scenePosZ);
                    //float variance = EvalTerrainDelta(scenePos, EvalRad);
                    float variance = ManWorld.inst.ProjectToGround(scenePos, true).y;
                    byte diff;
                    if (tile.m_ModifiedQuadTree.IsWalkable(x, z, Mathf.CeilToInt(EvalRad)))
                        diff = GetChunkByte((byte)(Mathf.Clamp(Mathf.Clamp01(variance / THVMD) * maxAltByte, 1, maxAltByte)), false);
                    else
                        diff = maxAltByte;
                    //AIGlobals.PopupPlayerInfo(diff.ToString(), WorldPosition.FromScenePosition(ManWorld.inst.ProjectToGround(scenePos, true)));
                    chunkBytes[x + (z * chunksPerTileWH)] = diff;
                }
                return chunk;
            }
        }
    }
}

