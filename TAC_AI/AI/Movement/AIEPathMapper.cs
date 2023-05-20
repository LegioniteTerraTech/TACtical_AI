using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using TerraTechETCUtil;
using TAC_AI.Templates;

namespace TAC_AI.AI.Movement
{
    internal class TileNotLoadedException : Exception
    {
        internal TileNotLoadedException(string ex) : base (ex)
        { 
        }
    }

    /// <summary>
    /// Evaluates world tiles (in smaller tiles) to figure out how path-findable they are.
    ///   Hosted in AIECore.TankAIManager.
    /// </summary>
    public class AIEPathMapper : MonoBehaviour
    {
        public static bool EnableAdvancedPathing => PathRequestsToCalcPerFrame != 0;
        private static bool ForceSphereCasts => true;//ManGameMode.inst.GetCurrentGameType() == ManGameMode.GameType.RaD;

        internal static AIEPathMapper inst;
        public const int chunksPerTileWH = 64;
        public const int chunksPerTileIndex = chunksPerTileWH - 1;
        public static float tileToChunk => (float)chunksPerTileIndex / ManWorld.inst.TileSize;
        public const int AutoPathersToCalcPerFrame = 2;
        public static int PathRequestsToCalcPerFrame = 1;

        /// <summary>
        /// TerrainHeightVarianceMaxDifference
        /// </summary>
        public static float THVMD => ManWorld.inst.TileSize;
        public const byte maxAltByte = 128;
        private static float Delta = 1f / chunksPerTileWH;
        private static float EvalRad = 1.42f * Delta * THVMD;
        private static bool sub = false;
        internal static bool ShowGIZMO = true;

