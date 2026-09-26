using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public class OutpostFacilityCompProperties_Indoctrination : OutpostFacilityCompProperties
	{
		public int intervalTicks = 120000;
		public int minSocial = 5;

		public OutpostFacilityCompProperties_Indoctrination()
		{
			compClass = typeof(OutpostFacilityComp_Indoctrination);
		}

		public override IEnumerable<string> ConfigErrors()
		{
			foreach (string error in base.ConfigErrors()) yield return error;
			if (intervalTicks <= 0) yield return "intervalTicks must be positive.";
			if (minSocial < 0) yield return "minSocial cannot be negative.";
		}
	}

	public class OutpostIndoctrinationState : IExposable
	{
		public Pawn pawn;
		public int progressTicks;

		public void ExposeData()
		{
			Scribe_References.Look(ref pawn, "pawn");
			Scribe_Values.Look(ref progressTicks, "progressTicks", 0);
		}
	}

	public class OutpostFacilityComp_Indoctrination : OutpostFacilityComp
	{
		private List<OutpostIndoctrinationState> targetStates = new List<OutpostIndoctrinationState>();

		private OutpostFacilityCompProperties_Indoctrination Props =>
			(OutpostFacilityCompProperties_Indoctrination)props;

		public int IntervalTicks => Props.intervalTicks;
		public int MinSocial => Props.minSocial;

		public Ideo PrimaryIdeo =>
			ModsConfig.IdeologyActive && Find.IdeoManager != null && !Find.IdeoManager.classicMode
				? Faction.OfPlayer?.ideos?.PrimaryIdeo
				: null;

		public override void Initialize(OutpostFacility parent, OutpostFacilityCompProperties props)
		{
			base.Initialize(parent, props);
			if (targetStates == null)
			{
				targetStates = new List<OutpostIndoctrinationState>();
			}
		}

		public override void Update(Outpost outpost, int delta)
		{
			if (!ModsConfig.IdeologyActive || Find.IdeoManager == null || Find.IdeoManager.classicMode)
			{
				return;
			}

			Ideo primary = PrimaryIdeo;
			Pawn guide = BestGuide(outpost, primary);
			SynchronizeTargets(outpost, primary);

			if (primary == null || guide == null)
			{
				return;
			}

			for (int i = targetStates.Count - 1; i >= 0; i--)
			{
				OutpostIndoctrinationState state = targetStates[i];
				Pawn target = state?.pawn;
				if (!IsEligibleTarget(target, primary))
				{
					targetStates.RemoveAt(i);
					continue;
				}

				if (delta > 0)
				{
					state.progressTicks += delta;
				}

				while (state.progressTicks >= Props.intervalTicks)
				{
					float reduction = InteractionWorker_ConvertIdeoAttempt.CertaintyReduction(guide, target);
					bool converted = target.ideo.IdeoConversionAttempt(reduction, primary);
					if (converted)
					{
						Messages.Message(
							"DreamsOutposts.Indoctrination.Converted".Translate(
								target.LabelShortCap, primary.name, guide.LabelShortCap).ToString(),
							MessageTypeDefOf.PositiveEvent);
						targetStates.RemoveAt(i);
						break;
					}

					state.progressTicks -= Props.intervalTicks;
				}
			}
		}

		public override void UpdateDisabled(Outpost outpost, int delta)
		{
			SynchronizeTargets(outpost, PrimaryIdeo);
		}

		public override void ExposeData()
		{
			Scribe_Collections.Look(ref targetStates, "targetStates", LookMode.Deep);
			if (Scribe.mode == LoadSaveMode.PostLoadInit && targetStates == null)
			{
				targetStates = new List<OutpostIndoctrinationState>();
			}
		}

		public Pawn CurrentGuide(Outpost outpost)
		{
			return BestGuide(outpost, PrimaryIdeo);
		}

		public List<OutpostIndoctrinationState> StatesForUi(Outpost outpost)
		{
			Ideo primary = PrimaryIdeo;
			SynchronizeTargets(outpost, primary);
			List<OutpostIndoctrinationState> result = new List<OutpostIndoctrinationState>();
			for (int i = 0; i < targetStates.Count; i++)
			{
				OutpostIndoctrinationState state = targetStates[i];
				if (state?.pawn != null && IsEligibleTarget(state.pawn, primary))
				{
					result.Add(state);
				}
			}
			return result;
		}

		public void ProgressFor(OutpostIndoctrinationState state, out float progress, out int remaining)
		{
			int ticks = state == null ? 0 : Mathf.Clamp(state.progressTicks, 0, Props.intervalTicks);
			progress = Mathf.Clamp01((float)ticks / Props.intervalTicks);
			remaining = Mathf.Max(Props.intervalTicks - ticks, 0);
		}

		private void SynchronizeTargets(Outpost outpost, Ideo primary)
		{
			if (targetStates == null)
			{
				targetStates = new List<OutpostIndoctrinationState>();
			}
			if (outpost?.pawns == null || primary == null)
			{
				return;
			}

			List<Pawn> pawns = outpost.pawns.InnerListForReading;
			HashSet<Pawn> presentTargets = new HashSet<Pawn>();
			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn pawn = pawns[i];
				if (IsEligibleTarget(pawn, primary))
				{
					presentTargets.Add(pawn);
				}
			}

			HashSet<Pawn> seen = new HashSet<Pawn>();
			for (int i = targetStates.Count - 1; i >= 0; i--)
			{
				OutpostIndoctrinationState state = targetStates[i];
				if (state?.pawn == null || !presentTargets.Contains(state.pawn) || !seen.Add(state.pawn))
				{
					targetStates.RemoveAt(i);
					continue;
				}
				state.progressTicks = Mathf.Max(state.progressTicks, 0);
			}

			foreach (Pawn pawn in presentTargets)
			{
				if (seen.Contains(pawn))
				{
					continue;
				}
				targetStates.Add(new OutpostIndoctrinationState
				{
					pawn = pawn,
					progressTicks = 0
				});
			}
		}

		private Pawn BestGuide(Outpost outpost, Ideo primary)
		{
			if (outpost?.pawns == null || primary == null)
			{
				return null;
			}

			Pawn best = null;
			float bestPower = float.MinValue;
			List<Pawn> pawns = outpost.pawns.InnerListForReading;
			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn pawn = pawns[i];
				if (pawn == null || pawn.Dead || pawn.Downed || !pawn.IsColonist || !pawn.DevelopmentalStage.Adult() || pawn.Ideo != primary)
				{
					continue;
				}
				if (OutpostDefenseUtility.AvailableSkillLevel(pawn, SkillDefOf.Social) < Props.minSocial)
				{
					continue;
				}

				float power = pawn.GetStatValue(StatDefOf.ConversionPower);
				if (best == null || power > bestPower)
				{
					best = pawn;
					bestPower = power;
				}
			}
			return best;
		}

		private static bool IsEligibleTarget(Pawn pawn, Ideo primary)
		{
			return pawn != null
				&& primary != null
				&& !pawn.Dead
				&& pawn.RaceProps.Humanlike
				&& !pawn.DevelopmentalStage.Baby()
				&& pawn.ideo != null
				&& pawn.Ideo != primary;
		}
	}
}
