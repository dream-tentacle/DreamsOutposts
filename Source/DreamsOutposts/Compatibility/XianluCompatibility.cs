using HarmonyLib;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 边缘仙路（RimImmortal）兼容：聚灵法坛按殖民者的修仙境界计算效率。
	/// 本模块没有需要 Harmony 补丁的地方，只负责在开局做一次反射自检，缺东西就早报错。
	/// </summary>
	public sealed class XianluCompatibility : IOutpostCompatibility
	{
		public string PackageId => "RI.RimImmortal.Core";

		public void Apply(Harmony harmony)
		{
			XianluCultivationUtility.EnsureResolved();
			if (!XianluCultivationUtility.Available)
			{
				Log.Error("[DreamsOutposts] RimImmortal compatibility could not resolve the cultivation rank API; spirit gathering altars will ignore every colonist's cultivation rank and run at their base efficiency.");
			}
		}
	}
}
