using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnderStatic.Missions
{
    [Serializable]
    public struct FrontlineHexCoordinate : IEquatable<FrontlineHexCoordinate>
    {
        public int column;
        public int row;

        public FrontlineHexCoordinate(int column, int row)
        {
            this.column = column;
            this.row = row;
        }

        public bool Equals(FrontlineHexCoordinate other) =>
            column == other.column && row == other.row;

        public override bool Equals(object obj) =>
            obj is FrontlineHexCoordinate other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(column, row);

        public override string ToString() => $"{column:00}-{row:00}";

        public static bool operator ==(FrontlineHexCoordinate left, FrontlineHexCoordinate right) =>
            left.Equals(right);

        public static bool operator !=(FrontlineHexCoordinate left, FrontlineHexCoordinate right) =>
            !left.Equals(right);
    }

    public static class FrontlineHexGrid
    {
        private static readonly Vector2Int[] EvenRowOffsets =
        {
            new(-1, 0), new(1, 0), new(-1, -1), new(0, -1), new(-1, 1), new(0, 1)
        };

        private static readonly Vector2Int[] OddRowOffsets =
        {
            new(-1, 0), new(1, 0), new(0, -1), new(1, -1), new(0, 1), new(1, 1)
        };

        public static bool Contains(FrontlineHexCoordinate coordinate, int columns, int rows) =>
            coordinate.column >= 0 && coordinate.column < columns
            && coordinate.row >= 0 && coordinate.row < rows;

        public static IReadOnlyList<FrontlineHexCoordinate> Neighbours(
            FrontlineHexCoordinate coordinate,
            int columns,
            int rows)
        {
            var offsets = (coordinate.row & 1) == 0 ? EvenRowOffsets : OddRowOffsets;
            var result = new List<FrontlineHexCoordinate>(6);
            foreach (var offset in offsets)
            {
                var candidate = new FrontlineHexCoordinate(
                    coordinate.column + offset.x,
                    coordinate.row + offset.y);
                if (Contains(candidate, columns, rows))
                {
                    result.Add(candidate);
                }
            }
            return result;
        }

        public static int Distance(FrontlineHexCoordinate first, FrontlineHexCoordinate second)
        {
            var a = ToAxial(first);
            var b = ToAxial(second);
            return (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.x + a.y - b.x - b.y)
                    + Mathf.Abs(a.y - b.y)) / 2;
        }

        public static Vector2 ToNormalized(
            FrontlineHexCoordinate coordinate,
            int columns,
            int rows)
        {
            var horizontalSlots = columns + 0.5f;
            var x = (coordinate.column + 0.5f + ((coordinate.row & 1) == 1 ? 0.5f : 0f))
                    / horizontalSlots;
            var y = (coordinate.row + 0.5f) / rows;
            return new Vector2(Mathf.Clamp01(x), Mathf.Clamp01(y));
        }

        public static FrontlineHexCoordinate ClosestToNormalized(
            Vector2 normalized,
            int columns,
            int rows)
        {
            var best = new FrontlineHexCoordinate();
            var bestDistance = float.MaxValue;
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var candidate = new FrontlineHexCoordinate(column, row);
                    var distance = Vector2.SqrMagnitude(ToNormalized(candidate, columns, rows) - normalized);
                    if (distance < bestDistance)
                    {
                        best = candidate;
                        bestDistance = distance;
                    }
                }
            }
            return best;
        }

        private static Vector2Int ToAxial(FrontlineHexCoordinate coordinate) => new(
            coordinate.column - (coordinate.row - (coordinate.row & 1)) / 2,
            coordinate.row);
    }
}
