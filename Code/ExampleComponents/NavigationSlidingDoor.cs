using System;
using Sandbox;

public sealed class NavigationSlidingDoor : Component
{
	[Property]
	public NavMeshArea Blocker;

	[Property]
	public GameObject DoorModel { get; set; }

	private Vector3 DoorClosedPostion { get; set; }

	private Vector3 DoorOpenPosition { get; set; }

	protected override void OnStart()
	{
		DoorClosedPostion = DoorModel.WorldPosition;
		DoorOpenPosition = DoorModel.WorldPosition + Vector3.Forward * 450f;
	}

	private bool _isOpen = false;
	private TimeSince _timeSinceOpenToggled;


	protected override void OnUpdate()
	{
		var prevOpenState = _isOpen;
		_isOpen = false;
		// check if agent is nearby
		foreach (var agent in Scene.GetAll<NavMeshAgent>())
		{
			if (agent.WorldPosition.Distance(WorldPosition) < 400f)
			{
				_isOpen = true;
			}
		}

		if (prevOpenState != _isOpen)
		{
			_timeSinceOpenToggled = 0;
		}

		if (_timeSinceOpenToggled > 2f)
		{
			if (_isOpen)
			{
				Blocker.Enabled = false;
				DoorModel.WorldPosition = Vector3.Lerp(DoorModel.WorldPosition, DoorOpenPosition, Time.Delta * 5f);
			}
			else
			{
				Blocker.Enabled = true;
				DoorModel.WorldPosition = Vector3.Lerp(DoorModel.WorldPosition, DoorClosedPostion, Time.Delta * 5f);
			}
		}


	}
}