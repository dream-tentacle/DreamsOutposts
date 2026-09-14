using HarmonyLib;

namespace DreamsOutposts
{
	public interface IOutpostCompatibility
	{
		string PackageId { get; }

		void Apply(Harmony harmony);
	}
}
