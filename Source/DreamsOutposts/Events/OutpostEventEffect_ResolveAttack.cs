using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public class OutpostEventEffect_ResolveAttack : OutpostEventEffect
	{
		public float pointsPerSilver = 4f;
		public float victoryLootFactor = 0.7f;
		public float incursionLootFactor = 0.25f;

		public static int Outcome(float ratio)
		{
			return ratio >= 1.45f ? 0 : ratio >= 1f ? 1 : ratio >= 0.65f ? 2 : ratio >= 0.25f ? 3 : 4;
		}

		public static float RaidFactor(float ratio)
		{
			if (ratio >= 1.45f) return 0f;
			if (ratio >= 1f) return Mathf.Lerp(0.4f, 0.2f, Mathf.InverseLerp(1f, 1.45f, ratio));
			if (ratio >= 0.65f) return Mathf.Lerp(0.8f, 0.6f, Mathf.InverseLerp(0.65f, 1f, ratio));
			if (ratio >= 0.25f) return Mathf.Lerp(1.2f, 0.8f, Mathf.InverseLerp(0.25f, 0.65f, ratio));
			return 1.2f;
		}

		public override void Apply(OutpostEventContext context)
		{
			OutpostAttackState state = context?.instance?.attack;
			if (state == null || state.resolved || context.outpost == null || state.strength <= 0f) return;
			// Prevent a repeated timeout from awarding loot or killing colonists twice.
			state.resolved = true;
			float defense = Mathf.Max(0f, context.outpost.Defense);
			float ratio = defense / state.strength;
			int outcome = Outcome(ratio);
			float raidFactor = RaidFactor(ratio);
			float lootFactor = outcome == 0 ? 1f : outcome == 1 ? victoryLootFactor : outcome == 2 ? incursionLootFactor : 0f;
			float budget = state.raidPoints / Mathf.Max(pointsPerSilver, 0.01f) * lootFactor;
			List<string> loot = new List<string>();
			float lootValue = OutpostAttackLootUtility.Generate(context, budget, loot);
			// Automated attacks report loot in the battle letter rather than opening a modal.
			context.itemRewards.Commit(false);
			string text = ("DreamsOutposts.Attack.Result" + outcome).Translate() + "\n\n"
				+ "DreamsOutposts.Attack.Comparison".Translate(defense.ToString("0.0"), state.strength.ToString("0.0"), ratio.ToString("0.00"));
			if (loot.Count > 0)
				text += "\n\n" + "DreamsOutposts.Attack.Loot".Translate(lootValue.ToString("0"), budget.ToString("0")) + "\n" + string.Join("\n", loot.ToArray());
			if (outcome >= 3)
			{
				float factor = outcome == 3 ? 0.5f : 0.2f;
				new OutpostEventEffect_AddTemporaryProductionFactor { factor = factor, durationTicks = 180000 }.Apply(context);
				text += "\n\n" + "DreamsOutposts.Attack.ProductionLoss".Translate(factor.ToString("0.0"));
			}
			if (outcome == 4)
			{
				List<Pawn> colonists = context.outpost.Colonists.Where(p => !p.Dead && !p.Destroyed).ToList();
				int count = Mathf.Min(colonists.Count, Mathf.Max(1, Mathf.CeilToInt(colonists.Count * 0.5f)));
				List<string> casualties = new List<string>();
				for (int i = 0; i < count; i++)
				{
					Pawn pawn = colonists.RandomElement();
					colonists.Remove(pawn);
					context.outpost.pawns.Remove(pawn);
					if (!Find.WorldPawns.Contains(pawn)) Find.WorldPawns.PassToWorld(pawn);
					pawn.Kill(null);
					if (pawn.Dead) casualties.Add(pawn.LabelShortCap);
					else OutpostUtility.MovePawnIntoOutpost(context.outpost, pawn);
				}
				if (casualties.Count > 0) text += "\n\n" + "DreamsOutposts.Attack.Casualties".Translate(string.Join(", ", casualties.ToArray()));
			}
			if (raidFactor > 0f)
			{
				Map map = state.homeMap;
				if (map == null || !Find.Maps.Contains(map) || !map.IsPlayerHome) map = Find.AnyPlayerHomeMap;
				bool fired = false;
				if (map != null && state.faction != null && !state.faction.defeated && state.faction.HostileTo(Faction.OfPlayer))
				{
					IncidentParms parms = new IncidentParms
					{
						target = map, faction = state.faction, forced = true,
						points = state.raidPoints * raidFactor
					};
					fired = IncidentDefOf.RaidEnemy.Worker.CanFireNow(parms) && IncidentDefOf.RaidEnemy.Worker.TryExecute(parms);
				}
				text += "\n\n" + (fired ? "DreamsOutposts.Attack.Raid".Translate(raidFactor.ToString("0.00"), (state.raidPoints * raidFactor).ToString("0")) : "DreamsOutposts.Attack.RaidFailed".Translate());
			}
			Find.LetterStack.ReceiveLetter("DreamsOutposts.Attack.ResultTitle".Translate(context.outpost.LabelCap, ("DreamsOutposts.Attack.Outcome" + outcome).Translate()), text,
				outcome <= 1 ? LetterDefOf.PositiveEvent : LetterDefOf.NegativeEvent, new LookTargets(context.outpost));
		}

		public override string GetPreview(OutpostEventContext context)
		{
			return "DreamsOutposts.Attack.Wait".Translate();
		}
	}
}
