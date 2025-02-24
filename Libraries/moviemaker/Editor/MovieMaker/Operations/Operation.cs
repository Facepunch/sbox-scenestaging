using Sandbox.MovieMaker;
using System.Text;

namespace Editor.MovieMaker;

#nullable enable

public abstract record PropertyOperation<T> : PropertySignal<T>
{
	public abstract IReadOnlyList<PropertySignal<T>> Operands { get; }

	protected override bool PrintMembers( StringBuilder builder )
	{
		return false;
	}
}

public abstract record UnaryOperation<T>( PropertySignal<T> Signal ) : PropertyOperation<T>
{
	public override IReadOnlyList<PropertySignal<T>> Operands => [Signal];
}

public sealed record IdentityOperation<T>( PropertySignal<T> Signal ) : UnaryOperation<T>( Signal )
{
	public override T GetValue( MovieTime time ) => Signal.GetValue( time );
}

public abstract record BinaryOperation<T>( PropertySignal<T> First, PropertySignal<T> Second ) : PropertyOperation<T>
{
	public override IReadOnlyList<PropertySignal<T>> Operands => [First, Second];
}
