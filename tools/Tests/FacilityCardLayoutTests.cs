// Exercises actual layout code with deterministic font metrics; not a Unity visual test.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DreamsOutposts;
using UnityEngine;

internal static class FacilityCardLayoutTests
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static float Layout(UiFacilityInfoGroup group, float width, bool draw)
    {
        return (float)typeof(CardLayout).GetMethod("LayoutModernFacilityInfoGroup", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(new CardLayout(), new object[] { 0f, 0f, width, group, draw });
    }
    public static void Main()
    {
        int actions = 0;
        var group = new UiFacilityInfoGroup { Title = "Facility", Tooltip = "group explanation" };
        group.Items.Add(new UiFacilityInfoItem { Id = "output", Value = "19%", CardPlacement = UiFacilityCardPlacement.Header });
        group.Items.Add(new UiFacilityInfoItem { Id = "output.extra", Value = "extra output", CardPlacement = UiFacilityCardPlacement.Header });
        group.Items.Add(new UiFacilityInfoItem { Id = "body.a", Value = "first supporting fact", CardPlacement = UiFacilityCardPlacement.Body });
        group.Items.Add(new UiFacilityInfoItem { Id = "body.b", Value = "second supporting fact", CardPlacement = UiFacilityCardPlacement.Body });
        group.Items.Add(new UiFacilityInfoItem { Id = "detail", Value = "detail only", CardPlacement = UiFacilityCardPlacement.Detail });
        for (int i = 0; i < 2; i++) {
            group.Items.Add(new UiFacilityInfoItem { Id = "progress." + i, CardPlacement = UiFacilityCardPlacement.Progress,
                LeftText = "long progress status " + i, RightText = "remaining 100 days", ProgressText = "10%", Progress = 0.1f });
            group.Items.Add(new UiFacilityInfoItem { Id = "action." + i, CardPlacement = UiFacilityCardPlacement.Action,
                CardPriority = UiFacilityCardPriority.Secondary, ActionLabel = "configure " + i, Action = () => actions++ });
        }
        foreach (float width in new[] { 140f, 400f }) {
            UiText.Rows.Clear(); UiDraw.Bars = 0; UiWidgets.Buttons.Clear();
            float measured = Layout(group, width, false);
            Check(UiText.Rows.Count == 0 && UiWidgets.Buttons.Count == 0 && actions == 0, "Measure pass caused drawing or interaction.");
            float drawn = Layout(group, width, true);
            Check(measured == drawn, "Measured and drawn card heights differ.");
            Check(UiText.Rows.Any(row => row.Text == "first supporting fact") && UiText.Rows.Any(row => row.Text == "second supporting fact"), "Renderer dropped a supporting fact.");
            Check(UiText.Rows.Any(row => row.Text == "extra output") && !UiText.Rows.Any(row => row.Text == "detail only"), "Secondary header or detail policy was ignored.");
            Check(UiDraw.Bars == 2 && UiWidgets.Buttons.Count == 2, "Renderer dropped an extra progress or action.");
            Check(UiText.Rows.All(row => row.Rect.x >= 0 && row.Rect.xMax <= width + 0.01f && row.Rect.yMax <= drawn), "Text escaped measured card bounds.");
            var left = UiText.Rows.First(row => row.Text == "long progress status 0");
            var right = UiText.Rows.First(row => row.Text == "remaining 100 days");
            Check(width < 200 ? left.Rect.yMax <= right.Rect.y : left.Rect.y == right.Rect.y, "Summary did not switch between wrapping and columns.");
        }
        Console.WriteLine("PASS actual modern renderer preserves all facts, progress and actions at narrow and wide widths");
        UiWidgets.Click = "configure 1";
        Layout(group, 200f, true);
        Check(actions == 1, "Action triggered more than once or not at all.");
        Check(Layout(new UiFacilityInfoGroup(), 200f, false) == 0f, "Empty group leaves an empty block.");
        Console.WriteLine("PASS equal measure/draw heights, card bounds, side-effect-free measure and working action");
    }
}
namespace Verse {
    public class ThingDef { }
    public static class Widgets { public static void ThingIcon(Rect rect, ThingDef def) { } }
    public static class Extensions { public static string Translate(this string text) { return text; } }
}
namespace UnityEngine {
    public struct Color { }
    public enum TextAnchor { MiddleLeft, MiddleRight, UpperLeft }
    public struct Rect {
        public float x, y, width, height;
        public float xMax => x + width; public float yMax => y + height;
        public Rect(float a, float b, float c, float d) { x = a; y = b; width = c; height = d; }
    }
    public static class Mathf {
        public static float Max(float a, float b) => Math.Max(a,b);
        public static float Min(float a, float b) => Math.Min(a,b);
        public static float Clamp01(float a) => Max(0, Min(1,a));
    }
}
namespace DreamsOutposts {
    public enum UiChipKind { Neutral, Good, Warn, Bad, Info }
    public enum UiFont { Body, Number }
    public enum UiButtonKind { Secondary }
    public enum UiButtonSize { Small }
    public class UiChipView { public UiChipView(string text, UiChipKind tone, string tooltip) { } }
    public static class UiMetrics { public const float ProdPaddingH=8, ProdPaddingV=6, MatIconSize=20, ProdGap=6, BarHeight=4; }
    public static class UiPalette { public static Color Ink, Ink2, Good, Warn, Bad, Accent, Track; }
    public static class UiText {
        public class Row { public Rect Rect; public string Text; }
        public static List<Row> Rows = new List<Row>();
        public static float Width(string text, UiFont font, bool bold = false) => (text ?? "").Length * 7;
        public static float LineHeight(UiFont font) => 18;
        public static float Height(string text, UiFont font, float width) => Math.Max(1,(float)Math.Ceiling(Width(text,font)/Math.Max(1,width))) * 18;
        public static void Draw(Rect rect, string text, UiFont font, Color color, TextAnchor anchor, bool bold=false, bool wrap=false) {
            Rows.Add(new Row { Rect = rect, Text = text });
        }
    }
    public static class UiDraw {
        public static int Bars;
        public static void Bar(Rect rect, float value, Color a, Color b) { Bars++; }
        public static float ChipsHeight(List<UiChipView> chips, float width, bool compact) => 20;
        public static void Chips(Rect rect, List<UiChipView> chips, bool compact) { }
    }
    public static class UiWidgets {
        public static List<string> Buttons = new List<string>();
        public static string Click;
        public static void Tip(Rect rect, string text, int id) { }
        public static float ButtonHeight(UiButtonSize size) => 24;
        public static bool Button(Rect rect, string text, UiButtonKind kind, bool enabled, object unused, UiButtonSize size, string tooltip) {
            Buttons.Add(text); return text == Click;
        }
    }
}
