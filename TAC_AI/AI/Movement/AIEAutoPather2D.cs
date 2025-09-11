using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TerraTechETCUtil;

namespace TAC_AI.AI.Movement
{
    /// <summary>
    /// Acts as the slow, non-immedeate pathfinding for land and sea AIs.
    /// </summary>

    public class AIEAutoPather2D : AIEAutoPather
    {
        private IntVector2 CurPos;
        private IntVector2 StartPos;
        private IntVector2 EndPos;
        private List<IntVector2> PathRoute = new List<IntVector2>();
        private readonly HashSet<IntVector2> pathed = new HashSet<IntVector2>();

        private AIEAutoPather2D(IPathfindable pathable, Vector3 startPos, Vector3 endPos) : base(pathable)
        {
            Recalc_Internal(startPos, endPos);

            AIEPathMapper.RegisterPather(this);
        }

        /// <summary>
        /// Must be called from within the class instance with the pather in it
        /// </summary>
        /// <param name="pathable"></param>
        /// <param name="startPos"></param>
        /// <param name="destPos"></param>
        /// <returns></returns>
        public static bool DoPathfinding(ref IPathfindable pathable)
        {
            if (pathable == null)
                throw new NullReferenceException("AIAutoPather - PathingUnit was null on DoPathfinding call!");

            Vector3 startPos = pathable.CurrentPosition();
            Vector3 destPos = pathable.GetTargetDestination();

            // Check if we should just rely on immedeate pathing
            if (!IsFarEnough(startPos, destPos))
                return false;
            var pathfinder = pathable.Pathfinder;
            if (pathfinder == null)
                pathable.Pathfinder = new AIEAutoPather2D(pathable, startPos, destPos);
            else if (!(pathable.Pathfinder is AIEAutoPather2D))
            {
                DebugTAC_AI.Log("AIAutoPather - Switching to 2D...");
                pathable.StopPathfinding();
                pathable.Pathfinder = new AIEAutoPather2D(pathable, startPos, destPos);
            }
            else
            {
                // Check if we should recalc it
                if (IsFarEnough(pathfinder.EndPosWP.ScenePosition, destPos) || (pathfinder.IsFinished && !pathfinder.Success &&
                    IsFarEnough(pathfinder.StartPosWP.ScenePosition, startPos)))
                {
                    //DebugTAC_AI.Log("AIAutoPather - RECALC Called.");
                    pathfinder.Recalc(startPos, destPos);
                }
            }
            return true;
        }


