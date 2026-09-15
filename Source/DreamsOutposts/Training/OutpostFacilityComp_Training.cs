namespace DreamsOutposts
{
	/// <summary>
	/// 训练型设施不提供 UI 区块：技能与每小时经验已经由卡片底部的「训练 …/时」chip
	/// 和它的 tooltip 表达，卡片中间再重复一次只会挤占版面。
	/// </summary>
	public class OutpostFacilityComp_Training : OutpostFacilityComp
	{
		public override void Update(Outpost outpost, int delta)
		{
			OutpostTrainingUtility.TickFacility(outpost, (OutpostTrainingProperties)props, delta);
		}
	}
}
