using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 据点交换窗口只需要原版交易界面的通用物资信息和带符号数量调节器。
	/// 某些界面 Mod 会全局替换这些公共方法，并假定调用者是原版 Dialog_Trade；
	/// Reverse Patch 直接调用当前 RimWorld 的未补丁方法体，避免把那些窗口专用假设带进本窗口。
	/// </summary>
	[HarmonyPatch]
	internal static class ExchangeTransferableUiOriginal
	{
		public static void DrawCountAdjust(Rect rect, Transferable transferable, int index, int min, int max)
		{
			DrawCountAdjustOriginal(rect, transferable, index, min, max, false, null, false);
		}

		public static void DrawTransferableInfo(Transferable transferable, Rect rect)
		{
			DrawTransferableInfoOriginal(transferable, rect, Color.white);
		}

		[HarmonyReversePatch(HarmonyReversePatchType.Original)]
		[HarmonyPatch(typeof(TransferableUIUtility), nameof(TransferableUIUtility.DoCountAdjustInterface))]
		private static void DrawCountAdjustOriginal(
			Rect rect,
			Transferable transferable,
			int index,
			int min,
			int max,
			bool flash,
			List<TransferableCountToTransferStoppingPoint> extraStoppingPoints,
			bool readOnly)
		{
			throw new NotImplementedException("Harmony reverse patch stub");
		}

		[HarmonyReversePatch(HarmonyReversePatchType.Original)]
		[HarmonyPatch(typeof(TransferableUIUtility), nameof(TransferableUIUtility.DrawTransferableInfo))]
		private static void DrawTransferableInfoOriginal(Transferable transferable, Rect rect, Color labelColor)
		{
			throw new NotImplementedException("Harmony reverse patch stub");
		}
	}
}
