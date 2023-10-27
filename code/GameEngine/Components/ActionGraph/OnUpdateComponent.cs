using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Sandbox;

[Title( "On Update Action" )]
[Category( "Actions" )]
[Icon( "account_tree", "red", "white" )]
public class OnUpdateComponent : BaseComponent
{
	public delegate Task UpdateHandler( GameObject self, float dt );

	[Title( "Update" ), Property] public UpdateHandler HandleUpdate { get; set; }

	public override void Update()
	{
		HandleUpdate?.Invoke( GameObject, Time.Delta );
	}
}
