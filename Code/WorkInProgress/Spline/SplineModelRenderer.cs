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

	protected override void OnEnabled()
	{
		if ( Model.IsValid() && Spline.IsValid() )
		{
			commands = new CommandList( "SplineModel" );
			Scene.Camera.AddCommandList( commands, Stage.AfterOpaque );
		}
	}

	CommandList commands = new CommandList( "SplineModel" );

	GpuBuffer<GpuSplineSegment> segmentGpuBuffer;

	GpuSplineSegment[] segmentBuffer;

	Transform[] transformBuffer;

	protected override void OnDisabled()
	{
		if ( commands != null )
		{
			Scene.Camera.RemoveCommandList( commands );
			commands = null;
		}
	}

	protected override void OnUpdate()
	{
		if ( !Model.IsValid() || !Spline.IsValid() )
		{
			return;
		}

		commands.Reset();

		var sizeInModelDir = Model.Bounds.Size.Dot( Vector3.Forward );

		var minInModelDir = Model.Bounds.Center.Dot( Vector3.Forward ) - sizeInModelDir / 2;

		var meshesRequired = (int)Math.Round( Spline.GetLength() / sizeInModelDir );
		var distancePerCurve = Spline.GetLength() / meshesRequired;
		
		if ( segmentBuffer == null || segmentBuffer.Length != meshesRequired )
		{
			segmentBuffer = new GpuSplineSegment[meshesRequired];
		}

		if ( transformBuffer == null || transformBuffer.Length != meshesRequired )
		{
			transformBuffer = new Transform[meshesRequired];
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

			transformBuffer[meshIndex] = segmentTransform;

			var segment = new GpuSplineSegment
			{
				P1 = new Vector4( segmentTransform.PointToLocal( P1World ) ),
				P2 = new Vector4( segmentTransform.PointToLocal( P2World ) ),
				P3 = new Vector4( segmentTransform.PointToLocal( P3World ) ),
				RollStartEnd = new Vector4( rollAtStart, rollAtEnd, 0, 0 ),
				WidthHeightScaleStartEnd = new Vector4( scaleAtStart.x, scaleAtStart.y, scaleAtEnd.x, scaleAtEnd.y )
			};

			segmentBuffer[meshIndex] = segment;
		}

		if ( segmentGpuBuffer == null || segmentGpuBuffer.ElementCount != meshesRequired )
		{
			segmentGpuBuffer = new ( meshesRequired, GpuBuffer.UsageFlags.Structured, "SplineSegments" );
		}
		segmentGpuBuffer.SetData( segmentBuffer );

		RenderAttributes attributes = new();
		attributes.Set( "SplineSegments", segmentGpuBuffer );

		attributes.Set( "MinInModelDir", minInModelDir );
		attributes.Set( "SizeInModelDir", sizeInModelDir );

		commands.DrawModelInstanced( Model, transformBuffer, attributes );	
	}
}
