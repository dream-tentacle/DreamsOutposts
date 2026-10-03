using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	/// <summary>Presentation only: matching and activation are resolved by OutpostModifierUtility.</summary>
	public static class UiOutpostModifiers
	{
		public static void Add(UiFacilityInfoModel output, Outpost outpost, IEnumerable<OutpostModifierInfo> modifiers)
		{
			foreach (OutpostModifierInfo info in modifiers)
			{
				if (Mathf.Approximately(info.Factor, 1f) && Mathf.Approximately(info.Offset, 0f)) continue;
				string id = "modifier." + info.Kind + ".";
				id += info.IsLevelModifier ? "level" : UiFacilityFactIds.ForSource("source", info.SourceInstance);
				if (info.Definition != null) id = UiFacilityFactIds.ForSource(id, info.Definition);
				string source = info.SourceInstance?.def?.LabelCap.ToString()
					?? outpost?.outpostTypeDef?.LabelCap.ToString() ?? "DreamsOutposts.Unknown".Translate().ToString();
				if (info.IsLevelModifier)
					source += " · " + "DreamsOutposts.Ui.LevelSource".Translate(outpost?.level ?? 1);
				string tip = "DreamsOutposts.Ui.Modifier.Source".Translate(source).ToString();
				if (!info.Active)
					tip += "\n\n" + "DreamsOutposts.Ui.Chip.LevelFactorSuppressed".Translate() + "\n" + info.InactiveReason;
				string label = ("DreamsOutposts.Ui.Modifier." + info.Kind).Translate().ToString();
				string suffix = info.Active ? string.Empty : " · " + "DreamsOutposts.Ui.Modifier.Inactive".Translate().ToString();
				if (!Mathf.Approximately(info.Factor, 1f))
					output.AddCompactFact(id + ".factor", label + " ×" + info.Factor.ToString("0.##") + suffix,
						!info.Active ? UiChipKind.Warn : info.Factor >= 1f ? UiChipKind.Good : UiChipKind.Bad, tip);
				if (!Mathf.Approximately(info.Offset, 0f))
					output.AddCompactFact(id + ".offset", label + " +" + info.Offset.ToString("0.##") + suffix,
						!info.Active ? UiChipKind.Warn : UiChipKind.Neutral, tip);
			}
		}
	}
}
