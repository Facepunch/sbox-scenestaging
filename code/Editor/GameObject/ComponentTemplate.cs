namespace Editor;

public abstract class ComponentTemplate
{
	public virtual string Name { get; set; }
	public virtual string Description { get; set; }
	public virtual string NameFilter => "Cs File (*.cs)";
	public virtual string Suffix => ".cs";

	public virtual void Create( string componentName, string path )
	{
		var content = $$"""
		using Sandbox;

		public sealed class {{componentName}} : BaseComponent
		{
			protected override void OnUpdate()
			{

			}
		}
		""";

		var directory = System.IO.Path.GetDirectoryName( path );
		System.IO.File.WriteAllText( System.IO.Path.Combine( directory, componentName + Suffix ), content );
	}

	/// <summary>
	/// Get all component template types that aren't abstract.
	/// </summary>
	/// <returns></returns>
	public static TypeDescription[] GetAllTypes()
	{
		return TypeLibrary.GetTypes<ComponentTemplate>().OrderByDescending( x => x.Name ).Where( x => !x.IsAbstract ).ToArray();
	}
}

[Icon( "check_box_outline_blank" )]
[Title( "Simple Component" )]
[Description( "A simple component with an Update method." )]
public partial class SimpleComponentTemplate : ComponentTemplate
{
}

[Icon( "dashboard" )]
[Title( "Razor Panel Component" )]
[Description( "A razor panel component demonstrating how to make a UI panel through a component." )]
public partial class RazorComponentTemplate : ComponentTemplate
{
	public override string Name => "Razor Panel Component";
	public override string Description => "A razor panel component demonstrating how to make a UI panel through a component.";

	// Needed for the file dialog
	public override string NameFilter => "Razor File (*.razor)";
	public override string Suffix => ".razor";

	public override void Create( string componentName, string path )
	{
		var razor = $$"""
		@using Sandbox;
		@using Sandbox.UI;
		@inherits PanelComponent

		<root>
			<div class="title">@MyStringValue</div>
		</root>

		@code
		{

			[Property] public string MyStringValue { get; set; } = "Hello World!";

			/// <summary>
			/// the hash determines if the system should be rebuilt. If it changes, it will be rebuilt
			/// </summary>
			protected override int BuildHash() => System.HashCode.Combine( MyStringValue );
		}
		""";

		var directory = System.IO.Path.GetDirectoryName( path );
		System.IO.File.WriteAllText( System.IO.Path.Combine( directory, componentName + Suffix ), razor );

		var scss = $$"""
		{{componentName}}
		{
			position: absolute;
			top: 0;
			left: 0;
			right: 0;
			bottom: 0;
			background-color: #444;
			justify-content: center;
			align-items: center;
			font-weight: bold;
			border-radius: 20px;

			.title
			{
				font-size: 25px;
				font-family: Poppins;
				color: #fff;
			}
		}
		""";

		System.IO.File.WriteAllText( System.IO.Path.Combine( directory, componentName + ".razor.scss" ), scss );
	}
}
