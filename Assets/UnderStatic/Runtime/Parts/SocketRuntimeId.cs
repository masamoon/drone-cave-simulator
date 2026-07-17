using System;

namespace UnderStatic.Parts
{
    [Serializable]
    public struct SocketRuntimeId : IEquatable<SocketRuntimeId>
    {
        public string value;

        public SocketRuntimeId(string runtimeValue)
        {
            value = runtimeValue ?? string.Empty;
        }

        public bool IsEmpty => string.IsNullOrWhiteSpace(value);
        public bool Equals(SocketRuntimeId other) => string.Equals(value, other.value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SocketRuntimeId other && Equals(other);
        public override int GetHashCode() => value == null ? 0 : StringComparer.Ordinal.GetHashCode(value);
        public override string ToString() => value ?? string.Empty;

        public static SocketRuntimeId Compose(string droneInstanceId, string localSocketId) =>
            string.IsNullOrWhiteSpace(droneInstanceId) || string.IsNullOrWhiteSpace(localSocketId)
                ? default
                : new SocketRuntimeId($"{droneInstanceId}::{localSocketId}");

        public static bool operator ==(SocketRuntimeId left, SocketRuntimeId right) => left.Equals(right);
        public static bool operator !=(SocketRuntimeId left, SocketRuntimeId right) => !left.Equals(right);
    }
}
