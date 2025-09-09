using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sandbox
{

	// This will be a game resource so you can put your own grass blade models in here too
	public class GlassBladeProperty : GameResource
	{
		// Procedural grass mesh
		[Property] public Model Model { get; set; }
		[Property, MakeDirty] int Segments { get; set; } = 6;
		[Property, MakeDirty] float Height { get; set; } = 16.0f;
		[Property, MakeDirty] float Width { get; set; } = 2.0f;

		public GlassBladeProperty()
		{
			// Default grass blade model generation
			Model = GenerateModel();
		}

		protected void OnPropertyDirty<T>( in WrappedPropertySet<T> p )
		{
			Model = GenerateModel();
		}

		struct BladeVertexLayout
		{
			public Vector3 Position;
			public float Height;


			public BladeVertexLayout( Vector3 position, float height )
			{
				Position = position;
				Height = height;
			}
			public static readonly VertexAttribute[] Layout =
			{
				new VertexAttribute( VertexAttributeType.Position, VertexAttributeFormat.Float32, 3 ),
				new VertexAttribute( VertexAttributeType.TexCoord, VertexAttributeFormat.Float32, 1 ),
			};
		}

		public Model GenerateModel()
		{
			int vertexCount = (Segments + 1) * 2;     // two verts per ring
			int indexCount = Segments * 6;           // two tris per ring

			var positions = new BladeVertexLayout[vertexCount];
			var indices = new int[indexCount];

			for ( int i = 0; i <= Segments; ++i )
			{
				float t = i / (float)Segments;   // 0‑1 along height
				float z = t * Height;
				float width = Width * (1.0f - 0.8f * t);

				// small bending for a *hint* of curve
				float bendX = MathF.Sin( t * MathF.PI * 0.5f ) * 0.15f * Height;
				float bendY = t * t * 0.2f * Height;

				int left = i * 2;
				int right = i * 2 + 1;

				positions[left] = new( new Vector3( -width * 0.5f + bendX, bendY, z ), i / (float)Segments );
				positions[right] = new( new Vector3( width * 0.5f + bendX, bendY, z ), i / (float)Segments );

				if ( i < Segments )
				{
					int baseIdx = i * 6;
					indices[baseIdx + 0] = left;
					indices[baseIdx + 1] = left + 2;
					indices[baseIdx + 2] = right;

					indices[baseIdx + 3] = right;
					indices[baseIdx + 4] = left + 2;
					indices[baseIdx + 5] = right + 2;
				}
			}

			var mesh = new Mesh();

			mesh.CreateVertexBuffer<BladeVertexLayout>( vertexCount, BladeVertexLayout.Layout, positions );
			mesh.CreateIndexBuffer( indexCount, indices );
			mesh.Material = Material.Create( "procGrassBladeMaterial", "grass" );

			return Model.Builder
				.WithName( "procGrassBlade" )
				.AddMesh( mesh )
				.Create();
		}
	}
}
