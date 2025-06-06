using System.Runtime.CompilerServices;

namespace Sandbox.MovieMaker.Properties;

#nullable enable

/// <summary>
/// Pseudo-property on a <see cref="SkinnedModelRenderer"/> that has a sub-property for each bone.
/// </summary>
internal sealed class BoneAccessor
{
	private readonly Dictionary<int, Transform> _parentSpaceOverrides = new();
	private readonly Dictionary<int, Transform> _localSpaceOverrides = new();
	private readonly SkinnedModelRenderer _renderer;

	public SkinnedModelRenderer Renderer => _renderer;
								
	public BoneAccessor( SkinnedModelRenderer renderer )
	{
		_renderer = renderer;
	}

	public bool HasBone( string name ) => _renderer.Model?.Bones.HasBone( name ) ?? false;

	public Transform GetParentSpace( int index )
	{
		return _parentSpaceOverrides.TryGetValue( index, out var overrideTransform )
			? overrideTransform
			: Renderer.GetParentSpaceBone( index );
	}

	public void SetParentSpace( int index, Transform value )
	{
		_parentSpaceOverrides[index] = value;
	}

	public void ClearOverrides()
	{
		_parentSpaceOverrides.Clear();
	}

	public void ApplyOverrides()
	{
		_renderer.ClearPhysicsBones();

		if ( _renderer.Model is not { } model ) return;
		if ( _renderer.SceneModel is not { } sceneModel ) return;
		if ( _parentSpaceOverrides.Count == 0 ) return;

		// TODO: I'm assuming parent bones are always listed before child bones

		_localSpaceOverrides.Clear();

		foreach ( var bone in model.Bones.AllBones )
		{
			if ( !_parentSpaceOverrides.TryGetValue( bone.Index, out var parentLocalTransform ) ) continue;

			var parentTransform = bone.Parent is not { Index: var parentIndex }
				? Transform.Zero
				: _localSpaceOverrides.TryGetValue( parentIndex, out var parentOverride )
					? parentOverride
					: sceneModel.GetBoneLocalTransform( parentIndex );

			var localTransform = parentTransform.ToWorld( parentLocalTransform );

			_localSpaceOverrides[bone.Index] = localTransform;

			sceneModel.SetBoneOverride( bone.Index, localTransform );
		}
	}
}

/// <summary>
/// Reads / writes a bone transform on a <see cref="SkinnedModelRenderer"/>.
/// </summary>
file sealed record BoneProperty( ITrackProperty<BoneAccessor?> Parent, string Name )
	: ITrackProperty<Transform>
{
	private (SkinnedModelRenderer? Renderer, int? Index)? _cached;

	public bool IsBound => Parent.Value?.HasBone( Name ) ?? false;

	public Transform Value
	{
		get => GetInfo().Index is not { } index ? Transform.Zero : Parent.Value?.GetParentSpace( index ) ?? Transform.Zero;
		set
		{
			if ( GetInfo().Index is { } index )
			{
				Parent.Value?.SetParentSpace( index, value );
			}
		}
	}

	ITrackTarget ITrackProperty.Parent => Parent;

	private (SkinnedModelRenderer? Renderer, int? Index) GetInfo()
	{
		if ( _cached is { } cached && cached.Renderer == Parent.Value?.Renderer )
		{
			return cached;
		}

		var renderer = Parent.Value?.Renderer;
		var index = renderer?.Model?.Bones.GetBone( Name )?.Index;

		_cached = cached = (renderer, index);

		return cached;
	}
}

file sealed class BonePropertyFactory : ITrackPropertyFactory<ITrackProperty<BoneAccessor?>, Transform>
{
	string ITrackPropertyFactory.CategoryName => "Bones";

	/// <summary>
	/// Any property inside a <see cref="BoneAccessor"/> is a bone.
	/// </summary>
	public bool PropertyExists( ITrackProperty<BoneAccessor?> parent, string name ) => true;

	public ITrackProperty<Transform> CreateProperty( ITrackProperty<BoneAccessor?> parent, string name ) => new BoneProperty( parent, name );

	public IEnumerable<string> GetPropertyNames( ITrackProperty<BoneAccessor?> parent )
	{
		return parent is { IsBound: true, Value.Renderer.Model: { } model }
			? model.Bones.AllBones.Select( x => x.Name )
			: [];
	}
}

file sealed record BoneAccessorProperty( ITrackReference<SkinnedModelRenderer> Parent )
	: ITrackProperty<BoneAccessor?>
{
	private readonly Dictionary<int, Transform> _parentLocalOverrides = new();

	public const string PropertyName = "Bones";

	public string Name => PropertyName;

	public BoneAccessor? Value
	{
		get => Parent.Value is { } renderer
			? MovieBoneAnimatorSystem.Current?.GetBoneAccessor( renderer )
			: null;

		set { }
	}

	bool ITrackProperty.CanWrite => false;

	ITrackTarget ITrackProperty.Parent => Parent;
}

file sealed class BoneAccessorPropertyFactory : ITrackPropertyFactory<ITrackReference<SkinnedModelRenderer>, BoneAccessor?>
{
	public IEnumerable<string> GetPropertyNames( ITrackReference<SkinnedModelRenderer> parent ) =>
		[BoneAccessorProperty.PropertyName];

	public bool PropertyExists( ITrackReference<SkinnedModelRenderer> parent, string name ) =>
		name == "Bones";

	public ITrackProperty<BoneAccessor?> CreateProperty( ITrackReference<SkinnedModelRenderer> parent, string name ) =>
		new BoneAccessorProperty( parent );
}

public sealed class MovieBoneAnimatorSystem : GameObjectSystem<MovieBoneAnimatorSystem>
{
	private readonly ConditionalWeakTable<SkinnedModelRenderer, BoneAccessor> _accessors = new();

	public MovieBoneAnimatorSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.UpdateBones, -1_000, UpdateBones, "UpdateBones" );
	}

	public void UpdateBones()
	{
		foreach ( var (_, accessor) in _accessors )
		{
			accessor.ApplyOverrides();
		}
	}

	public void ClearBones( SkinnedModelRenderer renderer )
	{
		if ( _accessors.TryGetValue( renderer, out var accessor ) )
		{
			accessor.ClearOverrides();
		}
	}

	public void SetParentSpaceBone( SkinnedModelRenderer renderer, int index, Transform transform )
	{
		GetBoneAccessor( renderer ).SetParentSpace( index, transform );
	}

	internal BoneAccessor GetBoneAccessor( SkinnedModelRenderer renderer )
	{
		if ( _accessors.TryGetValue( renderer, out var existing ) ) return existing;

		existing = new BoneAccessor( renderer );
		_accessors.Add( renderer, existing );

		return existing;
	}
}

public static class BoneExtensions
{
	public static Transform GetParentSpaceBone( this SkinnedModelRenderer renderer, int index)
	{
		if ( renderer.Model is not { } model ) return Transform.Zero;
		if ( index < 0 || index >= model.BoneCount ) return Transform.Zero;

		return renderer.GetParentSpaceBone( model.Bones.AllBones[index] );
	}

	public static Transform GetParentSpaceBone( this SkinnedModelRenderer renderer, BoneCollection.Bone bone )
	{
		var localTransform = renderer.SceneModel.GetBoneLocalTransform( bone.Index );

		return bone.Parent is { } parent
			? renderer.SceneModel.GetBoneLocalTransform( parent.Index ).ToLocal( localTransform )
			: localTransform;
	}
}
