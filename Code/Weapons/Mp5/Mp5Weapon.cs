public class Mp5Weapon : IronSightsWeapon
{
	[Property] public float TimeBetweenShots { get; set; } = 0.1f;

	protected override float GetPrimaryFireRate() => TimeBetweenShots;

	public override void PrimaryAttack()
	{
		ShootBullet( TimeBetweenShots, GetBullet() );
	}
}
