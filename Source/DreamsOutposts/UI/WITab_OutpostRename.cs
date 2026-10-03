using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public class WITab_OutpostRename : WITab
	{
		private const string NameControl = "DreamsOutposts.OutpostRenameField";

		private Outpost editingOutpost;
		private string pendingName;
		private bool focusNameField;

		public WITab_OutpostRename()
		{
			labelKey = "DreamsOutposts.RenameOutpost.Tab";
			size = new Vector2(360f, 170f);
		}

		public override bool IsVisible => SelObject is Outpost outpost && !outpost.Destroyed;

		public override void OnOpen()
		{
			base.OnOpen();
			BeginEditing(SelObject as Outpost);
		}

		private void BeginEditing(Outpost outpost)
		{
			editingOutpost = outpost;
			pendingName = outpost?.Label ?? string.Empty;
			focusNameField = true;
		}

		protected override void FillTab()
		{
			Outpost outpost = SelObject as Outpost;
			if (outpost == null || outpost.Destroyed) return;
			// Inspector tabs are shared: switching selections must discard the previous draft.
			if (editingOutpost != outpost) BeginEditing(outpost);

			GameFont previousFont = Text.Font;
			TextAnchor previousAnchor = Text.Anchor;
			Text.Font = GameFont.Small;
			Text.Anchor = TextAnchor.UpperLeft;
			try
			{
				bool accept = Event.current.type == EventType.KeyDown
					&& (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
					&& GUI.GetNameOfFocusedControl() == NameControl;
				if (accept) Event.current.Use();

				Rect content = new Rect(0f, 0f, size.x, size.y).ContractedBy(18f);
				Widgets.Label(new Rect(content.x, content.y, content.width - 24f, 26f),
					"DreamsOutposts.RenameOutpost.Name".Translate());

				GUI.SetNextControlName(NameControl);
				pendingName = Widgets.TextField(new Rect(content.x, content.y + 34f, content.width, 35f),
					pendingName, Outpost.MaxNameLength);
				if (focusNameField && Event.current.type == EventType.Repaint)
				{
					GUI.FocusControl(NameControl);
					focusNameField = false;
				}

				if (Widgets.ButtonText(new Rect(content.xMax - 120f, content.yMax - 35f, 120f, 35f),
					"DreamsOutposts.RenameOutpost.Confirm".Translate()) || accept)
				{
					if (outpost.TryRename(pendingName))
					{
						CloseTab();
					}
					else
					{
						Messages.Message("DreamsOutposts.RenameOutpost.InvalidName".Translate(Outpost.MaxNameLength),
							MessageTypeDefOf.RejectInput, historical: false);
					}
				}
			}
			finally
			{
				Text.Font = previousFont;
				Text.Anchor = previousAnchor;
			}
		}

		public override void Notify_ClearingAllMapsMemory()
		{
			base.Notify_ClearingAllMapsMemory();
			editingOutpost = null;
			pendingName = null;
			focusNameField = false;
		}
	}
}
