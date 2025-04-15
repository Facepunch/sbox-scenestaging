using System.Collections.Immutable;
using System.Linq;
using Editor.MapEditor;
using Editor.ShaderGraph.Nodes;
using Sandbox.MovieMaker;
using Sandbox.Utility;

namespace Editor.MovieMaker;

#nullable enable

partial class MotionEditMode
{
	protected override ISnapshot OnSnapshot() => new Snapshot( TimeSelection, Modification?.Snapshot() );

	protected override void OnRestore( ISnapshot snapshot )
	{
		if ( snapshot is not Snapshot data ) return;

		_timeSelection = data.Selection;

		if ( data is { Modification: { } modification, Selection: { } selection } )
		{
			SetModification( modification.Type, selection )
				.Restore( modification );
		}
		else
		{
			ClearChanges();
		}

		SelectionChanged();
	}
}

file sealed record Snapshot( TimeSelection? Selection, ModificationSnapshot? Modification ) : EditMode.ISnapshot;
