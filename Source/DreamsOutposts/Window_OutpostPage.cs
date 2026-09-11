using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public class Window_OutpostPage : Window
	{
		private readonly OutpostManagePage page;

		private readonly Window openedFrom;

		public override Vector2 InitialSize => new Vector2(760f, 560f);

		public Window_OutpostPage(OutpostManagePage page, Window openedFrom = null)
		{
			this.page = page;
			this.openedFrom = openedFrom;
			if (page != null)
			{
				page.hostWindow = this;
				optionalTitle = page.Label;
			}
			doCloseX = true;
			closeOnClickedOutside = true;
			absorbInputAroundWindow = true;
			page?.OnOpen();
		}

		public override void DoWindowContents(Rect inRect)
		{
			if (page == null)
			{
				Close();
			}
			else if (openedFrom != null && !Find.WindowStack.IsOpen(openedFrom))
			{
				Close();
			}
			else
			{
				page.DoContents(inRect);
			}
		}

		public override void PostClose()
		{
			base.PostClose();
			page?.OnClose();
		}
	}
}
