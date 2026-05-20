using Sandbox;

namespace Voxels;

partial class VoxelSystem
{
	private enum VoxelEdge : uint
	{
		AB = 0,
		AC = 1,
		AE = 2
	}

	private readonly record struct VertexData( Vector3Int Origin, VoxelEdge Edge );
	private readonly record struct TriangleData( VertexData A, VertexData B, VertexData C );

	private readonly record struct MarchingCubesLookupEntry(
		uint TriangleCount,
		TriangleData Tri0,
		TriangleData Tri1,
		TriangleData Tri2,
		TriangleData Tri3,
		TriangleData Tri4 )
	{
		public MarchingCubesLookupEntry( params TriangleData[] triangles )
			: this( (uint)triangles.Length,
				triangles.Length > 0 ? triangles[0] : default,
				triangles.Length > 1 ? triangles[1] : default,
				triangles.Length > 2 ? triangles[2] : default,
				triangles.Length > 3 ? triangles[3] : default,
				triangles.Length > 4 ? triangles[4] : default )
		{

		}
	}

	private static class Vertices
	{
		public static VertexData AB { get; } = new( new Vector3Int( 0, 0, 0 ), VoxelEdge.AB );
		public static VertexData AC { get; } = new( new Vector3Int( 0, 0, 0 ), VoxelEdge.AC );
		public static VertexData AE { get; } = new( new Vector3Int( 0, 0, 0 ), VoxelEdge.AE );
		public static VertexData BD { get; } = new( new Vector3Int( 1, 0, 0 ), VoxelEdge.AC );
		public static VertexData BF { get; } = new( new Vector3Int( 1, 0, 0 ), VoxelEdge.AE );
		public static VertexData CD { get; } = new( new Vector3Int( 0, 1, 0 ), VoxelEdge.AB );
		public static VertexData CG { get; } = new( new Vector3Int( 0, 1, 0 ), VoxelEdge.AE );
		public static VertexData DH { get; } = new( new Vector3Int( 1, 1, 0 ), VoxelEdge.AE );
		public static VertexData EF { get; } = new( new Vector3Int( 0, 0, 1 ), VoxelEdge.AB );
		public static VertexData EG { get; } = new( new Vector3Int( 0, 0, 1 ), VoxelEdge.AC );
		public static VertexData FH { get; } = new( new Vector3Int( 1, 0, 1 ), VoxelEdge.AC );
		public static VertexData GH { get; } = new( new Vector3Int( 0, 1, 1 ), VoxelEdge.AB );
	}

	private GpuBuffer<MarchingCubesLookupEntry>? _marchingCubesLookup;

