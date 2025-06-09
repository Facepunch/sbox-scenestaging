using System.IO;
using NativeEngine;

namespace Sandbox;

/// <summary>
/// File structure of .nvdb goes as follows:
///     * Header
///     * MetaData
///     * List of Grids
/// </summary>
public partial class NanoVDB

{
    public Header FileHeader { get; set; }
    public Metadata FileMetadata { get; set; }
    public List<Grid> Grids { get; set; } = new();


    [Category( "VDB Settings" )] public bool IsSequence { get; set; } = false;
	[Category( "VDB Info" )] public BBox BoundingBox { get => FileMetadata.WorldBBox; private set { } }

    public static NanoVDB Load( string filePath )
    {
        
		using var fileStream = FileSystem.Mounted.OpenRead( filePath );

		if ( !filePath.EndsWith( ".nvdb" ) )
            throw new Exception("Only NanoVDB files are supported for now");
            
        using var reader = new BinaryReader( fileStream );
        
        var nanoVDB = new NanoVDB();
        nanoVDB.Read( reader );

        return nanoVDB;
    }

    private void Read( BinaryReader reader )
    {
        FileHeader = new()
        {
            Magic = (Magic)reader.ReadUInt64(),
            Version = reader.ReadUInt32(),
            GridCount = reader.ReadInt16(),
            Compression = (Compression)reader.ReadInt16()
        };

        if( !FileHeader.IsValid() )
            throw new Exception("Invalid NanoVDB file");

        if( FileHeader.Compression != Compression.None )
            throw new Exception("Only uncompressed NanoVDB files are supported");

        FileMetadata = Metadata.Read(reader);

		if( FileMetadata.Codec != Compression.None )
			throw new Exception( "Only uncompressed NanoVDB files are supported" );

		for (int i = 0; i < FileHeader.GridCount; i++)
        {
            Grids.Add(Grid.Read(reader, FileMetadata ) );
        }
    }

    /// <summary>
    /// Represents the header of a NanoVDB file.
    /// </summary>
    public class Header
    {
        public Magic Magic { get; set; } // 8 bytes
        public uint Version { get; set; } // 4 bytes version numbers
        public short GridCount { get; set; } // 2 bytes
        public Compression Compression { get; set; } // 2 bytes
        public bool IsValid()
        {
            return Magic == Magic.Numb || Magic == Magic.File;
        }
    }

    /// <summary>
    /// Data encoded for each of the grids associated with a segment.
    /// </summary>
    public class Metadata
    {
        // Grid size in memory (uint64_t)
        public ulong GridSize { get; set; } // 8 bytes
        // Grid size on disk (uint64_t)
        public ulong FileSize { get; set; } // 8 bytes
        // Grid name hash key (uint64_t)
        public ulong NameKey { get; set; } // 8 bytes
        // Number of active voxels (uint64_t)
        public ulong VoxelCount { get; set; } // 8 bytes
        // Grid type (uint32_t)
        public GridType GridType { get; set; } // 4 bytes
        // Grid class (uint32_t)
        public GridClass GridClass { get; set; } // 4 bytes
        // AABB in world space (2*3*double)
        public BBox WorldBBox { get; set; } // 48 bytes
        // AABB in index space (2*3*int)
        public BBox IndexBBox { get; set; } // 24 bytes
        // Size of a voxel in world units (3*double)
        public Vector3 VoxelSize { get; set; } // 24 bytes
        // Byte size of the grid name (uint32_t)
        public uint NameSize { get; set; } // 4 bytes
        // Number of nodes per level (4*uint32_t)
        public uint[] NodeCount { get; set; } = new uint[4]; // 16 bytes
        // Number of active tiles per level (3*uint32_t)
        public uint[] TileCount { get; set; } = new uint[3]; // 12 bytes
        // Codec for file compression (uint16_t)
        public Compression Codec { get; set; } // 2 bytes
        // Padding due to 8B alignment (uint16_t)
        public ushort Padding { get; set; } // 2 bytes
        // Version number (uint32_t)
        public uint Version { get; set; } // 4 bytes

