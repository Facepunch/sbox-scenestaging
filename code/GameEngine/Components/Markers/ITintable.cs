public abstract partial class BaseComponent
{
	/// <summary>
	/// A component that lets you change its color
	/// </summary>
	public interface ITintable
	{
		public Color Color { get; set; }
	}
}
