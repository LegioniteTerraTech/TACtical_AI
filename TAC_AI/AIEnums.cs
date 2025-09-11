using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TAC_AI.AI
{
    public struct EControlOperatorSet
    {
        public EDriveDest DriveDest;
        public EDriveFacing DriveDir;
        private Vector3 lastDest;
        public Vector3 lastDestination => lastDest;
        public void SetLastDest(Vector3 posScene)
        {
            if (posScene.IsNaN())
                DebugTAC_AI.Exception("EControlOperatorSet - lastDestination was NaN!");
            lastDest = posScene;
        }

        public static EControlOperatorSet Default => new EControlOperatorSet(EDriveDest.None, EDriveFacing.Stop);

        private EControlOperatorSet(EDriveDest move, EDriveFacing facing)
        {
            DriveDest = move;
            DriveDir = facing;
            lastDest = Vector3.zero;
        }
        internal EControlOperatorSet(EControlOperatorSet prev)
        {
            DriveDest = EDriveDest.None;
            DriveDir = EDriveFacing.Stop;
            lastDest = prev.lastDestination;
        }


        public void STOP(TankAIHelper helper)
        {
            DriveDest = EDriveDest.None;
            DriveDir = EDriveFacing.Stop;
            helper.DriveVar = 0;
        }
        public void Forwards(TankAIHelper helper)
        {
            DriveDest = EDriveDest.Override;
            DriveDir = EDriveFacing.Forwards;
            helper.ThrottleState = AIThrottleState.ForceSpeed;
            helper.DriveVar = 1;
        }
        public void Reverse(TankAIHelper helper)
        {
            DriveDest = EDriveDest.Override;
            DriveDir = EDriveFacing.Forwards;
            helper.ThrottleState = AIThrottleState.ForceSpeed;
            helper.DriveVar = -1;
        }


        public void ResetActions()
        {
            DriveDest = EDriveDest.None;
            DriveDir = EDriveFacing.Stop;
        }

        public void FaceDest()
        {
            DriveDest = EDriveDest.ToLastDestination;
            DriveDir = EDriveFacing.Neutral;
        }
        public void DriveToFacingTowards()
        {
            DriveDest = EDriveDest.ToLastDestination;
            DriveDir = EDriveFacing.Forwards;
        }
        public void DriveToFacingBackwards()
        {
            DriveDest = EDriveDest.ToLastDestination;
            DriveDir = EDriveFacing.Backwards;
        }

        public void DriveAwayFacingTowards()
        {
            DriveDest = EDriveDest.FromLastDestination;
            DriveDir = EDriveFacing.Forwards;
        }
        public void DriveAwayFacingAway()
        {
            DriveDest = EDriveDest.FromLastDestination;
            DriveDir = EDriveFacing.Backwards;
        }

        public void DriveToFacingPerp()
        {
            DriveDest = EDriveDest.ToLastDestination;
            DriveDir = EDriveFacing.Perpendicular;
        }
        public void DriveAwayFacingPerp()
        {
            DriveDest = EDriveDest.FromLastDestination;
            DriveDir = EDriveFacing.Perpendicular;
        }
    }
    public struct EControlCoreSet
    {
        public EDriveDest DriveDest
        {
            get => _DriveDest;
            set {
                //DriveDestBacktrace.Append(" - case " + StackTraceUtility.ExtractStackTrace() + "\n");
                _DriveDest = value;
            }
        }
        public EDriveFacing DriveDir
        {
            get => _DriveDir;
            set
            {
                //DriveDirBacktrace.Append(" - case " + StackTraceUtility.ExtractStackTrace() + "\n");
                _DriveDir = value;
            }
        }
        /// <summary>The final destination, not the point we are driving to!</summary>
        public Vector3 lastDestination
        {
            get => lastDest;
            set
            {
                if (value.IsNaN())
                    DebugTAC_AI.Exception("EControlCoreSet.SetLastDest - lastDestination was NaN!");
                lastDest = value;
            }
        }
        private Vector3 lastDest;
        private EDriveDest _DriveDest;
        private EDriveFacing _DriveDir;
        public EDrivePathing DrivePathing;
        public ESteeringStrength TurningStrictness { get; set; }
        //public StringBuilder DriveDestBacktrace;
        //public StringBuilder DriveDirBacktrace;

        public static EControlCoreSet Default => new EControlCoreSet(EDriveDest.None, EDriveFacing.Stop);

        public EControlCoreSet(EControlOperatorSet direct)
        {
            _DriveDest = direct.DriveDest;
            _DriveDir = direct.DriveDir;
            DrivePathing = EDrivePathing.OnlyImmedeate;
            lastDest = direct.lastDestination;
            TurningStrictness = ESteeringStrength.Lazy;
            //DriveDestBacktrace = new StringBuilder();
            //DriveDirBacktrace = new StringBuilder();
        }
        private EControlCoreSet(EDriveDest move, EDriveFacing facing)
        {
            _DriveDest = move;
            _DriveDir = facing;
            DrivePathing = EDrivePathing.OnlyImmedeate;
            lastDest = Vector3.zero;
            TurningStrictness = ESteeringStrength.Lazy;
            //DriveDestBacktrace = new StringBuilder();
            //DriveDirBacktrace = new StringBuilder();
        }
        public void MergePrevCommands(EControlOperatorSet direct)
        {
            if (DriveDest == EDriveDest.None)
                DriveDest = direct.DriveDest;
            if (DriveDir == EDriveFacing.Stop)
                DriveDir = direct.DriveDir;
        }
        public void Stop()
        {
            DriveDest = EDriveDest.None;
            DriveDir = EDriveFacing.Stop;
        }
        public void NoBrakes()
        {
            DriveDest = EDriveDest.None;
            DriveDir = EDriveFacing.Neutral;
        }

        public void DriveToFacingTowards()
        {
            DriveDest = EDriveDest.ToLastDestination;
            DriveDir = EDriveFacing.Forwards;
        }
        public void DriveToFacingBackwards()
        {
            DriveDest = EDriveDest.ToLastDestination;
            DriveDir = EDriveFacing.Backwards;
        }
        public void FlagBusyUnstucking()
        {
            DriveDest = EDriveDest.AvoidenceActive;
            DriveDir = EDriveFacing.Neutral;
        }

        public void DriveAwayFacingTowards()
        {
            DriveDest = EDriveDest.FromLastDestination;
            DriveDir = EDriveFacing.Forwards;
        }
        public void DriveAwayFacingAway()
        {
            DriveDest = EDriveDest.FromLastDestination;
            DriveDir = EDriveFacing.Backwards;
        }

        public void DriveToFacingPerp()
        {
            DriveDest = EDriveDest.ToLastDestination;
            DriveDir = EDriveFacing.Perpendicular;
        }
        public void DriveAwayFacingPerp()
        {
            DriveDest = EDriveDest.FromLastDestination;
            DriveDir = EDriveFacing.Perpendicular;
        }

        public override string ToString()
        {
            return "EControlCoreSet - " + DriveDest.ToString() + " | " + DriveDir.ToString() + " | " + lastDestination;
            /*
            return "EControlCoreSet - " + DriveDest.ToString() + " = " + DriveDestBacktrace.ToString() + 
                " | " + DriveDir.ToString() + " = " + DriveDirBacktrace.ToString()  + " | " + lastDestination;*/
        }
    }

    /// <summary>
    /// What the AI does when attacking
    /// </summary>
    public enum EAttackMode
    {
        AutoSet,
        /// <summary> Circle the enemy while shooting at them
        /// <para>Use for: Skirmishers, Mid-long-ranged units with fast turrets [GSO Gigaton, VEN Rapid Cannon, HE HG Cannon, BF Arc Missiles, RR Sonic Blaster TAC Terminator] </para>
        /// <para>!! Only active with TweakTech or WeaponAimMod (because no target leading) !! </para></summary>
        Circle,
        /// <summary> Chase last assailant head-on until death regardless of range [default for SuicideMissile]
        /// <para>Use for: Homing missiles, Dualling units, Eradicators hellbent on removing the player from existance</para> </summary>
        Chase,
        /// <summary> Attack the weakest tech in range. 
        /// <para>Use for: Riots, Area Denial</para> </summary>
        Strong,
        /// <summary> Attack random techs. 
        /// <para>Use for: Interceptors, Raiders</para> </summary>
        Random,
        /// <summary> Attack player from afar because we are a F^bro-fracker. 
        /// <para>Use for: Artillery, Motherships</para> </summary>
        Ranged,
        /// <summary> Avoid danger.  
        /// <para>Use for: Any Non-Combat Tech </para></summary>
        Safety
    }

    /// <summary>
    /// We can only be going to one location at once!
    /// </summary>
    public enum EDriveDest
    {   //Control the AI drive direction
        None, // No target

        // Coordinate-Based Targets
        /// <summary>
        /// Drive away from target
        /// </summary>
        FromLastDestination,

        /// <summary>
        /// Drive to target
        /// </summary>
        ToLastDestination,

        /// <summary>
        /// The avoidence system is trying to unstuck tech
        /// </summary>
        AvoidenceActive,


        // Dynamically Changing Targets
        /// <summary>
        /// Counts also as [recharge home, block rally]
        /// </summary>
        ToBase,

        /// <summary>
        /// Counts also as [loose block, target enemy, target to charge]
        /// </summary>
        ToMine,

        /// <summary>
        /// Allows ForceSetDrive to pass through unhindered
        /// </summary>
        Override
    }

    public enum ESteeringStrength
    {   //Control the AI drive steering
        Lazy,
        Strict,
        MaxSteering
    }
    /// <summary>
    /// Facing towards destination, regardless of drive direction
    /// </summary>
    public enum EDriveFacing
    {   //Control the AI drive direction
        Stop,
        Neutral,
        Forwards,
        Perpendicular,
        Backwards
    }
    public enum EDrivePathing
    {
        IgnoreAll,
        OnlyImmedeate,
        Path,
        PrecisePathIgnoreScenery,
        PrecisePath,
    }
    public enum RequestSeverity
    {
        ThinkMcFly,
        Warn,
        SameTeam,
        AllHandsOnDeck,
    }
}
