using RimWorld;
using System.Linq;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public class OutpostEventEffect_AddTemporaryProductionFactor : OutpostEventEffect
	{
		public float factor = 1f;
		public int delayTicks;
		public int durationTicks;
		public string productionTag;
		public bool excludeProductionTag;
		public OutpostFacilityDef producingFacility;
		public bool targetCore;

		public override void Apply(OutpostEventContext context)
		{
			if (context?.outpost == null || delayTicks < 0 || durationTicks <= 0 || factor < 0f) return;
			int startTick = Find.TickManager.TicksGame + delayTicks;
			OutpostTemporaryEffectUtility.Add(context.outpost, new OutpostTemporaryEffect
			{
				kind = OutpostTemporaryEffectKind.ProductionFactor,
				startTick = startTick,
				expireTick = startTick + durationTicks,
				value = factor,
				productionTag = productionTag,
				excludeProductionTag = excludeProductionTag,
				producingFacility = producingFacility,
				targetCore = targetCore
			});
		}

		public override string GetPreview(OutpostEventContext context)
		{
			string target = targetCore
				? "DreamsOutposts.CoreFacility".Translate().ToString()
				: (producingFacility != null ? producingFacility.LabelCap.ToString() : ProductionTargetLabel());
			return "DreamsOutposts.EventEffect.TemporaryProductionFactor".Translate(target, factor.ToString("0.##"), durationTicks.ToStringTicksToPeriod()).ToString();
		}

		private string ProductionTargetLabel()
		{
			if (string.IsNullOrWhiteSpace(productionTag)) return "DreamsOutposts.AllProduction".Translate().ToString();
			if (productionTag == "Research") return "DreamsOutposts.ProductionTag.Research".Translate().ToString();
			return productionTag;
		}
	}

	public class OutpostEventEffect_AddRandomFacilityProductionFactor : OutpostEventEffect
	{
		public float factor = 1f;
		public int delayTicks;
		public int durationTicks;
		public bool nextProductionOnly;

		public override void Apply(OutpostEventContext context)
		{
			if (context?.outpost == null || factor < 0f || delayTicks < 0 || (!nextProductionOnly && durationTicks <= 0)) return;
			var candidates = context.outpost.OperationalFacilities.Where(facility => facility?.def?.IsProducer == true).ToList();
			if (!candidates.TryRandomElement(out OutpostFacility selected)) return;
			int startTick = Find.TickManager.TicksGame + delayTicks;
			OutpostTemporaryEffect effect = new OutpostTemporaryEffect
			{
				kind = OutpostTemporaryEffectKind.ProductionFactor,
				startTick = startTick,
				expireTick = nextProductionOnly ? int.MaxValue : startTick + durationTicks,
				value = factor,
				consumeAfterProduction = nextProductionOnly,
				expectedFacility = selected.def,
				targetCore = selected == context.outpost.coreFacility
			};
			if (!effect.targetCore)
			{
				for (int i = 0; i < (context.outpost.extensionSlots?.Count ?? 0); i++)
				{
					if (context.outpost.extensionSlots[i]?.facility == selected) { effect.targetSlot = i; break; }
				}
				if (effect.targetSlot < 0) return;
			}
			OutpostTemporaryEffectUtility.Add(context.outpost, effect);
		}

		public override string GetPreview(OutpostEventContext context)
		{
			return nextProductionOnly
				? "DreamsOutposts.EventEffect.RandomFacilityNextProductionFactor".Translate(factor.ToString("0.##")).ToString()
				: "DreamsOutposts.EventEffect.RandomFacilityProductionFactor".Translate(factor.ToString("0.##"), durationTicks.ToStringTicksToPeriod()).ToString();
		}
	}

	public class OutpostEventEffect_AddTemporaryCategoryOffset : OutpostEventEffect
	{
		public OutpostEventCategoryDef category;
		public float offset;
		public int durationTicks;

		public override void Apply(OutpostEventContext context)
		{
			if (context?.outpost == null || category == null || durationTicks <= 0) return;
			OutpostTemporaryEffectUtility.Add(context.outpost, new OutpostTemporaryEffect
			{
				kind = OutpostTemporaryEffectKind.CategoryOffset,
				expireTick = Find.TickManager.TicksGame + durationTicks,
				value = offset,
				category = category
			});
		}

		public override string GetPreview(OutpostEventContext context)
		{
			string sign = offset >= 0f ? "+" : string.Empty;
			return "DreamsOutposts.EventEffect.TemporaryCategoryOffset".Translate(category?.LabelCap ?? "DreamsOutposts.Unknown".Translate(), sign + offset.ToString("0.##"), durationTicks.ToStringTicksToPeriod()).ToString();
		}
	}

	public class OutpostEventEffect_AddTemporaryMovementCostFactor : OutpostEventEffect
	{
		public float factor = 1f;
		public int durationTicks;

		public override void Apply(OutpostEventContext context)
		{
			if (context?.outpost == null || durationTicks <= 0 || factor < 0f) return;
			OutpostTemporaryEffectUtility.Add(context.outpost, new OutpostTemporaryEffect
			{
				kind = OutpostTemporaryEffectKind.MovementCostFactor,
				expireTick = Find.TickManager.TicksGame + durationTicks,
				value = factor
			});
		}

		public override string GetPreview(OutpostEventContext context)
		{
			return "DreamsOutposts.EventEffect.TemporaryMovementFactor".Translate(factor.ToString("0.##"), durationTicks.ToStringTicksToPeriod()).ToString();
		}
	}

	public class OutpostEventEffect_DisableRandomFacility : OutpostEventEffect
	{
		public int durationTicks;
		public bool includeCore = true;
		public string facilityTag;
		public bool producersOnly;

		public override void Apply(OutpostEventContext context)
		{
			OutpostTemporaryEffectUtility.DisableRandomFacility(context?.outpost, durationTicks, includeCore, facilityTag, producersOnly);
		}

		public override string GetPreview(OutpostEventContext context)
		{
			return "DreamsOutposts.EventEffect.DisableRandomFacility".Translate(durationTicks.ToStringTicksToPeriod()).ToString();
		}
	}

	public class OutpostEventEffect_AddTemporaryDefenseOffset : OutpostEventEffect
	{
		public float offset;
		public int durationTicks;

		public override void Apply(OutpostEventContext context)
		{
			if (context?.outpost == null || durationTicks <= 0 || offset == 0f) return;
			OutpostTemporaryEffectUtility.Add(context.outpost, new OutpostTemporaryEffect
			{
				kind = OutpostTemporaryEffectKind.DefenseOffset,
				expireTick = Find.TickManager.TicksGame + durationTicks,
				value = offset
			});
		}

		public override string GetPreview(OutpostEventContext context)
		{
			string sign = offset > 0f ? "+" : string.Empty;
			return "DreamsOutposts.EventEffect.TemporaryDefenseOffset".Translate(sign + offset.ToString("0.#"), durationTicks.ToStringTicksToPeriod()).ToString();
		}
	}

	public class OutpostEventEffect_AddResearchPoints : OutpostEventEffect
	{
		public float points;

		public override void Apply(OutpostEventContext context)
		{
			ResearchProjectDef project = Find.ResearchManager?.GetProject();
			if (project != null && points > 0f) Find.ResearchManager.AddProgress(project, points);
		}

		public override string GetPreview(OutpostEventContext context)
		{
			return "DreamsOutposts.EventEffect.AddResearchPoints".Translate(Mathf.Max(points, 0f).ToString("0.#")).ToString();
		}
	}
}
