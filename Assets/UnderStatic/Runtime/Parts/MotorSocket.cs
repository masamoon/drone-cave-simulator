namespace UnderStatic.Parts
{
    // Compatibility adapter for Milestone 1 APIs and tests. Shared behavior lives in PartSocket.
    public sealed class MotorSocket : PartSocket
    {
        public new MotorPart OccupiedPart => base.OccupiedPart as MotorPart;
    }
}
