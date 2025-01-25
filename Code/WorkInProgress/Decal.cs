using Sandbox;
using Sandbox.Diagnostics;
using System;
using System.Numerics;

using Sandbox.Internal;

[Title( "Decal" )]
[Category( "Rendering" )]
[Icon( "lens_blur" )]
[EditorHandle( "materials/gizmo/decal.png" )]
public sealed class NewDecal : Component, Component.ExecuteInEditor
{
	[Property, Group( "Textures" ), MakeDirty] public Texture Color { get; set; }
	[Property, Group( "Textures" ), MakeDirty] public Texture Normal { get; set; }
	[Property, Group( "Textures" ), MakeDirty] public Texture RMA { get; set; }

	[Property, Group( "Sorting" ), MakeDirty] public uint SortOrder { get; set; }
	[Property, Group( "Cull Mask" ), MakeDirty] public uint ExclusionLayer { get; set; }

	[Property, Group( "Parameters" ), MakeDirty] public Color32 ColorTint { get; set; } = Color32.White;
	[Property, Group( "Parameters" ), Range( 0, 1 ), MakeDirty] public float AttenuationAngle { get; set; } = 1.0f;

	[Property, FeatureEnabled( "Emissive" )] public bool Emissive;
	[Property, FeatureEnabled( "Parallax" )] public bool Parallax;
	[Property, FeatureEnabled( "SDF" )] public bool SDF;
	[Property, FeatureEnabled( "UV Tiling" )] public bool UVTiling;

	DecalSceneObject _sceneObject;

	protected override void OnEnabled()
	{
		Assert.IsNull( _sceneObject );
		_sceneObject = new DecalSceneObject( Scene.SceneWorld );

		UpdateSceneObject();

		OnTransformChanged();
		Transform.OnTransformChanged += OnTransformChanged;
	}

	protected override void OnDirty()
	{
		UpdateSceneObject();
	}

	void UpdateSceneObject()
	{
		if ( !_sceneObject.IsValid() )
			return;

		_sceneObject.ColorTexture = Color;
		_sceneObject.NormalTexture = Normal;
		_sceneObject.RoughnessMetallicOcclusionTexture = RMA;

		_sceneObject.Color = ColorTint;

		// 24 bits gameobject id
		// 8 bits user sort layer
		// user sort layer highest bits
		byte[] bytes = GameObject.Id.ToByteArray();
		_sceneObject.SortOrder = ((uint)(SortOrder & 0xFF) << 24) | (uint)(bytes[0] | (bytes[1] << 8) | (bytes[2] << 16));

		_sceneObject.ExclusionBitMask = ExclusionLayer;

		_sceneObject.AttenuationAngle = AttenuationAngle;
	}

	protected override void OnDisabled()
	{
		Transform.OnTransformChanged -= OnTransformChanged;

		_sceneObject?.Delete();
		_sceneObject = null;
	}

	private void OnTransformChanged()
	{
		if ( _sceneObject.IsValid() )
			_sceneObject.Transform = Transform.World;
	}


	protected override void DrawGizmos()
	{
		if ( !Gizmo.IsSelected )
			return;

		Gizmo.Draw.LineThickness = 1;
		Gizmo.Draw.Color = Gizmo.Colors.Green.WithAlpha( Gizmo.IsSelected ? 1.0f : 0.2f );
		Gizmo.Draw.LineBBox( BBox.FromPositionAndSize( Vector3.Zero, Vector3.One ) );
	}
}
