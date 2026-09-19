using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	/// <summary>One attack clock for the whole game, independent of the number of outposts.</summary>
	public class GameComponent_OutpostAttacks : GameComponent
	{
		// 袭击间隔由玩家设置在 DreamsOutpostsSettings 里调整（默认 14~21 天），
		// 这里只是「没有设置实例」时的兜底默认值。
		private const int DefaultMinIntervalDays = 14;

		private const int DefaultMaxIntervalDays = 21;

		private const int TicksPerDay = 60000;

		private const int CheckIntervalTicks = 250;

		private int nextAttackTick;
		public GameComponent_OutpostAttacks(Game game) { }

		/// <summary>袭击的全局开关。关闭后不再生成新袭击，但已经开始的袭击仍会被结算。</summary>
		private static bool AttacksEnabled
		{
			get
			{
				DreamsOutpostsSettings settings = DreamsOutpostsMod.Settings;
				return settings == null || settings.attacksEnabled;
			}
		}

		/// <summary>
		/// 下一次袭击的间隔，读玩家设置的天数区间。
		/// 只在排期时调用，所以游戏中途改设置只影响「下一轮」，已经排好的那一次不会被推迟或提前。
		/// </summary>
		private static int RollAttackIntervalTicks()
		{
			DreamsOutpostsSettings settings = DreamsOutpostsMod.Settings;

			int minDays = settings != null
				? Mathf.RoundToInt(settings.attackIntervalDays.min)
				: DefaultMinIntervalDays;

			int maxDays = settings != null
				? Mathf.RoundToInt(settings.attackIntervalDays.max)
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

		public override void ExposeData()
		{
			Scribe_Values.Look(ref nextAttackTick, "nextAttackTick");
		}

		public override void GameComponentTick()
		{
			int now = Find.TickManager.TicksGame;
			if (now % CheckIntervalTicks != 0) return;
			var outposts = Find.WorldObjects.AllWorldObjects.OfType<Outpost>()
				.Where(o => !o.Destroyed && o.Faction == Faction.OfPlayer).ToList();
			// Starts and deadlines lie on 250-tick boundaries. Do not depend on the slower outpost tick.
			// 超时结算与设置无关：即使玩家关闭了袭击，已经开始的战斗也必须正常结算。
			foreach (Outpost outpost in outposts)
				if (outpost.events != null)
					foreach (OutpostEventInstance instance in outpost.events.Where(e => e?.attack != null && now >= e.expireTick).ToList())
						OutpostEventUtility.ResolveByTimeout(outpost, instance);
			if (!AttacksEnabled)
			{
				// 关闭后清空排期：保留旧值会让玩家重新开启时立刻补刷一次早就过期的袭击。
				// 重新开启时 nextAttackTick 为 0，下面会重新排期（只排期，不立刻打）。
				nextAttackTick = 0;
				return;
			}
			if (nextAttackTick > 0 && now < nextAttackTick) return;
			if (outposts.Count == 0)
			{
				nextAttackTick = 0;
				return;
			}
			if (nextAttackTick <= 0)
			{
				nextAttackTick = now + RollAttackIntervalTicks();
				return;
			}
			if (outposts.Any(o => o.events != null && o.events.Any(e => e?.attack != null && !e.attack.resolved)))
			{
				nextAttackTick = now + CheckIntervalTicks;
				return;
			}
			OutpostEventDef def = DefDatabase<OutpostEventDef>.GetNamedSilentFail("DO_OutpostAttack");
			if (def != null && outposts.RandomElement().AddEvent(def) != null)
				nextAttackTick = now + RollAttackIntervalTicks();
			else
				nextAttackTick = now + TicksPerDay;
		}
	}
}
