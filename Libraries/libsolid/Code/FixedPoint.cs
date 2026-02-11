using System;

namespace Sandbox.Solids;

public static class FixedPoint
{
	public static short ToFixed( this float value, int shift )
	{
		checked
		{
			return (short)MathF.Round( value * (1 << shift) );
		}
	}

	public static Vertex ToFixed( this Vector3 value, int shift )
	{
		return new Vertex(
			value.x.ToFixed( shift ),
			value.y.ToFixed( shift ),
			value.z.ToFixed( shift ) );
	}
}
