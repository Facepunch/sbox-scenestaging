using Sandbox;
using System;

namespace Sandbox
{
    /// <summary>
    /// Quadtree node for dynamic grass LOD system
    /// </summary>
    public sealed class GrassGrid
    {
        private readonly TerrainGrass Owner;
        public readonly Vector3 Center;
        public readonly float Size; // Half-size (radius) of the quad
        private readonly int Depth;
        private GrassGrid[] Children; // null => leaf

        GrassGridRenderable SceneObject = null; // Renderable object for this grid

        public GrassGrid( Vector3 center, float size, int depth, TerrainGrass owner )
        {
            Center = center;
            Size = size;
            Depth = depth;
            Owner = owner;

			Update( owner.Scene.Camera.WorldPosition );

			// Create sceneobject if we are leafiest
			if ( Children is null )
				SceneObject = new( this, owner );
        }


        // Simple LOD selection based on camera distance
        public void Update(Vector3 cameraPos)
        {
            // Calculate distance to camera (ignoring height)
            float distance = (cameraPos - Center).WithZ(0).Length;
            
            // Simple distance check - split if camera is close enough and we haven't reached max depth
            bool shouldSplit = distance < Size * Owner.DetailScale && Depth < Owner.MaxDepth;

			// Split or collapse based on distance
			if ( shouldSplit && Children is null )
				Split();
			else if ( !shouldSplit && Children is not null )
                Collapse();
            
            // Update children if they exist
            if (Children != null)
            {
				foreach (var child in Children)
					child.Update(cameraPos);
            }
        }

        // Gizmo drawing
        public void DrawGizmos()
        {
            // Draw if leafiest
            if ( Children == null)
            {
                // Depth-based color from red (root) to green (deepest)
                Gizmo.Draw.Color = Color.Lerp( Color.Red, Color.Green, (float)Depth / (float)Owner.MaxDepth ).WithAlpha( 0.25f );

                float half = Size * 2;
                float z = Owner.Scene.Camera?.WorldPosition.z - 128.0f ?? 0;
				var center = Center.WithZ( z );
				var pad = 3.0f;

				var bbox = BBox.FromPositionAndSize( new Vector3( center ), new Vector3( half - pad, half - pad, 0 ) );

				Gizmo.Draw.LineBBox( bbox );
			}

            // Recurse
            if ( Children != null )
            {
                foreach ( var child in Children )
                    child.DrawGizmos();
            }
        }

		private void Split()
		{
			float halfSize = Size / 2;
			Children = new GrassGrid[4];

			// Create 4 child quadrants properly positioned
			Children[0] = new GrassGrid( Center + new Vector3( -halfSize, -halfSize, 0 ), halfSize, Depth + 1, Owner ); // Bottom-left
			Children[1] = new GrassGrid( Center + new Vector3( halfSize, -halfSize, 0 ), halfSize, Depth + 1, Owner );  // Bottom-right
			Children[2] = new GrassGrid( Center + new Vector3( -halfSize, halfSize, 0 ), halfSize, Depth + 1, Owner );  // Top-left
			Children[3] = new GrassGrid( Center + new Vector3( halfSize, halfSize, 0 ), halfSize, Depth + 1, Owner );   // Top-right

			// Delete our sceneobject
			SceneObject?.Delete();
			SceneObject = null;
		}

        private void Collapse()
        {
            if ( Children == null ) return;

			foreach ( var child in Children )
			{
				child.Delete();
			}

            Children = null;

			SceneObject = new( this, Owner );

			Log.Info( $"[{nameof( GrassGrid )}] Collapsing grid at depth {Depth}." );
        }

		public void Delete()
		{

			SceneObject?.Delete();
			SceneObject = null;

			if ( Children == null ) return;

			foreach ( var child in Children )
			{
				child.Delete();
			};

			Children = null;
		}
    }
}
