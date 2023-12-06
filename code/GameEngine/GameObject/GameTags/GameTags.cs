using Sandbox;
using System.Text.RegularExpressions;

/// <summary>
/// Entity Tags are strings you can set and check for on any entity. Internally
/// these strings are tokenized and networked so they're also available clientside.
/// </summary>
public class GameTags : ITagSet
{
	HashSet<string> collection = new HashSet<string>( StringComparer.OrdinalIgnoreCase );

	bool dirty;
	GameObject target;

	internal GameTags( GameObject target )
	{
		this.target = target;
	}

	public override string ToString()
	{
		return string.Join( ", ", TryGetAll() );
	}

	/// <summary>
	/// Returns all the tags this object has.
	/// </summary>
	public IEnumerable<string> TryGetAll()
	{
		if ( target.Parent is null || target.Parent is Scene )
			return collection;

		return collection.Concat( target.Parent.Tags.TryGetAll() ).Distinct();
	}

	/// <summary>
	/// Returns all the tags this object has.
	/// </summary>
	public IEnumerable<string> TryGetAll( bool includeAncestors )
	{
		if ( !includeAncestors ) return collection;
		return TryGetAll();
	}

	/// <summary>
	/// Returns true if this object (or its parents) has given tag.
	/// </summary>
	public bool Has( string tag )
	{
		if ( collection.Contains( tag ) )
			return true;

		return target.Parent?.Tags.Has( tag ) ?? false;
	}

	/// <summary>
	/// Returns true if this object has given tag.
	/// </summary>
	public bool Has( string tag, bool includeAncestors )
	{
		if ( !includeAncestors ) return collection.Contains( tag );
		return Has( tag );
	}

	/// <summary>
	/// Returns true if this object has one or more tags from given tag list.
	/// </summary>
	public bool HasAny( HashSet<string> tagList )
	{
		return tagList.Any( x => Has( x ) );
	}

	/// <summary>
	/// Try to add the tag to this object.
	/// </summary>
	public void Add( string tag )
	{
		if ( string.IsNullOrWhiteSpace( tag ) ) return;
		if ( Has( tag ) ) return;

		if ( tag.Length > 32 )
		{
			Log.Warning( $"Ignoring tag '{tag}' - it's over 32 characters long" );
			return;
		}

		tag = tag.ToLowerInvariant();

		if ( !Regex.IsMatch( tag, "[a-z]{1,32}" ) )
		{
			Log.Warning( $"Ignoring tag '{tag}' - it doesn't match [a-z]" );
			return;
		}

		collection.Add( tag );
		MarkDirty();
	}

	/// <summary>
	/// Adds multiple tags. Calls <see cref="Add(string)">EntityTags.Add</see> for each tag.
	/// </summary>
	public void Add( params string[] tags )
	{
		if ( tags == null || tags.Length == 0 )
			return;

		foreach ( var tag in tags )
			Add( tag );
	}

	/// <summary>
	/// Try to remove the tag from this entity.
	/// </summary>
	public void Remove( string tag )
	{
		if ( !collection.Remove( tag ) )
			return;

		MarkDirty();
	}

	/// <summary>
	/// Removes or adds a tag based on the second argument.
	/// </summary>
	public void Set( string tag, bool on )
	{
		if ( on ) Add( tag );
		else Remove( tag );
	}

	/// <summary>
	/// Removes a tag if it exists, adds it otherwise.
	/// </summary>
	public void Toggle( string tag )
	{
		if ( Has( tag, false ) ) Remove( tag );
		else Add( tag );
	}

	/// <summary>
	/// Remove all tags
	/// </summary>
	public void RemoveAll()
	{
		foreach ( var t in collection.ToArray() )
			Remove( t );
	}

	internal void SetAll( string tags )
	{
		RemoveAll();
		Add( tags.SplitQuotesStrings() );
	}

	void MarkDirty()
	{
		if ( dirty ) return;
		dirty = true;

		// make all our children dirty too
		foreach ( var c in target.Children )
		{
			c.Tags.MarkDirty();
		}
	}

	internal bool PopDirty()
	{
		if ( !dirty ) return false;
		dirty = false;
		return true;
	}
}
