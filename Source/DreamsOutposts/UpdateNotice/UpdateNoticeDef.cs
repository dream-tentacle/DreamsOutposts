using System.Collections.Generic;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 一条更新日志：一个版本一条，写在 Defs 里的任意 XML 中。
	/// - version：本条日志对应的 Mod 版本号，与 About.xml 的 modVersion 比较。
	/// - entries：正文，每条单独一段；可用 DefInjected 翻译
	///   （&lt;defName.entries.0&gt;正文&lt;/defName.entries.0&gt;）。
	/// - label：日志窗口的标题词条（本 Mod 里各条都写同一个词条，即「更新日志」）。
	/// 注意：原版解析 Def 的 XML 元素名用的是完整类型名（含命名空间），
	/// 所以 XML 里必须写成 &lt;DreamsOutposts.UpdateNoticeDef&gt;，
	/// DefInjected 的文件夹名也必须是 DreamsOutposts.UpdateNoticeDef。
	/// </summary>
	public class UpdateNoticeDef : Def
	{
		/// <summary>本条日志对应的 Mod 版本号（如 "1.0.2"）。</summary>
		public string version;

		/// <summary>日志正文，每条一段。</summary>
		public List<string> entries = new List<string>();

		public override IEnumerable<string> ConfigErrors()
		{
			foreach (string error in base.ConfigErrors())
			{
				yield return error;
			}

			if (version.NullOrEmpty())
			{
				yield return "version is null or empty (should match the modVersion in About.xml).";
			}

			if (entries.NullOrEmpty())
			{
				yield return "entries is null or empty.";
			}
		}
	}
}
