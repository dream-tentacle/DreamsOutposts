using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
    /// <summary>
    /// 原版风设施详情正文：借鉴 RimWorld StatsReport 的左右双栏。
    /// 左侧是可选属性（属性名 + 值），右侧显示当前属性的说明。
    /// </summary>
    public sealed class UiVanillaFacilityDetailsModalBody : IUiModalBody
    {
        private const float BodyHeight = 500f;

        private readonly UiDetailsView details;
        private readonly List<Entry> entries = new List<Entry>();
        private Vector2 leftScroll;
        private Vector2 rightScroll;
        private int selectedIndex;

        private sealed class Entry
        {
            public string Category;
            public string Label;
            public string Value;
            public string Detail;
        }

        public UiVanillaFacilityDetailsModalBody(UiDetailsView details)
        {
            this.details = details;
            BuildEntries();
            selectedIndex = 0;
        }

        public float Height(float width)
        {
            return BodyHeight;
        }

        public void Draw(Rect rect)
        {
            if (entries.Count == 0)
            {
                return;
            }

            GameFont previousFont = Text.Font;
            TextAnchor previousAnchor = Text.Anchor;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            float leftWidth = Mathf.Floor(rect.width * 0.5f);
            Rect leftRect = new Rect(rect.x, rect.y, leftWidth, rect.height);
            Rect rightRect = new Rect(leftRect.xMax, rect.y, Mathf.Max(rect.xMax - leftRect.xMax, 1f), rect.height);
            DrawEntryList(leftRect);
            DrawSelectedDetail(rightRect);

            Text.Font = previousFont;
            Text.Anchor = previousAnchor;
            GUI.color = Color.white;
        }

        private void BuildEntries()
        {
            string basics = "DreamsOutposts.Ui.Section.Basics".Translate();
            entries.Add(new Entry
            {
                Category = basics,
                Label = "Description".Translate(),
                Value = string.Empty,
                Detail = details.Description ?? string.Empty
            });

            UiFacilityView source = details.Source;
            if (source?.OperationChips.Count > 0)
            {
                UiChipView status = source.OperationChips[0];
                bool enabled = status.Kind == UiChipKind.Good;
                string explanation = "DreamsOutposts.Ui.Details.EnabledDescription".Translate();
                if (!enabled && !string.IsNullOrEmpty(status.Tooltip))
                {
                    explanation += "\n\n" + status.Tooltip;
                }

                entries.Add(new Entry
                {
                    Category = basics,
                    Label = "DreamsOutposts.Ui.Details.Enabled".Translate(),
                    Value = (enabled ? "Yes" : "No").Translate(),
                    Detail = explanation
                });

                for (int i = 1; i < source.OperationChips.Count; i++)
                {
                    string infoValue = source.OperationChips[i].Label ?? string.Empty;
                    entries.Add(new Entry
                    {
                        Category = basics,
                        Label = "DreamsOutposts.Ui.Details.Info".Translate(i),
                        Value = infoValue,
                        Detail = infoValue
                    });
                }
            }

            string production = "DreamsOutposts.Ui.Section.Production".Translate();
            for (int i = 0; i < details.Rules.Count; i++)
            {
                UiRuleView rule = details.Rules[i];
                entries.Add(new Entry
                {
                    Category = production,
                    Label = rule.ProductLabel ?? string.Empty,
                    Value = rule.Facts.Count > 0 ? rule.Facts[0] : string.Empty,
                    Detail = string.Empty
                });

                int infoIndex = 1;
                if (!string.IsNullOrEmpty(rule.ProgressText))
                {
                    AddInfoEntry(production, ref infoIndex, rule.ProgressText);
                }
                for (int factIndex = 1; factIndex < rule.Facts.Count; factIndex++)
                {
                    AddInfoEntry(production, ref infoIndex, rule.Facts[factIndex]);
                }
                for (int inputIndex = 0; inputIndex < rule.Inputs.Count; inputIndex++)
                {
                    UiCostLine line = rule.Inputs[inputIndex];
                    string name = line.Thing != null ? line.Thing.LabelCap.ToString() : "-";
                    AddInfoEntry(production, ref infoIndex,
                        name + "  " + line.Have + " / " + line.Need + "  " + "DreamsOutposts.Ui.Rule.PerUnit".Translate());
                }
                if (!string.IsNullOrEmpty(rule.MaxCraftableText))
                {
                    AddInfoEntry(production, ref infoIndex, rule.MaxCraftableText);
                }
            }

            string bombardment = "DreamsOutposts.Ui.Section.Bombardment".Translate();
            for (int i = 0; i < details.Bombardment.Count; i++)
            {
                KeyValuePair<string, string> row = details.Bombardment[i];
                entries.Add(new Entry
                {
                    Category = bombardment,
                    Label = row.Key,
                    Value = row.Value,
                    Detail = string.Empty
                });
            }
        }

        private void AddInfoEntry(string category, ref int infoIndex, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }
            entries.Add(new Entry
            {
                Category = category,
                Label = "DreamsOutposts.Ui.Details.Info".Translate(infoIndex++),
                Value = value,
                Detail = value
            });
        }

        private void DrawEntryList(Rect rect)
        {
            float viewWidth = Mathf.Max(rect.width - 16f, 40f);
            float contentHeight = MeasureEntryList(viewWidth);
            Rect viewRect = new Rect(0f, 0f, viewWidth, contentHeight);
            Widgets.BeginScrollView(rect, ref leftScroll, viewRect);

            float y = 0f;
            string category = null;
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry.Category != category)
                {
                    Widgets.ListSeparator(ref y, viewRect.width, entry.Category);
                    category = entry.Category;
                }

                float rowWidth = Mathf.Max(viewRect.width - 8f, 20f);
                float rowHeight = EntryHeight(entry, rowWidth);
                Rect row = new Rect(8f, y, rowWidth, rowHeight);
                if (i == selectedIndex)
                {
                    Widgets.DrawHighlightSelected(row);
                }
                else if (Mouse.IsOver(row))
                {
                    Widgets.DrawHighlight(row);
                }

                float valueWidth = row.width * 0.45f;
                Rect labelRect = new Rect(row.x, row.y, row.width - valueWidth, row.height);
                Rect valueRect = new Rect(labelRect.xMax, row.y, valueWidth, row.height);
                Widgets.Label(labelRect, entry.Label ?? string.Empty);
                Widgets.Label(valueRect, entry.Value ?? string.Empty);

                if (Widgets.ButtonInvisible(row))
                {
                    selectedIndex = i;
                    rightScroll = Vector2.zero;
                }
                y += rowHeight;
            }
            Widgets.EndScrollView();
        }

        private float MeasureEntryList(float width)
        {
            float y = 0f;
            string category = null;
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry.Category != category)
                {
                    y += Widgets.ListSeparatorHeight;
                    category = entry.Category;
                }
                y += EntryHeight(entry, Mathf.Max(width - 8f, 20f));
            }
            return y + 16f;
        }

        private static float EntryHeight(Entry entry, float width)
        {
            float valueWidth = Mathf.Max(width * 0.45f, 20f);
            return Text.CalcHeight(entry.Value ?? string.Empty, valueWidth);
        }

        private void DrawSelectedDetail(Rect rect)
        {
            if (selectedIndex < 0 || selectedIndex >= entries.Count)
            {
                return;
            }

            Entry entry = entries[selectedIndex];
            Rect outRect = rect.ContractedBy(10f);
            float viewWidth = Mathf.Max(outRect.width - 16f, 40f);
            float contentHeight = string.IsNullOrEmpty(entry.Detail)
                ? 1f
                : Text.CalcHeight(entry.Detail, Mathf.Max(viewWidth - 4f, 20f)) + 10f;
            Rect viewRect = new Rect(0f, 0f, viewWidth, contentHeight);
            Widgets.BeginScrollView(outRect, ref rightScroll, viewRect);
            if (!string.IsNullOrEmpty(entry.Detail))
            {
                Widgets.Label(new Rect(0f, 0f, viewRect.width - 4f, contentHeight), entry.Detail);
            }
            Widgets.EndScrollView();
        }
    }
}