        private static readonly FieldInfo posGet = typeof(SceneryBlocker).GetField("m_Centre", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo shape = typeof(SceneryBlocker).GetField("m_Shape", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo size = typeof(SceneryBlocker).GetField("m_Size", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo rad = typeof(SceneryBlocker).GetField("m_Radius", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly Dictionary<IntVector2, AIPathTileCached> tilesMapped = new Dictionary<IntVector2, AIPathTileCached>();

        public void Update()
        {
            DrawObsticles(Input.GetKey(KeyCode.Space));
        }
        private static float lastDrawTime = 0;
        private static float drawDelay = 1;
        private void DrawObsticles(bool showUnpathable)
        {
            try
            {
                if (ShowGIZMO)
                {
                    bool drawFrame = false;
                    if (lastDrawTime < Time.time)
                    {
                        lastDrawTime = Time.time + drawDelay;
                        drawFrame = true;
                    }
                    foreach (var item in tilesMapped)
                    {
                        try
                        {
                            // Neat tile overlay
                            item.Value.tile.m_ModifiedQuadTree.Draw(item.Value.tile.CalcSceneOrigin(), true);

                            if (drawFrame)
                            {
                                foreach (var item2 in item.Value.blockers)
                                {
                                    try
                                    {
                                        //DebugTAC_AI.Log("DrawObsticles()");
                                        //item2.DrawGizmos();
                                        var WP = (WorldPosition)posGet.GetValue(item2);
                                        Vector3 scenePosGrounded = WP.ScenePosition.SetY(GetAltitudeCached(WP.ScenePosition));
                                        switch ((SceneryBlocker.Shape)shape.GetValue(item2))
                                        {
                                            case SceneryBlocker.Shape.Sphere:
                                                float radius2 = (float)rad.GetValue(item2);
                                                DebugRawTechSpawner.DrawDirIndicatorSphere(scenePosGrounded, radius2, Color.magenta, drawDelay);
                                                break;
                                            case SceneryBlocker.Shape.RectangularPrism:
                                                DebugRawTechSpawner.DrawDirIndicatorRecPriz(scenePosGrounded - (Vector3.up * 25), ((Vector2)size.GetValue(item2)).ToVector3XZ(50), Color.magenta, drawDelay);
                                                break;
                                            case SceneryBlocker.Shape.Circle:
                                                float radius = (float)rad.GetValue(item2);
                                                DebugRawTechSpawner.DrawDirIndicatorCircle(scenePosGrounded + (Vector3.up * 50), Vector3.up, Vector3.forward, radius, Color.magenta, drawDelay);
                                                DebugRawTechSpawner.DrawDirIndicator(scenePosGrounded + (Vector3.up * 50), scenePosGrounded, Color.magenta, drawDelay);
                                                DebugRawTechSpawner.DrawDirIndicatorCircle(scenePosGrounded, Vector3.up, Vector3.forward, radius, Color.magenta, drawDelay);
                                                break;
                                        }
                                        switch (item2.Mode)
                                        {
                                            case SceneryBlocker.BlockMode.Spawn:
                                                DebugRawTechSpawner.DrawDirIndicator(WP.ScenePosition, WP.ScenePosition + new Vector3(0, 10, 0), Color.blue, drawDelay);
                                                break;
                                            case SceneryBlocker.BlockMode.Regrow:
                                                DebugRawTechSpawner.DrawDirIndicator(WP.ScenePosition, WP.ScenePosition + new Vector3(0, 10, 0), Color.green, drawDelay);
                                                break;
                                        }
                                    }
                                    catch { }
                                }
                                if (showUnpathable)
                                {
                                    foreach (var item2 in item.Value.GetEntireTileUnpathable())
                                    {
                                        Vector3 scenePos = item2.ScenePosition;
                                        Templates.DebugRawTechSpawner.DrawDirIndicator(scenePos, scenePos + new Vector3(0, 10, 0), Color.red, drawDelay);
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (Exception e)
            {
                DebugTAC_AI.Assert("DrawObsticles() - FAILED!!!!!!!!!!!!!!!!!!!!!! " + e);
            }
        }

        public void FixedUpdate()
        {
            AutoPathUpdate();
            HandlePathingRequests();
        }

        private static int autoPathStep = 0;
        internal static readonly List<IPathfindable> autoPathers = new List<IPathfindable>();
        private void AutoPathUpdate()
        {
            int frameCalcs = Mathf.Min(autoPathers.Count, AutoPathersToCalcPerFrame);
            int frameCalcStep = 0;
            while (frameCalcStep < frameCalcs && autoPathers.Any())
            {
                if (autoPathStep >= autoPathers.Count)
                    autoPathStep = 0;
                IPathfindable pather = autoPathers[autoPathStep];
                try
                {
                    if (pather != null)
                    {
                        pather.StartPathfind();
                        autoPathStep++;
                    }
                    else
                    {
                        DebugTAC_AI.Log("AIEPathMapper: NULL pather in queue, discarding...");
                        autoPathers.RemoveAt(autoPathStep);
                    }
                }
                catch (Exception e)
                {
                    DebugTAC_AI.Log("AIEPathMapper: Error with a pather in queue, discarding... " + e);
                    autoPathers.RemoveAt(autoPathStep);
                }
                frameCalcStep++;
            }
        }


        /// <summary>
        /// Destination, Pathers
        /// </summary>
        internal static readonly List<AIEAutoPather> pathRequests = new List<AIEAutoPather>();
        internal static readonly List<AIEAutoPather> pathRequestsSuspended = new List<AIEAutoPather>();
        private static int pathStep = 0;
        private static int pathStepS = 0;
        private void HandlePathingRequests()
        {
            int frameCalcs = Mathf.Min(pathRequestsSuspended.Count, PathRequestsToCalcPerFrame);
            int frameCalcStep = 0;
            while (frameCalcStep < frameCalcs && pathRequestsSuspended.Any())
            {
                if (pathStepS >= pathRequestsSuspended.Count)
                    pathStepS = 0;
                var path = pathRequestsSuspended[pathStepS];
                try
                {
                    if (path.CanContinue())
                    {
                        //DebugTAC_AI.Log("Path can continue, resuming...");
                        pathRequests.Add(path);
                        pathRequestsSuspended.RemoveAt(pathStepS);
                    }
                    else
                        pathStepS++;
                }
                catch (Exception e)
                {
                    throw new Exception("AIEPathMapper.HandlePathingRequests(Suspended) hit an exception - ", e);
                }
                frameCalcStep++;
            }
            // ------------------------
            frameCalcs = Mathf.Min(pathRequests.Count, PathRequestsToCalcPerFrame);
            frameCalcStep = 0;
            while (frameCalcStep < frameCalcs && pathRequests.Any())
            {
                if (pathStep >= pathRequests.Count)
                    pathStep = 0;
                var path = pathRequests[pathStep];
                try
                {
                    if (path.CalcRoute())
                        pathStep++;
                    else
                    {
                        path.IsRegistered = false;
                        pathRequests.RemoveAt(pathStep);
                    }
                }
                catch (TileNotLoadedException)
                {
                    //DebugTAC_AI.Log("Path hit unloaded, waiting for tile to become available...");
                    pathRequestsSuspended.Add(path);
                    pathRequests.RemoveAt(pathStep);
                }
                catch (Exception e)
                {
                    throw new Exception("AIEPathMapper.HandlePathingRequests() hit an exception - ", e);
                }
                frameCalcStep++;
            }
        }
        private void ImmedeatelyPathAll()
        {
            for (int step = 0; step < pathRequests.Count;)
            {
                var path = pathRequests[0];
                if (path.CalcRoute())
                    pathStep++;
                else
                {
                    path.IsRegistered = false;
                    pathRequests.RemoveAt(pathStep);
                }
            }
        }

        public static bool StopPather(AIEAutoPather pather)
        {
            if (pather != null && pather.IsRegistered && pathRequests.Remove(pather) 
                || pathRequestsSuspended.Remove(pather))
            {
                pather.IsRegistered = false;
                return true;
            }
            return false;
        }
        public static void RegisterPather(AIEAutoPather pather)
        {
            pathRequests.Add(pather);
            pather.IsRegistered = true;
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
            //DebugTAC_AI.Log("AIEPathMapper: Registered tile " + tile.Coord.ToString());
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
            pathRequests.Clear();
            pathRequestsSuspended.Clear();
            autoPathers.Clear();
        }

        public static byte GetAlt(Vector3 scenePos, bool Throws)
        {
            WorldPosition wp = WorldPosition.FromScenePosition(scenePos);
            if (tilesMapped.TryGetValue(wp.TileCoord, out AIPathTileCached tile))
            {
                return tile.GetChunkAlt(wp, Throws);
            }
            else
            {
                var wTile = ManWorld.inst.TileManager.LookupTile(scenePos, false);
                if (wTile == null)
                {
                    throw new TileNotLoadedException("GetAlt - Hit non-loaded World-Tile");
                    //DebugTAC_AI.Assert("Trying to path on a NULL WorldTile at " + wp.TileCoord);
                    return maxAltByte;
                }
                RegisterTile(wTile);
                if (tilesMapped.TryGetValue(wp.TileCoord, out AIPathTileCached tile2))
                    return tile2.GetChunkAlt(wp,Throws);
                throw new Exception("AIPathMapper(GetAlt) - Could not register a AIPathTile properly.  \nAll AIPathMapper operations will cascade fail!");
            }
        }
        public static bool GetObst(Vector3 scenePos, WorldTile wTile)
        {
            if (wTile == null)
                return false;
            WorldPosition wp = WorldPosition.FromScenePosition(scenePos);
            Vector3 inTile = wp.TileRelativePos;
            int grid = Singleton.Manager<ManPath>.inst.GridSquareSize;
            return !wTile.m_ModifiedQuadTree.IsWalkable((int)(inTile.x / grid), (int)(inTile.z / grid), Mathf.CeilToInt(EvalRad));
        }
        public static bool HasObst(Vector3 scenePos)
        {
            var wTile = ManWorld.inst.TileManager.LookupTile(scenePos, true);
            return GetObst(scenePos, wTile);
        }
        public static float GetAltitudeCached(Vector3 scenePos)
        {
            WorldPosition wp = WorldPosition.FromScenePosition(scenePos);
            if (tilesMapped.TryGetValue(wp.TileCoord, out AIPathTileCached tile))
            {
                return tile.GetActualAltFast(wp, false);
            }
            else
            {
                var wTile = ManWorld.inst.TileManager.LookupTile(scenePos, false);
                if (wTile == null)
                {
                    //throw new TileNotLoadedException("GetAltitudeCached - Hit non-loaded World-Tile");
                    //DebugTAC_AI.Assert("Trying to path on a NULL WorldTile at " + wp.TileCoord);
                    return maxAltByte;
                }
                RegisterTile(wTile);
                if (tilesMapped.TryGetValue(wp.TileCoord, out AIPathTileCached tile2))
                    return tile2.GetActualAltFast(wp, false);
                throw new Exception("AIPathMapper(GetDifficulty) - Could not register a AIPathTile properly.  \nAll AIPathMapper operations will cascade fail!");
            }
        }
        public static float GetHighestAltInRadius(Vector3 scenePos, float radius, bool Throws)
        {
            float ToFill = radius * AIEAutoPather.PathingRadiusMulti;

            float highHeight = -100;
            Vector2 posAltSub = scenePos.ToVector2XZ() - new Vector2(ToFill / 2, ToFill / 2);
            Vector2 posAltPos = scenePos.ToVector2XZ() + new Vector2(ToFill / 2, ToFill / 2);
            Vector3 posAlt;
            for (float i = posAltSub.x; i < posAltPos.x; i += EvalRad)
            {
                for (float j = posAltSub.y; j < posAltPos.y; j += EvalRad)
                {
                    posAlt = new Vector3(i, 0, j);
                    WorldPosition wp = WorldPosition.FromScenePosition(posAlt);
                    if (tilesMapped.TryGetValue(wp.TileCoord, out AIPathTileCached tile))
                    {
                        var diff = tile.GetActualAltFast(wp, Throws);
                        if (diff > highHeight)
                            highHeight = diff;
                    }
                    else
                    {
                        var wTile = ManWorld.inst.TileManager.LookupTile(posAlt, true);
                        if (wTile == null)
                        {
                            throw new TileNotLoadedException("GetHighestAltInRadius - Hit non-loaded World-Tile");
                            //DebugTAC_AI.Assert("Trying to path on a NULL WorldTile at " + wp.TileCoord);
                            return maxAltByte;
                        }
                        RegisterTile(wTile);
                        if (tilesMapped.TryGetValue(wp.TileCoord, out AIPathTileCached tile2))
                        {
                            var diff = tile2.GetActualAltFast(wp, Throws);
                            if (diff > highHeight)
                                highHeight = diff;
                        }
                        else
                            throw new Exception("AIPathMapper(GetDifficulty) - Could not register a AIPathTile properly.  \nAll AIPathMapper operations will cascade fail!");
                    }
                }
            }
            return highHeight;
        }
        public static bool GetAltitudeLoadedOnly(Vector3 scenePos, out float height)
        {
            WorldPosition wp = WorldPosition.FromScenePosition(scenePos);
            if (tilesMapped.TryGetValue(wp.TileCoord, out AIPathTileCached tile))
            {
                height = tile.GetActualAltFast(wp, false);
                return true;
            }
            else
            {
                var wTile = ManWorld.inst.TileManager.LookupTile(scenePos, false);
                if (wTile == null)
                {
                    height = ManWorld.inst.TileManager.GetTerrainHeightAtPosition(scenePos, out _);
                    return false;
                }
                RegisterTile(wTile);
                if (tilesMapped.TryGetValue(wp.TileCoord, out AIPathTileCached tile2))
                {
                    height = tile2.GetActualAltFast(wp, false);
                    return true;
                }
                throw new Exception("AIPathMapper(GetDifficulty) - Could not register a AIPathTile properly.  \nAll AIPathMapper operations will cascade fail!");
            }
        }
        public static bool GetHighestAltInRadiusLoadedOnly(Vector3 scenePos, float radius, out float highHeight, bool Throws)
        {
            float ToFill = radius * AIEAutoPather.PathingRadiusMulti;

            highHeight = -100;
            Vector2 posAltSub = scenePos.ToVector2XZ() - new Vector2(ToFill / 2, ToFill / 2);
            Vector2 posAltPos = scenePos.ToVector2XZ() + new Vector2(ToFill / 2, ToFill / 2);
            Vector3 posAlt;
            for (float i = posAltSub.x; i < posAltPos.x; i += EvalRad)
            {
                for (float j = posAltSub.y; j < posAltPos.y; j += EvalRad)
                {
                    posAlt = new Vector3(i, 0, j);
                    WorldPosition wp = WorldPosition.FromScenePosition(posAlt);
                    if (tilesMapped.TryGetValue(wp.TileCoord, out AIPathTileCached tile))
                    {
                        var diff = tile.GetActualAltFast(wp, Throws);
                        if (diff > highHeight)
                            highHeight = diff;
                    }
                    else
                    {
                        var wTile = ManWorld.inst.TileManager.LookupTile(posAlt, false);
                        if (wTile == null)
                            continue;
                        RegisterTile(wTile);
                        if (tilesMapped.TryGetValue(wp.TileCoord, out AIPathTileCached tile2))
                        {
                            var diff = tile2.GetActualAltFast(wp,Throws);
                            if (diff > highHeight)
                                highHeight = diff;
                        }
                        else
                            throw new Exception("AIPathMapper(GetDifficulty) - Could not register a AIPathTile properly.  \nAll AIPathMapper operations will cascade fail!");
                    }
                }
            }
            return highHeight != -100;
        }


        public static bool MakeBlocker(Collider col, out SceneryBlocker SB)
        {
            if (col is BoxCollider BC)
            {
                Bounds bounds = BC.bounds;
                Vector3 posCenterScene = bounds.center;
                SB = SceneryBlocker.CreateRectangularPrismBlocker(SceneryBlocker.BlockMode.Spawn,
                    WorldPosition.FromScenePosition(posCenterScene), Quaternion.identity, bounds.size);
                return true;
            }
            else if (col is SphereCollider SC)
            {
                Vector3 posCenterScene = Vector3.Scale(col.transform.TransformPoint(SC.center), col.transform.lossyScale);
                SB = SceneryBlocker.CreateSphereBlocker(SceneryBlocker.BlockMode.Spawn,
                    WorldPosition.FromScenePosition(posCenterScene), SC.RadiusWorld());
                return true;
            }
            else if (col is CapsuleCollider CC)
            {
                Vector3 posCenterScene = Vector3.Scale(col.transform.TransformPoint(CC.center), col.transform.lossyScale);
                SB = SceneryBlocker.Create2DCircularBlocker(SceneryBlocker.BlockMode.Spawn,
                    WorldPosition.FromScenePosition(posCenterScene), CC.radius);
                return true;
            }
            else if (col is MeshCollider MC)
            {
                Bounds bounds = MC.bounds;
                Vector3 posCenterScene = bounds.center;
                SB = SceneryBlocker.CreateRectangularPrismBlocker(SceneryBlocker.BlockMode.Spawn,
                    WorldPosition.FromScenePosition(posCenterScene), Quaternion.identity, bounds.size);
                return true;
            }
            SB = null;
            return false;
        }
        public static void AddObstruction(Collider col, WorldPosition wp)
        {
            if (tilesMapped.TryGetValue(wp.TileCoord, out AIPathTileCached tile))
            {
                tile.AddObstruction(col);
            }
            else
            {
                var wTile = ManWorld.inst.TileManager.LookupTile(wp.ScenePosition, true);
                if (wTile == null)
                    return;
                RegisterTile(wTile);
                if (tilesMapped.TryGetValue(wp.TileCoord, out tile))
                {
                    tile.AddObstruction(col);
                }
                else
                    throw new Exception("AIPathMapper(AddObstruction) - Could not register a AIPathTile properly.  \nAll AIPathMapper operations will cascade fail!");
            }
        }
        public static byte GetDifficulty(Vector3 scenePos, AIEAutoPather pather)
        {
            float ToFill = pather.MoveGridScale * AIEAutoPather.PathingRadiusMulti;

            byte bestDiff = 0;
            Vector2 posAltSub = scenePos.ToVector2XZ() - new Vector2(ToFill / 2, ToFill / 2);
            Vector2 posAltPos = scenePos.ToVector2XZ() + new Vector2(ToFill / 2, ToFill / 2);
            Vector3 posAlt;
            for (float i = posAltSub.x; i < posAltPos.x; i += EvalRad)
            {
                for (float j = posAltSub.y; j < posAltPos.y; j += EvalRad)
                {
                    posAlt = new Vector3(i, 0, j);
                    WorldPosition wp = WorldPosition.FromScenePosition(posAlt);
                    if (tilesMapped.TryGetValue(wp.TileCoord, out AIPathTileCached tile))
                    {
                        var diff = pather.GetDifficultyFromAlt(tile.GetChunkAlt(wp, true));
                        if (diff > bestDiff)
                            bestDiff = diff;
                    }
                    else
                    {
                        var wTile = ManWorld.inst.TileManager.LookupTile(posAlt, false);
                        if (wTile == null)
                        {
                            throw new TileNotLoadedException("GetAltitudeCached - Hit non-loaded World-Tile");
                            //DebugTAC_AI.Assert("Trying to path on a NULL WorldTile at " + wp.TileCoord);
                            return maxAltByte;
                        }
                        RegisterTile(wTile);
                        if (tilesMapped.TryGetValue(wp.TileCoord, out AIPathTileCached tile2))
                        {
                            var diff = pather.GetDifficultyFromAlt(tile2.GetChunkAlt(wp, true));
                            if (diff > bestDiff)
                                bestDiff = diff;
                        }
                        else
                            throw new Exception("AIPathMapper(GetDifficulty) - Could not register a AIPathTile properly.  \nAll AIPathMapper operations will cascade fail!");
                    }
                }
            }
            return bestDiff;
        }
        public static byte GetDifficultyWater(Vector3 scenePos, AIEAutoPather pather)
        {
            float ToFill = pather.MoveGridScale * AIEAutoPather.PathingRadiusMulti;

            byte bestDiff = 0;
            Vector2 posAltSub = scenePos.ToVector2XZ() - new Vector2(ToFill / 2, ToFill / 2);
            Vector2 posAltPos = scenePos.ToVector2XZ() + new Vector2(ToFill / 2, ToFill / 2);
            Vector3 posAlt;
            for (float i = posAltSub.x; i < posAltPos.x; i += EvalRad)
            {
                for (float j = posAltSub.y; j < posAltPos.y; j += EvalRad)
                {
                    posAlt = new Vector3(i, 0, j);
                    WorldPosition wp = WorldPosition.FromScenePosition(posAlt);
                    if (tilesMapped.TryGetValue(wp.TileCoord, out AIPathTileCached tile))
                    {
                        byte cached = tile.GetChunkByte(wp, true);
                        if (!tile.BytesToWater(ref cached))
                            return maxAltByte;
                        var diff = pather.GetDifficultyFromAlt(tile.BytesToAlt(ref cached, wp));
                        if (diff > bestDiff)
                            bestDiff = diff;
                    }
                    else
                    {
                        var wTile = ManWorld.inst.TileManager.LookupTile(posAlt, false);
                        if (wTile == null)
                        {
                            throw new TileNotLoadedException("GetAltitudeCached - Hit non-loaded World-Tile");
                            //DebugTAC_AI.Assert("Trying to path on a NULL WorldTile at " + wp.TileCoord);
                            return maxAltByte;
                        }
                        RegisterTile(wTile);
                        if (tilesMapped.TryGetValue(wp.TileCoord, out AIPathTileCached tile2))
                        {
                            byte cached = tile2.GetChunkByte(wp, true);
                            if (!tile2.BytesToWater(ref cached))
                                return maxAltByte;
                            var diff = pather.GetDifficultyFromAlt(tile2.BytesToAlt(ref cached, wp));
                            if (diff > bestDiff)
                                bestDiff = diff;
                        }
                        else
                            throw new Exception("AIPathMapper(GetDifficultyWater) - Could not register a AIPathTile properly.  \nAll AIPathMapper operations will cascade fail!");
                    }
                }
            }
            return bestDiff;
        }
        public static byte GetDifficultyNoWater(Vector3 scenePos, AIEAutoPather pather)
        {
            float ToFill = pather.MoveGridScale * AIEAutoPather.PathingRadiusMulti;

            byte bestDiff = 0;
            Vector2 posAltSub = scenePos.ToVector2XZ() - new Vector2(ToFill / 2, ToFill / 2);
            Vector2 posAltPos = scenePos.ToVector2XZ() + new Vector2(ToFill / 2, ToFill / 2);
            Vector3 posAlt;
            for (float i = posAltSub.x; i < posAltPos.x; i += EvalRad)
            {
                for (float j = posAltSub.y; j < posAltPos.y; j += EvalRad)
                {
                    posAlt = new Vector3(i, 0, j);
                    WorldPosition wp = WorldPosition.FromScenePosition(posAlt);
                    if (tilesMapped.TryGetValue(wp.TileCoord, out AIPathTileCached tile))
                    {
                        byte cached = tile.GetChunkByte(wp, true);
                        if (tile.BytesToWater(ref cached))
                            return maxAltByte;
                        var diff = pather.GetDifficultyFromAlt(tile.BytesToAlt(ref cached, wp));
                        if (diff > bestDiff)
                            bestDiff = diff;
                    }
                    else
                    {
                        var wTile = ManWorld.inst.TileManager.LookupTile(posAlt, false);
                        if (wTile == null)
                        {
                            //throw new TileNotLoadedException("GetAltitudeCached - Hit non-loaded World-Tile");
                            //DebugTAC_AI.Assert("Trying to path on a NULL WorldTile at " + wp.TileCoord);
                            return maxAltByte;
                        }
                        RegisterTile(wTile);
                        if (tilesMapped.TryGetValue(wp.TileCoord, out AIPathTileCached tile2))
                        {
                            byte cached = tile2.GetChunkByte(wp, true);
                            if (tile2.BytesToWater(ref cached))
                                return maxAltByte;
                            var diff = pather.GetDifficultyFromAlt(tile2.BytesToAlt(ref cached, wp));
                            if (diff > bestDiff)
                                bestDiff = diff;
                        }
                        else
                            throw new Exception("AIPathMapper(GetDifficultyNoWater) - Could not register a AIPathTile properly.  \nAll AIPathMapper operations will cascade fail!");
                    }
                }
            }
            return bestDiff;
        }
        public static bool GetIsEnterable(Vector3 scenePos, AIEAutoPather pather)
        {
            float ToFill = pather.MoveGridScale * AIEAutoPather.PathingRadiusMulti;
            float unitBottom = -pather.MoveGridScale / 2;
            byte unitBottomByte = SceneAltToChunkAlt(scenePos.y + unitBottom);

            Vector2 posAltSub = scenePos.ToVector2XZ() - new Vector2(ToFill / 2, ToFill / 2);
            Vector2 posAltPos = scenePos.ToVector2XZ() + new Vector2(ToFill / 2, ToFill / 2);
            Vector3 posAlt;
            for (float i = posAltSub.x; i < posAltPos.x; i += EvalRad)
            {
                for (float j = posAltSub.y; j < posAltPos.y; j += EvalRad)
                {
                    posAlt = new Vector3(i, 0, j);
                    WorldPosition wp = WorldPosition.FromScenePosition(posAlt);
                    if (tilesMapped.TryGetValue(wp.TileCoord, out AIPathTileCached tile))
                    {
                        if (unitBottomByte <= tile.GetChunkAlt(wp, true))
                            return false;
                    }
                    else
                    {
                        var wTile = ManWorld.inst.TileManager.LookupTile(posAlt, false);
                        if (wTile == null)
                        {
                            //throw new TileNotLoadedException("GetAltitudeCached - Hit non-loaded World-Tile");
                            //DebugTAC_AI.Assert("Trying to path on a NULL WorldTile at " + wp.TileCoord);
                            return false;
                        }
                        RegisterTile(wTile);
                        if (tilesMapped.TryGetValue(wp.TileCoord, out AIPathTileCached tile2))
                        {
                            if (unitBottomByte <= tile2.GetChunkAlt(wp, true))
                                return false;
                        }
                        else
                            throw new Exception("AIPathMapper(GetDifficulty) - Could not register a AIPathTile properly.  \nAll AIPathMapper operations will cascade fail!");
                    }
                }
            }
            return true;
        }
        public static bool GetIsEnterableAboveWater(Vector3 scenePos, AIEAutoPather pather)
        {
            float ToFill = pather.MoveGridScale * AIEAutoPather.PathingRadiusMulti;
            float unitBottom = -pather.MoveGridScale / 2;
            byte unitBottomByte = SceneAltToChunkAlt(scenePos.y + unitBottom);

            Vector2 posAltSub = scenePos.ToVector2XZ() - new Vector2(ToFill / 2, ToFill / 2);
            Vector2 posAltPos = scenePos.ToVector2XZ() + new Vector2(ToFill / 2, ToFill / 2);
            Vector3 posAlt;
            for (float i = posAltSub.x; i < posAltPos.x; i += EvalRad)
            {
                for (float j = posAltSub.y; j < posAltPos.y; j += EvalRad)
                {
                    posAlt = new Vector3(i, 0, j);
                    WorldPosition wp = WorldPosition.FromScenePosition(posAlt);
                    if (tilesMapped.TryGetValue(wp.TileCoord, out AIPathTileCached tile))
                    {
                        byte cached = tile.GetChunkByte(wp, true);
                        byte heightByte;
                        if (!tile.BytesToWater(ref cached))
                            heightByte = tile.BytesToAlt(ref cached, wp);
                        else
                            heightByte = GetChunkAltWater();
                        if (unitBottomByte <= heightByte)
                            return false;
                    }
                    else
                    {
                        var wTile = ManWorld.inst.TileManager.LookupTile(posAlt, false);
                        if (wTile == null)
                        {
                            //throw new TileNotLoadedException("GetAltitudeCached - Hit non-loaded World-Tile");
                            //DebugTAC_AI.Assert("Trying to path on a NULL WorldTile at " + wp.TileCoord);
                            return false;
                        }
                        RegisterTile(wTile);
                        if (tilesMapped.TryGetValue(wp.TileCoord, out AIPathTileCached tile2))
                        {
                            byte cached = tile2.GetChunkByte(wp, true);
                            byte heightByte;
                            if (!tile2.BytesToWater(ref cached))
                                heightByte = tile2.BytesToAlt(ref cached, wp);
                            else
                                heightByte = GetChunkAltWater();
                            if (unitBottomByte <= heightByte)
                                return false;
                        }
                        else
                            throw new Exception("AIPathMapper(GetDifficulty) - Could not register a AIPathTile properly.  \nAll AIPathMapper operations will cascade fail!");
                    }
                }
            }
            return true;
        }
        public static bool GetIsEnterableWithinWater(Vector3 scenePos, AIEAutoPather pather)
        {
            float ToFill = pather.MoveGridScale * AIEAutoPather.PathingRadiusMulti;
            float unitTop = pather.MoveGridScale / 2;
            byte unitTopByte = SceneAltToChunkAlt(scenePos.y + unitTop);
            //byte unitBottomByte = SceneAltToChunkAlt(scenePos.y - unitTop);
            if (unitTopByte > GetChunkAltWater())
                return false;

            Vector2 posAltSub = scenePos.ToVector2XZ() - new Vector2(ToFill / 2, ToFill / 2);
            Vector2 posAltPos = scenePos.ToVector2XZ() + new Vector2(ToFill / 2, ToFill / 2);
            Vector3 posAlt;
            for (float i = posAltSub.x; i < posAltPos.x; i += EvalRad)
            {
                for (float j = posAltSub.y; j < posAltPos.y; j += EvalRad)
                {
                    posAlt = new Vector3(i, 0, j);
                    WorldPosition wp = WorldPosition.FromScenePosition(posAlt);
                    if (tilesMapped.TryGetValue(wp.TileCoord, out AIPathTileCached tile))
                    {
                        byte cached = tile.GetChunkByte(wp, true);
                        if (!tile.BytesToWater(ref cached))
                            return false;
                    }
                    else
                    {
                        var wTile = ManWorld.inst.TileManager.LookupTile(posAlt, false);
                        if (wTile == null)
                        {
                            //throw new TileNotLoadedException("GetAltitudeCached - Hit non-loaded World-Tile");
                            //DebugTAC_AI.Assert("Trying to path on a NULL WorldTile at " + wp.TileCoord);
                            return false;
                        }
                        RegisterTile(wTile);
                        if (tilesMapped.TryGetValue(wp.TileCoord, out AIPathTileCached tile2))
                        {
                            byte cached = tile2.GetChunkByte(wp, true);
                            if (!tile2.BytesToWater(ref cached))
                                return false;
                        }
                        else
                            throw new Exception("AIPathMapper(GetDifficulty) - Could not register a AIPathTile properly.  \nAll AIPathMapper operations will cascade fail!");
                    }
                }
            }
            return true;
        }

        public static bool IsWaterlogged(Vector3 scenePos)
        {
            WorldPosition wp = WorldPosition.FromScenePosition(scenePos);
            if (tilesMapped.TryGetValue(wp.TileCoord, out AIPathTileCached tile))
            {
                return tile.BelowWater(wp, false);
            }
            else
            {
                var wTile = ManWorld.inst.TileManager.LookupTile(scenePos, false);
                if (wTile == null)
                    return false;
                RegisterTile(wTile);
                if (tilesMapped.TryGetValue(wp.TileCoord, out AIPathTileCached tile2))
                    return tile2.BelowWater(wp, false);
                throw new Exception("AIPathMapper(IsWater) - Could not register a AIPathTile properly.  \nAll AIPathMapper operations will cascade fail!");
            }
        }

        private static int layerMask = Globals.inst.layerTerrain.mask | Globals.inst.layerScenery.mask | Globals.inst.layerLandmark.mask | Globals.inst.layerSceneryCoarse.mask;
        internal static bool GetActiveAlt(Vector3 scenePos, out float height, out Collider col)
        {
            //DebugTAC_AI.Log("GetActiveAlt Triggered");
            scenePos.y += THVMD - 50;
            if (Physics.SphereCast(scenePos, EvalRad, -Vector3.up, out RaycastHit hit, THVMD, layerMask, QueryTriggerInteraction.Ignore))
            {
                height = scenePos.y - hit.distance;
                col = hit.collider;
                //DebugTAC_AI.Assert("GetActiveAlt returned " + height + " on target " + hit.collider.name + ", layer " + hit.collider.gameObject.layer);
                return true;
            }
            height = 0;
            col = null;
            return false;
        }
        internal static float GetActiveAlt(Vector3 scenePos)
        {
            if (GetActiveAlt(scenePos, out float height, out _))
                return height;
            return GetAltitudeCached(scenePos);
        }
        internal static byte GetActiveChunkAlt(Vector3 scenePos)
        {
            if (GetActiveAlt(scenePos, out float height, out _))
                return SceneAltToChunkAlt(height);
            return GetChunkAlt(scenePos,false);
        }
        internal static byte SceneAltToChunkAlt(float height)
        {
            return (byte)(Mathf.Clamp(Mathf.Clamp01((height + 50f) / THVMD) * maxAltByte,
                        AIEAutoPather2D.BaseDifficulty, maxAltByte));
        }
        internal static byte GetChunkAlt(Vector3 scenePos, bool Throws)
        {
            WorldPosition wp = WorldPosition.FromScenePosition(scenePos);
            if (tilesMapped.TryGetValue(wp.TileCoord, out AIPathTileCached tile))
            {
                return tile.GetChunkAlt(wp, Throws);
            }
            else
            {
                var wTile = ManWorld.inst.TileManager.LookupTile(scenePos, true);
                if (wTile == null)
                    return maxAltByte;
                RegisterTile(wTile);
                if (tilesMapped.TryGetValue(wp.TileCoord, out AIPathTileCached tile2))
                    return tile2.GetChunkAlt(wp, Throws);
                throw new Exception("AIPathMapper(GetDifficulty) - Could not register a AIPathTile properly.  \nAll AIPathMapper operations will cascade fail!");
            }
        }
        internal static byte GetChunkAltWater()
        {
            return SceneAltToChunkAlt(KickStart.WaterHeight);
        }


        private class AIPathTileCached
        {
            internal static FieldInfo blockersGet = typeof(WorldTile).GetField("m_SceneryBlockers", BindingFlags.NonPublic | BindingFlags.Instance);

            public readonly WorldTile tile;
            public readonly HashSet<Collider> LooselyAddedBlockers;
            public readonly HashSet<TerrainObject> Objects;
            public readonly HashSet<SceneryBlocker> blockers;
            private readonly byte[] chunkBytes;
            private List<WorldPosition> unpathable = null;

            internal AIPathTileCached(WorldTile tile)
            {
                if (tile == null)
                    throw new Exception("new AIPathTile() - The WorldTile given was null!?!  \nAll AIPathMapper operations will cascade fail!");
                chunkBytes = new byte[chunksPerTileWH * chunksPerTileWH];
                for (int step = 0; step < chunkBytes.Length; step++)
                {
                    chunkBytes[step] = 0;
                }
                this.tile = tile;
                LooselyAddedBlockers = new HashSet<Collider>();
                if (tile.ManuallyAddedTerrainObjects != null)
                    Objects = new HashSet<TerrainObject>(tile.ManuallyAddedTerrainObjects);
                else
                    Objects = new HashSet<TerrainObject>();
                blockers = new HashSet<SceneryBlocker>();
                GatherAllPossibleBlockers();
            }
            private void GatherAllPossibleBlockers()
            {
                Vector2 MinExt = (-Vector2.one * ManWorld.inst.TileSize) + tile.WorldCentre.ToVector2XZ();
                Vector2 MaxExt = (Vector2.one * ManWorld.inst.TileSize) + tile.WorldCentre.ToVector2XZ();
                ManWorld.inst.TileManager.GetTileCoordRange(new Bounds(tile.CalcSceneCentre(), Vector3.one * ManWorld.inst.TileSize * 1.75f),
                       out IntVector2 min, out IntVector2 max);
                foreach (var item in ManWorld.inst.TileManager.IterateTiles(min, max, WorldTile.State.Created))
                {
                    foreach (var item2 in (List<SceneryBlocker>)blockersGet.GetValue(item))
                    {
                        if (!blockers.Contains(item2))
                            blockers.Add(item2);
                    }
                }
                foreach (var item in ManWorld.inst.LandmarkSpawner.SceneryBlockersOverlappingWorldCoords(MinExt, MaxExt))
                {
                    if (!blockers.Contains(item))
                        blockers.Add(item);
                }
                foreach (var item in ManWorld.inst.VendorSpawner.SceneryBlockersOverlappingWorldCoords(MinExt, MaxExt))
                {
                    if (!blockers.Contains(item))
                        blockers.Add(item);
                }
                foreach (var item in Objects.ToList().FindAll(x => x == null))
                {
                    Objects.Remove(item);
                }
                foreach (var item in Objects)
                {
                    foreach (var col in item.GetComponentsInChildren<Collider>())
                    {
                        LooselyAddedBlockers.Add(col);
                        if (!IsBlocked(col.transform.position) && MakeBlocker(col, out var SB))
                        {
                            blockers.Add(SB);
                        }
                    }
                }
                //DebugTAC_AI.Log("GatherAllPossibleBlockers() - On tile " + tile.Coord + " Searching from " + min + " to " + max +  " Gathered " + blockers.Count + " blockers.");
            }

            internal bool BelowWater(WorldPosition pos, bool Throws)
            {
                return (EvalChunk(pos, Throws) >> 1) < GetChunkAltWater();
            }
            /// <summary>
            /// Obst is an uncalculated devience in height presented by an unloaded object
            /// </summary>
            internal bool NotLoaded(WorldPosition pos, bool Throws)
            {
                return (EvalChunk(pos,Throws) & 1) == 1;
            }
            internal byte GetChunkAlt(WorldPosition pos, bool Throws)
            {
                byte chunkByte = EvalChunk(pos, Throws);
                if (BytesToNotLoaded(ref chunkByte) && ManWorld.inst.CheckIsTileAtPositionLoaded(pos.ScenePosition))
                {
                    if (GetActiveAlt(pos.ScenePosition, out float height, out var col))
                    {
                        AddObstruction(col);
                        chunkByte = CompactChunkByte(SceneAltToChunkAlt(height), false);
                        SetChunkByte(chunkByte, pos);
                    }
                }
                return (byte)(chunkByte >> 1);
            }
            internal float GetActualAltFast(WorldPosition pos, bool Throws)
            {
                byte chunkByte = EvalChunk(pos, Throws);
                if (BytesToNotLoaded(ref chunkByte) && ManWorld.inst.CheckIsTileAtPositionLoaded(pos.ScenePosition))
                {
                    if (GetActiveAlt(pos.ScenePosition, out float height, out var col))
                    {
                        AddObstruction(col);
                        SetChunkByte(CompactChunkByte(SceneAltToChunkAlt(height), false), pos);
                        return height;
                    }
                }
                return (((float)(chunkByte >> 1) / maxAltByte) * THVMD) - 50f;
            }
            internal byte GetChunkByte(WorldPosition pos, bool Throws)
            {
                return EvalChunk(pos, Throws);
            }
            internal bool BytesToWater(ref byte chunkByte)
            {
                bool isTrue = (chunkByte >> 1) < GetChunkAltWater();
                if (isTrue && !KickStart.isWaterModPresent)
                    throw new Exception("BytesToWater returned true when no water is present");
                return isTrue;
            }
            /// <summary>
            /// Obst is an uncalculated devience in height presented by an unloaded object
            /// </summary>
            internal bool BytesToNotLoaded(ref byte chunkByte)
            {
                return (chunkByte & 1) == 1;
            }
            internal byte BytesToAlt(ref byte chunkByte, WorldPosition pos)
            {
                if (BytesToNotLoaded(ref chunkByte) && ManWorld.inst.CheckIsTileAtPositionLoaded(pos.ScenePosition))
                {
                    if (GetActiveAlt(pos.ScenePosition, out float height, out var col))
                    {
                        AddObstruction(col);
                        chunkByte = CompactChunkByte(SceneAltToChunkAlt(height), false);
                        SetChunkByte(chunkByte, pos);
                    }
                }
                return (byte)(chunkByte >> 1);
            }

            private static byte CompactChunkByte(byte chunkByte, bool hasObst)
            {
                return (byte)((chunkByte << 1) | (hasObst ? 1 : 0));
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

            public List<WorldPosition> GetEntireTileUnpathable()
            {
                if (unpathable == null)
                {
                    List<KeyValuePair<WorldPosition, byte>> tilePathData = new List<KeyValuePair<WorldPosition, byte>>();
                    for (int xS = 0; xS < chunksPerTileWH; xS++)
                    {
                        for (int zS = 0; zS < chunksPerTileWH; zS++)
                        {
                            var wp = new WorldPosition(tile.Coord, new Vector3(xS, THVMD, zS));
                            tilePathData.Add(new KeyValuePair<WorldPosition, byte>(wp, EvalChunk(wp, false)));
                        }
                    }
                    unpathable = tilePathData.FindAll(x => x.Value >= AIEAutoPather2D.ObsticleDifficultyAddition).Select(x => x.Key).ToList();
                    DebugTAC_AI.Log("GetEntireTileUnpathable returned " + unpathable.Count + " results");
                }
                return unpathable;
            }
            private byte EvalChunk(WorldPosition pos, bool ThrowException)
            {
                Vector3 inTile = pos.TileRelativePos;
                int x = (int)((inTile.x * tileToChunk) + 0.5f);
                int z = (int)((inTile.z * tileToChunk) + 0.5f);

                if (x > chunksPerTileIndex)
                    throw new Exception("EvalChunk expects x to be within [0-" + chunksPerTileIndex + "] but was given " + x + " instead");
                if (z > chunksPerTileIndex)
                    throw new Exception("EvalChunk expects x to be within [0-" + chunksPerTileIndex + "] but was given " + z + " instead");

                //x = Mathf.Clamp(x, 0, chunksPerTileIndex);
                //z = Mathf.Clamp(z, 0, chunksPerTileIndex);

                int index = x + (z * chunksPerTileWH);
                byte chunkByte = chunkBytes[index];
                if (chunkByte == 0)
                {
                    //float variance = EvalTerrainDelta(scenePos, EvalRad);
                    bool obst = false;
                    int grid = ManPath.inst.GridSquareSize;
                    Vector3 scenePos = pos.ScenePosition;
                    float height = ManWorld.inst.TileManager.GetTerrainHeightAtPosition(scenePos, out _, true);
                    scenePos.y = height;
                    if (!ForceSphereCasts && tile.m_ModifiedQuadTree.IsWalkable((int)(inTile.x / grid), (int)(inTile.z / grid), Mathf.CeilToInt(EvalRad / grid)))
                    {
                        if (IsBlocked(scenePos))
                            obst = true;
                        //else if (SceneAltToChunkAlt(height) != BytesToAlt(ref chunkByte, pos))
                        //        throw new Exception("EvalChunk - MISMATCH WITH CompactChunkByte and GetAltitude while no Obsticle is present");
                    }
                    else
                        obst = true;
                    if (obst)
                    {
                        if (ManWorld.inst.CheckIsTileAtPositionLoaded(scenePos))
                        {
                            if (GetActiveAlt(scenePos, out float alt, out Collider col))
                            {
                                //DebugTAC_AI.Log("Pos " + scenePos + " is Obstructed and height calc with height " + alt);
                                AddObstruction(col);
                                chunkByte = CompactChunkByte(SceneAltToChunkAlt(alt), false);
                            }
                            else
                            {
                                //DebugTAC_AI.Log("Pos " + scenePos + " is Obstructed but loaded with height " + height);
                                chunkByte = CompactChunkByte(SceneAltToChunkAlt(height), false);
                            }
                        }
                        else
                        {
                            //throw new TileNotLoadedException("Pos " + scenePos + " is Obstructed, tile not loaded, height is " + height);
                            //DebugTAC_AI.Log("Pos " + scenePos + " is Obstructed, tile not loaded, height is " + height);
                            chunkByte = CompactChunkByte(SceneAltToChunkAlt(height), true);
                        }
                    }
                    else
                    {
                        DebugTAC_AI.Log("Pos " + scenePos + " is pathable with height " + height);
                        chunkByte = CompactChunkByte(SceneAltToChunkAlt(height), false);
                    }
                    //AIGlobals.PopupPlayerInfo(diff.ToString(), WorldPosition.FromScenePosition(ManWorld.inst.ProjectToGround(scenePos, true)));
                    chunkBytes[index] = chunkByte;
                }
                if (ThrowException && BytesToNotLoaded(ref chunkByte))
                {
                    chunkBytes[index] = 0;
                    throw new TileNotLoadedException("Tile at " + pos.TileCoord + " scenePos " + pos.ScenePosition + " is not loaded");
                }
                return chunkByte;
            }
            private void SetChunkByte(byte toSet, WorldPosition pos)
            {
                Vector3 inTile = pos.TileRelativePos;
                int x = (int)((inTile.x * tileToChunk) + 0.5f);
                int z = (int)((inTile.z * tileToChunk) + 0.5f);

                //AIGlobals.PopupPlayerInfo(toSet.ToString(), pos);
                chunkBytes[x + (z * chunksPerTileWH)] = toSet;
            }
            internal void AddObstruction(Collider col)
            {
                if (!LooselyAddedBlockers.Contains(col) && !(col is TerrainCollider))
                {
                    foreach (var col2 in col.transform.parent.GetComponentsInChildren<Collider>(false))
                    {
                        if (!LooselyAddedBlockers.Contains(col2))
                        {
                            LooselyAddedBlockers.Add(col2);
                            if (!IsBlocked(col2.transform.position) && MakeBlocker(col2, out SceneryBlocker SB))
                                blockers.Add(SB);
                        }
                    }
                }
            }
            private bool IsBlocked(Vector3 scenePos)
            {
                using (var temp = blockers.GetEnumerator())
                {
                    while (temp.MoveNext())
                    {
                        if (temp.Current.IsBlockingPos(SceneryBlocker.BlockMode.Spawn, scenePos, EvalRad))
                            return true;
                    }
                }
                return false;
            }
        }


        internal class GUIManaged
        {
            private static bool typesDisp = false;
            private static HashSet<WaterPathing> enabledTabs = null;
            public static void GUIGetTotalManaged()
            {
                if (!EnableAdvancedPathing)
                {
                    GUILayout.Box("--- Advanced Pathing [DISABLED] --- ");
                    return;
                }
                if (enabledTabs == null)
                {
                    enabledTabs = new HashSet<WaterPathing>();
                }
                GUILayout.Box("--- Advanced Pathing  --- ");
                GUILayout.Label("  Auto Pathers: " + autoPathers.Count);
                GUILayout.Label("  Total Pathers: " + pathRequests.Count);
                int activeCount = 0;
                Dictionary<WaterPathing, int> types = new Dictionary<WaterPathing, int>();
                foreach (WaterPathing item in Enum.GetValues(typeof(WaterPathing)))
                {
                    types.Add(item, 0);
                }
                foreach (var Path in pathRequests)
                {
                    if (Path != null && Path.PathingUnit != null)
                    {
                        activeCount++;
                        var Pathing = Path.PathingUnit.WaterPathing;
                        types[Pathing]++;
                    }
                }
                GUILayout.Label("    Standby: " + pathRequestsSuspended.Count + " | Active: " + activeCount);
                if (GUILayout.Button("    Types: " + types.Count))
                    typesDisp = !typesDisp;
                if (typesDisp)
                {
                    foreach (var item in types)
                    {
                        if (GUILayout.Button("      Type: " + item.Key.ToString() + " - " + item.Value))
                        {
                            if (enabledTabs.Contains(item.Key))
                                enabledTabs.Remove(item.Key);
                            else
                                enabledTabs.Add(item.Key);
                        }
                        if (enabledTabs.Contains(item.Key))
                        {
                            foreach (var item2 in pathRequests.FindAll(x => x != null && 
                            x.PathingUnit != null && x.PathingUnit.WaterPathing == item.Key))
                            {
                                Vector3 pos = item2.StartPosWP.ScenePosition;
                                Vector3 posEnd = item2.EndPosWP.ScenePosition;
                                GUILayout.Label("        Tech: " + pos + " | Dest " + posEnd);
                                DebugRawTechSpawner.DrawDirIndicator(pos, pos + new Vector3(0, 10, 0), Color.red);
                                DebugRawTechSpawner.DrawDirIndicator(posEnd, posEnd + new Vector3(0, 10, 0), Color.green);
                            }
                        }
                    }
                }
            }
        }
    }
}

