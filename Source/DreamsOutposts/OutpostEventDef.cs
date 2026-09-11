using System.Collections.Generic;
using Verse;

namespace DreamsOutposts
{
	public class OutpostEventDef : Def
	{
		public List<OutpostEventOption> options = new List<OutpostEventOption>();

		public string defaultOptionId;

		public int durationTicks;
	}
}
