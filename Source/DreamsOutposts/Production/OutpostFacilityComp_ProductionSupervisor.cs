using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 营地等级产能倍率的监管条件：本设施（据点的核心设施）正常运转、且据点里有合格的管理者，
	/// 受监管的等级倍率才生效。任一条件不满足时 OutpostProductionUtility.MatchingModifiers 会跳过这些倍率，
	/// 产出回到没有等级倍率时的原始值。
	///
	/// 状态由据点统一更新入口刷新；取值路径只读取缓存。
	/// </summary>
	public class OutpostFacilityCompProperties_ProductionSupervisor : OutpostFacilityCompProperties
	{
		/// <summary>
		/// 监管哪些等级倍率：与 OutpostProductionModifier.facilityTag 比对（忽略大小写）。
		/// 留空表示监管营地的全部等级倍率。
		/// </summary>
		public string facilityTag;

		public OutpostFacilityCompProperties_ProductionSupervisor()
		{
			compClass = typeof(OutpostFacilityComp_ProductionSupervisor);
		}

	}

	public class OutpostFacilityComp_ProductionSupervisor : OutpostFacilityComp
	{
		private bool operational;
		private string inactiveReason;
		private int stateVersion;

		public OutpostFacilityCompProperties_ProductionSupervisor Props => (OutpostFacilityCompProperties_ProductionSupervisor)props;

		/// <summary>缓存状态每变化一次 +1。UI 把它算进结构签名，状态翻转时才重建芯片文本。</summary>
		public int StateVersion => stateVersion;

		/// <summary>据点的监管组件：装在核心设施上，所以从 coreFacility 取。没有装这种组件的据点返回 null（等级倍率无条件生效）。</summary>
		public static OutpostFacilityComp_ProductionSupervisor GateFor(Outpost outpost)
		{
			return outpost?.coreFacility?.GetComp<OutpostFacilityComp_ProductionSupervisor>();
		}

		/// <summary>统一设施启用状态决定等级倍率是否生效。</summary>
		public bool AllowsLevelFactor => operational;

		public override void Update(Outpost outpost, int delta)
		{
			RefreshState(outpost);
		}

		public override void UpdateDisabled(Outpost outpost, int delta)
		{
			RefreshState(outpost);
		}

		private void RefreshState(Outpost outpost)
		{
			AcceptanceReport report = parent.CanOperate(outpost);
			string reason = report.Accepted ? null : report.Reason;
			if (operational == report.Accepted && inactiveReason == reason) return;
			operational = report.Accepted;
			inactiveReason = reason;
			stateVersion++;
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

		/// <summary>这个设施标签是否由本中枢监管。</summary>
		public bool GatesFacilityTag(string facilityTag)
		{
			string wanted = Props.facilityTag?.Trim();
			return string.IsNullOrEmpty(wanted) ||
				string.Equals(facilityTag?.Trim(), wanted, StringComparison.OrdinalIgnoreCase);
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

		public string InactiveReasons() => inactiveReason ?? string.Empty;
	}
}
