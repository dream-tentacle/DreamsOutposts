using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 普通随机事件的全局调度状态，随存档保存。
	/// 全局只有这一个计时器：据点自身不保存任何随机事件计时。
	/// 普通随机事件什么时候发生必须对玩家保持隐藏，所以这里不做任何 UI。
	/// </summary>
	public class GameComponent_OutpostRandomEvents : GameComponent
	{
		public int nextRandomEventTick;

		public GameComponent_OutpostRandomEvents(Game game)
		{
		}

		public static GameComponent_OutpostRandomEvents Instance => Current.Game?.GetComponent<GameComponent_OutpostRandomEvents>();

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref nextRandomEventTick, "nextRandomEventTick", 0);
		}

		public override void FinalizeInit()
		{
			base.FinalizeInit();
			OutpostFacilityTagRegistry.Initialize();
			// 新游戏，或旧存档里没有有效值（0）时，从当前时刻重新排期。
			// 已经过期的值也重新排期，保证读档后不会立刻生成随机事件。
			int now = Find.TickManager.TicksGame;
			if (nextRandomEventTick <= 0 || nextRandomEventTick <= now)
			{
				nextRandomEventTick = now + OutpostRandomEventScheduler.RollIntervalTicks();
			}
		}

		public override void GameComponentTick()
		{
			int now = Find.TickManager.TicksGame;
			if (nextRandomEventTick <= 0)
			{
				// FinalizeInit 没有跑过时的兜底，同样从当前时刻重新排期。
				nextRandomEventTick = now + OutpostRandomEventScheduler.RollIntervalTicks();
				return;
			}
			if (now < nextRandomEventTick)
			{
				return;
			}
			if (OutpostRandomEventScheduler.TryCreateRandomEvent())
			{
				nextRandomEventTick = now + OutpostRandomEventScheduler.RollIntervalTicks();
			}
			else
			{
				// 当前没有任何符合条件的据点：1 天后重新检查，不每 Tick 重试，也不提示玩家。
				nextRandomEventTick = now + OutpostRandomEventScheduler.RetryDelayTicks;
			}
		}
	}
}
