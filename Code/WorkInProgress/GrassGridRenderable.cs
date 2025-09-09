using System;
using Sandbox.Rendering;

namespace Sandbox
{
    public sealed class GrassGridRenderable : SceneCustomObject
    {
        private GrassGrid Grid;
        private TerrainGrass Owner;

        private int MaxBlades = 20000;
        private GpuBuffer<Blade> BladeBuffer = new(20000);
        private GpuBuffer<int> BladeCountBuffer = new(1);
        private GpuBuffer<GpuBuffer.IndirectDrawIndexedArguments> IndirectDrawCommandBuffer = new(1, GpuBuffer.UsageFlags.IndirectDrawArguments | GpuBuffer.UsageFlags.Structured );
        private Model Model => Owner.GrassBlade.Model;
        
        ComputeShader grassGen = new("GrassGen_cs");
        ComputeShader indirectDrawGen = new("GrassIndirectArgs_cs");
        
        public GrassGridRenderable( GrassGrid grid, TerrainGrass owner )
            : base( owner.Scene.SceneWorld )
        {
            Grid = grid;
            Owner = owner;
            Position = grid.Center;
            Bounds = BBox.FromPositionAndSize( Grid.Center, new Vector3( Grid.Size * 2, Grid.Size * 2, 99999 ) );
        }

		public override void RenderSceneObject()
        {
            base.RenderSceneObject();


			// Generate grass data using compute shader
			{

				//Input
				Graphics.Attributes.Set( "PatchMin", Grid.Center - new Vector3( Grid.Size, Grid.Size, 0 ) );
				Graphics.Attributes.Set( "PatchMax", Grid.Center + new Vector3( Grid.Size, Grid.Size, 0 ) );
				Graphics.Attributes.Set( "NumBladesWanted", MaxBlades );
				Graphics.Attributes.Set( "Time", Time.Now );

				//Output
				Graphics.Attributes.Set( "BladeBuffer", BladeBuffer );
				Graphics.Attributes.Set( "BladeCounter", BladeCountBuffer );

				grassGen.Dispatch( MaxBlades, 1, 1 );

				Graphics.ResourceBarrierTransition( BladeBuffer, ResourceState.UnorderedAccess );
			}

			// Generate indirect draw command buffer from results
			{
				//Input
				Graphics.Attributes.Set( "IndexCountPerInstance", Model.GetIndexCount( 0 ) );
				Graphics.Attributes.Set( "BladeCounter", BladeCountBuffer );

				//Output
				Graphics.Attributes.Set( "IndirectDrawCommandBuffer", IndirectDrawCommandBuffer );

				indirectDrawGen.Dispatch( 1, 1, 1 );
			}

			// Draw the grass using the indirect draw command buffer
			{
				RenderAttributes test = new();
				test.Set( "BladeBuffer", BladeBuffer );
				test.Set( "BladeCounter", BladeCountBuffer );
				Graphics.DrawModelInstancedIndirect( Model, IndirectDrawCommandBuffer, attributes: test );
			}

		}

        public struct Blade
        {
            /// <summary>World-space position</summary>
            public Vector3 Position { get; set; }
            
            /// <summary>Normalized 2-D heading</summary>
            public Vector2 Facing { get; set; }
            
            /// <summary>Sampled from noise/texture</summary>
            public float Wind { get; set; }
            
            /// <summary>Random seed for vertex shader</summary>
            public uint Hash { get; set; }
            
            /// <summary>0-N switch in VS/PS</summary>
            public uint GrassType { get; set; }
            
            /// <summary>Average heading of clump</summary>
            public Vector2 ClumpFacing { get; set; }
            
            /// <summary>Tint picked in compute</summary>
            public Vector3 Color { get; set; }
            
            public float Height { get; set; }
            public float Width { get; set; }
            public float Tilt { get; set; }
            public float Bend { get; set; }
            public float SideCurve { get; set; }
        }
    }

}
