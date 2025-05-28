using Sandbox.Diagnostics;
using Sandbox.MovieMaker;
using Sandbox.MovieMaker.Properties;
using Sandbox.Physics;
using System.Linq;

namespace Editor.MovieMaker;

#nullable enable

partial class Session
{
	private Vector3 _boneDragStart;

	private void DrawBoneGizmos( TrackView trackView, MovieTime time )
	{
		if ( trackView.Parent?.Parent?.Target is not ITrackReference<SkinnedModelRenderer> { Value: { } renderer } )
		{
			return;
		}

		if ( !trackView.IsBoneTransform || !trackView.TransformTrack.TryGetValue( time, out var handleTransform ) )
		{
			return;
		}

		if ( _draggedBone == trackView )
		{
			using var scope = Gizmo.Scope( "IKChain", renderer.WorldTransform );

			foreach ( var body in _boneBodies )
			{
				Gizmo.Draw.Color = body.Value.BodyType == PhysicsBodyType.Static ? Color.Red : Color.White;
				Gizmo.Draw.SolidSphere( body.Value.Position, 1f );
			}
		}

		{
			using var scope = Gizmo.Scope( "BoneHandle", handleTransform );
			
			var handleSphere = new Sphere( 0f, 1f );
			var baseTransform = renderer.WorldTransform;

			Gizmo.Draw.Color = (trackView.IsLocked ? Color.Red : Color.White).WithAlpha( Gizmo.IsHovered ? 1f : 0.5f );
			Gizmo.Draw.LineCircle( handleSphere.Center, 1f );

			if ( Gizmo.IsHovered )
			{
				Gizmo.Draw.SolidSphere( handleSphere.Center, handleSphere.Radius * 0.5f, 6, 6 );
			}

			Gizmo.Hitbox.DepthBias = 0.01f;
			Gizmo.Hitbox.Sphere( handleSphere );

			if ( Gizmo.IsLeftMouseDown && Gizmo.IsHovered )
			{
				if ( Gizmo.WasLeftMousePressed )
				{
					_boneDragStart = Gizmo.Transform.Position;
				}

				var plane = new Plane( _boneDragStart, Gizmo.CameraTransform.Forward );
				var ray = Gizmo.CurrentRay;

				if ( plane.TryTrace( ray, out var hit, true ) )
				{
					var localTarget = baseTransform.PointToLocal( hit );

					Gizmo.Draw.Arrow( 0f, Gizmo.Transform.PointToLocal( hit ), 1f, 0.4f );

					DragBone( renderer, trackView, localTarget );
				}
			}
			else
			{
				StopDraggingBone();
			}
		}
	}

	private PhysicsWorld? _ikWorld;
	private TrackView? _draggedBone;
	private readonly HashSet<BoneCollection.Bone> _unlockedBones = new();
	private readonly HashSet<BoneCollection.Bone> _lockedBones = new();
	private readonly Dictionary<BoneCollection.Bone, PhysicsBody> _boneBodies = new();

	private void DragBone( SkinnedModelRenderer renderer, TrackView boneTrackView, Vector3 localTarget )
	{
		if ( _draggedBone != boneTrackView )
		{
			StopDraggingBone();
		}

		_draggedBone = boneTrackView;

		Assert.True( boneTrackView.IsBoneTransform );

		if ( boneTrackView.IsLocked )
		{
			return;
		}

		if ( boneTrackView.Bone is not { } draggedBone )
		{
			return;
		}

		var firstTime = false;

		if ( _ikWorld is null )
		{
			StartDraggingBone( renderer, boneTrackView );

			firstTime = true;
		}

		if ( _ikWorld is not { } world )
		{
			return;
		}

		if ( !_boneBodies.TryGetValue( draggedBone, out var draggedBody ) )
		{
			return;
		}

		if ( firstTime )
		{
			foreach ( var bone in _unlockedBones )
			{
				EditorEvent.RunInterface<EditorEvent.ISceneEdited>( x => x.ComponentPreEdited( renderer, $"Bones.{bone.Name}" ) );
			}
		}

		const int steps = 10;

		var dt = Time.Delta / steps;

		for ( var i = 0; i < steps; ++i )
		{
			foreach ( var body in _boneBodies.Values )
			{
				if ( body != draggedBody )
				{
					body.Velocity = 0f;
					body.AngularVelocity = 0f;
				}
			}

			draggedBody.SmoothMove( localTarget, 0.1f, dt );
			world.Step( dt );
		}

		var baseTransform = renderer.SceneModel.Transform;

		foreach ( var bone in _unlockedBones.OrderBy( x => x.Index ) )
		{
			if ( !_boneBodies.TryGetValue( bone, out var body ) ) continue;
			if ( bone.Parent is not { } parent ) continue;

			var parentTransform = _boneBodies.TryGetValue( parent, out var parentBody )
				? parentBody.Transform
				: baseTransform.ToLocal( renderer.SceneModel.GetBoneWorldTransform( parent.Index ) );

			var localTransform = parentTransform.ToLocal( body.Transform );

			MovieBoneAnimatorSystem.Current.SetParentSpaceBone( renderer, bone.Index, localTransform );
		}

		foreach ( var bone in _unlockedBones.OrderBy( x => x.Index ) )
		{
			EditorEvent.RunInterface<EditorEvent.ISceneEdited>( x => x.ComponentEdited( renderer, $"Bones.{bone.Name}" ) );
		}
	}

