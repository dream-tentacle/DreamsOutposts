using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DreamsOutposts
{
	public class OutpostEventRequirement_AnyColonistSkill : OutpostEventRequirement
	{
		public List<SkillDef> skillDefs = new List<SkillDef>();

		public int minLevel;

		public override AcceptanceReport Check(OutpostEventContext context)
		{
			if (skillDefs.NullOrEmpty() || minLevel < 0)
			{
				return "Invalid colonist skill requirement.";
			}
			foreach (Pawn pawn in context?.outpost?.Colonists ?? new List<Pawn>())
			{
				for (int i = 0; i < skillDefs.Count; i++)
				{
					SkillRecord skill = pawn.skills?.GetSkill(skillDefs[i]);
					if (skill != null && !skill.TotallyDisabled && skill.GetLevel() >= minLevel)
					{
						return true;
					}
				}
			}
			string skills = string.Join(" / ", skillDefs.ConvertAll(skill => skill?.LabelCap.ToString() ?? "unknown skill").ToArray());
			return "DreamsOutposts.EventRequirement.AnyColonistSkill".Translate(skills, minLevel).ToString();
		}
	}
}
