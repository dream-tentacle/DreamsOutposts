using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public abstract class OutpostProcessWorker
	{
		private static readonly HashSet<Type> statefulWorkerWarned = new HashSet<Type>();

		public virtual bool UsesPersonnelCapacity(OutpostProcessProperties process)
		{
			return process?.capacityStat != null;
		}

		public virtual float CalculatePersonnelCapacity(Outpost outpost, OutpostProcessProperties process)
		{
			return CalculatePersonnelCapacity(outpost?.Pawns, outpost?.outpostTypeDef, process);
		}

		public virtual float CalculatePersonnelCapacity(
			IEnumerable<Pawn> pawns,
			OutpostTypeDef outpostTypeDef,
			OutpostProcessProperties process)
		{
			if (pawns == null) throw new ArgumentNullException("pawns");
			if (process == null) throw new ArgumentNullException("process");
			if (process.capacityStat == null) return 1f;

			float capacity = 0f;
			foreach (Pawn pawn in pawns)
			{
				if (pawn == null || pawn.Downed || (!pawn.IsPrisonerOfColony && pawn.Faction != Faction.OfPlayer))
					continue;
				if (OutpostStatUtility.CanSafelyReadStat(process.capacityStat, pawn) && process.PawnMeetsSkillRequirement(pawn))
				{
					float contributionFactor = pawn.IsPrisonerOfColony ? 0.4f : pawn.IsSlave ? 0.8f : 1f;
					capacity += pawn.GetStatValue(process.capacityStat) * contributionFactor;
				}
			}
			return capacity;
		}

		public virtual float CalculateOutput(
			float personnelCapacity,
			Outpost outpost,
			OutpostProcessProperties process,
			OutpostProcessState state)
		{
			return CalculateOutput(personnelCapacity, outpost?.outpostTypeDef, process, state);
		}

		public virtual float CalculateOutput(
			float personnelCapacity,
			OutpostTypeDef outpostTypeDef,
			OutpostProcessProperties process,
			OutpostProcessState state)
		{
			if (outpostTypeDef == null) throw new ArgumentNullException("outpostTypeDef");
			if (process == null) throw new ArgumentNullException("process");
			return personnelCapacity * process.outputPerCapacity;
		}

		public float CalculateProcess(Outpost outpost, OutpostProcessProperties process, OutpostProcessState state)
		{
			if (outpost == null) throw new ArgumentNullException("outpost");
			if (process == null) throw new ArgumentNullException("process");
			float capacity = CalculatePersonnelCapacity(outpost, process);
			float output = CalculateOutput(capacity, outpost, process, state);
			if (float.IsNaN(output) || float.IsInfinity(output) || output < 0f)
				throw new InvalidOperationException("Process " + process.id + " returned a non-finite or negative output.");
			return output;
		}

		public virtual int GetProcessIntervalTicks(OutpostProcessProperties process, OutpostProcessState state)
		{
			return process?.intervalTicks ?? 0;
		}

		public virtual Type StateClass => typeof(OutpostProcessState);

		public virtual OutpostProcessState CreateState(string processId, int nextProcessTick)
		{
			return (OutpostProcessState)Activator.CreateInstance(StateClass, processId, nextProcessTick);
		}

		public virtual OutpostProcessContext CreateContext(
			Outpost outpost,
			OutpostFacility facility,
			OutpostProcessProperties process,
			OutpostProcessState state,
			int now)
		{
			return new OutpostProcessContext(outpost, facility, process, state, now);
		}

		public virtual bool CanProcess(OutpostProcessContext context)
		{
			return true;
		}

		public virtual void ModifyProcess(OutpostProcessContext context)
		{
		}

		public abstract void Execute(OutpostProcessContext context);

		public virtual void AfterProcess(OutpostProcessContext context)
		{
		}

		public virtual void OnProcessFailed(OutpostProcessContext context, Exception ex)
		{
		}

		public virtual string GetDisplayLabel(OutpostProcessProperties process, OutpostProcessState state)
		{
			if (!string.IsNullOrEmpty(process?.outputLabelKey))
				return process.outputLabelKey.Translate().ToString();
			return process?.id ?? string.Empty;
		}

		public virtual ThingDef GetIconThing(OutpostProcessProperties process, OutpostProcessState state)
		{
			return null;
		}

		public virtual string DescribeCapacity(Outpost outpost, OutpostProcessProperties process, float capacity)
		{
			if (process?.capacityStat != null)
				return "DreamsOutposts.Ui.Rule.Capacity".Translate(
					process.capacityStat.LabelCap,
					process.capacityStat.ValueToString(capacity)).ToString();
			if (UsesPersonnelCapacity(process))
				return "DreamsOutposts.Ui.Rule.Efficiency".Translate(capacity.ToString("0.##")).ToString();
			return null;
		}

		public virtual string CapacityFactId(OutpostProcessProperties process)
		{
			if (process == null) return null;
			if (process.capacityStat == null)
				return "capacity.process." + process.id;
			return "capacity." + GetType().FullName + "." + process.capacityStat.defName + "." +
				(process.requiredSkill?.defName ?? "all") + "." + process.requiredSkillLevel;
		}

		public virtual string CapacityLabel(OutpostProcessProperties process)
		{
			return process?.capacityStat != null
				? process.capacityStat.LabelCap.ToString()
				: "DreamsOutposts.Ui.Fact.Efficiency".Translate().ToString();
		}

		public virtual string CapacityValue(OutpostProcessProperties process, float capacity)
		{
			return process?.capacityStat != null
				? process.capacityStat.ValueToString(capacity)
				: capacity.ToString("0.##");
		}

		public virtual string CapacityTooltip(Outpost outpost, OutpostProcessProperties process)
		{
			if (process?.capacityStat == null) return null;
			return "DreamsOutposts.Ui.Rule.CapacityTip".Translate(
				process.capacityStat.LabelCap,
				"DreamsOutposts.CoreFacility".Translate()).ToString();
		}

		public virtual string ProgressLeftText(
			Outpost outpost,
			OutpostFacility facility,
			OutpostProcessProperties process,
			OutpostProcessState state,
			int remaining)
		{
			return "DreamsOutposts.ProductionRemaining".Translate(
				GetDisplayLabel(process, state),
				remaining.ToStringTicksToPeriod()).ToString();
		}

		public virtual string ProgressRightText(
			Outpost outpost,
			OutpostFacility facility,
			OutpostProcessProperties process,
			OutpostProcessState state,
			int remaining)
		{
			return "DreamsOutposts.Ui.ProductionEvery".Translate(
				GetProcessIntervalTicks(process, state).ToStringTicksToPeriod()).ToString();
		}

		public virtual bool HasConfiguration(OutpostProcessProperties process)
		{
			return false;
		}

		public virtual void EnsureConfiguration(OutpostProcessProperties process, OutpostProcessState state)
		{
		}

		public virtual void OpenConfiguration(OutpostProcessProperties process, OutpostProcessState state, Action onChanged = null)
		{
		}

		public virtual string ConfigurationSummary(OutpostProcessProperties process, OutpostProcessState state)
		{
			return null;
		}

		public virtual string ConfigurationTip(OutpostProcessProperties process)
		{
			return string.Empty;
		}

		public virtual IEnumerable<string> ConfigErrors(OutpostProcessProperties process)
		{
			Type stateClass = StateClass;
			if (stateClass == null || !typeof(OutpostProcessState).IsAssignableFrom(stateClass))
				yield return "StateClass must derive from OutpostProcessState.";
			else if (stateClass.IsAbstract || stateClass.ContainsGenericParameters)
				yield return "StateClass must be a concrete OutpostProcessState subclass.";
			else if (stateClass.GetConstructor(new[] { typeof(string), typeof(int) }) == null)
				yield return "StateClass must have a public (string processId, int nextProcessTick) constructor.";
		}

		public static void WarnIfStateful(Type workerType)
		{
			if (workerType == null || !statefulWorkerWarned.Add(workerType)) return;
			if (workerType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Length != 0)
			{
				Log.Warning("[DreamsOutposts] Process worker " + workerType.FullName +
					" declares instance fields, but one worker instance is shared by every outpost using the same process rule. " +
					"Keep per-facility state in OutpostProcessState.");
			}
		}
	}
}
