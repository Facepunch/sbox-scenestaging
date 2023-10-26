using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Sandbox;

[Title( "Action Graph Component" )]
[Category( "Physics" )]
[Icon( "account_tree", "red", "white" )]
public class ActionGraphComponent : BaseComponent
{
	public delegate Task GameObjectHandler( GameObject self );

	[Title( "Start" ), Property] public GameObjectHandler HandleStart { get; set; }
	[Title( "Update" ), Property] public GameObjectHandler HandleUpdate { get; set; }
	[Title( "Destroy" ), Property] public GameObjectHandler HandleDestroy { get; set; }

	public override void OnStart()
	{
		HandleStart?.Invoke( GameObject );
	}

	public override void Update()
	{
		HandleUpdate?.Invoke( GameObject );
	}

	public override void OnDestroy()
	{
		HandleDestroy?.Invoke( GameObject );
	}
}
