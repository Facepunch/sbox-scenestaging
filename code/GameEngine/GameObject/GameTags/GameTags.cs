using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Entity Tags are strings you can set and check for on any entity. Internally
/// these strings are tokenized and networked so they're also available clientside.
/// </summary>
public class GameTags : ITagSet
{
	HashSet<string> collection = new HashSet<string>( StringComparer.OrdinalIgnoreCase );

	GameObject target;

	public Action<string> OnTagAdded { get; set; }
	public Action<string> OnTagRemoved { get; set; }

	internal GameTags( GameObject target )
	{
		this.target = target;
	}

	public override string ToString()
	{
		return string.Join( ", ", collection );
	}

	/// <summary>
	/// Returns all the tags this object has.
	/// </summary>
	public IEnumerable<string> TryGetAll() => collection;

	/// <summary>
	/// Returns true if this object has given tag.
	/// </summary>
	public bool Has( string tag ) => collection.Contains( tag );

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
		OnTagAdded?.Invoke( tag );
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

		OnTagRemoved?.Invoke( tag );
		// on tags changed
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
		if ( Has( tag ) ) Remove( tag );
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
}
