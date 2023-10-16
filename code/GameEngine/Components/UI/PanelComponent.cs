using Sandbox;
using Sandbox.Razor;
using Sandbox.UI;
using System;
using System.Linq;

[Category( "UI Panels" )]
[Icon( "widgets" )]
public class PanelComponent : BaseComponent, IPanelComponent
{
	Panel panel;

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

		var parentPanel = GameObject.GetComponentInParent<IPanelComponent>( false );
		return parentPanel?.GetPanel();
	}

	public override void OnDisabled()
	{
		panel?.Delete();
		panel = null;
	}

	public Panel GetPanel()
	{
		return panel;
	}

	protected virtual void BuildRenderTree( RenderTreeBuilder v )
	{

	}

	private int BuildRenderHash()
	{
		return HashCode.Combine( BuildHash() );
	}

	protected virtual int BuildHash()
	{
		return 0;
	}

	protected virtual string GetRenderTreeChecksum()
	{
		return string.Empty;
	}

	string loadedStyleSheet;

	void LoadStyleSheet()
	{
		var type = TypeLibrary?.GetType( GetType() );

		// get the shortest class file (incase we have MyPanel.SomeStuff.Blah)
		var location = type?.GetAttributes<Sandbox.Internal.ClassFileLocationAttribute>( false )
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
