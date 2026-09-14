namespace DreamsOutposts
{
	public class OutpostEventContext
	{
		public Outpost outpost;

		public OutpostEventInstance instance;

		/// <summary>由事件框架在结算选项前初始化，供所有物品奖励 effect 统一写入。</summary>
		public OutpostItemRewardCollector itemRewards;
	}
}
