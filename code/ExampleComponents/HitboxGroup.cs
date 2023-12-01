using Sandbox;
using Sandbox.MenuSystem;

public sealed class HitboxGroup : BaseComponent, BaseComponent.ExecuteInEditor
{
	protected override void DrawGizmos()
	{
		foreach( var hitbox in Hitboxes )
		{
			hitbox.DrawGizmos();
		}
	}

	protected override void OnEnabled()
	{
		Hitboxes.Clear();
		AddFrom( Components.Get<SkinnedModelRenderer>() );
	}

	private void AddFrom( SkinnedModelRenderer anim )
	{
		if ( anim is null )
			return;

		if ( anim.Model is null )
			return;

		foreach ( var hitbox in anim.Model.HitboxSet.All )
		{
			if ( hitbox.Bone is null )
				continue;

			var tx = anim.GetBoneTransform( hitbox.Bone, true );

			if ( hitbox.Shape is Sphere sphere )
			{

			}

			if ( hitbox.Shape is Capsule capsule )
			{
				var b = new CapsuleHitbox( GameObject, hitbox.Bone, hitbox.Tags, capsule );
				b.Transform = tx;
				AddHitbox( b );

			}

			if ( hitbox.Shape is BBox box )
			{

			}
		}
	}

	protected override void OnUpdate()
	{
		var renderer = Components.Get<SkinnedModelRenderer>();

		foreach ( var hitbox in Hitboxes )
		{
			hitbox.Transform = renderer.GetBoneTransform( hitbox.Bone, true );
		}
	}

	List<Hitbox> Hitboxes = new List<Hitbox>();

	public void AddHitbox( Hitbox hitbox )
	{
		Hitboxes.Add( hitbox );
	}

	public void Trace( in SceneTraceBuilder trace, List<SceneTraceResult> results )
	{
		foreach ( var hitbox in Hitboxes )
		{
			var tr = hitbox.Trace( trace );
			if ( !tr.Hit ) continue;

			tr.Hitbox = hitbox;
			tr.GameObject = GameObject;

			results.Add( tr );

		}
	}
}


public abstract class Hitbox
{
	public Hitbox( GameObject gameObject, Transform transform, IEnumerable<string> tagSet )
	{
		GameObject = gameObject;
		Transform = transform;

		Tags = new TagSet();

		foreach ( var tag in tagSet )
			Tags.Add( tag );
	}

	public Hitbox( GameObject gameObject, BoneCollection.Bone bone, IEnumerable<string> tagSet ) : this( gameObject, gameObject.Transform.World, tagSet )
	{
		Bone = bone;
	}

	public GameObject GameObject { get; private set; }

	public BoneCollection.Bone Bone { get; private set; }
	public Transform Transform { get; set; }
	public ITagSet Tags { get; private set; }

	public abstract void DrawGizmos();
	public abstract SceneTraceResult Trace( in SceneTraceBuilder trace );
}

public class CapsuleHitbox : Hitbox
{
	public Capsule Capsule { get; set; }

	public CapsuleHitbox( GameObject gameObject, Transform transform, IEnumerable<string> tags, Capsule capsule ) : base( gameObject, transform, tags )
	{
		Capsule = capsule;
	}

	public CapsuleHitbox( GameObject gameObject, BoneCollection.Bone bone, IEnumerable<string> tags, Capsule capsule ) : base( gameObject, bone, tags )
	{
		Capsule = capsule;
	}

	public override void DrawGizmos()
	{
		
		using ( Gizmo.ObjectScope( this, Transform ) )
		{
			Gizmo.Draw.Color = Color.White.WithAlpha( 0.1f );
			Gizmo.Transform = Transform;
			Gizmo.Draw.LineCapsule( Capsule );
		}
	}

	public override SceneTraceResult Trace( in SceneTraceBuilder trace )
	{
		return trace.RunAgainstCapsule( Capsule, Transform );
	}
}
