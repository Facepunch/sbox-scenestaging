namespace Sandbox.Events.Tests;

[TestClass]
public class DispatchTests
{
	[TestMethod]
	public void Simple()
	{
		var scene = new Scene();

		using var _ = scene.Push();

		var go = new GameObject();

		go.Components.Create<EarlyHandler>();
		go.Components.Create<Handler>();
		go.Components.Create<LateHandler>();

		go.Components.Create<AfterLateHandler>();
		go.Components.Create<BeforeLateHandler>();

		go.Components.Create<AfterEarlyHandler>();
		go.Components.Create<BeforeEarlyHandler>();

		go.Components.Create<BeforeHandler>();
		go.Components.Create<AfterHandler>();

		scene.Dispatch( new ExampleEventArgs() );

		Assert.IsTrue( go.Components.Get<EarlyHandler>().Index < go.Components.Get<Handler>().Index );
		Assert.IsTrue( go.Components.Get<Handler>().Index < go.Components.Get<LateHandler>().Index );

		Assert.IsTrue( go.Components.Get<BeforeEarlyHandler>().Index < go.Components.Get<EarlyHandler>().Index );
		Assert.IsTrue( go.Components.Get<EarlyHandler>().Index < go.Components.Get<AfterEarlyHandler>().Index );

		Assert.IsTrue( go.Components.Get<BeforeHandler>().Index < go.Components.Get<Handler>().Index );
		Assert.IsTrue( go.Components.Get<Handler>().Index < go.Components.Get<AfterHandler>().Index );

		Assert.IsTrue( go.Components.Get<BeforeLateHandler>().Index < go.Components.Get<LateHandler>().Index );
		Assert.IsTrue( go.Components.Get<LateHandler>().Index < go.Components.Get<AfterLateHandler>().Index );
	}
}

public class ExampleEventArgs : IGameEvent
{
	public int HandleCount { get; set; }
}

public abstract class BaseHandler : Component
{
	public int Index { get; set; }

	protected void Handle( ExampleEventArgs eventArgs )
	{
		Index = ++eventArgs.HandleCount;
	}
}

public sealed class Handler : BaseHandler, IGameEventHandler<ExampleEventArgs>
{
	void IGameEventHandler<ExampleEventArgs>.OnGameEvent( ExampleEventArgs eventArgs ) => Handle( eventArgs );
}

public sealed class EarlyHandler : BaseHandler, IGameEventHandler<ExampleEventArgs>
{
	[Early]
	void IGameEventHandler<ExampleEventArgs>.OnGameEvent( ExampleEventArgs eventArgs ) => Handle( eventArgs );
}

public sealed class LateHandler : BaseHandler, IGameEventHandler<ExampleEventArgs>
{
	[Late]
	void IGameEventHandler<ExampleEventArgs>.OnGameEvent( ExampleEventArgs eventArgs ) => Handle( eventArgs );
}

public sealed class BeforeHandler : BaseHandler, IGameEventHandler<ExampleEventArgs>
{
	[Before<Handler>]
	void IGameEventHandler<ExampleEventArgs>.OnGameEvent( ExampleEventArgs eventArgs ) => Handle( eventArgs );
}

public sealed class AfterHandler : BaseHandler, IGameEventHandler<ExampleEventArgs>
{
	[After<Handler>]
	void IGameEventHandler<ExampleEventArgs>.OnGameEvent( ExampleEventArgs eventArgs ) => Handle( eventArgs );
}

public sealed class BeforeEarlyHandler : BaseHandler, IGameEventHandler<ExampleEventArgs>
{
	[Before<EarlyHandler>]
	void IGameEventHandler<ExampleEventArgs>.OnGameEvent( ExampleEventArgs eventArgs ) => Handle( eventArgs );
}

public sealed class AfterEarlyHandler : BaseHandler, IGameEventHandler<ExampleEventArgs>
{
	[After<EarlyHandler>]
	void IGameEventHandler<ExampleEventArgs>.OnGameEvent( ExampleEventArgs eventArgs ) => Handle( eventArgs );
}

public sealed class BeforeLateHandler : BaseHandler, IGameEventHandler<ExampleEventArgs>
{
	[Before<LateHandler>]
	void IGameEventHandler<ExampleEventArgs>.OnGameEvent( ExampleEventArgs eventArgs ) => Handle( eventArgs );
}

public sealed class AfterLateHandler : BaseHandler, IGameEventHandler<ExampleEventArgs>
{
	[After<LateHandler>]
	void IGameEventHandler<ExampleEventArgs>.OnGameEvent( ExampleEventArgs eventArgs ) => Handle( eventArgs );
}
