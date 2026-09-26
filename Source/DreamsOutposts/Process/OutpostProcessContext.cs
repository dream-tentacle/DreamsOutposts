namespace DreamsOutposts
{
	public class OutpostProcessContext
	{
		public readonly Outpost Outpost;
		public readonly OutpostFacility Facility;
		public readonly OutpostProcessProperties Process;
		public readonly OutpostProcessState State;
		public readonly int Now;

		public float BaseOutput;
		public float ModifiedOutput;
		public int NextProcessInterval;
		public OutpostProcessOutcome Outcome = OutpostProcessOutcome.Idle;
		public string FailureReason;

		public OutpostProcessWorker Worker => Process?.Worker;
		public bool Succeeded => Outcome == OutpostProcessOutcome.Completed;
		public int EffectiveInterval => NextProcessInterval <= 0 ? 1 : NextProcessInterval;
		public string RuleLabel => OutpostProcessUtility.RuleLabel(Facility, Process);

		public OutpostProcessContext(
			Outpost outpost,
			OutpostFacility facility,
			OutpostProcessProperties process,
			OutpostProcessState state,
			int now)
		{
			Outpost = outpost;
			Facility = facility;
			Process = process;
			State = state;
			Now = now;
			NextProcessInterval = process?.Worker.GetProcessIntervalTicks(process, state) ?? 0;
		}
	}
}
