using System;
using System.IO;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 更新日志模块自带的独立记录文件（全局，所有存档共用）：
	/// Config\Mod_&lt;Mod 文件夹名&gt;_UpdateNotice.xml，只记「上次已经提示过的 Mod 版本号」。
	/// 刻意不使用 DreamsOutpostsSettings：这里的内容属于本机提示状态，不属于玩家的 Mod 设置，
	/// 也不该跟着存档走；独立文件还能让模块整体挪到别的 Mod 里。
	/// </summary>
	public class UpdateNoticeSettings : IExposable
	{
		/// <summary>上次已提示过的 Mod 版本号。</summary>
		private string lastSeenVersion;

		private static UpdateNoticeSettings current;

		public string LastSeenVersion
		{
			get { return lastSeenVersion; }
			set { lastSeenVersion = value; }
		}

		/// <summary>当前记录（首次访问时从磁盘读取，文件不存在则用默认值）。</summary>
		public static UpdateNoticeSettings Current
		{
			get
			{
				if (current == null)
				{
					current = Load();
				}
				return current;
			}
		}

		public void ExposeData()
		{
			Scribe_Values.Look(ref lastSeenVersion, "lastSeenVersion");
		}

		/// <summary>本模块记录文件的路径。</summary>
		public static string FilePath
		{
			get
			{
				ModContentPack own = UpdateNoticeUtility.OwnMod;
				string identifier = (own != null && !own.FolderName.NullOrEmpty())
					? own.FolderName
					: "DreamsOutposts";
				return Path.Combine(
					GenFilePaths.ConfigFolderPath,
					GenText.SanitizeFilename("Mod_" + identifier + "_UpdateNotice.xml"));
			}
		}

		private static UpdateNoticeSettings Load()
		{
			UpdateNoticeSettings result = null;
			try
			{
				string path = FilePath;
				if (File.Exists(path))
				{
					Scribe.loader.InitLoading(path);
					try
					{
						Scribe_Deep.Look(ref result, "UpdateNoticeSettings");
					}
					finally
					{
						Scribe.loader.FinalizeLoading();
					}
				}
			}
			catch (Exception ex)
			{
				Log.Warning("[DreamsOutposts] Failed to read update notice record, using defaults. " + ex);
				result = null;
			}

			return result ?? new UpdateNoticeSettings();
		}

		/// <summary>把当前记录写回磁盘。注意：不要在存档的 Scribe 会话内调用。</summary>
		public void Save()
		{
			try
			{
				UpdateNoticeSettings self = this;
				Scribe.saver.InitSaving(FilePath, "UpdateNoticeSettingsBlock");
				try
				{
					Scribe_Deep.Look(ref self, "UpdateNoticeSettings");
				}
				finally
				{
					Scribe.saver.FinalizeSaving();
				}
			}
			catch (Exception ex)
			{
				Log.Warning("[DreamsOutposts] Failed to write update notice record. " + ex);
			}
		}
	}
}
