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
        PrecisionStrike = 1 << 2
    }

    [Serializable]
    public struct PartStatModifiers
    {
        public float endurance;
        public float observation;
        public float control;
        public float durability;
        public float payload;
        public float noise;
        public float reliability;
    }
}
