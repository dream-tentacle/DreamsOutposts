using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 一条「待触发事件」：到达 triggerTick 后在所属据点直接创建 eventDef。
	/// 这不是普通随机事件，因此不经过全局随机事件调度器、不检查 CanReceiveRandomEvent()、
	/// 也不参与 Category/Event 权重抽取。它只存在于 Outpost.scheduledEvents 里，
	/// 在真正到期之前不会出现在 outpost.events 中。
	/// </summary>
	public class OutpostScheduledEvent : IExposable
	{
		public OutpostEventDef eventDef;

		public int triggerTick;

		public void ExposeData()
		{
			Scribe_Defs.Look(ref eventDef, "eventDef");
			Scribe_Values.Look(ref triggerTick, "triggerTick", 0);
		}
	}
}
