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
[CodeGenerator( CodeGeneratorFlags.Instance | CodeGeneratorFlags.WrapMethod, "RpcWrap" )]
public class BroadcastAttribute : Attribute
{

}
