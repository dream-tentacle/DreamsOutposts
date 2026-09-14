using LudeonTK;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 开发者菜单（Debug actions）里的据点事件指令。
	/// 这里用的是全局随机事件调度器的逻辑：所有据点 → CanReceiveRandomEvent() → 至少一个合法事件 → 等概率抽一个，
	/// 与据点上那个生成全部正权重事件的 gizmo 不是同一条路径。
	/// 指令本身不修改 nextRandomEventTick，全局排期不受影响。
	/// </summary>
	public static class DebugActions_OutpostEvents
	{
		[DebugAction("DreamsOutposts", "Generate outpost random event")]
		public static void GenerateRandomOutpostEvent()
		{
			if (OutpostRandomEventScheduler.TryCreateRandomEvent(out Outpost outpost, out OutpostEventDef eventDef))
			{
				Log.Message("DreamsOutposts: generated random event " + eventDef.defName + " on " + outpost.Label + ". Next scheduled random event: " + NextScheduledLabel() + ".");
			}
			else
			{
				Log.Message("DreamsOutposts: no outpost can receive a random event right now (no outpost passed CanReceiveRandomEvent() with at least one valid event def). Next scheduled random event: " + NextScheduledLabel() + ".");
			}
		}

		private static string NextScheduledLabel()
		{
			GameComponent_OutpostRandomEvents component = GameComponent_OutpostRandomEvents.Instance;
			if (component == null || component.nextRandomEventTick <= 0)
			{
				return "not scheduled";
			}
			int remaining = component.nextRandomEventTick - Find.TickManager.TicksGame;
			if (remaining <= 0)
			{
				return "due now";
			}
			return "tick " + component.nextRandomEventTick + " (" + ((float)remaining / 60000f).ToString("F1") + " days from now)";
		}
	}
}
