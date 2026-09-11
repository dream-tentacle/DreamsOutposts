using System.Collections.Generic;
using Verse;

namespace DreamsOutposts
{
	public class OutpostEventCategoryDef : Def
	{
		public float baseWeight;

		public override IEnumerable<string> ConfigErrors()
		{
			foreach (string error in base.ConfigErrors())
			{
				yield return error;
			}
			if (baseWeight < 0f)
			{
				yield return "baseWeight should not be negative; use 0 to disable a category by default.";
			}
		}
	}
}
