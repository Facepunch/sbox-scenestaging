using Sandbox;
using System;

public partial class GameObject
{
	internal void DrawGizmos()
	{
		if ( !Active ) return;

		var parentTx = Gizmo.Transform;

		using ( Gizmo.ObjectScope( this, Transform ) )
		{
			if ( Gizmo.IsSelected )
			{
				DrawTransformGizmos( parentTx );
			}

			bool clicked = Gizmo.WasClicked;

			foreach ( var component in Components )
			{
				if ( !component.Enabled ) continue;

				using var scope = Gizmo.Scope();

				component.DrawGizmos();
				clicked |= Gizmo.WasClicked;
			}

			if ( clicked )
			{
				Gizmo.Select();
			}

			foreach ( var child in Children )
			{
				child.DrawGizmos();
			}

		}
	}

	void DrawTransformGizmos( Transform parentTransform )
	{
		using var scope = Gizmo.Scope();

		var backup = Transform;
		var tx = backup;

		// use the local position but get rid of local rotation and local scale
		Gizmo.Transform = parentTransform.Add( tx.Position, false );

		Gizmo.Hitbox.DepthBias = 0.1f;

		if ( Gizmo.Settings.EditMode == "position" )
		{
			if ( Gizmo.Control.Position( "position", tx.Position, out var newPos, tx.Rotation ) )
			{
				EditLog( "Position", this, () => Transform = backup );

				tx.Position = newPos;
			}
		}

		if ( Gizmo.Settings.EditMode == "rotation" )
		{
			if ( Gizmo.Control.Rotate( "rotation", tx.Rotation, out var newRotation ) )
			{
				EditLog( "Rotation", this, () => Transform = backup );

				tx.Rotation = newRotation;
			}
		}

		if ( Gizmo.Settings.EditMode == "scale" )
		{
			if ( Gizmo.Control.Scale( "scale", tx.Scale, out var newScale ) )
			{
				EditLog( "Scale", this, () => Transform = backup );

				tx.Scale = newScale.Clamp( 0.001f, 100.0f );
			}
		}

		Transform = tx;
	}

}
