using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public class Page_OutpostInstall : OutpostManagePage
	{
		private const float HeaderHeight = 24f;

		private const float CardWidth = 200f;

		private const float CardGap = 10f;

		private const float NameHeight = 30f;

		private const float CostLineHeight = 20f;

		private const float CostIconSize = 16f;

		private const float StatusHeight = 20f;

		private const float BuildButtonHeight = 30f;

		private const float InnerGap = 4f;

		private const float ScrollBarWidth = 16f;

		public OutpostSlot slot;

		private Vector2 scroll;

		public override string Label => "Install facility";

		public override void DoContents(Rect inRect)
		{
			if (slot == null)
			{
				Widgets.Label(new Rect(0f, 0f, inRect.width, 24f), "This page was opened without a slot to install into.");
				return;
			}
			Widgets.Label(new Rect(0f, 0f, inRect.width, 24f), "Choose a facility to build here. Materials are paid from this outpost's store.");
			List<OutpostFacilityDef> candidates = CollectCandidates();
			Rect viewRect = new Rect(0f, 24f, inRect.width, Mathf.Max(inRect.height - 24f, 0f));
			if (candidates.Count == 0)
			{
				Widgets.Label(new Rect(viewRect.x, viewRect.y, viewRect.width, 30f), "No facility can be installed in this outpost.");
				return;
			}
			float cardHeight = CardHeight(MaxCostLines(candidates));
			int perRow = Mathf.Max(1, Mathf.FloorToInt((viewRect.width + 10f) / 210f));
			int rows = Mathf.Max(1, Mathf.CeilToInt((float)candidates.Count / (float)perRow));
			Widgets.BeginScrollView(viewRect: new Rect(0f, 0f, Mathf.Max(viewRect.width - 16f, 0f), (float)rows * (cardHeight + 10f)), outRect: viewRect, scrollPosition: ref scroll);
			for (int i = 0; i < candidates.Count; i++)
			{
				Rect cardRect = new Rect((float)(i % perRow) * 210f, (float)(i / perRow) * (cardHeight + 10f), 200f, cardHeight);
				DrawCard(cardRect, candidates[i]);
			}
			Widgets.EndScrollView();
		}

		private List<OutpostFacilityDef> CollectCandidates()
		{
			return OutpostUtility.InstallableFacilities(outpost?.outpostTypeDef);
		}

		private float CardHeight(int maxCostLines)
		{
			return 34f + (float)maxCostLines * 20f + 4f + 20f + 4f + 30f;
		}

		private static int MaxCostLines(List<OutpostFacilityDef> candidates)
		{
			int lines = 1;
			for (int i = 0; i < candidates.Count; i++)
			{
				int count = candidates[i].BuildCost.Count;
				if (count > lines)
				{
					lines = count;
				}
			}
			return lines;
		}

		private void DrawCard(Rect card, OutpostFacilityDef def)
		{
			string label = def.LabelCap;
			Rect nameRect = new Rect(card.x, card.y, card.width, 30f);
			if (Widgets.ButtonText(nameRect, label))
			{
				Find.WindowStack.Add(new Dialog_MessageBox(InstallInfoText(def), null, null, null, null, label));
			}
			TooltipHandler.TipRegion(nameRect, new TipSignal(InstallTooltip(def), nameRect.GetHashCode()));
			float y = nameRect.yMax + 4f;
			int costLines = Mathf.Max(def.BuildCost.Count, 1);
			DrawCostRows(new Rect(card.x, y, card.width, (float)costLines * 20f), def);
			y += (float)costLines * 20f + 4f;
			float bottom = card.yMax;
			Rect buildRect = new Rect(card.x, bottom - 30f, card.width, 30f);
			Rect statusRect = new Rect(card.x, y, card.width, Mathf.Max(buildRect.y - 4f - y, 0f));
			AcceptanceReport report = slot.CanInstall(def, outpost);
			if (!report.Accepted)
			{
				DrawStatusLine(statusRect, report.Reason, ColorLibrary.RedReadable);
				TooltipHandler.TipRegion(buildRect, new TipSignal(report.Reason, buildRect.GetHashCode()));
			}
			if (Widgets.ButtonText(buildRect, "Build", drawBackground: true, doMouseoverSound: true, report.Accepted) && slot.TryInstall(def, outpost, out var _))
			{
				CloseHostWindow();
			}
		}

		private void DrawCostRows(Rect rect, OutpostFacilityDef def)
		{
			List<ThingDefCountClass> cost = def.BuildCost;
			if (cost.NullOrEmpty())
			{
				DrawStatusLine(new Rect(rect.x, rect.y, rect.width, 20f), "No materials needed.", null);
				return;
			}
			for (int i = 0; i < cost.Count; i++)
			{
				ThingDefCountClass item = cost[i];
				if (item?.thingDef != null)
				{
					Rect rowRect = new Rect(rect.x, rect.y + (float)i * 20f, rect.width, 20f);
					Widgets.ThingIcon(new Rect(rowRect.x, rowRect.y + 2f, 16f, 16f), item.thingDef);
					int have = OutpostStockUtility.CountInStock(outpost, item.thingDef);
					string text = item.thingDef.label + " " + item.count + " / " + have;
					DrawStatusLine(new Rect(rowRect.x + 16f + 4f, rowRect.y, Mathf.Max(rowRect.width - 16f - 4f, 0f), rowRect.height), text, (have >= item.count) ? ((Color?)null) : new Color?(ColorLibrary.RedReadable));
				}
			}
		}

		private static void DrawStatusLine(Rect rect, string text, Color? color)
		{
			if (!string.IsNullOrEmpty(text))
			{
				Color previousColor = GUI.color;
				bool previousWrap = Text.WordWrap;
				TextAnchor previousAnchor = Text.Anchor;
				if (color.HasValue)
				{
					GUI.color = color.Value;
				}
				Text.WordWrap = false;
				Text.Anchor = TextAnchor.MiddleLeft;
				Widgets.Label(rect, text);
				Text.Anchor = previousAnchor;
				Text.WordWrap = previousWrap;
				GUI.color = previousColor;
			}
		}

		private static string InstallTooltip(OutpostFacilityDef def)
		{
			StringBuilder stringBuilder = new StringBuilder(def.LabelCap);
			if (!string.IsNullOrEmpty(def.description))
			{
				stringBuilder.AppendLine();
				stringBuilder.AppendLine();
				stringBuilder.Append(def.description);
			}
			stringBuilder.AppendLine();
			stringBuilder.AppendLine();
			stringBuilder.Append("Cost: " + OutpostBuildUtility.CostLabel(def));
			return stringBuilder.ToString();
		}

		private static string InstallInfoText(OutpostFacilityDef def)
		{
			StringBuilder stringBuilder = new StringBuilder();
			if (!string.IsNullOrEmpty(def.description))
			{
				stringBuilder.AppendLine(def.description);
				stringBuilder.AppendLine();
			}
			stringBuilder.AppendLine("Build cost: " + OutpostBuildUtility.CostLabel(def));
			if (def.maxPerOutpost > 0)
			{
				stringBuilder.AppendLine("Limit: " + def.maxPerOutpost + " per outpost.");
			}
			if (!def.IsResearchUnlocked)
			{
				stringBuilder.AppendLine("Requires research: " + (def.FirstMissingResearch?.LabelCap ?? ((TaggedString)"unknown")));
			}
			if (!def.productions.NullOrEmpty())
			{
				stringBuilder.AppendLine();
				stringBuilder.AppendLine("Production:");
				for (int i = 0; i < def.productions.Count; i++)
				{
					OutpostProductionProperties production = def.productions[i];
					if (production != null)
					{
						StringBuilder line = new StringBuilder("- ");
						if (production.product != null)
						{
							line.Append(production.product.LabelCap);
						}
						else if (production.Worker.UsesDynamicProduct)
						{
							line.Append("product chosen after installation");
						}
						else
						{
							line.Append(production.id);
						}
						line.Append(", every " + production.intervalTicks.ToStringTicksToPeriod());
						if (production.capacityStat != null)
						{
							line.Append(": " + production.outputPerCapacity.ToString("0.##") + " per " + production.capacityStat.LabelCap);
						}
						if (production.HasSkillRequirement)
						{
							line.Append(string.Concat(" (" + production.requiredSkill.LabelCap + " >= ", production.requiredSkillLevel.ToString(), " only)"));
						}
						if (production.HasInputs)
						{
							line.Append(", consumes " + OutpostBuildUtility.CostLabel(production.inputs) + " per unit");
						}
						stringBuilder.AppendLine(line.ToString());
					}
				}
			}
			return stringBuilder.ToString().TrimEndNewlines();
		}
	}
}
