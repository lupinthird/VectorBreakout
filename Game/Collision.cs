using System;
using Microsoft.Xna.Framework;

namespace VectorBreakout.Game;

public static class Collision
{
    public static bool TryBallSegmentCollision(
        Vector2 ballPosition,
        float ballRadius,
        Vector2 a,
        Vector2 b,
        out Vector2 hitPoint,
        out Vector2 normal)
    {
        hitPoint = ClosestPointOnSegment(ballPosition, a, b);
        Vector2 delta = ballPosition - hitPoint;
        float distanceSq = delta.LengthSquared();
        if (distanceSq > ballRadius * ballRadius)
        {
            normal = Vector2.Zero;
            return false;
        }

        if (distanceSq < 0.00001f)
        {
            Vector2 tangent = Vector2.Normalize(b - a);
            normal = new Vector2(-tangent.Y, tangent.X);
        }
        else
        {
            normal = Vector2.Normalize(delta);
        }

        return true;
    }

    public static bool TrySweptBallSegmentCollision(
        Vector2 previousPosition,
        Vector2 currentPosition,
        float ballRadius,
        Vector2 a,
        Vector2 b,
        out Vector2 hitNormal)
    {
        hitNormal = Vector2.Zero;
        Vector2 movement = currentPosition - previousPosition;
        float moveLengthSq = movement.LengthSquared();
        if (moveLengthSq < 0.00001f)
        {
            return TryBallSegmentCollision(currentPosition, ballRadius, a, b, out _, out hitNormal);
        }

        float bestT = 1.1f;
        Vector2 bestNormal = Vector2.Zero;
        int sampleCount = (int)MathHelper.Clamp(movement.Length() / (ballRadius * 0.35f), 2f, 10f);
        for (int i = 0; i <= sampleCount; i++)
        {
            float t = i / (float)sampleCount;
            Vector2 samplePosition = Vector2.Lerp(previousPosition, currentPosition, t);
            if (TryBallSegmentCollision(samplePosition, ballRadius, a, b, out _, out Vector2 normal) && t < bestT)
            {
                bestT = t;
                bestNormal = normal;
            }
        }

        if (bestT > 1f)
        {
            return false;
        }

        hitNormal = bestNormal;
        return true;
    }

    public static float EstimateSweptHitTime(
        Vector2 previousPosition,
        Vector2 currentPosition,
        float ballRadius,
        Vector2 a,
        Vector2 b)
    {
        int sampleCount = 10;
        for (int i = 0; i <= sampleCount; i++)
        {
            float t = i / (float)sampleCount;
            Vector2 samplePosition = Vector2.Lerp(previousPosition, currentPosition, t);
            if (TryBallSegmentCollision(samplePosition, ballRadius, a, b, out _, out _))
            {
                return t;
            }
        }

        return 1f;
    }

    public static Vector2 Reflect(Vector2 velocity, Vector2 normal)
    {
        return Vector2.Reflect(velocity, normal);
    }

    public static Vector2 ClampReflectionAngle(Vector2 velocity, Vector2 referenceDirection, float maxDeflectionRadians)
    {
        if (referenceDirection.LengthSquared() < 0.0001f)
        {
            return velocity;
        }

        referenceDirection = Vector2.Normalize(referenceDirection);
        float refAngle = MathF.Atan2(referenceDirection.Y, referenceDirection.X);
        float speed = velocity.Length();
        if (speed < 0.001f)
        {
            return referenceDirection * 120f;
        }

        float velocityAngle = MathF.Atan2(velocity.Y, velocity.X);
        float delta = WrapToPi(velocityAngle - refAngle);
        delta = MathHelper.Clamp(delta, -maxDeflectionRadians, maxDeflectionRadians);

        float clampedAngle = refAngle + delta;
        return new Vector2(MathF.Cos(clampedAngle), MathF.Sin(clampedAngle)) * speed;
    }

    private static float WrapToPi(float angle)
    {
        while (angle > MathF.PI)
        {
            angle -= MathHelper.TwoPi;
        }

        while (angle < -MathF.PI)
        {
            angle += MathHelper.TwoPi;
        }

        return angle;
    }

    private static Vector2 ClosestPointOnSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float denom = Vector2.Dot(ab, ab);
        if (denom < 0.00001f)
        {
            return a;
        }

        float t = Vector2.Dot(p - a, ab) / denom;
        t = MathHelper.Clamp(t, 0f, 1f);
        return a + (ab * t);
    }
}
