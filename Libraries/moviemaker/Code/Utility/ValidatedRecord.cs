namespace Sandbox.MovieMaker;

#nullable enable

/// <summary>
/// Base class for records that want to validate primary constructor arguments.
/// </summary>
public abstract record ValidatedRecord
{
	protected ValidatedRecord()
	{
		// ReSharper disable once VirtualMemberCallInConstructor
		OnValidate();
	}

	protected ValidatedRecord( ValidatedRecord other )
	{
		// ReSharper disable once VirtualMemberCallInConstructor
		OnValidate();
	}

	/// <summary>
	/// Called when this instance is being constructed.
	/// Only primary constructor arguments can be accessed here.
	/// </summary>
	protected virtual void OnValidate() { }
}
