using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 要求据点的酒馆（冒险者营地）当前还有待招募空位。
	/// 酒馆是否存在由 OutpostEventRequirement_HasFacility 单独负责，这里只管名额，
	/// 所以设施不存在时 offers 为空、这一条自然通过，失败信息不会两条重复。
	/// 只用计数判断，不调用 TryGenerateOffer / TryGetEntryFor 这类会消耗随机数的生成逻辑，
	/// 因为事件 UI 每 tick 都会重新求值一次条件。
	/// </summary>
	public class OutpostEventRequirement_FreeAdventurerSlot : OutpostEventRequirement
	{
		public override AcceptanceReport Check(OutpostEventContext context)
		{
			int used = context?.outpost?.adventurerRecruitment?.offers?.Count ?? 0;
			if (used >= AdventurerRecruitUtility.MaxOffers)
			{
				return "DreamsOutposts.EventRequirement.FreeAdventurerSlot".Translate(used, AdventurerRecruitUtility.MaxOffers).ToString();
			}
			return true;
		}
	}
}
