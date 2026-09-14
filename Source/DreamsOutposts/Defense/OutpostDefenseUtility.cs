using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 据点防卫值的唯一计算入口。Defense 是独立的据点能力，不属于 EventCategory 权重系统，
	/// 也不作为运行时值存档：每次调用都根据当前 Pawn 和已安装设施重新计算。
	/// 第一版只看技能等级，不计算武器、护甲、武器品质、DPS、射程和健康状态修正。
	/// </summary>
	public static class OutpostDefenseUtility
	{
		/// <summary>
		/// 单个 Pawn 的防卫：max(可用的 Shooting 等级, 可用的 Melee 等级)，两者都不可用则为 0。
		/// </summary>
		public static int PawnDefense(Pawn pawn)
		{
			if (pawn?.skills == null)
			{
				return 0;
			}
			int shooting = AvailableSkillLevel(pawn, SkillDefOf.Shooting);
			int melee = AvailableSkillLevel(pawn, SkillDefOf.Melee);
			return Mathf.Max(Mathf.Max(shooting, melee), 0);
		}

		/// <summary>
		/// 技能被这个 Pawn 禁用时返回 -1（表示不可用），否则返回技能等级。
		/// 禁用判断直接复用原版 SkillRecord.TotallyDisabled —— 它内部走的是
		/// def.IsDisabled(pawn.CombinedDisabledWorkTags, pawn.GetDisabledWorkTypes()) 加 PermanentlyDisabled，
		/// 已经涵盖背景故事、特性、基因和 WorkTags，这里不重新实现一套。
		/// 等级取 GetLevel()，与原版技能界面显示的数字一致（含基因天赋修正）。
		/// 公开出来只为 UI 展示来源，不参与别的计算。
		/// </summary>
		public static int AvailableSkillLevel(Pawn pawn, SkillDef skillDef)
		{
			if (pawn?.skills == null || skillDef == null)
			{
				return -1;
			}
			SkillRecord record = pawn.skills.GetSkill(skillDef);
			// 原版 GetSkill 找不到时会 Log.Error 并退回 skills[0]，所以这里额外确认 def 真的对得上，
			// 避免把别的技能等级算成射击或格斗。
			if (record == null || record.def != skillDef || record.TotallyDisabled)
			{
				return -1;
			}
			return record.GetLevel();
		}

		/// <summary>据点里所有 Pawn 的防卫之和。</summary>
		public static int PawnDefenseTotal(Outpost outpost)
		{
			if (outpost?.pawns == null)
			{
				return 0;
			}
			int total = 0;
			List<Pawn> pawns = outpost.pawns.InnerListForReading;
			for (int i = 0; i < pawns.Count; i++)
			{
				total += PawnDefense(pawns[i]);
			}
			return total;
		}

		/// <summary>
		/// 已安装设施提供的防卫之和。核心设施和扩展设施统一走 outpost.Facilities，这里不区分两者。
		/// </summary>
		public static float FacilityDefenseTotal(Outpost outpost)
		{
			if (outpost == null)
			{
				return 0f;
			}
			float total = 0f;
			foreach (OutpostFacility facility in outpost.OperationalFacilities)
			{
				total += facility?.def?.defense ?? 0f;
			}
			return total + OutpostTemporaryEffectUtility.DefenseOffset(outpost);
		}

		/// <summary>据点总防卫：Σ Pawn + Σ 设施。</summary>
		public static float TotalDefense(Outpost outpost)
		{
			return (float)PawnDefenseTotal(outpost) + FacilityDefenseTotal(outpost);
		}

		/// <summary>给 UI tooltip 用的来源分解。</summary>
		public static void GetBreakdown(Outpost outpost, out int pawnDefense, out float facilityDefense)
		{
			pawnDefense = PawnDefenseTotal(outpost);
			facilityDefense = FacilityDefenseTotal(outpost);
		}
	}
}
