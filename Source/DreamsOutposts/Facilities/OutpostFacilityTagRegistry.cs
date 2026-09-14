using System.Collections.Generic;

namespace DreamsOutposts
{
	public static class OutpostFacilityTagRegistry
	{
		public const string ProductionBoost = "ProductionBoost";
		public const string Processing = "Processing";
		public const string Defense = "Defense";
		public const string AutomaticProduction = "AutomaticProduction";
		public const string PowerGeneration = "PowerGeneration";
		public const string Recruitment = "Recruitment";
		public const string Training = "Training";
		public const string Other = "Other";

		private static readonly List<string> tags = new List<string>();
		private static bool initialized;

		public static IReadOnlyList<string> Tags
		{
			get
			{
				Initialize();
				return tags;
			}
		}

		public static void Initialize()
		{
			if (initialized)
			{
				return;
			}
			initialized = true;
			tags.Clear();
			tags.Add(ProductionBoost);
			tags.Add(Processing);
			tags.Add(Defense);
			tags.Add(AutomaticProduction);
			tags.Add(PowerGeneration);
			tags.Add(Recruitment);
			tags.Add(Training);
			tags.Add(Other);
		}

		public static string Normalize(string tag)
		{
			switch (tag)
			{
				case ProductionBoost:
				case Processing:
				case Defense:
				case AutomaticProduction:
				case PowerGeneration:
				case Recruitment:
				case Training:
				case Other:
					return tag;
				default:
					return Other;
			}
		}

		public static string LabelKey(string tag)
		{
			return "DreamsOutposts.FacilityTag." + Normalize(tag);
		}
	}
}
