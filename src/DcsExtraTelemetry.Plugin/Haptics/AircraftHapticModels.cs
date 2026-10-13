using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;

namespace DcsExtraTelemetry.Plugin.Haptics
{
    internal enum GroundContactKind
    {
        Unsupported,
        ThreePointWheels,
        FourPointSkids,
        WeightOnSkids,
        VerifiedGeneric
    }

    internal sealed class TouchdownParameters
    {
        public double AirborneArmSeconds { get; set; } = 0.25;
        public double ContactConfirmationSeconds { get; set; } = 0.05;
        public double PersistentSingleContactSeconds { get; set; } = 0.12;
        public double PreImpactHistorySeconds { get; set; } = 0.25;
        public double MinimumSinkRateMps { get; set; } = 0.15;
        public double FullScaleSinkRateMps { get; set; } = 4.0;
        public double BounceRearmSeconds { get; set; } = 0.10;
        public double RetriggerCooldownSeconds { get; set; } = 0.35;
        public double AttackSeconds { get; set; } = 0.015;
        public double HoldSeconds { get; set; } = 0.060;
        public double ReleaseSeconds { get; set; } = 0.240;

        public void Validate()
        {
            Positive(AirborneArmSeconds, "AirborneArmSeconds");
            Positive(ContactConfirmationSeconds, "ContactConfirmationSeconds");
            Positive(PersistentSingleContactSeconds, "PersistentSingleContactSeconds");
            Positive(PreImpactHistorySeconds, "PreImpactHistorySeconds");
            NonNegative(MinimumSinkRateMps, "MinimumSinkRateMps");
            if (FullScaleSinkRateMps <= MinimumSinkRateMps || FullScaleSinkRateMps > 20.0)
                throw new InvalidOperationException("FullScaleSinkRateMps is outside its valid range.");
            Positive(BounceRearmSeconds, "BounceRearmSeconds");
            Positive(RetriggerCooldownSeconds, "RetriggerCooldownSeconds");
            Positive(AttackSeconds, "AttackSeconds");
            NonNegative(HoldSeconds, "HoldSeconds");
            Positive(ReleaseSeconds, "ReleaseSeconds");
        }

        private static void Positive(double value, string name)
        {
            if (value <= 0.0 || value > 10.0 || double.IsNaN(value) || double.IsInfinity(value))
                throw new InvalidOperationException(name + " is outside its valid range.");
        }

        private static void NonNegative(double value, string name)
        {
            if (value < 0.0 || value > 20.0 || double.IsNaN(value) || double.IsInfinity(value))
                throw new InvalidOperationException(name + " is outside its valid range.");
        }
    }

    internal interface IAircraftHapticModel
    {
        string Id { get; }
        string Version { get; }
        string Family { get; }
        GroundContactKind GroundContactKind { get; }
        HapticSourceConfidence Confidence { get; }
        bool Provisional { get; }
        TouchdownParameters Touchdown { get; }
        bool MatchesExactType(string aircraftType);
    }

    internal sealed class AircraftHapticModel : IAircraftHapticModel
    {
        private readonly HashSet<string> aircraftTypes;

        public AircraftHapticModel(
            string id,
            string version,
            string family,
            GroundContactKind groundContactKind,
            HapticSourceConfidence confidence,
            bool provisional,
            TouchdownParameters touchdown,
            params string[] aircraftTypes)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Model id is required.", "id");
            if (string.IsNullOrWhiteSpace(version)) throw new ArgumentException("Model version is required.", "version");
            if (touchdown == null) throw new ArgumentNullException("touchdown");
            touchdown.Validate();
            Id = id;
            Version = version;
            Family = family ?? string.Empty;
            GroundContactKind = groundContactKind;
            Confidence = confidence;
            Provisional = provisional;
            Touchdown = touchdown;
            this.aircraftTypes = new HashSet<string>(aircraftTypes ?? new string[0], StringComparer.OrdinalIgnoreCase);
        }

