using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Sandbox;

[Title( "On Start Action" )]
[Category( "Actions" )]
[Icon( "account_tree", "red", "white" )]
public class OnStartComponent : BaseComponent
{
	public delegate Task StartHandler( GameObject self );

	[Title( "Start" ), Property] public StartHandler HandleStart { get; set; }

	public override void OnStart()
	{
		HandleStart?.Invoke( GameObject );
	}
}