        private void RecalcManual()
        {
            if (PathingUnit != null)
                Recalc(PathingUnit.CurrentPosition(), PathingUnit.GetTargetDestination());
        }
        public override void Recalc(Vector3 startPosScene, Vector3 endPosScene)
        {
            Recalc_Internal(startPosScene, endPosScene);
            base.Recalc(startPosScene, endPosScene);
        }
        private void Recalc_Internal(Vector3 startPos, Vector3 endPos)
        {
            PathingUnit.OnFinishedPathfinding(null);
            PathRoute.Clear();
            pathed.Clear();
            maxDiff = (byte)Mathf.Clamp(PathingUnit.MaxPathDifficulty, 1, AIEPathMapper.maxAltByte - 1);
            MoveGridScale = Mathf.Max(PathingUnit.PathingPrecision, 1);
            DebugTAC_AI.Assert(PathingUnit.PathingPrecision < 1,
                "AIEAutoPather expects PathingPrecision to be greater than one but got "
                + PathingUnit.PathingPrecision + " instead");
            StartPosWP = WorldPosition.FromScenePosition(startPos);
            CenterPos = WorldPosition.FromScenePosition((startPos + endPos) / 2);
            EndPosWP = WorldPosition.FromScenePosition(endPos);
            waterPath = PathingUnit.WaterPathing;

            CurAlt = AIEPathMapper.GetAlt(startPos, false);
            startPos = FindIdealStart(startPos);
            StartPos = ToLocal(startPos);
            CurAlt = AIEPathMapper.GetAlt(startPos, false);
            CurPos = StartPos;
            EndPos = ToLocal(endPos);
            pathed.Add(CurPos);
            Finished = false;
            Success = false;
            float mag = (startPos - endPos).magnitude / MoveGridScale;
            DebugTAC_AI.Assert(float.IsNaN(mag) || mag < 0,
                "AIEAutoPather magnitude is " + (float.IsNaN(mag) ? "NaN" : "Negative"));
            if (mag > int.MaxValue / maxPathedTillFailDistMulti)
                DebugTAC_AI.Exception("AIEAutoPather magnitude is too big, points given are "
                    + startPos + " vs " + endPos);
            maxDeadEndsTillFail = Mathf.CeilToInt(mag * maxDeadEndsTillFailMulti);
            maxPathedTillFail = Mathf.CeilToInt(mag * maxPathedTillFailDistMulti);
            DebugTAC_AI.Info("AIEAutoPather.Recalc_Internal() - MoveGridScale" + MoveGridScale +" |  maxPathedTillFail: " + maxPathedTillFail);
        }
        private List<IntVector2> iterateAround4 = IterateAroundExpand(6);
        private Vector3 FindIdealStart(Vector3 Initial)
        {
            IntVector2 loc = ToLocal(Initial);
            IntVector2 posC;
            try
            {
                switch (waterPath)
                {
                    case WaterPathing.AvoidWater:
                        if (CalcAvoidWater(loc) <= maxDiff)
                            return Initial;
                        else
                        {
                            foreach (var item in iterateAround4)
                            {
                                posC = loc + item;
                                try
                                {
                                    if (CalcAvoidWater(posC, loc) <= maxDiff)
                                        return ToSceneHeightFast(posC);
                                }
                                catch (TileNotLoadedException) { }
                            }
                            DebugTAC_AI.Log("AIAutoPather - Failed to FindIdealStart(Land) at " + loc + ", alt " + AIEPathMapper.GetAltitudeCached(Initial));
                            return Initial;
                        }
                    case WaterPathing.StayInWater:
                        if (CalcWaterOnly(loc) <= maxDiff)
                            return Initial;
                        else
                        {
                            foreach (var item in iterateAround4)
                            {
                                posC = loc + item;
                                try
                                {
                                    if (CalcWaterOnly(posC, loc) <= maxDiff)
                                        return ToSceneHeightFast(posC);
                                }
                                catch (TileNotLoadedException) { }
                            }
                            DebugTAC_AI.Log("AIAutoPather - Failed to FindIdealStart(Water) at " + loc + ", alt " + AIEPathMapper.GetAltitudeCached(Initial));
                            return Initial;
                        }
                    default:
                        if (CalcAll(loc) <= maxDiff)
                            return Initial;
                        else
                        {
                            foreach (var item in iterateAround4)
                            {
                                posC = loc + item;
                                try
                                {
                                    if (CalcAll(posC, loc) <= maxDiff)
                                        return ToSceneHeightFast(posC);
                                }
                                catch (TileNotLoadedException) { }
                            }
                            DebugTAC_AI.Log("AIAutoPather - Failed to FindIdealStart(All) at " + loc + ", alt " + AIEPathMapper.GetAltitudeCached(Initial));
                            return Initial;
                        }
                }
            }
            catch (TileNotLoadedException)
            {
                DebugTAC_AI.Log("AIAutoPather - Failed to FindIdealStart(All) - tile the tech is in is not loaded?!?");
                return Initial;
            }
        }


        private static HashSet<IntVector2> pos = new HashSet<IntVector2>();
        private static HashSet<IntVector2> posPre = new HashSet<IntVector2>();
        private static HashSet<IntVector2> posPre2 = new HashSet<IntVector2>();
        private static List<IntVector2> IterateAroundExpand(int rad)
        {
            pos.Clear();
            pos.Add(IntVector2.zero);
            for (int step = 0; step < rad; step++)
            {
                posPre2.Clear();
                foreach (var item in posPre)
                    posPre2.Add(item);
                posPre.Clear();
                posPre.Add(IntVector2.zero);
                foreach (var item in posPre2)
                {
                    foreach (var item2 in iterationsStr)
                    {
                        var coord = item2 + item;
                        if (!pos.Contains(coord))
                        {
                            pos.Add(coord);
                            posPre.Add(coord);
                        }
                    }
                }
            }
            posPre.Clear();
            posPre2.Clear();
            return pos.ToList();
        }
        private static List<IntVector2> iterationsAll = new List<IntVector2>
        {
            new IntVector2(-1,-1),
            new IntVector2(-1,0),
            new IntVector2(-1,1),
            new IntVector2(0,-1),
            new IntVector2(0,1),
            new IntVector2(1,-1),
            new IntVector2(1,0),
            new IntVector2(1,1),
        };

