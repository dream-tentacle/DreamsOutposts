using System;
using System.Collections.Generic;
using Verse;

namespace DreamsOutposts
{
	public class OutpostProductionModifier
	{
		public string productionTag;

		public ThingDef product;

		public float factor = 1f;

		public float offset;

		public string NormalizedTag => productionTag?.Trim();

		public bool IsGlobal => string.IsNullOrEmpty(NormalizedTag) && product == null;

		public bool Matches(OutpostProductionProperties production)
		{
			if (production == null)
			{
				return false;
			}
			if (product != null && production.product != product)
			{
				return false;
			}
			string tag = NormalizedTag;
			if (string.IsNullOrEmpty(tag))
			{
				return true;
			}
			List<string> tags = production.tags;
			if (tags == null)
			{
				return false;
			}
			for (int i = 0; i < tags.Count; i++)
			{
				string candidate = tags[i]?.Trim();
				if (!string.IsNullOrEmpty(candidate) && string.Equals(candidate, tag, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
			return false;
		}

		public IEnumerable<string> ConfigErrors()
		{
			if (float.IsNaN(factor) || float.IsInfinity(factor))
			{
				yield return "factor must be a finite number.";
			}
			else if (factor < 0f)
			{
				yield return "factor must not be negative; use 0 to stop this production instead.";
			}
			if (float.IsNaN(offset) || float.IsInfinity(offset))
			{
				yield return "offset must be a finite number.";
			}
			else if (offset < 0f)
			{
				yield return "offset must not be negative; production output can never be pushed below zero.";
			}
		}
	}
}
