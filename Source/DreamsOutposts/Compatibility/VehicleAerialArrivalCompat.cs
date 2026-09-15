using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 让载具拓展的飞机（带发射协议的载具）能像友好派系聚落那样选中本模组的据点，并直接「进入据点」。
	///
	/// 载具拓展的目标选择链路：
	///   SmashTools 的 WorldTargeter → CompVehicleLauncher.CanTarget/Select → LaunchProtocol.GetArrivalOptions。
	/// 原版那一份只认识 MapParent 世界对象（有地图的目标、派系聚落），非 MapParent 的世界对象
	/// （本模组的据点就是这种）会掉进「地块可通行 → 在此组建远行队」这一支：
	/// 飞机飞过去只会变成一支停在据点地块上的远行队，而且地块不可通行时连这一项都没有，表现为「无法选中」。
	///
	/// 这里不重写载具拓展的目标选择器，只沿它自己的路子补两步：
	///   1. 后置补丁 LaunchProtocol.GetArrivalOptions：目标是玩家据点时，把「在此组建远行队」这一项改名为
	///      「进入据点」（复用同一个 ArrivalAction_LandToCaravan 实例，飞行与落地行为与载具拓展完全一致）；
	///      如果本来就没有这一项（地块对这台载具不可通行），则按同样的方式补一项。
	///   2. 后置补丁 ArrivalAction_LandToCaravan.Arrived：飞机落地变成远行队之后，如果落地地块上是玩家据点，
	///      就调用据点自己的 OutpostCaravanUtility.TryEnterOutpost，把载具、乘员与货物一起收进据点。
	///
	/// 补丁方法本身是普通 C#：载具拓展的类型只出现在 object 形式的 __result / __instance 上
	/// （Harmony 允许 __result 用可赋值给返回值的基类型），既不需要引用 Vehicles.dll，也不需要 Emit。
	/// </summary>
	public static class VehicleAerialArrivalCompat
	{
		private static bool resolved;

		private static bool available;

		private static Type launchProtocolType;

		private static Type arrivalOptionType;

		private static Type arrivalActionInterfaceType;

		private static Type landToCaravanType;

		private static Type vehicleArrivalActionType;

		private static Type vehiclePawnType;

		private static ConstructorInfo arrivalOptionCtor;

		private static ConstructorInfo landToCaravanCtor;

		private static FieldInfo arrivalActionField;

		private static PropertyInfo protocolVehicleProperty;

		private static FieldInfo arrivalActionVehicleField;

		private static Type arrivalOptionListType;

		private static bool forcedArrivalLogged;

		/// <summary>给载具拓展的飞机挂上据点到达项与到达处理。只能在载具拓展激活时调用。</summary>
		public static void Apply(Harmony harmony)
		{
			if (harmony == null || !EnsureResolved())
			{
				return;
			}
			try
			{
				MethodInfo optionsGetter = AccessTools.Method(launchProtocolType, "GetArrivalOptions", new[] { typeof(GlobalTargetInfo) });
				MethodInfo arrivedMethod = AccessTools.Method(landToCaravanType, "Arrived", new[] { typeof(GlobalTargetInfo) });
				if (optionsGetter == null || arrivedMethod == null)
				{
					Log.Error("[DreamsOutposts] Vehicle Framework arrival API changed shape; aircraft will not be able to enter outposts.");
					return;
				}
				harmony.Patch(optionsGetter, postfix: new HarmonyMethod(typeof(VehicleAerialArrivalCompat), "GetArrivalOptionsPostfix"));
				harmony.Patch(arrivedMethod, postfix: new HarmonyMethod(typeof(VehicleAerialArrivalCompat), "LandToCaravanArrivedPostfix"));
			}
			catch (Exception ex)
			{
				Log.Error("[DreamsOutposts] Failed to patch the Vehicle Framework arrival options: " + ex);
			}
		}

		/// <summary>
		/// 载具拓展枚举到达项的后置补丁：目标是玩家据点时，把「在此组建远行队」换成「进入据点」。
		/// __result 是 IEnumerable&lt;ArrivalOption&gt;，这里用 object 接住再整体换成一个 List&lt;ArrivalOption&gt;。
		/// </summary>
		private static void GetArrivalOptionsPostfix(object __instance, GlobalTargetInfo target, ref object __result)
		{
			try
			{
				if (!available)
				{
					return;
				}
				Outpost outpost = target.WorldObject as Outpost;
				if (outpost == null || outpost.Destroyed || !outpost.Spawned || outpost.Faction != Faction.OfPlayer)
				{
					return;
				}
				IEnumerable original = __result as IEnumerable;
				if (original == null)
				{
					return;
				}
				// 载具拓展返回的是惰性迭代器，只能枚举一次，所以先整体取出，再决定要不要改标签。
				bool replaced = false;
				List<object> rebuilt = new List<object>();
				foreach (object option in original)
				{
					if (option == null)
					{
						continue;
					}
					object arrivalAction = arrivalActionField.GetValue(option);
					if (arrivalAction != null && landToCaravanType.IsInstanceOfType(arrivalAction))
					{
						rebuilt.Add(CreateOutpostOption(outpost, arrivalAction));
						replaced = true;
					}
					else
					{
						rebuilt.Add(option);
					}
				}
				if (!replaced)
				{
					// 地块对这台载具不可通行时载具拓展不会给出任何项，这里补一项，让飞机能回自己的据点。
					Pawn vehicle = GetProtocolVehicle(__instance);
					if (vehicle != null)
					{
						if (!forcedArrivalLogged)
						{
							forcedArrivalLogged = true;
							Log.Warning("[DreamsOutposts] Vehicle Framework offered no arrival option for outpost " + outpost.Label + " (its tile is likely impassable for this vehicle); added a forced enter-outpost option.");
						}
						rebuilt.Add(CreateOutpostOption(outpost, landToCaravanCtor.Invoke(new object[] { vehicle })));
					}
					else
					{
						Log.ErrorOnce("[DreamsOutposts] Could not read the vehicle from the launch protocol, so the outpost arrival option could not be added.", 8347112);
					}
				}
				__result = ToArrivalOptionList(rebuilt);
			}
			catch (Exception ex)
			{
				Log.Error("[DreamsOutposts] Failed to add the outpost arrival option: " + ex);
			}
		}

		/// <summary>
		/// 「在此组建远行队」落地处理的后置补丁：落地地块上是玩家据点时，直接把整支部队收进据点。
		/// 只靠载具自己找落点地块上的据点，不依赖 Arrived 的参数名，载具拓展改名也不会失效。
		/// </summary>
		private static void LandToCaravanArrivedPostfix(object __instance)
		{
			try
			{
				if (!available || __instance == null)
				{
					return;
				}
				Pawn vehicle = arrivalActionVehicleField.GetValue(__instance) as Pawn;
				if (vehicle == null || !VehicleCaravanCompat.IsVehicle(vehicle))
				{
					return;
				}
				Caravan caravan = vehicle.GetCaravan();
				if (caravan == null || caravan.Destroyed)
				{
					return;
				}
				Outpost outpost = FindPlayerOutpostAt(caravan.Tile);
				if (outpost == null)
				{
					return;
				}
				if (OutpostCaravanUtility.TryEnterOutpost(caravan, outpost))
				{
					Messages.Message("DreamsOutposts.AerialEnteredOutpost".Translate(outpost.LabelCap), outpost, MessageTypeDefOf.TaskCompletion, historical: false);
				}
			}
			catch (Exception ex)
			{
				Log.Error("[DreamsOutposts] Failed to enter the outpost after an aircraft arrived: " + ex);
			}
		}

		private static object CreateOutpostOption(Outpost outpost, object arrivalAction)
		{
			TaggedString label = "DreamsOutposts.AerialEnterOutpost".Translate(outpost.LabelCap);
			return arrivalOptionCtor.Invoke(new object[] { label, arrivalAction });
		}

		private static Pawn GetProtocolVehicle(object launchProtocol)
		{
			if (launchProtocol == null || protocolVehicleProperty == null)
			{
				return null;
			}
			return protocolVehicleProperty.GetValue(launchProtocol, null) as Pawn;
		}

		private static object ToArrivalOptionList(List<object> options)
		{
			IList list = (IList)Activator.CreateInstance(arrivalOptionListType);
			for (int i = 0; i < options.Count; i++)
			{
				list.Add(options[i]);
			}
			return list;
		}

		private static Outpost FindPlayerOutpostAt(PlanetTile tile)
		{
			if (!tile.Valid)
			{
				return null;
			}
			foreach (WorldObject worldObject in Find.WorldObjects.ObjectsAt(tile))
			{
				Outpost outpost = worldObject as Outpost;
				if (outpost != null && outpost.Spawned && !outpost.Destroyed && outpost.Faction == Faction.OfPlayer)
				{
					return outpost;
				}
			}
			return null;
		}

		private static bool EnsureResolved()
		{
			if (resolved)
			{
				return available;
			}
			resolved = true;
			try
			{
				launchProtocolType = AccessTools.TypeByName("Vehicles.LaunchProtocol");
				arrivalOptionType = AccessTools.TypeByName("Vehicles.World.ArrivalOption");
				arrivalActionInterfaceType = AccessTools.TypeByName("Vehicles.World.IArrivalAction");
				landToCaravanType = AccessTools.TypeByName("Vehicles.World.ArrivalAction_LandToCaravan");
				vehicleArrivalActionType = AccessTools.TypeByName("Vehicles.World.VehicleArrivalAction");
				vehiclePawnType = AccessTools.TypeByName("Vehicles.VehiclePawn");

				arrivalOptionCtor = (arrivalOptionType == null || arrivalActionInterfaceType == null)
					? null
					: AccessTools.Constructor(arrivalOptionType, new[] { typeof(TaggedString), arrivalActionInterfaceType });
				// 构造函数参数类型是 Vehicles.VehiclePawn（不是 Pawn），必须按形状找，不能按精确类型找。
				landToCaravanCtor = FindVehicleCtor(landToCaravanType, vehiclePawnType);
				arrivalActionField = (arrivalOptionType == null) ? null : AccessTools.Field(arrivalOptionType, "arrivalAction");
				protocolVehicleProperty = (launchProtocolType == null) ? null : AccessTools.Property(launchProtocolType, "Vehicle");
				arrivalActionVehicleField = (vehicleArrivalActionType == null) ? null : AccessTools.Field(vehicleArrivalActionType, "vehicle");
				arrivalOptionListType = (arrivalOptionType == null) ? null : typeof(List<>).MakeGenericType(arrivalOptionType);
			}
			catch (Exception ex)
			{
				Log.Error("[DreamsOutposts] Failed to resolve the Vehicle Framework arrival API: " + ex);
			}
			available = launchProtocolType != null && arrivalOptionType != null && arrivalActionInterfaceType != null
				&& landToCaravanType != null && vehicleArrivalActionType != null && vehiclePawnType != null
				&& arrivalOptionCtor != null && landToCaravanCtor != null && arrivalActionField != null
				&& protocolVehicleProperty != null && arrivalActionVehicleField != null && arrivalOptionListType != null;
			if (!available)
			{
				Log.Error("[DreamsOutposts] Vehicle Framework is active but its aircraft arrival API could not be resolved; missing: " + MissingParts() + ". Aircraft will not be able to fly into outposts.");
			}
			return available;
		}

		/// <summary>载具拓展的这些构造函数参数类型是 Vehicles.VehiclePawn 之类的子类，按形状取而不是按精确类型取。</summary>
		private static ConstructorInfo FindVehicleCtor(Type type, Type vehicleType)
		{
			if (type == null || vehicleType == null)
			{
				return null;
			}
			ConstructorInfo[] ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
			for (int i = 0; i < ctors.Length; i++)
			{
				ParameterInfo[] parameters = ctors[i].GetParameters();
				if (parameters.Length == 1 && parameters[0].ParameterType.IsAssignableFrom(vehicleType))
				{
					return ctors[i];
				}
			}
			return null;
		}

		private static string MissingParts()
		{
			List<string> missing = new List<string>();
			if (launchProtocolType == null) missing.Add("LaunchProtocol");
			if (arrivalOptionType == null) missing.Add("ArrivalOption");
			if (arrivalActionInterfaceType == null) missing.Add("IArrivalAction");
			if (landToCaravanType == null) missing.Add("ArrivalAction_LandToCaravan");
			if (vehicleArrivalActionType == null) missing.Add("VehicleArrivalAction");
			if (vehiclePawnType == null) missing.Add("VehiclePawn");
			if (arrivalOptionCtor == null) missing.Add("ArrivalOption(TaggedString, IArrivalAction)");
			if (landToCaravanCtor == null) missing.Add("ArrivalAction_LandToCaravan(vehicle)");
			if (arrivalActionField == null) missing.Add("ArrivalOption.arrivalAction");
			if (protocolVehicleProperty == null) missing.Add("LaunchProtocol.Vehicle");
			if (arrivalActionVehicleField == null) missing.Add("VehicleArrivalAction.vehicle");
			return string.Join(", ", missing.ToArray());
		}
	}
}
