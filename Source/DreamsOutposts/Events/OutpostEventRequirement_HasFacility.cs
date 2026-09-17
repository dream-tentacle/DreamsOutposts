using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 要求据点里已经建成至少 minCount 座指定设施。只看「建好了没有」，
	/// 不看它是否被临时效果停用：停用是暂时的，设施本身仍然属于这个据点。
	/// 判定只用计数，不消耗任何随机数——事件 UI 每 tick 都会重新求值一次条件。
	/// </summary>
	public class OutpostEventRequirement_HasFacility : OutpostEventRequirement
	{
		public OutpostFacilityDef facilityDef;

		public int minCount = 1;

		public override AcceptanceReport Check(OutpostEventContext context)
		{
			if (facilityDef == null || minCount <= 0)
			{
				return "Invalid facility requirement.";
			}
			int current = 0;
			Outpost outpost = context?.outpost;
			if (outpost != null)
			{
				foreach (OutpostFacility facility in outpost.Facilities)
				{
					if (facility?.def == facilityDef)
					{
						current++;
					}
				}
			}
			if (current < minCount)
			{
				return "DreamsOutposts.EventRequirement.HasFacility".Translate(facilityDef.LabelCap, current, minCount).ToString();
			}
			return true;
		}
	}
}
