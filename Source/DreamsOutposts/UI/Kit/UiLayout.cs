using UnityEngine;

namespace DreamsOutposts
{
	/// <summary>
	/// 竖向游标布局：等价 CSS 里「自上而下 flex 列 + gap」的写法。
	/// 所有页面都用它排版，避免手写一堆累加 y。
	/// </summary>
	public struct UiStack
	{
		private Rect area;

		private float cursor;

		public static UiStack Begin(Rect rect)
		{
			UiStack stack = default(UiStack);
			stack.area = rect;
			stack.cursor = rect.y;
			return stack;
		}

		public Rect Area => area;

		public float UsedHeight => cursor - area.y;

		public float RemainingHeight => Mathf.Max(area.yMax - cursor, 0f);

		public float Cursor => cursor;

		/// <summary>取满宽的一行。</summary>
		public Rect Next(float height)
		{
			Rect rect = new Rect(area.x, cursor, area.width, height);
			cursor += height;
			return rect;
		}

		/// <summary>取从左侧偏移的一行（x 相对 area 左边界）。</summary>
		public Rect Next(float x, float width, float height)
		{
			Rect rect = new Rect(area.x + x, cursor, width, height);
			cursor += height;
			return rect;
		}

		/// <summary>取一个右侧固定宽度的行，返回左侧剩余与右侧。</summary>
		public void NextSplit(float leftWidth, float height, out Rect left, out Rect right)
		{
			left = new Rect(area.x, cursor, leftWidth, height);
			right = new Rect(area.x + leftWidth, cursor, Mathf.Max(area.width - leftWidth, 0f), height);
			cursor += height;
		}

		public void Gap(float height)
		{
			cursor += height;
		}

		public void Skip(float height)
		{
			cursor += height;
		}

		/// <summary>把一个已知矩形登记进游标（用于嵌入手绘高度的区块）。</summary>
		public void Place(Rect rect)
		{
			cursor = Mathf.Max(cursor, rect.yMax);
		}

		public void AdvanceTo(float y)
		{
			cursor = Mathf.Max(cursor, y);
		}
	}

	/// <summary>行/栅格切分工具。</summary>
	public static class UiLayout
	{
		/// <summary>在一行里从左到右均分若干单元（含 gap）。</summary>
		public static Rect Cell(Rect row, int columns, float gap, int index)
		{
			if (columns < 1)
			{
				columns = 1;
			}
			float width = (row.width - gap * (columns - 1)) / columns;
			return new Rect(row.x + (width + gap) * index, row.y, width, row.height);
		}

		/// <summary>
		/// 栅格单元矩形。列数与单元宽度由 UiMetrics.GridColumns / GridCellWidth 算出，
		/// 行高由调用方按「本行最高卡片」给出（等价 CSS grid 的 align-items: stretch）。
		/// </summary>
		public static Rect GridCell(float originX, float rowY, float cellWidth, float gap, int column, float height)
		{
			return new Rect(originX + (cellWidth + gap) * column, rowY, cellWidth, height);
		}

		/// <summary>一行里左右两端对齐：返回左侧矩形与右侧矩形。</summary>
		public static void SplitRow(Rect row, float leftWidth, out Rect left, out Rect right)
		{
			left = new Rect(row.x, row.y, leftWidth, row.height);
			right = new Rect(row.x + leftWidth, row.y, Mathf.Max(row.width - leftWidth, 0f), row.height);
		}

		/// <summary>行内右对齐：给定宽度，得到贴着右边界的矩形。</summary>
		public static Rect AlignRight(Rect row, float width)
		{
			return new Rect(row.xMax - width, row.y, width, row.height);
		}

		/// <summary>行内左对齐：给定宽度，得到贴着左边界的矩形。</summary>
		public static Rect AlignLeft(Rect row, float width)
		{
			return new Rect(row.x, row.y, width, row.height);
		}
	}
}