        private static List<IntVector2> iterationsStr = new List<IntVector2>
        {
            new IntVector2(-1,0),
            new IntVector2(0,-1),
            new IntVector2(0,1),
            new IntVector2(1,0),
        };
        private static List<IntVector2> iterationsDia = new List<IntVector2>
        {
            new IntVector2(-1,-1),
            new IntVector2(-1,1),
            new IntVector2(1,-1),
            new IntVector2(1,1),
        };
        internal void PrintError(List<KeyValuePair<byte, IntVector2>> toCheckAlt)
        {
            try
            {
                KeyValuePair<byte, IntVector2> best = toCheckAlt.OrderBy(x => x.Key).FirstOrDefault();
                Vector3 posScene = ToSceneNoHeightCheck(best.Value);
                DebugTAC_AI.Log("AIAutoPather - Type " + waterPath.ToString() + " | CurAlt is " + CurAlt + " vs best alt " + AIEPathMapper.GetAlt(posScene, false) +
                    " | Max Difficulty " + maxDiff + " vs best possible " + best.Key + " | Obst " + AIEPathMapper.HasObst(posScene));
                PrintErrorInfoCoord(best.Value, best.Key);
                DebugTAC_AI.Log("OTHERS:");
                foreach (var item in toCheckAlt)
                {
                    if (item.Value != best.Value)
                    {
                        DebugTAC_AI.Log(item.Value + " alt " + AIEPathMapper.GetAlt(posScene, false) + " | diff " + best.Key +
                            " | obst " + AIEPathMapper.HasObst(posScene));
                        PrintErrorInfoCoord(item.Value, item.Key);
                    }
                }
            }
            catch { DebugTAC_AI.Log("PrintError no ENTRIES to report."); }
        }
        internal void PrintErrorInfoCoord(IntVector2 chunk, byte end)
        {
            Vector3 posV = ToSceneHeightFast(chunk);
            byte init = AIEPathMapper.GetDifficultyNoWater(posV, this);
            byte heading = CalcHeadingDiff(chunk, CurPos);
            byte obstActive = CalcActiveObst(posV);
            //DebugTAC_AI.Log("PrintErrorInfoCoord - " + posV +  " | initial: " + init + " | heading: " + heading + " | obst: " + obstActive + 
            //    " | total: " + end);
        }


