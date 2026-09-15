using System.Collections.Generic;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 设施在安装页里的分类。每个分类是一个 Def：显示名走 label（DefInjected 可覆盖翻译），
	/// 标签页顺序由 order 决定。新增分类只需要写一个 Def，不必改代码。
	/// </summary>
	public class OutpostFacilityCategoryDef : Def
	{
		/// <summary>安装页标签页顺序，越小越靠前。相同 order 时按显示名排序。</summary>
		public float order;

		public override IEnumerable<string> ConfigErrors()
		{
			foreach (string error in base.ConfigErrors())
			{
				yield return error;
			}
			if (float.IsNaN(order) || float.IsInfinity(order))
			{
				yield return "order must be a finite number.";
			}
		}
	}
}
