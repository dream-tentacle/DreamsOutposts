using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
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
		// 普通随机事件的间隔与重试间隔集中在这里定义，以后调整只改这几行。
		public const int MinIntervalDays = 3;

		public const int MaxIntervalDays = 6;

		public const int RetryDelayDays = 1;

		public const int TicksPerDay = 60000;

		public const int MinIntervalTicks = MinIntervalDays * TicksPerDay;

		public const int MaxIntervalTicks = MaxIntervalDays * TicksPerDay;

		public const int RetryDelayTicks = RetryDelayDays * TicksPerDay;

		public static int RollIntervalTicks()
		{
			return Rand.RangeInclusive(MinIntervalTicks, MaxIntervalTicks);
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
			if (!OutpostEventUtility.TryChooseRandomEvent(target, out eventDef))
			{
				return false;
			}
			target.AddEvent(eventDef);
			outpost = target;
			return true;
		}
	}
}
