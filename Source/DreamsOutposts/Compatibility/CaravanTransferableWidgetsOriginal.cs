using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 调用原版 CaravanUIUtility.CreateCaravanTransferableWidgets 的未补丁方法体。
	///
	/// 部分界面 Mod 会用返回 false 的 Harmony Prefix 完全替换该公共方法，却不填写它的三个 out 参数，
	/// 因为它们只在随后针对原版 Dialog_FormCaravan 的补丁里使用自己的静态控件。
	/// 据点窗口不是原版窗口，必须绕过这种全局替换，否则三个控件都会保持 null。
	/// Reverse Patch 让控件构造和物品分类继续由当前 RimWorld 原版实现负责，不在本 Mod 复制那套逻辑。
	/// </summary>
	[HarmonyPatch]
	internal static class CaravanTransferableWidgetsOriginal
	{
		public static bool TryCreate(
			List<TransferableOneWay> transferables,
			out TransferableOneWayWidget pawnsTransfer,
			out TransferableOneWayWidget itemsTransfer,
			out TransferableOneWayWidget travelSuppliesTransfer,
			string thingCountTip,
			IgnorePawnsInventoryMode ignorePawnInventoryMass,
			Func<float> availableMassGetter,
			bool ignoreSpawnedCorpsesGearAndInventoryMass,
			PlanetTile tile,
			bool playerPawnsReadOnly = false)
		{
			pawnsTransfer = null;
			itemsTransfer = null;
			travelSuppliesTransfer = null;
			try
			{
				CreateOriginal(
					transferables,
					out pawnsTransfer,
					out itemsTransfer,
					out travelSuppliesTransfer,
					thingCountTip,
					ignorePawnInventoryMass,
					availableMassGetter,
					ignoreSpawnedCorpsesGearAndInventoryMass,
					tile,
					playerPawnsReadOnly);
			}
			catch (Exception ex)
			{
				Log.Error("[DreamsOutposts] Failed to call the original caravan transferable widget factory: " + ex);
				return false;
			}
			if (pawnsTransfer == null || itemsTransfer == null || travelSuppliesTransfer == null)
			{
				Log.Error("[DreamsOutposts] The original caravan transferable widget factory returned a null widget.");
				return false;
			}
			return true;
		}

		[HarmonyReversePatch(HarmonyReversePatchType.Original)]
		[HarmonyPatch(typeof(CaravanUIUtility), nameof(CaravanUIUtility.CreateCaravanTransferableWidgets))]
		private static void CreateOriginal(
			List<TransferableOneWay> transferables,
			out TransferableOneWayWidget pawnsTransfer,
			out TransferableOneWayWidget itemsTransfer,
			out TransferableOneWayWidget travelSuppliesTransfer,
			string thingCountTip,
			IgnorePawnsInventoryMode ignorePawnInventoryMass,
			Func<float> availableMassGetter,
			bool ignoreSpawnedCorpsesGearAndInventoryMass,
			PlanetTile tile,
			bool playerPawnsReadOnly)
		{
			throw new NotImplementedException("Harmony reverse patch stub");
		}
	}
}
