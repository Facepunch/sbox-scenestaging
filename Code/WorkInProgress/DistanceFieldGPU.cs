using Sandbox.Rendering;
using System.Numerics;

namespace Sandbox;

/// <summary>
/// Distance Field GPU from Hitboxes
/// No acceleration structure, no nothing but capsules, proof of concept
/// </summary>
[Title("Distance Field from Hitboxes")]
[Icon("light_mode")]
[Category("Rendering")]
[Hide]
public sealed partial class DistanceFieldGPU : PostProcess, Component.ExecuteInEditor
{
    struct Shape
    {
		public Vector3 A;
		public Vector3 B;
		public float Radius;
	}

	List<Shape> Shapes = new();
	GpuBuffer<Shape> Buffer;

	CommandList Commands;

	protected override void OnEnabled()
	{
		base.OnEnabled();
		Commands = new CommandList( "Distance Field Constants Upload" );
		Camera.AddCommandList( Commands, Stage.AfterDepthPrepass, 0 );
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();
		Camera.RemoveCommandList( Commands );
	}

	protected override void OnDirty()
	{
		base.OnDirty();
	}

	protected override void OnPreRender()
	{
		base.OnPreRender();

		Shapes.Clear();

		foreach ( var anim in Scene.GetAll<SkinnedModelRenderer>() )
		{
			if ( anim is null )
				return;

			if ( anim.Model is null )
				return;

			foreach ( var hb in anim.Model.HitboxSet.All )
			{
				if ( hb.Bone is null )
					continue;

				if ( hb.Shape is Sphere sphere )
				{
				}
				else if ( hb.Shape is Capsule capsule && anim.TryGetBoneTransform( hb.Bone, out var tx ) )
				{
					Shape s = new Shape() { A = tx.PointToWorld( capsule.CenterA ), B = tx.PointToWorld( capsule.CenterB ), Radius = capsule.Radius };
					Shapes.Add( s );
				}
				else if ( hb.Shape is BBox box )
				{
				}
			}
		}

		if ( Buffer == null || Buffer.ElementCount != Shapes.Count() )
		{
			Buffer = new GpuBuffer<Shape>( Shapes.Count );
		}

		Buffer.SetData( Shapes );

		Commands.Reset();
		Commands.SetGlobal( "HitboxBuffer", Buffer );
		Commands.SetGlobal( "HitboxCount", Shapes.Count );
	}

	protected override void DrawGizmos()
	{
		base.DrawGizmos();

        Gizmo.Draw.Color = Color.White.WithAlpha( 0.5f );
		Gizmo.Transform = global::Transform.Zero;

		foreach( var s in Shapes )
		{
			Gizmo.Draw.SolidCapsule( s.A, s.B, s.Radius, 10, 10 );
		}

	}
}
