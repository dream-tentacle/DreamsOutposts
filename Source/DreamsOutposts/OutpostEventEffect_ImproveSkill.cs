using RimWorld;
using Verse;

namespace DreamsOutposts
{
	public class OutpostEventEffect_ImproveSkill : OutpostEventEffect
	{
		public SkillDef skillDef;

		public int levels;

		public override void Apply(OutpostEventContext context)
		{
			if (context?.outpost?.Pawns == null || skillDef == null || levels <= 0)
			{
				return;
			}
			foreach (Pawn pawn in context.outpost.Pawns)
			{
				SkillRecord record = pawn?.skills?.GetSkill(skillDef);
				if (record != null)
				{
					record.Level += levels;
				}
			}
		}

		public override string GetPreview(OutpostEventContext context)
		{
			return "+" + levels + " " + (skillDef?.LabelCap ?? "unknown skill") + " skill levels for everyone";
		}
	}
}
