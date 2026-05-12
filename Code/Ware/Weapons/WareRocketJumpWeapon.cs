public sealed class WareRocketJumpWeapon : BaseWeapon
{
	private static readonly Vector3 ProjectileColliderStart = new( -8f, 0f, 0f );
	private static readonly Vector3 ProjectileColliderEnd = new( 8f, 0f, 0f );
	private const float ProjectileColliderRadius = 3f;

	[Property] public SoundEvent ShootSound { get; set; }
	[Property] public float TimeBetweenShots { get; set; } = 0.75f;
	[Property] public float ProjectileForce { get; set; } = 5000000f;
	[Property] public string ProjectileModel { get; set; } = "models/weapons/sbox_ammo/84mm_missile/84mm_missile.vmdl";
	[Property] public bool SingleUse { get; set; }

	private bool _hasFired;

	protected override float GetPrimaryFireRate() => TimeBetweenShots;
	public override bool CanSecondaryAttack() => false;

	public override bool CanPrimaryAttack()
	{
		if ( SingleUse && _hasFired ) return false;
		return base.CanPrimaryAttack();
	}

	public override void PrimaryAttack()
	{
		if ( SingleUse && _hasFired ) return;

		AddShootDelay( TimeBetweenShots );
		_hasFired = true;

		if ( ViewModel.IsValid() )
			ViewModel.RunEvent<ViewModel>( x => x.OnAttack() );
		else if ( WorldModel.IsValid() )
			WorldModel.RunEvent<WorldModel>( x => x.OnAttack() );

		if ( ShootSound.IsValid() )
			GameObject.PlaySound( ShootSound );

		var ray = AimRay;
		CreateProjectile( ray.Position, ray.Forward, ProjectileForce );
	}

	[Rpc.Host]
	private void CreateProjectile( Vector3 start, Vector3 direction, float force )
	{
		var go = WareProjectileFactory.CreateAnimatedTriggerProjectile( "swent_rocketjump", start, direction, ProjectileModel, ProjectileColliderStart, ProjectileColliderEnd, ProjectileColliderRadius );
		var projectile = go.Components.Create<WareRocketJumpProjectile>();
		projectile.Instigator = Owner;
		go.NetworkSpawn();
		projectile.Rigidbody.ApplyImpulse( direction.Normal * force );
	}
}