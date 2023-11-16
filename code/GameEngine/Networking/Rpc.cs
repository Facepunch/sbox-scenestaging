using Sandbox;
using Sandbox.Network;
using Sandbox.Utility;
using System.Runtime;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Sandbox;

public class BaseRpcAttribute : System.Attribute
{

}


[AttributeUsage( AttributeTargets.Method )]
[CodeGenerator( CodeGeneratorFlags.Instance | CodeGeneratorFlags.WrapMethod, "__rpc_Broadcast" )]
public class BroadcastAttribute : Attribute
{

}
