using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 新开局 / 读档后弹出一次开局指引。原版 Game.FillComponents() 会反射创建所有非抽象
	/// GameComponent 子类（读档时也会补齐缺失的组件），所以这里不需要任何 Def。
	/// 开关存在 ModSettings 里，关掉后就不再弹。
	/// </summary>
	public class GameComponent_OutpostIntroTips : GameComponent
	{
		public GameComponent_OutpostIntroTips(Game game) { }

		public override void LoadedGame()
		{
			base.LoadedGame();
			ShowIntro();
		}

		public override void StartedNewGame()
		{
			base.StartedNewGame();
			ShowIntro();
		}

		private static void ShowIntro()
		{
			DreamsOutpostsSettings settings = DreamsOutpostsMod.Settings;
			if (settings == null || !settings.showIntroTips)
			{
				return;
			}
			// 这两个回调都在读档 / 开局的长事件内部，窗口必须等加载界面收起后再入栈。
			LongEventHandler.ExecuteWhenFinished(delegate
			{
				UiIntroTipsWindow.Open(UiIntroTipsWindow.AutoLockSeconds);
			});
		}
	}
}
