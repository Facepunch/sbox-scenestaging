public abstract partial class Component
{
	/// <summary>
	/// A component that lets you change its color
	/// </summary>
	public interface ITintable
	{
		public Color Color { get; set; }
	}
}
