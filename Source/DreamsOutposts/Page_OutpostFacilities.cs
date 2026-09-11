using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public class Page_OutpostFacilities : OutpostManagePage
	{
		private const float SectionHeaderHeight = 24f;

		private const float SectionGap = 16f;

		private const float UpgradeRowHeight = 30f;

		private const float LevelLabelWidth = 110f;

		private const float SlotWidth = 180f;

		private const float SlotButtonHeight = 30f;

		private const float SlotGap = 10f;

		private const float BarHeight = 10f;

		private const float BarTextHeight = 20f;

		private const float LineGap = 6f;

		private const float ScrollBarWidth = 16f;

		private const float ProductionLineHeight = 36f;

		private Vector2 extensionScroll;

		public override void DoContents(Rect inRect)
		{
			float cellHeight = 30f + (float)MaxProductionLines() * 36f;
			float y = DrawUpgradeRow(0f, inRect.width);
			y += 16f;
			y = DrawCoreFacilitySection(y, inRect.width, cellHeight);
			y += 16f;
			DrawExtensionSlotsSection(new Rect(0f, y, inRect.width, Mathf.Max(inRect.height - y, 0f)), cellHeight);
		}

		private float DrawUpgradeRow(float y, float width)
		{
			Rect rowRect = new Rect(0f, y, width, 30f);
			Widgets.Label(new Rect(rowRect.x, rowRect.y, 110f, rowRect.height), "DreamsOutposts.Level".Translate(outpost.level, outpost.MaxLevel));
			TooltipHandler.TipRegion(rowRect, new TipSignal(OutpostUpgradeUtility.UpgradeDescription(outpost), rowRect.GetHashCode()));
			if (outpost.IsMaxLevel)
			{
				Widgets.Label(new Rect(rowRect.x + 110f, rowRect.y, Mathf.Max(width - 110f, 0f), rowRect.height), "DreamsOutposts.MaxLevelReached".Translate(outpost.SlotCountForLevel));
				return rowRect.yMax;
			}
			string buttonText = "DreamsOutposts.UpgradeToLevel".Translate(outpost.level + 1);
			if (OutpostUpgradeUtility.CanUpgrade(outpost, out var reason))
			{
				if (Widgets.ButtonText(new Rect(rowRect.x + 110f, rowRect.y, 190f, rowRect.height), buttonText))
				{
					OutpostUpgradeUtility.TryUpgrade(outpost);
				}
				return rowRect.yMax;
			}
			Widgets.Label(new Rect(rowRect.x + 110f, rowRect.y, Mathf.Max(width - 110f, 0f), rowRect.height), buttonText + " — " + reason);
			return rowRect.yMax;
		}

		private int MaxProductionLines()
		{
			int lines = 1;
			foreach (OutpostFacility facility2 in outpost.Facilities)
			{
				int count = (facility2?.def?.productions?.Count).GetValueOrDefault();
				if (count > lines)
				{
					lines = count;
				}
			}
			return lines;
		}

		private float DrawCoreFacilitySection(float y, float width, float cellHeight)
		{
			Widgets.Label(new Rect(0f, y, width, 24f), "DreamsOutposts.CoreFacility".Translate());
			Rect cellRect = new Rect(0f, y + 24f, 180f, cellHeight);
			DrawFacilityCell(cellRect, outpost.coreFacility, null);
			return cellRect.yMax;
		}

		private void DrawExtensionSlotsSection(Rect rect, float cellHeight)
		{
			List<OutpostSlot> slots = outpost.extensionSlots;
			int count = slots?.Count ?? 0;
			int used = 0;
			for (int i = 0; i < count; i++)
			{
				if (slots[i] != null && !slots[i].IsEmpty)
				{
					used++;
				}
			}
			Widgets.Label(new Rect(rect.x, rect.y, rect.width, 24f), "DreamsOutposts.ExtensionFacilities".Translate(used, count));
			Rect viewRect = new Rect(rect.x, rect.y + 24f, rect.width, Mathf.Max(rect.height - 24f, 0f));
			int perRow = Mathf.Max(1, Mathf.FloorToInt((viewRect.width + 10f) / 190f));
			int rows = Mathf.Max(1, Mathf.CeilToInt((float)count / (float)perRow));
			Rect contentRect = new Rect(0f, 0f, Mathf.Max(viewRect.width - 16f, 0f), (float)rows * (cellHeight + 10f));
			Widgets.BeginScrollView(viewRect, ref extensionScroll, contentRect);
			if (count == 0)
			{
				Widgets.Label(new Rect(0f, 0f, contentRect.width, 30f), "DreamsOutposts.NoExtensionSlots".Translate());
			}
			else
			{
				for (int j = 0; j < count; j++)
				{
					Rect cellRect = new Rect((float)(j % perRow) * 190f, (float)(j / perRow) * (cellHeight + 10f), 180f, cellHeight);
					OutpostSlot slot = slots[j];
					DrawFacilityCell(cellRect, slot?.facility, slot);
				}
			}
			Widgets.EndScrollView();
		}

		private void DrawFacilityCell(Rect rect, OutpostFacility facility, OutpostSlot installTarget)
		{
			Rect buttonRect = new Rect(rect.x, rect.y, rect.width, 30f);
			if (facility != null)
			{
			string label = facility.def?.LabelCap ?? "DreamsOutposts.UnknownFacility".Translate();
				TooltipHandler.TipRegion(buttonRect, new TipSignal(FacilityTooltip(facility), buttonRect.GetHashCode()));
				if (Widgets.ButtonText(buttonRect, label))
				{
					OpenFacilityMenu(facility, installTarget);
				}
				DrawProductionProgress(new Rect(rect.x, buttonRect.yMax, rect.width, rect.height - 30f), facility);
			}
			else if (installTarget == null)
			{
				Widgets.Label(buttonRect, "DreamsOutposts.None".Translate());
			}
			else
			{
				TooltipHandler.TipRegion(buttonRect, new TipSignal("DreamsOutposts.EmptySlotTip".Translate(), buttonRect.GetHashCode()));
				if (Widgets.ButtonText(buttonRect, "DreamsOutposts.EmptySlot".Translate()))
				{
					OpenInstallPage(installTarget);
				}
			}
		}

		private void OpenInstallPage(OutpostSlot slot)
		{
			Page_OutpostInstall page = new Page_OutpostInstall
			{
				outpost = outpost,
				slot = slot
			};
			Find.WindowStack.Add(new Window_OutpostPage(page, hostWindow));
		}

		private void OpenFacilityMenu(OutpostFacility facility, OutpostSlot slot)
		{
			string label = facility?.def?.LabelCap ?? "DreamsOutposts.UnknownFacility".Translate();
			List<FloatMenuOption> options = new List<FloatMenuOption>
			{
				new FloatMenuOption("DreamsOutposts.Details".Translate(), delegate
				{
					Find.WindowStack.Add(new Dialog_MessageBox(FacilityInfoText(facility), null, null, null, null, label));
				})
			};
			if (slot != null)
			{
				AcceptanceReport report = slot.CanRemove(outpost);
				string removeLabel = "DreamsOutposts.Demolish".Translate();
				if (!report.Accepted)
				{
					removeLabel = removeLabel + " (" + report.Reason + ")";
				}
				FloatMenuOption removeOption = new FloatMenuOption(removeLabel, delegate
				{
					ConfirmRemove(facility, slot);
				}, MenuOptionPriority.Default, delegate(Rect optionRect)
				{
					TooltipHandler.TipRegion(optionRect, new TipSignal(RemoveTooltip(facility), optionRect.GetHashCode()));
				});
				removeOption.Disabled = !report.Accepted;
				options.Add(removeOption);
			}
			Find.WindowStack.Add(new FloatMenu(options));
		}

		private void ConfirmRemove(OutpostFacility facility, OutpostSlot slot)
		{
			string label = facility?.def?.LabelCap ?? "DreamsOutposts.UnknownFacility".Translate();
			string text = "DreamsOutposts.ConfirmDemolish".Translate(label, OutpostBuildUtility.RefundLabel(facility?.def));
			Find.WindowStack.Add(new Dialog_MessageBox(text, "Demolish", delegate
			{
				slot.TryRemove(outpost, out var _);
			}, "Cancel".Translate(), null, label, buttonADestructive: true));
		}

		private static string RemoveTooltip(OutpostFacility facility)
		{
			return "DreamsOutposts.RemoveFacility".Translate(facility?.def?.LabelCap ?? "DreamsOutposts.ThisFacility".Translate(), OutpostBuildUtility.RefundLabel(facility?.def));
		}

		private void DrawProductionProgress(Rect rect, OutpostFacility facility)
		{
			float y = rect.y;
			if (facility.def.productions.NullOrEmpty())
			{
				DrawProgressLine(new Rect(rect.x, y, rect.width, 36f), 0f, "DreamsOutposts.NoProduction".Translate());
				return;
			}
			for (int i = 0; i < facility.def.productions.Count; i++)
			{
				OutpostProductionProperties production = facility.def.productions[i];
				float progress = 0f;
				int ticksRemaining = 0;
				bool hasProgress = OutpostProductionUtility.TryGetCycleProgress(facility, production, out progress, out ticksRemaining);
				Rect lineRect = new Rect(rect.x, y, rect.width, 36f);
				Widgets.FillableBar(new Rect(lineRect.x, lineRect.y, lineRect.width, 10f), Mathf.Clamp01(progress));
				string label = ProductionLineText(facility, production, hasProgress, ticksRemaining);
				Rect textRect = new Rect(lineRect.x, lineRect.y + 10f, lineRect.width, 20f);
				if (production != null && production.Worker.HasConfiguration(production))
				{
					production.Worker.DrawConfiguration(textRect, label, production, facility.GetProductionState(production.id));
				}
				else
				{
					DrawLineLabel(lineRect, label);
				}
				y += 36f;
			}
		}

		private string ProductionLineText(OutpostFacility facility, OutpostProductionProperties production, bool hasProgress, int ticksRemaining)
		{
			if (production == null)
			{
				return "DreamsOutposts.InvalidProductionRule".Translate();
			}
			OutpostProductionUtility.TryGetProductionProduct(facility, production, out var product);
			string productLabel = product?.LabelCap ?? production.id;
			float expectedOutput;
			string text = (OutpostProductionUtility.TryCalculateExpectedOutput(outpost, facility, production, out expectedOutput) ? (productLabel + " x" + expectedOutput.ToString("0.#")) : productLabel);
			return hasProgress ? "DreamsOutposts.ProductionRemaining".Translate(text, ticksRemaining.ToStringTicksToPeriod()).ToString() : text;
		}

		private static void DrawProgressLine(Rect rect, float progress, string label)
		{
			Rect barRect = new Rect(rect.x, rect.y, rect.width, 10f);
			Widgets.FillableBar(barRect, Mathf.Clamp01(progress));
			DrawLineLabel(rect, label);
		}

		private static void DrawLineLabel(Rect lineRect, string label)
		{
			TextAnchor previousAnchor = Text.Anchor;
			Text.Anchor = TextAnchor.MiddleLeft;
			Widgets.Label(new Rect(lineRect.x, lineRect.y + 10f, lineRect.width, 20f), label);
			Text.Anchor = previousAnchor;
		}

		private static string FacilityTooltip(OutpostFacility facility)
		{
			OutpostFacilityDef def = facility?.def;
			if (def == null)
			{
				return "DreamsOutposts.FacilityNoDef".Translate();
			}
			StringBuilder stringBuilder = new StringBuilder(def.LabelCap);
			if (!string.IsNullOrEmpty(def.description))
			{
				stringBuilder.AppendLine();
				stringBuilder.AppendLine();
				stringBuilder.Append(def.description);
			}
			return stringBuilder.ToString();
		}

		private string FacilityInfoText(OutpostFacility facility)
		{
			OutpostFacilityDef def = facility?.def;
			if (def == null)
			{
				return "DreamsOutposts.FacilityNoDef".Translate();
			}
			StringBuilder stringBuilder = new StringBuilder();
			if (!string.IsNullOrEmpty(def.description))
			{
				stringBuilder.AppendLine(def.description);
			}
			if (def.bombardment != null)
			{
				int shells = OutpostBombardmentUtility.ShellsPerStrike(outpost);
				if (stringBuilder.Length > 0)
				{
					stringBuilder.AppendLine();
				}
				string bombardmentSummary = "DreamsOutposts.BombardmentSummary".Translate(shells, def.bombardment.maxRangeTiles, def.bombardment.CooldownTicks.ToStringTicksToPeriod()).ToString();
				string skillRequirement = def.bombardment.HasSkillRequirement ? " " + "DreamsOutposts.SkillRequirement".Translate(def.bombardment.requiredSkill.LabelCap, def.bombardment.requiredSkillLevel).ToString() : string.Empty;
				stringBuilder.AppendLine(bombardmentSummary + skillRequirement);
				stringBuilder.AppendLine("DreamsOutposts.CostPerStrike".Translate(OutpostBuildUtility.CostLabel(def.bombardment.CostForShells(shells))));
			}
			if (def.bombardmentShellBonus > 0)
			{
				if (stringBuilder.Length > 0)
				{
					stringBuilder.AppendLine();
				}
				stringBuilder.AppendLine("DreamsOutposts.BombardmentBonus".Translate(def.bombardmentShellBonus));
			}
			if (!def.productions.NullOrEmpty())
			{
				if (stringBuilder.Length > 0)
				{
					stringBuilder.AppendLine();
				}
				stringBuilder.AppendLine("DreamsOutposts.Production".Translate());
				for (int i = 0; i < def.productions.Count; i++)
				{
					OutpostProductionProperties production = def.productions[i];
					if (production != null)
					{
						OutpostProductionUtility.TryGetProductionProduct(facility, production, out var product);
						StringBuilder line = new StringBuilder("- " + (product?.LabelCap ?? ((TaggedString)production.id)));
						string summary = production.Worker.ConfigurationSummary(production, facility.GetProductionState(production.id));
						if (!string.IsNullOrEmpty(summary))
						{
							line.Append(" (" + summary + ")");
						}
						if (production.capacityStat != null && OutpostProductionUtility.TryCalculatePersonnelCapacity(outpost, production, out var capacity))
						{
							line.Append(": " + production.capacityStat.LabelCap + " total " + capacity.ToString("0.##"));
						}
						if (production.HasSkillRequirement)
						{
							line.Append(string.Concat(" (" + production.requiredSkill.LabelCap + " >= ", production.requiredSkillLevel.ToString(), " only)"));
						}
						if (OutpostProductionUtility.TryCalculateExpectedOutput(outpost, facility, production, out var expectedOutput))
						{
							line.Append(", expected " + expectedOutput.ToString("0.#") + " every " + production.intervalTicks.ToStringTicksToPeriod());
						}
						stringBuilder.AppendLine(line.ToString());
					}
				}
			}
			return stringBuilder.ToString().TrimEndNewlines();
		}
	}
}
