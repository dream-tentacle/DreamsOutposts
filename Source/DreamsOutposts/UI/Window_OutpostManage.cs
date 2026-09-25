using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace DreamsOutposts
{
	/// <summary>
	/// 据点「管理」窗口。
	/// 自绘圆角面板 + 阴影 + 左侧悬浮导航 + 内容区（页头 / 细滚动条）。
	/// </summary>
	public class Window_OutpostManage : Window
	{
		private readonly Outpost outpost;

		private readonly List<OutpostManagePage> pages;

		private readonly OutpostUiCache cache;

		private int selectedIndex;

		/// <summary>每个页签的方块缩放进度：1 = 选中态大小。只影响绘制，不参与布局。</summary>
		private float[] navProgress;

		/// <summary>上一帧的时间锚点：同一帧多次调用时 delta 为 0，动画不会加速。</summary>
		private float navAnimTime;

		/// <summary>现代科技风侧栏绿色游标的当前中心 Y。</summary>
		private float navIndicatorY;

		/// <summary>SmoothDamp 使用的速度状态。</summary>
		private float navIndicatorVelocity;

		/// <summary>首次绘制时直接落在当前页，之后切页才进行滑行动画。</summary>
		private bool navIndicatorInitialized;

		/// <summary>正文淡入的起始时刻；负无穷表示已淡入完成。</summary>
		private float contentFadeStart = float.NegativeInfinity;

		private Vector2 contentScroll;

		private Window_OutpostModal modal;

		private bool installOnlyAvailable;

		/// <summary>暂停键的边沿锁：同一帧的 Layout / Repaint 几趟只结算一次。</summary>
		private bool pauseKeyLatched;

		/// <summary>当前打开的管理窗口（DevMode 测试数据指令会用它取据点）。</summary>
		public static Window_OutpostManage Current { get; private set; }

		public OutpostUiCache Cache => cache;

		public Outpost Outpost => outpost;

		public override Vector2 InitialSize
		{
			get
			{
				// 整体管理界面宽度使用屏幕的 80%，高度继续保持接近全屏。
				float width = Mathf.Min(
					Mathf.Max(UI.screenWidth * 0.8f, 560f),
					Mathf.Max(UI.screenWidth - UiMetrics.WindowScreenMarginH * 2f, 560f));
				float height = Mathf.Min(
					Mathf.Max(UI.screenHeight - UiMetrics.WindowScreenMarginV * 2f, 400f),
					UI.screenHeight);
				// 现代风背景图只按面板高度等比铺满，窗口比图更宽时右侧会露出底色；
				// 这里把宽度压到「面板高 × 图片宽高比」以内，保证任何分辨率下图片都能横向铺满。
				float cappedWidth = MaxWidthForBackground(width, height);
				return new Vector2(Mathf.Round(cappedWidth), Mathf.Round(height));
			}
		}

		protected override float Margin => 0f;

		/// <summary>
		/// 按现代风背景图的宽高比收窄窗口宽度：面板高（contracted 后）乘以图片比例就是图片横向铺满时的宽度。
		/// 原版风不画背景图，或图片缺失时，宽度原样返回。
		/// </summary>
		private static float MaxWidthForBackground(float width, float height)
		{
			if (DreamsOutpostsMod.IsUiStyle(OutpostUiStyle.Vanilla))
			{
				return width;
			}
			Texture2D background = UiTex.BackgroundTexture();
			if (background == null || background.width <= 0 || background.height <= 0)
			{
				return width;
			}
			float panelHeight = height - UiMetrics.WindowShadowMargin * 2f;
			if (panelHeight <= 0f)
			{
				return width;
			}
			return Mathf.Min(width, panelHeight * (float)background.width / background.height);
		}

		public Window_OutpostManage(Outpost outpost)
		{
			this.outpost = outpost;
			pages = OutpostManagePageRegistry.BuildPages(outpost);
			for (int i = 0; i < pages.Count; i++)
			{
				pages[i].hostWindow = this;
			}
			cache = new OutpostUiCache(outpost);
			navProgress = new float[pages.Count];
			if (pages.Count > 0)
			{
				navProgress[0] = 1f;
			}
			navAnimTime = Time.realtimeSinceStartup;
			Current = this;
			doWindowBackground = false;
			drawShadow = false;
			doCloseX = false;
			doCloseButton = false;
			closeOnClickedOutside = true;
			absorbInputAroundWindow = true;
			if (pages.Count > 0)
			{
				pages[0].OnOpen();
			}
		}

		protected override void SetInitialSizeAndPosition()
		{
			Vector2 size = InitialSize;
			windowRect = new Rect(Mathf.Round((UI.screenWidth - size.x) * 0.5f), Mathf.Round((UI.screenHeight - size.y) * 0.5f), Mathf.Round(size.x), Mathf.Round(size.y));
		}

		public override void DoWindowContents(Rect inRect)
		{
			UiDebug.HandleHotkeys();
			UiDebug.BeginFrame();
			if (outpost == null || outpost.Destroyed)
			{
				Close();
				return;
			}
			cache.Refresh();
			// 告诉 UiDebug 这段内容画在哪个窗口的坐标空间里（叠层要按屏幕坐标落位才不会偏移）
			UiDebug.PushSpace("manage", windowRect.position);
			// 窗口矩形比面板大出阴影留白，面板要内缩，否则阴影会被 GUI 裁剪掉
			Rect panel = inRect.ContractedBy(UiMetrics.WindowShadowMargin);
			UiDebug.Scope("window.panel", panel);
			DrawWindowBackground(panel);
			if (pages.Count == 0)
			{
				UiText.Draw(new Rect(panel.x + UiMetrics.ContentPaddingH, panel.y + UiMetrics.ContentPaddingTop, panel.width - UiMetrics.ContentPaddingH * 2f, 30f),
					"DreamsOutposts.NoManagementPage".Translate(), UiFont.Body, UiPalette.Ink);
				UiDebug.PopSpace();
				return;
			}
			int index = Mathf.Clamp(selectedIndex, 0, pages.Count - 1);
			if (DreamsOutpostsMod.IsUiStyle(OutpostUiStyle.Vanilla))
			{
				DrawVanillaTabsAndPage(panel, pages[index]);
			}
			else
			{
				float sidebarWidth = Mathf.Min(UiMetrics.SidebarWidth, Mathf.Max(panel.width * 0.32f, 120f));
				DrawSidebar(new Rect(panel.x, panel.y, sidebarWidth, panel.height));
				Rect contentRect = new Rect(panel.x + sidebarWidth, panel.y, Mathf.Max(panel.width - sidebarWidth, 40f), panel.height);
				DrawPage(contentRect, pages[index]);
			}
			UiDebug.DrawOverlay();
			UiDebug.PopSpace();
		}

		/// <summary>
		/// 本窗口设了 absorbInputAroundWindow，于是 WindowStack.GetsInput(null) 为 false，
		/// 原版 UIRoot.UIRootOnGUI() → WindowStack.HandleEventsHighPriority() 会把每一个 KeyDown
		/// 事件在这个阶段就 Use() 掉；而暂停键的处理在后面的 MainButtonsRoot → TimeControls.DoTimeControlsGUI
		/// 里（那里开头就 `if (Event.current.type != EventType.KeyDown) return;`），所以永远收不到。
		/// 结果就是页面开着时空格完全没反应，2/3/4 加速键也一样。
		/// 这里在窗口自己这一层把暂停键补回来：只补这一个键，其余输入照旧被窗口挡住。
		/// </summary>
		public override void ExtraOnGUI()
		{
			base.ExtraOnGUI();
			// KeyBindingDef.IsDown 走 Input.GetKey，不受 Event.current.Use() 影响；顺带跳过搜索框聚焦的情况
			if (!KeyBindingDefOf.TogglePause.IsDown)
			{
				pauseKeyLatched = false;
				return;
			}
			if (pauseKeyLatched)
			{
				return;
			}
			// 先上锁再判断：这一下就算被弹窗吃掉，也不能等弹窗关掉后再补发一次
			pauseKeyLatched = true;
			// 自己的弹窗（安装 / 事件 / 拆除）压在上面时保持模态，不抢键
			if (!Find.WindowStack.GetsInput(this))
			{
				return;
			}
			Find.TickManager.TogglePaused();
			PlayPauseSound(Find.TickManager.CurTimeSpeed);
			PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.Pause, KnowledgeAmount.SpecificInteraction);
		}

		/// <summary>TimeControls.PlaySoundOf 是 private，这里照抄一份，让空格的手感和原版时间按钮一致。</summary>
		private static void PlayPauseSound(TimeSpeed speed)
		{
			SoundDef sound = null;
			switch (speed)
			{
			case TimeSpeed.Paused:
				sound = SoundDefOf.Clock_Stop;
				break;
			case TimeSpeed.Normal:
				sound = SoundDefOf.Clock_Normal;
				break;
			case TimeSpeed.Fast:
				sound = SoundDefOf.Clock_Fast;
				break;
			case TimeSpeed.Superfast:
			case TimeSpeed.Ultrafast:
				sound = SoundDefOf.Clock_Superfast;
				break;
			}
			sound?.PlayOneShotOnCamera();
			Verse.Steam.SteamDeck.Vibrate();
		}

		public override void PostClose()
		{
			base.PostClose();
			if (Current == this)
			{
				Current = null;
			}
			if (modal != null && Find.WindowStack.IsOpen(modal))
			{
				modal.Close(false);
				modal = null;
			}
			if (selectedIndex >= 0 && selectedIndex < pages.Count)
			{
				pages[selectedIndex].OnClose();
			}
		}

		private static void DrawWindowBackground(Rect rect)
		{
			// 原版风格直接使用 RimWorld 自带窗口背景。
			if (DreamsOutpostsMod.IsUiStyle(OutpostUiStyle.Vanilla))
			{
				Widgets.DrawWindowBackground(rect);
				return;
			}

			// 现代风：无背景图时 Surface 精确为 #D1CFCB；
			// 有背景图时仍按高度铺满，右侧溢出裁切。
			Texture2D background = UiTex.BackgroundTexture();
			UiDraw.Solid(rect, UiPalette.Surface);

			if (background != null && background.width > 0 && background.height > 0)
			{
				float scale = rect.height / background.height;
				float width = background.width * scale;

				GUI.BeginGroup(rect);
				GUI.DrawTexture(
					new Rect(0f, 0f, width, rect.height),
					background,
					ScaleMode.StretchToFill,
					true);
				GUI.EndGroup();
			}

			UiDraw.Solid(rect, UiPalette.BackgroundVeil);
		}

		// ---------------------------------------------------------------
		// 原版顶部页签 / 现代风侧栏
		// ---------------------------------------------------------------

		/// <summary>
		/// 原版风格直接使用 RimWorld 自带的 TabDrawer / TabRecord。
		/// 页签占据窗口顶部，正文从页签下方开始，并自动处理页签过多时的换行高度。
		/// </summary>
		private void DrawVanillaTabsAndPage(Rect panel, OutpostManagePage page)
		{
			List<TabRecord> tabs = new List<TabRecord>(pages.Count);
			for (int i = 0; i < pages.Count; i++)
			{
				int pageIndex = i;
				OutpostManagePage tabPage = pages[i];
				tabs.Add(new TabRecord(tabPage.Label, delegate
				{
					SelectPage(pageIndex);
				}, pageIndex == selectedIndex));
			}

			// 原版风：先画一个独立的系统标题栏，再把原版页签放到标题栏下方。
			// 标题与关闭按钮共享同一个 MenuSection，因此视觉上仍是 RimWorld 原版窗口语言。
			const float titleBarHeight = 54f;
			const float titleBarGap = 8f;
			Rect titleBarRect = new Rect(panel.x, panel.y, panel.width, titleBarHeight);
			Widgets.DrawWindowBackground(titleBarRect);

			GameFont previousFont = Text.Font;
			TextAnchor previousAnchor = Text.Anchor;
			Text.Font = GameFont.Medium;
			Text.Anchor = TextAnchor.MiddleLeft;
			Rect titleRect = new Rect(titleBarRect.x + 16f, titleBarRect.y,
				Mathf.Max(titleBarRect.width - 54f, 40f), titleBarRect.height);
			Color previousColor = GUI.color;
			GUI.color = Color.white;
			Widgets.Label(titleRect, "DreamsOutposts.Ui.ManagementSystem".Translate());
			GUI.color = previousColor;
			Text.Font = previousFont;
			Text.Anchor = previousAnchor;

			UiDebug.Scope("page.close", titleBarRect);
			if (Widgets.CloseButtonFor(titleBarRect))
			{
				Close();
			}

			Rect tabsRect = panel;
			tabsRect.yMin = titleBarRect.yMax + titleBarGap;
			float tabsHeight = TabDrawer.GetOverflowTabHeight(tabsRect, tabs, 100f, 200f);
			Rect contentRect = tabsRect;
			contentRect.yMin += tabsHeight;
			TabDrawer.DrawTabsOverflow(tabsRect, tabs, 100f, 200f);
			DrawPage(contentRect, page);
		}

		private void DrawSidebar(Rect rect)
		{
			UiDebug.Scope("window.sidebar", rect);
			UiDraw.Solid(rect, UiPalette.SidebarVeil);
			UiDraw.Divider(
				new Rect(rect.xMax - 1f, rect.y, 1f, rect.height),
				UiPalette.Line);

			EnsureNavProgress();

			// 按真实时间推进动画，与游戏暂停无关。
			float now = Time.realtimeSinceStartup;
			float delta = Mathf.Max(now - navAnimTime, 0f);
			navAnimTime = now;

			float step = (UiMetrics.NavTileAnimSeconds > 0f)
				? delta / UiMetrics.NavTileAnimSeconds
				: 1f;

			float minScale = 1f - UiMetrics.NavInactiveShrink;

			float x = rect.x + UiMetrics.SidebarPaddingLeft;
			float width =
				rect.width -
				UiMetrics.SidebarPaddingLeft -
				UiMetrics.SidebarPaddingH;

			float y = rect.y + UiMetrics.SidebarPaddingV;
			float firstItemY = y;
			float itemHeight = UiWidgets.NavItemHeight();

			bool modernTech =
				DreamsOutpostsMod.UsesModernTechLayout;

			if (!modernTech)
			{
				navIndicatorInitialized = false;
				navIndicatorVelocity = 0f;
			}

for (int i = 0; i < pages.Count; i++)
			{
				OutpostManagePage page = pages[i];
				UiIcon icon =
					UiIconMap.ForPage(
						page.def,
						page.GetType());

				string summary =
					UiPageSummary.For(page, outpost);

				int badge =
					UiPageSummary.BadgeFor(page, outpost);

				Rect itemRect = new Rect(
					x,
					y,
					width,
					itemHeight);

				UiDebug.Scope(
					"sidebar.item[" + i + "]",
					itemRect);

				// 同一个 progress 同时驱动：
				// 1) 页签原有缩放；
				// 2) 黑色选中底从左向右推出 / 从右向左收回。
				navProgress[i] = Mathf.MoveTowards(
					navProgress[i],
					(i == selectedIndex) ? 1f : 0f,
					step);

				float eased =
					navProgress[i] *
					navProgress[i] *
					(3f - 2f * navProgress[i]);

				float scale =
					Mathf.Lerp(
						minScale,
						1f,
						eased);

				if (UiWidgets.NavItem(
					itemRect,
					icon,
					page.Label,
					summary,
					i == selectedIndex,
					badge,
					page.Tooltip,
					scale,
					eased))
				{
					SelectPage(i);
				}

				y += itemHeight + UiMetrics.SidebarGap;
			}

			// 轨道游标最后绘制，始终压在线条和页签之上。
			if (modernTech &&
				pages.Count > 0 &&
				selectedIndex >= 0 &&
				selectedIndex < pages.Count)
			{
				float targetY =
					firstItemY +
					selectedIndex *
						(itemHeight + UiMetrics.SidebarGap) +
					itemHeight * 0.5f;

				if (!navIndicatorInitialized)
				{
					navIndicatorY = targetY;
					navIndicatorVelocity = 0f;
					navIndicatorInitialized = true;
				}
				else if (delta > 0f)
				{
					navIndicatorY = Mathf.SmoothDamp(
						navIndicatorY,
						targetY,
						ref navIndicatorVelocity,
						UiMetrics.ModernTechNavIndicatorSmoothTime,
						Mathf.Infinity,
						delta);

					if (Mathf.Abs(navIndicatorY - targetY) < 0.01f &&
						Mathf.Abs(navIndicatorVelocity) < 0.01f)
					{
						navIndicatorY = targetY;
						navIndicatorVelocity = 0f;
					}
				}

				float indicatorHeight =
					UiMetrics.ModernTechNavIndicatorHeight;

				Texture2D selectorTexture =
					ContentFinder<Texture2D>.Get(
						"DreamsOutposts/Ui/Selector",
						false);

				Rect indicatorRect = new Rect(
					rect.x,
					navIndicatorY - indicatorHeight * 0.5f,
					UiMetrics.ModernTechNavIndicatorWidth,
					UiMetrics.ModernTechNavIndicatorHeight);

				if (selectorTexture != null)
				{
					Color previous = GUI.color;
					Color tint = UiPalette.Brand;

					GUI.color = new Color(
						previous.r * tint.r,
						previous.g * tint.g,
						previous.b * tint.b,
						previous.a * tint.a);

					GUI.DrawTexture(
						indicatorRect,
						selectorTexture,
						ScaleMode.ScaleToFit,
						true);

					GUI.color = previous;
				}

UiDebug.Scope(
					"sidebar.moderntech.indicator",
					indicatorRect);
			}

			if (Event.current.type == EventType.ScrollWheel &&
				Mouse.IsOver(rect) &&
				pages.Count > 1)
			{
				int direction =
					(Event.current.delta.y > 0f)
						? 1
						: -1;

				SelectPage(
					Mathf.Clamp(
						selectedIndex + direction,
						0,
						pages.Count - 1));

				Event.current.Use();
			}
		}

		/// <summary>兜底初始化：打开时选中页直接是选中态大小。</summary>
		private void EnsureNavProgress()
		{
			if (navProgress != null && navProgress.Length == pages.Count)
			{
				return;
			}
			navProgress = new float[pages.Count];
			for (int i = 0; i < navProgress.Length; i++)
			{
				navProgress[i] = (i == selectedIndex) ? 1f : 0f;
			}
		}

		private void SelectPage(int index)
		{
			if (index == selectedIndex || index < 0 || index >= pages.Count)
			{
				return;
			}
			if (selectedIndex >= 0 && selectedIndex < pages.Count)
			{
				pages[selectedIndex].OnClose();
			}
			selectedIndex = index;
			pages[selectedIndex].OnOpen();
			// 原版页签自己会播放 RowTabSelect；现代风保留原有的切页出现音。
			if (!DreamsOutpostsMod.IsUiStyle(OutpostUiStyle.Vanilla))
			{
				SoundDefOf.DialogBoxAppear.PlayOneShotOnCamera();
			}
			// 原版风格直接切页；现代风保留正文淡入/滑入动画。
			if (!DreamsOutpostsMod.IsUiStyle(OutpostUiStyle.Vanilla))
			{
				contentFadeStart = Time.realtimeSinceStartup;
			}
		}

		/// <summary>
		/// 切到指定类型的页面，并先把该页配置好（训练设施卡片上的跳转按钮用它）。
		/// 该页本来就是当前页时 SelectPage 会直接返回，但配置已经生效，下一帧就按新配置重画。
		/// </summary>
		public void OpenPageOfType(Type pageClass, Action<OutpostManagePage> configure)
		{
			if (pageClass == null || pages == null)
			{
				return;
			}
			for (int i = 0; i < pages.Count; i++)
			{
				if (pages[i] == null || pages[i].GetType() != pageClass)
				{
					continue;
				}
				configure?.Invoke(pages[i]);
				SelectPage(i);
				return;
			}
		}

		/// <summary>
		/// 跳到仓库页，并把殖民者栏的技能筛选设成给定技能（null 表示「不显示」）。
		/// 管理窗口没开着（或该据点没有仓库页）时静默返回。
		/// </summary>
		public static void JumpToWarehouse(SkillDef skill)
		{
			Window_OutpostManage shell = FindManageWindow();
			if (shell == null)
			{
				return;
			}
			shell.OpenPageOfType(typeof(Page_OutpostInventory), delegate(OutpostManagePage page)
			{
				Page_OutpostInventory inventory = page as Page_OutpostInventory;
				if (inventory != null)
				{
					inventory.SetSkillFilter(skill);
				}
			});
		}

		private static Window_OutpostManage FindManageWindow()
		{
			IList<Window> windows = Find.WindowStack?.Windows;
			if (windows == null)
			{
				return null;
			}
			for (int i = 0; i < windows.Count; i++)
			{
				Window_OutpostManage manage = windows[i] as Window_OutpostManage;
				if (manage != null)
				{
					return manage;
				}
			}
			return null;
		}

		// ---------------------------------------------------------------
		// 内容区
		// ---------------------------------------------------------------

		private void DrawPage(Rect rect, OutpostManagePage page)
		{
			UiDebug.Scope("window.content", rect);
			bool vanilla = DreamsOutpostsMod.IsUiStyle(OutpostUiStyle.Vanilla);
			if (!vanilla)
			{
				UiDraw.Solid(rect, UiPalette.ContentVeil);
			}
			IUiShellPage shellPage = (IUiShellPage)page;
			float systemHeadTop = rect.y + UiMetrics.ContentPaddingTop;
			float pageHeadTop = vanilla ? systemHeadTop : systemHeadTop + UiMetrics.ManagementHeaderHeight;

			// 全屏时不让正文无限变宽：从左侧开始排版，右侧多出的区域保留为背景呼吸空间。
float availableContentWidth = Mathf.Max(rect.width - UiMetrics.ContentPaddingH * 2f, 60f);
float contentWidth = vanilla ? availableContentWidth : Mathf.Min(availableContentWidth, UiMetrics.ContentMaxWidth);
float contentX = rect.x + UiMetrics.ContentPaddingH;

// 系统级页头：原版风格省略；现代风保留现有管理系统标题与信息。
			if (!vanilla)
			{
				UiText.Draw(
					new Rect(contentX, systemHeadTop, DreamsOutpostsMod.UsesModernTechLayout ? contentWidth * 0.60f : contentWidth, UiText.LineHeight(UiFont.Heading)),
					"DreamsOutposts.Ui.ManagementSystem".Translate(),
					UiFont.Heading,
					UiPalette.Ink,
					TextAnchor.UpperLeft,
					true, false, true);
			}

			if (DreamsOutpostsMod.UsesModernTechLayout)
			{
				UiDraw.Solid(new Rect(contentX, systemHeadTop + 39f, contentWidth, 1f), UiPalette.LineStrong);
				string indexLabel = "DreamsOutposts.Ui.OutpostIndex".Translate(
					Mathf.Max(outpost.Tile.tileId, 0).ToString("D4")).ToString();
				float indexX = contentX + contentWidth * 0.64f;
				float indexRight = Mathf.Min(contentX + contentWidth,
					rect.xMax - UiMetrics.ContentPaddingH - UiMetrics.CloseButtonSize - 16f);
				UiText.Draw(new Rect(indexX, systemHeadTop, Mathf.Max(indexRight - indexX, 1f),
					UiText.LineHeight(UiFont.Heading)), indexLabel, UiFont.Body, UiPalette.Ink2,
					TextAnchor.MiddleRight, false, false, true);
			}

			// 原版风的关闭按钮已经提升到“据点管理系统”标题栏；现代风继续固定在系统级页头右上角。
			Rect closeRect = default(Rect);
			if (!vanilla)
			{
				closeRect = new Rect(rect.xMax - UiMetrics.ContentPaddingH - UiMetrics.CloseButtonSize,
					systemHeadTop, UiMetrics.CloseButtonSize, UiMetrics.CloseButtonSize);
				UiDebug.Scope("page.close", closeRect);
				if (UiWidgets.CloseButton(closeRect, "DreamsOutposts.Ui.Close".Translate()))
				{
					Close();
				}
			}

			float pageHeadWidth = vanilla
				? contentWidth
				: Mathf.Max(
					Mathf.Min(contentWidth, closeRect.x - UiMetrics.ButtonGap - contentX),
					40f);
			float headHeight = DrawPageHead(
				new Rect(contentX, pageHeadTop, pageHeadWidth, 0f),
				page, shellPage);

			float scrollTop = pageHeadTop + headHeight;
			Rect scrollArea = new Rect(
				contentX,
				scrollTop,
				contentWidth,
				Mathf.Max(rect.yMax - UiMetrics.ContentPaddingBottom - scrollTop, 20f));
			float bodyWidth = vanilla
				? Mathf.Max(scrollArea.width, 60f)
				: Mathf.Max(scrollArea.width - UiMetrics.ScrollbarGutter, 60f);
			float bodyHeight = shellPage.BodyHeight(bodyWidth, scrollArea.height);
			int scrollId = GetHashCode();
			// 切页动画：正文从右侧滑入 + 淡入，两者共用同一个进度
			float fade = ContentFade();

			// 真正淡入：直接让新页面正文的 GUI alpha 从 0 -> 1。
			// 背景、系统页头、关闭按钮和左侧页签都在这个作用域之外，因此保持稳定。
			Color previousContentColor = GUI.color;
			GUI.color = new Color(
				previousContentColor.r,
				previousContentColor.g,
				previousContentColor.b,
				previousContentColor.a * fade);

			UiWidgets.ScrollView(scrollArea, ref contentScroll, bodyHeight, delegate(Rect contentRect)
			{
				float actualBodyWidth = vanilla ? contentRect.width : bodyWidth;
				shellPage.DrawBody(new Rect(contentRect.x, contentRect.y, actualBodyWidth, contentRect.height), scrollArea.height);
			}, true, scrollId, true, UiMetrics.ContentSlideDistance * (1f - fade));

			GUI.color = previousContentColor;
		}

		/// <summary>正文切页动画的进度：1 = 完全到位（位移归零、不透明）。切页时从 0 开始；首次打开窗口直接是 1。</summary>
		private float ContentFade()
		{
			if (DreamsOutpostsMod.IsUiStyle(OutpostUiStyle.Vanilla))
			{
				return 1f;
			}
			if (UiMetrics.ContentFadeSeconds <= 0f || float.IsNegativeInfinity(contentFadeStart))
			{
				return 1f;
			}
			float t = Mathf.Clamp01((Time.realtimeSinceStartup - contentFadeStart) / UiMetrics.ContentFadeSeconds);
			// 先快后慢，淡入结束时更干净
			return 1f - (1f - t) * (1f - t);
		}

		private float DrawPageHead(Rect rect, OutpostManagePage page, IUiShellPage shellPage)
		{
			float y = rect.y;
			float titleHeight = UiText.LineHeight(UiFont.Heading);
			string description = shellPage.HeadDescription;
			string hint = shellPage.HeadHint;
			string body = null;
			if (!string.IsNullOrEmpty(description) && !string.IsNullOrEmpty(hint))
			{
				body = description + " " + hint;
			}
			else if (!string.IsNullOrEmpty(description))
			{
				body = description;
			}
			else if (!string.IsNullOrEmpty(hint))
			{
				body = hint;
			}
			float textWidth = DreamsOutpostsMod.IsUiStyle(OutpostUiStyle.Vanilla)
				? rect.width
				: Mathf.Min(rect.width, UiMetrics.PageHeadMaxTextWidth);
			float descriptionHeight = !string.IsNullOrEmpty(body) ? UiText.Height(body, UiFont.Body, textWidth) : 0f;
			if (DreamsOutpostsMod.IsUiStyle(OutpostUiStyle.Vanilla))
			{
				// 原版页签已经承担页面标题职责；正文不再重复显示“仓库 / 防卫 / 事件”等大标题。
				if (string.IsNullOrEmpty(body))
				{
					return 4f;
				}
				float totalVanilla = descriptionHeight + 10f;
				UiDebug.Scope("page.head", new Rect(rect.x, rect.y, rect.width, totalVanilla));
				UiText.Draw(new Rect(rect.x, y, textWidth, descriptionHeight), body,
					UiFont.Body, UiPalette.Ink2, TextAnchor.UpperLeft, false, true);
				return totalVanilla;
			}

			float total = titleHeight + UiMetrics.PageHeadSubGap + descriptionHeight + UiMetrics.PageHeadMarginBottom;
			UiDebug.Scope("page.head", new Rect(rect.x, rect.y, rect.width, total));
			// 现代风页面标题：竖直强调条 + 标题。
			float barHeight = Mathf.Max(titleHeight - 4f, 18f);
			UiDraw.Solid(new Rect(rect.x, rect.y + (titleHeight - barHeight) * 0.5f, 3f, barHeight), UiPalette.Brand);
			float titleX = rect.x + 14f + UiMetrics.PageHeadTitleIndent;
			UiText.Draw(new Rect(titleX, y, Mathf.Max(rect.xMax - titleX, 40f), titleHeight), page.Label, UiFont.Heading, UiPalette.Ink, TextAnchor.UpperLeft, true);
			y += titleHeight + UiMetrics.PageHeadSubGap;
			if (!string.IsNullOrEmpty(body))
			{
				UiText.Draw(new Rect(rect.x, y, textWidth, descriptionHeight), body, UiFont.Body, UiPalette.Ink2, TextAnchor.UpperLeft, false, true);
			}
			return total;
		}

		/// <summary>页头左侧的装饰底图：贴齐页头区域左边缘，按高度等比缩放。</summary>
		private static void DrawPageHeadDecor(Rect headRect)
		{
			Texture2D decor = UiTex.PageHeadDecorTexture();
			if (decor == null || decor.height <= 0 || headRect.height <= 0f)
			{
				return;
			}
			float width = headRect.height * decor.width / decor.height;
			GUI.DrawTexture(new Rect(headRect.x, headRect.y, width, headRect.height), decor, ScaleMode.StretchToFill, true);
		}

		// ---------------------------------------------------------------
		// 弹窗
		// ---------------------------------------------------------------

		public void OpenDetailsModal(UiFacilityView view)
		{
			UiDetailsView details = cache.BuildDetails(view);
			if (details == null)
			{
				return;
			}
			Window_OutpostModal window = new Window_OutpostModal();
			window.TitleText = details.Title;
			window.SubText = details.Subtitle;
			window.PanelWidth = UiMetrics.ModalWideWidth;
			window.Body = DreamsOutpostsMod.IsUiStyle(OutpostUiStyle.Vanilla)
				? (IUiModalBody)new UiVanillaFacilityDetailsModalBody(details)
				: new UiDetailsModalBody(details);
			window.FooterDrawer = delegate(Rect footerRect)
			{
				DrawDetailsFooter(footerRect, details);
			};
			ShowModal(window);
		}

		public void OpenInstallModal(OutpostSlot slot, int slotIndex)
		{
			Window_OutpostModal window = new Window_OutpostModal();
			window.TitleText = "DreamsOutposts.InstallFacility".Translate();
			window.SubText = "DreamsOutposts.Ui.Install.Sub".Translate(slotIndex + 1);
			window.PanelWidth = UiMetrics.ModalWideWidth;
			UiInstallModalBody body = new UiInstallModalBody(this, slot);
			window.Body = body;
			// 固定大小：一律撑到屏幕允许的最大，不随内容变化
			window.UseMaxHeight = true;
			window.FooterDrawer = DrawInstallFooter;
			ShowModal(window);
		}

		public void OpenDemolishModal(UiFacilityView view)
		{
			if (view == null)
			{
				return;
			}
			Window_OutpostModal window = new Window_OutpostModal();
			window.TitleText = "DreamsOutposts.Demolish".Translate();
			// 标题下不重复设施名（正文里已有「拆除X？」）
			window.PanelWidth = UiMetrics.ModalNarrowWidth;
			window.Body = new UiDemolishModalBody(view);
			window.FooterDrawer = delegate(Rect footerRect)
			{
				DrawDemolishFooter(footerRect, view);
			};
			ShowModal(window);
		}

		public void OpenAutomaticAirdropModal()
		{
			Window_OutpostModal window = new Window_OutpostModal();
			window.TitleText = "DreamsOutposts.AutomaticAirdropSettings".Translate();
			window.PanelWidth = UiMetrics.ModalNarrowWidth;
			window.Body = new UiAutomaticAirdropModalBody(outpost);
			window.FooterDrawer = delegate(Rect footerRect)
			{
				float buttonHeight = UiWidgets.ButtonHeight(UiButtonSize.Normal);
				string closeLabel = "DreamsOutposts.Ui.Close".Translate();
				float closeWidth = UiWidgets.ButtonWidth(closeLabel);
				Rect closeRect = new Rect(footerRect.xMax - closeWidth,
					footerRect.y + (footerRect.height - buttonHeight) * 0.5f, closeWidth, buttonHeight);
				if (UiWidgets.Button(closeRect, closeLabel))
				{
					CloseModal();
				}
			};
			ShowModal(window);
		}

		/// <summary>打开事件弹窗（选项面板）。确认后按玩家选择结算。</summary>
		public void OpenEventModal(UiEventView view)
		{
			if (view?.Instance?.def == null)
			{
				return;
			}
			Window_OutpostModal window = new Window_OutpostModal();
			window.TitleText = view.Label;
			window.SubText = "DreamsOutposts.Remaining".Translate(view.RemainingText);
			window.PanelWidth = UiMetrics.ModalWideWidth;
			UiEventModalBody body = new UiEventModalBody(this, view);
			window.Body = body;
			// 固定大小：和安装弹窗一致，一律撑到屏幕允许的最大高度
			window.UseMaxHeight = true;
			window.FooterDrawer = delegate(Rect footerRect)
			{
				DrawEventFooter(footerRect, body, view);
			};
			ShowModal(window);
		}

		private void DrawEventFooter(Rect rect, UiEventModalBody body, UiEventView view)
		{
			float buttonHeight = UiWidgets.ButtonHeight(UiButtonSize.Normal);
			float y = rect.y + (rect.height - buttonHeight) * 0.5f;
			// 确认在左下角（与原型一致），未选中时禁用
			UiEventOptionView selected = body.SelectedOption;
			bool canConfirm = selected != null && selected.Selectable;
			string confirmLabel = "DreamsOutposts.Ui.Option.Confirm".Translate();
			float confirmWidth = UiWidgets.ButtonWidth(confirmLabel);
			Rect confirmRect = new Rect(rect.x, y, confirmWidth, buttonHeight);
			if (UiWidgets.Button(confirmRect, confirmLabel, UiButtonKind.Primary, canConfirm, null, UiButtonSize.Normal))
			{
				OutpostEventUtility.ResolveByPlayer(outpost, view.Instance, selected.Option);
				cache.Invalidate();
				CloseModal();
			}
			string laterLabel = "DreamsOutposts.Ui.Option.Later".Translate();
			float laterWidth = UiWidgets.ButtonWidth(laterLabel);
			Rect laterRect = new Rect(rect.xMax - laterWidth, y, laterWidth, buttonHeight);
			if (UiWidgets.Button(laterRect, laterLabel))
			{
				CloseModal();
			}
		}

		/// <summary>安装弹窗里点「建造」：扣仓库材料、装进槽位、关弹窗。</summary>
		public void TryInstall(OutpostSlot slot, UiInstallCardView card)
		{
			if (slot == null || card?.Def == null)
			{
				return;
			}
			AcceptanceReport report;
			List<Type> pageTypesBefore = PageTypes();
			if (slot.TryInstall(card.Def, outpost, out report))
			{
				cache.Invalidate();
				CloseModal();
				NotifyIfNewPagesAppeared(pageTypesBefore);
			}
		}

		public void TryForceInstall(OutpostSlot slot, UiInstallCardView card)
		{
			if (!DebugSettings.godMode || slot == null || card?.Def == null)
			{
				return;
			}
			List<Type> pageTypesBefore = PageTypes();
			if (slot.TryForceInstall(card.Def))
			{
				cache.Invalidate();
				CloseModal();
				NotifyIfNewPagesAppeared(pageTypesBefore);
			}
		}

		/// <summary>当前页签的类型列表：安装前后各取一次，用来判断有没有多出新页面。</summary>
		private List<Type> PageTypes()
		{
			List<Type> types = new List<Type>();
			for (int i = 0; i < pages.Count; i++)
			{
				if (pages[i] != null)
				{
					types.Add(pages[i].GetType());
				}
			}
			return types;
		}

		/// <summary>
		/// 有些设施建成后才会出现自己的管理页（冒险者营地建成后才出现酒馆页），
		/// 而页签是打开管理窗口时算一次的，所以这里建完立刻重算一遍：
		/// 真的多出新页面就弹提示，让玩家知道要关掉窗口重新打开。
		/// 只在安装设施后调用，平时没有开销；页面没变（绝大多数设施）时什么都不做。
		/// </summary>
		private void NotifyIfNewPagesAppeared(List<Type> before)
		{
			List<OutpostManagePage> rebuilt = OutpostManagePageRegistry.BuildPages(outpost);
			List<string> labels = new List<string>();
			for (int i = 0; i < rebuilt.Count; i++)
			{
				OutpostManagePage page = rebuilt[i];
				if (page == null || (before != null && before.Contains(page.GetType())))
				{
					continue;
				}
				string label = page.Label;
				if (!label.NullOrEmpty() && !labels.Contains(label))
				{
					labels.Add(label);
				}
			}
			if (labels.Count == 0)
			{
				return;
			}
			UiNoticeWindow.Open("DreamsOutposts.Ui.Notice".Translate(),
				"DreamsOutposts.ManagePageReopen".Translate(GenText.ToCommaList(labels, true)));
		}

		public void CloseModal()
		{
			if (modal != null)
			{
				modal.Close();
			}
		}

		public bool InstallOnlyAvailable
		{
			get
			{
				return installOnlyAvailable;
			}
			set
			{
				installOnlyAvailable = value;
			}
		}

		private void ShowModal(Window_OutpostModal window)
		{
			modal = window;
			window.ClosedCallback = delegate
			{
				if (modal == window)
				{
					modal = null;
				}
			};
			Find.WindowStack.Add(window);
		}

		private void DrawDetailsFooter(Rect rect, UiDetailsView details)
		{
			float buttonHeight = UiWidgets.ButtonHeight(UiButtonSize.Normal);
			float y = rect.y + (rect.height - buttonHeight) * 0.5f;
			if (details.Source?.Facility?.def?.automaticAirdropController == true)
			{
				string settingsLabel = "DreamsOutposts.AutomaticAirdropSettings".Translate();
				float settingsWidth = UiWidgets.ButtonWidth(settingsLabel);
				Rect settingsRect = new Rect(rect.x, y, settingsWidth, buttonHeight);
				if (UiWidgets.Button(settingsRect, settingsLabel, UiButtonKind.Primary))
				{
					CloseModal();
					OpenAutomaticAirdropModal();
				}
			}
			if (!details.CanRemove)
			{
				return;
			}
			string demolishLabel = "DreamsOutposts.Demolish".Translate();
			// 与安装弹窗的「建造」按钮同款底图 / 同款尺寸算法，只是底图换成 NegativeButton 并按破坏性语义色染成红色
			Texture2D demolishTexture = UiTex.NegativeButtonTexture();
			float demolishWidth = UiWidgets.TexturedButtonWidth(buttonHeight, demolishTexture, demolishLabel);
			Rect demolishRect = new Rect(rect.xMax - demolishWidth, y, demolishWidth, buttonHeight);
			if (UiWidgets.TexturedButton(demolishRect, demolishLabel, demolishTexture, 420f, 300f,
				UiPalette.Danger, UiPalette.DangerHover, UiPalette.OnAccent,
				UiButtonSize.Normal, details.DemolishTooltip))
			{
				if (details.Source != null)
				{
					OpenDemolishModal(details.Source);
				}
			}
		}

		private void DrawInstallFooter(Rect rect)
		{
			float buttonHeight = UiWidgets.ButtonHeight(UiButtonSize.Normal);
			float y = rect.y + (rect.height - buttonHeight) * 0.5f;
			string onlyLabel = "DreamsOutposts.Ui.Install.OnlyAvailable".Translate();
			float checkboxWidth = Mathf.Min(UiWidgets.CheckboxSize + 8f + UiText.Width(onlyLabel, UiFont.Body) + 8f, rect.width * 0.6f);
			bool value = installOnlyAvailable;
			if (UiWidgets.Checkbox(new Rect(rect.x, y, checkboxWidth, buttonHeight), ref value, onlyLabel))
			{
				installOnlyAvailable = value;
			}
		}

		/// <summary>拆除确认弹窗的底部按钮：真正执行拆除（拆除会把材料返还一半进仓库）。</summary>
		private void DrawDemolishFooter(Rect rect, UiFacilityView view)
		{
			float buttonHeight = UiWidgets.ButtonHeight(UiButtonSize.Normal);
			float y = rect.y + (rect.height - buttonHeight) * 0.5f;
			string cancelLabel = "DreamsOutposts.Ui.Cancel".Translate();
			string demolishLabel = "DreamsOutposts.Demolish".Translate();
			Rect cancelRect;
			Rect demolishRect;
			if (DreamsOutpostsMod.IsUiStyle(OutpostUiStyle.Vanilla))
			{
				float buttonWidth = Mathf.Max((rect.width - UiMetrics.ModalFootGap) * 0.5f, 40f);
				demolishRect = new Rect(rect.x, y, buttonWidth, buttonHeight);
				cancelRect = new Rect(rect.xMax - buttonWidth, y, buttonWidth, buttonHeight);
			}
			else
			{
				float cancelWidth = UiWidgets.ButtonWidth(cancelLabel);
				cancelRect = new Rect(rect.xMax - cancelWidth, y, cancelWidth, buttonHeight);
				float demolishWidth = UiWidgets.ButtonWidth(demolishLabel);
				demolishRect = new Rect(cancelRect.x - UiMetrics.ModalFootGap - demolishWidth, y, demolishWidth, buttonHeight);
			}
			if (UiWidgets.Button(cancelRect, cancelLabel))
			{
				CloseModal();
			}
			bool canRemove = view != null && view.CanRemove && view.Slot != null;
			string tooltip = (view != null) ? view.RemoveTooltipGetter?.Invoke() : null;
			if (UiWidgets.Button(demolishRect, demolishLabel, UiButtonKind.Danger, canRemove,
				canRemove ? null : ((view != null) ? view.RemoveReason : null), UiButtonSize.Normal, tooltip))
			{
				AcceptanceReport report;
				if (view.Slot.TryRemove(outpost, out report))
				{
					// 拆除会把每种材料按建造成本的一半返还进据点仓库（OutpostSlot.TryRemove 内部处理）
					cache.Invalidate();
					CloseModal();
				}
			}
		}
	}
}
