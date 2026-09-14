using System.Collections.Generic;
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
			return "DreamsOutposts.EventEffect.ScheduledEvent".Translate(DelayLabel(delayTicksRange), eventDef.LabelCap).ToString();
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
				return "DreamsOutposts.EventEffect.DelayDaysRange".Translate(min / 60000, max / 60000).ToString();
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
					return "DreamsOutposts.EventEffect.DelayDays".Translate(days).ToString();
				}
				return "DreamsOutposts.EventEffect.DelayDaysHours".Translate(days, hours).ToString();
			}
			if (hours > 0)
			{
				return "DreamsOutposts.EventEffect.DelayHours".Translate(hours).ToString();
			}
			return "DreamsOutposts.EventEffect.DelayMoments".Translate().ToString();
		}
	}

	public class OutpostEventWeightedOption
	{
		public OutpostEventDef eventDef;
		public float weight = 1f;
	}

	public class OutpostEventEffect_ScheduleRandomEvent : OutpostEventEffect
	{
		public List<OutpostEventWeightedOption> options = new List<OutpostEventWeightedOption>();
		public IntRange delayTicksRange;

		public override void Apply(OutpostEventContext context)
		{
			if (context?.outpost == null || options.NullOrEmpty() || delayTicksRange.TrueMin < 0) return;
			float total = 0f;
			for (int i = 0; i < options.Count; i++)
			{
				if (options[i]?.eventDef != null) total += Mathf.Max(options[i].weight, 0f);
			}
			if (total <= 0f)
			{
				Log.Error("OutpostEventEffect_ScheduleRandomEvent has no positively weighted event options.");
				return;
			}
			float roll = Rand.Range(0f, total);
			OutpostEventDef selected = null;
			for (int i = 0; i < options.Count; i++)
			{
				OutpostEventWeightedOption option = options[i];
				if (option?.eventDef == null || option.weight <= 0f) continue;
				roll -= option.weight;
				if (roll < 0f)
				{
					selected = option.eventDef;
					break;
				}
			}
			if (selected == null) selected = options.FindLast(option => option?.eventDef != null && option.weight > 0f).eventDef;
			context.outpost.AddScheduledEvent(selected, Rand.RangeInclusive(delayTicksRange.TrueMin, delayTicksRange.TrueMax));
		}

		public override string GetPreview(OutpostEventContext context)
		{
			return "DreamsOutposts.EventEffect.RandomFollowUp".Translate().ToString();
		}
	}
}