        private static List<KeyValuePair<byte, IntVector2>> toCheck = new List<KeyValuePair<byte, IntVector2>>();
        private static List<KeyValuePair<byte, IntVector2>> toCheckAlt = new List<KeyValuePair<byte, IntVector2>>();
        private static List<KeyValuePair<byte, IntVector2>> toCheckExtra = new List<KeyValuePair<byte, IntVector2>>();
        private static List<KeyValuePair<byte, IntVector2>> toCheckExtra2 = new List<KeyValuePair<byte, IntVector2>>();
        /// <summary>
        /// For each call, paths each tile every call PathingIterationsPerCall times
        /// </summary>
        /// <returns>True if calcing, false when finished</returns>
        public override bool CalcRoute()
        {
            if (PathingUnit == null)
            {
                DebugTAC_AI.Log("AIAutoPather - Failed since PathingUnit is null");
                return false;
            }
            if (Finished)
            {
                Finished = false;
                DebugTAC_AI.Log("AIAutoPather - Stopped updating.");
                PathingUnit.OnFinishedPathfinding(null);
                return false;
            }
            toCheck.Clear();
            toCheckAlt.Clear();
            IntVector2 posC;
            byte diff;
            bool nearbyObst = false;
            for (int step = 0; step < PathingIterationsPerCall; step++)
            {
                switch (waterPath)
                {
                    case WaterPathing.AvoidWater:
                        foreach (var item in iterationsStr)
                        {
                            posC = CurPos + item;
                            if (!pathed.Contains(posC))
                            {
                                diff = CalcAvoidWater(posC, CurPos);
                                PrintErrorInfoCoord(posC, diff);
                                if (diff <= maxDiff)
                                    toCheck.Add(new KeyValuePair<byte, IntVector2>(diff, posC));
                                else
                                    nearbyObst = true;
                                toCheckAlt.Add(new KeyValuePair<byte, IntVector2>(diff, posC));
                            }
                        }
                        if (!nearbyObst)
                        {
                            toCheckExtra.Clear();
                            toCheckExtra2.Clear();
                            foreach (var item in iterationsDia)
                            {
                                posC = CurPos + item;
                                if (!pathed.Contains(posC))
                                {
                                    diff = CalcAvoidWater(posC, CurPos);
                                    PrintErrorInfoCoord(posC, diff);
                                    if (diff <= maxDiff)
                                        toCheckExtra.Add(new KeyValuePair<byte, IntVector2>(diff, posC));
                                    else
                                        nearbyObst = true;
                                    toCheckExtra2.Add(new KeyValuePair<byte, IntVector2>(diff, posC));
                                }
                            }
                            if (!nearbyObst)
                            {
                                toCheck.AddRange(toCheckExtra);
                                toCheckAlt.AddRange(toCheckExtra2);
                            }
                        }
                        break;
                    case WaterPathing.AllowWater:
                        foreach (var item in iterationsStr)
                        {
                            posC = CurPos + item;
                            if (!pathed.Contains(posC))
                            {
                                diff = CalcAll(posC, CurPos);
                                if (diff <= maxDiff)
                                    toCheck.Add(new KeyValuePair<byte, IntVector2>(diff, posC));
                                else
                                    nearbyObst = true;
                                toCheckAlt.Add(new KeyValuePair<byte, IntVector2>(diff, posC));
                            }
                        }
                        if (!nearbyObst)
                        {
                            toCheckExtra.Clear();
                            toCheckExtra2.Clear();
                            foreach (var item in iterationsDia)
                            {
                                posC = CurPos + item;
                                if (!pathed.Contains(posC))
                                {
                                    diff = CalcAll(posC, CurPos);
                                    if (diff <= maxDiff)
                                        toCheckExtra.Add(new KeyValuePair<byte, IntVector2>(diff, posC));
                                    else
                                        nearbyObst = true;
                                    toCheckExtra2.Add(new KeyValuePair<byte, IntVector2>(diff, posC));
                                }
                            }
                            if (!nearbyObst)
                            {
                                toCheck.AddRange(toCheckExtra);
                                toCheckAlt.AddRange(toCheckExtra2);
                            }
                        }
                        break;
                    case WaterPathing.StayInWater:
                        foreach (var item in iterationsStr)
                        {
                            posC = CurPos + item;
                            if (!pathed.Contains(posC))
                            {
                                diff = CalcWaterOnly(posC, CurPos);
                                if (diff <= maxDiff)
                                    toCheck.Add(new KeyValuePair<byte, IntVector2>(diff, posC));
                                else
                                    nearbyObst = true;
                                toCheckAlt.Add(new KeyValuePair<byte, IntVector2>(diff, posC));
                            }
                        }
                        if (!nearbyObst)
                        {
                            toCheckExtra.Clear();
                            toCheckExtra2.Clear();
                            foreach (var item in iterationsDia)
                            {
                                posC = CurPos + item;
                                if (!pathed.Contains(posC))
                                {
                                    diff = CalcWaterOnly(posC, CurPos);
                                    if (diff <= maxDiff)
                                        toCheckExtra.Add(new KeyValuePair<byte, IntVector2>(diff, posC));
                                    else
                                        nearbyObst = true;
                                    toCheckExtra2.Add(new KeyValuePair<byte, IntVector2>(diff, posC));
                                }
                            }
                            if (!nearbyObst)
                            {
                                toCheck.AddRange(toCheckExtra);
                                toCheckAlt.AddRange(toCheckExtra2);
                            }
                        }
                        break;
                }


                if (toCheck.Count == 0)
                {
                    if (deadEnds >= maxDeadEndsTillFail)
                    {
                        if (endPrematureDebug)
                        {
                            DebugTAC_AI.Assert("AIAutoPather - Failed since maxDeadEndsTillFail [" + maxDeadEndsTillFail + "] was reached");
                            PrintError(toCheckAlt);
                        }
                        PathingUnit.OnFinishedPathfinding(null);
                        return false;
                    }
                    if (PathRoute.Count > 1)
                    {
                        PathRoute.RemoveAt(PathRoute.Count - 1);
                        CurPos = PathRoute.Last();
                        CurAlt = AIEPathMapper.GetAlt(ToSceneNoHeightCheck(CurPos), false);
                        deadEnds++;
                    }
                    else
                    {
                        if (endPrematureDebug)
                        {
                            DebugTAC_AI.Log("AIAutoPather - Failed since dead end and no previous routes at " + CurPos + ", alt " + CurAlt);
                            PrintError(toCheckAlt);
                        }
                        PathingUnit.OnFinishedPathfinding(null);
                        return false;
                    }
                }
                else
                {
                    KeyValuePair<byte, IntVector2> best = toCheck.OrderBy(x => x.Key).FirstOrDefault();
                    CurPos = best.Value;
                    CurAlt = AIEPathMapper.GetAlt(ToSceneNoHeightCheck(CurPos), false);
                    if (SpamNumbers)
                    {
                        if (best.Key < BaseDifficulty + (WrongHeadingDifficultyAddition * 2))
                            AIGlobals.PopupAllyInfo(pathed.Count.ToString(), WorldPosition.FromScenePosition(ToSceneHeightFast(CurPos)));
                        else if (best.Key == AIEPathMapper.maxAltByte)
                            AIGlobals.PopupEnemyInfo(pathed.Count.ToString(), WorldPosition.FromScenePosition(ToSceneHeightFast(CurPos)));
                        else
                            AIGlobals.PopupSubNeutralInfo(pathed.Count.ToString() + " | " + best.Key, WorldPosition.FromScenePosition(ToSceneHeightFast(CurPos)));
                    }
                    pathed.Add(CurPos);
                    if (pathed.Count >= maxPathedTillFail)
                    {
                        if (endPrematureDebug)
                        {
                            DebugTAC_AI.Assert(true, "AIAutoPather - Failed since maxPathedTillFail [" + maxPathedTillFail + "] was reached");
                            PrintError(toCheckAlt);
                        }
                        PathingUnit.OnFinishedPathfinding(null);
                        return false;
                    }
                    PathRoute.Add(CurPos);
                    bool byEnd = false;
                    if (CurPos != EndPos)
                    {
                        foreach (var item in iterationsAll)
                        {
                            if (EndPos + item == CurPos)
                            {
                                byEnd = true;
                                break;
                            }
                        }
                    }
                    else
                        byEnd = true;
                    if (byEnd)
                    {
                        TryShorten(ref PathRoute);
                        Finished = true;
                        Success = true;
                        DebugTAC_AI.LogPathing("AIAutoPather - Success with " + PathRoute.Count + " points and " + pathed.Count + " attempts.");
                        GetPath(submitCache);
                        PathingUnit.OnFinishedPathfinding(submitCache);
                        submitCache.Clear();
                        return false;
                    }
                    else if (PathRoute.Count >= MinPointsToConsiderEarlyRoute && PathingUnit.IsRunningLowOnPathPoints())
                    {
                        PushSomeToPather();
                        return true;
                    }
                }
                toCheck.Clear();
            }
            //DebugTAC_AI.Log("AIAutoPather - Pathing attempt with " + pathed.Count);
            return true;
        }

