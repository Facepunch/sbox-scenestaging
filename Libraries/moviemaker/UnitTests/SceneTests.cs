using System;

namespace Sandbox.MovieMaker.Test;

#nullable enable

public abstract class SceneTests
{
	private IDisposable? _sceneScope;

	[TestInitialize]
	public void TestInitialize()
	{
		_sceneScope = new Scene().Push();
	}

	[TestCleanup]
	public void TestCleanup()
	{
		_sceneScope?.Dispose();
	}
}
