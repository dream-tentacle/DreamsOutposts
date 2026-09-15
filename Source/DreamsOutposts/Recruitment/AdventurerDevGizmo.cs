using RimWorld;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 开发者 gizmo：往酒馆里直接塞一名史诗级冒险者，用来测试最高档次候选人的表现。
	/// 走的是和自然刷出完全相同的路径（TryGenerateOffer 的强制等级重载），所以技能偏好、档次清理、
	/// 候选名单登记都一致；只额外要求已经有冒险者营地，否则 offers 会在下一个 tick 被清空。
	/// 只在开启开发者模式 + 上帝模式时由 Outpost.GetGizmos 挂出来。
	/// </summary>
	public static class AdventurerDevGizmo
	{
		public static Command GetCommand(Outpost outpost)
		{
			return new Command_Action
			{
				defaultLabel = "DEV: spawn epic adventurer",
				defaultDesc = "Generate one epic adventurer and put it straight into this outpost's tavern offers. Requires an adventurer camp.",
				icon = TexCommand.DesirePower,
				action = delegate
				{
					SpawnEpic(outpost);
				}
			};
		}

		private static void SpawnEpic(Outpost outpost)
		{
			if (outpost == null || outpost.adventurerRecruitment == null) return;
			if (AdventurerRecruitUtility.CampCount(outpost) <= 0)
			{
				Messages.Message("DEV: this outpost has no adventurer camp, so any offer would be cleared next tick.", MessageTypeDefOf.RejectInput, historical: false);
				return;
			}
			if (outpost.adventurerRecruitment.offers.Count >= AdventurerRecruitUtility.MaxOffers)
			{
				Messages.Message("DEV: all " + AdventurerRecruitUtility.MaxOffers + " adventurer slots are occupied.", MessageTypeDefOf.RejectInput, historical: false);
				return;
			}
			if (AdventurerRecruitUtility.TryGenerateOffer(outpost, AdventurerRarity.Epic))
			{
				Log.Message("DreamsOutposts: dev-spawned an epic adventurer in " + outpost.Label + ".");
			}
			else
			{
				Log.Warning("DreamsOutposts: could not dev-spawn an epic adventurer in " + outpost.Label + ".");
			}
		}
	}
}
