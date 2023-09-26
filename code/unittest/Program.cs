global using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace unittest
{
	[TestClass]
	public static class Program
	{
		[AssemblyInitialize]
		public static void MyTestInitialize( TestContext testContext )
		{
			Sandbox.Application.InitUnitTest();
		}
	}
}
