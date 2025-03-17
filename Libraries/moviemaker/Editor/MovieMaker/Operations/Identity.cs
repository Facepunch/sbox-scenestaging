using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

public sealed record IdentityOperation<T>( PropertySignal<T> Signal )
	: UnaryOperation<T>( Signal )
{
	public override T GetValue( MovieTime time ) => Signal.GetValue( time );
}
