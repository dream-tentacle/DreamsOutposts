using System.Collections.Generic;
using System.Linq;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public class OutpostCreationDialog : Window
	{
		private const float CardHeight = 104f;
		private readonly Caravan caravan;
		private readonly List<OutpostTypeDef> defs;
		private Vector2 scroll;
		public override Vector2 InitialSize => new Vector2(Mathf.Min(820f, UI.screenWidth - 20f), Mathf.Min(700f, UI.screenHeight - 20f));
		protected override float Margin => 0f;

		public OutpostCreationDialog(Caravan caravan)
		{
			this.caravan = caravan;
			defs = DefDatabase<OutpostTypeDef>.AllDefsListForReading.OrderBy(d => d.label).ToList();
			forcePause = absorbInputAroundWindow = closeOnClickedOutside = true;
			doWindowBackground = drawShadow = doCloseX = false;
		}

		public override void DoWindowContents(Rect inRect)
		{
			Rect panel = inRect.ContractedBy(UiMetrics.WindowShadowMargin);
			UiDraw.Shadow(panel, (int)UiMetrics.RadiusSm);
			UiDraw.Box(panel, (int)UiMetrics.RadiusSm, UiPalette.Surface, UiPalette.Line);
			Rect title = new Rect(panel.x, panel.y, panel.width, 76f);
			UiDraw.Box(new Rect(title.x + 1f, title.y + 1f, title.width - 2f, title.height - 1f), (int)(UiMetrics.RadiusSm - 1f), UiPalette.Raised, UiPalette.Clear, UiCorners.TopLeft | UiCorners.TopRight);
			UiDraw.Divider(new Rect(title.x, title.yMax - 1f, title.width, 1f), UiPalette.Line);
			float x = title.x + UiMetrics.TitlebarPaddingLeft;
			UiText.Draw(new Rect(x, title.y + 14f, title.width - 90f, UiText.LineHeight(UiFont.Heading)), "DreamsOutposts.CreateOutpost".Translate(), UiFont.Heading, UiPalette.Ink, TextAnchor.UpperLeft, true);
			UiText.Draw(new Rect(x, title.y + 43f, title.width - 90f, UiText.LineHeight(UiFont.Caption)), "DreamsOutposts.CreateOutpostHint".Translate(), UiFont.Caption, UiPalette.Ink2);
			Rect close = new Rect(title.xMax - UiMetrics.TitlebarPaddingRight - UiMetrics.CloseButtonSize, title.y + (title.height - UiMetrics.CloseButtonSize) * .5f, UiMetrics.CloseButtonSize, UiMetrics.CloseButtonSize);
			if (UiWidgets.CloseButton(close, "DreamsOutposts.Ui.Close".Translate())) Close();
			Rect body = new Rect(panel.x + 20f, title.yMax + 18f, panel.width - 40f, panel.yMax - title.yMax - 36f);
			UiWidgets.ScrollView(body, ref scroll, Mathf.Max(defs.Count * 114f - 10f, 1f), DrawCards, true, GetHashCode(), true);
		}

		private void DrawCards(Rect rect)
		{
			for (int i = 0; i < defs.Count; i++) DrawCard(new Rect(rect.x, rect.y + i * 114f, rect.width, CardHeight), defs[i]);
		}

		private void DrawCard(Rect rect, OutpostTypeDef def)
		{
			AcceptanceReport report = OutpostUtility.CanCreate(caravan, def);
			UiDraw.Panel(rect, (int)UiMetrics.RadiusSm, UiPalette.Card, report.Accepted ? UiPalette.Line : UiPalette.BadLine, false);
			Rect icon = new Rect(rect.x + 16f, rect.y + 16f, 48f, 48f);
			UiDraw.Box(icon, (int)UiMetrics.RadiusSm, UiPalette.Raised, UiPalette.Line);
			UiDraw.Icon(icon.ContractedBy(9f), UiIconMap.ForFacility(def.coreFacility), report.Accepted ? UiPalette.BrandText : UiPalette.Ink2);
			float bh = UiWidgets.ButtonHeight(UiButtonSize.Small);
			Rect create = new Rect(rect.xMax - 144f, rect.center.y - bh * .5f, 128f, bh);
			Rect details = new Rect(create.x - 94f, create.y, 86f, bh);
			float tx = icon.xMax + 14f;
			float tw = Mathf.Max(details.x - tx - 14f, 40f);
			UiText.Draw(new Rect(tx, rect.y + 14f, tw, 24f), def.LabelCap, UiFont.Body, UiPalette.Ink, TextAnchor.UpperLeft, true, false, true);
			string coreFacility = "DreamsOutposts.CoreFacilityInfo".Translate(def.coreFacility?.LabelCap ?? "DreamsOutposts.None".Translate());
			UiText.Draw(new Rect(tx, rect.y + 40f, tw, 24f), coreFacility, UiFont.Caption, UiPalette.Ink2, TextAnchor.UpperLeft, false, false, true);
			if (!report.Accepted) UiText.Draw(new Rect(tx, rect.yMax - 24f, tw, 20f), report.Reason, UiFont.Caption, UiPalette.Bad, TextAnchor.UpperLeft, false, false, true);
			if (UiWidgets.Button(details, "Details".Translate(), UiButtonKind.Secondary, true, null, UiButtonSize.Small)) Find.WindowStack.Add(new Dialog_InfoCard(def));
			if (UiWidgets.Button(create, "DreamsOutposts.CreateOutpost".Translate(), UiButtonKind.Primary, report.Accepted, report.Reason, UiButtonSize.Small))
			{
				Close();
				OutpostUtility.Create(caravan, def);
			}
		}
	}
}
