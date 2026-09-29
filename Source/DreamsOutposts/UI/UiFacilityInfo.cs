using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Verse;

namespace DreamsOutposts
{
	public enum UiFacilityInfoKind
	{
		Value,
		Status,
		Requirement,
		Progress,
		Action,
		Compact
	}

	public enum UiFacilityInfoImportance
	{
		Primary,
		Supporting,
		Compact,
		Detail
	}

	/// <summary>Explicit modern-card placement, independent of legacy importance and detail rendering.</summary>
	public enum UiFacilityCardPlacement
	{
		Detail,
		Header,
		Body,
		Progress,
		Action,
		Chip
	}

	public static class UiFacilityCardPriority
	{
		public const int Alert = 0;
		public const int Core = 10;
		public const int Effect = 20;
		public const int Secondary = 30;
	}

	/// <summary>Fact identity is independent of display order. Source tokens live only for the UI object lifetime.</summary>
	public static class UiFacilityFactIds
	{
		public const string Defense = "defense.total";
		public const string Bombardment = "bombardment.available";
		public const string ShellBonus = "bombardment.shellBonus";
		public const string PowerStatus = "power.status";
		public const string OperationStatus = "operation.status";

		private sealed class SourceToken
		{
			public readonly long Value = Interlocked.Increment(ref nextToken);
		}

		private static long nextToken;
		private static readonly ConditionalWeakTable<object, SourceToken> sourceTokens = new ConditionalWeakTable<object, SourceToken>();

		/// <summary>Distinguishes identical facilities/modifiers without using list positions or localized labels.</summary>
		public static string ForSource(string prefix, object source)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			return prefix + "." + sourceTokens.GetValue(source, _ => new SourceToken()).Value;
		}
	}

	public sealed class UiFacilityInfoItem
	{
		public string Id;
		public UiFacilityInfoKind Kind = UiFacilityInfoKind.Value;
		public UiFacilityInfoImportance Importance = UiFacilityInfoImportance.Supporting;
		public UiFacilityCardPlacement CardPlacement = UiFacilityCardPlacement.Detail;
		public int CardPriority = UiFacilityCardPriority.Core;
		public string Label;
		public string Value;
		public string CompactText;
		public string Tooltip;
		public UiChipKind Tone = UiChipKind.Neutral;
		public float Progress;
		public string ProgressText;
		public string LeftText;
		public string RightText;
		public string ActionLabel;
		public string ActionTooltip;
		public Action Action;

		public string DisplayText
		{
			get
			{
				if (!string.IsNullOrEmpty(CompactText)) return CompactText;
				if (string.IsNullOrEmpty(Label)) return Value ?? string.Empty;
				if (string.IsNullOrEmpty(Value)) return Label;
				return Label + ": " + Value;
			}
		}
	}

	public sealed class UiFacilityInfoGroup
	{
		public string Title;
		public ThingDef IconThing;
		public string Tooltip;
		public Type SourceCompType;
		public readonly List<UiFacilityInfoItem> Items = new List<UiFacilityInfoItem>();
		public List<UiFacilityInfoItem> CardItems => UiFacilityInfoModel.OrderCardItems(Items);
		public IEnumerable<UiFacilityInfoItem> DetailItems => Items.Where(item => item != null &&
			item.CardPlacement == UiFacilityCardPlacement.Detail && !string.IsNullOrEmpty(item.DisplayText));
	}

	public sealed class UiFacilityInfoModel
	{
		public readonly List<UiFacilityInfoItem> Facts = new List<UiFacilityInfoItem>();
		public readonly List<UiFacilityInfoGroup> Groups = new List<UiFacilityInfoGroup>();

		public void Clear()
		{
			Facts.Clear();
			Groups.Clear();
		}

		/// <summary>Complete compact projection for classic cards and details; unaffected by modern-card visibility.</summary>
		public IEnumerable<UiFacilityInfoItem> CompactFacts
		{
			get
			{
				foreach (UiFacilityInfoItem fact in Facts)
					if (fact != null && fact.Importance == UiFacilityInfoImportance.Compact && !string.IsNullOrEmpty(fact.DisplayText))
						yield return fact;
			}
		}

		// No fixed item cap: alerts and core effects must never disappear because a card is busy.
		public static List<UiFacilityInfoItem> OrderCardItems(IEnumerable<UiFacilityInfoItem> items)
		{
			return items.Where(item => item != null && item.CardPlacement != UiFacilityCardPlacement.Detail)
				.OrderBy(item => item.CardPriority)
				.ThenBy(item => item.Id, StringComparer.Ordinal).ToList();
		}

		public IEnumerable<UiFacilityInfoItem> CardFacts => OrderCardItems(Facts)
			.Where(item => item.CardPlacement == UiFacilityCardPlacement.Chip && !string.IsNullOrEmpty(item.DisplayText));

		public void AddCompactFact(string id, string text, UiChipKind tone = UiChipKind.Neutral, string tooltip = null,
			int cardPriority = UiFacilityCardPriority.Effect)
		{
			AddFact(new UiFacilityInfoItem
			{
				Id = id,
				CardPlacement = UiFacilityCardPlacement.Chip,
				CardPriority = cardPriority,
				Kind = UiFacilityInfoKind.Compact,
				Importance = UiFacilityInfoImportance.Compact,
				Value = text,
				CompactText = text,
				Tooltip = tooltip,
				Tone = tone
			});
		}

		public void AddFact(UiFacilityInfoItem item)
		{
			if (item == null) return;
			if (!string.IsNullOrEmpty(item.Id))
			{
				for (int i = 0; i < Facts.Count; i++)
				{
					if (Facts[i]?.Id == item.Id)
					{
						Facts[i] = item;
						return;
					}
				}
			}
			Facts.Add(item);
		}
	}
}
