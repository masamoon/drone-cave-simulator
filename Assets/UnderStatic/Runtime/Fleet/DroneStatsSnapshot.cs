namespace UnderStatic.Fleet
{
    public readonly struct DroneStatsSnapshot
    {
        public DroneStatsSnapshot(
            float speed,
            float endurance,
            float observation,
            float durability,
            float payload,
            float control,
            float noise,
            float reliability,
            int componentValue,
            bool motorMismatch)
        {
            Speed = speed;
            Endurance = endurance;
            Observation = observation;
            Durability = durability;
            Payload = payload;
            Control = control;
            Noise = noise;
            Reliability = reliability;
            ComponentValue = componentValue;
            HasMotorMismatch = motorMismatch;
        }

        public float Speed { get; }
        public float Endurance { get; }
        public float Observation { get; }
        public float Durability { get; }
        public float Payload { get; }
        public float Control { get; }
        public float Noise { get; }
        public float Reliability { get; }
        public int ComponentValue { get; }
        public bool HasMotorMismatch { get; }
    }
}
