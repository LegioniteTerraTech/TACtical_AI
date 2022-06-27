using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TAC_AI
{
    public enum FactionTypesExt
	{				
		// No I do not make any of the corps below (exclusing some of TAC and EFF) 
		//  - but these are needed to allow the AI to spawn the right bases with 
		//    the right block ranges
		// OFFICIAL
		NULL,	// not a corp, really, probably the most unique of all lol
		GSO,	// Galactic Survey Organization
		GC,		// GeoCorp
		EXP,	// Reticule Research
		VEN,	// VENture
		HE,		// HawkEye
		SPE,	// Special
		BF,		// Better Future
		SJ,		// Space Junkers
		LEG,    // Legion

		// Below is currently mostly unused as Custom Corps already address this.
		// Community
		AER = 256,    // Aerion
		BL = 257,     // Black Labs (EXT OF HE)
		CC = 258,     // CrystalCorp
		DC = 259,     // DEVCorp
		DL = 260,     // DarkLight
		EYM = 261,    // Ellydium
		GT = 262,     // GreenTech
		HS = 263,     // Hyperion Systems
		IEC = 264,    // Independant Earthern Colonies
		LK = 265,     // Lemon Kingdom
		OS = 266,     // Old Stars
		TC = 267,     // Tofuu Corp
		TAC = 268,    // Technocratic AI Colony

		// idk
		EFF = 269,    // Emperical Forge Fabrication
		MCC = 270,    // Mechaniccoid Cooperative Confederacy 
		BLN = 271,    // BuLwark Nation (Bulin)
		CNC = 272,    // ClaNg Clads (ChanClas)
		LOL = 273,	  // Larry's Overlord Laser
	}
}
