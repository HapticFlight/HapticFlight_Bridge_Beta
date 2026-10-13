using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using DcsExtraTelemetry.Plugin.Haptics;

namespace DcsExtraTelemetry.Plugin.Telemetry
{
    internal sealed class UdpTelemetryReceiver : IDisposable
    {
        public const int DefaultPort = 29381;
        public const int RecorderPort = 29382;
        private const int MaximumPacketBytes = 8192;
        private const int StaleAfterMilliseconds = 1000;
        private const int OutputZeroAfterMilliseconds = 3000;

        private readonly int port;
        private readonly JavaScriptSerializer serializer;
        private readonly object lifecycleLock;
        private readonly Action<string> errorLogger;
        private readonly HapticStateEngine hapticEngine;

        private UdpClient udpClient;
        private Thread receiveThread;
        private TelemetrySnapshot current;
        private int stopping;
        private long invalidPackets;
        private long droppedPackets;
        private long lastPacketTimestamp;
        private long lastPacketErrorLogTimestamp;
        private long lastSequence;
        private bool hasSequence;
        private string lastSessionId;
        private long rateWindowStartedTimestamp;
        private int packetsInRateWindow;
        private double packetRateHz;

        public UdpTelemetryReceiver(int port, Action<string> errorLogger = null)
        {
            this.port = port;
            this.errorLogger = errorLogger;
            serializer = new JavaScriptSerializer { MaxJsonLength = MaximumPacketBytes };
            lifecycleLock = new object();
            hapticEngine = new HapticStateEngine();
            current = TelemetrySnapshot.Empty;
            rateWindowStartedTimestamp = Stopwatch.GetTimestamp();
        }

        public TelemetrySnapshot Current
        {
            get
            {
                // Allow a short packet-jitter grace period so normal UDP gaps do
                // not cut effect bursts short. A hard disconnect still clears
                // all outputs after the longer safety timeout.
                return HasOutputGrace
                    ? Interlocked.CompareExchange(ref current, null, null)
                    : TelemetrySnapshot.Empty;
            }
        }

        public bool IsConnected
        {
            get
            {
                return Volatile.Read(ref lastPacketTimestamp) != 0
                    && GetDataAgeMilliseconds() < StaleAfterMilliseconds;
            }
        }

        public double DataAgeMilliseconds
        {
            get
            {
                return Volatile.Read(ref lastPacketTimestamp) == 0
                    ? -1.0
                    : GetDataAgeMilliseconds();
            }
        }

        public double PacketRateHz
        {
            get { return IsConnected ? Current.PacketRateHz : 0.0; }
        }

        private bool HasOutputGrace
        {
            get
            {
                return Volatile.Read(ref lastPacketTimestamp) != 0
                    && GetDataAgeMilliseconds() < OutputZeroAfterMilliseconds;
            }
        }

        public long InvalidPackets
        {
            get { return Interlocked.Read(ref invalidPackets); }
        }

        public long DroppedPackets
        {
            get { return Interlocked.Read(ref droppedPackets); }
        }

        public event Action<string, TelemetrySnapshot> PacketReceived;

        public double HapticEventValue(double value, DateTime hapticReceivedAtUtc)
        {
            if (!IsConnected || hapticReceivedAtUtc == DateTime.MinValue) return 0.0;
            double age = (DateTime.UtcNow - hapticReceivedAtUtc).TotalMilliseconds;
            return age >= 0.0 && age <= 250.0 ? HapticMath.Clamp01(value) : 0.0;
        }

        public double HapticContinuousValue(double value, DateTime hapticReceivedAtUtc)
        {
            if (!IsConnected || hapticReceivedAtUtc == DateTime.MinValue) return 0.0;
            double age = (DateTime.UtcNow - hapticReceivedAtUtc).TotalMilliseconds;
            if (age <= 250.0) return HapticMath.Clamp01(value);
            if (age >= StaleAfterMilliseconds) return 0.0;
            return HapticMath.Clamp01(value) * (StaleAfterMilliseconds - age)
                / (StaleAfterMilliseconds - 250.0);
        }