        private static List<IntVector2> partialCache = new List<IntVector2>();
        private static List<WorldPosition> submitCache = new List<WorldPosition>();
        private void PushSomeToPather()
        {
            for (int step2 = 0; step2 < PointsToSendEarlyRoute; step2++)
            {
                var posD = PathRoute[0];
                PathRoute.RemoveAt(0);
                partialCache.Add(posD);
            }
            //StartPos = PathRoute.Last();
            TryShorten(ref partialCache);
            DebugTAC_AI.LogPathing("AIAutoPather - Partial path with " + partialCache.Count + " points and " + pathed.Count + " attempts so far.");
            GetPathEarly(partialCache, submitCache);
            partialCache.Clear();
            PathingUnit.OnPartialPathfinding(submitCache);
            submitCache.Clear();
        }
        private void WaitForResults(AIEAutoPather2D pathMain)
        {
            if (!pathMain.Finished)
            {
                RecalcManual();
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
                DebugTAC_AI.Log("AIAutoPather - Merged routes with same destination at " + indexCopycat + " points of it's total " + pathMain.PathRoute.Count + " points.");

                TryShorten(ref PathRoute);
                Finished = true;
                Success = true;
                DebugTAC_AI.Log("AIAutoPather - Success with " + PathRoute.Count + " points and " + pathed.Count + " attempts.");
                GetPath(submitCache);
                PathingUnit.OnFinishedPathfinding(submitCache);
                submitCache.Clear();
            }
        }

