using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using TerraTechETCUtil;

namespace TAC_AI.AI.Movement
{
    public interface IPathfindable
    {
        /// <summary>
        /// Should we try actively pathfinding?
        /// Should ONLY be set by SetAutoPathfinding()!
        /// </summary>
        bool AutoPathfind { get; set; }
        /// <summary>
        /// Handled automatically.  DO NOT TOUCH!
        /// </summary>
        AIEAutoPather2D Pathfinder { get; set; }
        /// <summary>
        /// How we should handle water pathfinding
        /// </summary>
        WaterPathing WaterPathing { get; set; }
        /// <summary>
        /// The precision of the pathing grid.  Smaller Techs should have smaller values.
        /// </summary>
        float PathingPrecision { get; set; }
        /// <summary>
        /// The max allowed difficulty of the pathing when finding a route.  The more capable, the higher this is.
        /// </summary>
        byte MaxPathDifficulty { get; set; }

        Vector3 CurrentPosition();
        Vector3 GetTargetDestination();
        void OnFinishedPathfinding(List<WorldPosition> pos);
    }
    public static class IPathfindableExtensions
    {
        public static void SetAutoPathfinding(this IPathfindable pathable, bool active)
        {
            if (active != pathable.AutoPathfind)
            {
                pathable.AutoPathfind = active;
                if (active)
                    AIEPathMapper.autoPathers.Add(pathable);
                else
                    AIEPathMapper.autoPathers.Remove(pathable);
            }
        }
        public static bool StartPathfind(this IPathfindable pathable)
        {
            return AIEAutoPather2D.DoPathfinding(ref pathable);
        }
        public static void StopPathfinding(this IPathfindable pathable)
        {
            AIEAutoPather2D.CancelPathfinding(ref pathable);
        }
    }

    /// <summary>
    /// Acts as the slow, non-immedeate pathfinding for land and sea AIs.
    /// </summary>
    public class AIEAutoPather2D
    {
        public const int WrongHeadingDifficultyAddition = 6;
        public const int ObsticleDifficultyAddition = 16;
        public const float TerrainSlopePenaltyMulti = 8f;
        public const int maxDeadEndsTillFail = 8;
        public const float maxPathedTillFailDistMulti = 4f;

        public static int PathingIterationsPerCall = 1;
        public static int PathfindBeyondDistBox = 12;

        public WaterPathing waterPath;
        public byte maxDiff;
        public float MoveGridScale;
        private bool Finished = false;
        private readonly IPathfindable PathingUnit;
        private IntVector2 CurPos;
        public byte CurAlt;
        private IntVector2 StartPos;
        private IntVector2 EndPos;
        private WorldPosition Center;
        private readonly List<IntVector2> PathRoute = new List<IntVector2>();
        private readonly HashSet<IntVector2> pathed = new HashSet<IntVector2>();
        private int deadEnds = 0;
        private readonly Event<AIEAutoPather2D> resultsPasser = new Event<AIEAutoPather2D>();
        private int maxPathedTillFail;

        private AIEAutoPather2D(IPathfindable pathable, Vector3 startPos, Vector3 endPos)
        {
            PathingUnit = pathable;
            maxDiff = (byte)Mathf.Clamp(pathable.MaxPathDifficulty, 1, AIEPathMapper.maxAltByte - 1);
            MoveGridScale = pathable.PathingPrecision;
            Center = WorldPosition.FromScenePosition((startPos + endPos) / 2);

            StartPos = ToLocal(startPos);
            EndPos = ToLocal(endPos);
            CurPos = StartPos;
            pathed.Add(CurPos);
            waterPath = pathable.WaterPathing;
            maxPathedTillFail = Mathf.CeilToInt((startPos - endPos).magnitude * maxPathedTillFailDistMulti);

            AIEPathMapper.RegisterPather(GetWorldPathEnd(), this);
        }

