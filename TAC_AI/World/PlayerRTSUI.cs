using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace TAC_AI.World
{
	public class PlayerRTSUnitDisp
	{
		public Image unitPortrait;
		public void MakePortrait(Tank tech)
		{
			Singleton.Manager<ManScreenshot>.inst.RenderTechImage(tech, new IntVector2(96, 96), false, delegate (TechData techData, Texture2D techImage)
			{
				if (techImage.IsNotNull())
				{
					unitPortrait.sprite = Sprite.Create(techImage, new Rect(Vector2.zero, new Vector2(techImage.width, techImage.height)), Vector2.zero);
					unitPortrait.preserveAspect = true;
				}
			});
		}
	}
	public class ManPlayerRTSUI
	{
    }
}
