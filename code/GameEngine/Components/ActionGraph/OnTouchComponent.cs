using System.Collections.Generic;
using System.Threading.Tasks;
using Sandbox;

[Title( "On Touch Action" )]
[Category( "Actions" )]
[Icon( "account_tree", "red", "white" )]
public class OnTouchComponent : BaseComponent
{
	public delegate Task StartStopTouchHandler( GameObject self, Collider collider );
	public delegate Task TouchingHandler( GameObject self, IEnumerable<Collider> colliders );

	private HashSet<Collider> _prevTouching = new();
	private HashSet<Collider> _nextTouching = new();

	[Title( "Start Touch" ), Property] public StartStopTouchHandler HandleStartTouch { get; set; }
	[Title( "Touching" ), Property] public TouchingHandler HandleTouching { get; set; }
	[Title( "Stop Touch" ), Property] public StartStopTouchHandler HandleStopTouch { get; set; }

	protected override void OnUpdate()
	{
		_nextTouching.Clear();

		var collider = Components.Get<Collider>();

		if ( collider != null )
		{
			foreach ( var other in collider.Touching )
			{
				_nextTouching.Add( other );
			}
		}

		foreach ( var prev in _prevTouching )
		{
			if ( !_nextTouching.Contains( prev ) )
			{
				HandleStopTouch?.Invoke( GameObject, prev );
			}
		}

		foreach ( var next in _nextTouching )
		{
			if ( !_prevTouching.Contains( next ) )
			{
				HandleStartTouch?.Invoke( GameObject, next );
			}
		}

		if ( _nextTouching.Count > 0 )
		{
			HandleTouching?.Invoke( GameObject, _nextTouching );
		}

		(_prevTouching, _nextTouching) = (_nextTouching, _prevTouching);
	}
}
