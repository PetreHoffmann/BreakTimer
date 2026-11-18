// Copyright 2026 Petre Hoffmann
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;

namespace BreakTimer.Core.Util;

/// <summary>Rechteck in physikalischen Pixeln (virtueller Desktop, Koordinaten dürfen negativ sein).</summary>
public struct PixelRect : IEquatable<PixelRect>
{
    public PixelRect(int x, int y, int width, int height) { X = x; Y = y; Width = width; Height = height; }
    public int X { get; }
    public int Y { get; }
    public int Width { get; }
    public int Height { get; }
    public int Right => X + Width;
    public int Bottom => Y + Height;
    public int CenterX => X + Width / 2;
    public int CenterY => Y + Height / 2;

    public bool Contains(int x, int y) => x >= X && x < Right && y >= Y && y < Bottom;

    /// <summary>Zentriert ein Fenster der Größe (w,h) in diesem Rechteck und begrenzt es auf dessen Größe.</summary>
    public PixelRect Center(int w, int h)
    {
        w = Math.Min(Math.Max(w, 1), Width);
        h = Math.Min(Math.Max(h, 1), Height);
        return new PixelRect(X + (Width - w) / 2, Y + (Height - h) / 2, w, h);
    }

    public bool Equals(PixelRect o) => X == o.X && Y == o.Y && Width == o.Width && Height == o.Height;
    public override bool Equals(object obj) => obj is PixelRect r && Equals(r);
    public override int GetHashCode() => ((X * 397 ^ Y) * 397 ^ Width) * 397 ^ Height;
    public override string ToString() => $"{X},{Y} {Width}x{Height}";
}
