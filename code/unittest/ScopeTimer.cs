using System.Diagnostics;

namespace MyTests;

public class ScopeTimer
{
	private readonly Stopwatch stopwatch = new Stopwatch();
	string Name { get; set; }

	public ScopeTimer( string name )
	{
		Name = name;
		stopwatch.Start();
	}

	public void Dispose()
	{
		stopwatch.Stop();
		System.Console.WriteLine($"{Name} took {stopwatch.ElapsedMilliseconds} ms");
	}
}
