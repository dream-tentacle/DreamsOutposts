using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DreamsOutposts
{
	public class OutpostFacilityComp_Training : OutpostFacilityComp
	{
		public override void Tick(Outpost outpost, int delta)
		{
			OutpostTrainingUtility.TickFacility(outpost, (OutpostTrainingProperties)props, delta);
		}

		public override void BuildUiSections(Outpost outpost, List<UiFacilitySectionView> output)
		{
			OutpostTrainingProperties training = (OutpostTrainingProperties)props;
			output.Add(new UiFacilitySectionView
			{
				Title = training.skill.LabelCap.ToString(),
				MainText = training.xpPerHour.ToString("0.#") + " XP/h",
				LeftText = "DreamsOutposts.Ui.Chip.TrainingTip".Translate(training.skill.LabelCap, OutpostTrainingUtility.CountTrainees(outpost, parent.def)).ToString(),
				ShowProgress = false,
				ProgressKind = UiChipKind.Info
			});
		}
	}
}
