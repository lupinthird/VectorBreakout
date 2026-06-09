using Microsoft.Xna.Framework;

namespace VectorBreakout.Effects;

public sealed class ExplosionManager
{
    private bool _firstBrickExplosionFired;
    private readonly SparkParticleSystem _sparks = new();

    public ExplosionPolicy Policy { get; set; } = ExplosionPolicy.EveryBrick;
    public SparkParticleSystem Sparks => _sparks;

    public void OnBrickImpact(Vector2 position, Vector2 normal, Color brickColor, float ballSpeed, float maxBallSpeed)
    {
        float speedT = maxBallSpeed > 1f ? MathHelper.Clamp(ballSpeed / maxBallSpeed, 0.2f, 1f) : 0.5f;
        int count = brickColor == Color.Red ? 28 : 14;
        _sparks.SpawnImpact(position, normal, brickColor, count, speedScale: 0.55f + speedT * 0.65f);
    }

    public void OnBrickDestroyed(Vector2 position, bool isSpecial)
    {
        bool shouldExplode = Policy switch
        {
            ExplosionPolicy.AlwaysFirstOnly => !_firstBrickExplosionFired,
            ExplosionPolicy.EveryBrick => true,
            ExplosionPolicy.SpecialBricksOnly => isSpecial,
            _ => false,
        };

        if (!shouldExplode)
        {
            return;
        }

        _firstBrickExplosionFired = true;
        _sparks.Spawn(position, 420);
    }

    public void ResetRun()
    {
        _firstBrickExplosionFired = false;
    }
}
