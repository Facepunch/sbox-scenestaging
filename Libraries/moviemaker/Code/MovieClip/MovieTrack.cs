using System;
using System.Text.Json.Nodes;
using Sandbox.Diagnostics;

namespace Sandbox.MovieMaker;

#nullable enable

/// <summary>
/// Represents a property or object being animated by a <see cref="MovieClip"/>.
/// Tracks contain <see cref="MovieBlock"/>s, which are blocks of time for which values or actions are defined.
/// </summary>
public sealed partial class MovieTrack
{
	private MovieClip? _clip;
	private MovieTrack? _parent;
	private bool _cutsInvalid = true;

	private readonly List<MovieTrack> _children = new();

	/// <summary>
	/// List of blocks in this track, ordered by <see cref="MovieBlock.Id"/>.
	/// </summary>
	private readonly List<MovieBlock> _blocks = new();

	private readonly Dictionary<int, MovieBlock> _blockDict = new();

	private readonly List<(MovieTimeRange TimeRange, MovieBlock Block)> _cuts = new();

	/// <summary>
	/// Which clip contains this track.
	/// </summary>
	public MovieClip Clip => _clip ?? throw new Exception( $"{nameof(MovieTrack)} has been removed." );

	/// <summary>
	/// ID for referencing this track. Must be unique in this <see cref="MovieClip"/>.
	/// </summary>
	public Guid Id { get; }

	/// <summary>
	/// Display name of the track, and used when auto-resolving.
	/// </summary>
	public string Name { get; set; }

	/// <summary>
	/// Fully qualified name of the track including parent names, or <see cref="Name"/> if this track has no parent.
	/// </summary>
	public string FullName => Parent is null ? Name : $"{Parent.FullName}.{Name}";

	/// <summary>
	/// What type of property is this track controlling.
	/// </summary>
	public Type PropertyType { get; }

	/// <summary>
	/// Track that this is nested under in the hierarchy.
	/// </summary>
	public MovieTrack? Parent => _parent;

	/// <summary>
	/// False if this track has been removed.
	/// </summary>
	public bool IsValid => _clip is not null;

	/// <summary>
	/// Gets all tracks that are immediate children of this one.
	/// </summary>
	public IReadOnlyList<MovieTrack> Children => _children;

	/// <summary>
	/// Blocks contained in this track.
	/// </summary>
	public IReadOnlyList<MovieBlock> Blocks => _blocks;

	/// <summary>
	/// Maps time ranges to which block is active during that time.
	/// </summary>
	public IReadOnlyList<(MovieTimeRange TimeRange, MovieBlock Block)> Cuts
	{
		get
		{
			UpdateCuts();
			return _cuts;
		}
	}

	public MovieTimeRange TimeRange => Cuts is { Count: > 0 } cuts
		? new MovieTimeRange( cuts[0].TimeRange.Start, cuts[^1].TimeRange.End )
		: default;

	/// <summary>
	/// Editor-only information about this track.
	/// </summary>
	public JsonObject? EditorData { get; set; }

	internal MovieTrack( MovieClip clip, Guid id, string name, Type type, MovieTrack? parent )
	{
		_clip = clip;
		_parent = parent;

		Id = id;
		Name = name;
		PropertyType = type;
	}

	/// <summary>
	/// Should only be called from <see cref="MovieClip.AddTrackInternal"/>.
	/// </summary>
	internal void AddChildInternal( MovieTrack track )
	{
		Assert.AreEqual( track.Parent, this );
		Assert.False( _children.Contains( track ) );

		_children.Add( track );
	}

	/// <summary>
	/// Should only be called from <see cref="MovieClip.RemoveTrackInternal"/>.
	/// </summary>
	internal void RemoveChildInternal( MovieTrack track )
	{
		Assert.AreEqual( track.Parent, this );

		_children.Remove( track );
	}

	internal void BlockChangedInternal( MovieBlock block )
	{
		_cutsInvalid = true;
	}

	public MovieBlock AddBlock( IMovieBlock block ) => AddBlock( block.TimeRange, block.Data );

	public MovieBlock AddBlock( MovieTimeRange timeRange, IMovieBlockData data )
	{
		var nextId = _blocks.Count == 0 ? 1 : _blocks[^1].Id + 1;
		var block = new MovieBlock( this, nextId, timeRange, data );

		AddBlockInternal( block );

		return block;
	}

