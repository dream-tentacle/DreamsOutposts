using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public class OutpostProductionWorker
	{
		public virtual bool UsesDynamicProduct => false;

		public virtual Type StateClass => typeof(OutpostProductionState);

		public virtual float CalculatePersonnelCapacity(IEnumerable<Pawn> pawns, OutpostTypeDef outpostTypeDef, OutpostProductionProperties production)
		{
			if (pawns == null)
			{
				throw new ArgumentNullException("pawns");
			}
			if (production == null)
			{
				throw new ArgumentNullException("production");
			}
			if (production.capacityStat == null)
			{
				throw new InvalidOperationException("The default personnel capacity calculation requires a capacityStat.");
			}
			float capacity = 0f;
			foreach (Pawn pawn in pawns)
			{
				if (pawn != null && OutpostStatUtility.IsStatShownFor(production.capacityStat, pawn) && production.PawnMeetsSkillRequirement(pawn))
				{
					capacity += pawn.GetStatValue(production.capacityStat);
				}
			}
			return capacity;
		}

		public virtual float CalculateOutput(float personnelCapacity, OutpostTypeDef outpostTypeDef, OutpostProductionProperties production)
		{
			if (outpostTypeDef == null)
			{
				throw new ArgumentNullException("outpostTypeDef");
			}
			if (production == null)
			{
				throw new ArgumentNullException("production");
			}
			return personnelCapacity * production.outputPerCapacity;
		}

		public virtual float CalculateOutput(float personnelCapacity, OutpostTypeDef outpostTypeDef, OutpostProductionProperties production, OutpostProductionState state)
		{
			return CalculateOutput(personnelCapacity, outpostTypeDef, production);
		}

		public virtual ThingDef GetProduct(OutpostProductionProperties production, OutpostProductionState state)
		{
			return production?.product;
		}

		public float CalculateProduction(IEnumerable<Pawn> pawns, OutpostTypeDef outpostTypeDef, OutpostProductionProperties production)
		{
			return CalculateProduction(pawns, outpostTypeDef, production, null);
		}

		public float CalculateProduction(IEnumerable<Pawn> pawns, OutpostTypeDef outpostTypeDef, OutpostProductionProperties production, OutpostProductionState state)
		{
			if (outpostTypeDef == null)
			{
				throw new ArgumentNullException("outpostTypeDef");
			}
			if (production == null)
			{
				throw new ArgumentNullException("production");
			}
			float capacity = CalculatePersonnelCapacity(pawns, outpostTypeDef, production);
			float output = CalculateOutput(capacity, outpostTypeDef, production, state);
			if (float.IsNaN(output) || float.IsInfinity(output) || output < 0f)
			{
				throw new InvalidOperationException("Production " + production.id + " returned a non-finite or negative output.");
			}
			return output;
		}

		public virtual IEnumerable<string> ConfigErrors(OutpostProductionProperties production)
		{
			if (production.capacityStat == null)
			{
				yield return "capacityStat is required by the default personnel capacity calculation.";
			}
			foreach (string item in StateClassErrors())
			{
				yield return item;
			}
		}

		public virtual OutpostProductionState CreateState(string productionId, int nextProductionTick)
		{
			return (OutpostProductionState)Activator.CreateInstance(StateClass, productionId, nextProductionTick);
		}

		private IEnumerable<string> StateClassErrors()
		{
			Type stateClass = StateClass;
			if (stateClass == null || !typeof(OutpostProductionState).IsAssignableFrom(stateClass))
			{
				yield return "StateClass must derive from OutpostProductionState.";
			}
			else if (stateClass.IsAbstract || stateClass.ContainsGenericParameters)
			{
				yield return "StateClass must be a concrete OutpostProductionState subclass.";
			}
			else if (stateClass.GetConstructor(new Type[2]
			{
				typeof(string),
				typeof(int)
			}) == null)
			{
				yield return "StateClass must have a public (string productionId, int nextProductionTick) constructor.";
			}
		}

		public virtual bool OverrideProductionCycle(OutpostProductionContext context)
		{
			return false;
		}

		public virtual bool CanProduce(OutpostProductionContext context)
		{
			return true;
		}

		public virtual void ModifyProduction(OutpostProductionContext context)
		{
		}

		public virtual int MaxProducibleAmount(OutpostProductionContext context)
		{
			if (context?.Production == null || !context.Production.HasInputs)
			{
				return int.MaxValue;
			}
			return OutpostStockUtility.MaxCraftableUnits(context.Outpost, context.Production.inputs);
		}

		public virtual bool ConsumeInputs(OutpostProductionContext context)
		{
			return OutpostProductionUtility.TakeInputsFromStock(context.Outpost, context.Facility, context.Production, context.ActualAmount);
		}

		public virtual List<Thing> CreateProducts(OutpostProductionContext context)
		{
			if (context.ActualAmount <= 0)
			{
				return new List<Thing>();
			}
			return OutpostProductionUtility.MakeProductThings(context.Product, context.ActualAmount);
		}

		public virtual void DeliverProducts(OutpostProductionContext context)
		{
			OutpostProductionUtility.StoreInOutpostInventory(context.Outpost, context.Products);
		}

		public virtual void AfterProduction(OutpostProductionContext context)
		{
		}

		public virtual void OnProductionFailed(OutpostProductionContext context, Exception ex)
		{
		}

		public virtual bool HasConfiguration(OutpostProductionProperties production)
		{
			return false;
		}

		public virtual void EnsureConfiguration(OutpostProductionProperties production, OutpostProductionState state)
		{
		}

		public virtual void DrawConfiguration(Rect rect, string label, OutpostProductionProperties production, OutpostProductionState state)
		{
		}

		public virtual string ConfigurationSummary(OutpostProductionProperties production, OutpostProductionState state)
		{
			return null;
		}
	}
}
