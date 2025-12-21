using Sandbox.Clutter;

namespace Editor;

/// <summary>
/// Custom control widget for the ScattererTypeName property in ClutterDefinition.
/// Provides a dropdown/combobox to select scatterer types.
/// </summary>
[CustomEditor( typeof( string ), NamedEditor = "ScattererTypeSelector" )]
public class ScattererTypeControlWidget : DropdownControlWidget<string>
{
	public override bool SupportsMultiEdit => false;

	public ScattererTypeControlWidget( SerializedProperty property ) : base( property ) { }

	protected override IEnumerable<object> GetDropdownValues()
	{
		var scattererTypes = TypeLibrary.GetTypes()
			.Where( t => t.TargetType!.IsAssignableTo( typeof( Scatterer ) ) )
			.Where( t => !t.IsAbstract )
			.Where( t => t.TargetType != typeof( Scatterer ) ) // Ignore base class itself
			.OrderBy( t => t.Name );

		return scattererTypes.Select( s => s.Name );
	}

	protected override void OnValueChanged()
	{
		base.OnValueChanged();
		SerializedProperty.Parent?.NoteChanged( SerializedProperty );
	}
}
