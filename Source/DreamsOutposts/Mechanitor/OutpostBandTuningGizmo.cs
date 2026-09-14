using System.Linq;
using RimWorld;
using Verse;

namespace DreamsOutposts
{
	public static class OutpostBandTuningGizmo
	{
		public static Command GetCommand(Outpost outpost)
		{
			if (outpost == null || !OutpostBandwidthUtility.Nodes(outpost).Any()) return null;
			return new Command_Action
			{
				defaultLabel = "DreamsOutposts.BandTuning.Gizmo".Translate(),
				defaultDesc = "DreamsOutposts.BandTuning.GizmoDesc".Translate(),
				icon = TexCommand.DesirePower,
				action = () => Window_OutpostBandTuning.Open(outpost)
			};
		}
	}
}
