using HarmonyLib;

namespace DreamsOutposts
{
	/// <summary>
	/// 米莉拉（Milira Race）兼容：日光冶炼场按据点内驻守的米莉拉与米莉安计算效率。
	/// 本模块没有任何需要 Harmony 补丁或反射的地方：效率数值、产物和种族判定全部写在 Defs/Compatibility/Milira_*.xml 与
	/// OutpostProductionWorker_MiliraSolar 里，那些 def 自己用 MayRequire="Ancot.MiliraRace" 门禁，
	/// 米莉拉按 ThingDef Milira_Race、米莉安按原版 BodyDef Milian_Body 判定，不引用米莉拉的程序集。
	/// 这里只保留 PackageId，让 CompatibilityManager 的开关机制对米莉拉一样成立。
	/// 注意：不要在这里做 def 查询。mod 构造函数跑在 LoadedModManager.CreateModClasses()，早于 LoadModXML/ApplyPatches/ParseAndProcessXML，
	/// 那时连原版 def 都不存在，任何 def 查询都只会误报「找不到」。
	/// </summary>
	public sealed class MiliraCompatibility : IOutpostCompatibility
	{
		public string PackageId => "Ancot.MiliraRace";

		public void Apply(Harmony harmony)
		{
		}
	}
}
