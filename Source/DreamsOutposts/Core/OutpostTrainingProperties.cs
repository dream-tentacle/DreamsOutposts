using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 训练型设施的属性：设施安装后，据点里符合条件的殖民者会持续获得指定技能的经验。
	/// </summary>
	public class OutpostTrainingProperties
	{
		public SkillDef skill;

		/// <summary>每游戏小时每个殖民者获得的经验量（尚未计入热情与学习因子）。</summary>
		public float xpPerHour = 60f;

		public IEnumerable<string> ConfigErrors()
		{
			if (skill == null)
			{
				yield return "skill is required: a training facility must declare which skill it trains.";
			}
			if (float.IsNaN(xpPerHour) || float.IsInfinity(xpPerHour))
			{
				yield return "xpPerHour must be a finite number.";
			}
			else if (xpPerHour <= 0f)
			{
				yield return "xpPerHour must be positive.";
			}
		}
	}
}
