using HarmonyLib;
using Verse;

namespace DreamsOutposts
{
	public class DreamsOutpostsMod : Mod
	{
		public static Harmony harmony;

		public static DreamsOutpostsMod Instance;

		public DreamsOutpostsMod(ModContentPack content)
			: base(content)
		{
			Instance = this;
			harmony = new Harmony("mjcg.DreamsOutposts");
			harmony.PatchAll();
		}

		public override string SettingsCategory()
		{
			return base.Content.Name;
		}
	}
}