        private Vector3 ToSceneNoHeightCheck(IntVector2 local)
        {
            return ((Vector2)local * MoveGridScale).ToVector3XZ() + CenterPos.ScenePosition;
        }
        private Vector3 ToSceneHeightFast(IntVector2 local)
        {
            Vector3 posNoHeight = ToSceneNoHeightCheck(local);
            return posNoHeight.SetY(AIEPathMapper.GetAltitudeCached(posNoHeight));
        }
        private IntVector2 ToLocal(Vector3 pos)
        {
            return (pos.ToVector2XZ() - CenterPos.ScenePosition.ToVector2XZ()) / MoveGridScale;
        }

        public override byte GetDifficultyFromAlt(byte alt)
        {
            int modifier;
            if (alt > CurAlt)
                modifier = Mathf.FloorToInt(Mathf.Max(0, alt - CurAlt) * (TerrainSlopeClimbPenaltyMulti / MoveGridScale));
            else
                modifier = Mathf.FloorToInt(Mathf.Max(0, CurAlt - alt) * (TerrainSlopeFallPenaltyMulti / MoveGridScale));
            return (byte)Mathf.Clamp(modifier + BaseDifficulty, 0, 128);
        }
        private byte CalcHeadingDiff(IntVector2 posNext, IntVector2 posCur)
        {
            Vector2 idealHeading = ((Vector2)(posNext - posCur)).normalized;
            Vector2 currHeading = ((Vector2)(EndPos - posCur)).normalized;
            return (byte)Mathf.FloorToInt(-((Vector2.Dot(idealHeading, currHeading) - 1) * WrongHeadingDifficultyAdditionHalf));
        }
        private byte CalcActiveObst(Vector3 posScene)
        {
            foreach (var item in ManVisible.inst.VisiblesTouchingRadius(posScene, MoveGridScale, AIGlobals.crashBitMask))
            {
                if (item.isActive)
                {
                    if (item.tank)
                    {
                        if (item.tank.IsAnchored)
                        {
                            //DebugTAC_AI.Log("CalcActiveObst - Obstructed by Anchored Tech " + item.tank.name + " at " + posScene);
                            return ObsticleDifficultyAddition;
                        }
                    }
                    else
                    {
                        //DebugTAC_AI.Log("CalcActiveObst - Obstructed by Scenery at " + posScene);
                        return ObsticleDifficultyAddition;
                    }
                }
            }
            return 0;
        }
        private byte CalcAll(IntVector2 pos, IntVector2 posCur)
        {
            //int diff = AIEPathMapper.GetDifficultyWaterInv(Center.ScenePosition + (Vector3)pos.ToVector3XZ() * MoveGridScale, this);
            Vector3 posV = ToSceneNoHeightCheck(pos);
            int diff = AIEPathMapper.GetDifficulty(posV, this);
            diff += CalcHeadingDiff(pos, posCur);
            diff += CalcActiveObst(posV);
            return (byte)Mathf.Clamp(diff, 1, AIEPathMapper.maxAltByte);
        }
        private byte CalcAll(IntVector2 pos)
        {
            //int diff = AIEPathMapper.GetDifficultyWaterInv(Center.ScenePosition + (Vector3)pos.ToVector3XZ() * MoveGridScale, this);
            Vector3 posV = ToSceneHeightFast(pos);
            int diff = AIEPathMapper.GetDifficulty(posV, this);
            diff += CalcActiveObst(posV);
            return (byte)Mathf.Clamp(diff, 1, AIEPathMapper.maxAltByte);
        }

