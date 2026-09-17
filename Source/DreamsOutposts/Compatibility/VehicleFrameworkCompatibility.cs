using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld.Planet;
using Verse;

namespace DreamsOutposts
{
	public sealed class VehicleFrameworkCompatibility : IOutpostCompatibility
	{
		public string PackageId => "SmashPhil.VehicleFramework";

		public static bool Enabled { get; private set; }

		public void Apply(Harmony harmony)
		{
			Enabled = true;
			// 让飞机能选中据点并「进入据点」（沿用载具拓展自己的到达项与落地流程）。
			VehicleAerialArrivalCompat.Apply(harmony);
		}

		public static void MoveCaravanPawnsToOutpost(Caravan caravan, Outpost outpost)
		{
			// Vehicle Framework patches this list to include passengers held by vehicle seats.
			List<Pawn> members = caravan.PawnsListForReading.ToList();
			for (int i = 0; i < members.Count; i++)
			{
				Pawn pawn = members[i];
				if (pawn != null && !IsVehicle(pawn) && IsVehicleSeat(pawn.holdingOwner)) MoveMember(caravan, outpost, pawn);
			}
			for (int i = 0; i < members.Count; i++)
			{
				Pawn pawn = members[i];
				if (pawn != null && !IsVehicle(pawn) && !IsVehicleSeat(pawn.holdingOwner) && !outpost.pawns.Contains(pawn))
					MoveMember(caravan, outpost, pawn);
			}
			for (int i = 0; i < members.Count; i++)
				if (IsVehicle(members[i])) MoveMember(caravan, outpost, members[i]);
		}

		private static bool IsVehicle(Pawn pawn)
		{
			for (Type type = pawn?.GetType(); type != null; type = type.BaseType)
				if (type.FullName == "Vehicles.VehiclePawn") return true;
			return false;
		}

		private static bool IsVehicleSeat(ThingOwner owner)
		{
			return owner?.Owner?.GetType().FullName == "Vehicles.VehicleRoleHandler";
		}

		private static void MoveMember(Caravan caravan, Outpost outpost, Pawn pawn)
		{
			ThingOwner source = pawn.holdingOwner;
			if (source == null) return;
			bool vehicle = IsVehicle(pawn);
			ThingOwner<Pawn> target = vehicle ? outpost.vehicles : outpost.pawns;
			if (target == null) return;
			object holder = source.Owner;
			if (vehicle && holder == caravan)
			{
				// Put the vehicle safely in the outpost before removing the caravan's last vehicle.
				if (!source.TryTransferToContainer(pawn, target, true))
				{
					Log.Error("[DreamsOutposts] Failed to store vehicle " + pawn + " in outpost " + outpost.Label + ".");
					return;
				}
				caravan.Notify_PawnRemoved(pawn);
				OutpostUtility.TakeOutOfWorld(pawn);
				outpost.RequestUpdate();
				return;
			}
			if (IsVehicleSeat(source))
			{
				object parent = holder.GetType().GetField("vehicle", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(holder);
				MethodInfo remove = parent?.GetType().GetMethod("RemovePawn", new[] { typeof(Pawn) });
				if (remove == null || !(bool)remove.Invoke(parent, new object[] { pawn })) return;
			}
			else if (holder == caravan) caravan.RemovePawn(pawn);
			else if (!source.Remove(pawn)) return;
			if (!target.TryAdd(pawn))
			{
				Log.Error("[DreamsOutposts] Failed to move " + pawn + " from caravan " + caravan.Label + " into outpost " + outpost.Label + ".");
				if (!source.TryAdd(pawn)) Log.Error("[DreamsOutposts] Failed to return " + pawn + " to its original container.");
				return;
			}
			OutpostUtility.TakeOutOfWorld(pawn);
			if (!vehicle) OutpostUtility.MovePawnInventoryIntoOutpost(outpost, pawn);
			outpost.RequestUpdate();
		}
	}
}
