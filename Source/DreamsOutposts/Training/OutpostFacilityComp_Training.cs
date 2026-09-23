using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 训练型设施只提供一个 UI 区块：卡片中央那颗「跳到仓库」按钮。
	/// 技能与每小时经验已经由卡片底部的「训练 …/时」chip 表达，区块里再重复一次只会挤占版面；
	/// 那颗按钮则用来一眼看清驻扎人员的对应技能等级——切到仓库页并把殖民者栏的筛选设成本设施的技能。
	/// </summary>
	public class OutpostFacilityComp_Training : OutpostFacilityComp
	{
		public override void Update(Outpost outpost, int delta)
		{
			OutpostTrainingUtility.TickFacility(outpost, parent, (OutpostTrainingProperties)props, delta);
		}

		public override void BuildUiSections(Outpost outpost, List<UiFacilitySectionView> output)
		{
			OutpostTrainingProperties training = props as OutpostTrainingProperties;
			if (training?.skill == null)
			{
				return;
			}
			SkillDef skill = training.skill;
			UiFacilitySectionView section = new UiFacilitySectionView();
			section.Title = "DreamsOutposts.Training.JumpTitle".Translate().ToString();
			section.ActionLabel = "DreamsOutposts.Training.JumpButton".Translate(skill.LabelCap).ToString();
			section.ActionTooltip = "DreamsOutposts.Training.JumpTip".Translate(skill.LabelCap).ToString();
			section.Action = delegate
			{
				Window_OutpostManage.JumpToWarehouse(skill);
			};
			output.Add(section);
		}
	}
}
