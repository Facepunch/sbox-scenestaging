using System.IO;

namespace Sandbox;

public partial class NanoVDB
{
    public partial class Grid
    {
        public string Name { get; private set; } = "";
        public List<byte> Data { get; private set; }
        public GridHeader Header = new();
        
        public static Grid Read( BinaryReader reader, Metadata metadata )
        {
            Grid grid = new()
            {
                Name = new string( reader.ReadChars( (int)metadata.NameSize ) ),
                Header = GridHeader.Read(reader)
            };

            if( grid.Header.GridSize > int.MaxValue )
                throw new Exception("Grid size over 4GB is not supported");

            grid.Data = reader.ReadBytes( (int)grid.Header.GridSize ).ToList<byte>();
            return grid;
        }
        
        /// <summary>
        /// 
        /// </summary>
        public class GridHeader
        {
            public Magic Magic = 0;					// 8 bytes,     0
            public ulong Checksum = 0;              // 8 bytes,     8
            public uint Version = 0;                // 4 bytes,     16
            public GridFlags Flags = 0;             // 4 bytes,     20
            public uint GridIndex = 0;              // 4 bytes,     24
            public uint GridCount = 0;              // 4 bytes,     28
            public ulong GridSize = 0;              // 8 bytes,     32
            public string GridName = "";            // 256 bytes,   40 (64 elements)
            public byte[] Map = new byte[264];		// 264 bytes,   296
            public BBox WorldBBox = new();          // 48 bytes,    560 (6 elements)
            public Vector3 VoxelSize = new();       // 24 bytes,    608 (3 elements)
            public GridClass Class = 0;             // 4 bytes,     632
            public GridType Type = 0;               // 4 bytes,     636
            public long BlindMetadataOffset = 0;    // 8 bytes,     640
            public uint BlindMetadataCount = 0;     // 4 bytes,     648
            public byte[] Pad = new byte[20];       // 20 bytes,    652 (5 elements)

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
                    GridName = new string( reader.ReadChars(256) ),
                    Map = reader.ReadBytes(264),
                    WorldBBox = new BBox( new Vector3( (float)reader.ReadDouble(), (float)reader.ReadDouble(), (float)reader.ReadDouble() ), new Vector3( (float)reader.ReadDouble(), (float)reader.ReadDouble(), (float)reader.ReadDouble() ) ),
                    VoxelSize = new Vector3( (float)reader.ReadDouble(), (float)reader.ReadDouble(), (float)reader.ReadDouble() ),
                    Class = (GridClass)reader.ReadUInt32(),
                    Type = (GridType)reader.ReadUInt32(),
                    BlindMetadataOffset = reader.ReadInt64(),
                    BlindMetadataCount = reader.ReadUInt32(),
					Pad = reader.ReadBytes(20)
				};

                if( header.Magic != Magic.Grid && header.Magic != Magic.Numb )
                    throw new Exception("Invalid NanoVDB grid header");
                
                return header;
            }
        }
    }
}
