/// <summary>
/// A timed explosive - it explodes after a set time
/// </summary>
public sealed class Explosive : Component, IKillIcon
{
	[Sync]
	public Guid InstigatorId { get; set; }

	/// <summary>
	/// How long does this explosive last until exploding
	/// </summary>
	[Property]
	public float Lifetime { get; set; } = 3f;

	/// <summary>
	/// Damage radius
	/// </summary>
	[Property]
	public float Radius { get; set; }

	/// <summary>
	/// Max damage, respecting <see cref="DamageFalloff"/>
	/// </summary>
	[Property]
	public float MaxDamage { get; set; } = 125;

	/// <summary>
	/// A falloff curve to dictate damage inflicted to damageables in the area.
	/// </summary>
	[Property]
	public Curve DamageFalloff { get; set; } = new Curve( new Curve.Frame( 1.0f, 1.0f ), new Curve.Frame( 0.0f, 0.0f ) );

	/// <summary>
	/// Should this explosive explode?
	/// </summary>
	[Property]
	public bool ShouldExplode { get; set; } = true;

	[Property]
	public float ExtraForce { get; set; } = 0;

	[Property]
	Texture IKillIcon.DisplayIcon { get; set; }

	/// <summary>
	/// How long since this explosive was activated?
	/// </summary>
	public TimeSince TimeSinceActive;

	protected override void OnEnabled()
	{
		TimeSinceActive = 0;
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost )
			return;

		if ( Lifetime > 0.0f && TimeSinceActive > Lifetime )
		{
			Explode();
		}
	}

	[Rpc.Broadcast]
	private void CreateEffects()
	{
		if ( Application.IsDedicatedServer ) return;

		Effects.SpawnExplosion( WorldPosition );
		Effects.SpawnScorch( WorldPosition + Vector3.Up * 5 );
	}

	public void Explode()
	{
		if ( ShouldExplode )
		{
			CreateEffects();
			Damage.Radius( WorldPosition, Radius, MaxDamage, [DamageTags.Explosion], GameObject, GameObject, DamageFalloff, InstigatorId, null, ExtraForce );
			TemporaryEffect.CreateOrphans( GameObject );
		}

		GameObject.Destroy();
	}
}
