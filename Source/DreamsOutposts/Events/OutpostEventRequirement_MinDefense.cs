using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 要求据点当前防卫值不低于 minDefense。只负责判断选项是否可选，不产生任何 Effect。
	/// </summary>
	public class OutpostEventRequirement_MinDefense : OutpostEventRequirement
	{
		public float minDefense;

		public override AcceptanceReport Check(OutpostEventContext context)
		{
			float current = OutpostDefenseUtility.TotalDefense(context?.outpost);
			if (current < minDefense)
			{
				return "需要防卫：" + minDefense.ToString("0.#") + "\n当前防卫：" + current.ToString("0.#");
			}
			return true;
		}
	}
}
