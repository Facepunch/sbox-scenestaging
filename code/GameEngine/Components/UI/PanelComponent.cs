using Sandbox.Razor;
using Sandbox.UI;
using System;
using System.Linq;

[Category( "UI Panels" )]
[Icon( "widgets" )]
public abstract partial class PanelComponent : BaseComponent, IPanelComponent
{
	Panel panel;

	/// <summary>
	/// The panel. Can be null if the panel doesn't exist yet.
	/// </summary>
	public Panel Panel => panel;

	public override void OnEnabled()
	{
		loadedStyleSheet = null;
		panel = new CustomBuildPanel( BuildRenderTree, GetRenderTreeChecksum, BuildRenderHash );
		panel.ElementName = GetType().Name.ToLower();
		LoadStyleSheet();
		UpdateParent();
	}

	public override void OnStart()
	{
		UpdateParent();
	}

	void UpdateParent()
	{
		if ( panel is null ) return;

		panel.Parent = FindParentPanel();
	}

	Panel FindParentPanel()
	{
		// do we have any root panels with us?
		if ( GetComponent<IRootPanelComponent>( true ) is IRootPanelComponent r )
		{
			return r.GetPanel();
		}

		// Do we have any parent panels we can become a child of?
		var parentPanel = GameObject.GetComponentInParent<IPanelComponent>( false );
		return parentPanel?.GetPanel();
	}

	public override void OnDisabled()
	{
		panel?.Delete();
		panel = null;
	}

	Panel IPanelComponent.GetPanel()
	{
		return panel;
	}

	/// <summary>
	/// Gets overridden by .razor file
	/// </summary>
	protected virtual void BuildRenderTree( RenderTreeBuilder v ) { }

	/// <summary>
	/// Gets overridden by .razor file
	/// </summary>
	protected virtual string GetRenderTreeChecksum() => string.Empty;

	private int BuildRenderHash()
	{
		return HashCode.Combine( BuildHash() );
	}

	/// <summary>
	/// When this has changes, we will re-render this panel. This is usually
	/// implemented as a HashCode.Combine containing stuff that causes the
	/// panel's content to change.
	/// </summary>
	protected virtual int BuildHash() => 0;

	string loadedStyleSheet;

	void LoadStyleSheet()
	{
		var type = TypeLibrary?.GetType( GetType() );
		if ( type is null )
			return;

		// get the shortest class file (incase we have MyPanel.SomeStuff.Blah)
		var location = type.GetAttributes<Sandbox.Internal.ClassFileLocationAttribute>( false )
										.OrderBy( x => x.Path.Length )
										.FirstOrDefault();

		if ( location is null )
			return;

		var path = location.Path + ".scss";

		// nothing to do
		if ( loadedStyleSheet == path ) return;

		// remove old sheet
		if ( !string.IsNullOrWhiteSpace( loadedStyleSheet ) ) panel.StyleSheet.Remove( loadedStyleSheet );

		// add new one
		loadedStyleSheet = path;
		panel.StyleSheet.Load( loadedStyleSheet ); // todo ignore missing
	}
}

/// <summary>
/// A panel where we control the tree build.
/// </summary>
file class CustomBuildPanel : Panel
{
	Action<RenderTreeBuilder> treeBuilder;
	Func<string> treeChecksum;
	Func<int> buildHash;

	public CustomBuildPanel( Action<RenderTreeBuilder> treeBuilder, Func<string> treeChecksum, Func<int> buildHash )
	{
		this.treeBuilder = treeBuilder;
		this.treeChecksum = treeChecksum;
		this.buildHash = buildHash;
	}

	protected override void BuildRenderTree( RenderTreeBuilder v ) => treeBuilder?.Invoke( v );
	protected override string GetRenderTreeChecksum() => treeChecksum?.Invoke() ?? "";
	protected override int BuildHash() => buildHash?.Invoke() ?? 0;
}
