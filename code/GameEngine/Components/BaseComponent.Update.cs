using Sandbox;
using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

public abstract partial class BaseComponent
{
	bool _startCalled;

	internal void InternalUpdate()
	{
		if ( !Enabled ) return;
		if ( !ShouldExecute ) return;

		if ( !_startCalled )
		{
			_startCalled = true;
			ExceptionWrap( "Start", OnStart );
		}

		ExceptionWrap( "Update", OnUpdate );
	}

	internal void InternalFixedUpdate()
	{
		if ( !Enabled ) return;
		if ( !ShouldExecute ) return;

		ExceptionWrap( "FixedUpdate", OnFixedUpdate );
	}


	protected virtual void OnUpdate()
	{

	}

	protected virtual void OnFixedUpdate()
	{

	}
}
