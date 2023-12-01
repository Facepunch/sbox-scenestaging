using Sandbox;
using System.Collections.Generic;

public sealed class CameraPhysicsDebug : BaseComponent, BaseComponent.ExecuteInEditor
{
	[Property] public int MaxPoints { get; set; } = 10000;
	[Property] public int TracesPerFrame { get; set; } = 500;

	[Range( 0, 10 )]
	[Property] public float NormalLength { get; set; } = 2;
	[Property] public TraceTypes TraceType { get; set; } = TraceTypes.Ray;
	[Property] public bool Hitboxes { get; set; } = false;

	public record struct Hitpoint( Vector3 Position, Vector3 Normal, Color Tint );

	public enum TraceTypes
	{
		Ray,
		Box,
		Sphere
	}


	List<Hitpoint> worldPoints = new();

	protected override void OnUpdate()
	{
		for ( int i = 0; i < TracesPerFrame; i++ )
		{
			SceneTraceResult t = default;
			var start = Transform.Position;
			var end = Transform.Position + Transform.Rotation.Forward * 1000 + Vector3.Random * 400;

			if ( TraceType == TraceTypes.Ray )
			{
				t = Scene.Trace
						.Ray( start, end )
						.UseHitboxes( Hitboxes )
						.Run();
			}
			else if (  TraceType == TraceTypes.Box )
			{
				t = Scene.Trace
						.Ray( start, end )
						.Size( new BBox( -10, 10 ) )
						.UseHitboxes( Hitboxes )
						.Run();


			}
			else if ( TraceType == TraceTypes.Sphere )
			{
				t = Scene.Trace
						.Ray( start, end )
						.Radius( 5 )
						.UseHitboxes( Hitboxes )
						.Run();
			}

			if ( t.Hit )
			{
				Color tint = Color.White;
				worldPoints.Add( new Hitpoint { Position = t.EndPosition, Normal = t.Normal, Tint = tint } );
			}


		}

		foreach ( var t in worldPoints )
		{
			Gizmo.Draw.Color = t.Tint * new Color( (t.Normal.x + 1) * 0.5f, (t.Normal.y + 1) * 0.5f, (t.Normal.z + 1) * 0.5f );
			Gizmo.Draw.Line( t.Position, t.Position + t.Normal * NormalLength );
		}

		if ( worldPoints.Count > MaxPoints )
		{
			worldPoints.RemoveRange( 0, worldPoints.Count - MaxPoints );
		}
	}
}
