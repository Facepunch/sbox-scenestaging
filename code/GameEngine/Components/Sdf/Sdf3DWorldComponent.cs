using System.Collections.Generic;
using System.Threading.Tasks;
using Sandbox;
using Sandbox.Sdf;

[Title( "SDF 3D World" )]
public sealed class Sdf3DWorldComponent : BaseComponent, BaseComponent.ExecuteInEditor
{
	public Sdf3DWorld World { get; private set; }

	private bool _brushesInvalid = true;
	private HashSet<Sdf3DBrushComponent> _changes = new();
	private Task _rebuildTask = Task.CompletedTask;
	private int _rebuildCount;

	internal void InvalidateBrush( Sdf3DBrushComponent brush )
	{
		_brushesInvalid = true;
		_changes.Add( brush );
	}

	public override void OnEnabled()
	{
		if ( World is null && Scene.SceneWorld.IsValid() )
		{
			World?.Dispose();
			World = new Sdf3DWorld( Scene.SceneWorld );

			_brushesInvalid = true;
		}
	}

	public override void OnDisabled()
	{
		World?.Dispose();
		World = null;
	}

	public override void Update()
	{
		if ( World is null )
		{
			return;
		}

		if ( _brushesInvalid )
		{
			_brushesInvalid = false;
			_rebuildTask = RebuildFromBrushesAsync( ++_rebuildCount );
		}

		World.Transform = Transform.World;
		World.Update();
	}

	private async Task RebuildFromBrushesAsync( int rebuildCount )
	{
		var lastTask = _rebuildTask ?? Task.CompletedTask;

		if ( !lastTask.IsCompleted )
		{
			await lastTask;
		}

		if ( _rebuildCount != rebuildCount )
		{
			return;
		}

		var brushes = GetComponents<Sdf3DBrushComponent>( true, true )
			.ToArray();

		var modifications = GetComponents<Sdf3DBrushComponent>( true, true )
			.Select( x => x.NextModification )
			.Where( x => x.Resource != null )
			.ToArray();

		var changes = new List<Modification<Sdf3DVolume, ISdf3D>>( _changes.Count * 2 );

		foreach ( var changedBrush in _changes )
		{
			if ( changedBrush.PrevModification.Resource != null )
			{
				changes.Add( changedBrush.PrevModification );
			}

			if ( changedBrush.NextModification.Resource != null )
			{
				changes.Add( changedBrush.NextModification );
			}
		}

		_changes.Clear();

		foreach ( var brush in brushes )
		{
			brush.CommitModification();
		}

		await World.SetModificationsAsync( modifications, changes );
	}
}
