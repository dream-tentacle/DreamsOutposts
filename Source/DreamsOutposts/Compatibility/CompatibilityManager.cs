using System;
using HarmonyLib;
using Verse;

namespace DreamsOutposts
{
	public static class CompatibilityManager
	{
		private static readonly IOutpostCompatibility[] Modules =
		{
			new FleshHiveCompatibility(),
			new VehicleFrameworkCompatibility(),
			new XianluCompatibility(),
			new GD5Compatibility(),
			new MiliraCompatibility()
		};

		public static void ApplyAll(Harmony harmony)
		{
			for (int i = 0; i < Modules.Length; i++)
			{
				IOutpostCompatibility module = Modules[i];
				if (module == null || !ModsConfig.IsActive(module.PackageId))
				{
					continue;
				}

				try
				{
					module.Apply(harmony);
				}
				catch (Exception ex)
				{
					Log.Error("[DreamsOutposts] Failed to apply compatibility module for " + module.PackageId + ": " + ex);
				}
			}
		}
	}
}
