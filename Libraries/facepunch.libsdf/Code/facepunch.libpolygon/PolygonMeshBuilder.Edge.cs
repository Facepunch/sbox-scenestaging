
namespace Sandbox.Polygons;

partial class PolygonMeshBuilder
{
	private struct Edge
	{
		public int Index { get; }

		public Vector2 Origin { get; }
		public Vector2 Tangent { get; }
		public Vector2 Normal { get; }

		public Vector2 Velocity { get; set; }

		public int PrevEdge { get; set; }
		public int NextEdge { get; set; }

		public float Distance { get; set; }
		public float MaxDistance { get; set; }

		public (int Prev, int Next) Vertices { get; set; }

		public int Twin { get; }

		public Edge( int index, Vector2 origin, Vector2 tangent, float distance, int twin = -1 )
		{
			Index = index;

			Origin = origin;
			Tangent = tangent;
			Normal = Helpers.Rotate90( tangent );

			Velocity = Vector2.Zero;

			PrevEdge = -1;
			NextEdge = -1;

			Vertices = (-1, -1);

			Distance = distance;
			MaxDistance = float.PositiveInfinity;

			Twin = twin;
		}

		public readonly Vector2 Project( float distance )
		{
			return Origin + Velocity * (distance - Distance);
		}

		public override string ToString()
		{
			return $"[{Index}]";
		}

		public bool Equals( Edge other )
		{
			return Index == other.Index;
		}

		public override bool Equals( object obj )
		{
			return obj is Edge other && Equals( other );
		}

		public override int GetHashCode()
		{
			return Index;
		}
	}
}
