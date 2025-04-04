namespace Sandbox;

[Alias( "Sandbox.ApplyLocalClothing" )]
public sealed class Dresser : Component, Component.ExecuteInEditor
{
	public enum ClothingSource
	{
		Manual,
		LocalUser,
		OwnerConnection
	}

	/// <summary>
	/// Where to get the clothing from
	/// </summary>
	[Property]
	public ClothingSource Source { get; set; }

	/// <summary>
	/// Who are we dressing? This should be the renderer of the body of a Citizen or Human
	/// </summary>
	[Property]
	public SkinnedModelRenderer BodyTarget { get; set; }

	/// <summary>
	/// Should we change the height too?
	/// </summary>
	[Property]
	public bool ApplyHeightScale { get; set; } = true;

	[ShowIf( "Source", ClothingSource.Manual )]
	[Property]
	public List<ClothingContainer.ClothingEntry> Clothing { get; set; }

	protected override void OnAwake()
	{
		if ( IsProxy )
			return;

		Apply();
	}

	ClothingContainer GetClothing()
	{
		if ( Source == ClothingSource.OwnerConnection )
		{
			var clothing = new ClothingContainer();

			if ( Network.Owner != null )
			{
				clothing.Deserialize( Network.Owner.GetUserData( "avatar" ) );
			}

			return clothing;
		}

		if ( Source == ClothingSource.LocalUser )
		{
			return ClothingContainer.CreateFromLocalUser();
		}

		if ( Source == ClothingSource.Manual )
		{
			var clothing = new ClothingContainer();
			clothing.AddRange( Clothing );
			clothing.Normalize();
			return clothing;
		}

		return null;
	}

	[Button( "Apply Clothing" )]
	public void Apply()
	{
		if ( !BodyTarget.IsValid() )
			return;

		var clothing = GetClothing();
		if ( clothing is null )
			return;

		if ( !ApplyHeightScale )
		{
			clothing.Height = 1;
		}

		clothing.AddRange( Clothing );
		clothing.Normalize();

		clothing.Apply( BodyTarget );

		BodyTarget.PostAnimationUpdate();
	}

	protected override void OnValidate()
	{
		if ( IsProxy )
			return;

		base.OnValidate();

		using var p = Scene.Push();

		if ( !BodyTarget.IsValid() )
		{
			BodyTarget = GetComponentInChildren<SkinnedModelRenderer>();
		}

		Apply();
	}
}
