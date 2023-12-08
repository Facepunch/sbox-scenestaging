using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

public sealed class TestCamPlayer : Component
{
	public TestGameManager Manager { get; set; }

	protected override void OnAwake()
	{
		base.OnAwake();

		
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		TestBall targetBall = null;
		var tr = Physics.Trace.Ray( new Ray( Transform.Position, Screen.GetDirection( Mouse.Position ) ), 1500f )
				.WithTag( "ball" )
				.Run();

		Manager.CursorType = CursorType.Pointer;

		if ( tr.Hit )
		{
			GameObject ballObj = tr.Body.GameObject as GameObject;
			targetBall = ballObj.Components.Get<TestBall>();

			if ( !targetBall.IsLocked )
				Manager.CursorType = CursorType.Crosshair;
		}

		if ( Input.Pressed( "Attack1" ) )
		{
			if ( targetBall != null)
				targetBall.DestroyClick();
		}
	}
}
