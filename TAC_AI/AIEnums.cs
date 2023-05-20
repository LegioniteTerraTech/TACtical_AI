using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TAC_AI.AI
{
    public struct EControlOperatorSet
    {
        public EDriveDest DriveDest;
        public EDriveFacing DriveDir;
        public Vector3 lastDestination
        {
            get => lastDest;
            set
            {
                if (value.IsNaN())
                    DebugTAC_AI.Exception("EControlOperatorSet - lastDestination was NaN!");
                lastDest = value;
            }
        }
        private Vector3 lastDest;

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
        public Vector3 lastDestination
        {
            get => lastDest;
            set
            {
                if (value.IsNaN())
                    DebugTAC_AI.Exception("EControlCoreSet - lastDestination was NaN!");
                lastDest = value;
            }
        }
        private Vector3 lastDest;
        private EDriveDest _DriveDest;
        private EDriveFacing _DriveDir;
        public EDrivePathing DrivePathing;
        public bool StrictTurning { get; set; }
        //public StringBuilder DriveDestBacktrace;
        //public StringBuilder DriveDirBacktrace;

        public static EControlCoreSet Default => new EControlCoreSet(EDriveDest.None, EDriveFacing.Stop);

        public EControlCoreSet(EControlOperatorSet direct)
        {
            _DriveDest = direct.DriveDest;
            _DriveDir = direct.DriveDir;
            DrivePathing = EDrivePathing.OnlyImmedeate;
            lastDest = direct.lastDestination;
            StrictTurning = false;
            //DriveDestBacktrace = new StringBuilder();
            //DriveDirBacktrace = new StringBuilder();
        }
        private EControlCoreSet(EDriveDest move, EDriveFacing facing)
        {
            _DriveDest = move;
            _DriveDir = facing;
            DrivePathing = EDrivePathing.OnlyImmedeate;
            lastDest = Vector3.zero;
            StrictTurning = false;
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
        Circle,     // Circle the enemy while shooting at them
        // !! Only active with TweakTech or WeaponAimMod (because no target leading) !!
        // Use for: Skirmishers, Mid-long-ranged units with fast turrets [GSO Gigaton, VEN Rapid Cannon, HE HG Cannon, BF Arc Missiles, RR Sonic Blaster TAC Terminator]
        Chase,     // Chase last assailant head-on until death regardless of range [default for SuicideMissile]
        // Use for: Homing missiles, Dualling units, Eradicators hellbent on removing the player from existance
        Strong,      // Attack the weakest tech in range
        // Use for: Riots, Area Denial
        Random,   // Attack random techs
        // Use for: Interceptors, Raiders
        Ranged,     // Attack player from afar because we are a F^bro-fracker
        // Use for: Artillery, Motherships
        Safety,     // Avoid danger
        // Use for: Any Non-Combat Tech
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


        // Dynamically Changing Targets
        /// <summary>
        /// Counts also as [recharge home, block rally]
        /// </summary>
        ToBase,

        /// <summary>
        /// Counts also as [loose block, target enemy, target to charge]
        /// </summary>
        ToMine
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
        PathInv,
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