	private void AddBlockInternal( MovieBlock block )
	{
		_blocks.Add( block );
		_blockDict.Add( block.Id, block );

		BlockChangedInternal( block );
	}

	public MovieBlock? GetBlock( int id ) => _blockDict!.GetValueOrDefault( id );

	/// <summary>
	/// Gets the block that has control at the given <paramref name="time"/>. If multiple
	/// blocks overlap, returns the most recently added.
	/// </summary>
	public MovieBlock? GetBlock( MovieTime time )
	{
		var cuts = Cuts;

		if ( cuts.Count == 0 ) return null;
		if ( cuts[0].TimeRange.Start >= time ) return cuts[0].Block;
		if ( cuts[^1].TimeRange.End <= time ) return cuts[^1].Block;

		// TODO: binary search?

		foreach ( var cut in cuts )
		{
			if ( cut.TimeRange.Contains( time ) )
			{
				return cut.Block;
			}
		}

		return Cuts[^1].Block;
	}

	public bool TryGetValue( MovieTime time, out object? value )
	{
		if ( GetBlock( time ) is not { Data: IMovieBlockValueData data } block )
		{
			value = null;
			return false;
		}

		value = data.GetValue( time - block.Start );
		return true;
	}

	public void RemoveBlocks()
	{
		foreach ( var block in _blocks )
		{
			block.InvalidateInternal();
		}

		_blockDict.Clear();
		_blocks.Clear();
	}

	/// <summary>
	/// Remove this track from the clip. Also removes any child tracks.
	/// </summary>
	public void Remove()
	{
		foreach ( var child in Children.ToArray() )
		{
			child.Remove();
		}

		_clip?.RemoveTrackInternal( this );
		_clip = null;
		_parent = null;
	}

	/// <summary>
	/// Should only be called from <see cref="MovieBlock.Remove"/>
	/// </summary>
	internal void RemoveBlockInternal( MovieBlock block )
	{
		if ( GetBlock( block.Id ) == block )
		{
			BlockChangedInternal( block );

			_blocks.Remove( block );
			_blockDict.Remove( block.Id );
		}
	}

	private void UpdateCuts()
	{
		if ( !_cutsInvalid ) return;

		_cutsInvalid = false;
		_cuts.Clear();

		if ( Blocks is not { Count: >0 } blocks ) return;

		var cutTimes = blocks
			.SelectMany<MovieBlock, MovieTime>( x => [x.TimeRange.Start, x.TimeRange.End] )
			.Distinct()
			.Order()
			.ToArray();

		var prev = cutTimes[0];
		var prevBlock = blocks.LastOrDefault( x => x.TimeRange.Contains( prev ) ) ?? blocks.MinBy( x => x.Start );

		for ( var i = 1; i < cutTimes.Length; i++ )
		{
			var next = cutTimes[i];
			var nextBlock = blocks.LastOrDefault( x => x.TimeRange.Contains( prev ) ) ?? prevBlock;

			if ( nextBlock == prevBlock && _cuts.Count > 0 )
			{
				_cuts[^1] = ((_cuts[^1].TimeRange.Start, next), nextBlock);
			}
			else
			{
				_cuts.Add( ((prev, next), nextBlock) );
			}

			prev = next;
		}
	}

	public IEnumerable<(MovieTimeRange TimeRange, MovieBlock Block)> GetCuts( MovieTimeRange timeRange )
	{
		if ( Cuts is not { Count: > 0 } cuts ) yield break;

		var firstCut = Cuts[0];
		var lastCut = Cuts[^1];

		if ( firstCut.TimeRange.Start > timeRange.Start )
		{
			yield return ((timeRange.Start, MovieTime.Min( timeRange.End, firstCut.TimeRange.Start )), firstCut.Block);
		}

		foreach ( var cut in cuts )
		{
			if ( cut.TimeRange.End <= timeRange.Start ) continue;
			if ( cut.TimeRange.Start >= timeRange.End ) break;

			if ( cut.TimeRange.Intersect( timeRange ) is not { IsEmpty: false } intersection ) continue;

			yield return (intersection, cut.Block);
		}

		if ( lastCut.TimeRange.End < timeRange.End )
		{
			yield return ((MovieTime.Max( timeRange.Start, lastCut.TimeRange.End ), timeRange.End), lastCut.Block);
		}
	}
}