        private byte CalcAvoidWater(IntVector2 pos, IntVector2 posCur)
        {
            Vector3 posV = ToSceneHeightFast(pos);
            //DebugTAC_AI.Log("Pos" + pos + ", scene " + posV + " alt - " + CurAlt + " vs " + AIEPathMapper.GetAlt(posV));
            byte diff = AIEPathMapper.GetDifficultyNoWater(posV, this);
            //DebugTAC_AI.Log("byte - " + diff);
            diff += CalcHeadingDiff(pos, posCur);
            //DebugTAC_AI.Log("byte - " + diff);
            diff += CalcActiveObst(posV);
            //DebugTAC_AI.Assert("Clamp failed on byte as " + diff + " vs " + Mathf.Clamp(diff, 0, AIEPathMapper.maxAltByte));
            return (byte)Mathf.Clamp(diff, 1, AIEPathMapper.maxAltByte);
        }
        private byte CalcAvoidWater(IntVector2 pos)
        {
            Vector3 posV = ToSceneHeightFast(pos);
            int diff = AIEPathMapper.GetDifficultyNoWater(posV, this);
            diff += CalcActiveObst(posV);
            return (byte)Mathf.Clamp(diff, 1, AIEPathMapper.maxAltByte);
        }
        private byte CalcWaterOnly(IntVector2 pos, IntVector2 posCur)
        {
            Vector3 posV = ToSceneHeightFast(pos);
            int diff = AIEPathMapper.GetDifficultyWater(posV, this);
            diff += CalcHeadingDiff(pos, posCur);
            diff += CalcActiveObst(posV);
            return (byte)Mathf.Clamp(diff, 1, AIEPathMapper.maxAltByte);
        }
        private byte CalcWaterOnly(IntVector2 pos)
        {
            Vector3 posV = ToSceneHeightFast(pos);
            int diff = AIEPathMapper.GetDifficultyWater(posV, this);
            diff += CalcActiveObst(posV);
            return (byte)Mathf.Clamp(diff, 1, AIEPathMapper.maxAltByte);
        }
        public override bool CanGetPath(int minCount = 0)
        {
            return PathRoute.Count > minCount;
        }
        public override void GetPath(List<WorldPosition> cache, bool GetAccurateAlt = false)
        {
            if (PathRoute.Count == 0)
            {
                return;
                //return new List<WorldPosition>() { WorldPosition.FromScenePosition(EndPosWP.ScenePosition + (Vector3.up * 2)) };
            }
            //throw new Exception("AIAutoPather - GetPath returned no valid points for pathfinding");
            if (GetAccurateAlt)
            {
                foreach (var item in PathRoute)
                {
                    cache.Add(WorldPosition.FromScenePosition(ManWorld.inst.ProjectToGround(ToSceneNoHeightCheck(item), true) + (Vector3.up * 2)));
                }
            }
            else
            {
                foreach (var item in PathRoute)
                {
                    cache.Add(WorldPosition.FromScenePosition(ToSceneHeightFast(item) + (Vector3.up * 2)));
                }
            }
            foreach (var item in cache)
            {
                DebugTAC_AI.Assert(item.ScenePosition.IsNaN(), "GetPath RETURNED NaN");
            }
        }
        public void GetPathEarly(List<IntVector2> pathIn, List<WorldPosition> cache, bool GetAccurateAlt = false)
        {
            if (pathIn.Count == 0)
            {
                //return new List<WorldPosition>() { WorldPosition.FromScenePosition(
                //    ((Vector2)GetWorldPathEnd()).ToVector3XZ(CenterPos.ScenePosition.y) + (Vector3.up * 2)) };
                return;
            }
            if (GetAccurateAlt)
            {
                foreach (var item in pathIn)
                {
                    cache.Add(WorldPosition.FromScenePosition(ManWorld.inst.ProjectToGround(ToSceneNoHeightCheck(item), true) + (Vector3.up * 2)));
                }
            }
            else
            {
                foreach (var item in pathIn)
                {
                    cache.Add(WorldPosition.FromScenePosition(ToSceneHeightFast(item) + (Vector3.up * 2)));
                }
            }
            foreach (var item in cache)
            {
                DebugTAC_AI.Assert(item.ScenePosition.IsNaN(), "GetPathEarly RETURNED NaN");
            }
        }

