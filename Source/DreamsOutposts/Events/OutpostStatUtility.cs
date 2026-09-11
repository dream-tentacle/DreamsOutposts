using RimWorld;
using Verse;

namespace DreamsOutposts
{
	public static class OutpostStatUtility
	{
		public static bool IsStatShownFor(StatDef stat, Pawn pawn)
		{
			return stat != null && pawn != null && stat.Worker != null && stat.Worker.ShouldShowFor(StatRequest.For(pawn));
		}
	}
}
