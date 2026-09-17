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
				return new Vector2(Mathf.Round(width), Mathf.Round(height));
			}
		}

		protected override float Margin => 0f;

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
			float sidebarWidth = Mathf.Min(UiMetrics.SidebarWidth, Mathf.Max(panel.width * 0.32f, 120f));
			DrawSidebar(new Rect(panel.x, panel.y, sidebarWidth, panel.height));
			Rect contentRect = new Rect(panel.x + sidebarWidth, panel.y, Mathf.Max(panel.width - sidebarWidth, 40f), panel.height);
			DrawPage(contentRect, pages[index]);
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
			// 原版风格：固定使用 #15191D，不读取也不绘制现代风背景图。
			if (DreamsOutpostsMod.UseVanillaUi)
			{
				UiDraw.Solid(rect, UiPalette.Surface);
				return;
			}

			// 现代风：无背景图时 Surface 精确为 #E6E6E6；
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
		// 侧栏
		// ---------------------------------------------------------------

		private void DrawSidebar(Rect rect)
		{
			UiDebug.Scope("window.sidebar", rect);
			UiDraw.Solid(rect, UiPalette.SidebarVeil);
			UiDraw.Divider(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), UiPalette.Line);
			EnsureNavProgress();
			// 按真实时间推进动画，与暂停无关
			float now = Time.realtimeSinceStartup;
			float delta = Mathf.Max(now - navAnimTime, 0f);
			navAnimTime = now;
			float step = (UiMetrics.NavTileAnimSeconds > 0f) ? delta / UiMetrics.NavTileAnimSeconds : 1f;
			float minScale = 1f - UiMetrics.NavInactiveShrink;
			// 左栏按 SidebarWidth 隐性占位，页签只占其中一部分
			float x = rect.x + UiMetrics.SidebarPaddingLeft;
			float width = rect.width - UiMetrics.SidebarPaddingLeft - UiMetrics.SidebarPaddingH;
			float y = rect.y + UiMetrics.SidebarPaddingV;
			float itemHeight = UiWidgets.NavItemHeight();
			for (int i = 0; i < pages.Count; i++)
			{
				OutpostManagePage page = pages[i];
				UiIcon icon = UiIconMap.ForPage(page.def, page.GetType());
				string summary = UiPageSummary.For(page, outpost);
				int badge = UiPageSummary.BadgeFor(page, outpost);
				Rect itemRect = new Rect(x, y, width, itemHeight);
				UiDebug.Scope("sidebar.item[" + i + "]", itemRect);
				// 进度连续逼近目标，连点切换不会跳变
				navProgress[i] = Mathf.MoveTowards(navProgress[i], (i == selectedIndex) ? 1f : 0f, step);
				float eased = navProgress[i] * navProgress[i] * (3f - 2f * navProgress[i]);
				float scale = Mathf.Lerp(minScale, 1f, eased);
				if (UiWidgets.NavItem(itemRect, icon, page.Label, summary, i == selectedIndex, badge, page.Tooltip, scale))
				{
					SelectPage(i);
				}
				y += itemHeight + UiMetrics.SidebarGap;
			}
			// 侧栏上滚滚轮切页
			if (Event.current.type == EventType.ScrollWheel && Mouse.IsOver(rect) && pages.Count > 1)
			{
				int direction = (Event.current.delta.y > 0f) ? 1 : -1;
				SelectPage(Mathf.Clamp(selectedIndex + direction, 0, pages.Count - 1));
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
			// 与打开管理窗口时同一个出现音
			SoundDefOf.DialogBoxAppear.PlayOneShotOnCamera();
			// 切页时正文淡入
			contentFadeStart = Time.realtimeSinceStartup;
		}

		// ---------------------------------------------------------------
		// 内容区
		// ---------------------------------------------------------------

		private void DrawPage(Rect rect, OutpostManagePage page)
		{
			UiDebug.Scope("window.content", rect);
			UiDraw.Solid(rect, UiPalette.ContentVeil);
			IUiShellPage shellPage = (IUiShellPage)page;
			float systemHeadTop = rect.y + UiMetrics.ContentPaddingTop;
			float pageHeadTop = systemHeadTop + UiMetrics.ManagementHeaderHeight;

			// 全屏时不让正文无限变宽：从左侧开始排版，右侧多出的区域保留为背景呼吸空间。
float availableContentWidth = Mathf.Max(rect.width - UiMetrics.ContentPaddingH * 2f, 60f);
float contentWidth = Mathf.Min(availableContentWidth, UiMetrics.ContentMaxWidth);
float contentX = rect.x + UiMetrics.ContentPaddingH;

// 系统级页头：所有页面共用，占据独立高度并留出明显空白。
			UiText.Draw(
				new Rect(contentX, systemHeadTop, contentWidth, UiText.LineHeight(UiFont.Heading)),
				"DreamsOutposts.Ui.ManagementSystem".Translate(),
				UiFont.Heading,
				UiPalette.Ink,
				TextAnchor.UpperLeft,
				true);

			// 关闭按钮固定在系统级页头右上角。
			Rect closeRect = new Rect(rect.xMax - UiMetrics.ContentPaddingH - UiMetrics.CloseButtonSize,
				systemHeadTop, UiMetrics.CloseButtonSize, UiMetrics.CloseButtonSize);
			UiDebug.Scope("page.close", closeRect);
			if (UiWidgets.CloseButton(closeRect, "DreamsOutposts.Ui.Close".Translate()))
			{
				Close();
			}

			float pageHeadWidth = Mathf.Max(
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
			float bodyWidth = Mathf.Max(scrollArea.width - UiMetrics.ScrollbarGutter, 60f);
			float bodyHeight = shellPage.BodyHeight(bodyWidth, scrollArea.height);
			int scrollId = GetHashCode();
			// 切页动画：正文从右侧滑入 + 淡入，两者共用同一个进度
			float fade = ContentFade();
			UiWidgets.ScrollView(scrollArea, ref contentScroll, bodyHeight, delegate(Rect contentRect)
			{
				shellPage.DrawBody(new Rect(contentRect.x, contentRect.y, bodyWidth, contentRect.height), scrollArea.height);
			}, true, scrollId, true, UiMetrics.ContentSlideDistance * (1f - fade));
			// 淡入：整页画完后用面板底色压一层，等价于给正文做 alpha 淡入，页面内部不需要为动画做任何事
			if (fade < 1f)
			{
				UiDraw.Solid(scrollArea, new Color(UiPalette.Surface.r, UiPalette.Surface.g, UiPalette.Surface.b, 1f - fade));
			}
		}

		/// <summary>正文切页动画的进度：1 = 完全到位（位移归零、不透明）。切页时从 0 开始；首次打开窗口直接是 1。</summary>
		private float ContentFade()
		{
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
			float textWidth = Mathf.Min(rect.width, UiMetrics.PageHeadMaxTextWidth);
			float descriptionHeight = !string.IsNullOrEmpty(body) ? UiText.Height(body, UiFont.Body, textWidth) : 0f;
			float total = titleHeight + UiMetrics.PageHeadSubGap + descriptionHeight + UiMetrics.PageHeadMarginBottom;
			UiDebug.Scope("page.head", new Rect(rect.x, rect.y, rect.width, total));
			// 页面标题：竖直强调条 + 标题。
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
			window.Body = new UiDetailsModalBody(details);
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
			if (slot.TryInstall(card.Def, outpost, out report))
			{
				cache.Invalidate();
				CloseModal();
			}
		}

		public void TryForceInstall(OutpostSlot slot, UiInstallCardView card)
		{
			if (!DebugSettings.godMode || slot == null || card?.Def == null)
			{
				return;
			}
			if (slot.TryForceInstall(card.Def))
			{
				cache.Invalidate();
				CloseModal();
			}
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
			float cancelWidth = UiWidgets.ButtonWidth(cancelLabel);
			Rect cancelRect = new Rect(rect.xMax - cancelWidth, y, cancelWidth, buttonHeight);
			if (UiWidgets.Button(cancelRect, cancelLabel))
			{
				CloseModal();
			}
			string demolishLabel = "DreamsOutposts.Demolish".Translate();
			float demolishWidth = UiWidgets.ButtonWidth(demolishLabel);
			Rect demolishRect = new Rect(cancelRect.x - UiMetrics.ModalFootGap - demolishWidth, y, demolishWidth, buttonHeight);
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
