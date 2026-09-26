using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public class OutpostProductionWorker : OutpostProcessWorker
	{
		public virtual bool UsesDynamicProduct => false;

		public virtual bool UsesPersonnelCapacity(OutpostProductionProperties production)
		{
			return production?.capacityStat != null;
		}

		public override bool UsesPersonnelCapacity(OutpostProcessProperties process)
		{
			return UsesPersonnelCapacity((OutpostProductionProperties)process);
		}

		public virtual float CalculatePersonnelCapacity(Outpost outpost, OutpostProductionProperties production)
		{
			return CalculatePersonnelCapacity(outpost?.Pawns, outpost?.outpostTypeDef, production);
		}

		public virtual float CalculatePersonnelCapacity(
			IEnumerable<Pawn> pawns,
			OutpostTypeDef outpostTypeDef,
			OutpostProductionProperties production)
		{
			return base.CalculatePersonnelCapacity(pawns, outpostTypeDef, production);
		}

		public override float CalculatePersonnelCapacity(Outpost outpost, OutpostProcessProperties process)
		{
			return CalculatePersonnelCapacity(outpost, (OutpostProductionProperties)process);
		}

		public override float CalculatePersonnelCapacity(
			IEnumerable<Pawn> pawns,
			OutpostTypeDef outpostTypeDef,
			OutpostProcessProperties process)
		{
			return CalculatePersonnelCapacity(pawns, outpostTypeDef, (OutpostProductionProperties)process);
		}

		public virtual float CalculateOutput(
			float personnelCapacity,
			Outpost outpost,
			OutpostProductionProperties production,
			OutpostProductionState state)
		{
			return CalculateOutput(personnelCapacity, outpost?.outpostTypeDef, production, state);
		}

		public virtual float CalculateOutput(
			float personnelCapacity,
			OutpostTypeDef outpostTypeDef,
			OutpostProductionProperties production)
		{
			return CalculateOutput(personnelCapacity, outpostTypeDef, production, null);
		}

		public virtual float CalculateOutput(
			float personnelCapacity,
			OutpostTypeDef outpostTypeDef,
			OutpostProductionProperties production,
			OutpostProductionState state)
		{
			if (outpostTypeDef == null) throw new ArgumentNullException("outpostTypeDef");
			if (production == null) throw new ArgumentNullException("production");
			return personnelCapacity * production.outputPerCapacity;
		}

		public override float CalculateOutput(
			float personnelCapacity,
			Outpost outpost,
			OutpostProcessProperties process,
			OutpostProcessState state)
		{
			return CalculateOutput(
				personnelCapacity,
				outpost,
				(OutpostProductionProperties)process,
				state as OutpostProductionState);
		}

		public override float CalculateOutput(
			float personnelCapacity,
			OutpostTypeDef outpostTypeDef,
			OutpostProcessProperties process,
			OutpostProcessState state)
		{
			return CalculateOutput(
				personnelCapacity,
				outpostTypeDef,
				(OutpostProductionProperties)process,
				state as OutpostProductionState);
		}

		public float CalculateProduction(
			Outpost outpost,
			OutpostProductionProperties production,
			OutpostProductionState state)
		{
			return CalculateProcess(outpost, production, state);
		}

		public float CalculateProduction(
			IEnumerable<Pawn> pawns,
			OutpostTypeDef outpostTypeDef,
			OutpostProductionProperties production,
			OutpostProductionState state)
		{
			if (outpostTypeDef == null) throw new ArgumentNullException("outpostTypeDef");
			if (production == null) throw new ArgumentNullException("production");
			float capacity = CalculatePersonnelCapacity(pawns, outpostTypeDef, production);
			float output = CalculateOutput(capacity, outpostTypeDef, production, state);
			if (float.IsNaN(output) || float.IsInfinity(output) || output < 0f)
				throw new InvalidOperationException("Production " + production.id + " returned a non-finite or negative output.");
			return output;
		}

		public float CalculateProduction(
			IEnumerable<Pawn> pawns,
			OutpostTypeDef outpostTypeDef,
			OutpostProductionProperties production)
		{
			return CalculateProduction(pawns, outpostTypeDef, production, null);
		}

		public virtual ThingDef GetProduct(OutpostProductionProperties production, OutpostProductionState state)
		{
			return production?.product;
		}

		public override string GetDisplayLabel(OutpostProcessProperties process, OutpostProcessState state)
		{
			OutpostProductionProperties production = (OutpostProductionProperties)process;
			ThingDef product = GetProduct(production, state as OutpostProductionState);
			if (product != null) return product.LabelCap.ToString();
			return base.GetDisplayLabel(process, state);
		}

		public override ThingDef GetIconThing(OutpostProcessProperties process, OutpostProcessState state)
		{
			return GetProduct(
				(OutpostProductionProperties)process,
				state as OutpostProductionState);
		}

		public virtual int GetProductionIntervalTicks(
			OutpostProductionProperties production,
			OutpostProductionState state)
		{
			return production?.intervalTicks ?? 0;
		}

		public override int GetProcessIntervalTicks(OutpostProcessProperties process, OutpostProcessState state)
		{
			return GetProductionIntervalTicks(
				(OutpostProductionProperties)process,
				state as OutpostProductionState);
		}

		public override Type StateClass => typeof(OutpostProductionState);

		public override OutpostProcessContext CreateContext(
			Outpost outpost,
			OutpostFacility facility,
			OutpostProcessProperties process,
			OutpostProcessState state,
			int now)
		{
			return new OutpostProductionContext(
				outpost,
				facility,
				(OutpostProductionProperties)process,
				(OutpostProductionState)state,
				now);
		}

		public override string DescribeCapacity(Outpost outpost, OutpostProcessProperties process, float capacity)
		{
			return DescribeCapacity(outpost, (OutpostProductionProperties)process, capacity);
		}

		public virtual string DescribeCapacity(Outpost outpost, OutpostProductionProperties production, float capacity)
		{
			return base.DescribeCapacity(outpost, production, capacity);
		}

		public override bool CanProcess(OutpostProcessContext context)
		{
			return CanProduce((OutpostProductionContext)context);
		}

		public virtual bool CanProduce(OutpostProductionContext context)
		{
			return true;
		}

		public override void Execute(OutpostProcessContext genericContext)
		{
			OutpostProductionContext context = (OutpostProductionContext)genericContext;
			context.WantedAmount = GenMath.RoundRandom(context.ModifiedOutput);
			ModifyProduction(context);
			if (context.WantedAmount <= 0)
			{
				context.Outcome = OutpostProcessOutcome.Idle;
				context.FailureReason = "wanted amount is zero for this cycle";
				return;
			}

			context.Product = GetProduct(context.Production, context.State);
			if (context.Product == null)
			{
				context.Outcome = OutpostProcessOutcome.Failed;
				context.FailureReason = "no product def";
				Log.ErrorOnce(
					"Outpost production skipped: facility=" + context.RuleLabel +
					" has no product.",
					OutpostProcessUtility.FailureKey(context.Facility, context.Process));
				return;
			}

			context.ActualAmount = context.WantedAmount;
			int maxProducible = MaxProducibleAmount(context);
			if (maxProducible < context.ActualAmount) context.ActualAmount = maxProducible;
			if (context.ActualAmount <= 0)
			{
				context.Outcome = OutpostProcessOutcome.Idle;
				context.FailureReason = "not enough resources for a single unit";
				return;
			}
			if (!ConsumeInputs(context))
			{
				context.Outcome = OutpostProcessOutcome.Failed;
				context.FailureReason = "inputs could not be consumed";
				return;
			}
			if (context.ActualAmount <= 0)
			{
				context.Outcome = OutpostProcessOutcome.Idle;
				context.FailureReason = "the amount became zero while consuming inputs";
				return;
			}

			List<Thing> created = CreateProducts(context);
			if (created != null) context.Products = created;
			DeliverProducts(context);
			context.Outcome = OutpostProcessOutcome.Completed;
		}

		public virtual void ModifyProduction(OutpostProductionContext context)
		{
		}

		public virtual int MaxProducibleAmount(OutpostProductionContext context)
		{
			if (context?.Production == null || !context.Production.HasInputs) return int.MaxValue;
			return OutpostStockUtility.MaxCraftableUnits(context.Outpost, context.Production.inputs);
		}

		public virtual bool ConsumeInputs(OutpostProductionContext context)
		{
			return OutpostProductionUtility.TakeInputsFromStock(
				context.Outpost,
				context.Facility,
				context.Production,
				context.ActualAmount);
		}

		public virtual List<Thing> CreateProducts(OutpostProductionContext context)
		{
			if (context.ActualAmount <= 0) return new List<Thing>();
			return OutpostProductionUtility.MakeProductThings(context.Product, context.ActualAmount);
		}

		public virtual void DeliverProducts(OutpostProductionContext context)
		{
			if (!OutpostAutomaticAirdropUtility.TryDeliver(context))
				OutpostProductionUtility.StoreInOutpostInventory(context.Outpost, context.Products);
		}

		public override void AfterProcess(OutpostProcessContext context)
		{
			AfterProduction((OutpostProductionContext)context);
		}

		public virtual void AfterProduction(OutpostProductionContext context)
		{
		}

		public override void OnProcessFailed(OutpostProcessContext context, Exception ex)
		{
			OnProductionFailed((OutpostProductionContext)context, ex);
		}

		public virtual void OnProductionFailed(OutpostProductionContext context, Exception ex)
		{
		}

		public override bool HasConfiguration(OutpostProcessProperties process)
		{
			return HasConfiguration((OutpostProductionProperties)process);
		}

		public virtual bool HasConfiguration(OutpostProductionProperties production)
		{
			return false;
		}

		public override void EnsureConfiguration(OutpostProcessProperties process, OutpostProcessState state)
		{
			EnsureConfiguration(
				(OutpostProductionProperties)process,
				state as OutpostProductionState);
		}

		public virtual void EnsureConfiguration(
			OutpostProductionProperties production,
			OutpostProductionState state)
		{
		}

		public virtual void DrawConfiguration(
			Rect rect,
			string label,
			OutpostProductionProperties production,
			OutpostProductionState state)
		{
		}

		public override void OpenConfiguration(
			OutpostProcessProperties process,
			OutpostProcessState state,
			Action onChanged = null)
		{
			OpenConfiguration(
				(OutpostProductionProperties)process,
				state as OutpostProductionState,
				onChanged);
		}

		public virtual void OpenConfiguration(
			OutpostProductionProperties production,
			OutpostProductionState state,
			Action onChanged = null)
		{
		}

		public override string ConfigurationSummary(OutpostProcessProperties process, OutpostProcessState state)
		{
			return ConfigurationSummary(
				(OutpostProductionProperties)process,
				state as OutpostProductionState);
		}

		public virtual string ConfigurationSummary(
			OutpostProductionProperties production,
			OutpostProductionState state)
		{
			return null;
		}

		public override string ConfigurationTip(OutpostProcessProperties process)
		{
			return ConfigurationTip((OutpostProductionProperties)process);
		}

		public virtual string ConfigurationTip(OutpostProductionProperties production)
		{
			return string.Empty;
		}

		public override IEnumerable<string> ConfigErrors(OutpostProcessProperties process)
		{
			foreach (string error in ConfigErrors((OutpostProductionProperties)process))
				yield return error;
		}

		public virtual IEnumerable<string> ConfigErrors(OutpostProductionProperties production)
		{
			foreach (string error in base.ConfigErrors(production))
				yield return error;
		}
	}
}
