using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public class Page_OutpostInventory : OutpostManagePage
	{
		private const float ColumnHeaderHeight = 26f;

		private const float RowHeight = 30f;

		private const float ColumnGap = 14f;

		private const float IconSize = 26f;

		private const float InfoCardSize = 24f;

		private const float ScrollBarWidth = 16f;

		private const float ColonistColumnWidthFactor = 0.3f;

		private const float OtherPawnColumnWidthFactor = 0.3f;

		private readonly List<Thing> colonists = new List<Thing>();

		private readonly List<Thing> otherPawns = new List<Thing>();

		private readonly List<Thing> items = new List<Thing>();

		private Vector2 colonistsScroll;

		private Vector2 otherPawnsScroll;

		private Vector2 itemsScroll;

		public override void DoContents(Rect inRect)
		{
			RefreshContents();
			Rect columnsRect = new Rect(0f, 0f, inRect.width, inRect.height);
			float availableWidth = columnsRect.width - 28f;
			float colonistWidth = Mathf.Floor(availableWidth * 0.3f);
			float otherPawnWidth = Mathf.Floor(availableWidth * 0.3f);
			float itemWidth = availableWidth - colonistWidth - otherPawnWidth;
			Rect colonistRect = new Rect(columnsRect.x, columnsRect.y, colonistWidth, columnsRect.height);
			Rect otherPawnRect = new Rect(colonistRect.xMax + 14f, columnsRect.y, otherPawnWidth, columnsRect.height);
			Rect itemRect = new Rect(otherPawnRect.xMax + 14f, columnsRect.y, itemWidth, columnsRect.height);
			DrawColumn(colonistRect, "DreamsOutposts.Colonists".Translate(), colonists, ref colonistsScroll);
			DrawColumn(otherPawnRect, "DreamsOutposts.OtherPawns".Translate(), otherPawns, ref otherPawnsScroll);
			DrawColumn(itemRect, "DreamsOutposts.Items".Translate(), items, ref itemsScroll);
		}

		private void RefreshContents()
		{
			colonists.Clear();
			otherPawns.Clear();
			items.Clear();
			if (outpost.pawns != null)
			{
				List<Pawn> allPawns = outpost.pawns.InnerListForReading;
				for (int i = 0; i < allPawns.Count; i++)
				{
					Pawn pawn = allPawns[i];
					if (pawn != null)
					{
						(pawn.IsColonist ? colonists : otherPawns).Add(pawn);
					}
				}
			}
			if (outpost.inventory != null)
			{
				items.AddRange(outpost.inventory.InnerListForReading);
			}
		}

		private void DrawColumn(Rect outRect, string header, List<Thing> things, ref Vector2 scroll)
		{
			Widgets.Label(new Rect(outRect.x, outRect.y, outRect.width, 26f), header + " (" + things.Count + ")");
			Rect viewRect = new Rect(outRect.x, outRect.y + 26f, outRect.width, outRect.height - 26f);
			Rect contentRect = new Rect(0f, 0f, Mathf.Max(viewRect.width - 16f, 0f), (float)things.Count * 30f);
			Widgets.BeginScrollView(viewRect, ref scroll, contentRect);
			if (things.Count == 0)
			{
				Widgets.Label(new Rect(0f, 0f, contentRect.width, 30f), "DreamsOutposts.None".Translate());
			}
			else
			{
				for (int i = 0; i < things.Count; i++)
				{
					Rect rowRect = new Rect(0f, (float)i * 30f, contentRect.width, 30f);
					if (i % 2 == 1)
					{
						Widgets.DrawLightHighlight(rowRect);
					}
					DrawRow(rowRect, things[i]);
				}
			}
			Widgets.EndScrollView();
		}

		private static void DrawRow(Rect rowRect, Thing thing)
		{
			Widgets.ThingIcon(new Rect(rowRect.x + 2f, rowRect.y + (rowRect.height - 26f) * 0.5f, 26f, 26f), thing);
			float labelX = rowRect.x + 26f + 8f;
			float labelWidth = Mathf.Max(rowRect.xMax - 24f - 4f - labelX, 0f);
			TextAnchor previousAnchor = Text.Anchor;
			Text.Anchor = TextAnchor.MiddleLeft;
			Widgets.Label(new Rect(labelX, rowRect.y, labelWidth, rowRect.height), thing.LabelCap);
			Text.Anchor = previousAnchor;
			Widgets.InfoCardButton(rowRect.xMax - 24f, rowRect.y + (rowRect.height - 24f) * 0.5f, thing);
		}
	}
}
