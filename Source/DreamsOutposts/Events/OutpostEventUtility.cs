using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace DreamsOutposts
{
	public static class OutpostEventUtility
	{
		public static float GetCategoryWeight(Outpost outpost, OutpostEventCategoryDef category)
		{
			if (category == null)
			{
				return 0f;
			}
			float weight = category.baseWeight;
			if (outpost == null)
			{
				return weight;
			}
			foreach (OutpostFacility facility in outpost.Facilities)
			{
				List<OutpostEventCategoryModifier> modifiers = facility?.def?.eventCategoryModifiers;
				if (modifiers == null)
				{
					continue;
				}
				for (int i = 0; i < modifiers.Count; i++)
				{
					OutpostEventCategoryModifier modifier = modifiers[i];
					if (modifier != null && modifier.category == category)
					{
						weight += modifier.offset;
					}
				}
			}
			return weight;
		}

		public static Command AddTestEventCommand(Outpost outpost)
		{
			Command_Action command = new Command_Action
			{
				defaultLabel = "DEV: Add test event",
				defaultDesc = "Create a test event instance on this outpost.",
				icon = TexCommand.DesirePower
			};
			command.action = delegate
			{
				AddTestEvent(outpost);
			};
			return command;
		}

		public static void AddTestEvent(Outpost outpost)
		{
			if (outpost == null)
			{
				return;
			}
			OutpostEventDef def = DefDatabase<OutpostEventDef>.GetNamedSilentFail("DO_TestEvent");
			if (def == null)
			{
				Log.Error("Could not create test outpost event: DO_TestEvent was not found.");
				return;
			}
			outpost.AddEvent(def);
		}

		public static void TickEvents(Outpost outpost)
		{
			if (outpost?.events == null)
			{
				return;
			}
			int now = Find.TickManager.TicksGame;
			for (int i = outpost.events.Count - 1; i >= 0; i--)
			{
				OutpostEventInstance instance = outpost.events[i];
				if (instance != null && now >= instance.expireTick)
				{
					ResolveByTimeout(outpost, instance);
				}
			}
		}

		public static void ResolveByPlayer(Outpost outpost, OutpostEventInstance instance, OutpostEventOption option)
		{
			if (outpost == null || instance == null || option == null || !option.playerSelectable)
			{
				return;
			}
			OutpostEventContext context = new OutpostEventContext
			{
				outpost = outpost,
				instance = instance
			};
			if (!CheckRequirements(option, context, out var _))
			{
				return;
			}
			ApplyEffectsAndRemove(outpost, instance, option, context);
		}

		public static void ResolveByTimeout(Outpost outpost, OutpostEventInstance instance)
		{
			if (outpost == null || instance?.def == null)
			{
				return;
			}
			OutpostEventOption option = instance.def.options?.FirstOrDefault((OutpostEventOption candidate) => candidate != null && candidate.id == instance.def.defaultOptionId);
			if (option == null)
			{
				Log.Error("Outpost event " + instance.def.defName + " expired, but defaultOptionId '" + instance.def.defaultOptionId + "' did not match any option; keeping the event instance.");
				return;
			}
			ApplyEffectsAndRemove(outpost, instance, option, new OutpostEventContext
			{
				outpost = outpost,
				instance = instance
			}, sendExpiredLetter: true);
		}

		private static void ApplyEffectsAndRemove(Outpost outpost, OutpostEventInstance instance, OutpostEventOption option, OutpostEventContext context, bool sendExpiredLetter = false)
		{
			if (option.effects != null)
			{
				for (int i = 0; i < option.effects.Count; i++)
				{
					option.effects[i]?.Apply(context);
				}
			}
			int index = outpost.events?.IndexOf(instance) ?? -1;
			if (index >= 0)
			{
				outpost.events.RemoveAt(index);
				if (sendExpiredLetter)
				{
					SendExpiredLetter(outpost, instance, option);
				}
			}
		}

		public static void SendCreatedLetter(Outpost outpost, OutpostEventInstance instance)
		{
			if (outpost == null || instance?.def == null)
			{
				return;
			}
			Find.LetterStack.ReceiveLetter(instance.def.LabelCap, "DreamsOutposts.EventLetter".Translate(instance.def.description ?? string.Empty, RemainingTimeLabel(instance)), LetterDefOf.NeutralEvent, new LookTargets(outpost));
		}

		private static void SendExpiredLetter(Outpost outpost, OutpostEventInstance instance, OutpostEventOption option)
		{
			string result = EffectPreview(option);
			string optionLabel = option.label ?? option.id ?? "Unknown option";
			string text = instance.def.LabelCap + "\n\n" + (instance.def.description ?? string.Empty);
			text += "\n\nBecause the event was not handled in time, the outpost automatically chose:\n\n\"" + optionLabel + "\"";
			if (!string.IsNullOrEmpty(result))
			{
				text += "\n\nFinal result:\n" + result;
			}
			Find.LetterStack.ReceiveLetter(instance.def.LabelCap + " - timeout result", text, LetterDefOf.NeutralEvent, new LookTargets(outpost));
		}

		private static string EffectPreview(OutpostEventOption option)
		{
			if (option?.effects == null)
			{
				return null;
			}
			List<string> previews = new List<string>();
			for (int i = 0; i < option.effects.Count; i++)
			{
				if (option.effects[i] != null)
				{
					previews.Add(option.effects[i].GetPreview(null));
				}
			}
			return string.Join("\n", previews.ToArray());
		}

		public static void ResolveOption(Outpost outpost, OutpostEventInstance instance, OutpostEventOption option)
		{
			ResolveByPlayer(outpost, instance, option);
		}

		public static bool CheckRequirements(OutpostEventOption option, OutpostEventContext context, out string failureReason)
		{
			failureReason = null;
			if (option?.requirements == null)
			{
				return true;
			}
			for (int i = 0; i < option.requirements.Count; i++)
			{
				OutpostEventRequirement requirement = option.requirements[i];
				if (requirement == null)
				{
					continue;
				}
				AcceptanceReport report = requirement.Check(context);
				if (!report.Accepted)
				{
					failureReason = report.Reason;
					return false;
				}
			}
			return true;
		}

		public static string RemainingTimeLabel(OutpostEventInstance instance)
		{
			int remainingTicks = (instance?.expireTick ?? 0) - Find.TickManager.TicksGame;
			if (remainingTicks < 0)
			{
				remainingTicks = 0;
			}
			int days = remainingTicks / 60000;
			int hours = remainingTicks % 60000 / 2500;
			return days + " days / " + hours + " hours";
		}
	}
}
