using Sandbox;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Takes a bunch of transforms and times and allows querying interpolation between them
/// </summary>
public class TransformInterpolate
{
	public record struct Entry( float Time, Transform Transform );
	public List<Entry> entries = new List<Entry>( 100 );

	public Entry start;

	public void Clear( Transform tx )
	{
		start = new Entry( Time.Now, tx );
		entries.Clear();
	}

	public void Add( in float time, in Transform tx )
	{
		// last entry was this time, remove it
		if ( entries.Count > 0 )
		{
			var lastEntry = entries.Last();
			if ( lastEntry.Time == time )
				entries.RemoveAt( entries.Count - 1 );
		}

		entries.Add( new Entry( time, tx ) );
	}

	public bool Query( float now, ref Transform transform )
	{
		if ( entries.Count == 0 || start.Time == 0 )
		{
			start = new Entry( now, transform );
			return false;
		}

		var i = entries.FindIndex( x => x.Time >= now );
		if ( i < 0 ) return false;

		var from = start;

		if ( i > 0 )
		{
			from = entries[i - 1];
		}

		var to = entries[i];
		var delta = MathX.Remap( now, from.Time, to.Time, 0.0f, 1.0f );
		transform = Transform.Lerp( from.Transform, to.Transform, delta, true );

		return true;
	}

	public void CullOlderThan( float time )
	{
		entries.RemoveAll( x => x.Time < time );
	}
}
