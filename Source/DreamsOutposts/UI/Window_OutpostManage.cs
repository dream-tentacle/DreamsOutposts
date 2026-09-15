using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace DreamsOutposts
{
	/// <summary>
	/// 据点「管理」窗口（新样式外壳）。
	/// 自绘圆角面板 + 阴影 + 标题栏 + 左侧导航栏 + 内容区（页头 / 细滚动条）。
	/// </summary>
	public class Window_OutpostManage : Window
	{
		private readonly Outpost outpost;

		private readonly List<OutpostManagePage> pages;

		private readonly OutpostUiCache cache;

		private int selectedIndex;

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
				float width = Mathf.Min(UiMetrics.WindowMaxWidth + UiMetrics.WindowShadowMargin * 2f, UI.screenWidth - 20f);
				float height = Mathf.Min(UiMetrics.WindowMaxHeight + UiMetrics.WindowShadowMargin * 2f, UI.screenHeight - 20f);
				return new Vector2(Mathf.Max(width, 560f), Mathf.Max(height, 400f));
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
			UiDraw.Shadow(panel, (int)UiMetrics.RadiusSm);
			UiDraw.Box(panel, (int)UiMetrics.RadiusSm, UiPalette.Surface, UiPalette.Line);
			if (pages.Count == 0)
			{
				UiText.Draw(new Rect(panel.x + UiMetrics.ContentPaddingH, panel.y + UiMetrics.ContentPaddingTop, panel.width - UiMetrics.ContentPaddingH * 2f, 30f),
					"DreamsOutposts.NoManagementPage".Translate(), UiFont.Body, UiPalette.Ink);
				UiDebug.PopSpace();
				return;
			}
			int index = Mathf.Clamp(selectedIndex, 0, pages.Count - 1);
			float titlebarHeight = DrawTitlebar(panel);
			Rect bodyRect = new Rect(panel.x, panel.y + titlebarHeight, panel.width, Mathf.Max(panel.height - titlebarHeight, 0f));
			float sidebarWidth = Mathf.Min(UiMetrics.SidebarWidth, Mathf.Max(bodyRect.width * 0.32f, 120f));
			DrawSidebar(new Rect(bodyRect.x, bodyRect.y, sidebarWidth, bodyRect.height));
			Rect contentRect = new Rect(bodyRect.x + sidebarWidth, bodyRect.y, Mathf.Max(bodyRect.width - sidebarWidth, 40f), bodyRect.height);
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

		// ---------------------------------------------------------------
		// 标题栏
		// ---------------------------------------------------------------

		private float TitlebarHeight()
		{
			float textHeight = UiText.LineHeight(UiFont.Heading) + 3f + UiText.LineHeight(UiFont.Caption);
			float content = Mathf.Max(UiMetrics.EmblemSize, textHeight);
			return content + UiMetrics.TitlebarPaddingTop + UiMetrics.TitlebarPaddingBottom;
		}

		private float DrawTitlebar(Rect panel)
		{
			float height = TitlebarHeight();
			Rect bar = new Rect(panel.x, panel.y, panel.width, height);
			UiDebug.Scope("window.titlebar", bar);
			Rect inner = new Rect(bar.x + 1f, bar.y + 1f, Mathf.Max(bar.width - 2f, 1f), Mathf.Max(bar.height - 1f, 1f));
			UiDraw.Box(inner, (int)(UiMetrics.RadiusSm - 1f), UiPalette.Raised, UiPalette.Clear, UiCorners.TopLeft | UiCorners.TopRight);
			UiDraw.Divider(new Rect(bar.x, bar.yMax - 1f, bar.width, 1f), UiPalette.Line);
			// 徽标
			float emblemY = bar.y + (bar.height - UiMetrics.EmblemSize) * 0.5f;
			Rect emblem = new Rect(bar.x + UiMetrics.TitlebarPaddingLeft, emblemY, UiMetrics.EmblemSize, UiMetrics.EmblemSize);
			UiDraw.Box(emblem, (int)UiMetrics.RadiusSm, UiPalette.Card, UiPalette.Line);
			float glyph = UiMetrics.EmblemIconSize;
			UiDraw.Icon(new Rect(emblem.center.x - glyph * 0.5f, emblem.center.y - glyph * 0.5f, glyph, glyph),
				UiIconMap.ForFacility(outpost.coreFacility?.def), UiPalette.Ink);
			// 文字
			float textX = emblem.xMax + 12f;
			float textWidth = Mathf.Max(bar.xMax - UiMetrics.TitlebarPaddingRight - UiMetrics.CloseButtonSize - 12f - textX, 40f);
			float textY = bar.y + UiMetrics.TitlebarPaddingTop;
			UiText.Draw(new Rect(textX, textY, textWidth, UiText.LineHeight(UiFont.Heading)), outpost.LabelCap, UiFont.Heading, UiPalette.Ink,
				TextAnchor.UpperLeft, true, false, true);
			string sub = cache.DaysText;
			UiText.Draw(new Rect(textX, textY + UiText.LineHeight(UiFont.Heading) + 3f, textWidth, UiText.LineHeight(UiFont.Caption)),
				sub, UiFont.Caption, UiPalette.Ink2, TextAnchor.UpperLeft, false, false, true);
			// 关闭按钮
			Rect closeRect = new Rect(bar.xMax - UiMetrics.TitlebarPaddingRight - UiMetrics.CloseButtonSize,
				bar.y + (bar.height - UiMetrics.CloseButtonSize) * 0.5f, UiMetrics.CloseButtonSize, UiMetrics.CloseButtonSize);
			if (UiWidgets.CloseButton(closeRect, "DreamsOutposts.Ui.Close".Translate()))
			{
				Close();
			}
			return height;
		}

		// ---------------------------------------------------------------
		// 侧栏
		// ---------------------------------------------------------------

		private void DrawSidebar(Rect rect)
		{
			UiDebug.Scope("window.sidebar", rect);
			Rect inner = new Rect(rect.x + 1f, rect.y, Mathf.Max(rect.width - 2f, 1f), Mathf.Max(rect.height - 1f, 1f));
			UiDraw.Box(inner, (int)(UiMetrics.RadiusSm - 1f), UiPalette.Raised, UiPalette.Clear, UiCorners.BottomLeft);
			UiDraw.Divider(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), UiPalette.Line);
			float x = rect.x + UiMetrics.SidebarPaddingH;
			float width = rect.width - UiMetrics.SidebarPaddingH * 2f;
			float y = rect.y + UiMetrics.SidebarPaddingV;
			// 「管理」小标题
			Rect labelRect = new Rect(x, y, width, UiText.LineHeight(UiFont.Caption) + UiMetrics.NavLabelPaddingTop + UiMetrics.NavLabelPaddingBottom);
			UiText.Draw(new Rect(labelRect.x + UiMetrics.NavLabelPaddingH, labelRect.y + UiMetrics.NavLabelPaddingTop, width, UiText.LineHeight(UiFont.Caption)),
				"DreamsOutposts.ManageOutpost".Translate(), UiFont.Caption, UiPalette.Ink2);
			y = labelRect.yMax;
			float itemHeight = UiWidgets.NavItemHeight();
			for (int i = 0; i < pages.Count; i++)
			{
				OutpostManagePage page = pages[i];
				UiIcon icon = UiIconMap.ForPage(page.def, page.GetType());
				string summary = UiPageSummary.For(page, outpost);
				int badge = UiPageSummary.BadgeFor(page, outpost);
				Rect itemRect = new Rect(x, y, width, itemHeight);
				UiDebug.Scope("sidebar.item[" + i + "]", itemRect);
				if (UiWidgets.NavItem(itemRect, icon, page.Label, summary, i == selectedIndex, badge, page.Tooltip))
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
		}

		// ---------------------------------------------------------------
		// 内容区
		// ---------------------------------------------------------------

		private void DrawPage(Rect rect, OutpostManagePage page)
		{
			UiDebug.Scope("window.content", rect);
			if (!(page is IUiShellPage shellPage))
			{
				// 老样式页面：给它一块干净的区域，它自己管滚动
				GUI.BeginGroup(rect);
				Text.Font = GameFont.Small;
				try
				{
					page.DoContents(new Rect(0f, 0f, rect.width, rect.height));
				}
				finally
				{
					Text.Font = GameFont.Small;
					GUI.EndGroup();
				}
				return;
			}
			float headTop = rect.y + UiMetrics.ContentPaddingTop;
			float contentWidth = Mathf.Max(rect.width - UiMetrics.ContentPaddingH * 2f, 60f);
			float headHeight = DrawPageHead(new Rect(rect.x + UiMetrics.ContentPaddingH, headTop, contentWidth, 0f), page, shellPage);
			float scrollTop = headTop + headHeight;
			Rect scrollArea = new Rect(rect.x + UiMetrics.ContentPaddingH, scrollTop, contentWidth,
				Mathf.Max(rect.yMax - UiMetrics.ContentPaddingBottom - scrollTop, 20f));
			float bodyWidth = Mathf.Max(scrollArea.width - UiMetrics.ScrollbarGutter, 60f);
			float bodyHeight = shellPage.BodyHeight(bodyWidth, scrollArea.height);
			int scrollId = GetHashCode();
			UiWidgets.ScrollView(scrollArea, ref contentScroll, bodyHeight, delegate(Rect contentRect)
			{
				shellPage.DrawBody(new Rect(contentRect.x, contentRect.y, bodyWidth, contentRect.height), scrollArea.height);
			}, true, scrollId, true);
		}

		private float DrawPageHead(Rect rect, OutpostManagePage page, IUiShellPage shellPage)
		{
			float y = rect.y;
			float titleHeight = UiText.LineHeight(UiFont.Heading);
			UiText.Draw(new Rect(rect.x, y, rect.width, titleHeight), page.Label, UiFont.Heading, UiPalette.Ink, TextAnchor.UpperLeft, true);
			y += titleHeight + UiMetrics.PageHeadSubGap;
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
			float descriptionHeight = 0f;
			if (!string.IsNullOrEmpty(body))
			{
				descriptionHeight = UiText.Height(body, UiFont.Body, textWidth);
				UiText.Draw(new Rect(rect.x, y, textWidth, descriptionHeight), body, UiFont.Body, UiPalette.Ink2, TextAnchor.UpperLeft, false, true);
			}
			float total = titleHeight + UiMetrics.PageHeadSubGap + descriptionHeight + UiMetrics.PageHeadMarginBottom;
			UiDebug.Scope("page.head", new Rect(rect.x, rect.y, rect.width, total));
			return total;
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
			// 标题下不再重复显示设施名（正文里已经有「拆除X？」）
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
