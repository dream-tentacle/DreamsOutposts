using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public sealed class OutpostProductionModifierSource
	{
		public OutpostProductionModifier Modifier;
		public OutpostFacilityDef SourceFacility;
		public bool IsLevelModifier;
	}

	public static class OutpostProductionUtility
	{
		public static bool TryGetCycleProgress(
			OutpostFacility facility,
			OutpostProductionProperties production,
			out float progress,
			out int ticksRemaining)
		{
			return OutpostProcessUtility.TryGetCycleProgress(
				facility, production, out progress, out ticksRemaining);
		}

		public static bool TryCalculatePersonnelCapacity(
			Outpost outpost,
			OutpostProductionProperties production,
			out float capacity)
		{
			return OutpostProcessUtility.TryCalculatePersonnelCapacity(
				outpost, production, out capacity);
		}

		public static IEnumerable<OutpostProductionModifierSource> MatchingModifiers(
			Outpost outpost,
			OutpostFacility producingFacility,
			OutpostProductionProperties production)
		{
			foreach (OutpostProcessModifierSource source in
				OutpostProcessUtility.MatchingModifiers(outpost, producingFacility, production))
			{
				yield return new OutpostProductionModifierSource
				{
					Modifier = source.Modifier,
					SourceFacility = source.SourceFacility,
					IsLevelModifier = source.IsLevelModifier
				};
			}
		}

		public static void GetModifierTotals(
			Outpost outpost,
			OutpostFacility producingFacility,
			OutpostProductionProperties production,
			out float offsetSum,
			out float factorProduct)
		{
			OutpostProcessUtility.GetModifierTotals(
				outpost,
				producingFacility,
				production,
				out offsetSum,
				out factorProduct);
		}

		public static float ApplyModifiers(
			Outpost outpost,
			OutpostFacility producingFacility,
			OutpostProductionProperties production,
			float baseOutput)
		{
			return OutpostProcessUtility.ApplyModifiers(
				outpost,
				producingFacility,
				production,
				baseOutput);
		}

		public static bool TryCalculateExpectedOutput(
			Outpost outpost,
			OutpostFacility facility,
			OutpostProductionProperties production,
			out float expectedOutput)
		{
			return OutpostProcessUtility.TryCalculateExpectedOutput(
				outpost,
				facility,
				production,
				out expectedOutput);
		}

		public static bool TryGetProductionProduct(
			OutpostFacility facility,
			OutpostProductionProperties production,
			out ThingDef product)
		{
			product = null;
			if (production == null) return false;
			product = production.Worker.GetProduct(
				production,
				facility?.GetProductionState(production.id));
			return product != null;
		}

		public static bool TakeInputsFromStock(
			Outpost outpost,
			OutpostFacility facility,
			OutpostProductionProperties production,
			int amount)
		{
			if (production == null || !production.HasInputs || amount <= 0) return true;

			List<ThingDefCountClass> inputs = production.inputs;
			HashSet<ThingDef> handled = new HashSet<ThingDef>();
			Dictionary<ThingDef, int> required = new Dictionary<ThingDef, int>();

			for (int i = 0; i < inputs.Count; i++)
			{
				ThingDefCountClass input = inputs[i];
				if (input?.thingDef == null || input.count <= 0 || !handled.Add(input.thingDef))
					continue;

				int requiredPerUnit = 0;
				for (int j = 0; j < inputs.Count; j++)
				{
					ThingDefCountClass other = inputs[j];
					if (other?.thingDef == input.thingDef && other.count > 0)
						requiredPerUnit += other.count;
				}

				int need = requiredPerUnit * amount;
				if (need > 0) required[input.thingDef] = need;
			}

			foreach (KeyValuePair<ThingDef, int> entry in required)
			{
				int available = OutpostStockUtility.CountInStock(outpost, entry.Key);
				if (available < entry.Value)
				{
					Log.ErrorOnce(
						"Outpost production could not consume its inputs: outpost=" + outpost.Label +
						", production=" + RuleLabel(facility, production) +
						", needed " + entry.Value + " " + entry.Key.defName +
						" but available only " + available + ".",
						OutpostProcessUtility.FailureKey(facility, production));
					return false;
				}
			}

			foreach (KeyValuePair<ThingDef, int> entry in required)
				OutpostStockUtility.TakeFromStock(outpost, entry.Key, entry.Value);

			return true;
		}

		public static List<Thing> MakeProductThings(ThingDef product, int amount)
		{
			if (amount <= 0) return new List<Thing>();
			if (product == null) throw new InvalidOperationException("Production has no product def.");

			List<Thing> products = new List<Thing>();
			int stackLimit = Mathf.Max(product.stackLimit, 1);
			int remaining = amount;
			while (remaining > 0)
			{
				Thing thing = ThingMaker.MakeThing(product);
				thing.stackCount = Mathf.Min(remaining, stackLimit);
				remaining -= thing.stackCount;
				products.Add(thing);
			}
			return products;
		}

		public static void StoreInOutpostInventory(Outpost outpost, List<Thing> products)
		{
			if (products.NullOrEmpty()) return;
			if (outpost?.inventory == null)
				throw new InvalidOperationException("Outpost has no inventory to add products to.");

			for (int i = 0; i < products.Count; i++)
			{
				Thing thing = products[i];
				if (thing != null && !outpost.inventory.TryAdd(thing))
				{
					int refused = thing.stackCount;
					string productDefName = thing.def?.defName ?? "null";
					thing.Destroy();
					for (int j = i + 1; j < products.Count; j++)
						products[j]?.Destroy();
					throw new InvalidOperationException(
						"Outpost inventory refused " + refused + " " + productDefName + ".");
				}
			}
		}

		public static string RuleLabel(
			OutpostFacility facility,
			OutpostProductionProperties production)
		{
			return OutpostProcessUtility.RuleLabel(facility, production);
		}
	}
}
