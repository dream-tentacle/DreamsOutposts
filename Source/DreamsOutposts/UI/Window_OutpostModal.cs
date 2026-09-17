using System;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	/// <summary>弹窗正文：先报高度，再按同样的宽度画。</summary>
	public interface IUiModalBody
	{
		float Height(float width);

		void Draw(Rect rect);
	}

	/// <summary>
	/// 可选：正文开头这一段固定不滚动（描述 / 基础信息 / 超时说明之类），
	/// 后面的内容（例如事件选项列表）单独滚动，永远不会盖住它。
	/// </summary>
	public interface IUiModalBodyHeader
	{
		float HeaderHeight(float width);

		void DrawHeader(Rect rect);
	}

	/// <summary>
	/// 模态弹窗：全屏遮罩 + 居中圆角面板（标题栏 / 滚动正文 / 底部按钮区）。
	/// 窗口本身铺满屏幕，因此遮罩能盖住包括管理窗口在内的一切，同时吞掉所有点击。
	/// </summary>
	public class Window_OutpostModal : Window
	{
		private Vector2 scroll;

		public string TitleText;

		public string SubText;

		public float PanelWidth = UiMetrics.ModalNormalWidth;

		public IUiModalBody Body;

		/// <summary>
		/// true = 面板高度固定为屏幕允许的最大值，不随内容变化（安装设施弹窗用）。
		/// </summary>
		public bool UseMaxHeight;

		public Action<Rect> FooterDrawer;

		public Action ClosedCallback;

		public override Vector2 InitialSize => new Vector2(UI.screenWidth, UI.screenHeight);

		public Window_OutpostModal()
		{
			doWindowBackground = false;
			drawShadow = false;
			doCloseX = false;
			doCloseButton = false;
			closeOnClickedOutside = false;
			closeOnAccept = false;
			absorbInputAroundWindow = true;
			// 层级用原版默认的 Dialog：窗口按 layer 排序插入，Super 层会排在管理窗口之后，
			// 从弹窗里打开的原版信息面板就会被压在弹窗下面。同层后加入即可正常叠放。
		}

		protected override float Margin => 0f;

		protected override void SetInitialSizeAndPosition()
		{
			windowRect = new Rect(0f, 0f, UI.screenWidth, UI.screenHeight);
		}

		public static float HeadHeight(bool hasSubtitle)
		{
			float height = UiText.LineHeight(UiFont.Heading);
			if (hasSubtitle)
			{
				height += 3f + UiText.LineHeight(UiFont.Body);
			}
			return height + UiMetrics.ModalHeadPaddingTop + UiMetrics.ModalHeadPaddingBottom;
		}

		public static float FooterHeight()
		{
			return UiWidgets.ButtonHeight(UiButtonSize.Normal) + UiMetrics.ModalFootPaddingV * 2f;
		}

		public override void PostClose()
		{
			base.PostClose();
			ClosedCallback?.Invoke();
		}

		public override void DoWindowContents(Rect inRect)
		{
			// 弹窗自己也是一个绘制空间（窗口铺满屏幕，所以这里的局部坐标就是屏幕坐标）
			UiDebug.PushSpace("modal", windowRect.position);
			UiDraw.Scrim(inRect);
			float width = Mathf.Round(Mathf.Min(PanelWidth, Mathf.Max(inRect.width - 48f, 240f)));
			float headHeight = HeadHeight(!string.IsNullOrEmpty(SubText));
			float footerHeight = FooterHeight();
			float bodyWidth = Mathf.Max(width - UiMetrics.ModalBodyPadding * 2f - UiMetrics.ScrollbarGutter, 60f);
			float bodyHeight = Mathf.Max(Body?.Height(bodyWidth) ?? 0f, 0f);
			float maxPanelHeight = Mathf.Max(inRect.height * UiMetrics.ModalMaxHeightRatio, headHeight + footerHeight + 60f);
			// 正文内层还要被 ModalBodyPadding 四面缩一圈，所以面板高度必须把这圈留白算进去：
			// 否则可视区永远比正文矮 2 倍留白，正文会被切掉尾巴并平白多出一条滚动条
			float panelHeight = UseMaxHeight
				? maxPanelHeight
				: Mathf.Min(headHeight + bodyHeight + UiMetrics.ModalBodyPadding * 2f + footerHeight, maxPanelHeight);
			Rect panel = new Rect(Mathf.Round(inRect.center.x - width * 0.5f), Mathf.Round(inRect.center.y - panelHeight * 0.5f), width, Mathf.Round(panelHeight));
			UiDebug.Scope("modal.panel", panel);
			UiDraw.Panel(panel, (int)UiMetrics.RadiusSm, UiPalette.Surface, UiPalette.Line, true);
			DrawHead(panel, headHeight);
			float bodyOuterHeight = Mathf.Max(panel.height - headHeight - footerHeight, 0f);
			Rect bodyOuter = new Rect(panel.x, panel.y + headHeight, panel.width, bodyOuterHeight);
			Rect bodyInner = new Rect(bodyOuter.x + UiMetrics.ModalBodyPadding, bodyOuter.y + UiMetrics.ModalBodyPadding,
				Mathf.Max(bodyOuter.width - UiMetrics.ModalBodyPadding * 2f, 40f), Mathf.Max(bodyOuter.height - UiMetrics.ModalBodyPadding * 2f, 20f));
			// 正文可以选择把开头一段做成「固定头」：它不参与滚动，也就不会被下面的内容盖住
			IUiModalBodyHeader header = Body as IUiModalBodyHeader;
			float headerHeight = 0f;
			if (header != null)
			{
				headerHeight = Mathf.Clamp(header.HeaderHeight(bodyWidth), 0f, Mathf.Max(bodyInner.height - 60f, 0f));
			}
			float scrollHeight = Mathf.Max(bodyHeight - headerHeight, 0f);
			Rect headerRect = new Rect(bodyInner.x, bodyInner.y, bodyWidth, headerHeight);
			if (header != null && headerHeight > 0f)
			{
				// 固定头必须按 bodyWidth 画：上面是用 bodyWidth 测的，画的时候换宽度会让它和选项区错位
				header.DrawHeader(headerRect);
			}
			Rect scrollRect = new Rect(bodyInner.x, bodyInner.y + headerHeight, bodyInner.width, Mathf.Max(bodyInner.height - headerHeight, 20f));
			bool scrollNeeded = scrollHeight > scrollRect.height + 0.5f;
			int scrollId = GetHashCode();
			UiDebug.Scope("modal.body", bodyInner);
			UiDebug.Scope("modal.header", headerRect);
			UiDebug.Scope("modal.scroll", scrollRect);
			// 无论这一帧需不需要滚动条都恒定预留槽位（等价 CSS scrollbar-gutter: stable）：
			// 否则正文会在 bodyWidth / bodyInner.width 两种宽度之间跳，测量和绘制就对不上了
			UiWidgets.ScrollView(scrollRect, ref scroll, scrollHeight, delegate(Rect contentRect)
			{
				if (Body != null)
				{
					// 正文整体按 unsplit 的坐标布局，这里把窗口上移固定头的高度，让滚动区正好从固定头下面接上
					Body.Draw(new Rect(contentRect.x, contentRect.y - headerHeight, contentRect.width, contentRect.height + headerHeight));
				}
			}, true, scrollId, scrollNeeded);
			DrawFooter(panel, footerHeight);
			// 叠层在最上层窗口再画一遍，这样弹窗里的方框盖在弹窗上、位置也对
			UiDebug.DrawOverlay();
			UiDebug.PopSpace();
		}

		private void DrawHead(Rect panel, float headHeight)
		{
			Rect head = new Rect(panel.x, panel.y, panel.width, headHeight);
			UiDebug.Scope("modal.head", head);
			Rect headInner = new Rect(head.x + 1f, head.y + 1f, Mathf.Max(head.width - 2f, 1f), Mathf.Max(head.height - 2f, 1f));
			UiDraw.Box(headInner, (int)(UiMetrics.RadiusSm - 1f), UiPalette.Raised, UiPalette.Clear, UiCorners.TopLeft | UiCorners.TopRight);
			UiDraw.Divider(new Rect(head.x, head.yMax - 1f, head.width, 1f), UiPalette.Line);
			float textX = head.x + UiMetrics.ModalHeadPaddingH;
			float textWidth = Mathf.Max(head.width - UiMetrics.ModalHeadPaddingH * 2f - UiMetrics.CloseButtonSize - 8f, 40f);
			float y = head.y + UiMetrics.ModalHeadPaddingTop;
			UiText.Draw(new Rect(textX, y, textWidth, UiText.LineHeight(UiFont.Heading)), TitleText, UiFont.Heading, UiPalette.Ink, TextAnchor.UpperLeft, true, false, true);
			if (!string.IsNullOrEmpty(SubText))
			{
				UiText.Draw(new Rect(textX, y + UiText.LineHeight(UiFont.Heading) + 3f, textWidth, UiText.LineHeight(UiFont.Body)),
					SubText, UiFont.Body, UiPalette.Ink2, TextAnchor.UpperLeft, false, false, true);
			}
			Rect closeRect = new Rect(head.xMax - UiMetrics.ModalHeadPaddingH - UiMetrics.CloseButtonSize,
				head.y + (head.height - UiMetrics.CloseButtonSize) * 0.5f, UiMetrics.CloseButtonSize, UiMetrics.CloseButtonSize);
			if (UiWidgets.CloseButton(closeRect, "DreamsOutposts.Ui.Close".Translate()))
			{
				Close();
			}
		}

		private void DrawFooter(Rect panel, float footerHeight)
		{
			Rect footer = new Rect(panel.x, panel.yMax - footerHeight, panel.width, footerHeight);
			UiDebug.Scope("modal.foot", footer);
			Rect footerInner = new Rect(footer.x + 1f, footer.y, Mathf.Max(footer.width - 2f, 1f), Mathf.Max(footer.height - 2f, 1f));
			UiDraw.Box(footerInner, (int)(UiMetrics.RadiusSm - 1f), UiPalette.Raised, UiPalette.Clear, UiCorners.BottomLeft | UiCorners.BottomRight);
			UiDraw.Divider(new Rect(footer.x, footer.y, footer.width, 1f), UiPalette.Line);
			if (FooterDrawer != null)
			{
				Rect content = new Rect(footer.x + UiMetrics.ModalFootPaddingH, footer.y,
					Mathf.Max(footer.width - UiMetrics.ModalFootPaddingH * 2f, 40f), footer.height);
				FooterDrawer(content);
			}
		}
	}
}
