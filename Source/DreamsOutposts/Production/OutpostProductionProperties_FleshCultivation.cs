using Verse;

namespace DreamsOutposts
{
	public class OutpostProductionProperties_FleshCultivation : OutpostProductionProperties
	{
		public float baseEfficiency = 2f;

		public float efficiencyPerGroupCost = 1f;

		public OutpostProductionProperties_FleshCultivation()
		{
			workerClass = typeof(OutpostProductionWorker_FleshCultivation);
		}
	}
}
