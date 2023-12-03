
using Sandbox;
using System.Collections.Generic;

public class EditableMeshComponent : BaseComponent, BaseComponent.ExecuteInEditor
{

	[Property, HideInEditor]
	public EditableMesh Mesh { get; set; }

	Model model;
	PhysicsBody physicsBody;

	protected override void OnEnabled()
	{
		base.OnEnabled();

		Mesh ??= EditableMesh.Cube( new Vector3( 128 ) );

		physicsBody = new( Scene.PhysicsWorld );

		Mesh.OnMeshChanged = () =>
		{
			model = Model.Builder
				.AddMesh( Mesh.Mesh )
				.Create();

			if ( !Components.TryGet<ModelRenderer>( out var mr, FindMode.EverythingInSelf ) )
				mr = GameObject.Components.Create<ModelRenderer>();

			mr.Model = model;

			GenerateCollisionMesh();
		};

		Mesh.Refresh();
	}

	void GenerateCollisionMesh()
	{
		physicsBody.EnableTraceAndQueries = true;
		physicsBody.ClearShapes();
		physicsBody.Transform = Transform.World;
		var shape = physicsBody.AddMeshShape( Mesh.Vertexes.Select( x => x.Position ).ToList(), Mesh.Indices );
		shape.Tags.SetFrom( GameObject.Tags );
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();

		physicsBody?.Remove();
		physicsBody = null;
	}

	Dictionary<int, TestShit> hack;
	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		if ( Mesh == null ) return;

		if ( Mesh.Selection.Any() )
		{
			var center = Mesh.CalculateCenter( Mesh.Selection );
			var normal = Mesh.CalculateNormal( Mesh.Selection );

			var tx = new global::Transform( center, Rotation.Identity, 1.0f );

			if ( !Gizmo.Settings.GlobalSpace && Gizmo.Settings.EditMode != "rotation" )
			{
				tx = tx.WithRotation( Rotation.LookAt( normal ) );
			}

			using ( Gizmo.Scope( "handles", tx ) )
			{
				switch ( Gizmo.Settings.EditMode )
				{
					case "scale":
						// todo: per axis scaling
						if ( Gizmo.Control.Scale( "scale mesh part", 1f, out var newScale ) )
						{
							Mesh.Scale( Mesh.Selection, newScale, center );
						}
						break;
					case "rotation":
						if ( Gizmo.Control.Rotate( "rotate mesh part", new Angles(), out var newAngles ) )
						{
							Mesh.Rotate( Mesh.Selection, newAngles, center );
						}
						break;
					case "position":
						if ( Gizmo.Control.Position( "translate mesh part", center, out var newPos, null, 2f ) )
						{
							var delta = Matrix.CreateRotation( tx.Rotation ).Transform( newPos - center );
							Mesh.Translate( Mesh.Selection, delta );
						}
						break;
				}
			}
		}

		hack ??= new();

		foreach ( var part in Mesh.Parts )
		{
			var das = HashCode.Combine( part.A, part.B, part.C, part.D );

			if ( !hack.ContainsKey( das ) )
			{
				hack[das] = new TestShit();
			}

			hack[das].Part = part;
			hack[das].Mesh = Mesh;
			hack[das].Transform = Transform;

			using ( Gizmo.ObjectScope( hack[das], new Transform( 0, Rotation.Identity, 1 ) ) )
			{
				switch ( part.Type )
				{
					case MeshPartTypes.Face:
						var posA = Mesh.Vertexes[part.A].Position;
						var posB = Mesh.Vertexes[part.B].Position;
						var posC = Mesh.Vertexes[part.C].Position;
						var posD = Mesh.Vertexes[part.D].Position;

						var center = (posA + posB + posC + posD) / 4;

						Gizmo.Draw.Color = Gizmo.IsHovered ? Color.Yellow : Color.Black;
						Gizmo.Draw.Color = Gizmo.IsSelected ? Color.Green : Gizmo.Draw.Color;
						Gizmo.Draw.SolidBox( new BBox( center, 2f ) );
						Gizmo.Hitbox.BBox( new BBox( center, 5f ) );


						if ( Gizmo.IsSelected || Gizmo.IsHovered )
						{
							Gizmo.Draw.Color = Gizmo.Draw.Color.WithAlpha( 0.45f );
							Gizmo.Draw.SolidTriangle( posA, posB, posC );
							Gizmo.Draw.SolidTriangle( posB, posD, posA );
						}

						break;
					case MeshPartTypes.Vertex:
						var pos = Mesh.Vertexes[part.A].Position;

						var vertbox = new BBox()
						{
							Mins = pos - 2,
							Maxs = pos + 2
						};

						Gizmo.Hitbox.BBox( vertbox );
						Gizmo.Draw.Color = Gizmo.IsHovered ? Color.Yellow : Color.Black;
						Gizmo.Draw.Color = Gizmo.IsSelected ? Color.Green : Gizmo.Draw.Color;
						Gizmo.Draw.SolidSphere( pos, 1f );
						break;
					case MeshPartTypes.Edge:
						var edgeA = Mesh.Vertexes[part.A].Position;
						var edgeB = Mesh.Vertexes[part.B].Position;

						if ( !part.Selected )
						{
							using ( Gizmo.Hitbox.LineScope() )
							{
								Gizmo.Hitbox.AddPotentialLine( edgeA, edgeB, 2.0f );
								Gizmo.Draw.LineThickness = Gizmo.IsHovered ? 2.0f : 0.5f;
								Gizmo.Draw.Color = Gizmo.IsHovered ? Color.Yellow : Color.Black;
								Gizmo.Draw.Line( edgeA, edgeB );
							}
						}
						else
						{
							Gizmo.Draw.LineThickness = 2.25f;
							Gizmo.Draw.Color = Color.Green;
							Gizmo.Draw.Line( edgeA, edgeB );
						}
						break;
				}

				if ( Gizmo.IsPressed && Gizmo.HasClicked )
				{
					Gizmo.Select();
				}

				part.Selected = Gizmo.IsSelected;
			}
		}
	}

}

public class TestShit
{
	public MeshPart Part;
	public EditableMesh Mesh;
	public GameTransform Transform;
}