        public static Metadata Read( BinaryReader reader )
        {
            Metadata metadata = new()
            {
                GridSize = reader.ReadUInt64(),
                FileSize = reader.ReadUInt64(),
                NameKey = reader.ReadUInt64(),
                VoxelCount = reader.ReadUInt64(),
                GridType = (GridType)reader.ReadUInt32(),
                GridClass = (GridClass)reader.ReadUInt32(),
                WorldBBox = new BBox( new Vector3( (float)reader.ReadDouble(), (float)reader.ReadDouble(), (float)reader.ReadDouble() ), new Vector3( (float)reader.ReadDouble(), (float)reader.ReadDouble(), (float)reader.ReadDouble() ) ),
                IndexBBox = new BBox( new Vector3( (float)reader.ReadInt32(), (float)reader.ReadInt32(), (float)reader.ReadInt32() ), new Vector3( (float)reader.ReadInt32(), (float)reader.ReadInt32(), (float)reader.ReadInt32() ) ),
				VoxelSize = new Vector3( (float)reader.ReadDouble(), (float)reader.ReadDouble(), (float)reader.ReadDouble() ),
                NameSize = reader.ReadUInt32(),
                NodeCount = new uint[] { reader.ReadUInt32(), reader.ReadUInt32(), reader.ReadUInt32(), reader.ReadUInt32() },
                TileCount = new uint[] { reader.ReadUInt32(), reader.ReadUInt32(), reader.ReadUInt32() },
                Codec = (Compression)reader.ReadUInt16(),
                Padding = reader.ReadUInt16(),
                Version = reader.ReadUInt32()
            };
            return metadata;
        }
    }

    //---------------------------------------------------------------------------------------------------

    public enum Magic : ulong
    {
        Numb = 0x304244566f6e614eUL, // "NanoVDB0" in hex - little endian (uint64_t)
        Grid = 0x314244566f6e614eUL, // "NanoVDB1" in hex - little endian (uint64_t)
        File = 0x324244566f6e614eUL, // "NanoVDB2" in hex - little endian (uint64_t)
        Node = 0x334244566f6e614eUL, // "NanoVDB3" in hex - little endian (uint64_t)
        Frag = 0x344244566f6e614eUL  // "NanoVDB4" in hex - little endian (uint64_t)
    }

    public enum Compression : short
    {
        None = 0,
        Zip = 1,
        Blosc = 2
    }
    public enum GridType : uint 
    { 
        Unknown, //  unknown value type - should rarely be used
        Float, //  single precision floating point value
        Double, //  double precision floating point value
        Int16, //  half precision signed integer value
        Int32, //  single precision signed integer value
        Int64, //  double precision signed integer value
        Vec3f, //  single precision floating 3D vector
        Vec3d, //  double precision floating 3D vector
        Mask, //  no value, just the active state
        Half, //  half precision floating point value (placeholder for IEEE 754 Half)
        UInt32, // single precision unsigned integer value
        Boolean, // boolean value, encoded in bit array
        RGBA8, // RGBA packed into 32bit word in reverse-order, i.e. R is lowest byte.
        Fp4, // 4bit quantization of floating point value
        Fp8, // 8bit quantization of floating point value
        Fp16, // 16bit quantization of floating point value
        FpN, // variable bit quantization of floating point value
        Vec4f, // single precision floating 4D vector
        Vec4d, // double precision floating 4D vector
        Index, // index into an external array of active and inactive values
        OnIndex, // index into an external array of active values
        IndexMask, // like Index but with a mutable mask
        OnIndexMask, // like OnIndex but with a mutable mask
        PointIndex, // voxels encode indices to co-located points
        Vec3u8, // 8bit quantization of floating point 3D vector (only as blind data)
        Vec3u16, // 16bit quantization of floating point 3D vector (only as blind data)
        UInt8, // 8 bit unsigned integer values (eg 0 -> 255 gray scale)
    };

    public enum GridClass 
    {
        Unknown,
        LevelSet, // narrow band level set, e.g. SDF
        FogVolume, // fog volume, e.g. density
        Staggered, // staggered MAC grid, e.g. velocity
        PointIndex, // point index grid
        PointData, // point data grid
        Topology, // grid with active states only (no values)
        VoxelVolume, // volume of geometric cubes, e.g. colors cubes in Minecraft
        IndexGrid, // grid whose values are offsets, e.g. into an external array
        TensorGrid, // Index grid for indexing learnable tensor features
    };

    /// <summary>
    /// Grid flags which indicate what extra information is present in the grid buffer.
    /// </summary>
    [Flags]
    public enum GridFlags {
        HasLongGridName = 1 << 0, // grid name is longer than 256 characters
        HasBBox = 1 << 1, // nodes contain bounding-boxes of active values
        HasMinMax = 1 << 2, // nodes contain min/max of active values
        HasAverage = 1 << 3, // nodes contain averages of active values
        HasStdDeviation = 1 << 4, // nodes contain standard deviations of active values
        IsBreadthFirst = 1 << 5, // nodes are typically arranged breadth-first in memory
        End = 1 << 6, // use End - 1 as a mask for the 5 lower bit flags
    };
}