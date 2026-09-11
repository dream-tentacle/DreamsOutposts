namespace DreamsOutposts
{
	using System.Collections.Generic;

	public class OutpostEventOption
	{
		public string id;

		public string label;

		public string description;

		public bool playerSelectable = true;

		public List<OutpostEventEffect> effects = new List<OutpostEventEffect>();

		public List<OutpostEventRequirement> requirements = new List<OutpostEventRequirement>();
	}
}
