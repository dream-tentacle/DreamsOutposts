using System.Collections.Generic;
using Verse;

namespace DreamsOutposts
{
	public class OutpostEventDef : Def
	{
		public OutpostEventCategoryDef category;

		public float weight = 1f;

		public List<OutpostEventOption> options = new List<OutpostEventOption>();

		public string defaultOptionId;

		public int durationTicks;
	}
}
