using Verse;

namespace DreamsOutposts
{
	public class OutpostProductionState_XianluQi : OutpostProductionState
	{
		/// <summary>聚灵法坛当前产出的资源。</summary>
		public ThingDef selectedProduct;

		public OutpostProductionState_XianluQi()
		{
		}

		public OutpostProductionState_XianluQi(string productionId, int nextProductionTick)
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
