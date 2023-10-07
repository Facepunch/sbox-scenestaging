using Sandbox;
using Sandbox.Diagnostics;
using Sandbox.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Numerics;
using System.Text.Json.Nodes;

public static class Physics
{
	public static PhysicsTraceBuilder Trace => GameManager.ActiveScene.PhysicsWorld.Trace;
}
