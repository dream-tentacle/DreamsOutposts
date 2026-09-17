using Verse;

namespace DreamsOutposts
{
	public abstract class OutpostManagePage
	{
		public Outpost outpost;

		public OutpostManagePageDef def;

		public Window hostWindow;

		public virtual string Label => def?.LabelCap ?? ((TaggedString)GetType().Name);

		public virtual string Tooltip => def?.description;

		public virtual bool IsVisible => true;

		protected void CloseHostWindow()
		{
			hostWindow?.Close();
		}

		public virtual void OnOpen()
		{
		}

		public virtual void OnClose()
		{
		}
	}
}
