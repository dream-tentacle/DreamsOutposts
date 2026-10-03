using System.Collections.Generic;
using Verse;

namespace DreamsOutposts
{
	public enum OutpostModifierKind { Production, Power, Training }

	/// <summary>A resolved modifier shared by simulation and presentation. Inactive entries remain explainable.</summary>
	public sealed class OutpostModifierInfo
	{
		public OutpostModifierKind Kind;
		public object Definition;
		public OutpostProductionModifier ProductionModifier;
		public OutpostFacility SourceInstance;
		public bool IsLevelModifier;
		public float Factor = 1f;
		public float Offset;
		public bool Active = true;
		public string InactiveReason;
		public float EffectiveFactor => Active ? Factor : 1f;
		public float EffectiveOffset => Active ? Offset : 0f;
	}

	public static class OutpostModifierUtility
	{
		public static IEnumerable<OutpostModifierInfo> ForProcess(Outpost outpost, OutpostFacilityDef target, OutpostProcessProperties process)
		{
			if (outpost == null || process == null) yield break;
			foreach (OutpostFacility source in outpost.OperationalFacilities)
			{
				List<OutpostProductionModifier> modifiers = source?.def?.productionModifiers;
				if (modifiers == null) continue;
				foreach (OutpostProductionModifier modifier in modifiers)
				{
					if (modifier == null || !modifier.Matches(process, target)) continue;
					yield return new OutpostModifierInfo
					{
						Kind = OutpostModifierKind.Production, Definition = modifier, ProductionModifier = modifier,
						SourceInstance = source, Factor = modifier.factor, Offset = modifier.offset
					};
				}
			}
			List<OutpostProductionModifier> levels = outpost.CurrentLevelProperties?.productionModifiers;
			if (levels == null) yield break;
			OutpostFacilityComp_ProductionSupervisor gate = OutpostFacilityComp_ProductionSupervisor.GateFor(outpost);
			foreach (OutpostProductionModifier modifier in levels)
			{
				if (modifier == null || !modifier.Matches(process, target)) continue;
				OutpostModifierInfo info = Level(outpost, OutpostModifierKind.Production, modifier, modifier.factor,
					gate != null && gate.Gates(modifier) ? gate : null);
				info.ProductionModifier = modifier;
				info.Offset = modifier.offset;
				yield return info;
			}
		}

		public static OutpostModifierInfo Power(Outpost outpost)
		{
			OutpostLevelProperties level = outpost?.CurrentLevelProperties;
			OutpostFacilityComp_ProductionSupervisor gate = OutpostFacilityComp_ProductionSupervisor.GateFor(outpost);
			return Level(outpost, OutpostModifierKind.Power, level, level?.powerGenerationFactor ?? 1f,
				gate != null && gate.GatesFacilityTag(OutpostFacilityTagRegistry.PowerGeneration) ? gate : null);
		}

		public static OutpostModifierInfo Training(Outpost outpost)
		{
			OutpostLevelProperties level = outpost?.CurrentLevelProperties;
			float factor = level?.trainingFactor ?? 1f;
			if (float.IsNaN(factor) || float.IsInfinity(factor) || factor <= 0f) factor = 1f;
			// Training level bonuses have never required an operational supervisor.
			return Level(outpost, OutpostModifierKind.Training, level, factor, null);
		}

		/// <summary>Works for installed facilities and construction previews alike.</summary>
		public static IEnumerable<OutpostModifierInfo> ForFacility(Outpost outpost, OutpostFacilityDef target)
		{
			if (outpost == null || target == null) yield break;
			foreach (OutpostProcessProperties process in target.Processes)
				foreach (OutpostModifierInfo info in ForProcess(outpost, target, process))
					yield return info;
			if (target.GetCompProperties<OutpostFacilityCompProperties_PowerGenerator>() != null)
				yield return Power(outpost);
			if (OutpostTrainingUtility.Trains(target) && OutpostTrainingUtility.ReceivesTrainingFactor(target))
				yield return Training(outpost);
		}

		private static OutpostModifierInfo Level(Outpost outpost, OutpostModifierKind kind, object definition,
			float factor, OutpostFacilityComp_ProductionSupervisor gate)
		{
			bool active = gate == null || gate.AllowsLevelFactor;
			return new OutpostModifierInfo
			{
				Kind = kind, Definition = definition, SourceInstance = outpost?.coreFacility,
				IsLevelModifier = true, Factor = factor, Active = active,
				InactiveReason = active ? null : gate.InactiveReasons()
			};
		}
	}
}
