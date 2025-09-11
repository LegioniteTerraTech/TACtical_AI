using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TAC_AI.AI.Movement
{
    public enum WaterPathing
    {
        AvoidWater,
        AllowWater,
        StayInWater
    }
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
        AIEAutoPather Pathfinder { get; set; }
        /// <summary> Do pathfinding in 3D space - will cost more performance! </summary>
        bool Do3DPathing { get; }
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
        bool IsRunningLowOnPathPoints();
        void OnPartialPathfinding(List<WorldPosition> pos);
        void OnFinishedPathfinding(List<WorldPosition> pos);
    }
    public static class IPathfindableExtensions
    {
        public static void SetAutoPathfinding(this IPathfindable pathable, bool active)
        {
            if (active != pathable.AutoPathfind)
            {
                if (!AIEPathMapper.EnableAdvancedPathing)
                    return;
                pathable.AutoPathfind = active;
                if (active)
                    AIEPathMapper.autoPathers.Add(pathable);
                else
                {
                    if (AIEPathMapper.autoPathers.Remove(pathable))
                        pathable.StopPathfinding();
                }
            }
        }
        /// <summary>
        /// Updates Pathfinding
        /// </summary>
        /// <param name="pathable"></param>
        /// <returns></returns>
        public static bool StartPathfind(this IPathfindable pathable)
        {
            if (pathable.Do3DPathing)
                return AIEAutoPather3D.DoPathfinding(ref pathable);
            else
                return AIEAutoPather2D.DoPathfinding(ref pathable);
        }
        public static bool StopPathfinding(this IPathfindable pathable)
        {
            return AIEAutoPather.CancelPathfinding(ref pathable);
        }
    }
    public abstract class AIEAutoPather
    {
        protected static bool endPrematureDebug = false;
        public const int DefaultMaxDifficulty = 48;
        public const int BaseDifficulty = 1;
        public const int WrongHeadingDifficultyAddition = 16;
        public const int WrongHeadingDifficultyAdditionHalf = WrongHeadingDifficultyAddition / 2;
        public const int ObsticleDifficultyAddition = DefaultMaxDifficulty;
        [Range(0.1f, 50f)]
        public const float TerrainSlopeMaxClimbPerUnit = 1.0f;
        [Range(0.1f, 50f)]
        public const float TerrainSlopeMaxDropPerUnit = 6.25f;
        public const float TerrainSlopeClimbPenaltyMulti = DefaultMaxDifficulty / TerrainSlopeMaxClimbPerUnit;
        public const float TerrainSlopeFallPenaltyMulti = TerrainSlopeClimbPenaltyMulti;//DefaultMaxDifficulty / TerrainSlopeMaxDropPerUnit;
        public const float maxDeadEndsTillFailMulti = 2f;
        public const float maxPathedTillFailDistMulti = 8f;
        public const int MaxPointsInLineToRemove = 4;
        public const int MinPointsToConsiderEarlyRoute = 16;
        public const int PointsToSendEarlyRoute = 8;

        public const float PathingRadiusMulti = 3f;
        public static int PathingIterationsPerCall = 2;
        public static int PathfindBeyondDistBox = 8;

        internal static bool SpamNumbers = false;

        internal static bool IsFarEnough(Vector3 start, Vector3 end)
        {
            Vector3 pos = start - end;
            return pos.x > PathfindBeyondDistBox || pos.x < -PathfindBeyondDistBox
                 || pos.z > PathfindBeyondDistBox || pos.z < -PathfindBeyondDistBox;
        }

        public readonly IPathfindable PathingUnit;
        public WaterPathing waterPath;
        internal bool IsRegistered = false;
        public byte maxDiff;
        public bool Finished = false;
        public bool IsFinished => Finished;
        public bool Success = false;
        public bool IsSuccessful => Success;

        public float MoveGridScale;
        protected int deadEnds = 0;
        protected int maxDeadEndsTillFail;
        protected int maxPathedTillFail;

        public WorldPosition StartPosWP { get; protected set; }
        public WorldPosition CenterPos { get; protected set; }
        public WorldPosition EndPosWP { get; protected set; }
        public byte CurAlt;

        protected AIEAutoPather(IPathfindable pathable)
        {
            PathingUnit = pathable;
        }

        public static bool CancelPathfinding(ref IPathfindable pathable)
        {
            if (pathable.Pathfinder != null)
            {
                //DebugTAC_AI.Log("AIAutoPather - Cancelled pathfinding.");
                if (!AIEPathMapper.StopPather(pathable.Pathfinder))
                {
                    //DebugTAC_AI.Log("AIAutoPather - Cancelled pathfinding but it was already not active?!");
                }
                pathable.Pathfinder.Finished = true;
                pathable.Pathfinder = null;
            }
            return true;
        }

        public abstract byte GetDifficultyFromAlt(byte alt); // 128 max


        public virtual void Recalc(Vector3 startPosScene, Vector3 endPosScene)
        {
            if (!IsRegistered)
                AIEPathMapper.RegisterPather(this);
        }
        public abstract bool CanGetPath(int minCount = 0);
        public abstract void GetPath(List<WorldPosition> pathCache, bool GetAccurateAlt = false);

        public abstract bool CalcRoute();
        public abstract bool CanContinue();
    }

}
