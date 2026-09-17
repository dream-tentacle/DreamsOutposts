using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// DevMode 用的布局调试叠层。
	/// 快捷键走 KeyBindingDef（「选项 → 快捷键 → 游戏」里自己绑，不占用固定按键）：
	/// DreamsOutposts_ToggleUiLayoutOverlay 开关叠层（描边 + 尺寸文字 + 鼠标坐标），
	/// DreamsOutposts_DumpUiLayout 把当前布局数值写进日志。
	/// 记录时会把「当前绘制空间（窗口 / 弹窗 / 滚动组）」的局部坐标换算成屏幕坐标，
	/// 所以叠层与日志里的数字在任何窗口里都对得上。
	/// 目的：我看不到画面，但读日志就能知道「哪个盒子在什么位置、多大」。
	/// </summary>
	public static class UiDebug
	{
		/// <summary>开关布局叠层的快捷键 Def。</summary>
		public const string ToggleOverlayKeyDefName = "DreamsOutposts_ToggleUiLayoutOverlay";

		/// <summary>输出布局数值到日志的快捷键 Def。</summary>
		public const string DumpLayoutKeyDefName = "DreamsOutposts_DumpUiLayout";

		private struct Entry
		{
			public string Name;

			/// <summary>记录时所在的绘制空间（窗口 / 弹窗 / 滚动区）。</summary>
			public string Space;

			/// <summary>记录时该空间里的局部坐标。</summary>
			public Rect Rect;

			/// <summary>换算到屏幕的坐标（叠层与日志都用它，避免不同 GUI 组之间看起来「偏移」）。</summary>
			public Rect ScreenRect;
		}

		/// <summary>一个绘制空间：相对上一层（窗口 / 弹窗 / 滚动组）的绝对偏移。</summary>
		private struct Space
		{
			public string Name;

			public Vector2 Offset;
		}

		private const int MaxEntries = 600;

		private static readonly List<Entry> entries = new List<Entry>();

		/// <summary>绘制空间栈：窗口 → 滚动区这样嵌套，Record 时按栈顶换算屏幕坐标。</summary>
		private static readonly List<Space> spaces = new List<Space>();

		private static bool overlay;

		/// <summary>同一帧里 HandleHotkeys 会被每个 OnGUI 事件调用一次，用它去重。</summary>
		private static int lastHotkeyFrame = -1;

		public static bool Available => Prefs.DevMode;

		public static bool Overlay
		{
			get
			{
				return overlay && Available;
			}
			set
			{
				overlay = value;
			}
		}

		public static void HandleHotkeys()
		{
			if (!Available || Event.current == null)
			{
				return;
			}
			// KeyBindingDef.JustPressed 走的是 Input.GetKeyDown，同一帧内每次都返回 true，
			// 所以必须按帧去重，否则一次按键会被当成按了很多次。
			if (Time.frameCount == lastHotkeyFrame)
			{
				return;
			}
			KeyBindingDef toggleKey = KeyBindingDef.Named(ToggleOverlayKeyDefName);
			if (toggleKey != null && toggleKey.JustPressed)
			{
				lastHotkeyFrame = Time.frameCount;
				overlay = !overlay;
				Log.Message("DreamsOutposts UI: layout overlay " + (overlay ? "on" : "off") + ".");
				return;
			}
			KeyBindingDef dumpKey = KeyBindingDef.Named(DumpLayoutKeyDefName);
			if (dumpKey != null && dumpKey.JustPressed)
			{
				lastHotkeyFrame = Time.frameCount;
				Dump();
			}
		}

		/// <summary>快捷键标签，没绑定时给个明确的说明（叠层提示文字用）。</summary>
		private static string KeyLabel(string defName)
		{
			KeyBindingDef def = KeyBindingDef.Named(defName);
			if (def == null || def.MainKey == KeyCode.None)
			{
				return "unbound";
			}
			return def.MainKeyLabel;
		}

		public static void BeginFrame()
		{
			if (!Available || Event.current == null || Event.current.type != EventType.Repaint)
			{
				return;
			}
			entries.Clear();
			spaces.Clear();
		}

		/// <summary>
		/// 进入一个新的绘制空间（窗口 / 弹窗 / 滚动区）。记录时会把局部坐标换算成屏幕坐标，
		/// 叠层才不会因为「在别的 GUI 组里画的」而偏移。
		/// </summary>
		public static void PushSpace(string name, Vector2 offset)
		{
			Space parent = CurrentSpace;
			Space space = default(Space);
			space.Name = name;
			space.Offset = new Vector2(parent.Offset.x + offset.x, parent.Offset.y + offset.y);
			spaces.Add(space);
		}

		public static void PopSpace()
		{
			if (spaces.Count > 0)
			{
				spaces.RemoveAt(spaces.Count - 1);
			}
		}

		private static Space CurrentSpace
		{
			get
			{
				return (spaces.Count > 0) ? spaces[spaces.Count - 1] : default(Space);
			}
		}

		/// <summary>局部坐标 → 屏幕坐标。</summary>
		private static Rect ToScreen(Rect rect)
		{
			Space space = CurrentSpace;
			return new Rect(rect.x + space.Offset.x, rect.y + space.Offset.y, rect.width, rect.height);
		}

		/// <summary>屏幕坐标 → 当前空间的局部坐标（叠层当前在哪个窗口里画，就用哪个空间）。</summary>
		private static Rect FromScreen(Rect rect)
		{
			Space space = CurrentSpace;
			return new Rect(rect.x - space.Offset.x, rect.y - space.Offset.y, rect.width, rect.height);
		}

		public static void Record(string name, Rect rect)
		{
			if (!Available || !overlay || entries.Count >= MaxEntries)
			{
				return;
			}
			Entry entry = default(Entry);
			entry.Name = name;
			entry.Space = CurrentSpace.Name;
			entry.Rect = rect;
			entry.ScreenRect = ToScreen(rect);
			entries.Add(entry);
		}

		/// <summary>记录并返回同一个 rect，方便直接嵌在绘制代码里。</summary>
		public static Rect Scope(string name, Rect rect)
		{
			Record(name, rect);
			return rect;
		}

		public static void DrawOverlay()
		{
			if (!Overlay)
			{
				return;
			}
			for (int i = 0; i < entries.Count; i++)
			{
				Entry entry = entries[i];
				Rect rect = FromScreen(entry.ScreenRect);
				Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, rect.width, 1f), UiPalette.Brand);
				Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), UiPalette.Brand);
				Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, 1f, rect.height), UiPalette.Brand);
				Widgets.DrawBoxSolid(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), UiPalette.Brand);
				string label = entry.Name + " " + Mathf.RoundToInt(rect.width) + "x" + Mathf.RoundToInt(rect.height);
				UiText.Draw(new Rect(rect.x + 2f, rect.y + 1f, 320f, UiText.LineHeight(UiFont.Body)), label, UiFont.Body, UiPalette.Brand);
			}
			Vector2 mouse = Event.current.mousePosition;
			string info = "ui " + Mathf.RoundToInt(mouse.x) + "," + Mathf.RoundToInt(mouse.y) + " | boxes " + entries.Count
				+ " | overlay: " + KeyLabel(ToggleOverlayKeyDefName) + ", dump: " + KeyLabel(DumpLayoutKeyDefName);
			Rect infoRect = new Rect(4f, 2f, Mathf.Min(700f, UI.screenWidth - 8f), UiText.LineHeight(UiFont.Body) + 2f);
			Widgets.DrawBoxSolid(infoRect, new Color(1f, 1f, 1f, 0.85f));
			UiText.Draw(infoRect, info, UiFont.Body, UiPalette.Ink, TextAnchor.MiddleLeft);
		}

		public static void Dump()
		{
			StringBuilder builder = new StringBuilder();
			builder.Append("DreamsOutposts UI layout dump (").Append(entries.Count).Append(" boxes, all rects in screen coordinates)");
			for (int i = 0; i < entries.Count; i++)
			{
				Entry entry = entries[i];
				builder.Append("\n  [").Append(i).Append("] ").Append(entry.Name)
					.Append(" space=").Append(string.IsNullOrEmpty(entry.Space) ? "-" : entry.Space)
					.Append(" x=").Append(Mathf.RoundToInt(entry.ScreenRect.x))
					.Append(" y=").Append(Mathf.RoundToInt(entry.ScreenRect.y))
					.Append(" w=").Append(Mathf.RoundToInt(entry.ScreenRect.width))
					.Append(" h=").Append(Mathf.RoundToInt(entry.ScreenRect.height));
			}
			if (entries.Count == 0)
			{
				builder.Append("\n  (叠层未开启或这一帧没有记录；先在「选项 → 快捷键 → 游戏」里绑定并按下「"
					+ ToggleOverlayKeyDefName + "」)");
			}
			Log.Message(builder.ToString());
		}
	}
}
