using RimWorld;
using Verse;

namespace DreamsOutposts
{
	/// <summary>将据点生产周期直接转换为当前项目的科研进度。</summary>
	public class OutpostProductionWorker_Research : OutpostProductionWorker
	{
		public override bool UsesDynamicProduct => true;

		public override bool OverrideProductionCycle(OutpostProductionContext context)
		{
			ResearchProjectDef project = Find.ResearchManager.GetProject();
			if (project == null)
			{
				context.FailureReason = "no active research project";
				return true;
			}

			context.BaseOutput = CalculateProduction(context.Outpost.Pawns, context.Outpost.outpostTypeDef, context.Production, context.State);
			context.ModifiedOutput = OutpostProductionUtility.ApplyModifiers(context.Outpost, context.Facility, context.Production, context.BaseOutput);
			if (context.ModifiedOutput > 0f)
			{
				Find.ResearchManager.AddProgress(project, context.ModifiedOutput);
			}
			return true;
		}
	}
}
