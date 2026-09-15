using System.Collections.Generic;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 炮击命令。左键进入两步选点；右键切换这个据点使用的迫击炮弹种类。
	/// 需要用子类而不是直接用 Command_Action，因为原版只从 Gizmo.RightClickFloatMenuOptions 收右键菜单。
	/// </summary>
	public class Command_Bombard : Command_Action
	{
		public Outpost outpost;

		public override IEnumerable<FloatMenuOption> RightClickFloatMenuOptions => OutpostBombardmentUtility.ShellChoiceOptions(outpost);
	}
}
