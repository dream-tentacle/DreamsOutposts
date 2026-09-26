namespace DreamsOutposts
{
	public class OutpostProductionState : OutpostProcessState
	{
		public string productionId
		{
			get => processId;
			set => processId = value;
		}

		public int nextProductionTick
		{
			get => nextProcessTick;
			set => nextProcessTick = value;
		}

		public OutpostProductionState()
		{
		}

		public OutpostProductionState(string productionId, int nextProductionTick)
			: base(productionId, nextProductionTick)
		{
		}

		public override string ToString()
		{
			return productionId + "@" + nextProductionTick;
		}
	}
}
