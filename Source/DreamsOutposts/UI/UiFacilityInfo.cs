using System;
using System.Collections.Generic;
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

	public sealed class UiFacilityInfoItem
	{
		public string Id;
		public UiFacilityInfoKind Kind = UiFacilityInfoKind.Value;
		public UiFacilityInfoImportance Importance = UiFacilityInfoImportance.Supporting;
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
