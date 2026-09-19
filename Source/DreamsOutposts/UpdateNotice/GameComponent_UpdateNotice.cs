using System.Collections.Generic;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 读档时比对 Mod 版本，比上次记录的版本新就弹出更新日志。
	/// 原版 Game.FillComponents() 会反射实例化所有非抽象 GameComponent 子类（读档时也会补齐缺失的组件），
	/// 所以这里不需要任何 Def 注册。
	///
	/// 行为：
	/// - 只在读档时比对（新开局不比对）；
	/// - 当前版本比记录新时，把所有「不高于当前版本的新日志」一起弹出，并把记录更新为当前版本
	///   （同一个版本只弹一次）；
	/// - 记录写在模块自己的文件里（Config 目录，全局共用），不碰 DreamsOutpostsSettings；
	/// - ExposeData 不写任何字段，因此不改动存档内容。
	/// </summary>
	public class GameComponent_UpdateNotice : GameComponent
	{
		public GameComponent_UpdateNotice(Game game)
		{
		}

		public override void LoadedGame()
		{
			base.LoadedGame();
			// LoadedGame 是在存档的 Scribe 会话内部被调用的（原版读档流程），
			// 此时读写模块自己的记录文件会与存档的 Scribe 冲突，所以推迟到加载结束后执行。
			LongEventHandler.ExecuteWhenFinished(delegate
			{
				CheckVersionAndShow();
			});
		}

		private static void CheckVersionAndShow()
		{
			UpdateNoticeSettings settings = UpdateNoticeSettings.Current;

			string current = UpdateNoticeUtility.CurrentVersion;
			if (current.NullOrEmpty())
			{
				return;
			}

			// 记录的版本不比当前新（相同、或玩家降级了 Mod）时不弹。
			if (!UpdateNoticeUtility.IsNewerThan(current, settings.LastSeenVersion))
			{
				return;
			}

			List<UpdateNoticeDef> pending = UpdateNoticeUtility.PendingNotices(settings.LastSeenVersion);

			// 无论有没有日志可展示，都先把记录更新为当前版本并写回，这样同一个版本只会提示一次。
			settings.LastSeenVersion = current;
			settings.Save();

			UiUpdateNoticeWindow.Open(pending);
		}
	}
}
