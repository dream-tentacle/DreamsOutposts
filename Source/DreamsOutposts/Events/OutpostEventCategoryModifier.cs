using Verse;

namespace DreamsOutposts
{
	public class OutpostEventCategoryModifier : IExposable
	{
		public OutpostEventCategoryDef category;

		public float offset;

		public float factor = 1f;

		public void ExposeData()
		{
			Scribe_Defs.Look(ref category, "category");
			Scribe_Values.Look(ref offset, "offset", 0f);
			Scribe_Values.Look(ref factor, "factor", 1f);
		}
	}
}
