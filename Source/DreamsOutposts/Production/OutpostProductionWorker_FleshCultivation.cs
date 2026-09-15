using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace DreamsOutposts
{
	/// <summary>按驻守的我方血肉兽实际巢群容量占用计算三种血肉产出的效率。</summary>
	public class OutpostProductionWorker_FleshCultivation : OutpostProductionWorker
	{
		private static readonly Type FleshBeastCachePropsType = AccessTools.TypeByName("FleshHive.CompProperties_FleshBeastCache");
		private static readonly Type UnitPropsType = AccessTools.TypeByName("HiveCreatureFramework.UnitCompProperties");
		private static readonly FieldInfo FleshBeastSizeField = FleshBeastCachePropsType == null ? null : AccessTools.Field(FleshBeastCachePropsType, "size");
		private static readonly FieldInfo GroupCostField = UnitPropsType == null ? null : AccessTools.Field(UnitPropsType, "groupCost");

		public override bool UsesPersonnelCapacity(OutpostProductionProperties production)
		{
			return true;
		}

		public override float CalculatePersonnelCapacity(IEnumerable<Pawn> pawns, OutpostTypeDef outpostTypeDef, OutpostProductionProperties production)
		{
			OutpostProductionProperties_FleshCultivation props = production as OutpostProductionProperties_FleshCultivation;
			float efficiency = props?.baseEfficiency ?? 2f;
			if (pawns == null)
			{
				return efficiency;
			}

			foreach (Pawn pawn in pawns)
			{
				if (pawn == null || pawn.Dead || pawn.Faction != Faction.OfPlayer || !IsFleshBeast(pawn))
				{
					continue;
				}
				efficiency += (props?.efficiencyPerGroupCost ?? 1f) * GroupCost(pawn);
			}
			return efficiency;
		}

		public override IEnumerable<string> ConfigErrors(OutpostProductionProperties production)
		{
			foreach (string error in base.ConfigErrors(production))
			{
				yield return error;
			}
			OutpostProductionProperties_FleshCultivation props = production as OutpostProductionProperties_FleshCultivation;
			if (props == null)
			{
				yield return "FleshCultivation worker requires OutpostProductionProperties_FleshCultivation.";
				yield break;
			}
			if (float.IsNaN(props.baseEfficiency) || float.IsInfinity(props.baseEfficiency) || props.baseEfficiency < 0f)
			{
				yield return "baseEfficiency must be finite and non-negative.";
			}
			if (float.IsNaN(props.efficiencyPerGroupCost) || float.IsInfinity(props.efficiencyPerGroupCost) || props.efficiencyPerGroupCost < 0f)
			{
				yield return "efficiencyPerGroupCost must be finite and non-negative.";
			}
		}

		private static bool IsFleshBeast(Pawn pawn)
		{
			if (FleshBeastCachePropsType == null || FleshBeastSizeField == null || pawn.kindDef?.race?.comps == null)
			{
				return false;
			}
			foreach (CompProperties comp in pawn.kindDef.race.comps)
			{
				if (comp != null && FleshBeastCachePropsType.IsInstanceOfType(comp) && FleshBeastSizeField.GetValue(comp) != null)
				{
					return true;
				}
			}
			return false;
		}

		private static int GroupCost(Pawn pawn)
		{
			if (UnitPropsType == null || GroupCostField == null || pawn.kindDef?.race?.comps == null)
			{
				return 1;
			}
			foreach (CompProperties comp in pawn.kindDef.race.comps)
			{
				if (comp != null && UnitPropsType.IsInstanceOfType(comp))
				{
					object value = GroupCostField.GetValue(comp);
					return value is int cost ? cost : 1;
				}
			}
			return 1;
		}
	}
}
