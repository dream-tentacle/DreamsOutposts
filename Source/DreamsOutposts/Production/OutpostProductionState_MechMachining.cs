using Verse;

namespace DreamsOutposts
{
	public class OutpostProductionState_MechMachining : OutpostProductionState
	{
		/// <summary>精密加工车间当前加工的产物。</summary>
		public ThingDef selectedProduct;

		public OutpostProductionState_MechMachining()
		{
		}

		public OutpostProductionState_MechMachining(string productionId, int nextProductionTick)
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
