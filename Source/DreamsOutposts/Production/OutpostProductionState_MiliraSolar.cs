using Verse;

namespace DreamsOutposts
{
	public class OutpostProductionState_MiliraSolar : OutpostProductionState
	{
		/// <summary>日光冶炼场当前产出的材料。</summary>
		public ThingDef selectedProduct;

		public OutpostProductionState_MiliraSolar()
		{
		}

		public OutpostProductionState_MiliraSolar(string productionId, int nextProductionTick)
			: base(productionId, nextProductionTick)
		{
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Defs.Look(ref selectedProduct, "selectedProduct");
		}
	}
}
