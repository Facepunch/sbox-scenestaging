public class BaseInteractor : Component, Component.IPressable, IGrabAction
{
	[Property] public virtual Action OnUse { get; set; }
	[Property] public GrabAction GrabAction { get; set; } = GrabAction.PushButton;

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
