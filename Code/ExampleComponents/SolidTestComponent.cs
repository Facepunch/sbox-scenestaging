using Sandbox.Solids;
using Vertex = Sandbox.Solids.Vertex;

#nullable enable

public sealed class SolidTestComponent : Component
{
	[RequireComponent]
	public required ModelRenderer Renderer { get; init; }

	public Solid? Solid { get; set; }

	[Property]
	public Vector3 Size { get; set; } = 256f;

	[Property]
	public int Shift { get; set; } = 8;

	[Property]
	public SolidMaterial? Material { get; set; }

	[Property]
	public Material? PlaceholderMaterial { get; set; }

	[Property] public List<GameObject?> Subtractions { get; set; } = new();

	private readonly MeshWriter _writer = new();
	
	private Model? _model;
	private Mesh? _mesh;

	[Button]
	public void Generate()
	{
		if ( Material is not { } material || PlaceholderMaterial is not { } placeholderMaterial ) return;

		var min = (-Size * 0.5f).ToFixed( Shift );
		var max = (Size * 0.5f).ToFixed( Shift );

		//Solid = Solid.Tetrahedron(
		//	new Vertex( min.X, min.Y, min.Z ),
		//	new Vertex( max.X, max.Y, min.Z ),
		//	new Vertex( min.X, max.Y, max.Z ),
		//	new Vertex( max.X, min.Y, max.Z ),
		//	material );

		Solid = Solid.Box( min, max, material );

		foreach ( var subtraction in Subtractions )
		{
			if ( subtraction is null ) continue;
			if ( !subtraction.Enabled ) continue;

			Solid = Solid?.Subtract( CreateSubtractMask( subtraction.WorldTransform ) )!;
		}

		_writer.Clear();
		_writer.Write( Solid, Shift );

		_mesh ??= new Mesh( placeholderMaterial );
		_mesh.Material = placeholderMaterial;

		_writer.CopyTo( _mesh );

		_model ??= new ModelBuilder().AddMesh( _mesh ).Create();

		Renderer.Model = _model;
	}

	private Solid CreateSubtractMask( Transform transform )
	{
		var origin = transform.PointToWorld( new Vector3( 0f, 0f, 0f ) );

		var unitX = transform.PointToWorld( new Vector3( 1f, 0f, 0f ) ) - origin;
		var unitY = transform.PointToWorld( new Vector3( 0f, 1f, 0f ) ) - origin;
		var unitZ = transform.PointToWorld( new Vector3( 0f, 0f, 1f ) ) - origin;

		return Solid.Cuboid( origin.ToFixed( Shift ), unitX.ToFixed( Shift ), unitY.ToFixed( Shift ), unitZ.ToFixed( Shift ), Material! );

		//var a = origin - unitX - unitY - unitZ;
		//var b = origin + unitX + unitY - unitZ;
		//var c = origin - unitX + unitY + unitZ;
		//var d = origin + unitX - unitY + unitZ;

		//return Solid.Tetrahedron( a.ToFixed( Shift ), b.ToFixed( Shift ), c.ToFixed( Shift ), d.ToFixed( Shift ), Material! );
	}

	protected override void DrawGizmos()
	{
		if ( Solid is { } solid )
		{
			Gizmo.Draw.Color = Color.Red;
			solid.DrawGizmos( Shift );
		}

		foreach ( var subtraction in Subtractions )
		{
			if ( subtraction is null ) continue;
			if ( !subtraction.Enabled ) continue;

			Gizmo.Draw.Color = Color.Green;
			CreateSubtractMask( subtraction.WorldTransform ).DrawGizmos( Shift );
		}
	}
}