	private GpuBuffer<MarchingCubesLookupEntry> GenerateMarchingCubesLookupTable()
	{
		if ( _marchingCubesLookup is not null ) return _marchingCubesLookup;

		_marchingCubesLookup = new GpuBuffer<MarchingCubesLookupEntry>( 256 );

		MarchingCubesLookupEntry[] entries =
		[
			// CubeConfiguration.None:
			new(),

			// CubeConfiguration.A:
			new(
				new TriangleData( Vertices.AC, Vertices.AE, Vertices.AB )
			),

			// CubeConfiguration.B:
			new(
				new TriangleData( Vertices.BF, Vertices.BD, Vertices.AB )
			),

			// CubeConfiguration.A | CubeConfiguration.B:
			new(
				new TriangleData( Vertices.BF, Vertices.BD, Vertices.AC ),
				new TriangleData( Vertices.BF, Vertices.AC, Vertices.AE )
			),

			// CubeConfiguration.C:
			new(
				new TriangleData( Vertices.CD, Vertices.CG, Vertices.AC )
			),

			// CubeConfiguration.A | CubeConfiguration.C:
			new(
				new TriangleData( Vertices.CD, Vertices.CG, Vertices.AE ),
				new TriangleData( Vertices.CD, Vertices.AE, Vertices.AB )
			),

			// CubeConfiguration.B | CubeConfiguration.C:
			new(
				new TriangleData( Vertices.BF, Vertices.BD, Vertices.CD ),
				new TriangleData( Vertices.BF, Vertices.CD, Vertices.CG ),
				new TriangleData( Vertices.BF, Vertices.CG, Vertices.AC ),
				new TriangleData( Vertices.BF, Vertices.AC, Vertices.AB )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.C:
			new(
				new TriangleData( Vertices.BF, Vertices.BD, Vertices.CD ),
				new TriangleData( Vertices.BF, Vertices.CD, Vertices.CG ),
				new TriangleData( Vertices.BF, Vertices.CG, Vertices.AE )
			),

			// CubeConfiguration.D:
			new(
				new TriangleData( Vertices.BD, Vertices.DH, Vertices.CD )
			),

			// CubeConfiguration.A | CubeConfiguration.D:
			new(
				new TriangleData( Vertices.BD, Vertices.DH, Vertices.CD ),
				new TriangleData( Vertices.BD, Vertices.CD, Vertices.AC ),
				new TriangleData( Vertices.BD, Vertices.AC, Vertices.AE ),
				new TriangleData( Vertices.BD, Vertices.AE, Vertices.AB )
			),

			// CubeConfiguration.B | CubeConfiguration.D:
			new(
				new TriangleData( Vertices.BF, Vertices.DH, Vertices.CD ),
				new TriangleData( Vertices.BF, Vertices.CD, Vertices.AB )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.D:
			new(
				new TriangleData( Vertices.BF, Vertices.DH, Vertices.CD ),
				new TriangleData( Vertices.BF, Vertices.CD, Vertices.AC ),
				new TriangleData( Vertices.BF, Vertices.AC, Vertices.AE )
			),

			// CubeConfiguration.C | CubeConfiguration.D:
			new(
				new TriangleData( Vertices.BD, Vertices.DH, Vertices.CG ),
				new TriangleData( Vertices.BD, Vertices.CG, Vertices.AC )
			),

			// CubeConfiguration.A | CubeConfiguration.C | CubeConfiguration.D:
			new(
				new TriangleData( Vertices.BD, Vertices.DH, Vertices.CG ),
				new TriangleData( Vertices.BD, Vertices.CG, Vertices.AE ),
				new TriangleData( Vertices.BD, Vertices.AE, Vertices.AB )
			),

			// CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.D:
			new(
				new TriangleData( Vertices.BF, Vertices.DH, Vertices.CG ),
				new TriangleData( Vertices.BF, Vertices.CG, Vertices.AC ),
				new TriangleData( Vertices.BF, Vertices.AC, Vertices.AB )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.D:
			new(
				new TriangleData( Vertices.BF, Vertices.DH, Vertices.CG ),
				new TriangleData( Vertices.BF, Vertices.CG, Vertices.AE )
			),

			// CubeConfiguration.E:
			new(
				new TriangleData( Vertices.AE, Vertices.EG, Vertices.EF )
			),

			// CubeConfiguration.A | CubeConfiguration.E:
			new(
				new TriangleData( Vertices.AC, Vertices.EG, Vertices.EF ),
				new TriangleData( Vertices.AC, Vertices.EF, Vertices.AB )
			),

			// CubeConfiguration.B | CubeConfiguration.E:
			new(
				new TriangleData( Vertices.BF, Vertices.BD, Vertices.AB ),
				new TriangleData( Vertices.BF, Vertices.AB, Vertices.AE ),
				new TriangleData( Vertices.BF, Vertices.AE, Vertices.EG ),
				new TriangleData( Vertices.BF, Vertices.EG, Vertices.EF )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.E:
			new(
				new TriangleData( Vertices.BF, Vertices.BD, Vertices.AC ),
				new TriangleData( Vertices.BF, Vertices.AC, Vertices.EG ),
				new TriangleData( Vertices.BF, Vertices.EG, Vertices.EF )
			),

			// CubeConfiguration.C | CubeConfiguration.E:
			new(
				new TriangleData( Vertices.CD, Vertices.CG, Vertices.EG ),
				new TriangleData( Vertices.CD, Vertices.EG, Vertices.EF ),
				new TriangleData( Vertices.CD, Vertices.EF, Vertices.AE ),
				new TriangleData( Vertices.CD, Vertices.AE, Vertices.AC )
			),

			// CubeConfiguration.A | CubeConfiguration.C | CubeConfiguration.E:
			new(
				new TriangleData( Vertices.CD, Vertices.CG, Vertices.EG ),
				new TriangleData( Vertices.CD, Vertices.EG, Vertices.EF ),
				new TriangleData( Vertices.CD, Vertices.EF, Vertices.AB )
			),

			// CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.E:
			new(
				new TriangleData( Vertices.BF, Vertices.BD, Vertices.CD ),
				new TriangleData( Vertices.BF, Vertices.CD, Vertices.CG ),
				new TriangleData( Vertices.BF, Vertices.CG, Vertices.EG ),
				new TriangleData( Vertices.BF, Vertices.EG, Vertices.EF ),

				new TriangleData( Vertices.AE, Vertices.AC, Vertices.AB )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.E:
			new(
				new TriangleData( Vertices.BF, Vertices.BD, Vertices.CD ),
				new TriangleData( Vertices.BF, Vertices.CD, Vertices.CG ),
				new TriangleData( Vertices.BF, Vertices.CG, Vertices.EG ),
				new TriangleData( Vertices.BF, Vertices.EG, Vertices.EF )
			),

			// CubeConfiguration.D | CubeConfiguration.E:
			new(
				new TriangleData( Vertices.BD, Vertices.DH, Vertices.CD ),

				new TriangleData( Vertices.AE, Vertices.EG, Vertices.EF )
			),

			// CubeConfiguration.A | CubeConfiguration.D | CubeConfiguration.E:
			new(
				new TriangleData( Vertices.BD, Vertices.DH, Vertices.CD ),
				new TriangleData( Vertices.BD, Vertices.CD, Vertices.AC ),
				new TriangleData( Vertices.BD, Vertices.AC, Vertices.EG ),
				new TriangleData( Vertices.BD, Vertices.EG, Vertices.EF ),
				new TriangleData( Vertices.BD, Vertices.EF, Vertices.AB )
			),

			// CubeConfiguration.B | CubeConfiguration.D | CubeConfiguration.E:
			new(
				new TriangleData( Vertices.BF, Vertices.DH, Vertices.CD ),
				new TriangleData( Vertices.BF, Vertices.CD, Vertices.AB ),
				new TriangleData( Vertices.BF, Vertices.AB, Vertices.AE ),
				new TriangleData( Vertices.BF, Vertices.AE, Vertices.EG ),
				new TriangleData( Vertices.BF, Vertices.EG, Vertices.EF )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.D | CubeConfiguration.E:
			new(
				new TriangleData( Vertices.BF, Vertices.DH, Vertices.CD ),
				new TriangleData( Vertices.BF, Vertices.CD, Vertices.AC ),
				new TriangleData( Vertices.BF, Vertices.AC, Vertices.EG ),
				new TriangleData( Vertices.BF, Vertices.EG, Vertices.EF )
			),

			// CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.E:
			new(
				new TriangleData( Vertices.BD, Vertices.DH, Vertices.CG ),
				new TriangleData( Vertices.BD, Vertices.CG, Vertices.EG ),
				new TriangleData( Vertices.BD, Vertices.EG, Vertices.EF ),
				new TriangleData( Vertices.BD, Vertices.EF, Vertices.AE ),
				new TriangleData( Vertices.BD, Vertices.AE, Vertices.AC )
			),

			// CubeConfiguration.A | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.E:
			new(
				new TriangleData( Vertices.BD, Vertices.DH, Vertices.CG ),
				new TriangleData( Vertices.BD, Vertices.CG, Vertices.EG ),
				new TriangleData( Vertices.BD, Vertices.EG, Vertices.EF ),
				new TriangleData( Vertices.BD, Vertices.EF, Vertices.AB )
			),

			// CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.E:
			new(
				new TriangleData( Vertices.BF, Vertices.DH, Vertices.CG ),
				new TriangleData( Vertices.BF, Vertices.CG, Vertices.EG ),
				new TriangleData( Vertices.BF, Vertices.EG, Vertices.EF ),

				new TriangleData( Vertices.AE, Vertices.AC, Vertices.AB )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.E:
			new(
				new TriangleData( Vertices.BF, Vertices.DH, Vertices.CG ),
				new TriangleData( Vertices.BF, Vertices.CG, Vertices.EG ),
				new TriangleData( Vertices.BF, Vertices.EG, Vertices.EF )
			),

			// CubeConfiguration.F:
			new(
				new TriangleData( Vertices.FH, Vertices.BF, Vertices.EF )
			),

			// CubeConfiguration.A | CubeConfiguration.F:
			new(
				new TriangleData( Vertices.FH, Vertices.BF, Vertices.AB ),
				new TriangleData( Vertices.FH, Vertices.AB, Vertices.AC ),
				new TriangleData( Vertices.FH, Vertices.AC, Vertices.AE ),
				new TriangleData( Vertices.FH, Vertices.AE, Vertices.EF )
			),

			// CubeConfiguration.B | CubeConfiguration.F:
			new(
				new TriangleData( Vertices.FH, Vertices.BD, Vertices.AB ),
				new TriangleData( Vertices.FH, Vertices.AB, Vertices.EF )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.F:
			new(
				new TriangleData( Vertices.FH, Vertices.BD, Vertices.AC ),
				new TriangleData( Vertices.FH, Vertices.AC, Vertices.AE ),
				new TriangleData( Vertices.FH, Vertices.AE, Vertices.EF )
			),

			// CubeConfiguration.C | CubeConfiguration.F:
			new(
				new TriangleData( Vertices.FH, Vertices.BF, Vertices.EF ),

				new TriangleData( Vertices.CD, Vertices.CG, Vertices.AC )
			),

			// CubeConfiguration.A | CubeConfiguration.C | CubeConfiguration.F:
			new(
				new TriangleData( Vertices.FH, Vertices.BF, Vertices.AB ),
				new TriangleData( Vertices.FH, Vertices.AB, Vertices.CD ),
				new TriangleData( Vertices.FH, Vertices.CD, Vertices.CG ),
				new TriangleData( Vertices.FH, Vertices.CG, Vertices.AE ),
				new TriangleData( Vertices.FH, Vertices.AE, Vertices.EF )
			),

			// CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.F:
			new(
				new TriangleData( Vertices.FH, Vertices.BD, Vertices.CD ),
				new TriangleData( Vertices.FH, Vertices.CD, Vertices.CG ),
				new TriangleData( Vertices.FH, Vertices.CG, Vertices.AC ),
				new TriangleData( Vertices.FH, Vertices.AC, Vertices.AB ),
				new TriangleData( Vertices.FH, Vertices.AB, Vertices.EF )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.F:
			new(
				new TriangleData( Vertices.FH, Vertices.BD, Vertices.CD ),
				new TriangleData( Vertices.FH, Vertices.CD, Vertices.CG ),
				new TriangleData( Vertices.FH, Vertices.CG, Vertices.AE ),
				new TriangleData( Vertices.FH, Vertices.AE, Vertices.EF )
			),

			// CubeConfiguration.D | CubeConfiguration.F:
			new(
				new TriangleData( Vertices.FH, Vertices.DH, Vertices.CD ),
				new TriangleData( Vertices.FH, Vertices.CD, Vertices.BD ),
				new TriangleData( Vertices.FH, Vertices.BD, Vertices.BF ),
				new TriangleData( Vertices.FH, Vertices.BF, Vertices.EF )
			),

			// CubeConfiguration.A | CubeConfiguration.D | CubeConfiguration.F:
			new(
				new TriangleData( Vertices.FH, Vertices.DH, Vertices.CD ),
				new TriangleData( Vertices.FH, Vertices.CD, Vertices.AC ),
				new TriangleData( Vertices.FH, Vertices.AC, Vertices.AE ),
				new TriangleData( Vertices.FH, Vertices.AE, Vertices.EF ),

				new TriangleData( Vertices.BD, Vertices.BF, Vertices.AB )
			),

			// CubeConfiguration.B | CubeConfiguration.D | CubeConfiguration.F:
			new(
				new TriangleData( Vertices.FH, Vertices.DH, Vertices.CD ),
				new TriangleData( Vertices.FH, Vertices.CD, Vertices.AB ),
				new TriangleData( Vertices.FH, Vertices.AB, Vertices.EF )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.D | CubeConfiguration.F:
			new(
				new TriangleData( Vertices.FH, Vertices.DH, Vertices.CD ),
				new TriangleData( Vertices.FH, Vertices.CD, Vertices.AC ),
				new TriangleData( Vertices.FH, Vertices.AC, Vertices.AE ),
				new TriangleData( Vertices.FH, Vertices.AE, Vertices.EF )
			),

			// CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.F:
			new(
				new TriangleData( Vertices.FH, Vertices.DH, Vertices.CG ),
				new TriangleData( Vertices.FH, Vertices.CG, Vertices.AC ),
				new TriangleData( Vertices.FH, Vertices.AC, Vertices.BD ),
				new TriangleData( Vertices.FH, Vertices.BD, Vertices.BF ),
				new TriangleData( Vertices.FH, Vertices.BF, Vertices.EF )
			),

			// CubeConfiguration.A | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.F:
			new(
				new TriangleData( Vertices.FH, Vertices.DH, Vertices.CG ),
				new TriangleData( Vertices.FH, Vertices.CG, Vertices.AE ),
				new TriangleData( Vertices.FH, Vertices.AE, Vertices.EF ),

				new TriangleData( Vertices.BD, Vertices.BF, Vertices.AB )
			),

			// CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.F:
			new(
				new TriangleData( Vertices.FH, Vertices.DH, Vertices.CG ),
				new TriangleData( Vertices.FH, Vertices.CG, Vertices.AC ),
				new TriangleData( Vertices.FH, Vertices.AC, Vertices.AB ),
				new TriangleData( Vertices.FH, Vertices.AB, Vertices.EF )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.F:
			new(
				new TriangleData( Vertices.FH, Vertices.DH, Vertices.CG ),
				new TriangleData( Vertices.FH, Vertices.CG, Vertices.AE ),
				new TriangleData( Vertices.FH, Vertices.AE, Vertices.EF )
			),

			// CubeConfiguration.E | CubeConfiguration.F:
			new(
				new TriangleData( Vertices.FH, Vertices.BF, Vertices.AE ),
				new TriangleData( Vertices.FH, Vertices.AE, Vertices.EG )
			),

			// CubeConfiguration.A | CubeConfiguration.E | CubeConfiguration.F:
			new(
				new TriangleData( Vertices.FH, Vertices.BF, Vertices.AB ),
				new TriangleData( Vertices.FH, Vertices.AB, Vertices.AC ),
				new TriangleData( Vertices.FH, Vertices.AC, Vertices.EG )
			),

			// CubeConfiguration.B | CubeConfiguration.E | CubeConfiguration.F:
			new(
				new TriangleData( Vertices.FH, Vertices.BD, Vertices.AB ),
				new TriangleData( Vertices.FH, Vertices.AB, Vertices.AE ),
				new TriangleData( Vertices.FH, Vertices.AE, Vertices.EG )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.E | CubeConfiguration.F:
			new(
				new TriangleData( Vertices.FH, Vertices.BD, Vertices.AC ),
				new TriangleData( Vertices.FH, Vertices.AC, Vertices.EG )
			),

			// CubeConfiguration.C | CubeConfiguration.E | CubeConfiguration.F:
			new(
				new TriangleData( Vertices.FH, Vertices.BF, Vertices.AE ),
				new TriangleData( Vertices.FH, Vertices.AE, Vertices.AC ),
				new TriangleData( Vertices.FH, Vertices.AC, Vertices.CD ),
				new TriangleData( Vertices.FH, Vertices.CD, Vertices.CG ),
				new TriangleData( Vertices.FH, Vertices.CG, Vertices.EG )
			),

			// CubeConfiguration.A | CubeConfiguration.C | CubeConfiguration.E | CubeConfiguration.F:
			new(
				new TriangleData( Vertices.FH, Vertices.BF, Vertices.AB ),
				new TriangleData( Vertices.FH, Vertices.AB, Vertices.CD ),
				new TriangleData( Vertices.FH, Vertices.CD, Vertices.CG ),
				new TriangleData( Vertices.FH, Vertices.CG, Vertices.EG )
			),

			// CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.E | CubeConfiguration.F:
			new(
				new TriangleData( Vertices.FH, Vertices.BD, Vertices.CD ),
				new TriangleData( Vertices.FH, Vertices.CD, Vertices.CG ),
				new TriangleData( Vertices.FH, Vertices.CG, Vertices.EG ),

				new TriangleData( Vertices.AE, Vertices.AC, Vertices.AB )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.E | CubeConfiguration.F:
			new(
				new TriangleData( Vertices.FH, Vertices.BD, Vertices.CD ),
				new TriangleData( Vertices.FH, Vertices.CD, Vertices.CG ),
				new TriangleData( Vertices.FH, Vertices.CG, Vertices.EG )
			),

			// CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.F:
			new(
				new TriangleData( Vertices.FH, Vertices.DH, Vertices.CD ),
				new TriangleData( Vertices.FH, Vertices.CD, Vertices.BD ),
				new TriangleData( Vertices.FH, Vertices.BD, Vertices.BF ),
				new TriangleData( Vertices.FH, Vertices.BF, Vertices.AE ),
				new TriangleData( Vertices.FH, Vertices.AE, Vertices.EG )
			),

			// CubeConfiguration.A | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.F:
			new(
				new TriangleData( Vertices.FH, Vertices.DH, Vertices.CD ),
				new TriangleData( Vertices.FH, Vertices.CD, Vertices.AC ),
				new TriangleData( Vertices.FH, Vertices.AC, Vertices.EG ),

				new TriangleData( Vertices.BD, Vertices.BF, Vertices.AB )
			),

			// CubeConfiguration.B | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.F:
			new(
				new TriangleData( Vertices.FH, Vertices.DH, Vertices.CD ),
				new TriangleData( Vertices.FH, Vertices.CD, Vertices.AB ),
				new TriangleData( Vertices.FH, Vertices.AB, Vertices.AE ),
				new TriangleData( Vertices.FH, Vertices.AE, Vertices.EG )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.F:
			new(
				new TriangleData( Vertices.FH, Vertices.DH, Vertices.CD ),
				new TriangleData( Vertices.FH, Vertices.CD, Vertices.AC ),
				new TriangleData( Vertices.FH, Vertices.AC, Vertices.EG )
			),

			// CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.F:
			new(
				new TriangleData( Vertices.FH, Vertices.DH, Vertices.CG ),
				new TriangleData( Vertices.FH, Vertices.CG, Vertices.EG ),

				new TriangleData( Vertices.BD, Vertices.BF, Vertices.AE ),
				new TriangleData( Vertices.BD, Vertices.AE, Vertices.AC )
			),

			// CubeConfiguration.A | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.F:
			new(
				new TriangleData( Vertices.FH, Vertices.DH, Vertices.CG ),
				new TriangleData( Vertices.FH, Vertices.CG, Vertices.EG ),

				new TriangleData( Vertices.BD, Vertices.BF, Vertices.AB )
			),

			// CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.F:
			new(
				new TriangleData( Vertices.FH, Vertices.DH, Vertices.CG ),
				new TriangleData( Vertices.FH, Vertices.CG, Vertices.EG ),

				new TriangleData( Vertices.AE, Vertices.AC, Vertices.AB )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.F:
			new(
				new TriangleData( Vertices.FH, Vertices.DH, Vertices.CG ),
				new TriangleData( Vertices.FH, Vertices.CG, Vertices.EG )
			),

			// CubeConfiguration.G:
			new(
				new TriangleData( Vertices.CG, Vertices.GH, Vertices.EG )
			),

			// CubeConfiguration.A | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.CG, Vertices.GH, Vertices.EG ),
				new TriangleData( Vertices.CG, Vertices.EG, Vertices.AE ),
				new TriangleData( Vertices.CG, Vertices.AE, Vertices.AB ),
				new TriangleData( Vertices.CG, Vertices.AB, Vertices.AC )
			),

			// CubeConfiguration.B | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.BF, Vertices.BD, Vertices.AB ),

				new TriangleData( Vertices.CG, Vertices.GH, Vertices.EG )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.BF, Vertices.BD, Vertices.AC ),
				new TriangleData( Vertices.BF, Vertices.AC, Vertices.CG ),
				new TriangleData( Vertices.BF, Vertices.CG, Vertices.GH ),
				new TriangleData( Vertices.BF, Vertices.GH, Vertices.EG ),
				new TriangleData( Vertices.BF, Vertices.EG, Vertices.AE )
			),

			// CubeConfiguration.C | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.CD, Vertices.GH, Vertices.EG ),
				new TriangleData( Vertices.CD, Vertices.EG, Vertices.AC )
			),

			// CubeConfiguration.A | CubeConfiguration.C | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.CD, Vertices.GH, Vertices.EG ),
				new TriangleData( Vertices.CD, Vertices.EG, Vertices.AE ),
				new TriangleData( Vertices.CD, Vertices.AE, Vertices.AB )
			),

			// CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.BF, Vertices.BD, Vertices.CD ),
				new TriangleData( Vertices.BF, Vertices.CD, Vertices.GH ),
				new TriangleData( Vertices.BF, Vertices.GH, Vertices.EG ),
				new TriangleData( Vertices.BF, Vertices.EG, Vertices.AC ),
				new TriangleData( Vertices.BF, Vertices.AC, Vertices.AB )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.BF, Vertices.BD, Vertices.CD ),
				new TriangleData( Vertices.BF, Vertices.CD, Vertices.GH ),
				new TriangleData( Vertices.BF, Vertices.GH, Vertices.EG ),
				new TriangleData( Vertices.BF, Vertices.EG, Vertices.AE )
			),

			// CubeConfiguration.D | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.BD, Vertices.DH, Vertices.GH ),
				new TriangleData( Vertices.BD, Vertices.GH, Vertices.EG ),
				new TriangleData( Vertices.BD, Vertices.EG, Vertices.CG ),
				new TriangleData( Vertices.BD, Vertices.CG, Vertices.CD )
			),

			// CubeConfiguration.A | CubeConfiguration.D | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.BD, Vertices.DH, Vertices.GH ),
				new TriangleData( Vertices.BD, Vertices.GH, Vertices.EG ),
				new TriangleData( Vertices.BD, Vertices.EG, Vertices.AE ),
				new TriangleData( Vertices.BD, Vertices.AE, Vertices.AB ),

				new TriangleData( Vertices.CG, Vertices.CD, Vertices.AC )
			),

			// CubeConfiguration.B | CubeConfiguration.D | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.BF, Vertices.DH, Vertices.GH ),
				new TriangleData( Vertices.BF, Vertices.GH, Vertices.EG ),
				new TriangleData( Vertices.BF, Vertices.EG, Vertices.CG ),
				new TriangleData( Vertices.BF, Vertices.CG, Vertices.CD ),
				new TriangleData( Vertices.BF, Vertices.CD, Vertices.AB )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.D | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.BF, Vertices.DH, Vertices.GH ),
				new TriangleData( Vertices.BF, Vertices.GH, Vertices.EG ),
				new TriangleData( Vertices.BF, Vertices.EG, Vertices.AE ),

				new TriangleData( Vertices.CG, Vertices.CD, Vertices.AC )
			),

			// CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.BD, Vertices.DH, Vertices.GH ),
				new TriangleData( Vertices.BD, Vertices.GH, Vertices.EG ),
				new TriangleData( Vertices.BD, Vertices.EG, Vertices.AC )
			),

			// CubeConfiguration.A | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.BD, Vertices.DH, Vertices.GH ),
				new TriangleData( Vertices.BD, Vertices.GH, Vertices.EG ),
				new TriangleData( Vertices.BD, Vertices.EG, Vertices.AE ),
				new TriangleData( Vertices.BD, Vertices.AE, Vertices.AB )
			),

			// CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.BF, Vertices.DH, Vertices.GH ),
				new TriangleData( Vertices.BF, Vertices.GH, Vertices.EG ),
				new TriangleData( Vertices.BF, Vertices.EG, Vertices.AC ),
				new TriangleData( Vertices.BF, Vertices.AC, Vertices.AB )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.BF, Vertices.DH, Vertices.GH ),
				new TriangleData( Vertices.BF, Vertices.GH, Vertices.EG ),
				new TriangleData( Vertices.BF, Vertices.EG, Vertices.AE )
			),

			// CubeConfiguration.E | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.CG, Vertices.GH, Vertices.EF ),
				new TriangleData( Vertices.CG, Vertices.EF, Vertices.AE )
			),

			// CubeConfiguration.A | CubeConfiguration.E | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.CG, Vertices.GH, Vertices.EF ),
				new TriangleData( Vertices.CG, Vertices.EF, Vertices.AB ),
				new TriangleData( Vertices.CG, Vertices.AB, Vertices.AC )
			),

