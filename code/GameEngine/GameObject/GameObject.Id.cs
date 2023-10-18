using Sandbox;
using Sandbox.Diagnostics;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;


public partial class GameObject
{
	private Guid _id;
	public Guid Id
	{
		get => _id;
		private set
		{
			if ( _id == value ) return;

			var oldId = _id;
			_id = value;

			Scene?.Directory?.Add( this, oldId );
		}

	}

	/// <summary>
	/// Should only be called by Scene.RegisterGameObjectId
	/// </summary>
	internal void ForceChangeId( Guid guid )
	{
		_id = guid;
	}
}
