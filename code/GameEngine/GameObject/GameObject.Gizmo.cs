using Sandbox;
using System.Linq;

public partial class GameObject
{

	Texture handleTexture;
	Color handleColor;

	void BuildGizmoDetails()
	{
		handleTexture = null;

		var handles = Components
			.Where( x => x is not null )
			.Select( x => TypeLibrary.GetType( x.GetType() ) )
			.Where( x => x is not null )
			.SelectMany( x => x.GetAttributes<EditorHandleAttribute>() )
			.FirstOrDefault();

		if ( handles is null )
			return;

		handleColor = Color.White;

		var colorProvider = Components.OfType<IComponentColorProvider>().FirstOrDefault();
		if ( colorProvider is not null )
		{
			handleColor = colorProvider.ComponentColor;

			// this is mainly for lights, we don't want any black bulbs, but we do want to indicate light color
			// so if anything else starts using this we should probably move this logic into the light component implementation
			handleColor = ((Vector3)handleColor).Normal * 2;
		}

		handleTexture = Texture.Load( FileSystem.Mounted, handles.Texture );
	}

	void DrawGizmoHandle( ref bool clicked )
	{
		BuildGizmoDetails();

		if ( handleTexture is null )
			return;

		bool selected = Gizmo.IsSelected;

		using ( Gizmo.Scope( "Handle" ) )
		{
			Gizmo.Transform = Gizmo.Transform.WithScale( 1.0f );

			if ( !selected )
			{
				var initialRadius = 5.0f;
				var distance = Gizmo.Transform.Position.Distance( Gizmo.Camera.Position );
				float newRadius = initialRadius * (distance / 500.0f);
				Gizmo.Hitbox.DepthBias = 0.1f;
				Gizmo.Hitbox.Sphere( new Sphere( 0, newRadius ) );

				clicked = clicked || Gizmo.WasClicked;
			}

			float size = 32;
			if ( Gizmo.IsHovered ) size = 40;

			Gizmo.Draw.IgnoreDepth = true;
			Gizmo.Draw.Color = handleColor;
			Gizmo.Draw.Sprite( Vector3.Zero, size * Gizmo.Settings.GimzoScale, handleTexture, false );
		}
	}

	internal void DrawGizmos()
	{
		if ( !Active ) return;

		var parentTx = Gizmo.Transform;

		using ( Gizmo.ObjectScope( this, Transform.Local ) )
		{
			bool clicked = Gizmo.WasClicked;

			if ( Gizmo.IsSelected )
			{
				DrawTransformGizmos( parentTx );
			}

			DrawGizmoHandle( ref clicked );

			ForEachComponent( "DrawGizmos", true, c =>
			{
				using var scope = Gizmo.Scope();

				c.DrawGizmos();
				clicked = clicked || Gizmo.WasClicked;

			} );

			if ( clicked )
			{
				Gizmo.Select();
				EditLog( "Selection", this );
			}

			ForEachChild( "DrawGizmos", true, c => c.DrawGizmos() );

		}
	}

	void DrawTransformGizmos( Transform parentTransform )
	{
		using var scope = Gizmo.Scope();

		var backup = Transform.Local;
		var tx = backup;

		// use the local position but get rid of local rotation and local scale
		Gizmo.Transform = parentTransform.Add( tx.Position, false );

		Gizmo.Hitbox.DepthBias = 0.1f;

		if ( Gizmo.Settings.EditMode == "position" )
		{
			if ( Gizmo.Control.Position( "position", tx.Position, out var newPos, tx.Rotation ) )
			{
				EditLog( "Position", this );

				tx.Position = newPos;
			}
		}

		if ( Gizmo.Settings.EditMode == "rotation" )
		{
			if ( Gizmo.Control.Rotate( "rotation", tx.Rotation, out var newRotation ) )
			{
				EditLog( "Rotation", this );

				tx.Rotation = newRotation;
			}
		}

		if ( Gizmo.Settings.EditMode == "scale" )
		{
			if ( Gizmo.Control.Scale( "scale", tx.Scale, out var newScale ) )
			{
				EditLog( "Scale", this );

				tx.Scale = newScale.Clamp( 0.001f, 100.0f );
			}
		}

		Transform.Local = tx;
	}

}
