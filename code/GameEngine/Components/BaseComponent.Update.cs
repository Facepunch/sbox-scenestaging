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

		ExceptionWrap( "Update", Update );
	}

	internal void InternalFixedUpdate()
	{
		if ( !Enabled ) return;
		if ( !ShouldExecute ) return;

		ExceptionWrap( "FixedUpdate", OnFixedUpdate );
	}


	public virtual void Update()
	{

	}

	protected virtual void OnFixedUpdate()
	{

	}
}
