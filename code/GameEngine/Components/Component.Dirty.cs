using Sandbox;
using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

public abstract partial class Component
{
	bool _dirty;

	/// <summary>
	/// Called when a property is set, which will run a callback
	/// </summary>
	protected void OnPropertyDirty<T>( string propertyName, T newValue, Action<T> setter )
	{
		using var callbackBatch = CallbackBatch.StartGroup();

		setter( newValue );

		if ( _dirty )
			return;

		_dirty = true;

		CallbackBatch.Add( CommonCallback.Dirty, OnDirtyInternal, this, "OnDirty" );
	}

	void OnDirtyInternal()
	{
		if ( !_dirty ) return;

		_dirty = false;

		ExceptionWrap( "OnDirty", OnDirty );
	}

	/// <summary>
	/// Called when the component has become dirty
	/// </summary>
	protected virtual void OnDirty()
	{

	}
}


[AttributeUsage( AttributeTargets.Property )]
[CodeGenerator( CodeGeneratorFlags.WrapPropertySet | CodeGeneratorFlags.Instance, "OnPropertyDirty" )]
public class MakeDirtyAttribute : Attribute
{

}
