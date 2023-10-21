public abstract class TransformProxy
{
	public virtual Transform GetLocalTransform()
	{
		return default;
	}

	public virtual void SetLocalTransform( in Transform value )
	{

	}

	public virtual Transform GetWorldTransform()
	{
		return default;
	}

	public virtual void SetWorldTransform( Transform value )
	{

	}
}
