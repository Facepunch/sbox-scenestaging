using Editor;

namespace Sandbox.ExampleComponents
{
	/// <summary>
	/// Deforms parent
	/// </summary>
	public class LatticeDeform : Component, Component.ExecuteInEditor
	{
		[Property, MakeDirty, Range(2,10, 1)] public Vector3Int Segments { get; set; }

		public List<Vector3> Points { get; private set; }

		protected override void OnEnabled()
		{
			base.OnEnabled();
			//OnDirty();
		}

		protected override void OnUpdate()
		{
			// Get bounds from parent
			var parent = Components.GetInParent<ModelRenderer>();

			Transform.Scale = parent.Bounds.Size;
			Transform.Position = parent.Bounds.Mins;

			Points = new List<Vector3>( Segments.x * Segments.y * Segments.z );

			for ( var x = 0; x < Segments.x; x++ )
			{
				for ( var y = 0; y < Segments.y; y++ )
				{
					for ( var z = 0; z < Segments.z; z++ )
					{
						Points.Add( new Vector3( 
							x / (float)( Segments.x - 1 ), 
							y / (float)( Segments.y - 1 ), 
							z / (float)( Segments.z - 1 )  ) 
						);
					}
				}
			}
		}
		public int Convert3DTo1D(int x, int y, int z)
		{
			return x * (Segments.y * Segments.z) + y * Segments.z + z;
		}
		
		protected override void DrawGizmos()
        {
            base.DrawGizmos();
            for( var x = 0; x < Segments.x; x++ )
            {
                for( var y = 0; y < Segments.y; y++ )
                {
                    for( var z = 0; z < Segments.z; z++ )
                    {
                        // Draw a grid
						var self = Convert3DTo1D(x, y, z);
						
						var neighbors = new List<int>();

						if (x + 1 < Segments.x) neighbors.Add(Convert3DTo1D(x + 1, y, z));
						if (y + 1 < Segments.y) neighbors.Add(Convert3DTo1D(x, y + 1, z));
						if (z + 1 < Segments.z) neighbors.Add(Convert3DTo1D(x, y, z + 1));


						Gizmo.Draw.Color = Gizmo.Colors.Green.WithAlpha(0.25f);
						Gizmo.Draw.LineThickness = 4;
						foreach (var neighbor in neighbors)
						{
							Gizmo.Draw.Line(Points[self], Points[neighbor]);
						}
                    }
                }
            }
        }
	}
}
