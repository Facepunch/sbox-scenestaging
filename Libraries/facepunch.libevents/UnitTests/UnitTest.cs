global using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using Sandbox.Internal;

namespace Sandbox.Events.Tests;

[TestClass]
public class TestInit
{
	[AssemblyInitialize]
	public static void ClassInitialize( TestContext context )
	{
		Application.InitUnitTest();

		var addAssemblyMethod = typeof(TypeLibrary)
			.GetMethod( "AddAssembly", BindingFlags.NonPublic | BindingFlags.Instance, new[] { typeof(Assembly), typeof(bool) } )!;

		addAssemblyMethod.Invoke( GlobalGameNamespace.TypeLibrary, new object?[] { Assembly.GetExecutingAssembly(), true } );
	}
}