        internal static bool IsFarEnough(Vector3 start, Vector3 end)
        {
            Vector3 pos = start - end;
            return pos.x > PathfindBeyondDistBox || pos.x < -PathfindBeyondDistBox
                 || pos.z > PathfindBeyondDistBox || pos.z < -PathfindBeyondDistBox;
        }

        /// <summary>
        /// Must be called from within the class instance with the pather in it
        /// </summary>
        /// <param name="pathable"></param>
        /// <param name="startPos"></param>
        /// <param name="destPos"></param>
        /// <returns></returns>
        internal static bool DoPathfinding(ref IPathfindable pathable)
        {
            Vector3 startPos = pathable.CurrentPosition();
            Vector3 destPos = pathable.GetTargetDestination();
            // Check if we should just rely on immedeate pathing
            if (!IsFarEnough(startPos, destPos))
                return false;
            var pathfinder = pathable.Pathfinder;
            if (pathfinder == null)
                pathable.Pathfinder = new AIEAutoPather2D(pathable, startPos, destPos);
            else
            {
                if (pathable == null)
                    throw new NullReferenceException("AIAutoPather - PathingUnit was null on DoPathfinding call!");

                // Check if we should recalc it
                if (IsFarEnough(pathfinder.ToSceneNoHeightCheck(pathfinder.EndPos), destPos))
                    pathfinder.Recalc(startPos, destPos);
            }
            return true;
        }
        internal static bool CancelPathfinding(ref IPathfindable pathable)
        {
            if (pathable.Pathfinder != null)
            {
                Debug.Log("AIAutoPather - Cancelled pathfinding.");
                pathable.Pathfinder.Finished = true;
                pathable.Pathfinder = null;
            }
            return true;
        }

