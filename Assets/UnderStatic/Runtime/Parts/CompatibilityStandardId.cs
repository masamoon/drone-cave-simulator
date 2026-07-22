using System;
using System.Collections.Generic;
using System.Linq;

namespace UnderStatic.Parts
{
    [Serializable]
    public struct CompatibilityStandardId : IEquatable<CompatibilityStandardId>
    {
        public string value;

        public CompatibilityStandardId(string standardValue)
        {
            value = standardValue ?? string.Empty;
        }

        public bool IsEmpty => string.IsNullOrWhiteSpace(value);

        public bool Equals(CompatibilityStandardId other) =>
            string.Equals(value, other.value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is CompatibilityStandardId other && Equals(other);
        public override int GetHashCode() => value == null ? 0 : StringComparer.Ordinal.GetHashCode(value);
        public override string ToString() => value ?? string.Empty;

        public static bool operator ==(CompatibilityStandardId left, CompatibilityStandardId right) => left.Equals(right);
        public static bool operator !=(CompatibilityStandardId left, CompatibilityStandardId right) => !left.Equals(right);

        public static readonly CompatibilityStandardId CompactMotor = new("compact.motor");
        public static readonly CompatibilityStandardId CompactBattery = new("compact.battery");
        public static readonly CompatibilityStandardId CompactPropeller = new("compact.propeller");
        public static readonly CompatibilityStandardId SurveyMotor = new("survey.motor");
        public static readonly CompatibilityStandardId SurveyBattery = new("survey.battery");
        public static readonly CompatibilityStandardId SurveyPropeller = new("survey.propeller");
        public static readonly CompatibilityStandardId HeavyMotor = new("heavy.motor");
        public static readonly CompatibilityStandardId HeavyBattery = new("heavy.battery");
        public static readonly CompatibilityStandardId HeavyPropeller = new("heavy.propeller");
        public static readonly CompatibilityStandardId SharedCamera = new("utility.camera.shared");
        public static readonly CompatibilityStandardId SharedAntenna = new("utility.antenna.shared");
        public static readonly CompatibilityStandardId SharedStrikeRack = new("utility.strike-rack.shared");
        public static readonly CompatibilityStandardId SharedPayload = new("utility.payload.sealed");
        public static readonly CompatibilityStandardId SharedEsc = new("electronics.esc.30x30");
        public static readonly CompatibilityStandardId SharedFlightController = new("electronics.fc.30x30");

        public static CompatibilityStandardId FromLegacyTag(string legacyTag)
        {
            return legacyTag switch
            {
                "motor.standard" => CompactMotor,
                "battery.slide" or "battery.slide-4s" => CompactBattery,
                "propeller.quicklock" => CompactPropeller,
                "camera.rail" or "camera.micro-bracket" => SharedCamera,
                "antenna.thread" or "antenna.keyed-connector" => SharedAntenna,
                "strike-rack.rail" => SharedStrikeRack,
                "payload.sealed" => SharedPayload,
                "electronics.esc.30x30" => SharedEsc,
                "electronics.fc.30x30" => SharedFlightController,
                _ => string.IsNullOrWhiteSpace(legacyTag)
                    ? default
                    : new CompatibilityStandardId($"legacy.{legacyTag}")
            };
        }

        public static CompatibilityStandardId[] Migrate(IEnumerable<string> legacyTags) =>
            legacyTags?.Select(FromLegacyTag).Where(id => !id.IsEmpty).Distinct().ToArray()
            ?? Array.Empty<CompatibilityStandardId>();
    }

    public enum EquipmentGrade
    {
        Field,
        Professional
    }

    [Flags]
    public enum PartMissionCapability
    {
        None = 0,
        Observation = 1 << 0,
        Communications = 1 << 1,
        KamikazeWarhead = 1 << 3,
        GrenadeDrop = 1 << 4
    }

    [Serializable]
    public struct PartStatModifiers
    {
        public float speed;
        public float endurance;
        public float observation;
        public float control;
        public float durability;
        public float payload;
        public float noise;
        public float reliability;
    }
}
