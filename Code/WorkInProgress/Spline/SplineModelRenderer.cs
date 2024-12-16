using Sandbox.Rendering;
using System.Runtime.InteropServices;

namespace Sandbox;

public sealed class SplineModelRendererComponent : Component, Component.ExecuteInEditor
{
	[Property] public SplineComponent Spline { get; set; }

	[Property] public Model Model { get; set; }

	[StructLayout( LayoutKind.Sequential, Pack = 0 )]
	private struct GpuSplineSegment
	{
		// public Vector4 P0; // P0 is ommited because it is always 0,0,0
		public Vector4 P1;
		public Vector4 P2;
		public Vector4 P3;
		public Vector4 RollStartEnd;
		public Vector4 WidthHeightScaleStartEnd;
	}

	private List<SceneObject> sceneObjects;

	protected override void OnEnabled()
	{
		if ( Model.IsValid() && Spline.IsValid() )
		{
			sceneObjects = new();
		}
	}

	protected override void OnDisabled()
	{
		foreach ( var sceneObject in sceneObjects )
		{
			sceneObject.Delete();
		}
		sceneObjects.Clear();
	}

	protected override void OnPreRender()
	{
		if ( !Model.IsValid() || !Spline.IsValid() )
		{
			return;
		}

		var sizeInModelDir = Model.Bounds.Size.Dot( Vector3.Forward );

		var minInModelDir = Model.Bounds.Center.Dot( Vector3.Forward ) - sizeInModelDir / 2;

		var meshesRequired = (int)Math.Round( Spline.GetLength() / sizeInModelDir );
		var distancePerCurve = Spline.GetLength() / meshesRequired;

		// TODO handle somehow
		if (meshesRequired == 0 )
		{
			return;
		}

		// create enough sceneobjects
		for ( int i = sceneObjects.Count; i < meshesRequired; i++ )
		{
			var sceneObject = new SceneObject(Scene.SceneWorld, Model);
			sceneObject.Transform = WorldTransform;
			sceneObject.SetComponentSource( this );
			sceneObject.Tags.SetFrom( GameObject.Tags );
			sceneObjects.Add( sceneObject );
		}

		// delete if there are too many
		for ( int i = sceneObjects.Count - 1; i >= meshesRequired; i-- )
		{
			sceneObjects[i].Delete();
			sceneObjects.RemoveAt( i );
		}

		for ( var meshIndex = 0; meshIndex < meshesRequired; meshIndex++ )
		{
			var P0 = Spline.GetPositionAtDistance( meshIndex * distancePerCurve );
			var P1 = P0 + Spline.GetTangetAtDistance( meshIndex * distancePerCurve ) * distancePerCurve / 3;
			var P3 = Spline.GetPositionAtDistance( (meshIndex + 1) * distancePerCurve );
			var P2 = P3 - Spline.GetTangetAtDistance( (meshIndex + 1) * distancePerCurve ) * distancePerCurve / 3;

			// convert to worldspace
			var P0World = Spline.WorldTransform.PointToWorld( P0 );
			var P1World = Spline.WorldTransform.PointToWorld( P1 );
			var P2World = Spline.WorldTransform.PointToWorld( P2 );
			var P3World = Spline.WorldTransform.PointToWorld( P3 );

			var segmentTransform = new Transform( P0World , Rotation.LookAt( P3World - P0World ) );
			segmentTransform.Rotation = new Angles( 0, segmentTransform.Rotation.Yaw(), 0 ).ToRotation();

			var rollAtStart = MathX.DegreeToRadian(Spline.GetRollAtDistance( meshIndex * distancePerCurve ));
			var rollAtEnd = MathX.DegreeToRadian( Spline.GetRollAtDistance( (meshIndex + 1) * distancePerCurve ));

			var scaleAtStart = Spline.GetScaleAtDistance( meshIndex * distancePerCurve );
			var scaleAtEnd = Spline.GetScaleAtDistance( (meshIndex + 1) * distancePerCurve );

			var segment = new GpuSplineSegment
			{
				P1 = new Vector4( segmentTransform.PointToLocal( P1World ) ),
				P2 = new Vector4( segmentTransform.PointToLocal( P2World ) ),
				P3 = new Vector4( segmentTransform.PointToLocal( P3World ) ),
				RollStartEnd = new Vector4( rollAtStart, rollAtEnd, 0, 0 ),
				WidthHeightScaleStartEnd = new Vector4( scaleAtStart.x, scaleAtStart.y, scaleAtEnd.x, scaleAtEnd.y )
			};

			// TODO pack this more efficiently
			sceneObjects[meshIndex].Attributes.Set( "P1", segment.P1 );
			sceneObjects[meshIndex].Attributes.Set( "P2", segment.P2 );
			sceneObjects[meshIndex].Attributes.Set( "P3", segment.P3 );
			sceneObjects[meshIndex].Attributes.Set( "RollStartEnd", segment.RollStartEnd );
			sceneObjects[meshIndex].Attributes.Set( "WidthHeightScaleStartEnd", segment.WidthHeightScaleStartEnd );
			sceneObjects[meshIndex].Attributes.Set( "MinInModelDir", minInModelDir );
			sceneObjects[meshIndex].Attributes.Set( "SizeInModelDir", sizeInModelDir );

			// :(
			sceneObjects[meshIndex].Batchable = false;

			sceneObjects[meshIndex].Transform = segmentTransform;

			// TODO deform bounds to ennsure culling is working
		}
	}
}
