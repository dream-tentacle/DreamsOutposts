using System;
using System.Collections.Generic;
using Verse;

namespace DreamsOutposts
{
	public sealed class UiFacilitySectionView
	{
		public string Title;
		public ThingDef IconThing;
		public string MainText;
		public string LeftText;
		public string RightText;
		public string Tooltip;
		public bool ShowProgress;
		public float Progress;
		public UiChipKind ProgressKind = UiChipKind.Info;
		public string ActionLabel;
		public string ActionTooltip;
		public Action Action;
		public readonly List<UiChipView> Chips = new List<UiChipView>();
	}
}
