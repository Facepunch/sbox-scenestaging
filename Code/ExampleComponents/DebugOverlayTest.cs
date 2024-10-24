using Sandbox.Utility;

public sealed class DebugOverlayTest : Component, Component.ExecuteInEditor
{
	DebugDrawSystem Overlay => DebugDrawSystem.Current;

	protected override void OnUpdate()
	{
		var unit = Vector3.Right * 300;
		var up = Vector3.Up * 200;
		int x = 0;

		DrawLineTest( "Line", WorldPosition, 0, false );
		DrawLineTest( "Line - 2s", WorldPosition + up, 2, false );

		x++;
		DrawLineTest( "Overlay", WorldPosition + unit * x, 0, true );
		DrawLineTest( "2s Overlay", WorldPosition + Vector3.Up * 200 + unit * x, 2, true );

		x++;
		DrawBoxTest( "Box", WorldPosition + unit * x, 0, false );
		DrawBoxTest( "Box - 1s", WorldPosition + unit * x + up, 1, false );

		x++;
		DrawSphereTest( "Sphere", WorldPosition + unit * x, 0, false );
		DrawSphereTest( "Sphere - 1s", WorldPosition + unit * x + up, 1, false );

		DrawAllBones();
	}

	private void DrawLineTest( string text, Vector3 pos, float duration, bool ignorez )
	{
		Overlay.Line( pos + Vector3.Random * 100, pos + Vector3.Random * 100, duration: duration, overlay: ignorez );
		Overlay.Text( pos + Vector3.Down * 50, text, overlay: ignorez );
	}

	private void DrawBoxTest( string text, Vector3 pos, float duration, bool ignorez )
	{
		Overlay.Text( pos + Vector3.Down * 50, text, overlay: ignorez, color: Color.White );

		if ( duration > 0 ) pos += Noise.FbmVector( 1, Time.Now * 200 ) * 200;

		Overlay.Box( pos, new Vector3( 50 + MathF.Sin( Time.Now * 5.0f ) * 20, 50 + MathF.Cos( Time.Now * 5.0f ) * 20, 50 + MathF.Sin( Time.Now * 6.0f ) * 20 ), duration: duration, overlay: ignorez );
	}

	private void DrawSphereTest( string text, Vector3 pos, float duration, bool ignorez )
	{
		Overlay.Text( pos + Vector3.Down * 50, text, overlay: ignorez );

		if ( duration > 0 ) pos += Noise.FbmVector( 1, Time.Now * 200 ) * 200;
		Overlay.Sphere( new Sphere( pos, 50 ), duration: duration, overlay: ignorez );
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

				Overlay.Text( o.WorldPosition, o.Name, flags: TextFlag.LeftCenter, size: 8, overlay: true, color: color );
			}
		}

	}
}
