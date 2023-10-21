public enum GameObjectFlags
{
	None = 0,

	/// <summary>
	/// Hide this object in heirachy/inspector
	/// </summary>
	Hidden = 1,

	/// <summary>
	/// Don't save this object to disk, or when duplicating
	/// </summary>
	NotSaved = 2,

	/// <summary>
	/// Auto created - it's a bone
	/// </summary>
	Bone = 4,

	/// <summary>
	/// Auto created - it's an attachment
	/// </summary>
	Attachment = 4,
}


public partial class GameObject
{
	public GameObjectFlags Flags { get; set; } = GameObjectFlags.None;
}
