using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 普通随机事件的全局调度：从所有据点中等概率挑一个合格据点，然后交给统一事件创建入口。
	/// 据点之间没有权重。据点级准入只走 Outpost.CanReceiveRandomEvent()，
	/// 某个具体事件当前能不能发生由 EventDef 自己的 requirements 决定，两层互不混用。
	/// </summary>
	public static class OutpostRandomEventScheduler
	{
		// 普通随机事件的间隔由玩家设置在 DreamsOutpostsSettings 里调整（默认 3~6 天），
		// 这里的常量只是「没有设置实例」时的兜底默认值。
		// 重试间隔是「当前没有任何合格据点」时的内部重试，不对外暴露。
		public const int DefaultMinIntervalDays = 3;

		public const int DefaultMaxIntervalDays = 6;

		public const int RetryDelayDays = 1;

		public const int TicksPerDay = 60000;

		public const int RetryDelayTicks = RetryDelayDays * TicksPerDay;

		/// <summary>普通随机事件的全局开关。没有设置实例（例如设置尚未加载）时视为开启。</summary>
		public static bool RandomEventsEnabled
		{
			get
			{
				DreamsOutpostsSettings settings = DreamsOutpostsMod.Settings;
				return settings == null || settings.randomEventsEnabled;
			}
		}

		/// <summary>
		/// 下一次普通随机事件的间隔，读玩家设置的天数区间。
		/// 只在排期时调用，所以游戏中途改设置只影响「下一轮」，已经排好的那一次不会被推迟或提前。
		/// </summary>
		public static int RollIntervalTicks()
		{
			DreamsOutpostsSettings settings = DreamsOutpostsMod.Settings;

			int minDays = settings != null
				? Mathf.RoundToInt(settings.randomEventIntervalDays.min)
				: DefaultMinIntervalDays;

			int maxDays = settings != null
				? Mathf.RoundToInt(settings.randomEventIntervalDays.max)
				: DefaultMaxIntervalDays;

			if (minDays < 1)
			{
				minDays = 1;
			}
			if (maxDays < minDays)
			{
				maxDays = minDays;
			}

			return Rand.RangeInclusive(minDays, maxDays) * TicksPerDay;
		}

		/// <summary>
		/// 收集当前可以被普通随机事件选中的据点：玩家阵营、未被摧毁、
		/// CanReceiveRandomEvent() 通过，且当前至少存在一个合法随机 EventDef。
		/// 不判断任何具体设施或据点状态，那属于 CanReceiveRandomEvent() 内部的事。
		/// </summary>
		public static void CollectEligibleOutposts(List<Outpost> result)
		{
			if (result == null)
			{
				return;
			}
			result.Clear();
			List<WorldObject> worldObjects = Find.WorldObjects?.AllWorldObjects;
			if (worldObjects == null)
			{
				return;
			}
			for (int i = 0; i < worldObjects.Count; i++)
			{
				if (!(worldObjects[i] is Outpost outpost) || outpost.Destroyed)
				{
					continue;
				}
				if (outpost.Faction != Faction.OfPlayer)
				{
					continue;
				}
				if (!outpost.CanReceiveRandomEvent().Accepted)
				{
					continue;
				}
				if (!OutpostEventUtility.HasAnyValidEvent(outpost))
				{
					continue;
				}
				result.Add(outpost);
			}
		}

		public static bool TryCreateRandomEvent()
		{
			return TryCreateRandomEvent(out Outpost _, out OutpostEventDef _);
		}

		/// <summary>
		/// 选中一个据点并创建一个随机事件。必须通过 Outpost.AddEvent 这个统一事件创建入口，
		/// 以保证 createdTick、expireTick 和新事件 Letter 等现有逻辑正常执行。
		/// 固定刷出的事件（OutpostEventDef.forcedAtRandomEventCount 等于本次序号）跳过权重抽取直接使用；
		/// 只有真正创建成功才推进 GameComponent_OutpostRandomEvents.randomEventsGenerated。
		/// </summary>
		public static bool TryCreateRandomEvent(out Outpost outpost, out OutpostEventDef eventDef)
		{
			outpost = null;
			eventDef = null;
			List<Outpost> candidates = new List<Outpost>();
			CollectEligibleOutposts(candidates);
			if (candidates.Count == 0)
			{
				return false;
			}
			// 等概率选取：据点之间没有权重。
			Outpost target = candidates[Rand.Range(0, candidates.Count)];
			GameComponent_OutpostRandomEvents component = GameComponent_OutpostRandomEvents.Instance;
			// 即将生成的是第几次普通随机事件。没有管理器时按第一次处理，不影响正常抽取。
			int ordinal = (component?.randomEventsGenerated ?? 0) + 1;
			OutpostEventDef forced = OutpostEventUtility.ForcedEventForRandomOrdinal(ordinal);
			if (forced != null && OutpostEventUtility.IsEventDefAllowedFor(forced, new OutpostEventContext { outpost = target }))
			{
				eventDef = forced;
			}
			else if (!OutpostEventUtility.TryChooseRandomEvent(target, out eventDef))
			{
				return false;
			}
			if (target.AddEvent(eventDef) == null)
			{
				// 事件没有被真正创建（例如 Def 的 InitializeInstance 拒绝初始化）：
				// 这次不算生成过一次随机事件，计数不推进，序号留给下一次。
				return false;
			}
			if (component != null)
			{
				component.randomEventsGenerated++;
			}
			outpost = target;
			return true;
		}
	}
}
