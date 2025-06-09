using System.IO;

namespace Sandbox;

public partial class NanoVDB
{
    public partial class Grid
    {
        public string Name { get; private set; } = "";
        public uint[] Data { get; private set; }
        public GridHeader Header = new();

        public static Grid Read( BinaryReader reader, Metadata metadata )
        {
            var basePos = reader.BaseStream.Position;
            Grid grid = new()
            {
                Name = new string( reader.ReadChars( (int)metadata.NameSize ) ),
                Header = GridHeader.Read( reader )
            };

            // Go back to the start of the grid data
            reader.BaseStream.Position = basePos + (int)metadata.NameSize;

            grid.Data = new uint[grid.Header.GridSize / sizeof(uint)];
            for (int i = 0; i < grid.Data.Length; i++)
            {
                grid.Data[i] = reader.ReadUInt32();
            }
            
            
            return grid;
        }

        /// <summary>
        /// 
        /// </summary>
        public class GridHeader
        {
            public Magic Magic = 0;                             // 8 bytes,     0
            public ulong Checksum = 0;                          // 8 bytes,     8
            public uint Version = 0;                            // 4 bytes,     16
            public GridFlags Flags = 0;                         // 4 bytes,     20
            public uint GridIndex = 0;                          // 4 bytes,     24
            public uint GridCount = 0;                          // 4 bytes,     28
            public ulong GridSize = 0;                          // 8 bytes,     32
            public string GridName = "";                        // 256 bytes,   40 (64 elements)
            public byte[] Map = new byte[264];                  // 264 bytes,   296
            public BBox WorldBBox = new();                      // 48 bytes,    560 (6 elements)
            public Vector3 VoxelSize = new();                   // 24 bytes,    608 (3 elements)
            public GridClass Class = 0;                         // 4 bytes,     632
            public GridType Type = 0;                           // 4 bytes,     636
            public long BlindMetadataOffset = 0;                // 8 bytes,     640
            public uint BlindMetadataCount = 0;                 // 4 bytes,     648
            public uint Data0 = 0;                              // 4 bytes,     652 (unused)
            public ulong Data1 = 0;                             // 8 bytes,     656 (total value count)
            public Magic Data2 = 0;                             // 8 bytes,     664 (padding to 32 B alignment)

            public static GridHeader Read( BinaryReader reader )
            {
                GridHeader header = new()
                {
                    Magic = (Magic)reader.ReadUInt64(),
                    Checksum = reader.ReadUInt64(),
                    Version = reader.ReadUInt32(),
                    Flags = (GridFlags)reader.ReadUInt32(),
                    GridIndex = reader.ReadUInt32(),
                    GridCount = reader.ReadUInt32(),
                    GridSize = reader.ReadUInt64(),
                    GridName = new string( reader.ReadChars( 256 ) ),
                    Map = reader.ReadBytes( 264 ),
                    WorldBBox = new BBox(
                        new Vector3( (float)reader.ReadDouble(), (float)reader.ReadDouble(), (float)reader.ReadDouble() ),
                        new Vector3( (float)reader.ReadDouble(), (float)reader.ReadDouble(), (float)reader.ReadDouble() )
                    ),
                    VoxelSize = new Vector3( (float)reader.ReadDouble(), (float)reader.ReadDouble(), (float)reader.ReadDouble() ),
                    Class = (GridClass)reader.ReadUInt32(),
                    Type = (GridType)reader.ReadUInt32(),
                    BlindMetadataOffset = reader.ReadInt64(),
                    BlindMetadataCount = reader.ReadUInt32(),
                    Data0 = reader.ReadUInt32(),
                    Data1 = reader.ReadUInt64(),
                    Data2 = (Magic)reader.ReadUInt64() // NANOVDB_MAGIC_GRID, might change in the future
                };

                if ( header.Magic != Magic.Grid && header.Magic != Magic.Numb )
                    throw new Exception( "Invalid NanoVDB grid header" );
                
                if( header.GridSize % 32 != 0 )
                    throw new Exception( "Grid size must be divisible by 32" );

                if( header.Data2 != Magic.Grid )
                    throw new Exception( "Expected NANOVDB_MAGIC_GRID on Data2" );
                    
                return header;
            }
        }
    }
}

/// <summary>
/// When added to a string property, will become a VDB string selector
/// </summary>
[AttributeUsage( AttributeTargets.Property )]
public class NVDBPathAttribute : AssetPathAttribute
{
    public override string AssetTypeExtension => "nvdb";
}