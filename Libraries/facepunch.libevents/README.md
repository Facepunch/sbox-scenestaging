# facepunch.libevents
Easily dispatch events in your scene when stuff happens.

## Basics
Declare an event type implementing `IGameEvent` with all the properties you want to pass around.
```csharp
public record DamagedEvent(
    GameObject Attacker,
    GameObject Victim,
    int Damage ) : IGameEvent;
```
Implement `IGameEventHandler<T>` for your custom event type in a `Component`.
```csharp
public sealed class MyComponent : Component,
    IGameEventHandler<DamagedEvent>
{
    public void OnGameEvent( DamagedEvent eventArgs )
    {
        Log.Info( $"{eventArgs.Victim.Name} says \"Ouch!\"" );
    }
}
```
Dispatch the event on a `GameObject` or the `Scene`, which will notify any components in its descendants.
```csharp
GameObject.Dispatch( new DamagedEvent( attacker, victim, 50 ) );
```

## Invocation order
You can control the order that handlers are invoked using attributes on the handler method.
* `Early`: run this first
* `Late`: run this last
* `Before<T>`: run this before T's handler
* `After<T>`: run this after T's handler
```csharp
[Early, After<SomeOtherComponent>]
public void OnGameEvent( DamagedEvent eventArgs )
{
    Log.Info( $"{eventArgs.Victim.Name} says \"Ouch!\"" );
}
```
