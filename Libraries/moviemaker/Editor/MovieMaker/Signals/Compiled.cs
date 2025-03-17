using System.Linq;
using Sandbox.MovieMaker.Compiled;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

[JsonDiscriminator( "Compiled" )]
file sealed record CompiledSignal<T>( CompiledPropertyBlock<T> Block, MovieTime Offset = default ) : PropertySignal<T>
{
	public override T GetValue( MovieTime time ) => Block.GetValue( time - Offset );

	protected override PropertySignal<T> OnReduce( MovieTime offset, MovieTime? start, MovieTime? end )
	{
		if ( start >= Block.TimeRange.End + Offset + offset ) return Block.GetValue( Block.TimeRange.End );
		if ( end <= Block.TimeRange.Start + Offset + offset ) return Block.GetValue( Block.TimeRange.Start );

		if ( offset.IsZero ) return this;

		return this with { Offset = Offset + offset };
	}
}

partial record PropertySignal<T>
{
	public static implicit operator PropertySignal<T>( CompiledPropertyBlock<T> block ) => new CompiledSignal<T>( block );
}

partial class PropertySignalExtensions
{
	public static PropertySignal<T> AsSignal<T>( this CompiledPropertyBlock<T> block, MovieTime offset = default )
	{
		return new CompiledSignal<T>( block, offset );
	}

	/// <summary>
	/// Creates a signal that joins together the given blocks.
	/// </summary>
	public static PropertySignal<T>? AsSignal<T>( this IEnumerable<CompiledPropertyBlock<T>> blocks ) => blocks
		.Select( x => new PropertyBlock<T>( x, x.TimeRange ) )
		.AsSignal();
}
