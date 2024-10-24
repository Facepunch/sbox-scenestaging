using Sandbox.Utility;

public sealed class DebugOverlayTest : Component, Component.ExecuteInEditor
{
	protected override void OnUpdate()
	{
		var unit = Vector3.Right * 150;
		var up = Vector3.Up * 200;
		Vector3 pos = WorldPosition;

		DrawAllBones();


		{
			DebugOverlay.Line( pos, pos + Vector3.Up * 50 + Vector3.Right * 50 );
			DrawLabel( pos, "Line" );
		}

		pos += unit;

		{
			DebugOverlay.Line( pos, pos + Vector3.Random * 40, duration: 1, color: Color.White * Random.Shared.Float( 0.5f, 1f ) );
			DrawLabel( pos, "1s Line" );
		}

		pos += unit;

		{
			DebugOverlay.Line( pos, pos + Vector3.Up * 50, duration: 2, overlay: true );
			DrawLabel( pos, "Line with Overlay" );
		}

		pos += unit;

		{
			DebugOverlay.Line( pos, pos + Vector3.Up * 50, Color.Green );
			DrawLabel( pos, "Green Line" );
		}

		pos += unit;

		{
			DebugOverlay.Text( pos, "Hello There" );
			DrawLabel( pos, "Text" );
		}

		pos += unit;

		{
			DebugOverlay.Text( pos, "Hello There", overlay: true );
			DrawLabel( pos, "Text Overlay" );
		}

		pos += unit;

		{
			DebugOverlay.Text( pos, "🥹", size: 128 );
			DrawLabel( pos, "Text" );
		}

		pos += unit;

		{
			DebugOverlay.Sphere( new Sphere( pos, 30 ) );
			DrawLabel( pos, "Sphere" );
		}

		pos += unit;

		{
			DebugOverlay.Sphere( new Sphere( 0, 30 ), transform: new Transform( pos, new Angles( Time.Now * 180f, Time.Now * 240f, 0 ) ) );
			DrawLabel( pos, "Sphere" );
		}

		pos += unit;

		{
			DebugOverlay.Box( BBox.FromPositionAndSize( pos, 30 ) );
			DrawLabel( pos, "Box" );
		}

		pos += unit;

		{
			DebugOverlay.Box( BBox.FromPositionAndSize( 0, 30 ), transform: new Transform( pos, new Angles( Time.Now * 180f, Time.Now * 240f, 0 ) ) );
			DrawLabel( pos, "Transformed Box" );
		}

		pos += unit;


		/*
		DrawLineTest( "Line", WorldPosition, 0, false );

		x++;
		DrawLineTest( "Overlay", WorldPosition + unit * x, 0, true );

		x++;
		DrawBoxTest( "Box", WorldPosition + unit * x, 0, false );

		x++;
		DrawSphereTest( "Sphere", WorldPosition + unit * x, 0, false );
		*/

	}

	private void DrawLabel( Vector3 pos, string text )
	{
		DebugOverlay.Text( pos + Vector3.Down * 50, text );
	}

	private void DrawLineTest( string text, Vector3 pos, float duration, bool ignorez )
	{

		DebugOverlay.Text( pos + Vector3.Down * 50, text, overlay: ignorez );
	}

	private void DrawBoxTest( string text, Vector3 pos, float duration, bool ignorez )
	{
		DebugOverlay.Text( pos + Vector3.Down * 50, text, overlay: ignorez, color: Color.White );

		if ( duration > 0 ) pos += Noise.FbmVector( 1, Time.Now * 200 ) * 200;

		DebugOverlay.Box( pos, new Vector3( 50 + MathF.Sin( Time.Now * 5.0f ) * 20, 50 + MathF.Cos( Time.Now * 5.0f ) * 20, 50 + MathF.Sin( Time.Now * 6.0f ) * 20 ), duration: duration, overlay: ignorez );
	}

	private void DrawSphereTest( string text, Vector3 pos, float duration, bool ignorez )
	{
		DebugOverlay.Text( pos + Vector3.Down * 50, text, overlay: ignorez );

		if ( duration > 0 ) pos += Noise.FbmVector( 1, Time.Now * 200 ) * 200;
		DebugOverlay.Sphere( new Sphere( pos, 50 ), duration: duration, overlay: ignorez );
	}

	void DrawAllBones()
	{
		foreach ( var c in Scene.GetAll<SkinnedModelRenderer>() )
		{
			for ( int i = 0; i < c.Model.BoneCount; i++ )
			{
				var o = c.GetBoneObject( i );

				Color color = Color.White;

				if ( o.Name.Contains( "leg" ) ) color = Color.Orange;
				if ( o.Name.Contains( "arm" ) ) color = Color.Cyan;

				DebugOverlay.Text( o.WorldPosition, o.Name, flags: TextFlag.LeftCenter, size: 8, overlay: true, color: color );
			}
		}

	}
}
