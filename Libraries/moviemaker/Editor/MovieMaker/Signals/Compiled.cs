using System.Linq;
using Sandbox.MovieMaker.Compiled;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

[JsonDiscriminator( "Compiled" )]
file sealed record CompiledSignal<T>( CompiledPropertyBlock<T> Block ) : PropertySignal<T>
{
	public override T GetValue( MovieTime time ) => Block.GetValue( time );
}

partial record PropertySignal<T>
{
	public static implicit operator PropertySignal<T>( CompiledPropertyBlock<T> block ) => new CompiledSignal<T>( block );
}

partial class PropertySignalExtensions
{
	public static PropertySignal<T> AsSignal<T>( this CompiledPropertyBlock<T> block )
	{
		return new CompiledSignal<T>( block );
	}

	/// <summary>
	/// Creates a signal that joins together the given blocks.
	/// </summary>
	public static PropertySignal<T>? AsSignal<T>( this IEnumerable<CompiledPropertyBlock<T>> blocks ) => blocks
		.Select( x => new PropertyBlock<T>( x, x.TimeRange ) )
		.AsSignal();
}
