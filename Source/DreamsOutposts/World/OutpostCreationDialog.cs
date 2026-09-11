using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public class OutpostCreationDialog : Window
	{
		private const float TitleHeight = 35f;

		private const float RowHeight = 34f;

		private const float ReasonHeight = 20f;

		private const float RowGap = 10f;

		private const float CreateButtonWidth = 120f;

		private const float DetailsButtonWidth = 100f;

		private const float ButtonGap = 6f;

		private const float NameGap = 10f;

		private const float ScrollBarWidth = 16f;

		private const float BottomButtonHeight = 35f;

		private readonly Caravan caravan;

		private readonly List<OutpostTypeDef> defs;

		private Vector2 scroll;

		public override Vector2 InitialSize => new Vector2(600f, 600f);

		public OutpostCreationDialog(Caravan caravan)
		{
			this.caravan = caravan;
			defs = DefDatabase<OutpostTypeDef>.AllDefsListForReading.OrderBy((OutpostTypeDef d) => d.label).ToList();
			forcePause = true;
			absorbInputAroundWindow = true;
		}

		public override void DoWindowContents(Rect inRect)
		{
			Widgets.Label(new Rect(0f, 0f, inRect.width, 35f), "DreamsOutposts.CreateOutpost".Translate());
			Rect view = new Rect(0f, 40f, inRect.width - 20f, Mathf.Max(inRect.height - 80f, 0f));
			float contentWidth = Mathf.Max(view.width - 16f, 0f);
			float rowTotalHeight = 64f;
			Rect content = new Rect(0f, 0f, contentWidth, (float)defs.Count * rowTotalHeight);
			OutpostTypeDef selectedDef = null;
			Widgets.BeginScrollView(view, ref scroll, content);
			for (int i = 0; i < defs.Count; i++)
			{
				OutpostTypeDef def = defs[i];
				if (def != null)
				{
					Rect rowRect = new Rect(0f, (float)i * rowTotalHeight, contentWidth, 34f);
					if (DrawRow(rowRect, def))
					{
						selectedDef = def;
					}
				}
			}
			Widgets.EndScrollView();
			if (selectedDef != null)
			{
				Close();
				OutpostUtility.Create(caravan, selectedDef);
			}
			else if (Widgets.ButtonText(new Rect(inRect.width - 120f, inRect.height - 35f, 120f, 35f), "Cancel".Translate()))
			{
				Close();
			}
		}

		private bool DrawRow(Rect rowRect, OutpostTypeDef def)
		{
			AcceptanceReport report = OutpostUtility.CanCreate(caravan, def);
			Rect createRect = new Rect(rowRect.xMax - 120f, rowRect.y, 120f, rowRect.height);
			Rect detailsRect = new Rect(createRect.x - 6f - 100f, rowRect.y, 100f, rowRect.height);
			Rect nameRect = new Rect(rowRect.x, rowRect.y, Mathf.Max(detailsRect.x - 10f - rowRect.x, 0f), rowRect.height);
			TextAnchor previousAnchor = Text.Anchor;
			Text.Anchor = TextAnchor.MiddleLeft;
			Widgets.Label(nameRect, def.LabelCap);
			Text.Anchor = previousAnchor;
			if (!string.IsNullOrEmpty(def.description))
			{
				TooltipHandler.TipRegion(nameRect, new TipSignal(def.description, nameRect.GetHashCode()));
			}
			if (Widgets.ButtonText(detailsRect, "Details".Translate()))
			{
				Find.WindowStack.Add(new Dialog_MessageBox(OutpostTypeInfoText(def), null, null, null, null, def.LabelCap));
			}
			if (report.Accepted)
			{
				return Widgets.ButtonText(createRect, "DreamsOutposts.CreateOutpost".Translate());
			}
			TooltipHandler.TipRegion(createRect, new TipSignal(report.Reason ?? "DreamsOutposts.CannotCreateHere".Translate(), createRect.GetHashCode()));
			Widgets.ButtonText(createRect, "DreamsOutposts.CreateOutpost".Translate(), drawBackground: true, doMouseoverSound: true, active: false);
			Rect reasonRect = new Rect(rowRect.x, rowRect.yMax, rowRect.width, 20f);
			Color previousColor = GUI.color;
			GUI.color = ColorLibrary.RedReadable;
			Widgets.Label(reasonRect, report.Reason ?? "DreamsOutposts.CannotCreateHere".Translate());
			GUI.color = previousColor;
			return false;
		}

		private static string OutpostTypeInfoText(OutpostTypeDef def)
		{
			StringBuilder stringBuilder = new StringBuilder();
			if (!string.IsNullOrEmpty(def.description))
			{
				stringBuilder.AppendLine(def.description);
				stringBuilder.AppendLine();
			}
			stringBuilder.Append("DreamsOutposts.CoreFacilityInfo".Translate(def.coreFacility?.LabelCap ?? "DreamsOutposts.None".Translate()));
			if (def.coreFacility != null && !string.IsNullOrEmpty(def.coreFacility.description))
			{
				stringBuilder.Append("\n" + def.coreFacility.description);
			}
			stringBuilder.AppendLine();
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("DreamsOutposts.Levels".Translate());
			if (def.levels.NullOrEmpty())
			{
				stringBuilder.AppendLine("- " + "DreamsOutposts.NoLevelTable".Translate());
			}
			else
			{
				for (int i = 0; i < def.levels.Count; i++)
				{
					OutpostLevelProperties level = def.levels[i];
					if (level != null)
					{
						int levelNumber = i + 1;
						string slotLabel = (level.slotCount == 1) ? "DreamsOutposts.Slot".Translate().ToString() : "DreamsOutposts.Slots".Translate().ToString();
						StringBuilder line = new StringBuilder("DreamsOutposts.LevelInfo".Translate(levelNumber, level.slotCount, slotLabel));
						if (levelNumber == 1)
						{
							line.Append(" — " + "DreamsOutposts.StartingLevel".Translate());
						}
						else
						{
							line.Append(" — " + "DreamsOutposts.DaysSinceFounding".Translate(level.daysRequired.ToString("0.#")));
							line.Append(level.cost.NullOrEmpty() ? ", " + "DreamsOutposts.Free".Translate().ToString() : (", " + OutpostBuildUtility.CostLabel(level.cost)));
						}
						stringBuilder.AppendLine(line.ToString());
					}
				}
			}
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("DreamsOutposts.InstallableFacilities".Translate());
			List<OutpostFacilityDef> facilities = OutpostUtility.InstallableFacilities(def);
			if (facilities.Count == 0)
			{
				stringBuilder.AppendLine("- " + "DreamsOutposts.None".Translate());
			}
			else
			{
				for (int j = 0; j < facilities.Count; j++)
				{
					stringBuilder.AppendLine("- " + facilities[j].LabelCap);
				}
			}
			return stringBuilder.ToString().TrimEndNewlines();
		}
	}
}
