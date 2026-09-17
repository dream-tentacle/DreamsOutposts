using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public static class Window_OutpostBandTuning
	{
		public static void Open(Outpost outpost)
		{
			if (outpost == null) return;
			Window_OutpostModal window = new Window_OutpostModal
			{
				TitleText = "DreamsOutposts.BandTuning.Title".Translate(),
				SubText = outpost.LabelCap,
				PanelWidth = UiMetrics.ModalWideWidth,
				Body = new UiBandTuningModalBody(outpost)
			};
			AddCloseFooter(window);
			Find.WindowStack.Add(window);
		}

		private static void AddCloseFooter(Window_OutpostModal window)
		{
			window.FooterDrawer = delegate(Rect rect)
			{
				string label = "DreamsOutposts.Ui.Close".Translate();
				float height = UiWidgets.ButtonHeight(UiButtonSize.Normal);
				float width = UiWidgets.ButtonWidth(label);
				Rect button = new Rect(rect.xMax - width, rect.y + (rect.height - height) * 0.5f, width, height);
				if (UiWidgets.Button(button, label)) window.Close();
			};
		}

		public static void OpenMechanitorPicker(Outpost outpost, OutpostFacilityComp_Bandwidth node)
		{
			Window_OutpostModal window = new Window_OutpostModal
			{
				TitleText = "DreamsOutposts.BandTuning.ChooseTitle".Translate(),
				SubText = node.parent.def.LabelCap,
				PanelWidth = UiMetrics.ModalNormalWidth
			};
			window.Body = new UiMechanitorPickerModalBody(node, window);
			AddCloseFooter(window);
			Find.WindowStack.Add(window);
		}
	}

	public sealed class UiBandTuningModalBody : IUiModalBody
	{
		private const float StatusHeight = 52f;
		private const float StatusGap = 14f;
		private const float CardHeight = 92f;
		private const float CardGap = 10f;
		private readonly Outpost outpost;

		public UiBandTuningModalBody(Outpost outpost)
		{
			this.outpost = outpost;
		}

		public float Height(float width)
		{
			int count = OutpostBandwidthUtility.Nodes(outpost).Count();
			return StatusHeight + StatusGap + count * CardHeight + Mathf.Max(count - 1, 0) * CardGap;
		}

		public void Draw(Rect rect)
		{
			bool hasOperator = OutpostBandwidthUtility.HasOperator(outpost);
			Rect status = new Rect(rect.x, rect.y, rect.width, StatusHeight);
			UiDraw.Box(status, (int)UiMetrics.RadiusSm,
				hasOperator ? UiPalette.GoodBg : UiPalette.BadBg,
				hasOperator ? UiPalette.GoodLine : UiPalette.BadLine);
			UiDraw.Icon(new Rect(status.x + 14f, status.y + 14f, 24f, 24f),
				hasOperator ? UiIcon.Check : UiIcon.Cross,
				hasOperator ? UiPalette.Good : UiPalette.Bad);
			UiText.Draw(new Rect(status.x + 48f, status.y, status.width - 62f, status.height),
				hasOperator ? "DreamsOutposts.BandTuning.OperatorReady".Translate() : "DreamsOutposts.BandTuning.OperatorMissing".Translate(),
				UiFont.Body, hasOperator ? UiPalette.Good : UiPalette.Bad, TextAnchor.MiddleLeft, true, false, true);

			List<OutpostFacilityComp_Bandwidth> nodes = OutpostBandwidthUtility.Nodes(outpost).ToList();
			float y = status.yMax + StatusGap;
			for (int i = 0; i < nodes.Count; i++)
			{
				DrawNode(new Rect(rect.x, y, rect.width, CardHeight), nodes[i], hasOperator);
				y += CardHeight + CardGap;
			}
		}

		private void DrawNode(Rect card, OutpostFacilityComp_Bandwidth node, bool hasOperator)
		{
			UiDraw.Box(card, (int)UiMetrics.RadiusSm, UiPalette.Card, UiPalette.Line);
			float buttonWidth = Mathf.Max(UiWidgets.ButtonWidth("DreamsOutposts.BandTuning.Choose".Translate(), UiButtonSize.Small), 132f);
			Rect button = new Rect(card.xMax - 14f - buttonWidth, card.y + (card.height - UiWidgets.ButtonHeight(UiButtonSize.Small)) * 0.5f,
				buttonWidth, UiWidgets.ButtonHeight(UiButtonSize.Small));
			float textWidth = Mathf.Max(button.x - card.x - 42f, 80f);
			UiText.Draw(new Rect(card.x + 14f, card.y + 11f, textWidth, UiText.LineHeight(UiFont.Body)),
				node.parent.def.LabelCap, UiFont.Body, UiPalette.Ink, TextAnchor.UpperLeft, true, false, true);

			List<UiChipView> chips = new List<UiChipView>
			{
				new UiChipView("DreamsOutposts.BandTuning.Bonus".Translate(node.Props.bandwidth).ToString(), UiChipKind.Info)
			};
			if (node.IsTuning)
			{
				chips.Add(new UiChipView("DreamsOutposts.BandTuning.Retuning".Translate(
					node.tuningTo?.LabelShortCap ?? "?", node.retuneTicksLeft.ToStringTicksToPeriod()).ToString(),
					hasOperator ? UiChipKind.Warn : UiChipKind.Bad));
			}
			else if (node.tunedTo != null)
			{
				chips.Add(new UiChipView("DreamsOutposts.BandTuning.Tuned".Translate(node.tunedTo.LabelShortCap).ToString(),
					hasOperator ? UiChipKind.Good : UiChipKind.Bad));
			}
			else
			{
				chips.Add(new UiChipView("DreamsOutposts.BandTuning.Untuned".Translate().ToString(), UiChipKind.Neutral));
			}
			UiDraw.Chips(new Rect(card.x + 14f, card.y + 43f, textWidth, card.height - 51f), chips, true);
			if (UiWidgets.Button(button, "DreamsOutposts.BandTuning.Choose".Translate(), UiButtonKind.Secondary,
				true, null, UiButtonSize.Small))
			{
				Window_OutpostBandTuning.OpenMechanitorPicker(outpost, node);
			}
		}
	}

	public sealed class UiMechanitorPickerModalBody : IUiModalBody
	{
		private const float RowHeight = 62f;
		private const float RowGap = 8f;
		private readonly OutpostFacilityComp_Bandwidth node;
		private readonly Window_OutpostModal window;

		public UiMechanitorPickerModalBody(OutpostFacilityComp_Bandwidth node, Window_OutpostModal window)
		{
			this.node = node;
			this.window = window;
		}

		public float Height(float width)
		{
			int count = Mechanitors().Count;
			return count == 0 ? UiText.LineHeight(UiFont.Body) : count * RowHeight + Mathf.Max(count - 1, 0) * RowGap;
		}

		public void Draw(Rect rect)
		{
			List<Pawn> pawns = Mechanitors();
			if (pawns.Count == 0)
			{
				UiText.Draw(new Rect(rect.x, rect.y, rect.width, UiText.LineHeight(UiFont.Body)),
					"DreamsOutposts.BandTuning.NoMechanitors".Translate(), UiFont.Body, UiPalette.Ink2);
				return;
			}
			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn pawn = pawns[i];
				Rect row = new Rect(rect.x, rect.y + i * (RowHeight + RowGap), rect.width, RowHeight);
				bool current = node.IsTuning ? node.tuningTo == pawn : node.tunedTo == pawn;
				UiDraw.Box(row, (int)UiMetrics.RadiusSm, current ? UiPalette.BrandTint : UiPalette.Card,
					current ? UiPalette.BrandLine : UiPalette.Line);
				UiText.Draw(new Rect(row.x + 14f, row.y + 9f, row.width - 170f, UiText.LineHeight(UiFont.Body)),
					pawn.LabelShortCap, UiFont.Body, current ? UiPalette.BrandText : UiPalette.Ink,
					TextAnchor.UpperLeft, true, false, true);
				UiText.Draw(new Rect(row.x + 14f, row.y + 33f, row.width - 170f, UiText.LineHeight(UiFont.Body)),
					"DreamsOutposts.BandTuning.MechanitorBandwidth".Translate(pawn.mechanitor.TotalBandwidth),
					UiFont.Body, UiPalette.Ink2, TextAnchor.UpperLeft, false, false, true);
				string label = current ? "DreamsOutposts.BandTuning.Restart".Translate() : "DreamsOutposts.BandTuning.Select".Translate();
				float buttonWidth = Mathf.Max(UiWidgets.ButtonWidth(label, UiButtonSize.Small), 108f);
				Rect button = new Rect(row.xMax - 14f - buttonWidth,
					row.y + (row.height - UiWidgets.ButtonHeight(UiButtonSize.Small)) * 0.5f,
					buttonWidth, UiWidgets.ButtonHeight(UiButtonSize.Small));
				if (UiWidgets.Button(button, label, current ? UiButtonKind.Secondary : UiButtonKind.Primary,
					true, null, UiButtonSize.Small))
				{
					node.StartTuning(pawn);
					window.Close();
				}
			}
		}

		private static List<Pawn> Mechanitors()
		{
			return PawnsFinder.AllMapsWorldAndTemporary_Alive
				.Where(p => p.Faction == Faction.OfPlayer && MechanitorUtility.IsMechanitor(p))
				.Distinct().OrderBy(p => p.LabelShort).ToList();
		}
	}
}
