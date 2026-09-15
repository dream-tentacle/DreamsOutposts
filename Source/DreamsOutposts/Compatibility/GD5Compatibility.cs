using HarmonyLib;

namespace DreamsOutposts
{
	/// <summary>
	/// 闪耀世界毁灭者5（Glitterworld Destroyer 5）兼容：精密加工车间按据点内驻守机械族的加工能力计算效率。
	/// 本模块没有任何需要 Harmony 补丁或反射的地方：效率数值、产物和机械体名单全部写在 Defs/Compatibility/GD5_*.xml 里，
	/// 那些 def 自己用 MayRequire="fxz.glitterworlddestroyer.mk5" 门禁，重量档用的是原版 RaceProps.mechWeightClass。
	/// 这里只保留 PackageId，让 CompatibilityManager 的开关机制对 GD5 一样成立。
	/// 注意：不要在这里做 def 查询。mod 构造函数跑在 LoadedModManager.CreateModClasses()，早于 LoadModXML/ApplyPatches/ParseAndProcessXML，
	/// 那时连原版 def 都不存在，任何 def 查询都只会误报「找不到」。
	/// </summary>
	public sealed class GD5Compatibility : IOutpostCompatibility
	{
		public string PackageId => "fxz.glitterworlddestroyer.mk5";

		public void Apply(Harmony harmony)
		{
		}
	}
}