			// CubeConfiguration.B | CubeConfiguration.E | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.BF, Vertices.BD, Vertices.AB ),
				new TriangleData( Vertices.BF, Vertices.AB, Vertices.AE ),
				new TriangleData( Vertices.BF, Vertices.AE, Vertices.CG ),
				new TriangleData( Vertices.BF, Vertices.CG, Vertices.GH ),
				new TriangleData( Vertices.BF, Vertices.GH, Vertices.EF )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.E | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.BF, Vertices.BD, Vertices.AC ),
				new TriangleData( Vertices.BF, Vertices.AC, Vertices.CG ),
				new TriangleData( Vertices.BF, Vertices.CG, Vertices.GH ),
				new TriangleData( Vertices.BF, Vertices.GH, Vertices.EF )
			),

			// CubeConfiguration.C | CubeConfiguration.E | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.CD, Vertices.GH, Vertices.EF ),
				new TriangleData( Vertices.CD, Vertices.EF, Vertices.AE ),
				new TriangleData( Vertices.CD, Vertices.AE, Vertices.AC )
			),

			// CubeConfiguration.A | CubeConfiguration.C | CubeConfiguration.E | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.CD, Vertices.GH, Vertices.EF ),
				new TriangleData( Vertices.CD, Vertices.EF, Vertices.AB )
			),

			// CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.E | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.BF, Vertices.BD, Vertices.CD ),
				new TriangleData( Vertices.BF, Vertices.CD, Vertices.GH ),
				new TriangleData( Vertices.BF, Vertices.GH, Vertices.EF ),

				new TriangleData( Vertices.AE, Vertices.AC, Vertices.AB )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.E | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.BF, Vertices.BD, Vertices.CD ),
				new TriangleData( Vertices.BF, Vertices.CD, Vertices.GH ),
				new TriangleData( Vertices.BF, Vertices.GH, Vertices.EF )
			),

			// CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.BD, Vertices.DH, Vertices.GH ),
				new TriangleData( Vertices.BD, Vertices.GH, Vertices.EF ),
				new TriangleData( Vertices.BD, Vertices.EF, Vertices.AE ),
				new TriangleData( Vertices.BD, Vertices.AE, Vertices.CG ),
				new TriangleData( Vertices.BD, Vertices.CG, Vertices.CD )
			),

			// CubeConfiguration.A | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.BD, Vertices.DH, Vertices.GH ),
				new TriangleData( Vertices.BD, Vertices.GH, Vertices.EF ),
				new TriangleData( Vertices.BD, Vertices.EF, Vertices.AB ),

				new TriangleData( Vertices.CG, Vertices.CD, Vertices.AC )
			),

			// CubeConfiguration.B | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.BF, Vertices.DH, Vertices.GH ),
				new TriangleData( Vertices.BF, Vertices.GH, Vertices.EF ),

				new TriangleData( Vertices.CG, Vertices.CD, Vertices.AB ),
				new TriangleData( Vertices.CG, Vertices.AB, Vertices.AE )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.BF, Vertices.DH, Vertices.GH ),
				new TriangleData( Vertices.BF, Vertices.GH, Vertices.EF ),

				new TriangleData( Vertices.CG, Vertices.CD, Vertices.AC )
			),

			// CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.BD, Vertices.DH, Vertices.GH ),
				new TriangleData( Vertices.BD, Vertices.GH, Vertices.EF ),
				new TriangleData( Vertices.BD, Vertices.EF, Vertices.AE ),
				new TriangleData( Vertices.BD, Vertices.AE, Vertices.AC )
			),

			// CubeConfiguration.A | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.BD, Vertices.DH, Vertices.GH ),
				new TriangleData( Vertices.BD, Vertices.GH, Vertices.EF ),
				new TriangleData( Vertices.BD, Vertices.EF, Vertices.AB )
			),

			// CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.BF, Vertices.DH, Vertices.GH ),
				new TriangleData( Vertices.BF, Vertices.GH, Vertices.EF ),

				new TriangleData( Vertices.AE, Vertices.AC, Vertices.AB )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.BF, Vertices.DH, Vertices.GH ),
				new TriangleData( Vertices.BF, Vertices.GH, Vertices.EF )
			),

			// CubeConfiguration.F | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.FH, Vertices.BF, Vertices.EF ),
				new TriangleData( Vertices.FH, Vertices.EF, Vertices.EG ),
				new TriangleData( Vertices.FH, Vertices.EG, Vertices.CG ),
				new TriangleData( Vertices.FH, Vertices.CG, Vertices.GH )
			),

			// CubeConfiguration.A | CubeConfiguration.F | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.FH, Vertices.BF, Vertices.AB ),
				new TriangleData( Vertices.FH, Vertices.AB, Vertices.AC ),
				new TriangleData( Vertices.FH, Vertices.AC, Vertices.CG ),
				new TriangleData( Vertices.FH, Vertices.CG, Vertices.GH ),

				new TriangleData( Vertices.EG, Vertices.AE, Vertices.EF )
			),

			// CubeConfiguration.B | CubeConfiguration.F | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.FH, Vertices.BD, Vertices.AB ),
				new TriangleData( Vertices.FH, Vertices.AB, Vertices.EF ),
				new TriangleData( Vertices.FH, Vertices.EF, Vertices.EG ),
				new TriangleData( Vertices.FH, Vertices.EG, Vertices.CG ),
				new TriangleData( Vertices.FH, Vertices.CG, Vertices.GH )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.F | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.FH, Vertices.BD, Vertices.AC ),
				new TriangleData( Vertices.FH, Vertices.AC, Vertices.CG ),
				new TriangleData( Vertices.FH, Vertices.CG, Vertices.GH ),

				new TriangleData( Vertices.EG, Vertices.AE, Vertices.EF )
			),

			// CubeConfiguration.C | CubeConfiguration.F | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.FH, Vertices.BF, Vertices.EF ),
				new TriangleData( Vertices.FH, Vertices.EF, Vertices.EG ),
				new TriangleData( Vertices.FH, Vertices.EG, Vertices.AC ),
				new TriangleData( Vertices.FH, Vertices.AC, Vertices.CD ),
				new TriangleData( Vertices.FH, Vertices.CD, Vertices.GH )
			),

			// CubeConfiguration.A | CubeConfiguration.C | CubeConfiguration.F | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.FH, Vertices.BF, Vertices.AB ),
				new TriangleData( Vertices.FH, Vertices.AB, Vertices.CD ),
				new TriangleData( Vertices.FH, Vertices.CD, Vertices.GH ),

				new TriangleData( Vertices.EG, Vertices.AE, Vertices.EF )
			),

			// CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.F | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.FH, Vertices.BD, Vertices.CD ),
				new TriangleData( Vertices.FH, Vertices.CD, Vertices.GH ),

				new TriangleData( Vertices.EG, Vertices.AC, Vertices.AB ),
				new TriangleData( Vertices.EG, Vertices.AB, Vertices.EF )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.F | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.FH, Vertices.BD, Vertices.CD ),
				new TriangleData( Vertices.FH, Vertices.CD, Vertices.GH ),

				new TriangleData( Vertices.EG, Vertices.AE, Vertices.EF )
			),

			// CubeConfiguration.D | CubeConfiguration.F | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.FH, Vertices.DH, Vertices.GH ),

				new TriangleData( Vertices.BD, Vertices.BF, Vertices.EF ),
				new TriangleData( Vertices.BD, Vertices.EF, Vertices.EG ),
				new TriangleData( Vertices.BD, Vertices.EG, Vertices.CG ),
				new TriangleData( Vertices.BD, Vertices.CG, Vertices.CD )
			),

			// CubeConfiguration.A | CubeConfiguration.D | CubeConfiguration.F | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.FH, Vertices.DH, Vertices.GH ),

				new TriangleData( Vertices.BD, Vertices.BF, Vertices.AB ),

				new TriangleData( Vertices.CG, Vertices.CD, Vertices.AC ),

				new TriangleData( Vertices.EG, Vertices.AE, Vertices.EF )
			),

			// CubeConfiguration.B | CubeConfiguration.D | CubeConfiguration.F | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.FH, Vertices.DH, Vertices.GH ),

				new TriangleData( Vertices.CG, Vertices.CD, Vertices.AB ),
				new TriangleData( Vertices.CG, Vertices.AB, Vertices.EF ),
				new TriangleData( Vertices.CG, Vertices.EF, Vertices.EG )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.D | CubeConfiguration.F | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.FH, Vertices.DH, Vertices.GH ),

				new TriangleData( Vertices.CG, Vertices.CD, Vertices.AC ),

				new TriangleData( Vertices.EG, Vertices.AE, Vertices.EF )
			),

			// CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.F | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.FH, Vertices.DH, Vertices.GH ),

				new TriangleData( Vertices.BD, Vertices.BF, Vertices.EF ),
				new TriangleData( Vertices.BD, Vertices.EF, Vertices.EG ),
				new TriangleData( Vertices.BD, Vertices.EG, Vertices.AC )
			),

			// CubeConfiguration.A | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.F | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.FH, Vertices.DH, Vertices.GH ),

				new TriangleData( Vertices.BD, Vertices.BF, Vertices.AB ),

				new TriangleData( Vertices.EG, Vertices.AE, Vertices.EF )
			),

			// CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.F | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.FH, Vertices.DH, Vertices.GH ),

				new TriangleData( Vertices.EG, Vertices.AC, Vertices.AB ),
				new TriangleData( Vertices.EG, Vertices.AB, Vertices.EF )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.F | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.FH, Vertices.DH, Vertices.GH ),

				new TriangleData( Vertices.EG, Vertices.AE, Vertices.EF )
			),

			// CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.FH, Vertices.BF, Vertices.AE ),
				new TriangleData( Vertices.FH, Vertices.AE, Vertices.CG ),
				new TriangleData( Vertices.FH, Vertices.CG, Vertices.GH )
			),

			// CubeConfiguration.A | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.FH, Vertices.BF, Vertices.AB ),
				new TriangleData( Vertices.FH, Vertices.AB, Vertices.AC ),
				new TriangleData( Vertices.FH, Vertices.AC, Vertices.CG ),
				new TriangleData( Vertices.FH, Vertices.CG, Vertices.GH )
			),

			// CubeConfiguration.B | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.FH, Vertices.BD, Vertices.AB ),
				new TriangleData( Vertices.FH, Vertices.AB, Vertices.AE ),
				new TriangleData( Vertices.FH, Vertices.AE, Vertices.CG ),
				new TriangleData( Vertices.FH, Vertices.CG, Vertices.GH )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.FH, Vertices.BD, Vertices.AC ),
				new TriangleData( Vertices.FH, Vertices.AC, Vertices.CG ),
				new TriangleData( Vertices.FH, Vertices.CG, Vertices.GH )
			),

			// CubeConfiguration.C | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.FH, Vertices.BF, Vertices.AE ),
				new TriangleData( Vertices.FH, Vertices.AE, Vertices.AC ),
				new TriangleData( Vertices.FH, Vertices.AC, Vertices.CD ),
				new TriangleData( Vertices.FH, Vertices.CD, Vertices.GH )
			),

			// CubeConfiguration.A | CubeConfiguration.C | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.FH, Vertices.BF, Vertices.AB ),
				new TriangleData( Vertices.FH, Vertices.AB, Vertices.CD ),
				new TriangleData( Vertices.FH, Vertices.CD, Vertices.GH )
			),

			// CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.FH, Vertices.BD, Vertices.CD ),
				new TriangleData( Vertices.FH, Vertices.CD, Vertices.GH ),

				new TriangleData( Vertices.AE, Vertices.AC, Vertices.AB )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.FH, Vertices.BD, Vertices.CD ),
				new TriangleData( Vertices.FH, Vertices.CD, Vertices.GH )
			),

			// CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.FH, Vertices.DH, Vertices.GH ),

				new TriangleData( Vertices.BD, Vertices.BF, Vertices.AE ),
				new TriangleData( Vertices.BD, Vertices.AE, Vertices.CG ),
				new TriangleData( Vertices.BD, Vertices.CG, Vertices.CD )
			),

			// CubeConfiguration.A | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.FH, Vertices.DH, Vertices.GH ),

				new TriangleData( Vertices.BD, Vertices.BF, Vertices.AB ),

				new TriangleData( Vertices.CG, Vertices.CD, Vertices.AC )
			),

			// CubeConfiguration.B | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.FH, Vertices.DH, Vertices.GH ),

				new TriangleData( Vertices.CG, Vertices.CD, Vertices.AB ),
				new TriangleData( Vertices.CG, Vertices.AB, Vertices.AE )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.FH, Vertices.DH, Vertices.GH ),

				new TriangleData( Vertices.CG, Vertices.CD, Vertices.AC )
			),

			// CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.FH, Vertices.DH, Vertices.GH ),

				new TriangleData( Vertices.BD, Vertices.BF, Vertices.AE ),
				new TriangleData( Vertices.BD, Vertices.AE, Vertices.AC )
			),

			// CubeConfiguration.A | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.FH, Vertices.DH, Vertices.GH ),

				new TriangleData( Vertices.BD, Vertices.BF, Vertices.AB )
			),

			// CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.FH, Vertices.DH, Vertices.GH ),

				new TriangleData( Vertices.AE, Vertices.AC, Vertices.AB )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.G:
			new(
				new TriangleData( Vertices.FH, Vertices.DH, Vertices.GH )
			),

			// CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.FH, Vertices.GH )
			),

			// CubeConfiguration.A | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.FH, Vertices.GH ),

				new TriangleData( Vertices.AC, Vertices.AE, Vertices.AB )
			),

			// CubeConfiguration.B | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BD, Vertices.AB ),
				new TriangleData( Vertices.DH, Vertices.AB, Vertices.BF ),
				new TriangleData( Vertices.DH, Vertices.BF, Vertices.FH ),
				new TriangleData( Vertices.DH, Vertices.FH, Vertices.GH )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BD, Vertices.AC ),
				new TriangleData( Vertices.DH, Vertices.AC, Vertices.AE ),
				new TriangleData( Vertices.DH, Vertices.AE, Vertices.BF ),
				new TriangleData( Vertices.DH, Vertices.BF, Vertices.FH ),
				new TriangleData( Vertices.DH, Vertices.FH, Vertices.GH )
			),

			// CubeConfiguration.C | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.FH, Vertices.GH ),
				new TriangleData( Vertices.DH, Vertices.GH, Vertices.CG ),
				new TriangleData( Vertices.DH, Vertices.CG, Vertices.AC ),
				new TriangleData( Vertices.DH, Vertices.AC, Vertices.CD )
			),

			// CubeConfiguration.A | CubeConfiguration.C | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.FH, Vertices.GH ),
				new TriangleData( Vertices.DH, Vertices.GH, Vertices.CG ),
				new TriangleData( Vertices.DH, Vertices.CG, Vertices.AE ),
				new TriangleData( Vertices.DH, Vertices.AE, Vertices.AB ),
				new TriangleData( Vertices.DH, Vertices.AB, Vertices.CD )
			),

			// CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BD, Vertices.CD ),

				new TriangleData( Vertices.BF, Vertices.FH, Vertices.GH ),
				new TriangleData( Vertices.BF, Vertices.GH, Vertices.CG ),
				new TriangleData( Vertices.BF, Vertices.CG, Vertices.AC ),
				new TriangleData( Vertices.BF, Vertices.AC, Vertices.AB )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BD, Vertices.CD ),

				new TriangleData( Vertices.BF, Vertices.FH, Vertices.GH ),
				new TriangleData( Vertices.BF, Vertices.GH, Vertices.CG ),
				new TriangleData( Vertices.BF, Vertices.CG, Vertices.AE )
			),

			// CubeConfiguration.D | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BD, Vertices.FH, Vertices.GH ),
				new TriangleData( Vertices.BD, Vertices.GH, Vertices.CD )
			),

			// CubeConfiguration.A | CubeConfiguration.D | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BD, Vertices.FH, Vertices.GH ),
				new TriangleData( Vertices.BD, Vertices.GH, Vertices.CD ),
				new TriangleData( Vertices.BD, Vertices.CD, Vertices.AC ),
				new TriangleData( Vertices.BD, Vertices.AC, Vertices.AE ),
				new TriangleData( Vertices.BD, Vertices.AE, Vertices.AB )
			),

			// CubeConfiguration.B | CubeConfiguration.D | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BF, Vertices.FH, Vertices.GH ),
				new TriangleData( Vertices.BF, Vertices.GH, Vertices.CD ),
				new TriangleData( Vertices.BF, Vertices.CD, Vertices.AB )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.D | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BF, Vertices.FH, Vertices.GH ),
				new TriangleData( Vertices.BF, Vertices.GH, Vertices.CD ),
				new TriangleData( Vertices.BF, Vertices.CD, Vertices.AC ),
				new TriangleData( Vertices.BF, Vertices.AC, Vertices.AE )
			),

			// CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BD, Vertices.FH, Vertices.GH ),
				new TriangleData( Vertices.BD, Vertices.GH, Vertices.CG ),
				new TriangleData( Vertices.BD, Vertices.CG, Vertices.AC )
			),

			// CubeConfiguration.A | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BD, Vertices.FH, Vertices.GH ),
				new TriangleData( Vertices.BD, Vertices.GH, Vertices.CG ),
				new TriangleData( Vertices.BD, Vertices.CG, Vertices.AE ),
				new TriangleData( Vertices.BD, Vertices.AE, Vertices.AB )
			),

			// CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BF, Vertices.FH, Vertices.GH ),
				new TriangleData( Vertices.BF, Vertices.GH, Vertices.CG ),
				new TriangleData( Vertices.BF, Vertices.CG, Vertices.AC ),
				new TriangleData( Vertices.BF, Vertices.AC, Vertices.AB )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BF, Vertices.FH, Vertices.GH ),
				new TriangleData( Vertices.BF, Vertices.GH, Vertices.CG ),
				new TriangleData( Vertices.BF, Vertices.CG, Vertices.AE )
			),

			// CubeConfiguration.E | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.FH, Vertices.EF ),
				new TriangleData( Vertices.DH, Vertices.EF, Vertices.AE ),
				new TriangleData( Vertices.DH, Vertices.AE, Vertices.EG ),
				new TriangleData( Vertices.DH, Vertices.EG, Vertices.GH )
			),

			// CubeConfiguration.A | CubeConfiguration.E | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.FH, Vertices.EF ),
				new TriangleData( Vertices.DH, Vertices.EF, Vertices.AB ),
				new TriangleData( Vertices.DH, Vertices.AB, Vertices.AC ),
				new TriangleData( Vertices.DH, Vertices.AC, Vertices.EG ),
				new TriangleData( Vertices.DH, Vertices.EG, Vertices.GH )
			),

			// CubeConfiguration.B | CubeConfiguration.E | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BD, Vertices.AB ),
				new TriangleData( Vertices.DH, Vertices.AB, Vertices.AE ),
				new TriangleData( Vertices.DH, Vertices.AE, Vertices.EG ),
				new TriangleData( Vertices.DH, Vertices.EG, Vertices.GH ),

				new TriangleData( Vertices.BF, Vertices.FH, Vertices.EF )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.E | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BD, Vertices.AC ),
				new TriangleData( Vertices.DH, Vertices.AC, Vertices.EG ),
				new TriangleData( Vertices.DH, Vertices.EG, Vertices.GH ),

				new TriangleData( Vertices.BF, Vertices.FH, Vertices.EF )
			),

			// CubeConfiguration.C | CubeConfiguration.E | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.FH, Vertices.EF ),
				new TriangleData( Vertices.DH, Vertices.EF, Vertices.AE ),
				new TriangleData( Vertices.DH, Vertices.AE, Vertices.AC ),
				new TriangleData( Vertices.DH, Vertices.AC, Vertices.CD ),

				new TriangleData( Vertices.GH, Vertices.CG, Vertices.EG )
			),

			// CubeConfiguration.A | CubeConfiguration.C | CubeConfiguration.E | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.FH, Vertices.EF ),
				new TriangleData( Vertices.DH, Vertices.EF, Vertices.AB ),
				new TriangleData( Vertices.DH, Vertices.AB, Vertices.CD ),

				new TriangleData( Vertices.GH, Vertices.CG, Vertices.EG )
			),

			// CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.E | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BD, Vertices.CD ),

				new TriangleData( Vertices.BF, Vertices.FH, Vertices.EF ),

				new TriangleData( Vertices.GH, Vertices.CG, Vertices.EG ),

				new TriangleData( Vertices.AE, Vertices.AC, Vertices.AB )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.E | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BD, Vertices.CD ),

				new TriangleData( Vertices.BF, Vertices.FH, Vertices.EF ),

				new TriangleData( Vertices.GH, Vertices.CG, Vertices.EG )
			),

			// CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BD, Vertices.FH, Vertices.EF ),
				new TriangleData( Vertices.BD, Vertices.EF, Vertices.AE ),
				new TriangleData( Vertices.BD, Vertices.AE, Vertices.EG ),
				new TriangleData( Vertices.BD, Vertices.EG, Vertices.GH ),
				new TriangleData( Vertices.BD, Vertices.GH, Vertices.CD )
			),

			// CubeConfiguration.A | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BD, Vertices.FH, Vertices.EF ),
				new TriangleData( Vertices.BD, Vertices.EF, Vertices.AB ),

				new TriangleData( Vertices.GH, Vertices.CD, Vertices.AC ),
				new TriangleData( Vertices.GH, Vertices.AC, Vertices.EG )
			),

			// CubeConfiguration.B | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BF, Vertices.FH, Vertices.EF ),

				new TriangleData( Vertices.GH, Vertices.CD, Vertices.AB ),
				new TriangleData( Vertices.GH, Vertices.AB, Vertices.AE ),
				new TriangleData( Vertices.GH, Vertices.AE, Vertices.EG )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BF, Vertices.FH, Vertices.EF ),

				new TriangleData( Vertices.GH, Vertices.CD, Vertices.AC ),
				new TriangleData( Vertices.GH, Vertices.AC, Vertices.EG )
			),

			// CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BD, Vertices.FH, Vertices.EF ),
				new TriangleData( Vertices.BD, Vertices.EF, Vertices.AE ),
				new TriangleData( Vertices.BD, Vertices.AE, Vertices.AC ),

				new TriangleData( Vertices.GH, Vertices.CG, Vertices.EG )
			),

			// CubeConfiguration.A | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BD, Vertices.FH, Vertices.EF ),
				new TriangleData( Vertices.BD, Vertices.EF, Vertices.AB ),

				new TriangleData( Vertices.GH, Vertices.CG, Vertices.EG )
			),

			// CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BF, Vertices.FH, Vertices.EF ),

				new TriangleData( Vertices.GH, Vertices.CG, Vertices.EG ),

				new TriangleData( Vertices.AE, Vertices.AC, Vertices.AB )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BF, Vertices.FH, Vertices.EF ),

				new TriangleData( Vertices.GH, Vertices.CG, Vertices.EG )
			),

			// CubeConfiguration.F | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BF, Vertices.EF ),
				new TriangleData( Vertices.DH, Vertices.EF, Vertices.GH )
			),

			// CubeConfiguration.A | CubeConfiguration.F | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BF, Vertices.AB ),
				new TriangleData( Vertices.DH, Vertices.AB, Vertices.AC ),
				new TriangleData( Vertices.DH, Vertices.AC, Vertices.AE ),
				new TriangleData( Vertices.DH, Vertices.AE, Vertices.EF ),
				new TriangleData( Vertices.DH, Vertices.EF, Vertices.GH )
			),

			// CubeConfiguration.B | CubeConfiguration.F | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BD, Vertices.AB ),
				new TriangleData( Vertices.DH, Vertices.AB, Vertices.EF ),
				new TriangleData( Vertices.DH, Vertices.EF, Vertices.GH )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.F | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BD, Vertices.AC ),
				new TriangleData( Vertices.DH, Vertices.AC, Vertices.AE ),
				new TriangleData( Vertices.DH, Vertices.AE, Vertices.EF ),
				new TriangleData( Vertices.DH, Vertices.EF, Vertices.GH )
			),

			// CubeConfiguration.C | CubeConfiguration.F | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BF, Vertices.EF ),
				new TriangleData( Vertices.DH, Vertices.EF, Vertices.GH ),
				new TriangleData( Vertices.DH, Vertices.GH, Vertices.CG ),
				new TriangleData( Vertices.DH, Vertices.CG, Vertices.AC ),
				new TriangleData( Vertices.DH, Vertices.AC, Vertices.CD )
			),

			// CubeConfiguration.A | CubeConfiguration.C | CubeConfiguration.F | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BF, Vertices.AB ),
				new TriangleData( Vertices.DH, Vertices.AB, Vertices.CD ),

				new TriangleData( Vertices.GH, Vertices.CG, Vertices.AE ),
				new TriangleData( Vertices.GH, Vertices.AE, Vertices.EF )
			),

			// CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.F | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BD, Vertices.CD ),

				new TriangleData( Vertices.GH, Vertices.CG, Vertices.AC ),
				new TriangleData( Vertices.GH, Vertices.AC, Vertices.AB ),
				new TriangleData( Vertices.GH, Vertices.AB, Vertices.EF )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.F | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BD, Vertices.CD ),

				new TriangleData( Vertices.GH, Vertices.CG, Vertices.AE ),
				new TriangleData( Vertices.GH, Vertices.AE, Vertices.EF )
			),

			// CubeConfiguration.D | CubeConfiguration.F | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BD, Vertices.BF, Vertices.EF ),
				new TriangleData( Vertices.BD, Vertices.EF, Vertices.GH ),
				new TriangleData( Vertices.BD, Vertices.GH, Vertices.CD )
			),

			// CubeConfiguration.A | CubeConfiguration.D | CubeConfiguration.F | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BD, Vertices.BF, Vertices.AB ),

				new TriangleData( Vertices.GH, Vertices.CD, Vertices.AC ),
				new TriangleData( Vertices.GH, Vertices.AC, Vertices.AE ),
				new TriangleData( Vertices.GH, Vertices.AE, Vertices.EF )
			),

			// CubeConfiguration.B | CubeConfiguration.D | CubeConfiguration.F | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.GH, Vertices.CD, Vertices.AB ),
				new TriangleData( Vertices.GH, Vertices.AB, Vertices.EF )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.D | CubeConfiguration.F | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.GH, Vertices.CD, Vertices.AC ),
				new TriangleData( Vertices.GH, Vertices.AC, Vertices.AE ),
				new TriangleData( Vertices.GH, Vertices.AE, Vertices.EF )
			),

			// CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.F | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BD, Vertices.BF, Vertices.EF ),
				new TriangleData( Vertices.BD, Vertices.EF, Vertices.GH ),
				new TriangleData( Vertices.BD, Vertices.GH, Vertices.CG ),
				new TriangleData( Vertices.BD, Vertices.CG, Vertices.AC )
			),

			// CubeConfiguration.A | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.F | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BD, Vertices.BF, Vertices.AB ),

				new TriangleData( Vertices.GH, Vertices.CG, Vertices.AE ),
				new TriangleData( Vertices.GH, Vertices.AE, Vertices.EF )
			),

			// CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.F | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.GH, Vertices.CG, Vertices.AC ),
				new TriangleData( Vertices.GH, Vertices.AC, Vertices.AB ),
				new TriangleData( Vertices.GH, Vertices.AB, Vertices.EF )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.F | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.GH, Vertices.CG, Vertices.AE ),
				new TriangleData( Vertices.GH, Vertices.AE, Vertices.EF )
			),

			// CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BF, Vertices.AE ),
				new TriangleData( Vertices.DH, Vertices.AE, Vertices.EG ),
				new TriangleData( Vertices.DH, Vertices.EG, Vertices.GH )
			),

			// CubeConfiguration.A | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BF, Vertices.AB ),
				new TriangleData( Vertices.DH, Vertices.AB, Vertices.AC ),
				new TriangleData( Vertices.DH, Vertices.AC, Vertices.EG ),
				new TriangleData( Vertices.DH, Vertices.EG, Vertices.GH )
			),

			// CubeConfiguration.B | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BD, Vertices.AB ),
				new TriangleData( Vertices.DH, Vertices.AB, Vertices.AE ),
				new TriangleData( Vertices.DH, Vertices.AE, Vertices.EG ),
				new TriangleData( Vertices.DH, Vertices.EG, Vertices.GH )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BD, Vertices.AC ),
				new TriangleData( Vertices.DH, Vertices.AC, Vertices.EG ),
				new TriangleData( Vertices.DH, Vertices.EG, Vertices.GH )
			),

			// CubeConfiguration.C | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BF, Vertices.AE ),
				new TriangleData( Vertices.DH, Vertices.AE, Vertices.AC ),
				new TriangleData( Vertices.DH, Vertices.AC, Vertices.CD ),

				new TriangleData( Vertices.GH, Vertices.CG, Vertices.EG )
			),

			// CubeConfiguration.A | CubeConfiguration.C | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BF, Vertices.AB ),
				new TriangleData( Vertices.DH, Vertices.AB, Vertices.CD ),

				new TriangleData( Vertices.GH, Vertices.CG, Vertices.EG )
			),

			// CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BD, Vertices.CD ),

				new TriangleData( Vertices.GH, Vertices.CG, Vertices.EG ),

				new TriangleData( Vertices.AE, Vertices.AC, Vertices.AB )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BD, Vertices.CD ),

				new TriangleData( Vertices.GH, Vertices.CG, Vertices.EG )
			),

			// CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BD, Vertices.BF, Vertices.AE ),
				new TriangleData( Vertices.BD, Vertices.AE, Vertices.EG ),
				new TriangleData( Vertices.BD, Vertices.EG, Vertices.GH ),
				new TriangleData( Vertices.BD, Vertices.GH, Vertices.CD )
			),

			// CubeConfiguration.A | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BD, Vertices.BF, Vertices.AB ),

				new TriangleData( Vertices.GH, Vertices.CD, Vertices.AC ),
				new TriangleData( Vertices.GH, Vertices.AC, Vertices.EG )
			),

			// CubeConfiguration.B | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.GH, Vertices.CD, Vertices.AB ),
				new TriangleData( Vertices.GH, Vertices.AB, Vertices.AE ),
				new TriangleData( Vertices.GH, Vertices.AE, Vertices.EG )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.GH, Vertices.CD, Vertices.AC ),
				new TriangleData( Vertices.GH, Vertices.AC, Vertices.EG )
			),

			// CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BD, Vertices.BF, Vertices.AE ),
				new TriangleData( Vertices.BD, Vertices.AE, Vertices.AC ),

				new TriangleData( Vertices.GH, Vertices.CG, Vertices.EG )
			),

			// CubeConfiguration.A | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BD, Vertices.BF, Vertices.AB ),

				new TriangleData( Vertices.GH, Vertices.CG, Vertices.EG )
			),

			// CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.GH, Vertices.CG, Vertices.EG ),

				new TriangleData( Vertices.AE, Vertices.AC, Vertices.AB )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.GH, Vertices.CG, Vertices.EG )
			),

			// CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.FH, Vertices.EG ),
				new TriangleData( Vertices.DH, Vertices.EG, Vertices.CG )
			),

			// CubeConfiguration.A | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.FH, Vertices.EG ),
				new TriangleData( Vertices.DH, Vertices.EG, Vertices.AE ),
				new TriangleData( Vertices.DH, Vertices.AE, Vertices.AB ),
				new TriangleData( Vertices.DH, Vertices.AB, Vertices.AC ),
				new TriangleData( Vertices.DH, Vertices.AC, Vertices.CG )
			),

			// CubeConfiguration.B | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BD, Vertices.AB ),
				new TriangleData( Vertices.DH, Vertices.AB, Vertices.BF ),
				new TriangleData( Vertices.DH, Vertices.BF, Vertices.FH ),
				new TriangleData( Vertices.DH, Vertices.FH, Vertices.EG ),
				new TriangleData( Vertices.DH, Vertices.EG, Vertices.CG )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BD, Vertices.AC ),
				new TriangleData( Vertices.DH, Vertices.AC, Vertices.CG ),

				new TriangleData( Vertices.BF, Vertices.FH, Vertices.EG ),
				new TriangleData( Vertices.BF, Vertices.EG, Vertices.AE )
			),

			// CubeConfiguration.C | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.FH, Vertices.EG ),
				new TriangleData( Vertices.DH, Vertices.EG, Vertices.AC ),
				new TriangleData( Vertices.DH, Vertices.AC, Vertices.CD )
			),

			// CubeConfiguration.A | CubeConfiguration.C | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.FH, Vertices.EG ),
				new TriangleData( Vertices.DH, Vertices.EG, Vertices.AE ),
				new TriangleData( Vertices.DH, Vertices.AE, Vertices.AB ),
				new TriangleData( Vertices.DH, Vertices.AB, Vertices.CD )
			),

			// CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BD, Vertices.CD ),

				new TriangleData( Vertices.BF, Vertices.FH, Vertices.EG ),
				new TriangleData( Vertices.BF, Vertices.EG, Vertices.AC ),
				new TriangleData( Vertices.BF, Vertices.AC, Vertices.AB )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BD, Vertices.CD ),

				new TriangleData( Vertices.BF, Vertices.FH, Vertices.EG ),
				new TriangleData( Vertices.BF, Vertices.EG, Vertices.AE )
			),

			// CubeConfiguration.D | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BD, Vertices.FH, Vertices.EG ),
				new TriangleData( Vertices.BD, Vertices.EG, Vertices.CG ),
				new TriangleData( Vertices.BD, Vertices.CG, Vertices.CD )
			),

			// CubeConfiguration.A | CubeConfiguration.D | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BD, Vertices.FH, Vertices.EG ),
				new TriangleData( Vertices.BD, Vertices.EG, Vertices.AE ),
				new TriangleData( Vertices.BD, Vertices.AE, Vertices.AB ),

				new TriangleData( Vertices.CG, Vertices.CD, Vertices.AC )
			),

			// CubeConfiguration.B | CubeConfiguration.D | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BF, Vertices.FH, Vertices.EG ),
				new TriangleData( Vertices.BF, Vertices.EG, Vertices.CG ),
				new TriangleData( Vertices.BF, Vertices.CG, Vertices.CD ),
				new TriangleData( Vertices.BF, Vertices.CD, Vertices.AB )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.D | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BF, Vertices.FH, Vertices.EG ),
				new TriangleData( Vertices.BF, Vertices.EG, Vertices.AE ),

				new TriangleData( Vertices.CG, Vertices.CD, Vertices.AC )
			),

			// CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BD, Vertices.FH, Vertices.EG ),
				new TriangleData( Vertices.BD, Vertices.EG, Vertices.AC )
			),

			// CubeConfiguration.A | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BD, Vertices.FH, Vertices.EG ),
				new TriangleData( Vertices.BD, Vertices.EG, Vertices.AE ),
				new TriangleData( Vertices.BD, Vertices.AE, Vertices.AB )
			),

			// CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BF, Vertices.FH, Vertices.EG ),
				new TriangleData( Vertices.BF, Vertices.EG, Vertices.AC ),
				new TriangleData( Vertices.BF, Vertices.AC, Vertices.AB )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BF, Vertices.FH, Vertices.EG ),
				new TriangleData( Vertices.BF, Vertices.EG, Vertices.AE )
			),

			// CubeConfiguration.E | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.FH, Vertices.EF ),
				new TriangleData( Vertices.DH, Vertices.EF, Vertices.AE ),
				new TriangleData( Vertices.DH, Vertices.AE, Vertices.CG )
			),

			// CubeConfiguration.A | CubeConfiguration.E | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.FH, Vertices.EF ),
				new TriangleData( Vertices.DH, Vertices.EF, Vertices.AB ),
				new TriangleData( Vertices.DH, Vertices.AB, Vertices.AC ),
				new TriangleData( Vertices.DH, Vertices.AC, Vertices.CG )
			),

			// CubeConfiguration.B | CubeConfiguration.E | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BD, Vertices.AB ),
				new TriangleData( Vertices.DH, Vertices.AB, Vertices.AE ),
				new TriangleData( Vertices.DH, Vertices.AE, Vertices.CG ),

				new TriangleData( Vertices.BF, Vertices.FH, Vertices.EF )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.E | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BD, Vertices.AC ),
				new TriangleData( Vertices.DH, Vertices.AC, Vertices.CG ),

				new TriangleData( Vertices.BF, Vertices.FH, Vertices.EF )
			),

			// CubeConfiguration.C | CubeConfiguration.E | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.FH, Vertices.EF ),
				new TriangleData( Vertices.DH, Vertices.EF, Vertices.AE ),
				new TriangleData( Vertices.DH, Vertices.AE, Vertices.AC ),
				new TriangleData( Vertices.DH, Vertices.AC, Vertices.CD )
			),

			// CubeConfiguration.A | CubeConfiguration.C | CubeConfiguration.E | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.FH, Vertices.EF ),
				new TriangleData( Vertices.DH, Vertices.EF, Vertices.AB ),
				new TriangleData( Vertices.DH, Vertices.AB, Vertices.CD )
			),

			// CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.E | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BD, Vertices.CD ),

				new TriangleData( Vertices.BF, Vertices.FH, Vertices.EF ),

				new TriangleData( Vertices.AE, Vertices.AC, Vertices.AB )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.E | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BD, Vertices.CD ),

				new TriangleData( Vertices.BF, Vertices.FH, Vertices.EF )
			),

			// CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BD, Vertices.FH, Vertices.EF ),
				new TriangleData( Vertices.BD, Vertices.EF, Vertices.AE ),
				new TriangleData( Vertices.BD, Vertices.AE, Vertices.CG ),
				new TriangleData( Vertices.BD, Vertices.CG, Vertices.CD )
			),

			// CubeConfiguration.A | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BD, Vertices.FH, Vertices.EF ),
				new TriangleData( Vertices.BD, Vertices.EF, Vertices.AB ),

				new TriangleData( Vertices.CG, Vertices.CD, Vertices.AC )
			),

			// CubeConfiguration.B | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BF, Vertices.FH, Vertices.EF ),

				new TriangleData( Vertices.CG, Vertices.CD, Vertices.AB ),
				new TriangleData( Vertices.CG, Vertices.AB, Vertices.AE )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BF, Vertices.FH, Vertices.EF ),

				new TriangleData( Vertices.CG, Vertices.CD, Vertices.AC )
			),

			// CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BD, Vertices.FH, Vertices.EF ),
				new TriangleData( Vertices.BD, Vertices.EF, Vertices.AE ),
				new TriangleData( Vertices.BD, Vertices.AE, Vertices.AC )
			),

			// CubeConfiguration.A | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BD, Vertices.FH, Vertices.EF ),
				new TriangleData( Vertices.BD, Vertices.EF, Vertices.AB )
			),

			// CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BF, Vertices.FH, Vertices.EF ),

				new TriangleData( Vertices.AE, Vertices.AC, Vertices.AB )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BF, Vertices.FH, Vertices.EF )
			),

			// CubeConfiguration.F | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BF, Vertices.EF ),
				new TriangleData( Vertices.DH, Vertices.EF, Vertices.EG ),
				new TriangleData( Vertices.DH, Vertices.EG, Vertices.CG )
			),

			// CubeConfiguration.A | CubeConfiguration.F | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BF, Vertices.AB ),
				new TriangleData( Vertices.DH, Vertices.AB, Vertices.AC ),
				new TriangleData( Vertices.DH, Vertices.AC, Vertices.CG ),

				new TriangleData( Vertices.EG, Vertices.AE, Vertices.EF )
			),

			// CubeConfiguration.B | CubeConfiguration.F | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BD, Vertices.AB ),
				new TriangleData( Vertices.DH, Vertices.AB, Vertices.EF ),
				new TriangleData( Vertices.DH, Vertices.EF, Vertices.EG ),
				new TriangleData( Vertices.DH, Vertices.EG, Vertices.CG )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.F | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BD, Vertices.AC ),
				new TriangleData( Vertices.DH, Vertices.AC, Vertices.CG ),

				new TriangleData( Vertices.EG, Vertices.AE, Vertices.EF )
			),

			// CubeConfiguration.C | CubeConfiguration.F | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BF, Vertices.EF ),
				new TriangleData( Vertices.DH, Vertices.EF, Vertices.EG ),
				new TriangleData( Vertices.DH, Vertices.EG, Vertices.AC ),
				new TriangleData( Vertices.DH, Vertices.AC, Vertices.CD )
			),

			// CubeConfiguration.A | CubeConfiguration.C | CubeConfiguration.F | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BF, Vertices.AB ),
				new TriangleData( Vertices.DH, Vertices.AB, Vertices.CD ),

				new TriangleData( Vertices.EG, Vertices.AE, Vertices.EF )
			),

			// CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.F | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BD, Vertices.CD ),

				new TriangleData( Vertices.EG, Vertices.AC, Vertices.AB ),
				new TriangleData( Vertices.EG, Vertices.AB, Vertices.EF )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.F | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BD, Vertices.CD ),

				new TriangleData( Vertices.EG, Vertices.AE, Vertices.EF )
			),

			// CubeConfiguration.D | CubeConfiguration.F | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BD, Vertices.BF, Vertices.EF ),
				new TriangleData( Vertices.BD, Vertices.EF, Vertices.EG ),
				new TriangleData( Vertices.BD, Vertices.EG, Vertices.CG ),
				new TriangleData( Vertices.BD, Vertices.CG, Vertices.CD )
			),

			// CubeConfiguration.A | CubeConfiguration.D | CubeConfiguration.F | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BD, Vertices.BF, Vertices.AB ),

				new TriangleData( Vertices.CG, Vertices.CD, Vertices.AC ),

				new TriangleData( Vertices.EG, Vertices.AE, Vertices.EF )
			),

			// CubeConfiguration.B | CubeConfiguration.D | CubeConfiguration.F | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.CG, Vertices.CD, Vertices.AB ),
				new TriangleData( Vertices.CG, Vertices.AB, Vertices.EF ),
				new TriangleData( Vertices.CG, Vertices.EF, Vertices.EG )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.D | CubeConfiguration.F | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.CG, Vertices.CD, Vertices.AC ),

				new TriangleData( Vertices.EG, Vertices.AE, Vertices.EF )
			),

			// CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.F | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BD, Vertices.BF, Vertices.EF ),
				new TriangleData( Vertices.BD, Vertices.EF, Vertices.EG ),
				new TriangleData( Vertices.BD, Vertices.EG, Vertices.AC )
			),

			// CubeConfiguration.A | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.F | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BD, Vertices.BF, Vertices.AB ),

				new TriangleData( Vertices.EG, Vertices.AE, Vertices.EF )
			),

			// CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.F | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.EG, Vertices.AC, Vertices.AB ),
				new TriangleData( Vertices.EG, Vertices.AB, Vertices.EF )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.F | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.EG, Vertices.AE, Vertices.EF )
			),

			// CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BF, Vertices.AE ),
				new TriangleData( Vertices.DH, Vertices.AE, Vertices.CG )
			),

			// CubeConfiguration.A | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BF, Vertices.AB ),
				new TriangleData( Vertices.DH, Vertices.AB, Vertices.AC ),
				new TriangleData( Vertices.DH, Vertices.AC, Vertices.CG )
			),

			// CubeConfiguration.B | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BD, Vertices.AB ),
				new TriangleData( Vertices.DH, Vertices.AB, Vertices.AE ),
				new TriangleData( Vertices.DH, Vertices.AE, Vertices.CG )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BD, Vertices.AC ),
				new TriangleData( Vertices.DH, Vertices.AC, Vertices.CG )
			),

			// CubeConfiguration.C | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BF, Vertices.AE ),
				new TriangleData( Vertices.DH, Vertices.AE, Vertices.AC ),
				new TriangleData( Vertices.DH, Vertices.AC, Vertices.CD )
			),

			// CubeConfiguration.A | CubeConfiguration.C | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BF, Vertices.AB ),
				new TriangleData( Vertices.DH, Vertices.AB, Vertices.CD )
			),

			// CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BD, Vertices.CD ),

				new TriangleData( Vertices.AE, Vertices.AC, Vertices.AB )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.DH, Vertices.BD, Vertices.CD )
			),

			// CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BD, Vertices.BF, Vertices.AE ),
				new TriangleData( Vertices.BD, Vertices.AE, Vertices.CG ),
				new TriangleData( Vertices.BD, Vertices.CG, Vertices.CD )
			),

			// CubeConfiguration.A | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BD, Vertices.BF, Vertices.AB ),

				new TriangleData( Vertices.CG, Vertices.CD, Vertices.AC )
			),

			// CubeConfiguration.B | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.CG, Vertices.CD, Vertices.AB ),
				new TriangleData( Vertices.CG, Vertices.AB, Vertices.AE )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.CG, Vertices.CD, Vertices.AC )
			),

			// CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BD, Vertices.BF, Vertices.AE ),
				new TriangleData( Vertices.BD, Vertices.AE, Vertices.AC )
			),

			// CubeConfiguration.A | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.BD, Vertices.BF, Vertices.AB )
			),

			// CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.G | CubeConfiguration.H:
			new(
				new TriangleData( Vertices.AE, Vertices.AC, Vertices.AB )
			),

			// CubeConfiguration.A | CubeConfiguration.B | CubeConfiguration.C | CubeConfiguration.D | CubeConfiguration.E | CubeConfiguration.F | CubeConfiguration.G | CubeConfiguration.H:
			new(),
		];

		_marchingCubesLookup.SetData( entries );

		return _marchingCubesLookup;
	}
}
