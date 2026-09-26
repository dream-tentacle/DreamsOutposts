using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public sealed class OutpostProcessModifierSource
	{
		public OutpostProductionModifier Modifier;
		public OutpostFacilityDef SourceFacility;
		public bool IsLevelModifier;
	}

	public static class OutpostProcessUtility
	{
		public const int MaxCatchUpCyclesPerCheck = 100;

		public static void TickFacility(Outpost outpost, OutpostFacility facility, OutpostFacilityComp_Process comp)
		{
			if (outpost == null || outpost.Destroyed || facility?.def == null || comp == null) return;
			int now = Find.TickManager.TicksGame;
			foreach (OutpostProcessProperties process in comp.Processes)
			{
				if (process == null) continue;
				try
				{
					TickProcess(outpost, facility, comp, process, now);
				}
				catch (Exception ex)
				{
					ReportFailure(outpost, facility, process, ex.ToString());
				}
			}
		}

		public static bool TryGetCycleProgress(
			OutpostFacility facility,
			OutpostProcessProperties process,
			out float progress,
			out int ticksRemaining)
		{
			progress = 0f;
			ticksRemaining = 0;
			if (facility == null || process == null || string.IsNullOrEmpty(process.id)) return false;
			OutpostProcessState state = facility.GetProcessState(process.id);
			if (state == null) return false;
			int intervalTicks = process.Worker.GetProcessIntervalTicks(process, state);
			if (intervalTicks <= 0) return false;
			ticksRemaining = Mathf.Max(state.nextProcessTick - Find.TickManager.TicksGame, 0);
			progress = Mathf.Clamp01(1f - (float)ticksRemaining / intervalTicks);
			return true;
		}

		public static bool TryCalculatePersonnelCapacity(
			Outpost outpost,
			OutpostProcessProperties process,
			out float capacity)
		{
			capacity = 0f;
			if (outpost == null || process == null) return false;
			try
			{
				capacity = process.Worker.CalculatePersonnelCapacity(outpost, process);
				return true;
			}
			catch
			{
				return false;
			}
		}

		public static bool TryCalculateExpectedOutput(
			Outpost outpost,
			OutpostFacility facility,
			OutpostProcessProperties process,
			out float expectedOutput)
		{
			expectedOutput = 0f;
			if (outpost == null || process == null) return false;
			try
			{
				OutpostProcessState state = facility?.GetProcessState(process.id);
				expectedOutput = ApplyModifiers(
					outpost,
					facility,
					process,
					process.Worker.CalculateProcess(outpost, process, state));
				return true;
			}
			catch (Exception ex)
			{
				Log.ErrorOnce(
					"Outpost process forecast failed: outpost=" + outpost.Label +
					", process=" + RuleLabel(facility, process) + "\n" + ex,
					FailureKey(facility, process));
				return false;
			}
		}

		public static IEnumerable<OutpostProcessModifierSource> MatchingModifiers(
			Outpost outpost,
			OutpostFacility facility,
			OutpostProcessProperties process)
		{
			if (outpost == null || process == null) yield break;

			foreach (OutpostFacility sourceFacility in outpost.OperationalFacilities)
			{
				List<OutpostProductionModifier> modifiers = sourceFacility?.def?.productionModifiers;
				for (int i = 0; i < (modifiers?.Count ?? 0); i++)
				{
					OutpostProductionModifier modifier = modifiers[i];
					if (modifier != null && modifier.Matches(process, facility?.def))
					{
						yield return new OutpostProcessModifierSource
						{
							Modifier = modifier,
							SourceFacility = sourceFacility.def
						};
					}
				}
			}

			List<OutpostProductionModifier> levelModifiers = outpost.CurrentLevelProperties?.productionModifiers;
			OutpostFacilityComp_ProductionSupervisor supervisor = OutpostFacilityComp_ProductionSupervisor.GateFor(outpost);
			for (int i = 0; i < (levelModifiers?.Count ?? 0); i++)
			{
				OutpostProductionModifier modifier = levelModifiers[i];
				if (modifier == null || !modifier.Matches(process, facility?.def)) continue;
				if (supervisor != null && supervisor.Gates(modifier) && !supervisor.AllowsLevelFactor) continue;
				yield return new OutpostProcessModifierSource
				{
					Modifier = modifier,
					IsLevelModifier = true
				};
			}
		}

		public static void GetModifierTotals(
			Outpost outpost,
			OutpostFacility facility,
			OutpostProcessProperties process,
			out float offsetSum,
			out float factorProduct)
		{
			offsetSum = 0f;
			factorProduct = 1f;
			foreach (OutpostProcessModifierSource source in MatchingModifiers(outpost, facility, process))
			{
				offsetSum += source.Modifier.offset;
				factorProduct *= source.Modifier.factor;
			}
			factorProduct *= OutpostTemporaryEffectUtility.ProcessFactor(outpost, facility, process);
		}

		public static float ApplyModifiers(
			Outpost outpost,
			OutpostFacility facility,
			OutpostProcessProperties process,
			float baseOutput)
		{
			GetModifierTotals(outpost, facility, process, out float offsetSum, out float factorProduct);
			float globalMultiplier = DreamsOutpostsMod.Settings?.productionMultiplier
				?? DreamsOutpostsSettings.DefaultProductionMultiplier;
			return Mathf.Max((baseOutput + offsetSum) * factorProduct * globalMultiplier, 0f);
		}

		private static void TickProcess(
			Outpost outpost,
			OutpostFacility facility,
			OutpostFacilityComp_Process comp,
			OutpostProcessProperties process,
			int now)
		{
			OutpostProcessState state = comp.GetState(process.id);
			if (state == null)
			{
				Log.ErrorOnce(
					"Outpost process skipped: " + RuleLabel(facility, process) +
					" has no process state. Synchronize process states and check the def.",
					FailureKey(facility, process));
				return;
			}

			int intervalTicks = process.Worker.GetProcessIntervalTicks(process, state);
			if (intervalTicks <= 0)
			{
				Log.ErrorOnce(
					"Outpost process skipped: " + RuleLabel(facility, process) +
					" has invalid interval " + intervalTicks + ".",
					FailureKey(facility, process));
				return;
			}

			int cycles = 0;
			while (now >= state.nextProcessTick)
			{
				OutpostProcessContext context = process.Worker.CreateContext(
					outpost, facility, process, state, now);
				try
				{
					RunProcessPipeline(context);
				}
				catch (Exception ex)
				{
					context.Outcome = OutpostProcessOutcome.Failed;
					context.FailureReason = ex.Message;
					try
					{
						context.Worker.OnProcessFailed(context, ex);
					}
					catch (Exception hookEx)
					{
						Log.Error("[DreamsOutposts] Process failure hook threw for " +
							context.RuleLabel + ": " + hookEx);
					}
					ReportFailure(outpost, facility, process, ex.ToString());
					state.nextProcessTick += context.EffectiveInterval;
					break;
				}

				state.nextProcessTick += context.EffectiveInterval;
				if (++cycles >= MaxCatchUpCyclesPerCheck) break;
			}
		}

		private static void RunProcessPipeline(OutpostProcessContext context)
		{
			try
			{
				if (!context.Worker.CanProcess(context))
				{
					context.Outcome = OutpostProcessOutcome.Idle;
					context.FailureReason = "declined by CanProcess";
					return;
				}

				context.BaseOutput = context.Worker.CalculateProcess(
					context.Outpost,
					context.Process,
					context.State);
				context.ModifiedOutput = ApplyModifiers(
					context.Outpost,
					context.Facility,
					context.Process,
					context.BaseOutput);
				context.Worker.ModifyProcess(context);
				context.Worker.Execute(context);

				if (context.Outcome == OutpostProcessOutcome.Completed)
				{
					OutpostTemporaryEffectUtility.ConsumeProcessEffects(
						context.Outpost,
						context.Facility,
						context.Process);
				}
			}
			finally
			{
				try
				{
					context.Worker.AfterProcess(context);
				}
				catch (Exception ex)
				{
					Log.Error("[DreamsOutposts] Process AfterProcess hook threw for " +
						context.RuleLabel + ": " + ex);
				}
			}
		}

		public static string RuleLabel(OutpostFacility facility, OutpostProcessProperties process)
		{
			return (facility?.def?.defName ?? "null") + "." + (process?.id ?? "null");
		}

		public static int FailureKey(OutpostFacility facility, OutpostProcessProperties process)
		{
			return GenText.StableStringHash("DreamsOutposts.ProcessFailure." + RuleLabel(facility, process));
		}

		private static void ReportFailure(
			Outpost outpost,
			OutpostFacility facility,
			OutpostProcessProperties process,
			string detail)
		{
			Log.ErrorOnce(
				"Outpost process failed: outpost=" + (outpost?.Label ?? "null") +
				", process=" + RuleLabel(facility, process) + "\n" + detail,
				FailureKey(facility, process));
		}
	}
}
