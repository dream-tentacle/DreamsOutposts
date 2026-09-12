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

		public static bool CanSafelyReadStat(StatDef stat, Pawn pawn)
		{
			return IsStatShownFor(stat, pawn) && !stat.Worker.IsDisabledFor(pawn);
		}
	}
}
