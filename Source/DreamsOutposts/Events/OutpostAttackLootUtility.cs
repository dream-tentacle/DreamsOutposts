using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace DreamsOutposts
{
	public static class OutpostAttackLootUtility
	{
		/// <summary>Generate twenty humans sequentially; retain only detached gear, never the pawns.</summary>
		public static float Generate(OutpostEventContext context, float budget, List<string> received)
		{
			if (budget <= 0f) return 0f;
			List<PawnGenOption> options = OutpostAttackEventDef.HumanCombatOptions(context.instance.attack.faction);
			if (options.Count == 0) return 0f;
			List<Thing> pool = new List<Thing>();
			float value = 0f;
			try
			{
				for (int i = 0; i < 20; i++)
				{
					Pawn pawn = null;
					try
					{
						PawnKindDef kind = options.RandomElementByWeight(o => o.selectionWeight).kind;
						pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, context.instance.attack.faction,
							PawnGenerationContext.NonPlayer, context.outpost.Tile, forceGenerateNewPawn: true,
							canGeneratePawnRelations: false, mustBeCapableOfViolence: true,
							allowPregnant: false, allowFood: false, forbidAnyTitle: true));
						if (pawn == null || !pawn.RaceProps.Humanlike) continue;
						if (pawn.equipment != null)
							foreach (ThingWithComps gear in pawn.equipment.AllEquipmentListForReading.ToList())
							{
								pawn.equipment.Remove(gear);
								pool.Add(gear);
								gear.TryGetComp<CompBiocodable>()?.UnCode();
							}
						if (pawn.apparel != null)
							foreach (Apparel apparel in pawn.apparel.WornApparel.ToList())
							{
								pawn.apparel.Remove(apparel);
								pool.Add(apparel);
								apparel.TryGetComp<CompBiocodable>()?.UnCode();
							}
					}
					catch (Exception ex)
					{
						Log.Warning("Outpost attack loot candidate failed: " + ex.Message);
					}
					finally
					{
						OutpostUtility.DiscardCandidate(pawn);
					}
				}
				// Stop after reaching the budget; equipment is indivisible, so the final item may overshoot.
				while (pool.Count > 0 && value < budget)
				{
					int index = Rand.Range(0, pool.Count);
					Thing gear = pool[index];
					float price = gear.MarketValue * gear.stackCount;
					if (gear.Destroyed || price <= 0f || float.IsNaN(price) || float.IsInfinity(price))
					{
						if (!gear.Destroyed) gear.Destroy();
						pool.RemoveAt(index);
						continue;
					}
					OutpostItemRewardUtility.Add(context, gear);
					pool.RemoveAt(index);
					value += price;
					received.Add(gear.LabelCap + " (" + price.ToString("0") + ")");
				}
				return value;
			}
			finally
			{
				foreach (Thing gear in pool) if (!gear.Destroyed) gear.Destroy();
			}
		}
	}
}
