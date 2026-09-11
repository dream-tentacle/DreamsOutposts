using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld.Planet;
using Verse;

namespace DreamsOutposts
{
	[HarmonyPatch(typeof(Caravan), "GetGizmos")]
	public static class CaravanGizmoPatch
	{
		public static void Postfix(Caravan __instance, ref IEnumerable<Gizmo> __result)
		{
			if (__instance.IsPlayerControlled && Find.WorldSelector.SingleSelectedObject == __instance)
			{
				__result = __result.Concat(new Gizmo[1] { OutpostUtility.CreateCommand(__instance) });
			}
		}
	}
}
