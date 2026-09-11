using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public class Window_OutpostManage : Window
	{
		private class ManageTabRecord : TabRecord
		{
			private readonly string tip;

			public ManageTabRecord(string label, Action clickedAction, Func<bool> selected, string tip)
				: base(label, clickedAction, selected)
			{
				this.tip = tip;
			}

			public override string GetTip()
			{
				return tip;
			}
		}

		private const float TitleHeight = 34f;

		private const float TabContentGap = 12f;

		private readonly Outpost outpost;

		private readonly List<OutpostManagePage> pages;

		private readonly List<TabRecord> tabs = new List<TabRecord>();

		private int selectedIndex;

		public override Vector2 InitialSize => new Vector2(940f, 620f);

		public Window_OutpostManage(Outpost outpost)
		{
			this.outpost = outpost;
			pages = OutpostManagePageRegistry.BuildPages(outpost);
			for (int i = 0; i < pages.Count; i++)
			{
				pages[i].hostWindow = this;
			}
			BuildTabs();
			doCloseX = true;
			closeOnClickedOutside = true;
			absorbInputAroundWindow = true;
			if (pages.Count > 0)
			{
				pages[0].OnOpen();
			}
		}

		public override void DoWindowContents(Rect inRect)
		{
			if (outpost == null || outpost.Destroyed)
			{
				Close();
				return;
			}
			Text.Font = GameFont.Medium;
			Widgets.Label(new Rect(0f, 0f, inRect.width, 34f), outpost.LabelCap);
			Text.Font = GameFont.Small;
			if (pages.Count == 0)
			{
				Widgets.Label(new Rect(0f, 34f, inRect.width, 24f), "No management page is available for this outpost.");
				return;
			}
			Rect body = new Rect(0f, 66f, inRect.width, Mathf.Max(inRect.height - 34f - 32f, 0f));
			TabDrawer.DrawTabs(body, tabs);
			float contentY = body.y + 1f + 12f;
			Rect contentRect = new Rect(body.x, contentY, body.width, Mathf.Max(inRect.height - contentY, 0f));
			pages[Mathf.Clamp(selectedIndex, 0, pages.Count - 1)].DoContents(contentRect);
		}

		public override void PostClose()
		{
			base.PostClose();
			if (selectedIndex >= 0 && selectedIndex < pages.Count)
			{
				pages[selectedIndex].OnClose();
			}
		}

		private void BuildTabs()
		{
			tabs.Clear();
			for (int i = 0; i < pages.Count; i++)
			{
				int index = i;
				tabs.Add(new ManageTabRecord(pages[i].Label, delegate
				{
					SelectPage(index);
				}, () => selectedIndex == index, pages[i].Tooltip));
			}
		}

		private void SelectPage(int index)
		{
			if (index != selectedIndex && index >= 0 && index < pages.Count)
			{
				pages[selectedIndex].OnClose();
				selectedIndex = index;
				pages[selectedIndex].OnOpen();
			}
		}
	}
}
