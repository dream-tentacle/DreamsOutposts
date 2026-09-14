using System.Collections.Generic;
using Verse;

namespace DreamsOutposts
{
	public class OutpostEventDef : Def
	{
		public OutpostEventCategoryDef category;

		public float weight = 1f;

		public List<OutpostEventRequirement> requirements = new List<OutpostEventRequirement>();

		public List<OutpostEventEffect> onCreatedEffects = new List<OutpostEventEffect>();

		public List<OutpostEventOption> options = new List<OutpostEventOption>();

		public string defaultOptionId;

		public int durationTicks;

		public virtual bool InitializeInstance(Outpost outpost, OutpostEventInstance instance) => true;

		public virtual string DescriptionFor(OutpostEventInstance instance) => description ?? string.Empty;

		public override IEnumerable<string> ConfigErrors()
		{
			foreach (string error in base.ConfigErrors()) yield return error;
			if (category == null) yield return "category must be assigned.";
			if (weight < 0f) yield return "weight must not be negative.";
			if (options.NullOrEmpty())
			{
				yield return "options must contain at least one option.";
				yield break;
			}
			HashSet<string> ids = new HashSet<string>();
			bool defaultFound = false;
			for (int i = 0; i < options.Count; i++)
			{
				OutpostEventOption option = options[i];
				if (option == null)
				{
					yield return "options[" + i + "] is null.";
					continue;
				}
				if (string.IsNullOrEmpty(option.id)) yield return "options[" + i + "].id must not be empty.";
				else if (!ids.Add(option.id)) yield return "Duplicate option id: " + option.id;
				if (option.id == defaultOptionId) defaultFound = true;
			}
			if (string.IsNullOrEmpty(defaultOptionId)) yield return "defaultOptionId must be assigned.";
			else if (!defaultFound) yield return "defaultOptionId '" + defaultOptionId + "' does not match any option.";
		}
	}
}