        public string Id { get; private set; }
        public string Version { get; private set; }
        public string Family { get; private set; }
        public GroundContactKind GroundContactKind { get; private set; }
        public HapticSourceConfidence Confidence { get; private set; }
        public bool Provisional { get; private set; }
        public TouchdownParameters Touchdown { get; private set; }
        public bool MatchesExactType(string aircraftType) { return aircraftTypes.Contains(aircraftType ?? string.Empty); }
    }

    internal sealed class AircraftHapticModelCatalog
    {
        private AircraftHapticModel[] models;
        private readonly AircraftHapticModel generic;

        public AircraftHapticModelCatalog()
        {
            generic = new AircraftHapticModel("Generic-v1", "1.0.0", "OTHER",
                GroundContactKind.VerifiedGeneric, HapticSourceConfidence.DerivedVerified,
                false, new TouchdownParameters());
            Initialize(ReadEmbeddedDefinitions);
        }

        internal AircraftHapticModelCatalog(string definitionsJson)
        {
            generic = new AircraftHapticModel("Generic-v1", "1.0.0", "OTHER",
                GroundContactKind.VerifiedGeneric, HapticSourceConfidence.DerivedVerified,
                false, new TouchdownParameters());
            Initialize(() => definitionsJson);
        }

        private void Initialize(Func<string> readDefinitions)
        {
            try
            {
                models = LoadDefinitions(readDefinitions());
                Status = "Loaded " + models.Length + " validated aircraft models";
            }
            catch (Exception ex)
            {
                models = new AircraftHapticModel[0];
                Status = "Definitions invalid; verified generic model only: " + ex.GetType().Name + ": " + ex.Message;
            }
        }

        public string Status { get; private set; }

        public IAircraftHapticModel Select(string aircraftType, string family)
        {
            for (int index = 0; index < models.Length; index++)
            {
                if (models[index].MatchesExactType(aircraftType)) return models[index];
            }
            for (int index = 0; index < models.Length; index++)
            {
                if (string.Equals(models[index].Family, family, StringComparison.OrdinalIgnoreCase)) return models[index];
            }
            return generic;
        }

        private static string ReadEmbeddedDefinitions()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith("aircraft-haptic-models.json", StringComparison.OrdinalIgnoreCase));
            if (resourceName == null) throw new InvalidOperationException("Aircraft haptic model resource is missing.");
            string json;
            using (System.IO.Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (System.IO.StreamReader reader = new System.IO.StreamReader(stream)) { json = reader.ReadToEnd(); }
            return json;
        }

        private static AircraftHapticModel[] LoadDefinitions(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new InvalidOperationException("Aircraft haptic model definitions are empty.");
            AircraftHapticModelDefinition[] definitions = new JavaScriptSerializer().Deserialize<AircraftHapticModelDefinition[]>(json);
            if (definitions == null || definitions.Length == 0) throw new InvalidOperationException("No aircraft haptic models were defined.");
            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> families = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> aircraftTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AircraftHapticModel[] loaded = new AircraftHapticModel[definitions.Length];
            for (int index = 0; index < definitions.Length; index++)
            {
                AircraftHapticModelDefinition definition = definitions[index];
                if (definition == null || !ids.Add(definition.Id ?? string.Empty))
                    throw new InvalidOperationException("Aircraft model ids must be non-empty and unique.");
                if (string.IsNullOrWhiteSpace(definition.Version)
                    || !families.Add(definition.Family ?? string.Empty)
                    || definition.AircraftTypes == null || definition.AircraftTypes.Length == 0)
                    throw new InvalidOperationException("Aircraft model version, family, and exact types are required and must be unique.");
                for (int typeIndex = 0; typeIndex < definition.AircraftTypes.Length; typeIndex++)
                {
                    if (string.IsNullOrWhiteSpace(definition.AircraftTypes[typeIndex])
                        || !aircraftTypes.Add(definition.AircraftTypes[typeIndex]))
                        throw new InvalidOperationException("Aircraft exact types must be non-empty and unique.");
                }
                GroundContactKind contactKind;
                HapticSourceConfidence confidence;
                if (!Enum.TryParse(definition.GroundContactKind, true, out contactKind)
                    || !Enum.TryParse(definition.Confidence, true, out confidence))
                    throw new InvalidOperationException("Aircraft model enum value is invalid: " + definition.Id);
                loaded[index] = new AircraftHapticModel(definition.Id, definition.Version, definition.Family,
                    contactKind, confidence, definition.Provisional, definition.Touchdown ?? new TouchdownParameters(),
                    definition.AircraftTypes ?? new string[0]);
            }
            return loaded;
        }
    }

    internal sealed class AircraftHapticModelDefinition
    {
        public string Id { get; set; }
        public string Version { get; set; }
        public string Family { get; set; }
        public string GroundContactKind { get; set; }
        public string Confidence { get; set; }
        public bool Provisional { get; set; }
        public string[] AircraftTypes { get; set; }
        public TouchdownParameters Touchdown { get; set; }
    }
}
