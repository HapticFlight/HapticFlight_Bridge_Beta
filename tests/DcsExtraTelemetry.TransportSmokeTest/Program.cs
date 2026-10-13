using DcsExtraTelemetry.Plugin.Telemetry;
using DcsExtraTelemetry.Plugin.Haptics;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace DcsExtraTelemetry.TransportSmokeTest
{
    internal static class Program
    {
        private const int TestPort = 29382;

        private static int Main()
        {
            try
            {
                RunTest();
                Console.WriteLine("PASS: UDP transport, weapon properties, validation, sequence tracking, and stale detection.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FAIL: " + ex.Message);
                return 1;
            }
        }

        private static void RunTest()
        {
            using (UdpTelemetryReceiver receiver = new UdpTelemetryReceiver(TestPort))
            using (UdpClient sender = new UdpClient())
            {
                Assert(receiver.DataAgeMilliseconds < 0.0, "empty receiver did not report no data");
                Assert(receiver.Current.AircraftFamily == string.Empty, "empty aircraft family was not normalized");
                Assert(receiver.Current.GroundType == string.Empty, "empty ground type was not normalized");
                Assert(receiver.Current.BrakeSource == string.Empty, "empty brake source was not normalized");
                Assert(receiver.Current.LoadoutSummary == string.Empty, "empty loadout summary was not normalized");
                receiver.Start();

                Send(sender, "{\"protocol\":99}");
                Send(sender, LegacyPacket());
                WaitUntil(() => receiver.Current.AircraftType == "LEGACY_AIRCRAFT", 1000);
                Assert(!receiver.Current.HasWeaponPayload, "legacy packet incorrectly enabled weapon payload capability");
                Assert(!receiver.Current.HasWeaponEvents, "legacy packet incorrectly enabled weapon event capability");
                Send(sender, Packet("smoke-a", 10, 0.25, 1.10, -0.50));
                Send(sender, Packet("smoke-a", 12, 0.30, 1.20, -0.40));

                WaitUntil(() => receiver.Current.AircraftType == "TEST_AIRCRAFT"
                    && Math.Abs(receiver.Current.AccelerationXG - 0.30) < 0.0001, 1000);

                Assert(receiver.IsConnected, "receiver did not become connected");
                Assert(receiver.InvalidPackets == 1, "invalid packet count was not updated");
                Assert(receiver.DroppedPackets == 1, "sequence gap was not counted");
                AssertNear(receiver.Current.AccelerationXG, 0.30, "X acceleration");
                AssertNear(receiver.Current.AccelerationYG, 1.20, "Y acceleration");
                AssertNear(receiver.Current.AccelerationZG, -0.40, "Z acceleration");
                AssertNear(receiver.Current.AngularXRadS, 0.01, "X angular velocity");
                AssertNear(receiver.Current.IndicatedAirSpeedMps, 120.0, "indicated airspeed");
                AssertNear(receiver.Current.EngineRpmLeftPct, 82.0, "left engine RPM");
                AssertNear(receiver.Current.GearPosition, 1.0, "gear position");
                Assert(receiver.Current.HasAcceleration, "acceleration capability was not published");
                Assert(receiver.Current.HasAngularVelocity, "angular-velocity capability was not published");
                Assert(receiver.Current.HasAirData, "air-data capability was not published");
                Assert(receiver.Current.HasEngineRpm, "engine RPM capability was not published");
                Assert(receiver.Current.HasGear, "gear capability was not published");
                Assert(receiver.Current.HasFlaps, "flaps capability was not published");
                Assert(receiver.Current.HasSpeedbrake, "speedbrake capability was not published");
                Assert(receiver.Current.HasShakeAmplitude, "shake-amplitude capability was not published");
                Assert(receiver.Current.AircraftFamily == "TEST", "aircraft family did not match");
                Assert(receiver.Current.GroundType == "WHEELS", "ground type did not match");
                AssertNear(receiver.Current.WeaponPayloadCount, 6.0, "weapon payload count");
                AssertNear(receiver.Current.CannonShells, 497.0, "cannon shells");
                AssertNear(receiver.Current.WeaponFirePulse, 1.0, "weapon fire pulse");
                AssertNear(receiver.Current.GunFirePulse, 1.0, "gun fire pulse");
                AssertNear(receiver.Current.PayloadReleasePulse, 0.0, "payload release pulse");
                AssertNear(receiver.Current.GunRoundsFired, 3.0, "gun rounds fired");
                AssertNear(receiver.Current.StationGunFirePulse, 1.0, "station gun fire pulse");
                AssertNear(receiver.Current.CrewGunFirePulse, 1.0, "crew gun fire pulse");
                AssertNear(receiver.Current.LeftCrewGunFirePulse, 1.0, "left crew gun fire pulse");
                AssertNear(receiver.Current.RightCrewGunFirePulse, 0.0, "right crew gun fire pulse");
                AssertNear(receiver.Current.StationGunRoundsFired, 2.0, "station gun rounds fired");
                AssertNear(receiver.Current.CrewGunRoundsFired, 2.0, "crew gun rounds fired");
                AssertNear(receiver.Current.LeftCrewGunRoundsFired, 2.0, "left crew gun rounds fired");
                AssertNear(receiver.Current.RightCrewGunRoundsFired, 0.0, "right crew gun rounds fired");
                AssertNear(receiver.Current.StationGunAmmoRemaining, 15998.0, "station gun ammunition");
                AssertNear(receiver.Current.LeftCrewGunAmmoRemaining, 7998.0, "left crew gun ammunition");
                AssertNear(receiver.Current.RightCrewGunAmmoRemaining, 8000.0, "right crew gun ammunition");
                AssertNear(receiver.Current.LastFiredWeaponStation, 3.0, "last fired weapon station");
                Assert(receiver.Current.LastFiredWeaponName == "UH60L_M134", "last fired weapon name did not match");
                Assert(receiver.Current.WeaponEventCounter == 7, "weapon event counter did not match");
                Assert(receiver.Current.SelectedWeaponName == "TEST_ROCKET", "selected weapon name did not match");
                Assert(receiver.Current.HasWeaponPayload, "weapon payload capability was not published");
                Assert(receiver.Current.HasWeaponEvents, "weapon event capability was not published");
                Assert(receiver.Current.HasStationGuns, "station gun capability was not published");
                Assert(receiver.Current.HasCrewGuns, "crew gun capability was not published");
                AssertNear(receiver.Current.GroundSpeedMps, 12.5, "ground speed");
                AssertNear(receiver.Current.AltitudeAglM, 0.42, "altitude AGL");
                AssertNear(receiver.Current.LeftStrutCompression, 0.55, "left strut compression");
                AssertNear(receiver.Current.RightStrutCompression, 0.57, "right strut compression");
                AssertNear(receiver.Current.TailStrutCompression, 0.30, "tail strut compression");
                AssertNear(receiver.Current.StrutCompressionRate, -0.18, "signed strut compression rate");
                AssertNear(receiver.Current.LeftWheelContact, 1.0, "left wheel contact");
                AssertNear(receiver.Current.TouchdownPulse, 1.0, "touchdown pulse");
                AssertNear(receiver.Current.TouchdownVerticalSpeedMps, 2.8, "touchdown vertical speed");
                AssertNear(receiver.Current.TouchdownStrength, 0.7, "touchdown strength");
                AssertNear(receiver.Current.GroundRollIntensity, 0.45, "ground roll intensity");
                AssertNear(receiver.Current.GroundResonanceIntensity, 0.18, "ground resonance intensity");
                AssertNear(receiver.Current.BrakingIntensity, 0.25, "braking intensity");
                AssertNear(receiver.Current.BrakeDecelerationIntensity, 0.62, "brake deceleration intensity");
                AssertNear(receiver.Current.WheelBrakesPosition, 0.4, "brake pedal position");
                Assert(receiver.Current.BrakeSource == "UH60PedalBrakeArgument", "brake source did not match");
                Assert(receiver.Current.ParkingBrakeOn, "parking brake state was not published");
                Assert(receiver.Current.StrutSource == "LoGetMechInfo", "strut source did not match");
                AssertNear(receiver.Current.ChaffRemaining, 29.0, "chaff remaining");
                AssertNear(receiver.Current.FlareRemaining, 28.0, "flare remaining");
                AssertNear(receiver.Current.FlareReleasePulse, 1.0, "flare release pulse");
                Assert(receiver.Current.MasterArmOn, "master arm state was not published");
                Assert(receiver.Current.WeaponSelectorName == "RKT", "weapon selector name did not match");
                Assert(receiver.Current.RocketPairMode, "rocket pair mode was not published");
                Assert(receiver.Current.HasGearStruts, "gear strut capability was not published");
                Assert(receiver.Current.HasGroundMotion, "ground motion capability was not published");
                Assert(receiver.Current.HasWheelRotation, "wheel rotation capability was not published");
                AssertNear(receiver.Current.SkidFrontLeftCompression, 0.21, "front-left skid compression");
                AssertNear(receiver.Current.SkidRearRightContact, 1.0, "rear-right skid contact");
                Assert(receiver.Current.HasSkids, "skid capability was not published");
                Assert(receiver.Current.HasCountermeasures, "countermeasure capability was not published");
                Assert(receiver.Current.HasCockpitStates, "cockpit state capability was not published");
                AssertNear(receiver.Current.LeftGunnerDoorPosition, 0.8, "left gunner door position");
                AssertNear(receiver.Current.RightCargoDoorPosition, 0.6, "right cargo door position");
                AssertNear(receiver.Current.DoorMovementIntensity, 0.25, "door movement intensity");
                Assert(receiver.Current.HasBrakes, "brake capability was not published");
                Assert(receiver.Current.HasDoors, "door capability was not published");
                Assert(receiver.Current.WeaponClass == "LIGHT_GUN", "weapon class did not match");
                AssertNear(receiver.Current.WeaponRateHz, 48.0, "weapon rate");
                AssertNear(receiver.Current.NominalWeaponRateHz, 100.0, "nominal weapon rate");
                AssertNear(receiver.Current.LightGunIntensity, 0.8, "light gun intensity");
                Assert(receiver.Current.LoadoutSummary == "LIGHT_GUN,ROCKET", "loadout summary did not match");
                AssertNear(receiver.Current.TouchdownEnvelope, 0.7, "touchdown envelope");
                AssertNear(receiver.Current.CombatDamageEnvelope, 0.4, "combat damage envelope");
                AssertNear(receiver.Current.DoorMotionEnvelope, 0.25, "door movement envelope");
                AssertNear(receiver.Current.BrakeEnvelope, 0.25, "brake envelope");
                AssertNear(receiver.Current.WeaponRecoilEnvelope, 0.8, "weapon recoil envelope");
                AssertNear(receiver.Current.MainRotorRpm, 258.0, "main rotor RPM");
                AssertNear(receiver.Current.BladePassHz, 17.2, "blade-pass frequency");
                AssertNear(receiver.Current.CollectivePosition, 0.62, "collective position");
                AssertNear(receiver.Current.TransmissionLoadIntensity, 0.68, "transmission load");
                AssertNear(receiver.Current.EngineGovernorResponseIntensity, 0.21, "governor response intensity");
                AssertNear(receiver.Current.ControlResponseIntensity, 0.19, "control response intensity");
                AssertNear(receiver.Current.ManeuverResponseIntensity, 0.24, "maneuver response intensity");
                AssertNear(receiver.Current.NormalG, 1.24, "normal G");
                AssertNear(receiver.Current.NormalGDeviation, 0.24, "signed normal G deviation");
                AssertNear(receiver.Current.PositiveGIntensity, 0.34, "positive-G intensity");
                AssertNear(receiver.Current.NegativeGIntensity, 0.0, "negative-G intensity");
                AssertNear(receiver.Current.LateralManeuverIntensity, 0.31, "lateral maneuver intensity");
                AssertNear(receiver.Current.VrsRiskIntensity, 0.37, "derived VRS-risk intensity");
                AssertNear(receiver.Current.BladeSlapIntensity, 0.44, "blade slap intensity");
                AssertNear(receiver.Current.SkidScrubIntensity, 0.33, "skid scrub");
                AssertNear(receiver.Current.LiftoffPulse, 1.0, "liftoff pulse");
                Assert(receiver.Current.HasRotorDynamics, "rotor capability was not published");
                Assert(receiver.Current.HasControlPositions, "control-position capability was not published");
                Assert(receiver.Current.HasPowertrainEffects, "powertrain capability was not published");
                Assert(receiver.Current.HasAerodynamicEffects, "aerodynamic capability was not published");
                Assert(receiver.Current.HasBladeSlap, "blade slap capability was not published");
                Assert(receiver.Current.HasDetailedGroundEffects, "detailed ground capability was not published");
                Assert(receiver.Current.HasWeaponEffects, "weapon effects capability was not published");
                Assert(receiver.Current.HasEngineLifecycle, "engine lifecycle capability was not published");
                Assert(!receiver.Current.HasEngineTorque, "unavailable torque was incorrectly published");
                Assert(receiver.Current.HasTorqueGauge, "torque gauge capability was not published");
                AssertNear(receiver.Current.TorqueGaugeNormalized, 0.73, "normalized torque gauge");
                Assert(receiver.Current.HasRotorWarning, "rotor warning capability was not published");
                Assert(receiver.Current.LowRotorRpmWarning, "low rotor warning state was not published");
                Assert(receiver.Current.Haptics.EngineVersion == "1.7.1", "haptic engine version did not match");
                Assert(receiver.Current.Haptics.ModelId == "Generic-v1", "unknown aircraft did not select verified generic haptics");
                Assert(receiver.Current.Haptics.Get(HapticSignalName.Touchdown).Supported,
                    "generic touchdown was not enabled for complete contact and air data");
                Assert(receiver.Current.Haptics.Get(HapticSignalName.Touchdown).EventId == 0,
                    "grounded initialization produced a haptic touchdown event");
                Assert(!receiver.Current.Haptics.Get(HapticSignalName.GroundRoll).Supported,
                    "unknown aircraft invented wheel rolling");
                Assert(!receiver.Current.Haptics.Get(HapticSignalName.GroundBump).Supported,
                    "unknown aircraft invented suspension bumps");
                Assert(!receiver.Current.Haptics.Get(HapticSignalName.Uh60TailwheelSettle).Supported,
                    "unknown aircraft activated a UH-60-only haptic signal");
                Assert(!receiver.Current.Haptics.Get(HapticSignalName.SkidScrub).Supported,
                    "unknown aircraft activated skid-specific haptic interpretation");

                Send(sender, Packet("smoke-a", 13, 0.30, 1.20, -0.40)
                    .Replace("\"weaponFirePulse\":1", "\"weaponFirePulse\":2"));
                WaitUntil(() => receiver.InvalidPackets == 2, 1000);
                Assert(receiver.Current.WeaponFirePulse == 1.0, "invalid weapon pulse replaced the valid snapshot");

                Send(sender, Packet("smoke-b", 0, 0.40, 1.00, 0.10));
                WaitUntil(() => Math.Abs(receiver.Current.AccelerationXG - 0.40) < 0.0001, 1000);
                Assert(receiver.DroppedPackets == 1, "new session was incorrectly counted as packet loss");

                // Let the one-second rate window close, then prove that a
                // short packet gap marks the connection stale without cutting
                // the cached effect snapshot short. A longer hard gap must
                // still clear all outputs.
                Thread.Sleep(1050);
                Send(sender, Packet("smoke-b", 1, 0.40, 1.00, 0.10));
                WaitUntil(() => receiver.PacketRateHz > 0.0, 1000);
                DateTime hapticReceivedAtUtc = receiver.Current.Haptics.ReceivedAtUtc;
                AssertNear(receiver.HapticEventValue(1.0, hapticReceivedAtUtc), 1.0, "fresh haptic event");
                Thread.Sleep(300);
                AssertNear(receiver.HapticEventValue(1.0, hapticReceivedAtUtc), 0.0, "expired haptic event");
                double faded = receiver.HapticContinuousValue(1.0, hapticReceivedAtUtc);
                Assert(faded > 0.0 && faded < 1.0, "continuous haptic value did not fade after 250 ms");
                Thread.Sleep(900);
                Assert(!receiver.IsConnected, "short packet gap remained connected");
                AssertNear(receiver.HapticContinuousValue(1.0, hapticReceivedAtUtc), 0.0, "stale haptic value");
                AssertNear(receiver.PacketRateHz, 0.0, "stale packet rate");
                Assert(receiver.Current.AircraftType == "TEST_AIRCRAFT", "short packet gap discarded the cached aircraft identity");
                AssertNear(receiver.Current.AccelerationXG, 0.40, "short packet gap discarded acceleration");
                AssertNear(receiver.Current.TouchdownPulse, 1.0, "short packet gap discarded touchdown pulse");
                AssertNear(receiver.Current.RotorLoadIntensity, 0.71, "short packet gap discarded rotor load");
                Assert(receiver.Current.HasRotorDynamics, "short packet gap discarded capability flags");

                Thread.Sleep(2100);
                Assert(!receiver.IsConnected, "stale data remained connected");
                AssertNear(receiver.HapticContinuousValue(1.0, hapticReceivedAtUtc), 0.0, "stale haptic value");
                AssertNear(receiver.PacketRateHz, 0.0, "stale packet rate");
                Assert(receiver.Current.AircraftType == string.Empty, "hard stale data retained aircraft identity");
                AssertNear(receiver.Current.AccelerationXG, 0.0, "hard stale acceleration");
                AssertNear(receiver.Current.TouchdownPulse, 0.0, "hard stale touchdown pulse");
                AssertNear(receiver.Current.RotorLoadIntensity, 0.0, "hard stale rotor load");
                Assert(!receiver.Current.HasRotorDynamics, "hard stale capability flags were retained");
            }
        }

        private static string Packet(string sessionId, long sequence, double x, double y, double z)
        {
            object packet = new
            {
                protocol = 1,
                sessionId,
                sequence,
                sentAtMs = 1.0,
                modelTime = 1.0,
                aircraft = new { dcsType = "TEST_AIRCRAFT", family = "TEST", groundType = "WHEELS" },
                capabilities = new
                {
                    acceleration = true,
                    angularVelocity = true,
                    airData = true,
                    engineRpm = true,
                    gear = true,
                    flaps = true,
                    speedbrake = true,
                    shakeAmplitude = true,
                    weaponPayload = true,
                    weaponEvents = true,
                    stationGuns = true,
                    crewGuns = true
                    ,gearStruts = true
                    ,groundMotion = true
                    ,wheelRotation = true
                    ,skids = true
                    ,countermeasures = true
                    ,cockpitStates = true
                    ,rotorDynamics = true
                    ,controlPositions = true
                    ,engineTorque = false
                    ,torqueGauge = true
                    ,rotorWarning = true
                    ,powertrainEffects = true
                    ,aerodynamicEffects = true
                    ,bladeSlap = true
                    ,detailedGroundEffects = true
                    ,weaponEffects = true
                    ,engineLifecycle = true
                    ,brakes = true
                    ,doors = true
                },
                values = new
                {
                    acceleration = new { x, y, z },
                    angularVelocity = new { x = 0.01, y = 0.02, z = 0.03 },
                    indicatedAirSpeedMps = 120.0,
                    trueAirSpeedMps = 125.0,
                    verticalVelocityMps = -2.0,
                    angleOfAttackRad = 0.12,
                    mach = 0.65,
                    shakeAmplitude = 0.15,
                    engineRpmPct = new { left = 82.0, right = 83.0 },
                    weapons = new
                    {
                        selectedStation = 2.0,
                        selectedWeaponName = "TEST_ROCKET",
                        totalPayloadCount = 6.0,
                        cannonShells = 497.0,
                        weaponFirePulse = 1.0,
                        gunFirePulse = 1.0,
                        stationGunFirePulse = 1.0,
                        crewGunFirePulse = 1.0,
                        leftCrewGunFirePulse = 1.0,
                        rightCrewGunFirePulse = 0.0,
                        payloadReleasePulse = 0.0,
                        gunRoundsFired = 3.0,
                        stationGunRoundsFired = 2.0,
                        crewGunRoundsFired = 2.0,
                        leftCrewGunRoundsFired = 2.0,
                        rightCrewGunRoundsFired = 0.0,
                        stationGunAmmoRemaining = 15998.0,
                        leftCrewGunAmmoRemaining = 7998.0,
                        rightCrewGunAmmoRemaining = 8000.0,
                        payloadReleasedCount = 0.0,
                        lastFiredWeaponStation = 3.0,
                        lastFiredWeaponName = "UH60L_M134",
                        weaponEventCounter = 7,
                        weaponClass = "LIGHT_GUN",
                        weaponBurstActive = 1.0,
                        weaponBurstDurationMs = 350.0,
                        weaponRateHz = 48.0,
                        nominalWeaponRateHz = 100.0,
                        weaponImpulseStrength = 0.75,
                        lightGunIntensity = 0.8,
                        heavyGunIntensity = 0.0,
                        rocketLaunchIntensity = 0.0,
                        missileLaunchIntensity = 0.0
                        ,loadoutSummary = "LIGHT_GUN,ROCKET"
                    },
                    ground = new
                    {
                        groundSpeedMps = 12.5,
                        altitudeAglM = 0.42,
                        wheelBrakesPosition = 0.4,
                        brakeSource = "UH60PedalBrakeArgument",
                        parkingBrakeOn = true,
                        tailStrutCompression = 0.30,
                        leftStrutCompression = 0.55,
                        rightStrutCompression = 0.57,
                        strutCompressionRate = -0.18,
                        tailWheelRotation = 0.1,
                        leftWheelRotation = 0.2,
                        rightWheelRotation = 0.3,
                        skidFrontLeftCompression = 0.21,
                        skidFrontRightCompression = 0.22,
                        skidRearLeftCompression = 0.31,
                        skidRearRightCompression = 0.32,
                        leftSkidCompression = 0.26,
                        rightSkidCompression = 0.27,
                        skidFrontLeftContact = 1.0,
                        skidFrontRightContact = 1.0,
                        skidRearLeftContact = 1.0,
                        skidRearRightContact = 1.0,
                        skidFrontLeftContactPulse = 0.0,
                        skidFrontRightContactPulse = 0.0,
                        skidRearLeftContactPulse = 1.0,
                        skidRearRightContactPulse = 1.0,
                        strutSource = "LoGetMechInfo",
                        onGround = 1.0,
                        tailWheelContact = 1.0,
                        leftWheelContact = 1.0,
                        rightWheelContact = 1.0,
                        tailWheelContactPulse = 0.0,
                        leftWheelContactPulse = 1.0,
                        rightWheelContactPulse = 1.0,
                        touchdownPulse = 1.0,
                        touchdownVerticalSpeedMps = 2.8,
                        touchdownStrength = 0.7,
                        suspensionMovementIntensity = 0.2,
                        groundRollIntensity = 0.45,
                        groundResonanceIntensity = 0.18,
                        brakingIntensity = 0.25,
                        brakeDecelerationIntensity = 0.62,
                        groundPitchRockIntensity = 0.12,
                        groundRollRockIntensity = 0.18,
                        longitudinalGroundSpeedMps = 11.8,
                        lateralGroundSlipMps = 1.3,
                        skidScrubIntensity = 0.33,
                        wheelShimmyIntensity = 0.0,
                        tailWheelCasterIntensity = 0.1,
                        liftoffPulse = 1.0,
                        weightOnGroundIntensity = 0.47
                    },
                    countermeasures = new
                    {
                        chaffRemaining = 29.0,
                        flareRemaining = 28.0,
                        chaffReleasePulse = 0.0,
                        flareReleasePulse = 1.0,
                        chaffReleasedCount = 0.0,
                        flareReleasedCount = 2.0
                    },
                    cockpit = new
                    {
                        masterArmOn = true,
                        weaponSelectorPosition = 2.0,
                        weaponSelectorName = "RKT",
                        rocketPairMode = true,
                        rocketRippleMode = false,
                        countermeasureArmed = true,
                        countermeasureMode = 0.5
                        ,doors = new
                        {
                            pilotDoorPosition = 0.1,
                            copilotDoorPosition = 0.2,
                            leftCargoDoorPosition = 0.4,
                            rightCargoDoorPosition = 0.6,
                            leftGunnerDoorPosition = 0.8,
                            rightGunnerDoorPosition = 1.0,
                            doorMovementIntensity = 0.25
                        }
                    },
                    dynamics = new
                    {
                        mainRotorRpmPct = 100.0,
                        mainRotorRpm = 258.0,
                        mainRotorHz = 4.3,
                        bladePassHz = 17.2,
                        rotorSource = "UH60RotorPhase",
                        collectivePosition = 0.62,
                        collectiveMovementRate = 0.15,
                        torqueGaugeNormalized = 0.73,
                        rotorLoadIntensity = 0.71,
                        transmissionLoadIntensity = 0.68,
                        engineGovernorResponseIntensity = 0.21,
                        rotorDroopIntensity = 0.03,
                        rotorSpoolIntensity = 0.04,
                        turbulenceIntensity = 0.22,
                        controlResponseIntensity = 0.19,
                        airframeBuffetIntensity = 0.25,
                        maneuverLoadIntensity = 0.18,
                        normalG = 1.24,
                        normalGDeviation = 0.24,
                        positiveGIntensity = 0.34,
                        negativeGIntensity = 0.0,
                        maneuverResponseIntensity = 0.24,
                        lateralManeuverIntensity = 0.31,
                        settlingWithPowerCue = 0.0,
                        vrsRiskIntensity = 0.37,
                        translationalLiftTransition = 0.52,
                        bladeSlapIntensity = 0.44,
                        hardManeuverPulse = 0.0,
                        airframeImpactPulse = 0.0,
                        rotorRunning = true,
                        apuRunning = false,
                        engine1StarterActive = false,
                        engine2StarterActive = false,
                        lowRotorRpmWarning = true
                    },
                    effects = new
                    {
                        touchdownEnvelope = 0.7,
                        combatDamageEnvelope = 0.4,
                        doorMotionEnvelope = 0.25,
                        brakeEnvelope = 0.25,
                        weaponRecoilEnvelope = 0.8
                    },
                    mechanical = new
                    {
                        gearPosition = 1.0,
                        flapsPosition = 0.5,
                        speedbrakePosition = 0.25
                    }
                }
            };

            return new JavaScriptSerializer().Serialize(packet);
        }

        private static string LegacyPacket()
        {
            return "{\"protocol\":1,\"sessionId\":\"legacy\",\"sequence\":1," +
                "\"sentAtMs\":1,\"modelTime\":1,\"aircraft\":{\"dcsType\":\"LEGACY_AIRCRAFT\"}," +
                "\"capabilities\":{},\"values\":{}}";
        }

        private static void Send(UdpClient sender, string json)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            sender.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Loopback, TestPort));
        }

        private static void WaitUntil(Func<bool> condition, int timeoutMilliseconds)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
            while (!condition() && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(10);
            }

            Assert(condition(), "timed out waiting for receiver state");
        }

        private static void AssertNear(double actual, double expected, string name)
        {
            Assert(Math.Abs(actual - expected) < 0.0001, name + " did not match");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
