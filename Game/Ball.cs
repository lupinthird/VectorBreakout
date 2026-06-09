using Microsoft.Xna.Framework;

namespace VectorBreakout.Game;

public sealed class Ball
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float Radius;

    public Ball(Vector2 position, Vector2 velocity, float radius)
    {
        Position = position;
        Velocity = velocity;
        Radius = radius;
    }

    public void Update(float dt)
    {
        Position += Velocity * dt;
    }

    public void Reset(Vector2 position, Vector2 velocity)
    {
        Position = position;
        Velocity = velocity;
    }
}
