using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TAC_AI
{
    [Flags]
    public enum AIEnabledModes : byte
    {
        None = 0,
        Assassin = 1,
        Aegis = 2,
        Prospector = 4,
        Scrapper = 8,
        Energizer = 16,
        Aviator = 32,
        Astrotech = 64,
        Buccaneer = 128,
        All = 255,
    }
    /*
            public bool isAegisAvail = false;       //Is there an Aegis-enabled AI on this tech?

            public bool isProspectorAvail = false;  //Is there a Prospector-enabled AI on this tech?
            public bool isScrapperAvail = false;    //Is there a Scrapper-enabled AI on this tech?
            public bool isEnergizerAvail = false;   //Is there a Energizer-enabled AI on this tech?

            public bool isAviatorAvail = false;
            public bool isAstrotechAvail = false;
            public bool isBuccaneerAvail = false;
     */
}
