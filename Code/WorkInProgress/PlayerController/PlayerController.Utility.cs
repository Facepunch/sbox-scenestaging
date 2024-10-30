namespace Sandbox;

public sealed partial class PlayerController : Component
{
	/// <summary>
	/// Create a ragdoll gameobject version of our render body.
	/// </summary>
	public GameObject CreateRagdoll( string name = "Ragdoll" )
	{
		var go = new GameObject( true, name );
		go.Tags.Add( "ragdoll" );
		go.Transform.World = Transform.World;

		var originalBody = Renderer.Components.Get<SkinnedModelRenderer>();

		if ( originalBody.IsValid() )
		{
			var mainBody = go.Components.Create<SkinnedModelRenderer>();
			mainBody.CopyFrom( originalBody );
			mainBody.UseAnimGraph = false;

			// copy the clothes
			foreach ( var clothing in originalBody.GameObject.Children.SelectMany( x => x.Components.GetAll<SkinnedModelRenderer>() ) )
			{
				var newClothing = new GameObject( true, clothing.GameObject.Name );
				newClothing.Parent = go;

				var item = newClothing.Components.Create<SkinnedModelRenderer>();
				item.CopyFrom( clothing );
				item.BoneMergeTarget = mainBody;
			}

			var physics = go.Components.Create<ModelPhysics>();
			physics.Model = mainBody.Model;
			physics.Renderer = mainBody;
			physics.CopyBonesFrom( originalBody, true );
		}

		return go;
	}
}
