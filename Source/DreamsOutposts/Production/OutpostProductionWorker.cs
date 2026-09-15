using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public class OutpostProductionWorker
	{
		/// <summary>已提醒过的 worker 类型，避免开发模式里每帧重复检查。</summary>
		private static readonly HashSet<Type> statefulWorkerWarned = new HashSet<Type>();

		public virtual bool UsesDynamicProduct => false;

		/// <summary>
		/// 这条生产的产量是否随人数/产能变化。默认：写了 capacityStat 就是。
		/// 自己算产能的 worker（例如边缘仙路按修仙境界算的效率）覆盖为 true，
		/// 框架才会把产能算出来交给 DescribeCapacity 显示。
		/// </summary>
		public virtual bool UsesPersonnelCapacity(OutpostProductionProperties production)
		{
			return production?.capacityStat != null;
		}

		/// <summary>
		/// 产能那一行的显示文本；返回 null 表示这条生产不显示产能。
		/// 默认分三种：有 capacityStat 按 StatDef 自己的格式显示（PercentZero → "120%"）；
		/// 没有 capacityStat 但 worker 自己算产能，按普通数字显示；两者都不是就显示「固定产能 1」。
		/// 数字说不清楚的 worker 可以覆盖它，自己写一句玩家看得懂的话。
		/// </summary>
		public virtual string DescribeCapacity(Outpost outpost, OutpostProductionProperties production, float capacity)
		{
			if (production?.capacityStat != null)
			{
				return "DreamsOutposts.Ui.Rule.Capacity".Translate(production.capacityStat.LabelCap, production.capacityStat.ValueToString(capacity)).ToString();
			}
			if (UsesPersonnelCapacity(production))
			{
				return "DreamsOutposts.Ui.Rule.Efficiency".Translate(capacity.ToString("0.##")).ToString();
			}
			return "DreamsOutposts.Ui.Rule.FixedCapacity".Translate().ToString();
		}

		public virtual Type StateClass => typeof(OutpostProductionState);

		/// <summary>
		/// 产能（人数/效率）。带 Outpost 的重载是框架内部唯一的调用入口：
		/// 需要据点等级、地块、在场设施这类上下文的 worker 覆盖这一个。
		/// </summary>
		public virtual float CalculatePersonnelCapacity(Outpost outpost, OutpostProductionProperties production)
		{
			return CalculatePersonnelCapacity(outpost?.Pawns, outpost?.outpostTypeDef, production);
		}

		/// <summary>
		/// 保留给拿不到 Outpost 的调用方；框架内部一律走带 Outpost 的重载，
		/// 只在只关心人员时才有必要覆盖它（否则覆盖带 Outpost 的那个）。
		/// </summary>
		public virtual float CalculatePersonnelCapacity(IEnumerable<Pawn> pawns, OutpostTypeDef outpostTypeDef, OutpostProductionProperties production)
		{
			if (pawns == null)
			{
				throw new ArgumentNullException("pawns");
			}
			if (production == null)
			{
				throw new ArgumentNullException("production");
			}
			// 没写 capacityStat 表示这个设施不依赖任何技能属性：产能固定为 1，
			// 于是每周期产量就是 outputPerCapacity 本身，与人数、技能都无关。
			if (production.capacityStat == null)
			{
				return 1f;
			}
			float capacity = 0f;
			foreach (Pawn pawn in pawns)
			{
				if (pawn != null && OutpostStatUtility.CanSafelyReadStat(production.capacityStat, pawn) && production.PawnMeetsSkillRequirement(pawn))
				{
					capacity += pawn.GetStatValue(production.capacityStat);
				}
			}
			return capacity;
		}

		/// <summary>带 Outpost 的产出计算；框架内部唯一的调用入口。</summary>
		public virtual float CalculateOutput(float personnelCapacity, Outpost outpost, OutpostProductionProperties production, OutpostProductionState state)
		{
			return CalculateOutput(personnelCapacity, outpost?.outpostTypeDef, production, state);
		}

		public virtual float CalculateOutput(float personnelCapacity, OutpostTypeDef outpostTypeDef, OutpostProductionProperties production)
		{
			if (outpostTypeDef == null)
			{
				throw new ArgumentNullException("outpostTypeDef");
			}
			if (production == null)
			{
				throw new ArgumentNullException("production");
			}
			return personnelCapacity * production.outputPerCapacity;
		}

		public virtual float CalculateOutput(float personnelCapacity, OutpostTypeDef outpostTypeDef, OutpostProductionProperties production, OutpostProductionState state)
		{
			return CalculateOutput(personnelCapacity, outpostTypeDef, production);
		}

		public virtual ThingDef GetProduct(OutpostProductionProperties production, OutpostProductionState state)
		{
			return production?.product;
		}

		public virtual int GetProductionIntervalTicks(OutpostProductionProperties production, OutpostProductionState state)
		{
			return production?.intervalTicks ?? 0;
		}

		/// <summary>框架内部唯一的产出计算入口：产能 + 产出 + 合法性检查。</summary>
		public float CalculateProduction(Outpost outpost, OutpostProductionProperties production, OutpostProductionState state)
		{
			if (outpost == null)
			{
				throw new ArgumentNullException("outpost");
			}
			if (production == null)
			{
				throw new ArgumentNullException("production");
			}
			float capacity = CalculatePersonnelCapacity(outpost, production);
			float output = CalculateOutput(capacity, outpost, production, state);
			if (float.IsNaN(output) || float.IsInfinity(output) || output < 0f)
			{
				throw new InvalidOperationException("Production " + production.id + " returned a non-finite or negative output.");
			}
			return output;
		}

		/// <summary>保留给拿不到 Outpost 的调用方；框架内部一律走带 Outpost 的重载。</summary>
		public float CalculateProduction(IEnumerable<Pawn> pawns, OutpostTypeDef outpostTypeDef, OutpostProductionProperties production)
		{
			return CalculateProduction(pawns, outpostTypeDef, production, null);
		}

		/// <summary>保留给拿不到 Outpost 的调用方；框架内部一律走带 Outpost 的重载。</summary>
		public float CalculateProduction(IEnumerable<Pawn> pawns, OutpostTypeDef outpostTypeDef, OutpostProductionProperties production, OutpostProductionState state)
		{
			if (outpostTypeDef == null)
			{
				throw new ArgumentNullException("outpostTypeDef");
			}
			if (production == null)
			{
				throw new ArgumentNullException("production");
			}
			float capacity = CalculatePersonnelCapacity(pawns, outpostTypeDef, production);
			float output = CalculateOutput(capacity, outpostTypeDef, production, state);
			if (float.IsNaN(output) || float.IsInfinity(output) || output < 0f)
			{
				throw new InvalidOperationException("Production " + production.id + " returned a non-finite or negative output.");
			}
			return output;
		}

		/// <summary>
		/// 开发模式下的提醒：worker 实例按「生产规则 Def」缓存并共享，同一个 Def 的所有据点共用同一个对象，
		/// 所以实例字段等于全局状态，会串到别的据点去。跨调用要保存的数据请放进 OutpostProductionState（跟着设施存档）。
		/// 每个类型只提醒一次；只在开发模式调用。
		/// </summary>
		public static void WarnIfStateful(Type workerType)
		{
			if (workerType == null || !statefulWorkerWarned.Add(workerType)) return;
			if (workerType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Length != 0)
			{
				Log.Warning("[DreamsOutposts] Production worker " + workerType.FullName + " declares instance fields, but one worker instance is shared by every outpost using the same production rule, so whatever is written to them leaks between outposts. Keep the worker stateless and put per-facility data into OutpostProductionState. (Ignore this if those fields are only per-call scratch space.)");
			}
		}

		public virtual IEnumerable<string> ConfigErrors(OutpostProductionProperties production)
		{
			// capacityStat 留空是合法的：此时产能固定为 1，设施不依赖任何技能属性。
			foreach (string item in StateClassErrors())
			{
				yield return item;
			}
		}

		public virtual OutpostProductionState CreateState(string productionId, int nextProductionTick)
		{
			return (OutpostProductionState)Activator.CreateInstance(StateClass, productionId, nextProductionTick);
		}

		private IEnumerable<string> StateClassErrors()
		{
			Type stateClass = StateClass;
			if (stateClass == null || !typeof(OutpostProductionState).IsAssignableFrom(stateClass))
			{
				yield return "StateClass must derive from OutpostProductionState.";
			}
			else if (stateClass.IsAbstract || stateClass.ContainsGenericParameters)
			{
				yield return "StateClass must be a concrete OutpostProductionState subclass.";
			}
			else if (stateClass.GetConstructor(new Type[2]
			{
				typeof(string),
				typeof(int)
			}) == null)
			{
				yield return "StateClass must have a public (string productionId, int nextProductionTick) constructor.";
			}
		}

		public virtual bool OverrideProductionCycle(OutpostProductionContext context)
		{
			return false;
		}

		public virtual bool CanProduce(OutpostProductionContext context)
		{
			return true;
		}

		public virtual void ModifyProduction(OutpostProductionContext context)
		{
		}

		public virtual int MaxProducibleAmount(OutpostProductionContext context)
		{
			if (context?.Production == null || !context.Production.HasInputs)
			{
				return int.MaxValue;
			}
			return OutpostStockUtility.MaxCraftableUnits(context.Outpost, context.Production.inputs);
		}

		public virtual bool ConsumeInputs(OutpostProductionContext context)
		{
			return OutpostProductionUtility.TakeInputsFromStock(context.Outpost, context.Facility, context.Production, context.ActualAmount);
		}

		public virtual List<Thing> CreateProducts(OutpostProductionContext context)
		{
			if (context.ActualAmount <= 0)
			{
				return new List<Thing>();
			}
			return OutpostProductionUtility.MakeProductThings(context.Product, context.ActualAmount);
		}

		public virtual void DeliverProducts(OutpostProductionContext context)
		{
			if (!OutpostAutomaticAirdropUtility.TryDeliver(context))
			{
				OutpostProductionUtility.StoreInOutpostInventory(context.Outpost, context.Products);
			}
		}

		public virtual void AfterProduction(OutpostProductionContext context)
		{
		}

		public virtual void OnProductionFailed(OutpostProductionContext context, Exception ex)
		{
		}

		public virtual bool HasConfiguration(OutpostProductionProperties production)
		{
			return false;
		}

		public virtual void EnsureConfiguration(OutpostProductionProperties production, OutpostProductionState state)
		{
		}

		public virtual void DrawConfiguration(Rect rect, string label, OutpostProductionProperties production, OutpostProductionState state)
		{
		}

		public virtual void OpenConfiguration(OutpostProductionProperties production, OutpostProductionState state, Action onChanged = null)
		{
		}

		public virtual string ConfigurationSummary(OutpostProductionProperties production, OutpostProductionState state)
		{
			return null;
		}

		public virtual string ConfigurationTip(OutpostProductionProperties production)
		{
			return string.Empty;
		}
	}
}
