using Sandbox.Rendering;
using Sandbox.Volumes;

namespace Sandbox;

public class BasePostProcess : Component, Component.ExecuteInEditor
{


	internal virtual void Build( PostProcessContext ctx )
	{
		
	}
}


public class BasePostProcess<T> : BasePostProcess where T: BasePostProcess
{
	PostProcessContext? _currentContext;

	PostProcessContext context
	{
		get
		{
			if ( !_currentContext.HasValue ) throw new System.Exception( "Should only be called during build" );
			return _currentContext.Value;
		}
	}

	protected readonly RenderAttributes Attributes = new();
	protected CameraComponent Camera => context.Camera;

	internal override void Build( PostProcessContext ctx )
	{
		_currentContext = ctx;

		try
		{
			// always cleared before build
			Attributes.Clear();
			Render();
		}
		finally
		{
			_currentContext = default;
		}
	}

	public virtual void Render()
	{

	}

	protected void Blit( Material shader, Stage stage, int order )
	{
		Blit( shader, Attributes, stage, order );
	}

	protected void Blit( Material shader, RenderAttributes attr, Stage stage, int order )
	{
		CommandList cl = new CommandList( shader.Name );

		cl.Attributes.GrabFrameTexture( "ColorBuffer", true );
		cl.Blit( shader, attr );

		AddCommandList( cl, Stage.AfterPostProcess, order );
	}

	protected void AddCommandList( CommandList cl, Sandbox.Rendering.Stage stage, int order = 0 )
	{
		context.Add( cl, stage, order );
	}

	protected U GetWeighted<U>( System.Func<T, U> selector, U defaultValue = default, bool onlyLerpBetweenVolumes = false )
	{
		return context.GetBlended( selector, defaultValue, onlyLerpBetweenVolumes );
	}

	protected float GetWeighted( System.Func<T, float> selector )
	{
		return context.GetBlended( selector );
	}
}
