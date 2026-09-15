using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public class OutpostEventEffect_GeneratePawn : OutpostEventEffect
	{
		public PawnKindDef pawnKindDef;

		public int count = 1;

		public override void Apply(OutpostEventContext context)
		{
			if (context?.outpost == null || pawnKindDef == null || count <= 0)
			{
				return;
			}
			for (int i = 0; i < count; i++)
			{
				Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(pawnKindDef, null, PawnGenerationContext.NonPlayer, context.outpost.Tile, forceGenerateNewPawn: true));
				pawn.SetFaction(Faction.OfPlayer);
				if (!OutpostUtility.MovePawnIntoOutpost(context.outpost, pawn))
				{
					Log.Error("Failed to add generated pawn " + pawn.ToStringSafe() + " to outpost " + context.outpost.Label + ".");
					pawn.Destroy();
				}
			}
		}

		public override string GetPreview(OutpostEventContext context)
		{
			return "DreamsOutposts.EventEffect.GeneratePawn".Translate(count, pawnKindDef?.LabelCap ?? "DreamsOutposts.Unknown".Translate()).ToString();
		}
	}

	public class OutpostEventEffect_AdjustFactionGoodwill : OutpostEventEffect
	{
		public FactionDef factionDef;

		public int amount;

		public HistoryEventDef reason;

		public override void Apply(OutpostEventContext context)
		{
			if (amount == 0 || Faction.OfPlayer == null)
			{
				return;
			}
			List<Faction> candidates = Find.FactionManager.AllFactionsVisible
				.Where(candidate => candidate != null && !candidate.IsPlayer && !candidate.defeated && !candidate.def.permanentEnemy && (factionDef == null || candidate.def == factionDef) && candidate.CanChangeGoodwillFor(Faction.OfPlayer, amount))
				.ToList();
			if (candidates.TryRandomElement(out Faction faction))
			{
				faction.TryAffectGoodwillWith(Faction.OfPlayer, amount, canSendMessage: true, canSendHostilityLetter: true, reason, context?.outpost);
			}
			else
			{
				Log.Warning("No eligible faction found for outpost event goodwill effect" + (factionDef == null ? "." : " using " + factionDef.defName + "."));
			}
		}

		public override string GetPreview(OutpostEventContext context)
		{
			string target = factionDef?.LabelCap ?? "DreamsOutposts.EventEffect.RandomFaction".Translate();
			string signedAmount = amount > 0 ? "+" + amount : amount.ToString();
			return "DreamsOutposts.EventEffect.AdjustFactionGoodwill".Translate(target, signedAmount).ToString();
		}
	}

	public class OutpostEventEffect_GenerateRandomItems : OutpostEventEffect
	{
		/// <summary>补差生成的最大轮数，避免极端脸黑时无限生成。</summary>
		private const int MaxGenerateRounds = 6;

		/// <summary>缺口低于目标市值的这个比例时，剩下的零头交给市值填充物补平。</summary>
		private const float ShortfallTolerancePct = 0.05f;

		/// <summary>与原版 RewardsGenerator 一致：缺口不足该物资的 15 倍时不再用它填充。</summary>
		private const float FillerMinMultiplier = 15f;

		private static List<ThingDef> marketValueFillers;

		public FloatRange marketValue;

		public ThingSetMakerDef thingSetMaker;

		public override void Apply(OutpostEventContext context)
		{
			if (context?.outpost == null || marketValue.max <= 0f)
			{
				return;
			}
			ThingSetMakerDef maker = thingSetMaker ?? ThingSetMakerDefOf.Reward_ItemsStandard;
			if (maker?.root == null)
			{
				Log.Error("Outpost random item reward has no thing set maker to generate from.");
				return;
			}
			float target = marketValue.RandomInRange;
			if (target <= 0f)
			{
				return;
			}
			List<Thing> things = GenerateUpToTargetValue(maker, target, context.outpost.Tile, out float generatedValue);
			OutpostItemRewardUtility.AddRange(context, things);
			AddMarketValueFillers(context, things, target - generatedValue);
		}

		/// <summary>
		/// 按目标市值生成物资。原版 ThingSetMaker_Sum 会把非末位选项的市值下限强制改成 0，
		/// 于是内部的 ThingSetMaker_MarketValue 只在 [0, 请求值] 里随机取一个目标值，
		/// 实际到手常常远低于请求值（例如请求 1000 白银只给一个 100 白银的基因包，
		/// 原版是靠 RewardsGenerator 另外补市值填充物才凑够的）。
		/// 这里仍然调用原版生成器本身，但按"还差多少价值"反复生成，把缺口一轮轮补回来。
		/// </summary>
		private static List<Thing> GenerateUpToTargetValue(ThingSetMakerDef maker, float target, PlanetTile tile, out float generatedValue)
		{
			List<Thing> result = new List<Thing>();
			generatedValue = 0f;
			float tolerance = Mathf.Max(target * ShortfallTolerancePct, 1f);
			for (int round = 0; round < MaxGenerateRounds; round++)
			{
				float remaining = target - generatedValue;
				if (remaining <= tolerance)
				{
					break;
				}
				ThingSetMakerParams parms = default(ThingSetMakerParams);
				parms.totalMarketValueRange = new FloatRange(remaining, remaining);
				parms.tile = tile;
				List<Thing> roundThings = maker.root.Generate(parms);
				if (roundThings.NullOrEmpty())
				{
					break;
				}
				float roundValue = TotalMarketValue(roundThings);
				if (roundValue <= 0f)
				{
					for (int i = 0; i < roundThings.Count; i++)
					{
						roundThings[i].Destroy();
					}
					break;
				}
				result.AddRange(roundThings);
				generatedValue += roundValue;
			}
			return result;
		}

		private static float TotalMarketValue(List<Thing> things)
		{
			float num = 0f;
			for (int i = 0; i < things.Count; i++)
			{
				num += things[i].MarketValue * (float)things[i].stackCount;
			}
			return num;
		}

		/// <summary>
		/// 用原版同款市值填充物把最后一点零头补平，保证到手价值贴近请求值。
		/// 逻辑与原版 RewardsGenerator.AddMarketValueFillers 一致：优先沿用本次奖励里
		/// 已经出现的填充物，否则从白银、黄金、铀、翡翠、玻璃钢里随机挑一种。
		/// </summary>
		private static void AddMarketValueFillers(OutpostEventContext context, List<Thing> things, float shortfall)
		{
			if (shortfall <= 0f)
			{
				return;
			}
			List<ThingDef> candidates = new List<ThingDef>();
			foreach (ThingDef filler in MarketValueFillers)
			{
				if (filler != null && filler.BaseMarketValue > 0f && shortfall / filler.BaseMarketValue >= FillerMinMultiplier)
				{
					candidates.Add(filler);
				}
			}
			if (candidates.Count == 0)
			{
				return;
			}
			ThingDef chosen = null;
			if (!things.NullOrEmpty())
			{
				for (int i = 0; i < things.Count; i++)
				{
					if (candidates.Contains(things[i].def))
					{
						chosen = things[i].def;
						break;
					}
				}
			}
			if (chosen == null)
			{
				chosen = candidates.RandomElement();
			}
			int count = GenMath.RoundRandom(shortfall / chosen.BaseMarketValue);
			if (count > 0)
			{
				OutpostItemRewardUtility.Add(context, chosen, count);
			}
		}

		private static List<ThingDef> MarketValueFillers
		{
			get
			{
				if (marketValueFillers == null)
				{
					marketValueFillers = new List<ThingDef>
					{
						ThingDefOf.Silver,
						ThingDefOf.Gold,
						ThingDefOf.Uranium,
						ThingDefOf.Jade,
						ThingDefOf.Plasteel
					};
				}
				return marketValueFillers;
			}
		}

		public override string GetPreview(OutpostEventContext context)
		{
			return "DreamsOutposts.EventEffect.GenerateRandomItems".Translate(marketValue.ToString()).ToString();
		}
	}

	public class OutpostEventEffect_AddRandomAnimalProducts : OutpostEventEffect
	{
		public float minBodySize = 2f;

		public int animalCount = 10;

		public override void Apply(OutpostEventContext context)
		{
			if (context?.outpost == null || animalCount <= 0)
			{
				return;
			}
			BiomeDef biome = Find.WorldGrid[context.outpost.Tile].PrimaryBiome;
			PawnKindDef animalKind = null;
			if (biome != null)
			{
				biome.AllWildAnimals
					.Where(IsEligibleAnimal)
					.TryRandomElementByWeight(kind => biome.CommonalityOfAnimal(kind), out animalKind);
			}
			if (animalKind == null)
			{
				animalKind = DefDatabase<PawnKindDef>.GetNamedSilentFail("Cow");
			}
			ThingDef race = animalKind?.race;
			if (race?.race?.meatDef == null || race.race.leatherDef == null)
			{
				Log.Error("Outpost animal products effect could not find an eligible animal or the Cow fallback.");
				return;
			}
			int meatCount = GenMath.RoundRandom(race.GetStatValueAbstract(StatDefOf.MeatAmount) * animalCount);
			int leatherCount = GenMath.RoundRandom(race.GetStatValueAbstract(StatDefOf.LeatherAmount) * animalCount);
			if (meatCount > 0)
			{
				OutpostItemRewardUtility.Add(context, race.race.meatDef, meatCount);
			}
			if (leatherCount > 0)
			{
				OutpostItemRewardUtility.Add(context, race.race.leatherDef, leatherCount);
			}
		}

		private bool IsEligibleAnimal(PawnKindDef kind)
		{
			return kind?.race?.race != null
				&& kind.race.race.Animal
				&& kind.race.race.baseBodySize > minBodySize
				&& kind.race.race.meatDef != null
				&& kind.race.race.leatherDef != null;
		}

		public override string GetPreview(OutpostEventContext context)
		{
			return "DreamsOutposts.EventEffect.AddRandomAnimalProducts".Translate().ToString();
		}
	}

	public class OutpostEventEffect_RaidPlayerHome : OutpostEventEffect
	{
		public FactionDef factionDef;
		public float pointsFactor = 1f;

		public override void Apply(OutpostEventContext context)
		{
			Map map = Find.AnyPlayerHomeMap;
			Faction faction = factionDef == null ? null : Find.FactionManager.FirstFactionOfDef(factionDef);
			if (map == null || faction == null || pointsFactor <= 0f)
			{
				Log.Warning("Outpost event could not launch a raid: no player home map, faction, or positive points factor was available.");
				return;
			}
			IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentDefOf.RaidEnemy.category, map);
			parms.forced = true;
			parms.faction = faction;
			parms.points *= pointsFactor;
			if (!IncidentDefOf.RaidEnemy.Worker.CanFireNow(parms) || !IncidentDefOf.RaidEnemy.Worker.TryExecute(parms))
			{
				Log.Warning("Outpost event failed to launch a raid from faction " + factionDef.defName + ".");
			}
		}

		public override string GetPreview(OutpostEventContext context)
		{
			return "DreamsOutposts.EventEffect.RaidPlayerHome".Translate(pointsFactor.ToStringPercent()).ToString();
		}
	}
}
