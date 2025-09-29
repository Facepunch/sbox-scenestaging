using Sandbox.Rendering;
using Sandbox.Volumes;

namespace Sandbox;

public class BasePostProcess : Component, Component.ExecuteInEditor
{
	public virtual void Build( PostProcessContext ctx )
	{
		
	}
}


public class BasePostProcess<T> : BasePostProcess where T: BasePostProcess
{
	public ref struct Context
	{
		internal PostProcessContext ctx;

		public CameraComponent Camera => ctx.Camera;

		public U GetWeighted<U>( System.Func<T, U> selector, U defaultValue = default ) => ctx.GetBlended( selector, defaultValue );
		public float GetWeighted( System.Func<T, float> selector ) => ctx.GetBlended( selector );
		

		public void Add( CommandList cl, Sandbox.Rendering.Stage stage, int order = 0 ) => ctx.Add( cl, stage, order );
	}

	public override void Build( PostProcessContext ctx )
	{
		Context context = new Context() { ctx = ctx };
		Build( context );
	}

	public virtual void Build( Context ctx )
	{

	}
}
