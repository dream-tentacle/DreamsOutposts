using RimWorld;
using Verse;

namespace DreamsOutposts
{
	public class OutpostProcessWorker_Research : OutpostProcessWorker
	{
		public override void Execute(OutpostProcessContext context)
		{
			ResearchProjectDef project = Find.ResearchManager.GetProject();
			if (project == null || context.ModifiedOutput <= 0f)
			{
				context.Outcome = OutpostProcessOutcome.Idle;
				context.FailureReason = project == null ? "no active research project" : "research output is zero";
				return;
			}

			Find.ResearchManager.AddProgress(project, context.ModifiedOutput);
			context.Outcome = OutpostProcessOutcome.Completed;
		}

		public override string ProgressLeftText(
			Outpost outpost,
			OutpostFacility facility,
			OutpostProcessProperties process,
			OutpostProcessState state,
			int remaining)
		{
			return (Find.ResearchManager.GetProject()?.LabelCap ?? "DreamsOutposts.None".Translate()).ToString();
		}

		public override string ProgressRightText(
			Outpost outpost,
			OutpostFacility facility,
			OutpostProcessProperties process,
			OutpostProcessState state,
			int remaining)
		{
			return remaining.ToStringTicksToPeriod().ToString();
		}

		public override string CapacityTooltip(Outpost outpost, OutpostProcessProperties process)
		{
			return "DreamsOutposts.Ui.Chip.ResearchSpeedTip".Translate().ToString();
		}
	}
}
