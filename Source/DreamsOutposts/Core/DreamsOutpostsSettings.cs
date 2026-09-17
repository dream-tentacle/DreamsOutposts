using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public class DreamsOutpostsSettings : ModSettings
	{
		public const float DefaultProductionMultiplier = 1f;
		public const float MinProductionMultiplier = 0f;
		public const float MaxProductionMultiplier = 10f;

		public float productionMultiplier = DefaultProductionMultiplier;

		public bool useVanillaUi = false;

		public override void ExposeData()
		{
			Scribe_Values.Look(ref productionMultiplier, "productionMultiplier", DefaultProductionMultiplier);
			Scribe_Values.Look(ref useVanillaUi, "useVanillaUi", false);
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				ClampValues();
			}
		}

		public void ClampValues()
		{
			if (float.IsNaN(productionMultiplier) || float.IsInfinity(productionMultiplier))
			{
				productionMultiplier = DefaultProductionMultiplier;
			}
			productionMultiplier = Mathf.Clamp(productionMultiplier, MinProductionMultiplier, MaxProductionMultiplier);
		}
	}
}
