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
		public const int ProductionCheckIntervalTicks = 250;

		public const int MaxCatchUpCyclesPerCheck = 100;

		private static readonly HashSet<int> failureDialogShown = new HashSet<int>();

		public static void TickOutpost(Outpost outpost)
		{
			if (outpost == null || outpost.Destroyed)
			{
				return;
			}
			int now = Find.TickManager.TicksGame;
			foreach (OutpostFacility facility in outpost.Facilities)
			{
				if (facility == null || facility.def == null || facility.def.productions == null)
				{
					continue;
				}
				List<OutpostProductionProperties> productions = facility.def.productions;
				for (int i = 0; i < productions.Count; i++)
				{
					OutpostProductionProperties production = productions[i];
					if (production != null)
					{
						try
						{
							TickProduction(outpost, facility, production, now);
						}
						catch (Exception ex)
						{
							ReportFailure(outpost, facility, production, ex.ToString());
						}
					}
				}
			}
		}

		public static bool TryGetCycleProgress(OutpostFacility facility, OutpostProductionProperties production, out float progress, out int ticksRemaining)
		{
			progress = 0f;
			ticksRemaining = 0;
			if (facility == null || production == null || string.IsNullOrEmpty(production.id))
			{
				return false;
			}
			if (production.intervalTicks <= 0)
			{
				return false;
			}
			OutpostProductionState state = facility.GetProductionState(production.id);
			if (state == null)
			{
				return false;
			}
			OutpostProductionState_Power powerState = state as OutpostProductionState_Power;
			OutpostProductionProperties_Power powerProps = production as OutpostProductionProperties_Power;
			if (powerState != null && powerProps != null)
			{
				ticksRemaining = Mathf.Max(powerState.poweredUntilTick - Find.TickManager.TicksGame, 0);
				if (ticksRemaining <= 0)
					return false;
				progress = Mathf.Clamp01(1f - (float)ticksRemaining / powerProps.fuelDurationTicks);
				return true;
			}
			ticksRemaining = Mathf.Max(state.nextProductionTick - Find.TickManager.TicksGame, 0);
			progress = Mathf.Clamp01(1f - (float)ticksRemaining / (float)production.intervalTicks);
			return true;
		}

		public static bool TryCalculatePersonnelCapacity(Outpost outpost, OutpostProductionProperties production, out float capacity)
		{
			capacity = 0f;
			if (outpost == null || production == null)
			{
				return false;
			}
			try
			{
				capacity = production.Worker.CalculatePersonnelCapacity(outpost.Pawns, outpost.outpostTypeDef, production);
				return true;
			}
			catch (Exception)
			{
				return false;
			}
		}

		public static IEnumerable<OutpostProductionModifierSource> MatchingModifiers(Outpost outpost, OutpostFacility producingFacility, OutpostProductionProperties production)
		{
			if (outpost == null || production == null)
			{
				yield break;
			}
			foreach (OutpostFacility sourceFacility in outpost.Facilities)
			{
				List<OutpostProductionModifier> modifiers = sourceFacility?.def?.productionModifiers;
				for (int i = 0; i < (modifiers?.Count ?? 0); i++)
				{
					OutpostProductionModifier modifier = modifiers[i];
					if (modifier != null && modifier.Matches(production, producingFacility?.def))
					{
						yield return new OutpostProductionModifierSource { Modifier = modifier, SourceFacility = sourceFacility.def };
					}
				}
			}
			List<OutpostProductionModifier> levelModifiers = outpost.CurrentLevelProperties?.productionModifiers;
			for (int i = 0; i < (levelModifiers?.Count ?? 0); i++)
			{
				OutpostProductionModifier modifier = levelModifiers[i];
				if (modifier != null && modifier.Matches(production, producingFacility?.def))
				{
					yield return new OutpostProductionModifierSource { Modifier = modifier, IsLevelModifier = true };
				}
			}
		}

		public static void GetModifierTotals(Outpost outpost, OutpostFacility producingFacility, OutpostProductionProperties production, out float offsetSum, out float factorProduct)
		{
			offsetSum = 0f;
			factorProduct = 1f;
			foreach (OutpostProductionModifierSource source in MatchingModifiers(outpost, producingFacility, production))
			{
				offsetSum += source.Modifier.offset;
				factorProduct *= source.Modifier.factor;
			}
		}

		public static float ApplyModifiers(Outpost outpost, OutpostFacility producingFacility, OutpostProductionProperties production, float baseOutput)
		{
			GetModifierTotals(outpost, producingFacility, production, out var offsetSum, out var factorProduct);
			return Mathf.Max((baseOutput + offsetSum) * factorProduct, 0f);
		}

		public static bool TryCalculateExpectedOutput(Outpost outpost, OutpostFacility facility, OutpostProductionProperties production, out float expectedOutput)
		{
			expectedOutput = 0f;
			if (outpost == null || production == null)
			{
				return false;
			}
			try
			{
				expectedOutput = ApplyModifiers(outpost, facility, production, production.Worker.CalculateProduction(outpost.Pawns, outpost.outpostTypeDef, production, facility?.GetProductionState(production.id)));
				return true;
			}
			catch (Exception ex)
			{
				Log.ErrorOnce("Outpost production forecast failed: outpost=" + outpost.Label + ", production=" + RuleLabel(facility, production) + "\n" + ex, FailureKey(facility, production));
				return false;
			}
		}

		public static bool TryGetProductionProduct(OutpostFacility facility, OutpostProductionProperties production, out ThingDef product)
		{
			product = null;
			if (production == null)
			{
				return false;
			}
			product = production.Worker.GetProduct(production, facility?.GetProductionState(production.id));
			return product != null;
		}

		private static void TickProduction(Outpost outpost, OutpostFacility facility, OutpostProductionProperties production, int now)
		{
			OutpostProductionState state = facility.GetProductionState(production.id);
			if (state == null)
			{
				Log.ErrorOnce("Outpost production skipped: facility=" + RuleLabel(facility, production) + " has no production state. Run SynchronizeProductionStates or check the save.", FailureKey(facility, production));
				return;
			}
			if (production.intervalTicks <= 0)
			{
				Log.ErrorOnce("Outpost production skipped: facility=" + RuleLabel(facility, production) + " has intervalTicks=" + production.intervalTicks + "; it must be positive. ConfigErrors should have reported this.", FailureKey(facility, production));
				return;
			}
			int cycles = 0;
			while (now >= state.nextProductionTick)
			{
				OutpostProductionContext context = new OutpostProductionContext(outpost, facility, production, state, now);
				try
				{
					ProduceOneCycle(context);
				}
				catch (Exception ex)
				{
					InvokeFailedHook(context, ex);
					ReportFailure(outpost, facility, production, ex.ToString());
					state.nextProductionTick += context.EffectiveInterval;
					break;
				}
				state.nextProductionTick += context.EffectiveInterval;
				if (++cycles >= MaxCatchUpCyclesPerCheck)
				{
					break;
				}
			}
		}

		private static void ProduceOneCycle(OutpostProductionContext context)
		{
			try
			{
				RunProductionPipeline(context);
			}
			catch (Exception ex)
			{
				context.Outcome = OutpostProductionOutcome.Failed;
				context.FailureReason = ex.Message;
				throw;
			}
			finally
			{
				InvokeAfterHook(context);
			}
		}

		private static void RunProductionPipeline(OutpostProductionContext context)
		{
			OutpostProductionWorker worker = context.Worker;
			OutpostProductionProperties production = context.Production;
			if (worker.OverrideProductionCycle(context))
			{
				context.Outcome = OutpostProductionOutcome.TakenOver;
				return;
			}
			if (!worker.CanProduce(context))
			{
				context.Outcome = OutpostProductionOutcome.Idle;
				context.FailureReason = "declined by CanProduce";
				return;
			}
			context.BaseOutput = worker.CalculateProduction(context.Outpost.Pawns, context.Outpost.outpostTypeDef, production, context.State);
			context.ModifiedOutput = ApplyModifiers(context.Outpost, context.Facility, production, context.BaseOutput);
			context.WantedAmount = GenMath.RoundRandom(context.ModifiedOutput);
			worker.ModifyProduction(context);
			if (context.WantedAmount <= 0)
			{
				context.Outcome = OutpostProductionOutcome.Idle;
				context.FailureReason = "wanted amount is zero for this cycle";
				return;
			}
			context.Product = worker.GetProduct(production, context.State);
			if (context.Product == null)
			{
				context.Outcome = OutpostProductionOutcome.Failed;
				context.FailureReason = "no product def";
				Log.ErrorOnce("Outpost production skipped: facility=" + RuleLabel(context.Facility, production) + " has no product (dynamic-product rules need a valid plant selection).", FailureKey(context.Facility, production));
				return;
			}
			context.ActualAmount = context.WantedAmount;
			int maxProducible = worker.MaxProducibleAmount(context);
			if (maxProducible < context.ActualAmount)
			{
				context.ActualAmount = maxProducible;
			}
			if (context.ActualAmount <= 0)
			{
				context.Outcome = OutpostProductionOutcome.Idle;
				context.FailureReason = "not enough resources for a single unit";
				return;
			}
			if (!worker.ConsumeInputs(context))
			{
				context.Outcome = OutpostProductionOutcome.Failed;
				context.FailureReason = "inputs could not be consumed";
				return;
			}
			if (context.ActualAmount <= 0)
			{
				context.Outcome = OutpostProductionOutcome.Idle;
				context.FailureReason = "the amount became zero while consuming inputs";
				return;
			}
			List<Thing> created = worker.CreateProducts(context);
			if (created != null)
			{
				context.Products = created;
			}
			worker.DeliverProducts(context);
			context.Outcome = OutpostProductionOutcome.Completed;
		}

		private static void InvokeAfterHook(OutpostProductionContext context)
		{
			try
			{
				context.Worker.AfterProduction(context);
			}
			catch (Exception ex)
			{
				Log.Error("Outpost production AfterProduction hook threw for " + context.RuleLabel + ": " + ex);
			}
		}

		private static void InvokeFailedHook(OutpostProductionContext context, Exception ex)
		{
			try
			{
				context.Worker.OnProductionFailed(context, ex);
			}
			catch (Exception ex2)
			{
				Log.Error("Outpost production OnProductionFailed hook threw for " + context.RuleLabel + ": " + ex2);
			}
		}

		public static bool TakeInputsFromStock(Outpost outpost, OutpostFacility facility, OutpostProductionProperties production, int amount)
		{
			if (production == null || !production.HasInputs)
			{
				return true;
			}
			if (amount <= 0)
			{
				return true;
			}
			List<ThingDefCountClass> inputs = production.inputs;
			HashSet<ThingDef> handled = new HashSet<ThingDef>();
			Dictionary<ThingDef, int> required = new Dictionary<ThingDef, int>();
			for (int i = 0; i < inputs.Count; i++)
			{
				ThingDefCountClass input = inputs[i];
				if (input?.thingDef == null || input.count <= 0 || !handled.Add(input.thingDef))
				{
					continue;
				}
				int requiredPerUnit = 0;
				for (int j = 0; j < inputs.Count; j++)
				{
					ThingDefCountClass other = inputs[j];
					if (other?.thingDef == input.thingDef && other.count > 0)
					{
						requiredPerUnit += other.count;
					}
				}
				int need = requiredPerUnit * amount;
				if (need > 0)
				{
					required[input.thingDef] = need;
				}
			}
			foreach (KeyValuePair<ThingDef, int> entry in required)
			{
				int available = OutpostStockUtility.CountInStock(outpost, entry.Key);
				if (available < entry.Value)
				{
					Log.ErrorOnce("Outpost production could not consume its inputs: outpost=" + outpost.Label + ", production=" + RuleLabel(facility, production) + ", needed " + entry.Value + " " + entry.Key.defName + " but available only " + available + ".", FailureKey(facility, production));
					return false;
				}
			}
			foreach (KeyValuePair<ThingDef, int> entry in required)
			{
				OutpostStockUtility.TakeFromStock(outpost, entry.Key, entry.Value);
			}
			return true;
		}

		public static List<Thing> MakeProductThings(ThingDef product, int amount)
		{
			if (amount <= 0)
			{
				return new List<Thing>();
			}
			if (product == null)
			{
				throw new InvalidOperationException("Production has no product def.");
			}
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
			if (products.NullOrEmpty())
			{
				return;
			}
			if (outpost?.inventory == null)
			{
				throw new InvalidOperationException("Outpost has no inventory to add products to.");
			}
			for (int i = 0; i < products.Count; i++)
			{
				Thing thing = products[i];
				if (thing != null && !outpost.inventory.TryAdd(thing))
				{
					int refused = thing.stackCount;
					string productDefName = thing.def?.defName ?? "null";
					thing.Destroy();
					for (int j = i + 1; j < products.Count; j++)
					{
						products[j]?.Destroy();
					}
					throw new InvalidOperationException("Outpost inventory refused " + refused + " " + productDefName + ".");
				}
			}
		}

		private static void ReportFailure(Outpost outpost, OutpostFacility facility, OutpostProductionProperties production, string detail)
		{
			int key = FailureKey(facility, production);
			Log.ErrorOnce("Outpost production failed: outpost=" + outpost.Label + ", facility=" + RuleLabel(facility, production) + "\n" + detail, key);
			if (failureDialogShown.Add(key))
			{
				Find.WindowStack.Add(new Dialog_MessageBox("DreamsOutposts.ProductionErrorText".Translate(outpost.LabelCap, facility?.def?.LabelCap ?? ((TaggedString)"null"), production?.id ?? "null"), null, null, null, null, "DreamsOutposts.ProductionErrorTitle".Translate()));
			}
		}

		public static string RuleLabel(OutpostFacility facility, OutpostProductionProperties production)
		{
			return (facility?.def?.defName ?? "null") + "." + (production?.id ?? "null");
		}

		private static int FailureKey(OutpostFacility facility, OutpostProductionProperties production)
		{
			return GenText.StableStringHash("DreamsOutposts.ProductionFailure." + RuleLabel(facility, production));
		}
	}
}
