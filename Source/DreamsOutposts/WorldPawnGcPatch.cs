using HarmonyLib;
using RimWorld.Planet;
using Verse;

namespace DreamsOutposts
{
	[HarmonyPatch(typeof(WorldPawnGC), "GetCriticalPawnReason")]
	public static class WorldPawnGcPatch
	{
		public static void Postfix(Pawn pawn, ref string __result)
		{
			if (__result.NullOrEmpty() && OutpostUtility.IsHeldByOutpost(pawn))
			{
				__result = "Outpost";
			}
		}
	}
}
