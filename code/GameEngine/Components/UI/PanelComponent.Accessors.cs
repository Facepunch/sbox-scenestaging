using Sandbox.Razor;
using Sandbox.UI;

public partial class PanelComponent 
{
	/// <inheritdoc cref="Panel.HasClass(string)"/>
	public bool HasClass( string className ) => Panel.HasClass( className );

	/// <inheritdoc cref="Panel.RemoveClass(string)"/>
	public void RemoveClass( string className ) => Panel.RemoveClass( className );

	/// <inheritdoc cref="Panel.AddClass(string)"/>
	public void AddClass( string className ) => Panel.AddClass( className );

	/// <inheritdoc cref="Panel.BindClass(string, Func{bool})"/>
	public void BindClass( string className, Func<bool> func ) => Panel.BindClass( className, func );

	/// <inheritdoc cref="Panel.SetClass(string, bool)"/>
	public void SetClass( string className, bool enabled ) => Panel.SetClass( className, enabled );
}
