using RimWorld;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 把一位指定档次的冒险者放进据点的酒馆待招募位，由玩家在酒馆页面自行招募。
	/// 生成、档次回退、候选登记全部复用 AdventurerRecruitUtility.TryGenerateOffer，
	/// 这里只负责传入档次和给出反馈；「酒馆已建成且有位置」由选项的 requirements 保证。
	/// </summary>
	public class OutpostEventEffect_AddAdventurerOffer : OutpostEventEffect
	{
		public AdventurerRarity rarity = AdventurerRarity.Epic;

		public override void Apply(OutpostEventContext context)
		{
			Outpost outpost = context?.outpost;
			if (outpost == null)
			{
				return;
			}
			if (!AdventurerRecruitUtility.IsAvailable(outpost))
			{
				Log.Warning("[DreamsOutposts] an event tried to add a " + rarity + " adventurer offer to " + outpost.Label
					+ ", but that outpost has no adventurer camp. Nothing was added.");
				return;
			}
			if (!AdventurerRecruitUtility.TryGenerateOffer(outpost, rarity))
			{
				Log.Warning("[DreamsOutposts] failed to generate a " + rarity + " adventurer offer for " + outpost.Label
					+ ". The option's requirements were met, so the kind pool was probably empty.");
				return;
			}
			Messages.Message("DreamsOutposts.EventEffect.AdventurerOfferArrived".Translate(
				AdventurerRecruitUtility.RarityLabel(rarity), outpost.LabelCap), outpost, MessageTypeDefOf.PositiveEvent, historical: false);
		}

		public override string GetPreview(OutpostEventContext context)
		{
			return "DreamsOutposts.EventEffect.AdventurerOffer".Translate(AdventurerRecruitUtility.RarityLabel(rarity)).ToString();
		}
	}
}
