using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public enum OutpostEventWeightContributionKind
	{
		Add,
		Multiply
	}

	public sealed class OutpostEventWeightContribution
	{
		public string Label;

		public float Value;

		public OutpostEventWeightContributionKind Kind;
	}

	public static class OutpostEventUtility
	{
		public static float GetCategoryWeight(Outpost outpost, OutpostEventCategoryDef category, List<OutpostEventWeightContribution> details = null)
		{
			if (category == null)
			{
				return 0f;
			}
			float weight = category.baseWeight;
			AddDetail(details, "DreamsOutposts.EventWeight.Base".Translate(), category.baseWeight);
			if (outpost == null)
			{
				return Mathf.Max(weight, 0f);
			}
			weight += SituationWeight(outpost, category, details);
			float factor = 1f;
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
						factor *= Mathf.Max(modifier.factor, 0f);
						if (modifier.offset != 0f)
						{
							AddDetail(details, "DreamsOutposts.EventWeight.Facility".Translate(facility.def.LabelCap), modifier.offset);
						}
						if (modifier.factor != 1f)
						{
							MultiplyDetail(details, "DreamsOutposts.EventWeight.Facility".Translate(facility.def.LabelCap), modifier.factor);
						}
					}
				}
			}
			return Mathf.Max(weight * factor, 0f);
		}

		private static float SituationWeight(Outpost outpost, OutpostEventCategoryDef category, List<OutpostEventWeightContribution> details)
		{
			switch (category.defName)
			{
				case "DreamsOutposts_Frontier":
					return FrontierWeight(outpost, details);
				case "DreamsOutposts_Industrial":
					return IndustrialWeight(outpost, details);
				case "DreamsOutposts_Trade":
					return TradeWeight(outpost, details);
				case "DreamsOutposts_Population":
					return PopulationWeight(outpost, details);
				case "DreamsOutposts_Research":
					return ResearchWeight(outpost, details);
				default:
					return 0f;
			}
		}

		private static float FrontierWeight(Outpost outpost, List<OutpostEventWeightContribution> details)
		{
			int builtSlots = outpost.extensionSlots?.Count(slot => slot?.facility != null) ?? 0;
			float level = -3f * (outpost.level - 1);
			float facilities = -1.5f * builtSlots;
			float isolation = IsolationBonus(outpost, out float nearest);
			AddDetail(details, "DreamsOutposts.EventWeight.Level".Translate(outpost.level), level);
			AddDetail(details, "DreamsOutposts.EventWeight.BuiltFacilities".Translate(builtSlots), facilities);
			string distance = float.IsPositiveInfinity(nearest)
				? "DreamsOutposts.EventWeight.NoOtherOutpost".Translate().ToString()
				: "DreamsOutposts.EventWeight.DistanceTiles".Translate(nearest.ToString("0.#")).ToString();
			AddDetail(details, "DreamsOutposts.EventWeight.Isolation".Translate(distance), isolation);
			return level + facilities + isolation;
		}

		private static float IsolationBonus(Outpost outpost, out float nearest)
		{
			nearest = float.PositiveInfinity;
			List<WorldObject> worldObjects = Find.WorldObjects?.AllWorldObjects;
			if (worldObjects != null)
			{
				for (int i = 0; i < worldObjects.Count; i++)
				{
					WorldObject other = worldObjects[i];
					if (other == null || other == outpost || other.Faction != Faction.OfPlayer || !(other is Settlement || other is Outpost) || !other.Tile.Valid || other.Tile.Layer != outpost.Tile.Layer)
					{
						continue;
					}
					nearest = Mathf.Min(nearest, Find.WorldGrid.ApproxDistanceInTiles(outpost.Tile, other.Tile));
				}
			}
			if (nearest <= 2f) return 0f;
			if (nearest <= 5f) return 2f;
			if (nearest <= 10f) return 4f;
			if (nearest <= 20f) return 6f;
			return 8f;
		}

		private static float IndustrialWeight(Outpost outpost, List<OutpostEventWeightContribution> details)
		{
			float weight = 0f;
			foreach (OutpostFacility facility in outpost.Facilities)
			{
				OutpostFacilityDef def = facility?.def;
				if (def == null || IsResearchFacility(outpost, def)) continue;
				if (def == outpost.coreFacility?.def && def.IsProducer)
				{
					weight += 3f;
					AddDetail(details, def.LabelCap, 3f);
				}
				else if (def.FacilityTag == OutpostFacilityTagRegistry.Processing) { weight += 4f; AddDetail(details, def.LabelCap, 4f); }
				else if (def.FacilityTag == OutpostFacilityTagRegistry.AutomaticProduction) { weight += 3f; AddDetail(details, def.LabelCap, 3f); }
				else if (def.FacilityTag == OutpostFacilityTagRegistry.ProductionBoost) { weight += 2f; AddDetail(details, def.LabelCap, 2f); }
				else if (def.IsProducer) { weight += 3f; AddDetail(details, def.LabelCap, 3f); }
			}
			return weight;
		}

		private static float TradeWeight(Outpost outpost, List<OutpostEventWeightContribution> details)
		{
			float marketValue = 0f;
			List<Thing> items = outpost.InventoryItems;
			for (int i = 0; i < items.Count; i++)
			{
				Thing thing = items[i];
				if (thing != null && !thing.Destroyed) marketValue += thing.MarketValue * thing.stackCount;
			}
			float result = Mathf.Min(10f, 2f * Mathf.Log(1f + marketValue / 1000f, 2f));
			AddDetail(details, "DreamsOutposts.EventWeight.StockValue".Translate(marketValue.ToStringMoney()), result);
			return result;
		}

		private static float PopulationWeight(Outpost outpost, List<OutpostEventWeightContribution> details)
		{
			int colonists = outpost.Colonists.Count();
			float level = outpost.level - 1;
			float population = Mathf.Min(4f, Mathf.Sqrt(colonists));
			AddDetail(details, "DreamsOutposts.EventWeight.Level".Translate(outpost.level), level);
			AddDetail(details, "DreamsOutposts.EventWeight.Colonists".Translate(colonists), population);
			return level + population;
		}

		private static float ResearchWeight(Outpost outpost, List<OutpostEventWeightContribution> details)
		{
			OutpostTypeDef researchType = DefDatabase<OutpostTypeDef>.GetNamedSilentFail("DreamsOutposts_Research");
			float weight = outpost.outpostTypeDef == researchType ? 6f : 0f;
			if (weight > 0f) AddDetail(details, "DreamsOutposts.EventWeight.ResearchOutpost".Translate(), weight);
			foreach (OutpostFacility facility in outpost.Facilities)
			{
				OutpostFacilityDef def = facility?.def;
				if (!IsResearchFacility(outpost, def)) continue;
				weight += 3f;
				AddDetail(details, def.LabelCap, 3f);
			}
			int bestIntellectual = 0;
			foreach (Pawn pawn in outpost.Colonists)
			{
				SkillRecord skill = pawn.skills?.GetSkill(SkillDefOf.Intellectual);
				if (skill != null && !skill.TotallyDisabled) bestIntellectual = Mathf.Max(bestIntellectual, skill.GetLevel());
			}
			float intellectual = Mathf.Min(5f, bestIntellectual / 4f);
			AddDetail(details, "DreamsOutposts.EventWeight.Intellectual".Translate(bestIntellectual), intellectual);
			return weight + intellectual;
		}

		private static void AddDetail(List<OutpostEventWeightContribution> details, string label, float value)
		{
			if (details == null) return;
			details.Add(new OutpostEventWeightContribution { Label = label, Value = value, Kind = OutpostEventWeightContributionKind.Add });
		}

		private static void MultiplyDetail(List<OutpostEventWeightContribution> details, string label, float value)
		{
			if (details == null) return;
			details.Add(new OutpostEventWeightContribution { Label = label, Value = value, Kind = OutpostEventWeightContributionKind.Multiply });
		}

		private static bool IsResearchFacility(Outpost outpost, OutpostFacilityDef def)
		{
			if (def == null) return false;
			OutpostTypeDef researchType = DefDatabase<OutpostTypeDef>.GetNamedSilentFail("DreamsOutposts_Research");
			return def == researchType?.coreFacility || (def.allowedOutpostTypes != null && def.allowedOutpostTypes.Contains(researchType));
		}

		/// <summary>
		/// 当前对这个据点合法的 EventDef，按 Category 归组。这里是合法事件筛选的唯一来源：
		/// TryChooseRandomEvent 和 HasAnyValidEvent 都使用同一份结果，条件判断只写一遍。
		/// </summary>
		private static Dictionary<OutpostEventCategoryDef, List<OutpostEventDef>> BuildEligibleEventsByCategory(Outpost outpost)
		{
			Dictionary<OutpostEventCategoryDef, List<OutpostEventDef>> eventsByCategory = new Dictionary<OutpostEventCategoryDef, List<OutpostEventDef>>();
			if (outpost == null)
			{
				return eventsByCategory;
			}
			OutpostEventContext context = new OutpostEventContext { outpost = outpost };
			foreach (OutpostEventDef candidate in DefDatabase<OutpostEventDef>.AllDefs)
			{
				if (candidate == null || candidate.category == null || candidate.weight <= 0f || !HasValidOptionConfiguration(candidate) || !CheckRequirements(candidate.requirements, context, out var _))
				{
					continue;
				}
				if (!eventsByCategory.TryGetValue(candidate.category, out List<OutpostEventDef> events))
				{
					events = new List<OutpostEventDef>();
					eventsByCategory.Add(candidate.category, events);
				}
				events.Add(candidate);
			}
			return eventsByCategory;
		}

		/// <summary>
		/// 这个据点当前是否至少存在一个合法随机事件。只回答「有没有」，
		/// 判定口径与 TryChooseRandomEvent 的 Category 权重筛选保持一致。
		/// </summary>
		public static bool HasAnyValidEvent(Outpost outpost)
		{
			foreach (KeyValuePair<OutpostEventCategoryDef, List<OutpostEventDef>> pair in BuildEligibleEventsByCategory(outpost))
			{
				if (pair.Value.Count > 0 && GetCategoryWeight(outpost, pair.Key) > 0f)
				{
					return true;
				}
			}
			return false;
		}

		public static bool TryChooseRandomEvent(Outpost outpost, out OutpostEventDef eventDef)
		{
			eventDef = null;
			if (outpost == null)
			{
				return false;
			}
			Dictionary<OutpostEventCategoryDef, List<OutpostEventDef>> eventsByCategory = BuildEligibleEventsByCategory(outpost);
			List<OutpostEventCategoryDef> categories = new List<OutpostEventCategoryDef>();
			List<float> categoryWeights = new List<float>();
			foreach (KeyValuePair<OutpostEventCategoryDef, List<OutpostEventDef>> pair in eventsByCategory)
			{
				float categoryWeight = GetCategoryWeight(outpost, pair.Key);
				if (pair.Value.Count > 0 && categoryWeight > 0f)
				{
					categories.Add(pair.Key);
					categoryWeights.Add(Mathf.Pow(categoryWeight, 1.5f));
				}
			}
			int categoryIndex = WeightedIndex(categoryWeights);
			if (categoryIndex < 0)
			{
				return false;
			}
			List<OutpostEventDef> selectedEvents = eventsByCategory[categories[categoryIndex]];
			List<float> eventWeights = new List<float>();
			for (int i = 0; i < selectedEvents.Count; i++)
			{
				eventWeights.Add(selectedEvents[i].weight);
			}
			int eventIndex = WeightedIndex(eventWeights);
			if (eventIndex < 0)
			{
				return false;
			}
			eventDef = selectedEvents[eventIndex];
			return true;
		}

		private static bool HasValidOptionConfiguration(OutpostEventDef eventDef)
		{
			if (eventDef.options.NullOrEmpty() || string.IsNullOrEmpty(eventDef.defaultOptionId)) return false;
			for (int i = 0; i < eventDef.options.Count; i++)
			{
				if (eventDef.options[i] != null && eventDef.options[i].id == eventDef.defaultOptionId) return true;
			}
			return false;
		}

		private static int WeightedIndex(List<float> weights)
		{
			float total = 0f;
			for (int i = 0; i < weights.Count; i++) total += Mathf.Max(weights[i], 0f);
			if (total <= 0f) return -1;
			float roll = Rand.Range(0f, total);
			for (int j = 0; j < weights.Count; j++)
			{
				roll -= Mathf.Max(weights[j], 0f);
				if (roll < 0f) return j;
			}
			return weights.Count - 1;
		}

		public static Command AddTestEventCommand(Outpost outpost)
		{
			Command_Action command = new Command_Action
			{
				defaultLabel = "DEV: Add test event",
				defaultDesc = "Create the fixed test event instance on this outpost.",
				icon = TexCommand.DesirePower
			};
			command.action = delegate
			{
				AddTestEvent(outpost);
			};
			return command;
		}

		public static Command RollRandomEventCommand(Outpost outpost)
		{
			Command_Action command = new Command_Action
			{
				defaultLabel = "DEV: Roll random event",
				defaultDesc = "Choose and create a random valid outpost event.",
				icon = TexCommand.DesirePower
			};
			command.action = delegate
			{
				if (TryChooseRandomEvent(outpost, out OutpostEventDef eventDef)) outpost.AddEvent(eventDef);
				else Log.Message("No valid outpost event found.");
			};
			return command;
		}

		/// <summary>
		/// 开发者用的固定测试事件：延迟事件链的事件 A，用来验证「选项 → 3 天后事件 B」。
		/// </summary>
		public static void AddTestEvent(Outpost outpost)
		{
			if (outpost == null)
			{
				return;
			}
			OutpostEventDef def = DefDatabase<OutpostEventDef>.GetNamedSilentFail("DO_DelayedEventA");
			if (def == null)
			{
				Log.Error("Could not create test outpost event: DO_DelayedEventA was not found.");
				return;
			}
			outpost.AddEvent(def);
		}

		public static void TickEvents(Outpost outpost)
		{
			if (outpost == null)
			{
				return;
			}
			int now = Find.TickManager.TicksGame;
			TickScheduledEvents(outpost, now);
			if (outpost.events == null)
			{
				return;
			}
			for (int i = outpost.events.Count - 1; i >= 0; i--)
			{
				OutpostEventInstance instance = outpost.events[i];
				if (instance != null && now >= instance.expireTick)
				{
					ResolveByTimeout(outpost, instance);
				}
			}
		}

		/// <summary>
		/// 到期就直接创建指定 EventDef。这条路径不走全局随机事件调度器、不检查 CanReceiveRandomEvent()、
		/// 也不参与 Category/Event 权重抽取，但仍然通过 Outpost.AddEvent 这个统一创建入口，
		/// 因此 createdTick、expireTick 和新事件 Letter 照常产生。
		/// 无论如何都先把条目从 scheduledEvents 删掉，避免同一个排期重复触发。
		/// </summary>
		private static void TickScheduledEvents(Outpost outpost, int now)
		{
			List<OutpostScheduledEvent> scheduled = outpost.scheduledEvents;
			if (scheduled.NullOrEmpty())
			{
				return;
			}
			for (int i = scheduled.Count - 1; i >= 0; i--)
			{
				OutpostScheduledEvent entry = scheduled[i];
				if (entry == null)
				{
					scheduled.RemoveAt(i);
					continue;
				}
				if (now < entry.triggerTick)
				{
					continue;
				}
				scheduled.RemoveAt(i);
				if (entry.eventDef == null)
				{
					Log.Error("Outpost " + outpost.Label + " had a scheduled event with no EventDef; it was dropped without creating anything.");
					continue;
				}
				outpost.AddEvent(entry.eventDef);
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
			return CheckRequirements(option?.requirements, context, out failureReason);
		}

		public static bool CheckRequirements(List<OutpostEventRequirement> requirements, OutpostEventContext context, out string failureReason)
		{
			failureReason = null;
			if (requirements == null)
			{
				return true;
			}
			for (int i = 0; i < requirements.Count; i++)
			{
				OutpostEventRequirement requirement = requirements[i];
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
