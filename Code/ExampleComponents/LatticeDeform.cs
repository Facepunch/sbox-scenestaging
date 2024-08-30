using Editor;

namespace Sandbox.ExampleComponents
{
	/// <summary>
	/// Deforms parent
	/// Proof of concept, will be made into a proper feature with compute shaders
	/// Make just a sibling component rather than child, right now I prefer using the GameObject gizmo
	/// </summary>
	public class LatticeDeform : Component, Component.ExecuteInEditor
	{
		// This will be hidden from user once we have gizmos per segment
		[Property, MakeDirty ] public Vector3Int Segments 
		{ 
			get => _segments;
			set
			{
				// Clamp it to minimum 2
				_segments = Vector3Int.Max( value, new Vector3Int(2,2,2) );

				// Clear previous points if we change it
				if( _segments.x * _segments.y * _segments.z != Points.Count )
					Points = Enumerable.Repeat( Vector4.Zero, _segments.x * _segments.y * _segments.z ).ToList();
					
				PointsBuffer = new ComputeBuffer<Vector4>( Points.Count );
			}
		}
		internal Vector3Int _segments;

		// Needs to be a float4-aligned for ComputeBuffer
		// Dirty doesn't seem to be called when list elements are changed
		[Property, MakeDirty] public List<Vector4> Points { get; set; }

		ComputeBuffer<Vector4> PointsBuffer;

		public LatticeDeform()
		{
			Points = new List<Vector4>(2*2*2);
			Segments = new Vector3Int(2,2,2);
		}

		protected override void OnEnabled()
		{
			if( Transform == default )
			{
				// Get bounds from parent
				var parent = Components.GetInParent<ModelRenderer>();

				// Make it use the same bounds as the parent
				Transform.Scale = parent.Bounds.Size;
				Transform.Position = parent.Bounds.Mins;
			}

			base.OnEnabled();
		}

		protected override void OnDisabled()
		{
			base.OnDisabled();

			var parent = Components.GetInParent<ModelRenderer>();

			parent.SceneObject.Attributes.Set( "LocalToLattice", Matrix.Identity );
			parent.SceneObject.Attributes.Set( "Segments", Vector3Int.Zero );
		}

		protected override void OnDirty()
		{
			base.OnDirty();
		}

		public int Convert3DTo1D(int x, int y, int z)
		{
			return x * (Segments.y * Segments.z) + y * Segments.z + z;
		}

		protected override void OnPreRender()
		{
			base.OnPreRender();
			
			if( PointsBuffer is null )
				return;

			// Needs update only when dirty
			PointsBuffer.SetData( Points.ToArray() );

			var parent = Components.GetInParent<ModelRenderer>();

			// Matrix.FromTransform is internal, no way to pass Transform directly
			var matrix = new Matrix();
			var transform = Transform.Local;
			
			matrix = Matrix.CreateScale(transform.Scale) * Matrix.CreateRotation(transform.Rotation) * Matrix.CreateTranslation(transform.Position);
			
			parent.SceneObject.Attributes.Set( "Scale", Transform.Scale );
			parent.SceneObject.Attributes.Set( "LocalToLattice", matrix.Inverted );
			parent.SceneObject.Attributes.Set( "Lattice", PointsBuffer );
			parent.SceneObject.Attributes.Set( "Segments", Segments );
		}

		protected override void DrawGizmos()
        {
            base.DrawGizmos();

			// Draw Grid
            for( var x = 0; x < Segments.x; x++ )
            {
                for( var y = 0; y < Segments.y; y++ )
                {
                    for( var z = 0; z < Segments.z; z++ )
                    {
                        // Draw a grid
						var self = Convert3DTo1D(x, y, z);
						
						var neighbors = new List<Vector3Int>();

						// Add neighbors, don't add them on edges
						if (x + 1 < Segments.x) neighbors.Add( new Vector3Int(x + 1, y, z));
						if (y + 1 < Segments.y) neighbors.Add( new Vector3Int(x, y + 1, z));
						if (z + 1 < Segments.z) neighbors.Add( new Vector3Int(x, y, z + 1));

						var o1 = new Vector3( 	x / (float)( Segments.x - 1 ), 
												y / (float)( Segments.y - 1 ), 
												z / (float)( Segments.z - 1 ) );

						Gizmo.Draw.Color = Color.White.WithAlpha(0.25f);
						Gizmo.Draw.LineThickness = 4;

						foreach (var n in neighbors)
						{
							var o2 = new Vector3( 	n.x / (float)( Segments.x - 1 ), 
													n.y / (float)( Segments.y - 1 ), 
													n.z / (float)( Segments.z - 1 ) );

							Gizmo.Draw.Line(Points[self] + o1, Points[ Convert3DTo1D( n.x, n.y, n.z) ] + o2 );
						}
                    }
                }
            }
		}
	}
}
