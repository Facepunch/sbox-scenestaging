using Sandbox;

public class Hitbox: IDisposable
{
	public Hitbox( GameObject gameObject, ITagSet tagSet, PhysicsBody body )
	{
		GameObject = gameObject;
		Body = body;
		Tags = tagSet;

		Body.MotionEnabled = false;
		Body.BodyType = PhysicsBodyType.Keyframed;
	}

	public Hitbox( GameObject gameObject, BoneCollection.Bone bone, ITagSet tagSet, PhysicsBody body ) : this( gameObject, tagSet, body )
	{
		Bone = bone;
	}

	public GameObject GameObject { get; private set; }

	public BoneCollection.Bone Bone { get; private set; }
	public ITagSet Tags { get; private set; }
	public PhysicsBody Body { get; set; }

	public void Dispose()
	{
		Body?.Remove();
		Body = null;
	}
}
