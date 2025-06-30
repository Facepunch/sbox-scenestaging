public class BaseInteractor : Component, Component.IPressable
{
	[Property] public virtual Action OnUse { get; set; }

	protected override void OnUpdate()
	{

	}

	public virtual void OnUsed()
	{
		OnUse?.Invoke();
	}

	bool IPressable.Press( IPressable.Event e )
	{
		OnUsed();
		return true;
	}
}
