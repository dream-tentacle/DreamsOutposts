using System;
using System.Collections.Generic;
using Verse;

namespace DreamsOutposts
{
	public class ChoiceLetter_OutpostEvent : StandardLetter
	{
		public override IEnumerable<DiaOption> Choices
		{
			get
			{
				foreach (DiaOption option in base.Choices)
				{
					yield return option;
				}

				Outpost outpost = lookTargets.TryGetPrimaryTarget().WorldObject as Outpost;
				DiaOption jumpToEvents = Option_JumpToLocation;
				jumpToEvents.SetText("DreamsOutposts.JumpToEventPage".Translate());
				Action jumpToLocation = jumpToEvents.action;
				jumpToEvents.action = delegate
				{
					if (outpost == null || outpost.Destroyed)
					{
						return;
					}
					jumpToLocation();
					Window_OutpostManage window = new Window_OutpostManage(outpost);
					Find.WindowStack.Add(window);
					window.OpenPageOfType(typeof(Page_OutpostEvents), null);
				};
				if (outpost == null || outpost.Destroyed)
				{
					jumpToEvents.Disable(null);
				}
				yield return jumpToEvents;
			}
		}
	}
}
