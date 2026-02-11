using System;
using System.Collections.Generic;
using System.Text;

namespace Sandbox.Solids;

public readonly struct Bounds
{
	public Vertex Min { get; }
	public Vertex Max { get; }

	public Bounds( Vertex min, Vertex max )
	{
		Min = min;
		Max = max;
	}

	public bool Touches( Bounds other )
	{
		if ( Min.X > other.Max.X ) return false;
		if ( Min.Y > other.Max.Y ) return false;
		if ( Min.Z > other.Max.Z ) return false;

		if ( Max.X < other.Min.X ) return false;
		if ( Max.Y < other.Min.Y ) return false;
		if ( Max.Z < other.Min.Z ) return false;

		return true;
	}
}
