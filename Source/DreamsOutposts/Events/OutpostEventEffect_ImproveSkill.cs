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

	public class OutpostEventEffect_GainSkillXp : OutpostEventEffect
	{
		public SkillDef skillDef;

		public float xp;

		public override void Apply(OutpostEventContext context)
		{
			if (context?.outpost == null || skillDef == null || xp <= 0f)
			{
				return;
			}
			foreach (Pawn pawn in context.outpost.Colonists)
			{
				SkillRecord record = pawn.skills?.GetSkill(skillDef);
				if (record != null && !record.TotallyDisabled)
				{
					record.Learn(xp, direct: true);
				}
			}
		}

		public override string GetPreview(OutpostEventContext context)
		{
			return "DreamsOutposts.EventEffect.GainSkillXp".Translate(skillDef?.LabelCap ?? "DreamsOutposts.Unknown".Translate(), xp.ToString("0.#")).ToString();
		}
	}
}
