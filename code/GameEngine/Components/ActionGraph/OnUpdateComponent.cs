using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Sandbox;

[Title( "On Update Action" )]
[Category( "Actions" )]
[Icon( "account_tree", "red", "white" )]
public class OnUpdateComponent : BaseComponent
{
	public delegate Task UpdateHandler( GameObject self, float dt );

	private TimeSince _lastUpdate;

	[Property]
	public float MinPeriod { get; set; } = 0f;

	[Title( "Update" ), Property] public UpdateHandler HandleUpdate { get; set; }

	public override void Update()
	{
		var dt = MinPeriod > 0f ? (float)_lastUpdate : Time.Delta;
		if ( dt < MinPeriod ) return;

		_lastUpdate = 0f;
		HandleUpdate?.Invoke( GameObject, dt );
	}
}
