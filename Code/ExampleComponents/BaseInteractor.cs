using Sandbox;

public class BaseInteractor : Component
{
	[Property] public virtual Action OnUse { get; set; }

	protected override void OnUpdate()
	{

	}

	public virtual void OnUsed()
	{
		OnUse?.Invoke();
	}
}
