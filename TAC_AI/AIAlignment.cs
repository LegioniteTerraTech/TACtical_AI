using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TAC_AI
{
    public enum AIAlignment
    {
        Static,
        Player,
        NonPlayer,
        Neutral,
    }
    public enum AIWeaponState
    { // 0 is sleep, 1 is target, 2 is obsticle, 3 is mimic
        Off,
        Enemy,
        Obsticle,
        Mimic,
    }
    public enum AIDriveState
    {
        None,
        Driving,
        NonPlayer,
        Neutral,
    }
}
