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
            bool motorMismatch,
            float totalMass = 0f,
            float maximumMass = 1f,
            float powerDraw = 0f,
            float powerBudget = 1f)
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
            TotalMass = totalMass;
            MaximumMass = maximumMass;
            PowerDraw = powerDraw;
            PowerBudget = powerBudget;
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
        public float TotalMass { get; }
        public float MaximumMass { get; }
        public float PowerDraw { get; }
        public float PowerBudget { get; }
        public float MassRatio => MaximumMass <= 0f ? 0f : TotalMass / MaximumMass;
        public float PowerRatio => PowerBudget <= 0f ? 0f : PowerDraw / PowerBudget;
    }
}
