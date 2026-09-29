using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public class OutpostEducationProperties : OutpostFacilityCompProperties
	{
		public float baseQuality;
		public float qualityBonus;
		public bool settlesGrowth;

		public OutpostEducationProperties()
		{
			compClass = typeof(OutpostFacilityComp_Education);
		}

		public override IEnumerable<string> ConfigErrors()
		{
			foreach (string error in base.ConfigErrors()) yield return error;
			if (baseQuality < 0f || baseQuality > 1f) yield return "baseQuality must be between 0 and 1.";
			if (qualityBonus < 0f || qualityBonus > 1f) yield return "qualityBonus must be between 0 and 1.";
		}
	}

	public static class OutpostEducationUtility
	{
		public const int TicksPerDay = 60000;
		public const float MaxQuality = 0.95f;

		public static OutpostEducationProperties GetEducation(OutpostFacilityDef def)
		{
			return def?.GetCompProperties<OutpostEducationProperties>();
		}

		public static bool IsSchoolOperational(Outpost outpost, out AcceptanceReport report)
		{
			if (outpost == null || !ModsConfig.BiotechActive)
			{
				report = false;
				return false;
			}

			foreach (OutpostFacility facility in outpost.Facilities)
			{
				OutpostEducationProperties education = GetEducation(facility?.def);
				if (education?.settlesGrowth == true)
				{
					report = facility.CanOperate(outpost);
					return report.Accepted;
				}
			}
			report = false;
			return false;
		}

		public static float ConfiguredQuality(Outpost outpost)
		{
			if (outpost == null || !ModsConfig.BiotechActive)
			{
				return 0f;
			}

			float quality = 0f;
			foreach (OutpostFacility facility in outpost.Facilities)
			{
				OutpostEducationProperties education = GetEducation(facility?.def);
				if (education == null)
				{
					continue;
				}
				quality += education.baseQuality + education.qualityBonus;
			}
			return Mathf.Clamp(quality, 0f, MaxQuality);
		}

		public static float EffectiveQuality(Outpost outpost)
		{
			AcceptanceReport report;
			return IsSchoolOperational(outpost, out report) ? ConfiguredQuality(outpost) : 0f;
		}

		public static float GrowthPointsPerDay(Pawn pawn, Outpost outpost)
		{
			if (pawn?.ageTracker == null || pawn.Dead || !pawn.IsColonist || !pawn.DevelopmentalStage.Child() || !pawn.ageTracker.canGainGrowthPoints)
			{
				return 0f;
			}
			float quality = EffectiveQuality(outpost);
			if (quality <= 0f)
			{
				return 0f;
			}
			float ageFactor = pawn.ageTracker.AgeBiologicalYearsFloat < 7f ? 0.75f : 1f;
			return quality * ageFactor * pawn.ageTracker.ChildAgingMultiplier;
		}

		public static int StudentCount(Outpost outpost)
		{
			if (outpost?.pawns == null || !ModsConfig.BiotechActive)
			{
				return 0;
			}

			int count = 0;
			List<Pawn> pawns = outpost.pawns.InnerListForReading;
			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn pawn = pawns[i];
				if (pawn != null && !pawn.Dead && pawn.IsColonist && pawn.DevelopmentalStage.Child())
				{
					count++;
				}
			}
			return count;
		}

		public static void TickSchool(Outpost outpost, int delta)
		{
			if (outpost == null || outpost.Destroyed || delta <= 0 || !ModsConfig.BiotechActive)
			{
				return;
			}

			float quality = EffectiveQuality(outpost);
			if (quality <= 0f || outpost.pawns == null)
			{
				return;
			}

			float elapsedDays = (float)delta / TicksPerDay;
			List<Pawn> pawns = outpost.pawns.InnerListForReading;
			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn pawn = pawns[i];
				if (pawn == null || pawn.Dead || !pawn.IsColonist || !pawn.DevelopmentalStage.Child() || pawn.ageTracker == null)
				{
					continue;
				}

				if (pawn.ageTracker.canGainGrowthPoints)
				{
					pawn.ageTracker.growthPoints += GrowthPointsPerDay(pawn, outpost) * elapsedDays;
				}

				// Outpost pawns do not receive normal Need ticks. Keep the visible Learning need
				// aligned with the school's simulated long-term education quality, but do not use
				// the need itself to calculate growth points.
				if (pawn.needs?.learning != null)
				{
					pawn.needs.learning.CurLevelPercentage = quality;
				}
			}
		}
	}

	public class OutpostFacilityComp_Education : OutpostFacilityComp
	{
		private OutpostEducationProperties Props => (OutpostEducationProperties)props;

		public override void Update(Outpost outpost, int delta)
		{
			if (Props.settlesGrowth)
			{
				OutpostEducationUtility.TickSchool(outpost, delta);
			}
		}

		public override void BuildUiInfo(Outpost outpost, UiFacilityInfoModel output)
		{
			if (Props.settlesGrowth)
			{
				UiFacilityInfoGroup group = NewUiInfoGroup("DreamsOutposts.Education.Quality".Translate().ToString());
				group.Items.Add(new UiFacilityInfoItem
				{
					Id = "education.quality",
					Kind = UiFacilityInfoKind.Value,
					CardPlacement = UiFacilityCardPlacement.Header,
					CardPriority = UiFacilityCardPriority.Core,
					Importance = UiFacilityInfoImportance.Primary,
					Value = OutpostEducationUtility.EffectiveQuality(outpost).ToStringPercent("F0")
				});
				group.Items.Add(new UiFacilityInfoItem
				{
					Id = "education.students",
					Kind = UiFacilityInfoKind.Value,
					CardPlacement = UiFacilityCardPlacement.Body,
					CardPriority = UiFacilityCardPriority.Core,
					Importance = UiFacilityInfoImportance.Supporting,
					Value = "DreamsOutposts.Education.Students".Translate(OutpostEducationUtility.StudentCount(outpost)).ToString()
				});
				output.Groups.Add(group);
			}
			else if (Props.qualityBonus > 0f)
			{
				UiFacilityInfoGroup group = NewUiInfoGroup("DreamsOutposts.Education.Contribution".Translate().ToString());
				group.Items.Add(new UiFacilityInfoItem
				{
					Id = "education.contribution",
					Kind = UiFacilityInfoKind.Value,
					CardPlacement = UiFacilityCardPlacement.Header,
					CardPriority = UiFacilityCardPriority.Core,
					Importance = UiFacilityInfoImportance.Primary,
					Value = "+" + Props.qualityBonus.ToStringPercent("F0")
				});
				output.Groups.Add(group);
			}
		}
	}
}
