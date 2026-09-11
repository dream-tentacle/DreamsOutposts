using Verse;

namespace DreamsOutposts
{
	public abstract class OutpostEventRequirement
	{
		public abstract AcceptanceReport Check(OutpostEventContext context);
	}
}
