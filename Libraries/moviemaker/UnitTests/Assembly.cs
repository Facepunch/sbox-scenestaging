global using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sandbox.MovieMaker.Test;

#nullable enable

[TestClass]
public class TestInit
{
	[AssemblyInitialize]
	public static void ClassInitialize( TestContext context )
	{
		Application.InitUnitTest();
	}

	[AssemblyCleanup]
	public static void AssemblyCleanup()
	{
		Application.ShutdownUnitTest();
	}
}
