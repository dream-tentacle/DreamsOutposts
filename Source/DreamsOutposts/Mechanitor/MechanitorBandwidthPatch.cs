using HarmonyLib;
using RimWorld;

namespace DreamsOutposts
{
	[HarmonyPatch(typeof(Pawn_MechanitorTracker), "get_TotalBandwidth")]
	public static class Pawn_MechanitorTracker_TotalBandwidth_OutpostPatch
	{
		public static void Postfix(Pawn_MechanitorTracker __instance, ref int __result)
		{
			__result += OutpostBandwidthUtility.BonusFor(__instance.Pawn);
		}
	}
}
