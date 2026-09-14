using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DreamsOutposts
{
	public class AdventurerRecruitState : IExposable
	{
		public SkillDef preferredSkill;
		public int nextRecruitTick;
		public List<AdventurerOffer> offers = new List<AdventurerOffer>();

		public void ExposeData()
		{
			Scribe_Defs.Look(ref preferredSkill, "preferredSkill");
			Scribe_Values.Look(ref nextRecruitTick, "nextRecruitTick");
			Scribe_Collections.Look(ref offers, "offers", LookMode.Deep);
			if (Scribe.mode == LoadSaveMode.PostLoadInit && offers == null)
			{
				offers = new List<AdventurerOffer>();
			}
		}
	}
}
