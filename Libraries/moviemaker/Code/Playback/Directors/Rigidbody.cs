namespace Sandbox.MovieMaker.Controllers;

#nullable enable

internal class RigidbodyDirector : IComponentDirector<Rigidbody>
{
	private bool _wasMotionEnabled;

	public required Rigidbody Component { get; init; }

	void IComponentDirector.Start( MoviePlayer player )
	{
		_wasMotionEnabled = Component.MotionEnabled;

		Component.MotionEnabled = false;
	}

	void IComponentDirector.Stop( MoviePlayer player )
	{
		Component.MotionEnabled = _wasMotionEnabled;
	}
}
