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
		/// current 是否比 other 新。两者都能按版本号解析时比较数值，否则「不同即视为更新」。
		/// other 为空（还没有记录）时视为需要提示，返回 true。
		/// </summary>
		public static bool IsNewerThan(string current, string other)
		{
			if (current.NullOrEmpty())
			{
				return false;
			}

			if (other.NullOrEmpty())
			{
				return true;
			}

			if (string.Equals(current.Trim(), other.Trim(), StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			Version a;
			Version b;
			if (Version.TryParse(current.Trim(), out a) && Version.TryParse(other.Trim(), out b))
			{
				return a > b;
			}

			return true;
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
		/// 收集「比 seenVersion 新、且不高于当前版本」的日志，按版本从高到低排序（新版本排在最上面）。
		/// seenVersion 为空时返回全部不高于当前版本的日志（老存档首次引入本模块的情况）。
		/// </summary>
		public static List<UpdateNoticeDef> PendingNotices(string seenVersion)
		{
			string current = CurrentVersion;
			List<UpdateNoticeDef> result = new List<UpdateNoticeDef>();
			List<UpdateNoticeDef> all = DefDatabase<UpdateNoticeDef>.AllDefsListForReading;
			for (int i = 0; i < all.Count; i++)
			{
				UpdateNoticeDef def = all[i];
				if (def == null || def.version.NullOrEmpty())
				{
					continue;
				}

				if (!IsNewerThan(def.version, seenVersion))
				{
					continue;
				}

				// 日志版本高于当前 Mod 版本（玩家降级了 Mod）时不展示，升级回来后仍会提示。
				if (!current.NullOrEmpty() && IsNewerThan(def.version, current))
				{
					continue;
				}

				result.Add(def);
			}

			result.Sort(delegate(UpdateNoticeDef x, UpdateNoticeDef y)
			{
				return CompareVersions(y.version, x.version);
			});
			return result;
		}

		/// <summary>
		/// 全部日志（不高于当前版本的那些），同样按版本从高到低排序，供设置里的「查看更新日志」使用。
		/// </summary>
		public static List<UpdateNoticeDef> AllNotices()
		{
			return PendingNotices(null);
		}
	}
}
