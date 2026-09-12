using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 扩建设施的「训练」机制：安装带 &lt;training&gt; 的设施后，据点里符合条件的殖民者
	/// 会按 xpPerHour 持续获得对应技能的经验。
	///
	/// 结算发生在据点的 TickInterval 里（沿用 Outpost.UpdateRateTicks = 1250），
	/// 因此不受游戏速度影响：一小时该拿多少经验，跑一小时就是多少。
	/// </summary>
	public static class OutpostTrainingUtility
	{
		public const int TicksPerHour = 2500;

		/// <summary>
		/// 一次结算最多补多少 tick。据点被长时间搁置（例如读档、大地图暂停）后
		/// lastTrainingTick 可能落后很多，这里给它一个上限，避免一次结算里
		/// 灌进海量经验、直接顶满技能。
		/// </summary>
		public const int MaxElapsedTicksPerTick = 12500;

		private static readonly HashSet<int> errorKeysReported = new HashSet<int>();

		public static OutpostTrainingProperties GetTraining(OutpostFacilityDef def)
		{
			return def?.training;
		}

		public static bool Trains(OutpostFacilityDef def)
		{
			return def?.training?.skill != null && def.training.xpPerHour > 0f;
		}

		/// <summary>这个殖民者能否从这个训练设施获得经验。</summary>
		public static bool CanTrain(Pawn pawn, SkillDef skill)
		{
			if (pawn == null || skill == null || pawn.skills == null)
			{
				return false;
			}
			SkillRecord record = pawn.skills.GetSkill(skill);
			return record != null && !record.TotallyDisabled;
		}

		/// <summary>当前有多少个殖民者会从这个训练设施获得经验（用于 UI 显示）。</summary>
		public static int CountTrainees(Outpost outpost, OutpostFacilityDef def)
		{
			if (outpost == null || !Trains(def))
			{
				return 0;
			}
			int count = 0;
			foreach (Pawn pawn in outpost.Colonists)
			{
				if (CanTrain(pawn, def.training.skill))
				{
					count++;
				}
			}
			return count;
		}

		public static void TickOutpost(Outpost outpost)
		{
			if (outpost == null || outpost.Destroyed)
			{
				return;
			}
			int now = Find.TickManager.TicksGame;
			foreach (OutpostFacility facility in outpost.Facilities)
			{
				if (Trains(facility?.def))
				{
					TickFacility(outpost, facility, now);
				}
			}
		}

		private static void TickFacility(Outpost outpost, OutpostFacility facility, int now)
		{
			OutpostTrainingProperties training = facility.def.training;
			if (facility.lastTrainingTick <= 0)
			{
				facility.lastTrainingTick = now;
				return;
			}
			int elapsed = now - facility.lastTrainingTick;
			if (elapsed <= 0)
			{
				return;
			}
			facility.lastTrainingTick = now;
			if (elapsed > MaxElapsedTicksPerTick)
			{
				elapsed = MaxElapsedTicksPerTick;
			}
			float xp = training.xpPerHour * ((float)elapsed / (float)TicksPerHour);
			if (xp <= 0f)
			{
				return;
			}
			try
			{
				GrantXp(outpost, training, xp);
			}
			catch (Exception ex)
			{
				Log.ErrorOnce("Outpost training failed: outpost=" + outpost.Label + ", facility=" + facility.def.defName + "\n" + ex, GenText.StableStringHash("DreamsOutposts.TrainingFailure." + facility.def.defName));
			}
		}

		private static void GrantXp(Outpost outpost, OutpostTrainingProperties training, float xp)
		{
			List<Pawn> colonists = outpost.PawnsListForReading;
			if (colonists.NullOrEmpty())
			{
				return;
			}
			SkillDef skill = training.skill;
			for (int i = 0; i < colonists.Count; i++)
			{
				Pawn pawn = colonists[i];
				// 只训练殖民者（不含囚犯、奴隶与访客），并且只训练没有被完全禁用该技能的殖民者。
				// 禁用工作类型 / 无能力同样会让 SkillRecord.TotallyDisabled 为 true，这里一并排除。
				if (pawn == null || !pawn.IsColonist || !CanTrain(pawn, skill))
				{
					continue;
				}
				pawn.skills.Learn(skill, xp);
			}
		}
	}
}