        public void Start()
        {
            lock (lifecycleLock)
            {
                if (receiveThread != null)
                {
                    return;
                }

                stopping = 0;
                udpClient = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
                receiveThread = new Thread(ReceiveLoop)
                {
                    IsBackground = true,
                    Name = "DCS Extra Telemetry UDP Receiver"
                };
                receiveThread.Start();
            }
        }

        public void Dispose()
        {
            Thread threadToJoin;

            lock (lifecycleLock)
            {
                Interlocked.Exchange(ref stopping, 1);

                if (udpClient != null)
                {
                    udpClient.Close();
                    udpClient = null;
                }

                threadToJoin = receiveThread;
                receiveThread = null;
                hapticEngine.Reset(HapticResetReason.Shutdown);
            }

            if (threadToJoin != null && threadToJoin != Thread.CurrentThread)
            {
                threadToJoin.Join(500);
            }
        }

        private void ReceiveLoop()
        {
            UdpClient localClient = udpClient;

            try
            {
                while (Volatile.Read(ref stopping) == 0)
                {
                    IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                    byte[] packetBytes = localClient.Receive(ref remote);

                    if (!IPAddress.IsLoopback(remote.Address)
                        || packetBytes.Length == 0
                        || packetBytes.Length > MaximumPacketBytes)
                    {
                        Interlocked.Increment(ref invalidPackets);
                        continue;
                    }

                    ProcessPacket(packetBytes);
                }
            }
            catch (ObjectDisposedException)
            {
                // Expected during shutdown.
            }
            catch (SocketException ex)
            {
                if (Volatile.Read(ref stopping) == 0)
                {
                    LogError("[DCS Extra Telemetry] UDP receiver stopped: " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                if (Volatile.Read(ref stopping) == 0)
                {
                    LogError("[DCS Extra Telemetry] Receiver failure: " + ex);
                }
            }
        }

        private void ProcessPacket(byte[] packetBytes)
        {
            try
            {
                string json = Encoding.UTF8.GetString(packetBytes);
                TelemetryPacket packet = serializer.Deserialize<TelemetryPacket>(json);

                if (!IsValid(packet))
                {
                    Interlocked.Increment(ref invalidPackets);
                    LogPacketError("Rejected invalid telemetry packet.");
                    return;
                }

                UpdateSequenceStatistics(packet.sessionId, packet.sequence);
                UpdateRateStatistics();

                DateTime receivedAtUtc = DateTime.UtcNow;
                HapticStateSnapshot haptics = hapticEngine.Update(packet, receivedAtUtc);

                TelemetrySnapshot snapshot = TelemetrySnapshot.FromPacket(
                    packet,
                    receivedAtUtc,
                    packetRateHz,
                    Interlocked.Read(ref invalidPackets),
                    Interlocked.Read(ref droppedPackets),
                    haptics);

                Interlocked.Exchange(ref lastPacketTimestamp, Stopwatch.GetTimestamp());
                Interlocked.Exchange(ref current, snapshot);
                RaisePacketReceived(json, snapshot);
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref invalidPackets);
                LogPacketError("Packet parse failed: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static bool IsValid(TelemetryPacket packet)
        {
            if (packet == null
                || packet.protocol != 1
                || string.IsNullOrWhiteSpace(packet.sessionId)
                || packet.sessionId.Length > 128
                || packet.sequence < 0
                || packet.values == null
                || packet.capabilities == null)
            {
                return false;
            }

            TelemetryValues values = packet.values;
            TelemetryCapabilities capabilities = packet.capabilities;

            return IsValidVector(values.acceleration)
                && IsValidVector(values.angularVelocity)
                && IsValidNumber(values.indicatedAirSpeedMps)
                && IsValidNumber(values.trueAirSpeedMps)
                && IsValidNumber(values.verticalVelocityMps)
                && IsValidNumber(values.angleOfAttackRad)
                && IsValidNumber(values.mach)
                && IsValidNumber(values.shakeAmplitude)
                && IsValidPair(values.engineRpmPct)
                && IsValidMechanical(values.mechanical)
                && IsValidWeapons(values.weapons)
                && IsValidGround(values.ground)
                && IsValidCountermeasures(values.countermeasures)
                && IsValidCockpit(values.cockpit)
                && IsValidDynamics(values.dynamics)
                && IsValidEffects(values.effects)
                && (!capabilities.acceleration || IsCompleteVector(values.acceleration))
                && (!capabilities.angularVelocity || IsCompleteVector(values.angularVelocity))
                && (!capabilities.airData || HasCompleteAirData(values))
                && (!capabilities.engineRpm || IsCompletePair(values.engineRpmPct))
                && (!capabilities.gear || HasValue(values.mechanical == null ? null : values.mechanical.gearPosition))
                && (!capabilities.flaps || HasValue(values.mechanical == null ? null : values.mechanical.flapsPosition))
                && (!capabilities.speedbrake || HasValue(values.mechanical == null ? null : values.mechanical.speedbrakePosition))
                && (!capabilities.shakeAmplitude || HasValue(values.shakeAmplitude))
                && (!capabilities.weaponPayload || HasCompleteWeaponPayload(values.weapons))
                && (!capabilities.weaponEvents || HasCompleteWeaponEvents(values.weapons))
                && (!capabilities.stationGuns || HasCompleteStationGuns(values.weapons))
                && (!capabilities.crewGuns || HasCompleteCrewGuns(values.weapons))
                && (!capabilities.gearStruts || HasCompleteGearStruts(values.ground))
                && (!capabilities.groundMotion || HasCompleteGroundMotion(values.ground))
                && (!capabilities.wheelRotation || HasCompleteWheelRotation(values.ground))
                && (!capabilities.skids || HasCompleteSkids(values.ground))
                && (!capabilities.countermeasures || HasCompleteCountermeasures(values.countermeasures))
                && (!capabilities.cockpitStates || HasCompleteCockpit(values.cockpit))
                && (!capabilities.rotorDynamics || HasCompleteRotorDynamics(values.dynamics))
                && (!capabilities.controlPositions || HasCompleteControlPositions(values.dynamics))
                && (!capabilities.engineTorque || HasCompleteEngineTorque(values.dynamics))
                && (!capabilities.torqueGauge || HasCompleteTorqueGauge(values.dynamics))
                && (!capabilities.rotorWarning || values.dynamics != null)
                && (!capabilities.powertrainEffects || HasCompletePowertrainEffects(values.dynamics))
                && (!capabilities.aerodynamicEffects || HasCompleteAerodynamicEffects(values.dynamics))
                && (!capabilities.bladeSlap || (values.dynamics != null && HasValue(values.dynamics.bladeSlapIntensity)))
                && (!capabilities.detailedGroundEffects || HasCompleteDetailedGroundEffects(values.ground))
                && (!capabilities.weaponEffects || HasCompleteWeaponEffects(values.weapons))
                && (!capabilities.engineLifecycle || values.dynamics != null)
                && (!capabilities.brakes || HasCompleteBrakes(values.ground))
                && (!capabilities.doors || HasCompleteDoors(values.cockpit));
        }

        private void RaisePacketReceived(string json, TelemetrySnapshot snapshot)
        {
            Action<string, TelemetrySnapshot> handler = PacketReceived;
            if (handler == null) return;
            try { handler(json, snapshot); }
            catch (Exception ex) { LogError("[DCS Extra Telemetry] Recorder callback failed: " + ex.Message); }
        }

        private void UpdateSequenceStatistics(string sessionId, long sequence)
        {
            if (!string.Equals(lastSessionId, sessionId, StringComparison.Ordinal))
            {
                lastSessionId = sessionId;
                lastSequence = sequence;
                hasSequence = true;
                return;
            }

            if (hasSequence && sequence > lastSequence + 1)
            {
                Interlocked.Add(ref droppedPackets, sequence - lastSequence - 1);
            }

            if (!hasSequence || sequence > lastSequence)
            {
                lastSequence = sequence;
                hasSequence = true;
            }
        }

        private void LogError(string message)
        {
            if (errorLogger == null)
            {
                return;
            }

            try
            {
                errorLogger(message);
            }
            catch (Exception)
            {
                // Diagnostics must never terminate the receiver thread.
            }
        }

        private void UpdateRateStatistics()
        {
            long now = Stopwatch.GetTimestamp();
            packetsInRateWindow++;
            double seconds = (now - rateWindowStartedTimestamp) / (double)Stopwatch.Frequency;

            if (seconds >= 1.0)
            {
                packetRateHz = packetsInRateWindow / seconds;
                packetsInRateWindow = 0;
                rateWindowStartedTimestamp = now;
            }
        }

        private void LogPacketError(string message)
        {
            long now = Stopwatch.GetTimestamp();
            long prior = Interlocked.Read(ref lastPacketErrorLogTimestamp);
            if (prior != 0 && now - prior < Stopwatch.Frequency * 5L) return;
            Interlocked.Exchange(ref lastPacketErrorLogTimestamp, now);
            LogError("[DCS Extra Telemetry] " + message);
        }

        private static bool IsValidVector(Vector3 value)
        {
            return value == null
                || (IsValidNumber(value.x) && IsValidNumber(value.y) && IsValidNumber(value.z));
        }

        private static bool IsCompleteVector(Vector3 value)
        {
            return value != null && HasValue(value.x) && HasValue(value.y) && HasValue(value.z);
        }

        private static bool IsValidPair(ScalarPair value)
        {
            return value == null || (IsValidNumber(value.left) && IsValidNumber(value.right));
        }

        private static bool IsCompletePair(ScalarPair value)
        {
            return value != null && HasValue(value.left) && HasValue(value.right);
        }

        private static bool IsValidMechanical(MechanicalValues value)
        {
            return value == null
                || (IsValidNumber(value.gearPosition)
                    && IsValidNumber(value.flapsPosition)
                    && IsValidNumber(value.speedbrakePosition));
        }

        private static bool IsValidWeapons(WeaponValues value)
        {
            return value == null
                || ((value.selectedWeaponName == null || value.selectedWeaponName.Length <= 256)
                    && (value.lastFiredWeaponName == null || value.lastFiredWeaponName.Length <= 256)
                    && value.weaponEventCounter >= 0
                    && IsNonNegative(value.selectedStation)
                    && IsNonNegative(value.totalPayloadCount)
                    && IsNonNegative(value.cannonShells)
                    && IsValidPulse(value.weaponFirePulse)
                    && IsValidPulse(value.gunFirePulse)
                    && IsValidPulse(value.stationGunFirePulse)
                    && IsValidPulse(value.crewGunFirePulse)
                    && IsValidPulse(value.leftCrewGunFirePulse)
                    && IsValidPulse(value.rightCrewGunFirePulse)
                    && IsValidPulse(value.payloadReleasePulse)
                    && IsNonNegative(value.gunRoundsFired)
                    && IsNonNegative(value.stationGunRoundsFired)
                    && IsNonNegative(value.crewGunRoundsFired)
                    && IsNonNegative(value.leftCrewGunRoundsFired)
                    && IsNonNegative(value.rightCrewGunRoundsFired)
                    && IsNonNegative(value.stationGunAmmoRemaining)
                    && IsNonNegative(value.leftCrewGunAmmoRemaining)
                    && IsNonNegative(value.rightCrewGunAmmoRemaining)
                    && IsNonNegative(value.lastFiredWeaponStation)
                    && IsNonNegative(value.payloadReleasedCount)
                    && (value.weaponClass == null || value.weaponClass.Length <= 32)
                    && IsValidPulse(value.weaponBurstActive)
                    && IsNonNegative(value.weaponBurstDurationMs)
                    && IsNonNegative(value.weaponRateHz)
                    && IsNonNegative(value.nominalWeaponRateHz)
                    && IsValidPulse(value.weaponImpulseStrength)
                    && IsValidPulse(value.lightGunIntensity)
                    && IsValidPulse(value.heavyGunIntensity)
                    && IsValidPulse(value.rocketLaunchIntensity)
                    && IsValidPulse(value.missileLaunchIntensity));
        }

        private static bool IsValidGround(GroundValues value)
        {
            return value == null
                || ((value.strutSource == null || value.strutSource.Length <= 64)
                    && IsNonNegative(value.groundSpeedMps)
                    && IsNonNegative(value.altitudeAglM)
                    && IsValidNumber(value.wheelBrakesPosition)
                    && (value.brakeSource == null || value.brakeSource.Length <= 64)
                    && IsValidNumber(value.tailStrutCompression)
                    && IsValidNumber(value.leftStrutCompression)
                    && IsValidNumber(value.rightStrutCompression)
                    && IsValidNumber(value.tailWheelRotation)
                    && IsValidNumber(value.leftWheelRotation)
                    && IsValidNumber(value.rightWheelRotation)
                    && IsValidNumber(value.skidFrontLeftCompression)
                    && IsValidNumber(value.skidFrontRightCompression)
                    && IsValidNumber(value.skidRearLeftCompression)
                    && IsValidNumber(value.skidRearRightCompression)
                    && IsValidNumber(value.leftSkidCompression)
                    && IsValidNumber(value.rightSkidCompression)
                    && IsValidPulse(value.skidFrontLeftContact)
                    && IsValidPulse(value.skidFrontRightContact)
                    && IsValidPulse(value.skidRearLeftContact)
                    && IsValidPulse(value.skidRearRightContact)
                    && IsValidPulse(value.skidFrontLeftContactPulse)
                    && IsValidPulse(value.skidFrontRightContactPulse)
                    && IsValidPulse(value.skidRearLeftContactPulse)
                    && IsValidPulse(value.skidRearRightContactPulse)
                    && IsValidPulse(value.onGround)
                    && IsValidPulse(value.tailWheelContact)
                    && IsValidPulse(value.leftWheelContact)
                    && IsValidPulse(value.rightWheelContact)
                    && IsValidPulse(value.tailWheelContactPulse)
                    && IsValidPulse(value.leftWheelContactPulse)
                    && IsValidPulse(value.rightWheelContactPulse)
                    && IsValidPulse(value.touchdownPulse)
                    && IsNonNegative(value.touchdownVerticalSpeedMps)
                    && IsValidPulse(value.touchdownStrength)
                    && IsValidPulse(value.suspensionMovementIntensity)
                    && IsValidPulse(value.groundRollIntensity)
                    && IsValidPulse(value.groundResonanceIntensity)
                    && IsValidPulse(value.brakingIntensity)
                    && IsValidPulse(value.brakeDecelerationIntensity)
                    && IsValidPulse(value.wheelSkidIntensity)
                    && IsValidPulse(value.groundPitchRockIntensity)
                    && IsValidPulse(value.groundRollRockIntensity)
                    && IsValidNumber(value.longitudinalGroundSpeedMps)
                    && IsValidNumber(value.lateralGroundSlipMps)
                    && IsValidPulse(value.skidScrubIntensity)
                    && IsValidPulse(value.wheelShimmyIntensity)
                    && IsValidPulse(value.tailWheelCasterIntensity)
                    && IsValidPulse(value.liftoffPulse)
                    && IsValidPulse(value.weightOnGroundIntensity));
        }

        private static bool IsValidDynamics(DynamicsValues value)
        {
            return value == null
                || ((value.rotorSource == null || value.rotorSource.Length <= 64)
                    && IsNonNegative(value.mainRotorRpmPct)
                    && IsNonNegative(value.mainRotorRpm)
                    && IsNonNegative(value.mainRotorHz)
                    && IsNonNegative(value.bladePassHz)
                    && IsValidPulse(value.collectivePosition)
                    && IsNonNegative(value.collectiveMovementRate)
                    && IsNonNegative(value.engineTorqueLeftPct)
                    && IsNonNegative(value.engineTorqueRightPct)
                    && IsValidPulse(value.torqueGaugeNormalized)
                    && IsValidPulse(value.rotorLoadIntensity)
                    && IsValidPulse(value.transmissionLoadIntensity)
                    && IsValidPulse(value.engineGovernorResponseIntensity)
                    && IsValidPulse(value.rotorDroopIntensity)
                    && IsValidPulse(value.rotorSpoolIntensity)
                    && IsValidPulse(value.turbulenceIntensity)
                    && IsValidPulse(value.controlResponseIntensity)
                    && IsValidPulse(value.airframeBuffetIntensity)
                    && IsValidPulse(value.maneuverLoadIntensity)
                    && IsValidPulse(value.maneuverResponseIntensity)
                    && IsValidPulse(value.lateralManeuverIntensity)
                    && IsValidPulse(value.settlingWithPowerCue)
                    && IsValidPulse(value.vrsRiskIntensity)
                    && IsValidPulse(value.translationalLiftTransition)
                    && IsValidPulse(value.bladeSlapIntensity)
                    && IsValidPulse(value.hardManeuverPulse)
                    && IsValidPulse(value.airframeImpactPulse));
        }

        private static bool IsValidEffects(EffectEnvelopeValues value)
        {
            return value == null
                || (IsValidPulse(value.touchdownEnvelope)
                    && IsValidPulse(value.combatDamageEnvelope)
                    && IsValidPulse(value.doorMotionEnvelope)
                    && IsValidPulse(value.brakeEnvelope)
                    && IsValidPulse(value.weaponRecoilEnvelope));
        }

        private static bool IsValidCountermeasures(CountermeasureValues value)
        {
            return value == null
                || (IsNonNegative(value.chaffRemaining)
                    && IsNonNegative(value.flareRemaining)
                    && IsValidPulse(value.chaffReleasePulse)
                    && IsValidPulse(value.flareReleasePulse)
                    && IsNonNegative(value.chaffReleasedCount)
                    && IsNonNegative(value.flareReleasedCount));
        }

        private static bool IsValidCockpit(CockpitValues value)
        {
            return value == null
                || ((value.weaponSelectorName == null || value.weaponSelectorName.Length <= 32)
                    && IsNonNegative(value.weaponSelectorPosition)
                    && IsValidNumber(value.countermeasureMode)
                    && IsValidDoors(value.doors));
        }

        private static bool HasCompleteGearStruts(GroundValues value)
        {
            return value != null
                && HasValue(value.tailStrutCompression)
                && HasValue(value.leftStrutCompression)
                && HasValue(value.rightStrutCompression)
                && HasValue(value.onGround)
                && HasValue(value.tailWheelContact)
                && HasValue(value.leftWheelContact)
                && HasValue(value.rightWheelContact)
                && HasValue(value.touchdownPulse);
        }

        private static bool HasCompleteGroundMotion(GroundValues value)
        {
            return value != null
                && HasValue(value.groundSpeedMps)
                && HasValue(value.groundRollIntensity)
                && HasValue(value.brakingIntensity);
        }

        private static bool HasCompleteBrakes(GroundValues value)
        {
            return value != null
                && HasValue(value.wheelBrakesPosition)
                && !string.IsNullOrEmpty(value.brakeSource);
        }

        private static bool IsValidDoors(DoorValues value)
        {
            return value == null
                || (IsValidPulse(value.pilotDoorPosition)
                    && IsValidPulse(value.copilotDoorPosition)
                    && IsValidPulse(value.leftCargoDoorPosition)
                    && IsValidPulse(value.rightCargoDoorPosition)
                    && IsValidPulse(value.leftGunnerDoorPosition)
                    && IsValidPulse(value.rightGunnerDoorPosition)
                    && IsValidPulse(value.doorMovementIntensity));
        }

        private static bool HasCompleteDoors(CockpitValues value)
        {
            DoorValues doors = value == null ? null : value.doors;
            return doors != null
                && HasValue(doors.pilotDoorPosition)
                && HasValue(doors.copilotDoorPosition)
                && HasValue(doors.leftCargoDoorPosition)
                && HasValue(doors.rightCargoDoorPosition)
                && HasValue(doors.leftGunnerDoorPosition)
                && HasValue(doors.rightGunnerDoorPosition)
                && HasValue(doors.doorMovementIntensity);
        }

        private static bool HasCompleteWheelRotation(GroundValues value)
        {
            return value != null
                && HasValue(value.tailWheelRotation)
                && HasValue(value.leftWheelRotation)
                && HasValue(value.rightWheelRotation);
        }

        private static bool HasCompleteCountermeasures(CountermeasureValues value)
        {
            return value != null
                && HasValue(value.chaffRemaining)
                && HasValue(value.flareRemaining)
                && HasValue(value.chaffReleasePulse)
                && HasValue(value.flareReleasePulse);
        }

        private static bool HasCompleteSkids(GroundValues value)
        {
            return value != null
                && HasValue(value.skidFrontLeftCompression)
                && HasValue(value.skidFrontRightCompression)
                && HasValue(value.skidRearLeftCompression)
                && HasValue(value.skidRearRightCompression)
                && HasValue(value.leftSkidCompression)
                && HasValue(value.rightSkidCompression)
                && HasValue(value.skidFrontLeftContact)
                && HasValue(value.skidFrontRightContact)
                && HasValue(value.skidRearLeftContact)
                && HasValue(value.skidRearRightContact);
        }

        private static bool HasCompleteCockpit(CockpitValues value)
        {
            return value != null
                && HasValue(value.weaponSelectorPosition)
                && !string.IsNullOrEmpty(value.weaponSelectorName)
                && HasValue(value.countermeasureMode);
        }

        private static bool HasCompleteRotorDynamics(DynamicsValues value)
        {
            return value != null
                && HasValue(value.mainRotorRpmPct)
                && HasValue(value.mainRotorRpm)
                && HasValue(value.mainRotorHz)
                && HasValue(value.bladePassHz)
                && !string.IsNullOrEmpty(value.rotorSource);
        }

        private static bool HasCompleteControlPositions(DynamicsValues value)
        {
            return value != null
                && HasValue(value.collectivePosition)
                && HasValue(value.collectiveMovementRate);
        }

        private static bool HasCompleteEngineTorque(DynamicsValues value)
        {
            return value != null
                && HasValue(value.engineTorqueLeftPct)
                && HasValue(value.engineTorqueRightPct);
        }

        private static bool HasCompleteTorqueGauge(DynamicsValues value)
        {
            return value != null && HasValue(value.torqueGaugeNormalized);
        }

        private static bool HasCompletePowertrainEffects(DynamicsValues value)
        {
            return value != null
                && HasValue(value.rotorLoadIntensity)
                && HasValue(value.transmissionLoadIntensity)
                && HasValue(value.rotorDroopIntensity)
                && HasValue(value.rotorSpoolIntensity);
        }

        private static bool HasCompleteAerodynamicEffects(DynamicsValues value)
        {
            return value != null
                && HasValue(value.turbulenceIntensity)
                && HasValue(value.airframeBuffetIntensity)
                && HasValue(value.maneuverLoadIntensity)
                && HasValue(value.settlingWithPowerCue)
                && HasValue(value.vrsRiskIntensity)
                && HasValue(value.translationalLiftTransition)
                && HasValue(value.hardManeuverPulse)
                && HasValue(value.airframeImpactPulse);
        }

        private static bool HasCompleteDetailedGroundEffects(GroundValues value)
        {
            return value != null
                && HasValue(value.groundPitchRockIntensity)
                && HasValue(value.groundRollRockIntensity)
                && HasValue(value.longitudinalGroundSpeedMps)
                && HasValue(value.lateralGroundSlipMps)
                && HasValue(value.skidScrubIntensity)
                && HasValue(value.wheelShimmyIntensity)
                && HasValue(value.tailWheelCasterIntensity)
                && HasValue(value.liftoffPulse)
                && HasValue(value.weightOnGroundIntensity);
        }

        private static bool HasCompleteWeaponEffects(WeaponValues value)
        {
            return value != null
                && !string.IsNullOrEmpty(value.weaponClass)
                && HasValue(value.weaponBurstActive)
                && HasValue(value.weaponBurstDurationMs)
                && HasValue(value.weaponRateHz)
                && HasValue(value.weaponImpulseStrength)
                && HasValue(value.lightGunIntensity)
                && HasValue(value.heavyGunIntensity)
                && HasValue(value.rocketLaunchIntensity)
                && HasValue(value.missileLaunchIntensity);
        }

        private static bool HasCompleteWeaponPayload(WeaponValues value)
        {
            return value != null
                && (HasValue(value.totalPayloadCount) || HasValue(value.cannonShells));
        }

        private static bool HasCompleteWeaponEvents(WeaponValues value)
        {
            return value != null
                && HasValue(value.weaponFirePulse)
                && HasValue(value.gunFirePulse)
                && HasValue(value.payloadReleasePulse)
                && HasValue(value.gunRoundsFired)
                && HasValue(value.payloadReleasedCount);
        }

        private static bool HasCompleteStationGuns(WeaponValues value)
        {
            return value != null
                && HasValue(value.stationGunFirePulse)
                && HasValue(value.stationGunRoundsFired)
                && HasValue(value.stationGunAmmoRemaining);
        }

        private static bool HasCompleteCrewGuns(WeaponValues value)
        {
            return value != null
                && HasValue(value.crewGunFirePulse)
                && HasValue(value.leftCrewGunFirePulse)
                && HasValue(value.rightCrewGunFirePulse)
                && HasValue(value.crewGunRoundsFired)
                && HasValue(value.leftCrewGunRoundsFired)
                && HasValue(value.rightCrewGunRoundsFired)
                && (HasValue(value.leftCrewGunAmmoRemaining) || HasValue(value.rightCrewGunAmmoRemaining));
        }

        private static bool IsValidPulse(double? value)
        {
            return !value.HasValue
                || (IsFinite(value.Value) && value.Value >= 0.0 && value.Value <= 1.0);
        }

        private static bool IsNonNegative(double? value)
        {
            return !value.HasValue || (IsFinite(value.Value) && value.Value >= 0.0);
        }

        private static bool HasCompleteAirData(TelemetryValues values)
        {
            return HasValue(values.indicatedAirSpeedMps)
                && HasValue(values.trueAirSpeedMps)
                && HasValue(values.verticalVelocityMps)
                && HasValue(values.angleOfAttackRad)
                && HasValue(values.mach);
        }

        private static bool IsValidNumber(double? value)
        {
            return !value.HasValue || IsFinite(value.Value);
        }

        private static bool HasValue(double? value)
        {
            return value.HasValue && IsFinite(value.Value);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private double GetDataAgeMilliseconds()
        {
            long timestamp = Volatile.Read(ref lastPacketTimestamp);
            if (timestamp == 0) return -1.0;
            double age = (Stopwatch.GetTimestamp() - timestamp) * 1000.0 / Stopwatch.Frequency;
            return age < 0.0 ? 0.0 : age;
        }
    }
}
