public abstract class WareMinigame
{
	public WareRoundSystem System { get; private set; }

	public abstract string Ident { get; }
	public abstract string Title { get; }
	public virtual string Room => "generic";
	public virtual float Windup => 2f;
	public virtual float Duration => 5f;
	public virtual int OccurrencesPerCycle => 1;
	public virtual bool? InitialResult => false;

	protected WareEnvironment Environment => System.Environment;
	protected Scene Scene => System.Scene;

	internal void Bind( WareRoundSystem system )
	{
		System = system;
	}

	public virtual bool CanPlay( IReadOnlyList<Player> players ) => true;
	public virtual void Initialize() { }
	public virtual void StartAction() { }
	public virtual void UpdateAction() { }
	public virtual void EndAction() { }

	protected IEnumerable<Player> Players => System.Players;

	protected void SetInstruction( string instruction )
	{
		System.SetInstruction( instruction );
	}

	protected void SetAchieved( Player player, bool achieved )
	{
		System.SetAchieved( player, achieved );
	}

	protected void LockResult( Player player, bool achieved )
	{
		System.LockResult( player, achieved );
	}

	protected void GiveWeapon( Player player, string prefabPath )
	{
		System.GiveWeapon( player, prefabPath );
	}

	protected GameObject CreateTemporaryObject( string name, Vector3 position )
	{
		return System.CreateTemporaryObject( name, position );
	}
}