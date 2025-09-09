using Sandbox;
using System;
using System.Collections.Generic;

namespace Sandbox
{
	[Icon("grass")]
	public sealed class TerrainGrass : Component, Component.ExecuteInEditor
	{
		// Reference to the terrain that this grass system should cover
		[Property, Title("Terrain")]
		public Terrain Terrain { get; set; }
		
		public BBox Bounds;

		// LOD Tuning Properties
		[Property, Range(1, 8)] public int MaxDepth { get; set; } = 6;

		[Property] public float DetailScale { get; set; } = 5.0f;


		[Property]
		public GlassBladeProperty GrassBlade { get; set; }

		// Quadtree root
		private GrassGrid Root;

		protected override void OnEnabled()
		{
			// Attempt auto-discovery if not explicitly assigned
			Terrain ??= GameObject.Components.Get<Terrain>(FindMode.EverythingInSelfAndParent);

			float terrainSize = Terrain.TerrainSize;
			Vector3 center = new Vector3(terrainSize / 2, terrainSize / 2, 0);
			Root = new GrassGrid(center, terrainSize / 2, 0, this);

			GrassBlade.Model = GrassBlade.GenerateModel();
		}

		protected override void OnDisabled()
		{
			base.OnDisabled();
			Root.Delete();
			Root = null;
			Terrain = null;
		}

		protected override void OnFixedUpdate()
		{
			if (Root == null) return;

			var camPos = Scene.Camera.WorldPosition;
			Root.Update(camPos);
		}

		protected override void DrawGizmos()
		{
			if (Root == null) return;
			
			Root.DrawGizmos();
		}
	}
}