        public override bool CanContinue()
        {
            if (PathRoute.Count >= MinPointsToConsiderEarlyRoute && PathingUnit.IsRunningLowOnPathPoints())
            {
                PushSomeToPather();
                return true;
            }
            IntVector2 pos = WorldPosition.FromScenePosition(ToSceneNoHeightCheck(CurPos)).TileCoord;
            IntVector2 min = pos - IntVector2.one;
            IntVector2 max = pos + IntVector2.one;
            foreach (var item in ManWorld.inst.TileManager.IterateTiles(min, max))
            {
                if (item == null)
                    return false;
            }
            return true;
        }

        private static Dictionary<IntVector2, int> posssss = new Dictionary<IntVector2, int>();
        private void TryShorten(ref List<IntVector2> PathRoute)
        {
            if (PathRoute.Count == 0)
            {
                return;
                //throw new Exception("AIAutoPather - TryShorten returned no valid points for pathfinding");
            }
            int removed = 1;
            // Remove loopbacks
            int index = 0;
            posssss.Clear();
            foreach (var item in PathRoute)
            {
                posssss.Add(item, index);
                index++;
            }
            for (int step = 4; step < PathRoute.Count; step++)
            {
                IntVector2 posCheck = PathRoute[step];
                foreach (var item in iterationsAll)
                {
                    var pos = posCheck + item;
                    if (posssss.TryGetValue(pos, out int indexGet) && indexGet < step - 1)
                    {
                        for (int step2 = indexGet + 1; step2 < step; step2++)
                        {
                            PathRoute.RemoveAt(indexGet + 1);
                            step--;
                            removed++;
                        }
                        posssss.Clear();
                        index = 0;
                        foreach (var item2 in PathRoute)
                        {
                            posssss.Add(item2, index);
                            index++;
                        }
                    }
                }
            }
            // Remove straight extra points
            for (int step = 0; step < PathRoute.Count - 2; step++)
            {
                IntVector2 posCheck = PathRoute[step];
                IntVector2 posNext = PathRoute[step + 1];
                IntVector2 moveDelta = posNext - posCheck;
                int ChainRemoveStep = 0;
                while (step < PathRoute.Count - 2 && PathRoute[step + 2] - PathRoute[step + 1] == moveDelta 
                    && MaxPointsInLineToRemove > ChainRemoveStep)
                {
                    PathRoute.RemoveAt(step + 1);
                    removed++;
                    ChainRemoveStep++;
                }
            }
            /*
            // Remove corner bend points and longer straights
            for (int step = 2; step < PathRoute.Count; step++)
            {
                if (((Vector2)(PathRoute[step] - PathRoute[step - 2])).sqrMagnitude == 16) // 4 * 4
                {
                    PathRoute.RemoveAt(step - 1);
                    removed++;
                }
            }*/
            PathRoute.RemoveAt(0);
            //DebugTAC_AI.Log("AIAutoPather - TryShorten has removed " + removed + " entries");
            //throw new NotImplementedException("AIAutoPather - TryShorten is incomplete");
        }

    }
}
