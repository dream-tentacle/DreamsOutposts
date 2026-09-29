using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 更新日志模块的通用工具：识别本模块所属的 Mod、读取其当前版本号（About.xml 的 modVersion）、
	/// 比较版本号并收集要展示的日志。
	/// </summary>
	public static class UpdateNoticeUtility
	{
		private static bool ownModResolved;
		private static ModContentPack ownMod;
		private static string ownVersion;

		/// <summary>本模块所属的 Mod（依据「哪个已加载 Mod 的程序集包含本类型」自动判定）。</summary>
		public static ModContentPack OwnMod
		{
			get
			{
				ResolveOwnMod();
				return ownMod;
			}
		}

		/// <summary>本 Mod 当前版本号，取自 About.xml 的 modVersion；取不到时返回 null。</summary>
		public static string CurrentVersion
		{
			get
			{
				ResolveOwnMod();
				return ownVersion;
			}
		}

		private static void ResolveOwnMod()
		{
			if (ownModResolved)
			{
				return;
			}
			ownModResolved = true;

			Assembly self = typeof(UpdateNoticeUtility).Assembly;
			List<ModContentPack> mods = LoadedModManager.RunningModsListForReading;
			for (int i = 0; i < mods.Count; i++)
			{
				ModContentPack pack = mods[i];
				if (pack.assemblies == null || pack.assemblies.loadedAssemblies == null)
				{
					continue;
				}
				if (pack.assemblies.loadedAssemblies.Contains(self))
				{
					ownMod = pack;
					break;
				}
			}

			if (ownMod != null && ownMod.ModMetaData != null)
			{
				ownVersion = ownMod.ModMetaData.ModVersion;
			}
		}

		/// <summary>
		/// 版本号是否匹配。忽略两端空白和大小写，不比较版本大小。
		/// 空版本不匹配，尚未记录版本时仍应提示。
		/// </summary>
		public static bool VersionsMatch(string current, string other)
		{
			return !string.IsNullOrWhiteSpace(current) && !string.IsNullOrWhiteSpace(other) &&
				string.Equals(current.Trim(), other.Trim(), StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>版本号比较：可解析时按数值比较，否则按字符串序比较（仅用于排序展示）。</summary>
		public static int CompareVersions(string a, string b)
		{
			if (a.NullOrEmpty() && b.NullOrEmpty())
			{
				return 0;
			}

			if (a.NullOrEmpty())
			{
				return -1;
			}

			if (b.NullOrEmpty())
			{
				return 1;
			}

			Version va;
			Version vb;
			if (Version.TryParse(a.Trim(), out va) && Version.TryParse(b.Trim(), out vb))
			{
				return va.CompareTo(vb);
			}

			return string.CompareOrdinal(a, b);
		}

		/// <summary>
		/// 当前版本与已提示版本不匹配时，只收集与 About 当前版本匹配的公告。
		/// 升级、降级和首次提示使用同一规则。
		/// </summary>
		public static List<UpdateNoticeDef> PendingNotices(string seenVersion)
		{
			string current = CurrentVersion;
			List<UpdateNoticeDef> result = new List<UpdateNoticeDef>();
			if (string.IsNullOrWhiteSpace(current) || VersionsMatch(current, seenVersion))
				return result;
			List<UpdateNoticeDef> all = DefDatabase<UpdateNoticeDef>.AllDefsListForReading;
			for (int i = 0; i < all.Count; i++)
			{
				UpdateNoticeDef def = all[i];
				if (def != null && VersionsMatch(def.version, current))
					result.Add(def);
			}

			return result;
		}

		/// <summary>
		/// 全部日志（不高于当前版本的那些），同样按版本从高到低排序，供设置里的「查看更新日志」使用。
		/// </summary>
		public static List<UpdateNoticeDef> AllNotices()
		{
			string current = CurrentVersion;
			List<UpdateNoticeDef> result = new List<UpdateNoticeDef>();
			foreach (UpdateNoticeDef def in DefDatabase<UpdateNoticeDef>.AllDefsListForReading)
			{
				if (def == null || string.IsNullOrWhiteSpace(def.version)) continue;
				if (!string.IsNullOrWhiteSpace(current) && CompareVersions(def.version, current) > 0) continue;
				result.Add(def);
			}
			result.Sort((x, y) => CompareVersions(y.version, x.version));
			return result;
		}
	}
}
