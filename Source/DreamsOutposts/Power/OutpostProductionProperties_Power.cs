using System.Collections.Generic;
using Verse;

namespace DreamsOutposts
{
	public class OutpostProductionProperties_Power : OutpostProductionProperties
	{
		public ThingDef fuel;
		public int fuelPerCycle;
		public int fuelDurationTicks = 60000;
		public float basePowerOutput = 1000f;

		public override string ToString()
		{
			return id + " (remote power)";
		}

		public IEnumerable<string> PowerConfigErrors()
		{
			if (fuel == null)
				yield return "fuel is required.";
			if (fuelPerCycle <= 0)
				yield return "fuelPerCycle must be positive.";
			if (fuelDurationTicks <= 0)
				yield return "fuelDurationTicks must be positive.";
			if (basePowerOutput <= 0f || float.IsNaN(basePowerOutput) || float.IsInfinity(basePowerOutput))
				yield return "basePowerOutput must be finite and positive.";
		}
	}
}
