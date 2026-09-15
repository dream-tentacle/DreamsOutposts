using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public class OutpostEventRequirement_AvailableTechprint : OutpostEventRequirement
	{
		public override AcceptanceReport Check(OutpostEventContext context)
		{
			if (!ModsConfig.RoyaltyActive) return "DreamsOutposts.EventRequirement.TechprintDlc".Translate();
			return TechprintUtility.TryGetTechprintDefToGenerate_NewTemp(null, out ThingDef _) ? AcceptanceReport.WasAccepted : "DreamsOutposts.EventRequirement.NoTechprint".Translate();
		}
	}

	public class OutpostEventEffect_AddRandomTechprint : OutpostEventEffect
	{
		public override void Apply(OutpostEventContext context)
		{
			if (context?.outpost != null && ModsConfig.RoyaltyActive && TechprintUtility.TryGetTechprintDefToGenerate_NewTemp(null, out ThingDef techprint))
				OutpostItemRewardUtility.Add(context, techprint, 1);
		}

		public override string GetPreview(OutpostEventContext context) => "DreamsOutposts.EventEffect.RandomTechprint".Translate();
	}

	public class OutpostEventEffect_GeneratePawnByRarity : OutpostEventEffect
	{
		/// <summary>
		/// Optional. When set, exactly this kind is generated. When left empty, a kind rated at
		/// <see cref="rarity"/> is drawn from the factions' adventurer pool instead.
		/// </summary>
		public PawnKindDef pawnKindDef;
		public AdventurerRarity rarity = AdventurerRarity.Common;
		public int count = 1;

		public override void Apply(OutpostEventContext context)
		{
			if (context?.outpost == null || count <= 0) return;
			for (int n = 0; n < count; n++)
			{
				Pawn pawn = Generate(context);
				if (pawn == null) continue;
				pawn.SetFaction(Faction.OfPlayer);
				if (!OutpostUtility.MovePawnIntoOutpost(context.outpost, pawn)) pawn.Destroy();
			}
		}

		private Pawn Generate(OutpostEventContext context)
		{
			PawnKindDef kind = pawnKindDef;
			Faction faction = null;
			if (kind == null)
			{
				if (!AdventurerRecruitUtility.TryGetEntryFor(rarity, out kind, out faction))
				{
					Log.WarningOnce("DreamsOutposts: no pawn kind rated " + rarity + " is fielded by any faction, so GeneratePawnByRarity produced nothing.", Gen.HashCombineInt((int)rarity, 7391));
					return null;
				}
			}
			else if (AdventurerRecruitUtility.RarityForKind(kind) != rarity)
			{
				Log.WarningOnce("DreamsOutposts: OutpostEventEffect_GeneratePawnByRarity declares kind " + kind.defName
					+ " (combatPower " + kind.combatPower + ", rated " + AdventurerRecruitUtility.RarityForKind(kind)
					+ ") but asks for rarity " + rarity + ". Rating comes from combatPower, so the kind's own rating is used.", Gen.HashCombineInt(kind.shortHash, (int)rarity));
			}
			Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, faction, PawnGenerationContext.NonPlayer, context.outpost.Tile, forceGenerateNewPawn: true));
			if (pawn != null) AdventurerRecruitUtility.SanitizeAdventurer(pawn);
			return pawn;
		}

		public override string GetPreview(OutpostEventContext context) => "DreamsOutposts.EventEffect.GeneratePawnRarity".Translate(count, AdventurerRecruitUtility.RarityLabel(rarity));
	}

	public class OutpostEventEffect_StealInventoryFraction : OutpostEventEffect
	{
		public float fraction = 0.2f;

		public override void Apply(OutpostEventContext context)
		{
			if (context?.outpost?.inventory == null || context.instance == null || fraction <= 0f) return;
			List<Thing> candidates = context.outpost.InventoryItems.Where(Eligible).InRandomOrder().ToList();
			float target = candidates.Sum(t => t.MarketValue * t.stackCount) * Mathf.Clamp01(fraction);
			float removedValue = 0f;
			for (int i = 0; i < candidates.Count && removedValue < target; i++)
			{
				Thing thing = candidates[i];
				int count = Mathf.Clamp(Mathf.CeilToInt((target - removedValue) / Mathf.Max(thing.MarketValue, 0.01f)), 1, thing.stackCount);
				Thing taken = context.outpost.inventory.Take(thing, count);
				if (taken == null) continue;
				context.instance.storedThingDefs.Add(taken.def);
				context.instance.storedThingCounts.Add(taken.stackCount);
				removedValue += taken.MarketValue * taken.stackCount;
				taken.Destroy();
			}
		}

		private static bool Eligible(Thing thing) => thing != null && !thing.Destroyed && thing.def.category == ThingCategory.Item && thing.def.stackLimit > 1 && thing.MarketValue > 0f && thing.questTags.NullOrEmpty();
		public override string GetPreview(OutpostEventContext context) => "DreamsOutposts.EventEffect.InventoryStolen".Translate(fraction.ToStringPercent());
	}

	public class OutpostEventEffect_RestoreStolenInventory : OutpostEventEffect
	{
		public override void Apply(OutpostEventContext context)
		{
			if (context?.outpost == null || context.instance == null) return;
			int count = Math.Min(context.instance.storedThingDefs.Count, context.instance.storedThingCounts.Count);
			for (int i = 0; i < count; i++) OutpostItemRewardUtility.Add(context, context.instance.storedThingDefs[i], context.instance.storedThingCounts[i]);
			context.instance.storedThingDefs.Clear();
			context.instance.storedThingCounts.Clear();
		}
		public override string GetPreview(OutpostEventContext context) => "DreamsOutposts.EventEffect.RestoreStolen".Translate();
	}

	public class OutpostEventEffect_GenerateTradeRequestQuest : OutpostEventEffect
	{
		public override void Apply(OutpostEventContext context)
		{
			QuestScriptDef def = DefDatabase<QuestScriptDef>.GetNamedSilentFail("TradeRequest");
			if (def == null) { Log.Warning("DreamsOutposts: TradeRequest quest Def was unavailable."); return; }
			float points = StorytellerUtility.DefaultThreatPointsNow(Find.AnyPlayerHomeMap ?? (IIncidentTarget)Find.World);
			Slate slate = new Slate();
			slate.Set("points", points);
			if (!def.CanRun(slate, Find.World)) { Log.Warning("DreamsOutposts: TradeRequest quest could not run."); return; }
			Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(def, slate);
			if (!quest.hidden && def.sendAvailableLetter) QuestUtility.SendLetterQuestAvailable(quest);
		}
		public override string GetPreview(OutpostEventContext context) => "DreamsOutposts.EventEffect.TradeRequest".Translate();
	}

	public class OutpostEventEffect_DisableRandomResearchFacility : OutpostEventEffect
	{
		public int durationTicks;
		public override void Apply(OutpostEventContext context)
		{
			Outpost outpost = context?.outpost;
			if (outpost == null || durationTicks <= 0) return;
			OutpostTypeDef research = DefDatabase<OutpostTypeDef>.GetNamedSilentFail("DreamsOutposts_Research");
			List<OutpostFacility> candidates = outpost.OperationalFacilities.Where(f => f?.def != null && (f == outpost.coreFacility || f.def.allowedOutpostTypes?.Contains(research) == true)).ToList();
			if (!candidates.TryRandomElement(out OutpostFacility selected)) return;
			OutpostTemporaryEffect effect = new OutpostTemporaryEffect { kind = OutpostTemporaryEffectKind.FacilityDisabled, expireTick = Find.TickManager.TicksGame + durationTicks, expectedFacility = selected.def, targetCore = selected == outpost.coreFacility };
			if (!effect.targetCore)
			{
				for (int i = 0; i < outpost.extensionSlots.Count; i++) if (outpost.extensionSlots[i]?.facility == selected) { effect.targetSlot = i; break; }
			}
			OutpostTemporaryEffectUtility.Add(outpost, effect);
		}
		public override string GetPreview(OutpostEventContext context) => "DreamsOutposts.EventEffect.DisableResearchFacility".Translate(durationTicks.ToStringTicksToPeriod());
	}

	public class OutpostEventEffect_OpenTemporaryMarket : OutpostEventEffect
	{
		public float maxMarketValue = 5000f;
		public float returnFactor = 1.5f;
		public override void Apply(OutpostEventContext context)
		{
			if (context?.outpost != null) Find.WindowStack.Add(new Window_OutpostTemporaryMarket(context.outpost, maxMarketValue, returnFactor));
		}
		public override string GetPreview(OutpostEventContext context) => "DreamsOutposts.EventEffect.TemporaryMarket".Translate(maxMarketValue.ToStringMoney(), returnFactor.ToString("0.##"));
	}
}
