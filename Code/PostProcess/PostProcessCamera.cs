using Sandbox.Rendering;
using static Sandbox.GameObjectSystem;
using static Sandbox.PostProcessCamera;
namespace Sandbox;


internal class PostProcessCamera
{
	internal PostProcessCamera( CameraComponent cc )
	{
		Camera = cc;
	}

	public CameraComponent Camera { get; set; }
	public List<WeightedEffect> Effects { get; set; } = new();

	Dictionary<Sandbox.Rendering.Stage, List<Layer>> layers = new();

	public class Layer
	{
		public CommandList CommandList;
		public int Order;
		public Layer()
		{

		}

		public void Render()
		{
			CommandList.ExecuteOnRenderThread();
		}
	}

	public Layer Get( Sandbox.Rendering.Stage stage, int order )
	{
		Layer layer = new Layer();
		layer.Order = order;

		if ( !layers.TryGetValue( stage, out var list ) )
		{
			list = new List<Layer>();
			layers[stage] = list;
		}

		list.Add( layer );

		return layer;
	}

	internal void Clear()
	{
		layers.Clear();
		Effects.Clear();
	}

	/// <summary>
	/// Called for each stage during this camera's render
	/// </summary>
	public void OnRenderStage( Sandbox.Rendering.Stage stage )
	{
		if ( !layers.TryGetValue( stage, out var list ) )
			return;

		foreach( var entry in list.OrderBy( x => x.Order ) )
		{
			entry.Render();
		}
	}
}

public record struct WeightedEffect
{
	public BasePostProcess Effect;
	public float Weight;
}
