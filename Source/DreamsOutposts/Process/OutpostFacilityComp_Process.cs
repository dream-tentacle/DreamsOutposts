using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public abstract class OutpostFacilityCompProperties_ProcessBase : OutpostFacilityCompProperties
	{
		public abstract IEnumerable<OutpostProcessProperties> Processes { get; }
		protected virtual string ProcessCollectionName => "processes";

		public override IEnumerable<string> ConfigErrors()
		{
			foreach (string error in base.ConfigErrors()) yield return error;
			HashSet<string> ids = new HashSet<string>();
			int index = 0;
			foreach (OutpostProcessProperties process in Processes)
			{
				if (process == null)
				{
					yield return ProcessCollectionName + "[" + index + "] is null.";
					index++;
					continue;
				}
				if (!ids.Add(process.id ?? string.Empty))
					yield return "Duplicate process id: " + process.id;
				foreach (string error in process.ConfigErrors())
					yield return "Process " + (process.id ?? "<null>") + ": " + error;
				index++;
			}
		}
	}

	public class OutpostFacilityCompProperties_Process : OutpostFacilityCompProperties_ProcessBase
	{
		public List<OutpostProcessProperties> processes = new List<OutpostProcessProperties>();

		public OutpostFacilityCompProperties_Process()
		{
			compClass = typeof(OutpostFacilityComp_Process);
		}

		public override IEnumerable<OutpostProcessProperties> Processes
		{
			get
			{
				for (int i = 0; i < (processes?.Count ?? 0); i++)
					yield return processes[i];
			}
		}

	}

	public class OutpostFacilityComp_Process : OutpostFacilityComp
	{
		public List<OutpostProcessState> states = new List<OutpostProcessState>();

		protected virtual OutpostFacilityCompProperties_ProcessBase ProcessProps =>
			(OutpostFacilityCompProperties_ProcessBase)props;

		public virtual IEnumerable<OutpostProcessProperties> Processes => ProcessProps.Processes;

		public override void Initialize(OutpostFacility parent, OutpostFacilityCompProperties props)
		{
			base.Initialize(parent, props);
			SynchronizeStates();
		}

		public void SynchronizeStates()
		{
			if (states == null) states = new List<OutpostProcessState>();

			Dictionary<string, OutpostProcessProperties> processById =
				new Dictionary<string, OutpostProcessProperties>();
			foreach (OutpostProcessProperties process in Processes)
			{
				if (process != null && !string.IsNullOrEmpty(process.id))
					processById[process.id] = process;
			}

			HashSet<string> seen = new HashSet<string>();
			for (int i = states.Count - 1; i >= 0; i--)
			{
				OutpostProcessState state = states[i];
				if (state == null ||
					string.IsNullOrEmpty(state.processId) ||
					!processById.TryGetValue(state.processId, out OutpostProcessProperties process) ||
					!seen.Add(state.processId) ||
					!process.Worker.StateClass.IsInstanceOfType(state))
				{
					states.RemoveAt(i);
				}
			}

			int now = Find.TickManager.TicksGame;
			foreach (OutpostProcessProperties process in Processes)
			{
				if (process == null || string.IsNullOrEmpty(process.id)) continue;
				OutpostProcessState state = GetState(process.id);
				if (state == null)
				{
					int interval = Mathf.Max(process.Worker.GetProcessIntervalTicks(process, null), 1);
					state = process.Worker.CreateState(process.id, now + interval);
					states.Add(state);
				}
				process.Worker.EnsureConfiguration(process, state);
			}
		}

		public OutpostProcessState GetState(string id)
		{
			for (int i = 0; i < (states?.Count ?? 0); i++)
				if (states[i]?.processId == id) return states[i];
			return null;
		}

		public override void Update(Outpost outpost, int delta)
		{
			OutpostProcessUtility.TickFacility(outpost, parent, this);
		}

		public override void UpdateDisabled(Outpost outpost, int delta)
		{
			if (delta <= 0) return;
			for (int i = 0; i < (states?.Count ?? 0); i++)
				if (states[i] != null) states[i].nextProcessTick += delta;
		}

		public override void BuildUiInfo(Outpost outpost, UiFacilityInfoModel output)
		{
			foreach (OutpostProcessProperties process in Processes)
			{
				if (process == null) continue;
				OutpostProcessState state = GetState(process.id);
				if (process.Worker.UsesPersonnelCapacity(process) &&
					OutpostProcessUtility.TryCalculatePersonnelCapacity(outpost, process, out float capacity))
				{
					string capacityText = process.Worker.DescribeCapacity(outpost, process, capacity);
					if (!string.IsNullOrEmpty(capacityText))
					{
						output.AddFact(new UiFacilityInfoItem
						{
							Id = process.Worker.CapacityFactId(process),
							Kind = UiFacilityInfoKind.Value,
							CardPlacement = UiFacilityCardPlacement.Chip,
							Importance = UiFacilityInfoImportance.Compact,
							Label = process.Worker.CapacityLabel(process),
							Value = process.Worker.CapacityValue(process, capacity),
							CompactText = capacityText,
							Tooltip = process.Worker.CapacityTooltip(outpost, process),
							Tone = UiChipKind.Info
						});
					}
				}

				string label = process.Worker.GetDisplayLabel(process, state);
				ThingDef icon = process.Worker.GetIconThing(process, state);
				float amount = 0f;
				OutpostProcessUtility.TryCalculateExpectedOutput(outpost, parent, process, out amount);
				bool hasProgress = OutpostProcessUtility.TryGetCycleProgress(
					parent, process, out float progress, out int remaining);
				float displayProgress = hasProgress ? Mathf.Clamp01(progress) : 0f;

				UiFacilityInfoGroup group = NewUiInfoGroup(label, icon);
				if (amount > 0f)
				{
					group.Items.Add(new UiFacilityInfoItem
					{
						Id = "process." + process.id + ".output",
						Kind = UiFacilityInfoKind.Value,
						CardPlacement = UiFacilityCardPlacement.Header,
						CardPriority = UiFacilityCardPriority.Core,
						Importance = UiFacilityInfoImportance.Primary,
						Value = "×" + amount.ToString("0.#")
					});
				}

				group.Items.Add(new UiFacilityInfoItem
				{
					Id = "process." + process.id + ".progress",
					Kind = UiFacilityInfoKind.Progress,
					CardPlacement = UiFacilityCardPlacement.Progress,
					CardPriority = UiFacilityCardPriority.Core,
					Importance = UiFacilityInfoImportance.Supporting,
					Label = label,
					Progress = displayProgress,
					ProgressText = hasProgress
						? "DreamsOutposts.Ui.ProgressCompact".Translate(
							Mathf.RoundToInt(displayProgress * 100f),
							remaining.ToStringTicksToPeriod()).ToString()
						: "0%",
					LeftText = process.Worker.ProgressLeftText(
						outpost, parent, process, state, remaining),
					RightText = process.Worker.ProgressRightText(
						outpost, parent, process, state, remaining),
					Tone = UiChipKind.Info
				});

				if (process.Worker.HasConfiguration(process))
				{
					group.Items.Add(new UiFacilityInfoItem
					{
						Id = "process." + process.id + ".configure",
						Kind = UiFacilityInfoKind.Action,
						CardPlacement = UiFacilityCardPlacement.Action,
						CardPriority = UiFacilityCardPriority.Secondary,
						Importance = UiFacilityInfoImportance.Supporting,
						ActionLabel = process.Worker.ConfigurationSummary(process, state),
						ActionTooltip = process.Worker.ConfigurationTip(process),
						Action = () => process.Worker.OpenConfiguration(process, state)
					});
				}

				output.Groups.Add(group);
			}
		}

		public override void ExposeData()
		{
			Scribe_Collections.Look(ref states, "states", LookMode.Deep);
		}
	}
}
