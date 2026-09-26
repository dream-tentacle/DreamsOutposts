using System;
using System.Collections.Generic;
using Verse;

namespace DreamsOutposts
{
	public class OutpostProductionProperties : OutpostProcessProperties
	{
		public ThingDef product;
		public List<ThingDefCountClass> inputs = new List<ThingDefCountClass>();

		public OutpostProductionProperties()
		{
			workerClass = typeof(OutpostProductionWorker);
		}

		public bool HasInputs => !inputs.NullOrEmpty();

		public new OutpostProductionWorker Worker => (OutpostProductionWorker)base.Worker;

		public override IEnumerable<string> ConfigErrors()
		{
			foreach (string error in base.ConfigErrors()) yield return error;

			if (workerClass == null || !typeof(OutpostProductionWorker).IsAssignableFrom(workerClass))
			{
				yield return "workerClass for an item production must derive from OutpostProductionWorker.";
				yield break;
			}

			HashSet<ThingDef> seen = new HashSet<ThingDef>();
			for (int i = 0; i < (inputs?.Count ?? 0); i++)
			{
				ThingDefCountClass input = inputs[i];
				if (input == null)
				{
					yield return "inputs[" + i + "] is null.";
					continue;
				}
				if (input.thingDef == null)
				{
					yield return "inputs[" + i + "] has no thingDef.";
					continue;
				}
				if (input.count <= 0)
					yield return "inputs[" + i + "] (" + input.thingDef.defName + ") must have a positive count.";
				if (!seen.Add(input.thingDef))
					yield return "Duplicate inputs entry for " + input.thingDef.defName + ".";
			}

			if (product == null && !Worker.UsesDynamicProduct)
			{
				yield return "product is required unless the worker provides a dynamic product (UsesDynamicProduct).";
			}
		}
	}
}
