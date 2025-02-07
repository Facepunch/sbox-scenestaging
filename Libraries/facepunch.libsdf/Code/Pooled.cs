using System;
using System.Collections.Generic;

namespace Sandbox;

public abstract class Pooled<T> : IDisposable
	where T : Pooled<T>, new()
{
#pragma warning disable SB3000
	private const int MaxPoolCount = 64;
	private static List<T> Pool { get; } = new();
#pragma warning restore SB3000

	public static T Rent()
	{
		lock ( Pool )
		{
			if ( Pool.Count <= 0 ) return new T();

			var writer = Pool[^1];
			Pool.RemoveAt( Pool.Count - 1 );

			writer._isInPool = false;
			writer.Reset();

			return writer;
		}
	}

	public void Return()
	{
		lock ( Pool )
		{
			if ( _isInPool ) throw new InvalidOperationException( "Already returned." );

			Reset();

			_isInPool = true;

			if ( Pool.Count < MaxPoolCount ) Pool.Add( (T)this );
		}
	}

	private bool _isInPool;

	public abstract void Reset();

	public void Dispose()
	{
		Return();
	}
}
