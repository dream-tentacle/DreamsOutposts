using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 边缘仙路（RimImmortal）修仙境界的只读反射入口。
	/// 和血肉巢穴兼容一样，这里不引用对方程序集，只按名字解析类型和字段，对方缺席或改结构时只是拿不到境界。
	/// 读取链路：Hediff_RI_EnergyRoot.energy → Pawn_EnergyTracker.currentRI → RimImmortal.def → RimImmortalDef.level
	/// 「蓬絮」的 level 为 0，登仙为 10。
	/// </summary>
	public static class XianluCultivationUtility
	{
		/// <summary>灵芽（灵根）在边缘仙路里的 HediffDef 名，只用于日志。</summary>
		public const string EnergyRootHediffDefName = "Hediff_RI_EnergyRoot";

		private const int RealmReadFailureKey = 7645123;

		private static bool resolved;

		private static bool available;

		private static Type energyRootType;

		private static FieldInfo energyField;

		private static FieldInfo currentRiField;

		private static FieldInfo immortalDefField;

		private static FieldInfo levelField;

		/// <summary>反射是否可用。任何一环缺失都为 false，此时调用方应把所有人都当成凡人。</summary>
		public static bool Available
		{
			get
			{
				EnsureResolved();
				return available;
			}
		}

		public static void EnsureResolved()
		{
			if (resolved)
			{
				return;
			}
			resolved = true;
			try
			{
				energyRootType = AccessTools.TypeByName("Core.Hediff_RI_EnergyRoot");
				Type trackerType = AccessTools.TypeByName("Core.Pawn_EnergyTracker");
				Type immortalType = AccessTools.TypeByName("Core.RimImmortal");
				Type realmDefType = AccessTools.TypeByName("Core.RimImmortalDef");
				energyField = (energyRootType == null) ? null : AccessTools.Field(energyRootType, "energy");
				currentRiField = (trackerType == null) ? null : AccessTools.Field(trackerType, "currentRI");
				immortalDefField = (immortalType == null) ? null : AccessTools.Field(immortalType, "def");
				levelField = (realmDefType == null) ? null : AccessTools.Field(realmDefType, "level");
			}
			catch (Exception ex)
			{
				Log.Error("[DreamsOutposts] Failed to resolve the RimImmortal cultivation API: " + ex);
			}
			available = energyRootType != null && energyField != null && currentRiField != null && immortalDefField != null && levelField != null && levelField.FieldType == typeof(int);
		}

		/// <summary>
		/// 读取一个小人的修仙境界等级。没有灵芽（不是修炼者）或反射不可用时返回 false。
		/// </summary>
		public static bool TryGetRealmLevel(Pawn pawn, out int realmLevel)
		{
			realmLevel = 0;
			if (pawn?.health?.hediffSet == null)
			{
				return false;
			}
			EnsureResolved();
			if (!available)
			{
				return false;
			}
			try
			{
				Hediff energyRoot = FindEnergyRoot(pawn);
				if (energyRoot == null)
				{
					return false;
				}
				object tracker = energyField.GetValue(energyRoot);
				if (tracker == null)
				{
					return false;
				}
				object currentImmortal = currentRiField.GetValue(tracker);
				if (currentImmortal == null)
				{
					return false;
				}
				object realmDef = immortalDefField.GetValue(currentImmortal);
				if (realmDef == null)
				{
					return false;
				}
				realmLevel = (int)levelField.GetValue(realmDef);
				return true;
			}
			catch (Exception ex)
			{
				Log.ErrorOnce("[DreamsOutposts] Failed to read a pawn's cultivation rank from RimImmortal; that pawn will be treated as a mortal this time.\n" + ex, RealmReadFailureKey);
				realmLevel = 0;
				return false;
			}
		}

		private static Hediff FindEnergyRoot(Pawn pawn)
		{
			List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
			for (int i = 0; i < (hediffs?.Count ?? 0); i++)
			{
				Hediff hediff = hediffs[i];
				if (hediff != null && energyRootType.IsInstanceOfType(hediff))
				{
					return hediff;
				}
			}
			return null;
		}
	}
}
