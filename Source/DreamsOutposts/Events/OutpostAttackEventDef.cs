using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace DreamsOutposts
{
	public class OutpostAttackEventDef : OutpostEventDef
	{
		public SimpleCurve strengthByRaidPoints;
		public FloatRange strengthFactor = new FloatRange(0.8f, 1.2f);

		public static List<PawnGenOption> HumanCombatOptions(Faction faction)
		{
			return faction?.def?.pawnGroupMakers?
				.Where(m => m != null && m.kindDef == PawnGroupKindDefOf.Combat && m.options != null)
				.SelectMany(m => m.options)
				.Where(o => o?.kind?.race?.race != null && o.kind.RaceProps.Humanlike
					&& o.selectionWeight > 0f && o.kind.combatPower > 0f && !o.kind.factionLeader
					&& !o.kind.trader && !o.kind.isBoss).ToList() ?? new List<PawnGenOption>();
		}

		public override bool InitializeInstance(Outpost outpost, OutpostEventInstance instance)
		{
			Map map = Find.AnyPlayerHomeMap;
			if (map == null || !Find.Storyteller.difficulty.allowBigThreats || strengthByRaidPoints == null) return false;
			List<Faction> factions = Find.FactionManager.AllFactionsListForReading.Where(f =>
				f != null && !f.IsPlayer && !f.Hidden && !f.temporary && !f.defeated
				&& f.def.humanlikeFaction && f.HostileTo(Faction.OfPlayer)
				&& HumanCombatOptions(f).Count > 0).ToList();
			if (!factions.TryRandomElement(out Faction faction)) return false;
			float points = StorytellerUtility.DefaultThreatPointsNow(map);
			instance.attack = new OutpostAttackState
			{
				faction = faction,
				homeMap = map,
				raidPoints = points,
				strength = strengthByRaidPoints.Evaluate(points) * strengthFactor.RandomInRange
			};
			return true;
		}

		public override string DescriptionFor(OutpostEventInstance instance)
		{
			OutpostAttackState state = instance?.attack;
			return base.DescriptionFor(instance) + (state == null ? string.Empty : "\n\n" +
				"DreamsOutposts.Attack.Enemy".Translate(state.faction?.Name ?? "?", state.strength.ToString("0.0"), state.raidPoints.ToString("0")).ToString());
		}

		public override IEnumerable<string> ConfigErrors()
		{
			foreach (string error in base.ConfigErrors()) yield return error;
			if (strengthByRaidPoints == null) yield return "strengthByRaidPoints is required.";
			if (strengthFactor.min <= 0f || strengthFactor.max < strengthFactor.min) yield return "strengthFactor must be positive and ordered.";
			if (durationTicks != 20000) yield return "Outpost attacks must last 8 hours (20000 ticks).";
			if (weight != 0f) yield return "Outpost attacks use their own global scheduler; weight must be zero.";
		}
	}
}
