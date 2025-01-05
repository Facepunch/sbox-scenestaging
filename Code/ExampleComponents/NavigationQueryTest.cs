public sealed class NavigationQueryTest : Component
{

	[Property] public bool ClosestPoint { get; set; }

	[Property] public bool ClosestEdge { get; set; }

	[Property, ToggleGroup( "Path" )] public bool Path { get; set; }
	[Property, ToggleGroup( "Path" )] public GameObject Target { get; set; }

	[Property, ToggleGroup( "RandomPoint" )] public bool RandomPoint { get; set; }
	[Property, ToggleGroup( "RandomPoint" )] public float RandomRadius { get; set; } = 100.0f;


	[Property] public bool AttractAgents { get; set; }

	RealTimeSince timeSinceUpdate = 0;

	protected override void OnUpdate()
	{
		if ( AttractAgents && timeSinceUpdate > 1 )
		{
			timeSinceUpdate = 0;
			foreach ( var agent in Scene.GetAllComponents<NavMeshAgent>() )
			{
				agent.MoveTo( WorldPosition );
			}
		}
	}

	protected override void DrawGizmos()
	{
		Gizmo.Transform = global::Transform.Zero;

		if ( ClosestPoint )
		{
			var pos = Scene.NavMesh.GetClosestPoint( BBox.FromPositionAndSize( WorldPosition, 80000 ) );

			if ( pos.HasValue )
			{
				Gizmo.Draw.Color = Color.Orange;
				Gizmo.Draw.LineThickness = 5;
				Gizmo.Draw.Arrow( WorldPosition, pos.Value );

			}
		}

		if ( RandomPoint )
		{
			Gizmo.Draw.LineThickness = 2;
			Gizmo.Draw.Color = Color.Black.WithAlpha( 0.3f );
			Gizmo.Draw.LineSphere( WorldPosition, RandomRadius );

			for ( int i = 0; i < 100; i++ )
			{
				var pos = Scene.NavMesh.GetRandomPoint( WorldPosition, RandomRadius );

				if ( pos.HasValue )
				{
					Gizmo.Draw.Color = Color.Black;
					Gizmo.Draw.SolidBox( BBox.FromPositionAndSize( pos.Value, 5 ) );
				}
			}
		}

		if ( ClosestEdge )
		{
			var wall = Scene.NavMesh.GetClosestEdge( WorldPosition );

			if ( wall.HasValue )
			{
				Gizmo.Draw.Color = Color.Yellow;
				Gizmo.Draw.LineThickness = 5;
				Gizmo.Draw.Arrow( WorldPosition, wall.Value );

			}
		}

		if ( Path && Target.IsValid() )
		{
			Gizmo.Draw.Color = Color.Red;
			Gizmo.Draw.LineThickness = 1;
			//DrawArrow( Transform.Position, Target.Transform.Position );

			Gizmo.Draw.LineThickness = 8;
			var triangles = Scene.NavMesh.GetSimplePath( WorldPosition, Target.WorldPosition );

			var up = Vector3.Up * 32.0f;

			for ( int i = 1; i < triangles.Count; i++ )
			{
				Gizmo.Draw.Arrow( up + triangles[i - 1], up + triangles[i] );
			}


		}
	}

}