	private void StartDraggingBone( SkinnedModelRenderer renderer, TrackView boneTrackView )
	{
		foreach ( var (_, body) in _boneBodies )
		{
			body.Remove();
		}

		_boneBodies.Clear();
		_ikWorld ??= new PhysicsWorld();

		_unlockedBones.Clear();
		_lockedBones.Clear();

		if ( renderer.Model is not { } model )
		{
			return;
		}

		if ( boneTrackView.Bone is not { } bone )
		{
			return;
		}

		AddBone( bone, boneTrackView );
		SetupRagdoll( renderer.SceneModel, model.Bones, model.Physics );
	}

	private void AddBone( BoneCollection.Bone bone, TrackView? boneTrackView )
	{
		if ( boneTrackView?.IsLocked is not false )
		{
			_lockedBones.Add( bone );

			if ( bone.Parent is { } lockedParent )
			{
				AddBone( lockedParent, null );
			}

			return;
		}

		if ( !_unlockedBones.Add( bone ) ) return;

		if ( bone.Parent is { } parent )
		{
			AddBone( parent, boneTrackView?.Parent?.Children.FirstOrDefault( x => x.Track.Name == parent.Name ) );
		}

		if ( boneTrackView?.IsLocked is false )
		{
			foreach ( var child in bone.Children )
			{
				if ( boneTrackView.Parent?.Children.FirstOrDefault( x => x.Track.Name == child.Name ) is { } childBoneTrackView )
				{
					AddBone( child, childBoneTrackView );
				}
			}
		}
	}

	private void SetupRagdoll( SceneModel model, BoneCollection bones, PhysicsGroupDescription physics )
	{
		// Adapted from Sandbox.Ragdoll

		var bodies = new List<PhysicsBody?>();

		foreach ( var part in physics.Parts )
		{
			var sourceBone = bones.GetBone( part.BoneName );

			if ( !_lockedBones.Contains( sourceBone ) && !_unlockedBones.Contains( sourceBone ) )
			{
				bodies.Add( null );
				continue;
			}

			var body = new PhysicsBody( _ikWorld! )
			{
				Transform = model.Transform.ToLocal( model.GetBoneWorldTransform( sourceBone.Index ) ),
				BodyType = _lockedBones.Contains( sourceBone ) ? PhysicsBodyType.Static : PhysicsBodyType.Dynamic,
				LinearDamping = 100f, // part.LinearDamping;
				AngularDamping = 100f // part.AngularDamping;
			};

			foreach ( var sphere in part.Spheres )
			{
				body.AddSphereShape( sphere.Sphere )
					.EnableSolidCollisions = false;
			}

			foreach ( var capsule in part.Capsules )
			{
				body.AddCapsuleShape( capsule.Capsule.CenterA, capsule.Capsule.CenterB, capsule.Capsule.Radius )
					.EnableSolidCollisions = false;
			}

			foreach ( var hull in part.Hulls )
			{
				body.AddHullShape( 0f, Rotation.Identity, hull.GetPoints().ToList() ).EnableSolidCollisions = false;
			}

			_boneBodies[sourceBone] = body;
			bodies.Add( body );
		}

		foreach ( var jointDesc in physics.Joints )
		{
			if ( bodies[jointDesc.Body1] is not { } body1 ) continue;
			if ( bodies[jointDesc.Body2] is not { } body2 ) continue;

			var point1 = new PhysicsPoint( body1, jointDesc.Frame1.Position, jointDesc.Frame1.Rotation );
			var point2 = new PhysicsPoint( body2, jointDesc.Frame2.Position, jointDesc.Frame2.Rotation );

			switch ( jointDesc.Type )
			{
				case PhysicsGroupDescription.JointType.Hinge:
					{
						var hingeJoint = PhysicsJoint.CreateHinge( point1, point2 );

						if ( jointDesc.EnableTwistLimit )
						{
							hingeJoint.MinAngle = jointDesc.TwistMin;
							hingeJoint.MaxAngle = jointDesc.TwistMax;
						}

						break;
					}
				case PhysicsGroupDescription.JointType.Ball:
					{
						var ballJoint = PhysicsJoint.CreateBallSocket( point1, point2 );

						if ( jointDesc.EnableSwingLimit )
						{
							ballJoint.SwingLimitEnabled = true;
							ballJoint.SwingLimit = new Vector2( jointDesc.SwingMin, jointDesc.SwingMax );
						}

						if ( jointDesc.EnableTwistLimit )
						{
							ballJoint.TwistLimitEnabled = true;
							ballJoint.TwistLimit = new Vector2( jointDesc.TwistMin, jointDesc.TwistMax );
						}

						break;
					}
				case PhysicsGroupDescription.JointType.Fixed:
					{
						var fixedJoint = PhysicsJoint.CreateFixed( point1, point2 );

						fixedJoint.SpringLinear = new PhysicsSpring(
							jointDesc.LinearFrequency,
							jointDesc.LinearDampingRatio );

						fixedJoint.SpringAngular = new PhysicsSpring(
							jointDesc.AngularFrequency,
							jointDesc.AngularDampingRatio );
						break;
					}
				case PhysicsGroupDescription.JointType.Slider:
					{
						PhysicsJoint.CreateSlider( point1, point2, jointDesc.LinearMin, jointDesc.LinearMax );
						break;
					}
			}
		}
	}

	private void StopDraggingBone()
	{
		_draggedBone = null;

		_boneBodies.Clear();

		_ikWorld?.Delete();
		_ikWorld = null;
	}
}
