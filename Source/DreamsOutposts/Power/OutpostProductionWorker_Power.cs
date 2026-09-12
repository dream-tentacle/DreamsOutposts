using System;
using System.Collections.Generic;
using Verse;

namespace DreamsOutposts
{
	public class OutpostProductionWorker_Power : OutpostProductionWorker
	{
		public override bool UsesDynamicProduct => true;
		public override Type StateClass => typeof(OutpostProductionState_Power);

		public override OutpostProductionState CreateState(string productionId, int nextProductionTick)
		{
			return new OutpostProductionState_Power(productionId, Find.TickManager.TicksGame);
		}

		public override bool OverrideProductionCycle(OutpostProductionContext context)
		{
			OutpostProductionState_Power state = context.State as OutpostProductionState_Power;
			OutpostProductionProperties_Power props = context.Production as OutpostProductionProperties_Power;
			context.NextProductionInterval = 1250;
			if (state == null || props == null)
				return true;

			if (!RemotePowerUtility.IsValidReceiver(state.linkedReceiver))
			{
				state.linkedReceiver = null;
				return true;
			}
			if (state.poweredUntilTick > context.Now)
			{
				context.NextProductionInterval = state.poweredUntilTick - context.Now;
				return true;
			}

			if (OutpostStockUtility.CountInStock(context.Outpost, props.fuel) < props.fuelPerCycle)
			{
				RemotePowerUtility.NotifyReceiver(state.linkedReceiver);
				return true;
			}

			int taken = OutpostStockUtility.TakeFromStock(context.Outpost, props.fuel, props.fuelPerCycle);
			if (taken == props.fuelPerCycle)
			{
				state.poweredUntilTick = context.Now + props.fuelDurationTicks;
				context.NextProductionInterval = props.fuelDurationTicks;
				context.Outcome = OutpostProductionOutcome.Completed;
			}
			RemotePowerUtility.NotifyReceiver(state.linkedReceiver);
			return true;
		}

		public override float CalculateOutput(float personnelCapacity, OutpostTypeDef outpostTypeDef, OutpostProductionProperties production, OutpostProductionState state)
		{
			OutpostProductionProperties_Power props = production as OutpostProductionProperties_Power;
			return props?.basePowerOutput ?? 0f;
		}

		public override IEnumerable<string> ConfigErrors(OutpostProductionProperties production)
		{
			foreach (string error in base.ConfigErrors(production))
				yield return error;
			OutpostProductionProperties_Power props = production as OutpostProductionProperties_Power;
			if (props == null)
			{
				yield return "Power worker requires OutpostProductionProperties_Power.";
				yield break;
			}
			foreach (string error in props.PowerConfigErrors())
				yield return error;
		}
	}
}
