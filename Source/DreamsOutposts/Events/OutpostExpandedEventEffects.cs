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
			return PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, faction, PawnGenerationContext.NonPlayer, context.outpost.Tile, forceGenerateNewPawn: true));
		}

		public override string GetPreview(OutpostEventContext context) => "DreamsOutposts.EventEffect.GeneratePawnRarity".Translate(count, rarity.ToString());
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

	public class Window_OutpostTemporaryMarket : Window
	{
		private readonly Outpost outpost;
		private readonly float cap;
		private readonly float factor;
		private readonly Dictionary<Thing, int> selected = new Dictionary<Thing, int>();
		private Vector2 scroll;
		public override Vector2 InitialSize => new Vector2(720f, 650f);
		public Window_OutpostTemporaryMarket(Outpost outpost, float cap, float factor) { this.outpost = outpost; this.cap = cap; this.factor = factor; doCloseX = true; absorbInputAroundWindow = true; }

		public override void DoWindowContents(Rect inRect)
		{
			Text.Font = GameFont.Medium; Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 35f), "DreamsOutposts.TemporaryMarket.Title".Translate()); Text.Font = GameFont.Small;
			float total = SelectedValue();
			Widgets.Label(new Rect(inRect.x, inRect.y + 40f, inRect.width, 28f), "DreamsOutposts.TemporaryMarket.Value".Translate(total.ToStringMoney(), cap.ToStringMoney(), (total * factor).ToStringMoney()));
			List<Thing> items = outpost.InventoryItems.Where(Eligible).ToList();
			Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, items.Count * 34f);
			Rect scrollRect = new Rect(inRect.x, inRect.y + 75f, inRect.width, inRect.height - 125f);
			Widgets.BeginScrollView(scrollRect, ref scroll, viewRect);
			for (int i = 0; i < items.Count; i++)
			{
				Thing thing = items[i]; Rect row = new Rect(0f, i * 34f, viewRect.width, 32f);
				Widgets.Label(new Rect(row.x, row.y, row.width - 250f, row.height), thing.LabelCap + " ×" + thing.stackCount + "  (" + thing.MarketValue.ToStringMoney() + ")");
				int value = selected.TryGetValue(thing, out int current) ? current : 0;
				if (Widgets.ButtonText(new Rect(row.xMax - 235f, row.y, 32f, 30f), "-")) value--;
				Widgets.Label(new Rect(row.xMax - 195f, row.y, 65f, 30f), value.ToString());
				if (Widgets.ButtonText(new Rect(row.xMax - 130f, row.y, 32f, 30f), "+")) value++;
				if (Widgets.ButtonText(new Rect(row.xMax - 90f, row.y, 90f, 30f), "DreamsOutposts.TemporaryMarket.All".Translate())) value = thing.stackCount;
				value = Mathf.Clamp(value, 0, thing.stackCount);
				float without = total - current * thing.MarketValue;
				value = Mathf.Min(value, Mathf.FloorToInt((cap - without) / Mathf.Max(thing.MarketValue, 0.01f)));
				if (value > 0) selected[thing] = value; else selected.Remove(thing);
				total = SelectedValue();
			}
			Widgets.EndScrollView();
			if (Widgets.ButtonText(new Rect(inRect.xMax - 170f, inRect.yMax - 42f, 170f, 42f), "DreamsOutposts.TemporaryMarket.Exchange".Translate())) Exchange();
		}

		private void Exchange()
		{
			float value = SelectedValue();
			if (value <= 0f) return;
			foreach (KeyValuePair<Thing, int> pair in selected.ToList())
			{
				if (pair.Key == null || pair.Key.Destroyed || !outpost.inventory.Contains(pair.Key)) continue;
				Thing taken = outpost.inventory.Take(pair.Key, Mathf.Min(pair.Value, pair.Key.stackCount)); taken?.Destroy();
			}
			OutpostEventContext context = new OutpostEventContext { outpost = outpost, itemRewards = new OutpostItemRewardCollector(outpost) };
			new OutpostEventEffect_GenerateRandomItems { marketValue = new FloatRange(value * factor, value * factor) }.Apply(context);
			context.itemRewards.Commit();
			Close();
		}
		private float SelectedValue() => selected.Where(p => p.Key != null && !p.Key.Destroyed).Sum(p => p.Key.MarketValue * p.Value);
		private static bool Eligible(Thing t) => t != null && !t.Destroyed && t.def.category == ThingCategory.Item && t.def.stackLimit > 1 && t.MarketValue > 0f && t.questTags.NullOrEmpty();
	}
}