        private void Recalc()
        {
            if (PathingUnit != null)
                Recalc(PathingUnit.CurrentPosition(), PathingUnit.GetTargetDestination());
        }
        private void Recalc(Vector3 startPos, Vector3 endPos)
        {
            PathRoute.Clear();
            pathed.Clear();
            maxDiff = (byte)Mathf.Clamp(PathingUnit.MaxPathDifficulty, 1, AIEPathMapper.maxAltByte - 1);
            MoveGridScale = PathingUnit.PathingPrecision;
            Center = WorldPosition.FromScenePosition((startPos + endPos) / 2);

            StartPos = ToLocal(startPos);
            EndPos = ToLocal(endPos);
            CurPos = StartPos;
            pathed.Add(CurPos);
            waterPath = PathingUnit.WaterPathing;
            maxPathedTillFail = Mathf.CeilToInt((startPos - endPos).magnitude * maxPathedTillFailDistMulti);

            if (Finished)
                AIEPathMapper.RegisterPather(GetWorldPathEnd(), this);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>True if calcing, false when finished</returns>
        internal bool CalcRoute()
        {
            if (PathingUnit == null)
                return false;
            List<KeyValuePair<byte, IntVector2>> toCheck = new List<KeyValuePair<byte, IntVector2>>();
            IntVector2 posC;
            byte diff;
            for (int step = 0; step < PathingIterationsPerCall; step++)
            {
                switch (waterPath)
                {
                    case WaterPathing.AvoidWater:
                        posC = CurPos + new IntVector2(1, 0);
                        if (!pathed.Contains(posC))
                        {
                            diff = CalcWater(posC, CurPos);
                            if (diff <= maxDiff)
                                toCheck.Add(new KeyValuePair<byte, IntVector2>(diff, posC));
                        }
                        posC = CurPos + new IntVector2(-1, 0);
                        if (!pathed.Contains(posC))
                        {
                            diff = CalcWater(posC, CurPos);
                            if (diff <= maxDiff)
                                toCheck.Add(new KeyValuePair<byte, IntVector2>(diff, posC));
                        }
                        posC = CurPos + new IntVector2(0, 1);
                        if (!pathed.Contains(posC))
                        {
                            diff = CalcWater(posC, CurPos);
                            if (diff <= maxDiff)
                                toCheck.Add(new KeyValuePair<byte, IntVector2>(diff, posC));
                        }
                        posC = CurPos + new IntVector2(0, -1);
                        if (!pathed.Contains(posC))
                        {
                            diff = CalcWater(posC, CurPos);
                            if (diff <= maxDiff)
                                toCheck.Add(new KeyValuePair<byte, IntVector2>(diff, posC));
                        }
                        break;
                    case WaterPathing.AllowWater:
                        posC = CurPos + new IntVector2(1, 0);
                        if (!pathed.Contains(posC))
                        {
                            diff = CalcDiff(posC, CurPos);
                            if (diff <= maxDiff)
                                toCheck.Add(new KeyValuePair<byte, IntVector2>(diff, posC));
                        }
                        posC = CurPos + new IntVector2(-1, 0);
                        if (!pathed.Contains(posC))
                        {
                            diff = CalcDiff(posC, CurPos);
                            if (diff <= maxDiff)
                                toCheck.Add(new KeyValuePair<byte, IntVector2>(diff, posC));
                        }
                        posC = CurPos + new IntVector2(0, 1);
                        if (!pathed.Contains(posC))
                        {
                            diff = CalcDiff(posC, CurPos);
                            if (diff <= maxDiff)
                                toCheck.Add(new KeyValuePair<byte, IntVector2>(diff, posC));
                        }
                        posC = CurPos + new IntVector2(0, -1);
                        if (!pathed.Contains(posC))
                        {
                            diff = CalcDiff(posC, CurPos);
                            if (diff <= maxDiff)
                                toCheck.Add(new KeyValuePair<byte, IntVector2>(diff, posC));
                        }
                        break;
                    case WaterPathing.StayInWater:
                        posC = CurPos + new IntVector2(1, 0);
                        if (!pathed.Contains(posC))
                        {
                            diff = CalcWaterOnly(posC, CurPos);
                            if (diff <= maxDiff)
                                toCheck.Add(new KeyValuePair<byte, IntVector2>(diff, posC));
                        }
                        posC = CurPos + new IntVector2(-1, 0);
                        if (!pathed.Contains(posC))
                        {
                            diff = CalcWaterOnly(posC, CurPos);
                            if (diff <= maxDiff)
                                toCheck.Add(new KeyValuePair<byte, IntVector2>(diff, posC));
                        }
                        posC = CurPos + new IntVector2(0, 1);
                        if (!pathed.Contains(posC))
                        {
                            diff = CalcWaterOnly(posC, CurPos);
                            if (diff <= maxDiff)
                                toCheck.Add(new KeyValuePair<byte, IntVector2>(diff, posC));
                        }
                        posC = CurPos + new IntVector2(0, -1);
                        if (!pathed.Contains(posC))
                        {
                            diff = CalcWaterOnly(posC, CurPos);
                            if (diff <= maxDiff)
                                toCheck.Add(new KeyValuePair<byte, IntVector2>(diff, posC));
                        }
                        break;
                }


                if (toCheck.Count == 0)
                {
                    if (deadEnds == maxDeadEndsTillFail)
                    {
                        Debug.Assert(true, "AIAutoPather - Failed since maxDeadEndsTillFail was reached");
                        resultsPasser.Send(this);
                        resultsPasser.EnsureNoSubscribers();
                        return false;
                    }
                    PathRoute.RemoveAt(PathRoute.Count - 2);
                    CurPos = PathRoute.Last();
                    deadEnds++;
                }
                else
                {
                    KeyValuePair<byte, IntVector2> best = toCheck.OrderBy(x => x.Key).First();
                    CurPos = best.Value;
                    if (best.Key == 1)
                        AIGlobals.PopupAllyInfo(pathed.Count.ToString(), WorldPosition.FromScenePosition(ManWorld.inst.ProjectToGround(ToSceneNoHeightCheck(CurPos), true)));
                    else if (best.Key == AIEPathMapper.maxAltByte)
                        AIGlobals.PopupEnemyInfo(pathed.Count.ToString(), WorldPosition.FromScenePosition(ManWorld.inst.ProjectToGround(ToSceneNoHeightCheck(CurPos), true)));
                    else
                        AIGlobals.PopupNeutralInfo(pathed.Count.ToString() + " | " + best.Key, WorldPosition.FromScenePosition(ManWorld.inst.ProjectToGround(ToSceneNoHeightCheck(CurPos), true)));
                    pathed.Add(CurPos);
                    if (AIEPathMapper.pathRequests.TryGetValue(GetWorldPathEnd(), out List<AIEAutoPather2D> pathB))
                    {
                        AIEAutoPather2D path2 = pathB.First();
                        if (path2.pathed.Contains(Center.ScenePosition.ToVector2XZ() + ((Vector2)EndPos * MoveGridScale)))
                        {
                            path2.resultsPasser.Subscribe(WaitForResults);
                            Debug.Log("AIAutoPather - Encountered existing main path and will wait for it to finish to reuse results from it.");
                            return false;
                        }
                    }
                    if (pathed.Count == maxPathedTillFail)
                    {
                        Debug.Assert(true, "AIAutoPather - Failed since maxPathedTillFail was reached");
                        resultsPasser.Send(this);
                        resultsPasser.EnsureNoSubscribers();
                        return false;
                    }
                    PathRoute.Add(CurPos);

                    if (CurPos == EndPos)
                    {
                        TryShorten();
                        Finished = true;
                        Debug.Log("AIAutoPather - Success with " + PathRoute.Count + " points and " + pathed.Count + " attempts.");
                        PathingUnit.OnFinishedPathfinding(GetPath());
                        resultsPasser.Send(this);
                        resultsPasser.EnsureNoSubscribers();
                        return false;
                    }
                }
                toCheck.Clear();
            }
            return true;
        }

        internal byte GetDiff(byte alt)
        {
            return (byte)Mathf.Clamp(Mathf.FloorToInt(Mathf.Abs(alt - CurAlt) * TerrainSlopePenaltyMulti), 0, 128);
        }
        private void WaitForResults(AIEAutoPather2D pathMain)
        {
            if (!pathMain.Finished)
            {
                Recalc();
            }
            else
            {
                int indexCopycat = pathMain.PathRoute.FindIndex(delegate (IntVector2 cand)
                { return cand.x == CurPos.x && cand.y == CurPos.y; });
                for (int indexStep = indexCopycat + 1; indexStep < pathMain.PathRoute.Count; indexStep++)
                {
                    PathRoute.Add(pathMain.PathRoute[indexStep]);
                    pathed.Add(pathMain.PathRoute[indexStep]);
                }
                CurPos = pathMain.PathRoute.Last();
                Debug.Log("AIAutoPather - Merged routes with same destination at " + indexCopycat + " points of it's total " + pathMain.PathRoute.Count + " points.");

                TryShorten();
                Finished = true;
                Debug.Log("AIAutoPather - Success with " + PathRoute.Count + " points and " + pathed.Count + " attempts.");
                PathingUnit.OnFinishedPathfinding(GetPath());
            }
        }

        private Vector3 ToSceneNoHeightCheck(IntVector2 local)
        {
            return ((Vector2)local * MoveGridScale).ToVector3XZ() + Center.ScenePosition;
        }
        private IntVector2 GetWorldPathEnd()
        {
            return new IntVector2(ToSceneNoHeightCheck(EndPos).ToVector2XZ() / PathfindBeyondDistBox);
        }
        private IntVector2 ToLocal(Vector3 pos)
        {
            return (pos.ToVector2XZ() - Center.ScenePosition.ToVector2XZ()) / MoveGridScale;
        }

        private byte CalcDiff(IntVector2 pos, IntVector2 posCur)
        {
            int diff = AIEPathMapper.GetDifficulty(Center.ScenePosition + (Vector3)pos.ToVector3XZ() * MoveGridScale, this);
            diff += Mathf.FloorToInt(-(Vector2.Dot(pos - posCur, EndPos - posCur) - 1) * WrongHeadingDifficultyAddition);
            if (ManVisible.inst.VisiblesTouchingRadius(ToSceneNoHeightCheck(pos), MoveGridScale, new Bitfield<ObjectTypes>(new ObjectTypes[1] { ObjectTypes.Scenery })).Any())
                diff += ObsticleDifficultyAddition;
            return (byte)Mathf.Clamp(diff, 1, AIEPathMapper.maxAltByte);
        }
        private byte CalcWater(IntVector2 pos, IntVector2 posCur)
        {
            int diff = AIEPathMapper.GetDifficultyWater(Center.ScenePosition + (Vector3)pos.ToVector3XZ() * MoveGridScale, this);
            diff += Mathf.FloorToInt(-(Vector2.Dot(pos - posCur, EndPos - posCur) - 1) * WrongHeadingDifficultyAddition);
            if (ManVisible.inst.VisiblesTouchingRadius(ToSceneNoHeightCheck(pos), MoveGridScale, new Bitfield<ObjectTypes>(new ObjectTypes[1] { ObjectTypes.Scenery })).Any())
                diff += ObsticleDifficultyAddition;
            return (byte)Mathf.Clamp(diff, 1, AIEPathMapper.maxAltByte);
        }
        private byte CalcWaterOnly(IntVector2 pos, IntVector2 posCur)
        {
            int diff = AIEPathMapper.GetDifficultyWaterInv(Center.ScenePosition + (Vector3)pos.ToVector3XZ() * MoveGridScale, this);
            diff += Mathf.FloorToInt(-(Vector2.Dot(pos - posCur, EndPos - posCur) - 1) * WrongHeadingDifficultyAddition);
            if (ManVisible.inst.VisiblesTouchingRadius(ToSceneNoHeightCheck(pos), MoveGridScale, new Bitfield<ObjectTypes>(new ObjectTypes[1] { ObjectTypes.Scenery })).Any())
                diff += ObsticleDifficultyAddition;
            return (byte)Mathf.Clamp(diff, 1, AIEPathMapper.maxAltByte);
        }
        private List<WorldPosition> GetPath()
        {
            if (PathRoute.Count == 0)
                throw new Exception("AIAutoPather - GetPath returned no valid points for pathfinding");
            List<WorldPosition> path = new List<WorldPosition>();
            foreach (var item in PathRoute)
            {
                path.Add(WorldPosition.FromScenePosition(ManWorld.inst.ProjectToGround(ToSceneNoHeightCheck(item), true)));
            }
            return path;
        }

        private void TryShorten()
        {
            if (PathRoute.Count == 0)
                throw new Exception("AIAutoPather - TryShorten returned no valid points for pathfinding");
            int removed = 1;
            // Remove straight extra points
            for (int step = 0; step < PathRoute.Count - 2; step++)
            {
                if (((Vector2)(PathRoute[step] - PathRoute[step + 2])).sqrMagnitude == 2)
                {
                    PathRoute.RemoveAt(step + 1);
                    removed++;
                }
            }
            // Remove corner bend points and longer straights
            for (int step = 0; step < PathRoute.Count - 4; step++)
            {
                if (((Vector2)(PathRoute[step] - PathRoute[step + 2])).sqrMagnitude == 4)
                {
                    PathRoute.RemoveAt(step + 1);
                    removed++;
                }
            }
            PathRoute.RemoveAt(0);
            Debug.Log("AIAutoPather - TryShorten has removed " + removed + " entries");
            //throw new NotImplementedException("AIAutoPather - TryShorten is incomplete");
        }

    }
    public enum WaterPathing
    {
        AvoidWater,
        AllowWater,
        StayInWater
    }
}
