using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace VectorBreakout.Rendering;

public readonly struct LineVertex : IVertexType
{
    public readonly Vector3 Position;
    public readonly Color Color;

    public static readonly VertexDeclaration VertexDeclaration = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Color, VertexElementUsage.Color, 0));

    VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;

    public LineVertex(Vector2 position, Color color)
    {
        Position = new Vector3(position, 0f);
        Color = color;
    }
}
