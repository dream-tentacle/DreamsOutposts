using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	/// <summary>自动空投机的紧凑设施选择列表。</summary>
	public sealed class UiAutomaticAirdropModalBody : IUiModalBody
	{
		private const float RowHeight = 54f;
		private const float RowGap = 8f;
		private readonly Outpost outpost;

		public UiAutomaticAirdropModalBody(Outpost outpost)
		{
			this.outpost = outpost;
		}

		public float Height(float width)
		{
			int count = Producers().Count;
			return count == 0 ? UiText.LineHeight(UiFont.Body) : count * RowHeight + Mathf.Max(count - 1, 0) * RowGap;
		}

		public void Draw(Rect rect)
		{
			List<OutpostFacility> producers = Producers();
			if (producers.Count == 0)
			{
				UiText.Draw(new Rect(rect.x, rect.y, rect.width, UiText.LineHeight(UiFont.Body)),
					"DreamsOutposts.AutomaticAirdropNoProducers".Translate(), UiFont.Body, UiPalette.Ink2);
				return;
			}
			for (int i = 0; i < producers.Count; i++)
			{
				OutpostFacility facility = producers[i];
				Rect row = new Rect(rect.x, rect.y + i * (RowHeight + RowGap), rect.width, RowHeight);
				UiDraw.Box(row, (int)UiMetrics.RadiusSm, UiPalette.Raised, UiPalette.Line);
				bool enabled = facility.autoAirdropEnabled;
				Rect checkbox = new Rect(row.x + 14f, row.y + 8f, row.width - 28f, row.height - 16f);
				if (UiWidgets.Checkbox(checkbox, ref enabled, facility.def.LabelCap, ProductSummary(facility)))
				{
					facility.autoAirdropEnabled = enabled;
				}
			}
		}

		private List<OutpostFacility> Producers()
		{
			List<OutpostFacility> result = new List<OutpostFacility>();
			if (outpost == null)
			{
				return result;
			}
			foreach (OutpostFacility facility in outpost.Facilities)
			{
				if (OutpostAutomaticAirdropUtility.IsSelectableProducer(facility))
				{
					result.Add(facility);
				}
			}
			return result;
		}

		private static string ProductSummary(OutpostFacility facility)
		{
			StringBuilder text = new StringBuilder();
			for (int i = 0; i < facility.def.productions.Count; i++)
			{
				OutpostProductionProperties production = facility.def.productions[i];
				ThingDef product = production?.Worker.GetProduct(production, facility.GetProductionState(production.id));
				if (product == null)
				{
					continue;
				}
				if (text.Length > 0)
				{
					text.Append("、");
				}
				text.Append(product.LabelCap);
			}
			return text.ToString();
		}
	}
}
