using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 载具拓展（Vehicle Framework）组建远行队的反射入口。
	/// 本工程不引用 Vehicles.dll，所有类型与成员都按名字解析；对方缺席或改结构时只是不提供载具页。
	///
	/// 复用链路（全部是载具拓展自己的东西，本模组不重写界面）：
	///   1. <c>TransferableVehicleWidget</c> 是载具拓展的 4 列载具卡片，勾选时打开座位分配窗口；
	///   2. <c>Dialog_AssignSeats</c> 负责驾驶员/炮手/乘客的座位分配，结果写进全局静态 <c>CaravanHelper.assignedSeats</c>；
	///   3. 提交据点远行队时按 assignedSeats 登车，再交给 <c>CaravanMaker.MakeCaravan</c>
	///      ——载具拓展用前缀补丁拦截了它，只要列表里有 VehiclePawn 就会创建 VehicleCaravan。
	///
	/// 难点：上面两套界面都读全局静态 <c>CaravanFormation.Current</c>（= formation ?? splitter），
	/// 而这两个字段只在原版 Dialog_FormCaravan / Dialog_SplitCaravan 的组建流程里被赋值；
	/// 据点窗口没有原版组建窗口，直接调用会在勾选/确认时碰到空状态。
	/// 处理办法：窗口打开期间临时把一个动态生成的 ICaravanInfo 代理塞进 CaravanFormation.splitter，
	/// 关窗时还原。这样载具卡片与座位窗口都能原样复用，也不需要任何 Harmony 补丁。
	/// </summary>
	public static class VehicleCaravanCompat
	{
		private static bool resolved;

		private static bool available;

		private static Type vehiclePawnType;

		private static Type roleHandlerType;

		private static Type assignedSeatType;

		private static Type vehicleAssignmentType;

		private static Type caravanHelperType;

		private static Type splitInfoType;

		private static Type caravanInfoType;

		private static Type caravanFormationType;

		private static Type widgetType;

		private static Type vehicleDefType;

		private static FieldInfo widgetImpassableField;

		private static MethodInfo widgetImpassableRemove;

		private static PropertyInfo vehicleCompLauncherProperty;

		private static ConstructorInfo widgetCtor;

		private static MethodInfo widgetOnGui;

		private static FieldInfo splitterField;

		private static FieldInfo assignedSeatsField;

		private static PropertyInfo assignmentsProperty;

		private static MethodInfo assignmentGetAssignments;

		private static MethodInfo assignmentRemoveAssignments;

		private static FieldInfo seatHandlerField;

		private static FieldInfo handlerVehicleField;

		private static MethodInfo vehicleTryAddPawn;

		private static MethodInfo vehicleRemovePawn;

		private static PropertyInfo pawnCountToOperateProperty;

		private static PropertyInfo movementPermissionsProperty;

		private static FieldInfo movementPermissionsField;

		private static Type proxyType;

		private static FieldInfo proxyNotifyField;

		private static object installedProxy;

		/// <summary>载具拓展是否可用：mod 激活，且组建远行队所需的类型与成员都解析成功。</summary>
		public static bool Available
		{
			get
			{
				EnsureResolved();
				return available;
			}
		}

		public static bool IsVehicle(Pawn pawn)
		{
			if (pawn == null)
			{
				return false;
			}
			EnsureResolved();
			return vehiclePawnType != null && vehiclePawnType.IsInstanceOfType(pawn);
		}

		/// <summary>小人是否已经坐在载具座位里、或躺在载具货舱里。用于组建时排除已经登车的人。</summary>
		public static bool IsAboard(Pawn pawn)
		{
			EnsureResolved();
			IThingHolder holder = pawn?.ParentHolder;
			if (holder == null)
			{
				return false;
			}
			if (roleHandlerType != null && roleHandlerType.IsInstanceOfType(holder))
			{
				return true;
			}
			Pawn_InventoryTracker inventory = holder as Pawn_InventoryTracker;
			return inventory != null && IsVehicle(inventory.pawn);
		}

		/// <summary>把已经登车的小人从座位上/货舱里拿下来，并回报它乘坐的是哪辆载具。</summary>
		public static bool TryUnboard(Pawn pawn, out Pawn vehicle)
		{
			vehicle = null;
			if (pawn == null)
			{
				return false;
			}
			EnsureResolved();
			if (vehicleRemovePawn == null)
			{
				return false;
			}
			IThingHolder holder = pawn.ParentHolder;
			Pawn carrier = null;
			if (holder != null && roleHandlerType != null && roleHandlerType.IsInstanceOfType(holder))
			{
				carrier = handlerVehicleField.GetValue(holder) as Pawn;
			}
			else
			{
				Pawn_InventoryTracker inventory = holder as Pawn_InventoryTracker;
				if (inventory != null && IsVehicle(inventory.pawn))
				{
					carrier = inventory.pawn;
				}
			}
			if (carrier == null)
			{
				return false;
			}
			vehicle = carrier;
			object result = vehicleRemovePawn.Invoke(carrier, new object[] { pawn });
			return result is bool && (bool)result;
		}

		/// <summary>清掉这些载具上的座位分配，避免全局静态状态泄漏到远行队/下一次组建。</summary>
		public static void ClearAssignments(IEnumerable<Pawn> vehicles)
		{
			if (vehicles == null)
			{
				return;
			}
			EnsureResolved();
			if (assignedSeatsField == null || assignmentRemoveAssignments == null)
			{
				return;
			}
			object assignedSeats = assignedSeatsField.GetValue(null);
			if (assignedSeats == null)
			{
				return;
			}
			foreach (Pawn vehicle in vehicles)
			{
				if (IsVehicle(vehicle))
				{
					assignmentRemoveAssignments.Invoke(assignedSeats, new object[] { vehicle });
				}
			}
		}

		/// <summary>该载具当前分配了几名乘员（座位的临时分配，尚未登车）。</summary>
		public static int CountAssignedCrew(Pawn vehicle)
		{
			EnsureResolved();
			if (vehicle == null || assignedSeatsField == null || assignmentGetAssignments == null)
			{
				return 0;
			}
			object assignedSeats = assignedSeatsField.GetValue(null);
			IList list = assignedSeats == null ? null : assignmentGetAssignments.Invoke(assignedSeats, new object[] { vehicle }) as IList;
			return list == null ? 0 : list.Count;
		}

		/// <summary>无人载具（自动驾驶）不需要驾驶员。</summary>
		public static bool IsAutonomous(Pawn vehicle)
		{
			EnsureResolved();
			if (vehicle == null)
			{
				return false;
			}
			object value = null;
			if (movementPermissionsProperty != null)
			{
				value = movementPermissionsProperty.GetValue(vehicle, null);
			}
			else if (movementPermissionsField != null)
			{
				value = movementPermissionsField.GetValue(vehicle);
			}
			return value != null && value.ToString().IndexOf("Autonomous", StringComparison.Ordinal) >= 0;
		}

		/// <summary>该载具开动起来至少需要几名操作乘员。</summary>
		public static int PawnCountToOperate(Pawn vehicle)
		{
			EnsureResolved();
			if (vehicle == null || pawnCountToOperateProperty == null)
			{
				return 0;
			}
			object value = pawnCountToOperateProperty.GetValue(vehicle, null);
			return (value is int) ? (int)value : 0;
		}

		/// <summary>提交前校验：每辆选中的载具都要在座位窗口里分配够操作乘员。</summary>
		public static bool ValidateCrew(ICollection<Pawn> selectedVehicles, out string failReason)
		{
			failReason = null;
			if (selectedVehicles == null || selectedVehicles.Count == 0)
			{
				return true;
			}
			EnsureResolved();
			if (!available)
			{
				failReason = "DreamsOutposts.VehicleFrameworkUnavailable".Translate();
				return false;
			}
			foreach (Pawn vehicle in selectedVehicles)
			{
				if (IsAutonomous(vehicle))
				{
					continue;
				}
				int required = PawnCountToOperate(vehicle);
				int assigned = CountAssignedCrew(vehicle);
				if (assigned < required)
				{
					failReason = "DreamsOutposts.VehicleCrewMissing".Translate(vehicle.LabelShortCap, required, assigned);
					return false;
				}
			}
			return true;
		}

		/// <summary>
		/// 按 CaravanHelper.assignedSeats 里的座位分配把小人送上载具。
		/// 只有“已选中且分配在本据点载具上”的小人才会登车，成功登车的小人追加进 boarded 供失败回滚。
		/// </summary>
		public static bool TryBoardAssignedPawns(ICollection<Pawn> selectedPawns, ICollection<Pawn> selectedVehicles, List<Pawn> boarded, out string failReason)
		{
			failReason = null;
			EnsureResolved();
			if (selectedPawns == null || selectedVehicles == null || boarded == null)
			{
				return true;
			}
			if (assignedSeatsField == null || assignmentsProperty == null || seatHandlerField == null || vehicleTryAddPawn == null)
			{
				failReason = "DreamsOutposts.VehicleFrameworkUnavailable".Translate();
				return false;
			}
			object assignedSeats = assignedSeatsField.GetValue(null);
			IDictionary table = assignedSeats == null ? null : assignmentsProperty.GetValue(assignedSeats, null) as IDictionary;
			if (table == null)
			{
				return true;
			}
			foreach (DictionaryEntry entry in table)
			{
				Pawn pawn = entry.Key as Pawn;
				Pawn vehicle = GetSeatVehicle(entry.Value);
				if (pawn == null || vehicle == null || !selectedVehicles.Contains(vehicle) || !selectedPawns.Contains(pawn))
				{
					continue;
				}
				object handler = seatHandlerField.GetValue(entry.Value);
				bool added = false;
				try
				{
					object result = vehicleTryAddPawn.Invoke(vehicle, new object[] { pawn, handler });
					added = result is bool && (bool)result;
				}
				catch (Exception ex)
				{
					Log.Error("[DreamsOutposts] Failed to board " + pawn + " into vehicle " + vehicle + ": " + ex);
				}
				if (!added)
				{
					failReason = "DreamsOutposts.VehicleBoardingFailed".Translate(pawn.LabelShortCap, vehicle.LabelShortCap);
					return false;
				}
				boarded.Add(pawn);
			}
			return true;
		}

		/// <summary>用载具拓展的载具卡片组件建一个载具页控件；vehicles/pawns 必须都是本窗口自己的 transferable。</summary>
		public static object CreateVehicleWidget(string title, List<TransferableOneWay> vehicles, List<TransferableOneWay> pawns, PlanetTile tile)
		{
			EnsureResolved();
			if (widgetCtor == null)
			{
				return null;
			}
			try
			{
				return widgetCtor.Invoke(new object[] { title, vehicles, pawns, tile });
			}
			catch (Exception ex)
			{
				Log.Error("[DreamsOutposts] Failed to create the Vehicle Framework transferable widget: " + ex);
				return null;
			}
		}

		public static void DrawVehicleWidget(object widget, Rect inRect)
		{
			if (widget == null || widgetOnGui == null)
			{
				return;
			}
			try
			{
				widgetOnGui.Invoke(widget, new object[] { inRect });
			}
			catch (Exception ex)
			{
				Log.Error("[DreamsOutposts] Failed to draw the Vehicle Framework transferable widget: " + ex);
			}
		}

		/// <summary>
		/// 载具卡片构造时会把「本据点地块对该型号不可通行」的型号记进 impassableOnTile，勾选框直接红叉
		/// （提示 VF_ImpassableBiome），而这一项在 AllowSelectionOfAllVehicles 之前判断，绕不过去。
		/// 但据点里的载具是停放状态：对能起飞的载具（带 CompVehicleLauncher）来说，本地块通不通并不影响它出发，
		/// 所以这里只把这些型号从拦截集合里摘掉，地面/水上载具仍保留载体拓展原本的限制。
		/// </summary>
		public static void AllowLaunchableVehiclesOnTile(object widget, List<TransferableOneWay> vehicleTransferables)
		{
			if (widget == null || vehicleTransferables == null || widgetImpassableField == null
				|| widgetImpassableRemove == null || vehicleCompLauncherProperty == null)
			{
				return;
			}
			object impassable;
			try
			{
				impassable = widgetImpassableField.GetValue(widget);
			}
			catch (Exception ex)
			{
				Log.Error("[DreamsOutposts] Failed to read the vehicle widget passability filter: " + ex);
				return;
			}
			if (impassable == null)
			{
				return;
			}
			for (int i = 0; i < vehicleTransferables.Count; i++)
			{
				Pawn vehicle = vehicleTransferables[i].AnyThing as Pawn;
				if (!IsVehicle(vehicle))
				{
					continue;
				}
				bool launchable;
				try
				{
					launchable = vehicleCompLauncherProperty.GetValue(vehicle, null) != null;
				}
				catch (Exception ex)
				{
					Log.Error("[DreamsOutposts] Failed to check whether " + vehicle + " can launch: " + ex);
					continue;
				}
				if (!launchable)
				{
					continue;
				}
				try
				{
					widgetImpassableRemove.Invoke(impassable, new object[] { vehicle.def });
				}
				catch (Exception ex)
				{
					Log.Error("[DreamsOutposts] Failed to allow launchable vehicle " + vehicle + " on the outpost tile: " + ex);
				}
			}
		}

		/// <summary>
		/// 窗口打开期间安装 ICaravanInfo 代理，让载具拓展的卡片组件与座位窗口拿到非空状态。
		/// 原版组建/分队窗口正在进行（splitter 非空）时绝不抢占，返回 false，调用方应隐藏载具页。
		/// </summary>
		public static bool TryBeginContext(Action onTransferablesChanged)
		{
			if (!Available || installedProxy != null)
			{
				return false;
			}
			try
			{
				object existing = splitterField.GetValue(null);
				if (existing != null)
				{
					Log.Error("[DreamsOutposts] Another caravan dialog already owns CaravanFormation state; the outpost window will not offer vehicle seats this time.");
					return false;
				}
				if (proxyType == null)
				{
					proxyType = BuildProxyType();
				}
				if (proxyType == null)
				{
					return false;
				}
				// 基类构造器只把入参当作委托绑定目标，不会调用它们，所以用一个未初始化的占位对象即可。
				object placeholderDialog = FormatterServices.GetUninitializedObject(typeof(Dialog_SplitCaravan));
				object proxy = Activator.CreateInstance(proxyType, new object[] { placeholderDialog, null });
				if (proxy == null)
				{
					return false;
				}
				proxyNotifyField.SetValue(proxy, onTransferablesChanged);
				splitterField.SetValue(null, proxy);
				installedProxy = proxy;
				return true;
			}
			catch (Exception ex)
			{
				Log.Error("[DreamsOutposts] Failed to install the Vehicle Framework caravan info proxy: " + ex);
				proxyType = null;
				installedProxy = null;
				return false;
			}
		}

		public static void EndContext()
		{
			if (installedProxy == null)
			{
				return;
			}
			try
			{
				if (splitterField != null && ReferenceEquals(splitterField.GetValue(null), installedProxy))
				{
					splitterField.SetValue(null, null);
				}
			}
			catch (Exception ex)
			{
				Log.Error("[DreamsOutposts] Failed to restore CaravanFormation state: " + ex);
			}
			installedProxy = null;
		}

		private static Pawn GetSeatVehicle(object assignedSeat)
		{
			if (assignedSeat == null || seatHandlerField == null || handlerVehicleField == null)
			{
				return null;
			}
			object handler = seatHandlerField.GetValue(assignedSeat);
			return handler == null ? null : handlerVehicleField.GetValue(handler) as Pawn;
		}

		private static void EnsureResolved()
		{
			if (resolved)
			{
				return;
			}
			resolved = true;
			if (!VehicleFrameworkCompatibility.Enabled)
			{
				return;
			}
			try
			{
				vehiclePawnType = AccessTools.TypeByName("Vehicles.VehiclePawn");
				roleHandlerType = AccessTools.TypeByName("Vehicles.VehicleRoleHandler");
				assignedSeatType = AccessTools.TypeByName("Vehicles.AssignedSeat");
				vehicleAssignmentType = AccessTools.TypeByName("Vehicles.VehicleAssignment");
				caravanHelperType = AccessTools.TypeByName("Vehicles.CaravanHelper");
				splitInfoType = AccessTools.TypeByName("Vehicles.World.SplitInfo");
				caravanInfoType = AccessTools.TypeByName("Vehicles.World.ICaravanInfo");
				caravanFormationType = AccessTools.TypeByName("Vehicles.World.CaravanFormation");
				widgetType = AccessTools.TypeByName("Vehicles.World.TransferableVehicleWidget");
				vehicleDefType = AccessTools.TypeByName("Vehicles.VehicleDef");

				widgetCtor = FindVehicleWidgetCtor();
				widgetOnGui = (widgetType == null) ? null : AccessTools.Method(widgetType, "OnGUI", new[] { typeof(Rect) });
				widgetImpassableField = FindField(widgetType, "impassableOnTile");
				widgetImpassableRemove = (widgetImpassableField == null || vehicleDefType == null)
					? null
					: widgetImpassableField.FieldType.GetMethod("Remove", new[] { vehicleDefType });
				vehicleCompLauncherProperty = FindProperty(vehiclePawnType, "CompVehicleLauncher");
				splitterField = (caravanFormationType == null) ? null : AccessTools.Field(caravanFormationType, "splitter");
				assignedSeatsField = (caravanHelperType == null) ? null : AccessTools.Field(caravanHelperType, "assignedSeats");
				assignmentsProperty = FindProperty(vehicleAssignmentType, "AllAssignments");
				assignmentGetAssignments = FindMethod(vehicleAssignmentType, "GetAssignments", new[] { vehiclePawnType });
				assignmentRemoveAssignments = FindMethod(vehicleAssignmentType, "RemoveAssignments", new[] { vehiclePawnType });
				seatHandlerField = FindField(assignedSeatType, "handler");
				handlerVehicleField = FindField(roleHandlerType, "vehicle");
				vehicleTryAddPawn = FindMethod(vehiclePawnType, "TryAddPawn", new[] { typeof(Pawn), roleHandlerType });
				vehicleRemovePawn = FindMethod(vehiclePawnType, "RemovePawn", new[] { typeof(Pawn) });
				pawnCountToOperateProperty = FindProperty(vehiclePawnType, "PawnCountToOperate");
				movementPermissionsProperty = FindProperty(vehiclePawnType, "MovementPermissions");
				movementPermissionsField = (movementPermissionsProperty == null) ? FindField(vehiclePawnType, "MovementPermissions") : null;
			}
			catch (Exception ex)
			{
				Log.Error("[DreamsOutposts] Failed to resolve the Vehicle Framework caravan API: " + ex);
			}
			available = vehiclePawnType != null && roleHandlerType != null && assignedSeatType != null
				&& vehicleAssignmentType != null && caravanHelperType != null && splitInfoType != null
				&& caravanInfoType != null && caravanFormationType != null && widgetType != null
				&& widgetCtor != null && widgetOnGui != null && splitterField != null && assignedSeatsField != null
				&& assignmentsProperty != null && assignmentGetAssignments != null && assignmentRemoveAssignments != null
				&& seatHandlerField != null && handlerVehicleField != null && vehicleTryAddPawn != null
				&& vehicleRemovePawn != null && pawnCountToOperateProperty != null;
			if (!available)
			{
				Log.Error("[DreamsOutposts] Vehicle Framework is active but its caravan API could not be resolved; outpost caravans will keep vehicles but the window will not offer vehicle seats.");
			}
		}

		private static ConstructorInfo FindVehicleWidgetCtor()
		{
			if (widgetType == null)
			{
				return null;
			}
			ConstructorInfo[] ctors = widgetType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
			for (int i = 0; i < ctors.Length; i++)
			{
				ParameterInfo[] parameters = ctors[i].GetParameters();
				if (parameters.Length == 4 && parameters[0].ParameterType == typeof(string)
					&& parameters[1].ParameterType == typeof(List<TransferableOneWay>)
					&& parameters[2].ParameterType == typeof(List<TransferableOneWay>)
					&& parameters[3].ParameterType == typeof(PlanetTile))
				{
					return ctors[i];
				}
			}
			return null;
		}

		/// <summary>
		/// 用 Reflection.Emit 生成一个 SplitInfo 子类，重新实现 ICaravanInfo：
		/// AllowSelectionOfAllVehicles 固定 true——它唯一的用处是让载具卡片跳过 VehicleDef.canCaravan 这一层过滤，
		/// 飞机（VehicleType.Air）的 def 是 canCaravan=false，不跳过的话卡片的勾选框是灰的（提示 Caravaning disabled），
		/// 飞机就永远没法从据点被编入远行队；据点里的载具本来就是停放的，该不该选交给玩家。
		/// NotifyTransferablesChanged 转发到据点窗口的缓存刷新回调。
		/// </summary>
		private static Type BuildProxyType()
		{
			ConstructorInfo baseCtor = splitInfoType.GetConstructor(BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(Dialog_SplitCaravan), typeof(Caravan) }, null);
			MethodInfo allowGetter = caravanInfoType.GetMethod("get_AllowSelectionOfAllVehicles", BindingFlags.Public | BindingFlags.Instance);
			MethodInfo notifyMethod = caravanInfoType.GetMethod("NotifyTransferablesChanged", BindingFlags.Public | BindingFlags.Instance);
			if (baseCtor == null || allowGetter == null || notifyMethod == null)
			{
				Log.Error("[DreamsOutposts] Vehicle Framework ICaravanInfo members changed shape; cannot build the outpost caravan info proxy.");
				return null;
			}
			AssemblyBuilder assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName("DreamsOutposts.VehicleCaravanProxy"), AssemblyBuilderAccess.Run);
			ModuleBuilder module = assembly.DefineDynamicModule("DreamsOutposts.VehicleCaravanProxy");
			TypeBuilder builder = module.DefineType("OutpostCaravanInfoProxy", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class, splitInfoType, new[] { caravanInfoType });
			FieldBuilder notifyField = builder.DefineField("notify", typeof(Action), FieldAttributes.Public);

			ConstructorBuilder ctor = builder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { typeof(Dialog_SplitCaravan), typeof(Caravan) });
			ILGenerator ctorIl = ctor.GetILGenerator();
			ctorIl.Emit(OpCodes.Ldarg_0);
			ctorIl.Emit(OpCodes.Ldarg_1);
			ctorIl.Emit(OpCodes.Ldarg_2);
			ctorIl.Emit(OpCodes.Call, baseCtor);
			ctorIl.Emit(OpCodes.Ret);

			MethodBuilder getAllow = builder.DefineMethod("get_AllowSelectionOfAllVehicles", MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot | MethodAttributes.HideBySig, typeof(bool), Type.EmptyTypes);
			ILGenerator allowIl = getAllow.GetILGenerator();
			allowIl.Emit(OpCodes.Ldc_I4_1);
			allowIl.Emit(OpCodes.Ret);
			builder.DefineMethodOverride(getAllow, allowGetter);

			MethodBuilder notify = builder.DefineMethod("NotifyTransferablesChanged", MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot | MethodAttributes.HideBySig, typeof(void), Type.EmptyTypes);
			ILGenerator notifyIl = notify.GetILGenerator();
			Label skip = notifyIl.DefineLabel();
			notifyIl.Emit(OpCodes.Ldarg_0);
			notifyIl.Emit(OpCodes.Ldfld, notifyField);
			notifyIl.Emit(OpCodes.Brfalse, skip);
			notifyIl.Emit(OpCodes.Ldarg_0);
			notifyIl.Emit(OpCodes.Ldfld, notifyField);
			notifyIl.Emit(OpCodes.Callvirt, typeof(Action).GetMethod("Invoke", Type.EmptyTypes));
			notifyIl.MarkLabel(skip);
			notifyIl.Emit(OpCodes.Ret);
			builder.DefineMethodOverride(notify, notifyMethod);

			Type created = builder.CreateType();
			// FieldBuilder 不支持 SetValue（动态模块），必须换成生成类型上的真实 FieldInfo。
			proxyNotifyField = created.GetField("notify", BindingFlags.Public | BindingFlags.Instance);
			if (proxyNotifyField == null)
			{
				Log.Error("[DreamsOutposts] Could not read the notify field of the generated caravan info proxy.");
				return null;
			}
			return created;
		}

		private static MethodInfo FindMethod(Type type, string name, Type[] parameters)
		{
			return (type == null) ? null : AccessTools.Method(type, name, parameters);
		}

		private static PropertyInfo FindProperty(Type type, string name)
		{
			return (type == null) ? null : AccessTools.Property(type, name);
		}

		private static FieldInfo FindField(Type type, string name)
		{
			return (type == null) ? null : AccessTools.Field(type, name);
		}
	}
}
