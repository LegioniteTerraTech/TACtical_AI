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
		LEG,    // LEGION!!1!

		// Community
		AER,    // Aerion
		BL,     // Black Labs (EXT OF HE)
		CC,     // CrystalCorp
		DC,     // DEVCorp
		DL,     // DarkLight
		EYM,    // Ellydium
		GT,     // GreenTech
		HS,     // Hyperion Systems
		IEC,    // Independant Earthern Colonies
		LK,     // Lemon Kingdom
		OS,     // Old Stars
		TC,		// Tofuu Corp
		TAC,	// Technocratic AI Colony

		// idk
		EFF,    // Emperical Forge Fabrication
		MCC,	// Mechaniccoid Cooperative Confederacy 
		BLN,	// BuLwark Nation (Bulin)
		CNC,	// ClaNg Clads (ChanClas)
		LOL,	// Larry's Overlord Laser
	}
}
