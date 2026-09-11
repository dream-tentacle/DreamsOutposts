using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public class Page_OutpostEvents : OutpostManagePage
	{
		private Vector2 scrollPosition;

		public override void DoContents(Rect inRect)
		{
			Rect viewRect = new Rect(0f, 0f, Mathf.Max(inRect.width - 16f, 0f), Mathf.Max(inRect.height, 60f));
			float contentHeight = 60f + (outpost.events?.Count ?? 0) * 60f;
			viewRect.height = Mathf.Max(viewRect.height, contentHeight);
			Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect, true);
			Widgets.Label(new Rect(0f, 0f, viewRect.width, 24f), "DreamsOutposts.Events".Translate());
			if (outpost.events == null || outpost.events.Count == 0)
			{
				Widgets.Label(new Rect(0f, 30f, viewRect.width, 30f), "DreamsOutposts.NoCurrentEvents".Translate());
				Widgets.EndScrollView();
				return;
			}
			float y = 30f;
			for (int i = 0; i < outpost.events.Count; i++)
			{
				OutpostEventInstance instance = outpost.events[i];
				if (instance == null)
				{
					continue;
				}
				Rect buttonRect = new Rect(0f, y, viewRect.width, 30f);
				if (Widgets.ButtonText(buttonRect, instance.def?.LabelCap ?? "DreamsOutposts.UnknownEvent".Translate()))
				{
					Find.WindowStack.Add(new Window_OutpostEventDialog(outpost, instance));
				}
				Widgets.Label(new Rect(0f, buttonRect.yMax, viewRect.width, 24f), "DreamsOutposts.Remaining".Translate(OutpostEventUtility.RemainingTimeLabel(instance)));
				y += 60f;
			}
			Widgets.EndScrollView();
		}
	}
}
