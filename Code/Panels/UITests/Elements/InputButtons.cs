using Sandbox.UI.Construct;
using System;

namespace Sandbox.UI.Tests.Elements;

public class InputButtons : Panel
{
	readonly ScenePanel scenePanel;

	Angles CamAngles = new( 25.0f, 0.0f, 0.0f );
	Vector3 CamPos;

	public InputButtons()
	{
		AcceptsFocus = true;
		ButtonInput = PanelInputType.Game;

		Style.FlexWrap = Wrap.Wrap;
		Style.JustifyContent = Justify.Center;
		Style.AlignItems = Align.Center;
		Style.AlignContent = Align.Center;
		Style.Padding = 0;
		Style.Width = Length.Percent( 100 );
		Style.Height = Length.Percent( 100 );

		CamPos = Vector3.Up * 10 + CamAngles.Forward * 50;

		var world = new SceneWorld();
		scenePanel = new ScenePanel();
		scenePanel.World = world;
		scenePanel.Camera.Position = CamPos;
		scenePanel.Camera.Rotation = Rotation.From( CamAngles );
		scenePanel.Camera.FieldOfView = 70;
		scenePanel.Style.PointerEvents = PointerEvents.None;

		scenePanel.Style.Width = Length.Percent( 100 );
		scenePanel.Style.Height = Length.Percent( 100 );

		AddChild( scenePanel );

		for ( int i = 0; i < 500; i++ )
		{
			var pos = Vector3.Random * 500;
			if ( pos.Length < 100 ) pos = pos.Normal * 100;
			new SceneModel( world, "models/citizen_props/roadcone01.vmdl", new Transform( pos, Rotation.Random ) );
		}

		new SceneLight( world, Vector3.Up * 150.0f, 200.0f, Color.Red * 5.0f );
		new SceneLight( world, Vector3.Up * 10.0f + Vector3.Forward * 100.0f, 200, Color.White * 15.0f );
		new SceneLight( world, Vector3.Up * 10.0f + Vector3.Backward * 100.0f, 200, Color.Magenta * 15f );
		new SceneLight( world, Vector3.Up * 10.0f + Vector3.Right * 100.0f, 200, Color.Blue * 15.0f );
		new SceneLight( world, Vector3.Up * 10.0f + Vector3.Left * 100.0f, 200, Color.Green * 15.0f );
	}

	protected override void OnMouseDown( MousePanelEvent e )
	{
		// don't pass clicks down to the drag scroller
		e.StopPropagation();
	}

	public override void Tick()
	{
		base.Tick();

		var camRot = CamAngles.ToRotation();
		var velocity = Vector3.Zero;

		if ( HasFocus )
		{
			SetMouseCapture( true );
			CamAngles.pitch += Mouse.Delta.y * 0.2f;
			CamAngles.yaw -= Mouse.Delta.x * 0.2f;
			CamAngles.pitch = CamAngles.pitch.Clamp( -89, 89 );
		}
		else
		{
			SetMouseCapture( false );
		}

		if ( Input.Down( "Forward" ) ) velocity += camRot.Forward;
		if ( Input.Down( "Backward" ) ) velocity += camRot.Backward;
		if ( Input.Down( "Left" ) ) velocity += camRot.Left;
		if ( Input.Down( "Right" ) ) velocity += camRot.Right;

		velocity = velocity.Normal;

		if ( Input.Down( "run" ) ) velocity *= 4;

		CamPos += velocity * Time.Delta * 100.0f;

		scenePanel.Camera.Position = CamPos;
		scenePanel.Camera.Rotation = Rotation.From( CamAngles );

		float f = 1;
		foreach ( var obj in scenePanel.World.SceneObjects )
		{
			if ( obj is not SceneLight ) continue;

			obj.Position += new Vector3( MathF.Sin( f + RealTime.Now * 2.0f ), MathF.Cos( f + RealTime.Now * 2.0f ), 0 ) * RealTime.Delta * 100.0f;
			f += 1.0f;
		}
	}
}
