using System;
using System.Diagnostics;

public class ScopeTimer : IDisposable
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
		System.Console.WriteLine($"{Name} took {stopwatch.Elapsed.TotalMilliseconds} ms");
	}
}
