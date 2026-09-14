using System.Collections.Generic;
using System.Linq;
using RimWorld;
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
		public FloatRange marketValue;

		public ThingSetMakerDef thingSetMaker;

		public override void Apply(OutpostEventContext context)
		{
			if (context?.outpost == null || marketValue.max <= 0f)
			{
				return;
			}
			ThingSetMakerDef maker = thingSetMaker ?? ThingSetMakerDefOf.Reward_ItemsStandard;
			ThingSetMakerParams parms = default(ThingSetMakerParams);
			parms.totalMarketValueRange = marketValue;
			parms.tile = context.outpost.Tile;
			List<Thing> things = maker.root.Generate(parms);
			OutpostItemRewardUtility.AddRange(context, things);
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
