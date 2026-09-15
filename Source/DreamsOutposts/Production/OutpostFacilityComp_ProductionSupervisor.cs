using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 营地等级产能倍率的监管条件：本设施（据点的核心设施）正常运转、且据点里有合格的管理者，
	/// 受监管的等级倍率才生效。任一条件不满足时 OutpostProductionUtility.MatchingModifiers 会跳过这些倍率，
	/// 产出回到没有等级倍率时的原始值。
	///
	/// 状态按 <see cref="StateCheckIntervalTicks"/> 缓存在组件里，不在每 tick 的取值路径上重复判定，
	/// 节奏与远程供电等据点设施的检查一致。
	/// </summary>
	public class OutpostFacilityCompProperties_ProductionSupervisor : OutpostFacilityCompProperties
	{
		/// <summary>管理者需要的技能，通常为智识（Intellectual）。</summary>
		public SkillDef requiredSkill;

		/// <summary>管理者的最低技能等级。</summary>
		public int requiredSkillLevel = 5;

		/// <summary>
		/// 监管哪些等级倍率：与 OutpostProductionModifier.facilityTag 比对（忽略大小写）。
		/// 留空表示监管营地的全部等级倍率。
		/// </summary>
		public string facilityTag;

		public OutpostFacilityCompProperties_ProductionSupervisor()
		{
			compClass = typeof(OutpostFacilityComp_ProductionSupervisor);
		}

		public override IEnumerable<string> ConfigErrors()
		{
			foreach (string error in base.ConfigErrors())
			{
				yield return error;
			}
			if (requiredSkill == null)
			{
				yield return "requiredSkill is required; without it nobody can ever be a supervisor.";
			}
			else if (requiredSkillLevel < 1 || requiredSkillLevel > 20)
			{
				yield return "requiredSkillLevel must be between 1 and 20.";
			}
		}
	}

	public class OutpostFacilityComp_ProductionSupervisor : OutpostFacilityComp
	{
		/// <summary>管理者与禁用状态的重算间隔，和远程供电等据点设施的检查同频（1250 tick）。</summary>
		public const int StateCheckIntervalTicks = 1250;

		private int nextStateCheckTick;
		private bool hasSupervisor;
		private bool hubDisabled;
		private int stateVersion;

		public OutpostFacilityCompProperties_ProductionSupervisor Props => (OutpostFacilityCompProperties_ProductionSupervisor)props;

		/// <summary>缓存状态每变化一次 +1。UI 把它算进结构签名，状态翻转时才重建芯片文本。</summary>
		public int StateVersion => stateVersion;

		/// <summary>据点的监管组件：装在核心设施上，所以从 coreFacility 取。没有装这种组件的据点返回 null（等级倍率无条件生效）。</summary>
		public static OutpostFacilityComp_ProductionSupervisor GateFor(Outpost outpost)
		{
			return outpost?.coreFacility?.GetComp<OutpostFacilityComp_ProductionSupervisor>();
		}

		/// <summary>等级倍率现在是否生效：中枢没有被事件禁用，并且据点里有合格管理者。读的是缓存，不重新判定。</summary>
		public bool AllowsLevelFactor => !hubDisabled && hasSupervisor;

		/// <summary>中枢是否被事件禁用（缓存）。</summary>
		public bool HubDisabled => hubDisabled;

		/// <summary>据点里是否有合格管理者（缓存）。</summary>
		public bool HasSupervisor => hasSupervisor;

		public override void Initialize(OutpostFacility parent, OutpostFacilityCompProperties props)
		{
			base.Initialize(parent, props);
			// 状态要等第一次 Tick 才能算（那时才拿得到 Outpost），所以这里只是把检查点推到一个立即到期的值
			nextStateCheckTick = 0;
		}

		public override void Tick(Outpost outpost, int delta)
		{
			int now = Find.TickManager.TicksGame;
			// 被禁用期间 Tick 不会走，恢复后的第一次 Tick 立刻重算，禁用状态不会多残留一个周期
			if (!hubDisabled && now < nextStateCheckTick)
			{
				return;
			}
			RefreshState(outpost, now);
		}

		public override void TickDisabled(Outpost outpost, int delta)
		{
			int now = Find.TickManager.TicksGame;
			if (hubDisabled && now < nextStateCheckTick)
			{
				return;
			}
			RefreshState(outpost, now);
		}

		private void RefreshState(Outpost outpost, int now)
		{
			nextStateCheckTick = now + StateCheckIntervalTicks;
			bool supervisor = HasQualifiedSupervisor(outpost);
			bool disabled = parent != null && OutpostTemporaryEffectUtility.IsFacilityDisabled(outpost, parent);
			if (supervisor == hasSupervisor && disabled == hubDisabled)
			{
				return;
			}
			hasSupervisor = supervisor;
			hubDisabled = disabled;
			stateVersion++;
		}

		/// <summary>
		/// 派驻本据点且技能达标的殖民者。判定口径与机械师中继站的带宽需求一致：
		/// 只看技能等级，不看健康、倒地或是否在别处忙碌。
		/// </summary>
		private bool HasQualifiedSupervisor(Outpost outpost)
		{
			if (Props.requiredSkill == null || outpost == null)
			{
				return false;
			}
			List<Pawn> pawns = outpost.PawnsListForReading;
			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn pawn = pawns[i];
				if (pawn != null && pawn.IsColonist && MeetsRequirement(pawn))
				{
					return true;
				}
			}
			return false;
		}

		public bool MeetsRequirement(Pawn pawn)
		{
			SkillRecord record = pawn?.skills?.GetSkill(Props.requiredSkill);
			return record != null && record.Level >= Props.requiredSkillLevel;
		}

		/// <summary>这个等级倍率是否由本设施监管。</summary>
		public bool Gates(OutpostProductionModifier modifier)
		{
			if (modifier == null)
			{
				return false;
			}
			string wanted = Props.facilityTag?.Trim();
			if (string.IsNullOrEmpty(wanted))
			{
				return true;
			}
			return string.Equals(modifier.facilityTag?.Trim(), wanted, StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>
		/// 本设施监管、却因为中枢被禁用或缺少管理者而没有生效的等级倍率是否命中这条生产规则。
		/// 只给 UI 提示用：真正取值的地方是 OutpostProductionUtility.MatchingModifiers。
		/// </summary>
		public bool SuppressesLevelFactorFor(Outpost outpost, OutpostFacility producingFacility, OutpostProductionProperties production)
		{
			if (production == null || AllowsLevelFactor)
			{
				return false;
			}
			List<OutpostProductionModifier> levelModifiers = outpost?.CurrentLevelProperties?.productionModifiers;
			for (int i = 0; i < (levelModifiers?.Count ?? 0); i++)
			{
				OutpostProductionModifier modifier = levelModifiers[i];
				if (Gates(modifier) && modifier.Matches(production, producingFacility?.def))
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>核心设施卡片上的管理者状态芯片：「已有/缺少 {等级}{技能}管理者」；中枢被事件禁用时改报禁用。</summary>
		public UiChipView BuildStatusChip()
		{
			string key = hubDisabled
				? "DreamsOutposts.ProductionSupervisor.Disabled"
				: (hasSupervisor ? "DreamsOutposts.ProductionSupervisor.Present" : "DreamsOutposts.ProductionSupervisor.Missing");
			string text = key.Translate(Props.requiredSkillLevel, SkillLabel).ToString();
			return new UiChipView(text, AllowsLevelFactor ? UiChipKind.Good : UiChipKind.Warn,
				AllowsLevelFactor ? BuildRuleTooltip() : InactiveReasons());
		}

		/// <summary>没配 requiredSkill 时只可能是配置错误（ConfigErrors 会报），这里退化成占位文本而不是抛异常。</summary>
		public string SkillLabel => (Props.requiredSkill != null) ? Props.requiredSkill.LabelCap.ToString() : "DreamsOutposts.Unknown".Translate().ToString();

		/// <summary>等级倍率没有生效的原因（UI 提示用），多条原因各占一行。</summary>
		public string InactiveReasons()
		{
			StringBuilder builder = new StringBuilder();
			if (hubDisabled)
			{
				builder.Append("DreamsOutposts.ProductionSupervisor.ReasonDisabled".Translate().ToString());
			}
			if (!hasSupervisor)
			{
				if (builder.Length > 0)
				{
					builder.Append("\n");
				}
				builder.Append("DreamsOutposts.ProductionSupervisor.ReasonNoManager".Translate(Props.requiredSkillLevel, SkillLabel).ToString());
			}
			return builder.ToString();
		}

		private string BuildRuleTooltip()
		{
			string gated = string.IsNullOrEmpty(Props.facilityTag?.Trim())
				? "DreamsOutposts.ProductionSupervisor.AllFacilities".Translate().ToString()
				: OutpostFacilityTagRegistry.LabelKey(Props.facilityTag).Translate().ToString();
			return "DreamsOutposts.ProductionSupervisor.Tip".Translate(Props.requiredSkillLevel, SkillLabel, gated).ToString();
		}
	}
}
