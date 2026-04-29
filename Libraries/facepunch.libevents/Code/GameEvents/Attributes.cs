
using System;

namespace Sandbox.Events;

/// <summary>
/// Only valid on <see cref="IGameEventHandler{T}.OnGameEvent"/> implementations. Forces this
/// event handler to be invoked before any handlers not marked as early, except if more specific
/// constraints are given (i.e., <see cref="BeforeAttribute{T}"/>, <see cref="AfterAttribute{T}"/>).
/// </summary>
[AttributeUsage( AttributeTargets.Method )]
public sealed class EarlyAttribute : Attribute
{

}

/// <summary>
/// Only valid on <see cref="IGameEventHandler{T}.OnGameEvent"/> implementations. Forces this
/// event handler to be invoked after any handlers not marked as late, except if more specific
/// constraints are given (i.e., <see cref="BeforeAttribute{T}"/>, <see cref="AfterAttribute{T}"/>).
/// </summary>
[AttributeUsage( AttributeTargets.Method )]
public sealed class LateAttribute : Attribute
{

}

internal interface IBeforeAttribute
{
	Type Type { get; }
}

internal interface IAfterAttribute
{
	Type Type { get; }
}

/// <summary>
/// Only valid on <see cref="IGameEventHandler{T}.OnGameEvent"/> implementations. Forces this
/// event handler to be invoked before any handlers in the specified type.
/// </summary>
[AttributeUsage( AttributeTargets.Method, AllowMultiple = true )]
public sealed class BeforeAttribute<T> : Attribute, IBeforeAttribute
{
	Type IBeforeAttribute.Type => typeof(T);
}

/// <summary>
/// Only valid on <see cref="IGameEventHandler{T}.OnGameEvent"/> implementations. Forces this
/// event handler to be invoked after any handlers in the specified type.
/// </summary>
[AttributeUsage( AttributeTargets.Method, AllowMultiple = true )]
public sealed class AfterAttribute<T> : Attribute, IAfterAttribute
{
	Type IAfterAttribute.Type => typeof( T );
}
