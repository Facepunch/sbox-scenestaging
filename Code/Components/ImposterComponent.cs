using Sandbox;
using System.Collections.Generic;

namespace SceneStaging;

/// <summary>
/// Configuration component for octahedral imposters.
/// Rendering is handled by ImposterSystem.
/// </summary>
[Title( "Octahedral Imposter" )]
[Category( "Rendering" )]
[Icon( "view_in_ar" )]
public sealed class ImposterComponent : Component, Component.ExecuteInEditor, Component.SpatialGrid
{
	[Property] public OctahedralImposterAsset ImposterAsset { get; set; }
	[Property, Range( 0f, 10000f )] public float ImposterDistance { get; set; } = 5000f;
	[Property, Range( 0.1f, 5.0f ), Group( "Appearance" )] public float SizeMultiplier { get; set; } = 1.0f;
	[Property, Group( "Appearance" )] public Color Tint { get; set; } = Color.White;
	[Property, Group( "Rendering" )] public bool Lighting { get; set; } = true;
	[Property, Group( "Debug" )] public bool ForceShowImposter { get; set; } = false;

	private List<ModelRenderer> _renderers = new();

	public IReadOnlyList<ModelRenderer> Renderers => _renderers;

	protected override void OnStart()
	{
		base.OnStart();
		CacheRenderers();
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( _renderers.Count == 0 ) CacheRenderers();
	}

	private void CacheRenderers()
	{
		_renderers.Clear();
		_renderers.AddRange( GameObject.Components.GetAll<ModelRenderer>(
			FindMode.EnabledInSelfAndDescendants ) );
	}
}
