using System.Text.Json.Serialization;

namespace Sandbox.Volumes;

/// <summary>
/// A generic way to represent volumes in a scene. If we all end up using this instead of defining our own version
/// in everything, we can improve this and improve everything at the same time.
/// </summary>
public struct SceneVolume
{
	public SceneVolume()
	{
	}

	public enum VolumeTypes
	{
		/// <summary>
		/// A sphere. It's like the earth. Or an eyeball.
		/// </summary>
		Sphere,

		/// <summary>
		/// A box, like a cube.
		/// </summary>
		Box
	}

	[JsonInclude]
	public VolumeTypes Type = VolumeTypes.Box;

	[JsonInclude]
	[ShowIf( "Type", VolumeTypes.Sphere )]
	public Sphere Sphere = new Sphere( 0, 10 );

	[JsonInclude]
	[ShowIf( "Type", VolumeTypes.Box )]
	public BBox Box = BBox.FromPositionAndSize( 0, 100 );

	/// <summary>
	/// Draws an editable sphere/box gizmo, for adjusting the volume
	/// </summary>
	public void DrawGizmos( bool withControls )
	{
		if ( Type == VolumeTypes.Sphere )
		{
			if ( withControls )
			{
				Gizmo.Control.Sphere( "Volume", Sphere.Radius, out Sphere.Radius, Color.Yellow );
			}

			Gizmo.Draw.IgnoreDepth = false;
			Gizmo.Draw.Color = Gizmo.Colors.Blue.WithAlpha( 0.8f );
			Gizmo.Draw.LineSphere( Sphere );

			Gizmo.Draw.IgnoreDepth = true;
			Gizmo.Draw.Color = Color.White.WithAlpha( 0.05f );
			Gizmo.Draw.LineSphere( Sphere );
		}

		if ( Type == VolumeTypes.Box )
		{
			Gizmo.Draw.IgnoreDepth = false;

			if ( withControls )
			{
				Gizmo.Control.BoundingBox( "Volume", Box, out Box );
			}

			Gizmo.Draw.Color = Gizmo.Colors.Blue.WithAlpha( 0.8f );
			Gizmo.Draw.LineBBox( Box );

			Gizmo.Draw.IgnoreDepth = true;
			Gizmo.Draw.Color = Color.White.WithAlpha( 0.05f );
			Gizmo.Draw.LineBBox( Box );
		}
	}

	/// <summary>
	/// Is this point within the volume
	/// </summary>
	public bool Test( in Transform volumeTransform, in Vector3 position )
	{
		return Test( volumeTransform.PointToLocal( position ) );
	}

	/// <summary>
	/// Is this point within the (local space) volume
	/// </summary>
	public bool Test( in Vector3 position )
	{
		if ( Type == VolumeTypes.Sphere )
		{
			return Sphere.Contains( position );
		}

		if ( Type == VolumeTypes.Box )
		{
			return Box.Contains( position );
		}

		return false;
	}

	/// <summary>
	/// Get the actual amount of volume in this shape. This is useful if you want to make
	/// a system where you prioritize by volume size. Don't forget to multiply by scale!
	/// </summary>
	public float GetVolume()
	{
		if ( Type == VolumeTypes.Sphere )
		{
			return Sphere.GetVolume();
		}

		if ( Type == VolumeTypes.Box )
		{
			return Box.GetVolume();
		}

		return 0.0f;
	}
}
