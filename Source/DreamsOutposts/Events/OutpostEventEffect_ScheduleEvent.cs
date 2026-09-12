using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 把一个后续事件排进当前据点的待触发列表，delayTicksRange 之后由据点自己的低频更新逻辑创建。
	/// 延迟在排期这一刻就摇成一个具体 tick 存进 OutpostScheduledEvent，
	/// 因此读档不会改变已经定好的触发时间。
	/// 这里只负责登记，不创建事件、不发 Letter；真正创建走 Outpost.AddEvent 这个统一入口。
	/// </summary>
	public class OutpostEventEffect_ScheduleEvent : OutpostEventEffect
	{
		public OutpostEventDef eventDef;

		/// <summary>
		/// 延迟区间，XML 写法与原版 IntRange 一致：单个值 <c>180000</c> 表示固定延迟，
		/// <c>180000~300000</c> 表示在 3~5 天之间随机。
		/// </summary>
		public IntRange delayTicksRange;

		public override void Apply(OutpostEventContext context)
		{
			if (context?.outpost == null)
			{
				return;
			}
			if (eventDef == null)
			{
				Log.Error("OutpostEventEffect_ScheduleEvent has no eventDef; nothing was scheduled for outpost " + context.outpost.Label + ".");
				return;
			}
			if (delayTicksRange.TrueMin < 0)
			{
				Log.Error("OutpostEventEffect_ScheduleEvent for " + eventDef.defName + " has a negative delayTicksRange (" + delayTicksRange + "); nothing was scheduled for outpost " + context.outpost.Label + ".");
				return;
			}
			int delayTicks = Rand.RangeInclusive(delayTicksRange.TrueMin, delayTicksRange.TrueMax);
			context.outpost.AddScheduledEvent(eventDef, delayTicks);
		}

		public override string GetPreview(OutpostEventContext context)
		{
			if (eventDef == null)
			{
				return null;
			}
			return "In " + DelayLabel(delayTicksRange) + ": " + (string)eventDef.LabelCap;
		}

		private static string DelayLabel(IntRange range)
		{
			int min = Mathf.Max(range.TrueMin, 0);
			int max = Mathf.Max(range.TrueMax, 0);
			if (min == max)
			{
				return TicksLabel(min);
			}
			// 整数天区间合并成「3~5 days」，其余情况逐项写出来。
			if (min > 0 && max > 0 && min % 60000 == 0 && max % 60000 == 0)
			{
				return (min / 60000) + "~" + (max / 60000) + " days";
			}
			return TicksLabel(min) + " to " + TicksLabel(max);
		}

		private static string TicksLabel(int ticks)
		{
			int days = ticks / 60000;
			int hours = ticks % 60000 / 2500;
			if (days > 0)
			{
				if (hours == 0)
				{
					return (days == 1) ? "1 day" : (days + " days");
				}
				return days + "d " + hours + "h";
			}
			if (hours > 0)
			{
				return (hours == 1) ? "1 hour" : (hours + " hours");
			}
			return "moments";
		}
	}
}
