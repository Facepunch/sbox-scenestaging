using Facepunch.ActionGraphs;
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

	protected override PropertySignal<T> OnReduce( MovieTime offset, MovieTime? start, MovieTime? end )
	{
		return this with { Signal = Signal.Reduce( offset, start, end ) };
	}
}

public abstract record BinaryOperation<T>( PropertySignal<T> First, PropertySignal<T> Second )
	: PropertyOperation<T>
{
	public override IReadOnlyList<PropertySignal<T>> Operands => [First, Second];

	protected override PropertySignal<T> OnReduce( MovieTime offset, MovieTime? start, MovieTime? end )
	{
		return this with { First = First.Reduce( offset, start, end ), Second = Second.Reduce( offset, start, end ) };
	}
}
