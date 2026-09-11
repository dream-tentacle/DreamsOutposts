namespace DreamsOutposts
{
	public abstract class OutpostEventEffect
	{
		public abstract void Apply(OutpostEventContext context);

		public abstract string GetPreview(OutpostEventContext context);
	}
}
