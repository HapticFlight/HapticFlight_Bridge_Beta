-- DCS Extra Telemetry for SimHub
-- Install this file in: Saved Games\DCS*\Scripts\Hooks\DCSExtraTelemetry.lua
-- This Hook never reads or modifies Export.lua.

local PLUGIN_NAME = "DCSExtraTelemetry"
local PROTOCOL_VERSION = 1
local DESTINATION_IP = "127.0.0.1"
local DEFAULT_DESTINATION_PORT = 29381
local DEFAULT_RECORDER_DESTINATION_PORT = 29382
local SEND_INTERVAL_SECONDS = 1.0 / 60.0
local WEAPON_PULSE_SECONDS = 0.15
local EVENT_PULSE_SECONDS = 0.20
local WS_TYPE_SHELL = 6
local UH60_TAIL_STRUT_AIRBORNE_BASELINE = 0.33
local UH60_TAIL_CONTACT_ON = 0.36
local UH60_TAIL_CONTACT_OFF = 0.345
local UH60_MAIN_CONTACT_ON = 0.03
local UH60_MAIN_CONTACT_OFF = 0.015
local UH60_TAIL_BRAKE_ARM_POSITION = 0.75
local UH60_TAIL_BRAKE_RELEASE_POSITION = 0.25
local UH60_TAIL_BRAKE_UNLOAD_DELTA = 0.025
local UH60_TAIL_BRAKE_SETTLE_RECOVERY = 0.012
local UH60_TAIL_BRAKE_SETTLE_RISE = 0.0015
local UH60_TAIL_BRAKE_LATCH_TIMEOUT_SECONDS = 3.0
local ROTOR_SPOOL_SMOOTHING_SECONDS = 1.0
local ROTOR_SPOOL_RATE_DEADBAND = 0.02
local ROTOR_SPOOL_RATE_FULL_SCALE = 0.18

local dcs = DCS
local exportApi = Export
local netApi = net
local logger = log
local luaPackage = package
local luaDebug = debug
local globalGetDevice = GetDevice
local ioApi = io
local osApi = os

local state = {
    socketLibrary = nil,
    sender = nil,
    recorderSender = nil,
    destinationPort = DEFAULT_DESTINATION_PORT,
    recorderDestinationPort = DEFAULT_RECORDER_DESTINATION_PORT,
    sessionId = "not-started",
    sequence = 0,
    lastSentAt = -1.0,
    lastErrorLogAt = -1000.0,
    lastAircraftType = "",
    lastCannonShells = nil,
    lastStations = nil,
    weaponFirePulseUntil = -1.0,
    gunFirePulseUntil = -1.0,
    stationGunFirePulseUntil = -1.0,
    crewGunFirePulseUntil = -1.0,
    leftCrewGunFirePulseUntil = -1.0,
    rightCrewGunFirePulseUntil = -1.0,
    payloadReleasePulseUntil = -1.0,
    gunRoundsFired = 0.0,
    stationGunRoundsFired = 0.0,
    crewGunRoundsFired = 0.0,
    leftCrewGunRoundsFired = 0.0,
    rightCrewGunRoundsFired = 0.0,
    payloadReleasedCount = 0.0,
    lastFiredWeaponStation = 0.0,
    lastFiredWeaponName = "",
    weaponNameCache = {},
    weaponEventCounter = 0,
    lastGroundSampleAt = nil,
    lastGroundSpeed = nil,
    filteredBrakeDeceleration = 0.0,
    lastStruts = nil,
    lastContacts = nil,
    lastVerticalVelocity = nil,
    tailSupportCompression = nil,
    tailBrakeUnloaded = false,
    tailBrakeUnloadedAt = nil,
    tailContactPulseUntil = -1.0,
    leftContactPulseUntil = -1.0,
    rightContactPulseUntil = -1.0,
    touchdownPulseUntil = -1.0,
    touchdownVerticalSpeedMps = 0.0,
    lastSkids = nil,
    lastSkidContacts = nil,
    lastDoors = nil,
    lastDoorSampleAt = nil,
    skidFrontLeftPulseUntil = -1.0,
    skidFrontRightPulseUntil = -1.0,
    skidRearLeftPulseUntil = -1.0,
    skidRearRightPulseUntil = -1.0,
    lastChaff = nil,
    lastFlare = nil,
    chaffPulseUntil = -1.0,
    flarePulseUntil = -1.0,
    chaffReleasedCount = 0.0,
    flareReleasedCount = 0.0,
    liftoffPulseUntil = -1.0,
    lastRotorSampleAt = nil,
    lastDynamicsSampleAt = nil,
    lastRotorPhase = nil,
    filteredRotorRpm = nil,
    spoolRotorRpmPct = nil,
    lastSpoolSampleAt = nil,
    lastCollective = nil,
    lastAcceleration = nil,
    lastAngularVelocity = nil,
    lastManeuverLoad = 0.0,
    lastTurbulence = 0.0,
    lastEngineRpmAverage = nil,
    lastEtlAirspeed = nil,
    filteredBladeSlap = 0.0,
    vrsExposureSeconds = 0.0,
    filteredVrsRisk = 0.0,
    filteredManeuverResponse = 0.0,
    filteredLateralManeuver = 0.0,
    hardManeuverPulseUntil = -1.0,
    airframeImpactPulseUntil = -1.0,
    weaponBurstStartedAt = nil,
    weaponBurstLastAt = -1.0,
    weaponBurstClass = "NONE",
    weaponRateHz = 0.0,
    weaponImpulseStrength = 0.0
}

local callbacks = {}

local function readPortConfiguration()
    local destinationPort = DEFAULT_DESTINATION_PORT
    local recorderDestinationPort = DEFAULT_RECORDER_DESTINATION_PORT
    local warnings = {}

    local ok, errorMessage = pcall(function()
        if not osApi or not osApi.getenv or not ioApi or not ioApi.open then
            return
        end

        local localAppData = osApi.getenv("LOCALAPPDATA")
        if type(localAppData) ~= "string" or localAppData == "" then
            return
        end

        local file = ioApi.open(localAppData .. "\\DcsExtraTelemetry\\ports.ini", "r")
        if not file then
            return
        end

        local values = {}
        for line in file:lines() do
            if not string.match(line, "^%s*$") and not string.match(line, "^%s*[#;]") then
                local key, value = string.match(line, "^%s*([%a]+)%s*=%s*(%d+)%s*$")
                if key then key = string.lower(key) end
                local numericValue = tonumber(value)
                if (key == "simhubPort" or key == "recorderPort")
                    and numericValue and numericValue >= 1024 and numericValue <= 65535 then
                    values[key] = numericValue
                else
                    warnings[#warnings + 1] = "Ignored invalid ports.ini line: " .. tostring(line)
                end
            end
        end
        file:close()

        local candidateSimhub = values.simhubPort or destinationPort
        local candidateRecorder = values.recorderPort or recorderDestinationPort
        if candidateSimhub ~= candidateRecorder then
            destinationPort = candidateSimhub
            recorderDestinationPort = candidateRecorder
        else
            warnings[#warnings + 1] = "SimHub and recorder ports must differ; using defaults."
        end
    end)

    if not ok and logger and logger.write then
        pcall(logger.write, PLUGIN_NAME, logger.WARNING,
            "Port settings unavailable; using defaults: " .. tostring(errorMessage))
    end
    if logger and logger.write then
        for _, warning in ipairs(warnings) do
            pcall(logger.write, PLUGIN_NAME, logger.WARNING, warning)
        end
        pcall(logger.write, PLUGIN_NAME, logger.INFO,
            "UDP ports: SimHub " .. tostring(destinationPort)
                .. ", recorder " .. tostring(recorderDestinationPort))
    end

    return destinationPort, recorderDestinationPort
end

local function nowSeconds()
    if dcs and dcs.getRealTime then
        local ok, value = pcall(dcs.getRealTime)
        if ok and type(value) == "number" then
            return value
        end
    end

    return 0.0
end

local function logError(context, message)
    local now = nowSeconds()
    if now - state.lastErrorLogAt < 5.0 then
        return
    end

    state.lastErrorLogAt = now
    if logger and logger.write then
        pcall(logger.write, PLUGIN_NAME, logger.ERROR, context .. ": " .. tostring(message))
    end
end

local function traceback(message)
    if luaDebug and luaDebug.traceback then
        return luaDebug.traceback(tostring(message), 2)
    end

    return tostring(message)
end

local function runSafely(context, action)
    local ok, err = xpcall(action, traceback)
    if not ok then
        logError(context, err)
    end

    return ok
end

local function readExport(functionName)
    if not exportApi then
        return nil, false
    end

    local exportFunction = exportApi[functionName]
    if type(exportFunction) ~= "function" then
        return nil, false
    end

    local ok, value = pcall(exportFunction)
    if not ok then
        return nil, false
    end

    return value, value ~= nil
end

local function finiteNumber(value)
    if type(value) ~= "number" or value ~= value or value == math.huge or value == -math.huge then
        return nil
    end

    return value
end

local function readNumberExport(functionName)
    local value, available = readExport(functionName)
    value = finiteNumber(value)
    return value, available and value ~= nil
end

local function readVectorExport(functionName)
    local value, available = readExport(functionName)
    if not available or type(value) ~= "table" then
        return nil, false
    end

    local vector = {
        x = finiteNumber(value.x),
        y = finiteNumber(value.y),
        z = finiteNumber(value.z)
    }

    if vector.x == nil or vector.y == nil or vector.z == nil then
        return nil, false
    end

    return vector, true
end

local function componentValue(container, componentName)
    if type(container) ~= "table" then
        return nil
    end

    local component = container[componentName]
    if type(component) ~= "table" then
        return nil
    end

    return finiteNumber(component.value)
end

local function nestedNumber(container, first, second)
    if type(container) ~= "table" or type(container[first]) ~= "table" then
        return nil
    end

    return finiteNumber(container[first][second])
end

local function readDrawArgument(argumentNumber)
    if not exportApi or type(exportApi.LoGetAircraftDrawArgumentValue) ~= "function" then
        return nil, false
    end

    local ok, value = pcall(exportApi.LoGetAircraftDrawArgumentValue, argumentNumber)
    value = finiteNumber(value)
    return value, ok and value ~= nil
end

local function clamp01(value)
    if value == nil or value <= 0.0 then
        return 0.0
    end
    if value >= 1.0 then
        return 1.0
    end
    return value
end

local function normalizedAboveBaseline(value, baseline)
    if value == nil then return 0.0 end
    return clamp01((value - baseline) / math.max(0.001, 1.0 - baseline))
end

local function contactWithHysteresis(value, wasContact, contactOn, contactOff)
    if value == nil then return false end
    return value > (wasContact and contactOff or contactOn)
end

local function resetGroundTracking()
    state.lastGroundSampleAt = nil
    state.lastGroundSpeed = nil
    state.filteredBrakeDeceleration = 0.0
    state.lastStruts = nil
    state.lastContacts = nil
    state.lastVerticalVelocity = nil
    state.tailSupportCompression = nil
    state.tailBrakeUnloaded = false
    state.tailBrakeUnloadedAt = nil
    state.tailContactPulseUntil = -1.0
    state.leftContactPulseUntil = -1.0
    state.rightContactPulseUntil = -1.0
    state.touchdownPulseUntil = -1.0
    state.touchdownVerticalSpeedMps = 0.0
    state.lastSkids = nil
    state.lastSkidContacts = nil
    state.lastDoors = nil
    state.lastDoorSampleAt = nil
    state.skidFrontLeftPulseUntil = -1.0
    state.skidFrontRightPulseUntil = -1.0
    state.skidRearLeftPulseUntil = -1.0
    state.skidRearRightPulseUntil = -1.0
    state.liftoffPulseUntil = -1.0
end

local function resetDynamicsTracking()
    state.lastRotorSampleAt = nil
    state.lastDynamicsSampleAt = nil
    state.lastRotorPhase = nil
    state.filteredRotorRpm = nil
    state.spoolRotorRpmPct = nil
    state.lastSpoolSampleAt = nil
    state.lastCollective = nil
    state.lastAcceleration = nil
    state.lastAngularVelocity = nil
    state.lastManeuverLoad = 0.0
    state.lastTurbulence = 0.0
    state.lastEngineRpmAverage = nil
    state.lastEtlAirspeed = nil
    state.filteredBladeSlap = 0.0
    state.vrsExposureSeconds = 0.0
    state.filteredVrsRisk = 0.0
    state.filteredManeuverResponse = 0.0
    state.filteredLateralManeuver = 0.0
    state.hardManeuverPulseUntil = -1.0
    state.airframeImpactPulseUntil = -1.0
end

local function readGroundValues(now, aircraftType, mechInfo, verticalVelocity)
    local gear = type(mechInfo) == "table" and mechInfo.gear or nil
    local tailStrut = nestedNumber(gear, "nose", "rod")
    local leftStrut = nestedNumber(gear, "main", "left")
    local rightStrut = nestedNumber(gear, "main", "right")

    -- Some DCS schemas store each main gear as a table containing `rod`.
    if type(gear) == "table" and type(gear.main) == "table" then
        if type(gear.main.left) == "table" then
            leftStrut = finiteNumber(gear.main.left.rod)
        end
        if type(gear.main.right) == "table" then
            rightStrut = finiteNumber(gear.main.right.rod)
        end
    end

    local skidFrontLeft, skidFrontRight, skidRearLeft, skidRearRight = nil, nil, nil, nil
    local hasSkids = false
    local source = ""
    if aircraftType == "AH-6J" or aircraftType == "MH-6J" then
        local hasFrontLeft, hasFrontRight, hasRearLeft, hasRearRight
        skidFrontLeft, hasFrontLeft = readDrawArgument(202)
        skidFrontRight, hasFrontRight = readDrawArgument(204)
        skidRearLeft, hasRearLeft = readDrawArgument(201)
        skidRearRight, hasRearRight = readDrawArgument(203)
        hasSkids = hasFrontLeft and hasFrontRight and hasRearLeft and hasRearRight
        if hasSkids then
            leftStrut = (skidFrontLeft + skidRearLeft) / 2.0
            rightStrut = (skidFrontRight + skidRearRight) / 2.0
            tailStrut = (skidRearLeft + skidRearRight) / 2.0
            source = "AH6SkidDrawArguments"
        end
    elseif aircraftType == "OH58D" then
        -- Polychop's FM configuration maps its four skid suspension points to
        -- draw arguments 1/9 (front) and 6/4 (rear).
        local hasFrontLeft, hasFrontRight, hasRearLeft, hasRearRight
        skidFrontLeft, hasFrontLeft = readDrawArgument(1)
        skidFrontRight, hasFrontRight = readDrawArgument(9)
        skidRearLeft, hasRearLeft = readDrawArgument(6)
        skidRearRight, hasRearRight = readDrawArgument(4)
        hasSkids = hasFrontLeft and hasFrontRight and hasRearLeft and hasRearRight
        if hasSkids then
            leftStrut = (skidFrontLeft + skidRearLeft) / 2.0
            rightStrut = (skidFrontRight + skidRearRight) / 2.0
            tailStrut = (skidRearLeft + skidRearRight) / 2.0
            source = "OH58SkidDrawArguments"
        end
    end

    local binaryWheelContacts = nil
    if source == "" and tailStrut ~= nil and leftStrut ~= nil and rightStrut ~= nil then
        source = "LoGetMechInfo"
    elseif source == "" and (aircraftType == "UH-60L" or aircraftType == "UH-60L_DAP") then
        local tail, hasTail = readDrawArgument(1)
        local left, hasLeft = readDrawArgument(6)
        local right, hasRight = readDrawArgument(4)
        if hasTail and hasLeft and hasRight then
            tailStrut, leftStrut, rightStrut = tail, left, right
            source = "UH60DrawArguments"
        end
    elseif source == "" and aircraftType == "AH-64D_BLK_II" then
        local tail, hasTail = readDrawArgument(1)
        local left, hasLeft = readDrawArgument(6)
        local right, hasRight = readDrawArgument(4)
        if hasTail and hasLeft and hasRight then
            binaryWheelContacts = {
                tail = tail > 0.5,
                left = left > 0.5,
                right = right > 0.5
            }
            source = "AH64WeightOnWheels"
        end
    end

    -- The UH-1H model exposes a single weight-on-skids draw argument, not
    -- independent compression values. Keep that distinction explicit.
    local skidWeightOn, hasSkidWeight = nil, false
    if aircraftType == "UH-1H" then
        skidWeightOn, hasSkidWeight = readDrawArgument(104)
        if hasSkidWeight and source == "" then source = "UH1WeightOnSkids" end
    end

    local velocity = readVectorExport("LoGetVectorVelocity")
    local groundSpeed = velocity and math.sqrt(velocity.x * velocity.x + velocity.z * velocity.z) or nil
    local helicopterFm = readExport("LoGetHelicopterFMData")
    local bodySpeed = type(helicopterFm) == "table" and helicopterFm.speed or nil
    local longitudinalSpeed = type(bodySpeed) == "table" and finiteNumber(bodySpeed.x) or nil
    local lateralSpeed = type(bodySpeed) == "table" and finiteNumber(bodySpeed.z) or nil
    local altitudeAgl = readNumberExport("LoGetAltitudeAboveGround")
    if altitudeAgl ~= nil and altitudeAgl < 0.0 then altitudeAgl = 0.0 end
    local wheelBrakes = componentValue(mechInfo, "wheelbrakes")
    local brakeSource = wheelBrakes ~= nil and "LoGetMechInfo" or ""
    local parkingBrakeOn = false
    if aircraftType == "UH-60L" or aircraftType == "UH-60L_DAP" then
        -- UH-60L mod: argument 501 is the animated pedal-brake position and
        -- argument 81 is the parking-brake handle.  The mod does not publish
        -- pedal braking through LoGetMechInfo, so prefer these direct sources.
        local pedalBrakes, hasPedalBrakes = readDrawArgument(501)
        if hasPedalBrakes then
            wheelBrakes = clamp01(pedalBrakes)
            brakeSource = "UH60PedalBrakeArgument"
        end
        local parkingBrake, hasParkingBrake = readDrawArgument(81)
        if hasParkingBrake then parkingBrakeOn = parkingBrake > 0.5 end
    end

    local tailRotation, hasTailRotation = nil, false
    local leftRotation, hasLeftRotation = nil, false
    local rightRotation, hasRightRotation = nil, false
    if aircraftType == "UH-60L" or aircraftType == "UH-60L_DAP" then
        tailRotation, hasTailRotation = readDrawArgument(101)
        leftRotation, hasLeftRotation = readDrawArgument(103)
        rightRotation, hasRightRotation = readDrawArgument(102)
    end

    local hasStruts = tailStrut ~= nil and leftStrut ~= nil and rightStrut ~= nil
    local isUh60 = aircraftType == "UH-60L" or aircraftType == "UH-60L_DAP"
    local contacts = nil
    if hasStruts then
        if isUh60 then
            local previous = state.lastContacts or { tail = false, left = false, right = false }
            contacts = {
                tail = contactWithHysteresis(tailStrut, previous.tail,
                    UH60_TAIL_CONTACT_ON, UH60_TAIL_CONTACT_OFF),
                left = contactWithHysteresis(leftStrut, previous.left,
                    UH60_MAIN_CONTACT_ON, UH60_MAIN_CONTACT_OFF),
                right = contactWithHysteresis(rightStrut, previous.right,
                    UH60_MAIN_CONTACT_ON, UH60_MAIN_CONTACT_OFF)
            }
        else
            contacts = {
                tail = tailStrut > 0.001,
                left = leftStrut > 0.001,
                right = rightStrut > 0.001
            }
        end
    elseif binaryWheelContacts ~= nil then
        contacts = binaryWheelContacts
    elseif hasSkidWeight then
        local touching = skidWeightOn > 0.001
        contacts = { tail = false, left = touching, right = touching }
    end

    local suspensionMovement = 0.0
    if hasStruts and state.lastStruts ~= nil and state.lastGroundSampleAt ~= nil then
        local elapsed = now - state.lastGroundSampleAt
        if elapsed > 0.0 then
            local movementPerSecond = (math.abs(tailStrut - state.lastStruts.tail)
                + math.abs(leftStrut - state.lastStruts.left)
                + math.abs(rightStrut - state.lastStruts.right)) / elapsed
            suspensionMovement = clamp01(movementPerSecond / 3.0)
        end
    end

    if contacts ~= nil and state.lastContacts ~= nil then
        local tailFirst = contacts.tail and not state.lastContacts.tail
        local leftFirst = contacts.left and not state.lastContacts.left
        local rightFirst = contacts.right and not state.lastContacts.right
        if tailFirst then state.tailContactPulseUntil = now + EVENT_PULSE_SECONDS end
        if leftFirst then state.leftContactPulseUntil = now + EVENT_PULSE_SECONDS end
        if rightFirst then state.rightContactPulseUntil = now + EVENT_PULSE_SECONDS end

        local wasOnGround = state.lastContacts.tail or state.lastContacts.left or state.lastContacts.right
        local isOnGround = contacts.tail or contacts.left or contacts.right
        if isOnGround and not wasOnGround then
            state.touchdownPulseUntil = now + EVENT_PULSE_SECONDS
            local impactVelocity = state.lastVerticalVelocity or verticalVelocity or 0.0
            state.touchdownVerticalSpeedMps = math.max(0.0, -impactVelocity)
        elseif wasOnGround and not isOnGround then
            state.liftoffPulseUntil = now + EVENT_PULSE_SECONDS
        end
    end

    -- LoGetMechInfo's UH-60 tail strut can remain above its normal airborne
    -- threshold during a brake-induced tail lift. Track the supported strut
    -- reference separately so the later tailwheel settle still produces the
    -- ordinary contact pulse while the main wheels remain on the ground.
    if isUh60 and hasStruts and contacts ~= nil and (contacts.left or contacts.right) then
        local brakePosition = clamp01(wheelBrakes or 0.0)
        if state.tailSupportCompression == nil then
            state.tailSupportCompression = tailStrut
        end

        if not state.tailBrakeUnloaded then
            if brakePosition < UH60_TAIL_BRAKE_RELEASE_POSITION then
                state.tailSupportCompression = state.tailSupportCompression
                    + (tailStrut - state.tailSupportCompression) * 0.08
            elseif brakePosition >= UH60_TAIL_BRAKE_ARM_POSITION
                and tailStrut <= state.tailSupportCompression - UH60_TAIL_BRAKE_UNLOAD_DELTA then
                state.tailBrakeUnloaded = true
                state.tailBrakeUnloadedAt = now
            end
        else
            local previousTail = state.lastStruts and state.lastStruts.tail or tailStrut
            local rising = tailStrut >= previousTail + UH60_TAIL_BRAKE_SETTLE_RISE
            local recovered = tailStrut >= state.tailSupportCompression
                - UH60_TAIL_BRAKE_SETTLE_RECOVERY
            local brakeReleased = brakePosition <= UH60_TAIL_BRAKE_RELEASE_POSITION
            if rising and (recovered or brakeReleased) then
                state.tailContactPulseUntil = now + EVENT_PULSE_SECONDS
                state.tailBrakeUnloaded = false
                state.tailBrakeUnloadedAt = nil
                state.tailSupportCompression = tailStrut
            elseif brakeReleased and state.tailBrakeUnloadedAt ~= nil
                and now - state.tailBrakeUnloadedAt > UH60_TAIL_BRAKE_LATCH_TIMEOUT_SECONDS then
                -- Do not carry an unconfirmed unload into a later taxi event.
                state.tailBrakeUnloaded = false
                state.tailBrakeUnloadedAt = nil
                state.tailSupportCompression = tailStrut
            end
        end
    elseif isUh60 then
        state.tailSupportCompression = nil
        state.tailBrakeUnloaded = false
        state.tailBrakeUnloadedAt = nil
    end

    local skidContacts = nil
    if hasSkids then
        skidContacts = {
            frontLeft = skidFrontLeft > 0.001,
            frontRight = skidFrontRight > 0.001,
            rearLeft = skidRearLeft > 0.001,
            rearRight = skidRearRight > 0.001
        }
        if state.lastSkidContacts ~= nil then
            if skidContacts.frontLeft and not state.lastSkidContacts.frontLeft then state.skidFrontLeftPulseUntil = now + EVENT_PULSE_SECONDS end
            if skidContacts.frontRight and not state.lastSkidContacts.frontRight then state.skidFrontRightPulseUntil = now + EVENT_PULSE_SECONDS end
            if skidContacts.rearLeft and not state.lastSkidContacts.rearLeft then state.skidRearLeftPulseUntil = now + EVENT_PULSE_SECONDS end
            if skidContacts.rearRight and not state.lastSkidContacts.rearRight then state.skidRearRightPulseUntil = now + EVENT_PULSE_SECONDS end
        end
        state.lastSkids = {
            frontLeft = skidFrontLeft, frontRight = skidFrontRight,
            rearLeft = skidRearLeft, rearRight = skidRearRight
        }
        state.lastSkidContacts = skidContacts
    end

    local onGround = contacts ~= nil and (contacts.tail or contacts.left or contacts.right)
    -- Signed compression rate distinguishes a wheel/strut loading event from
    -- unloading. Positive values mean compression is increasing; negative
    -- values mean the gear is extending. A small deadband removes draw-frame
    -- noise at rest without hiding a real touchdown or rebound.
    local strutCompressionRate = 0.0
    if hasStruts and state.lastStruts ~= nil and state.lastGroundSampleAt ~= nil then
        local elapsed = now - state.lastGroundSampleAt
        if elapsed > 0.0 then
            local currentAverage = (tailStrut + leftStrut + rightStrut) / 3.0
            local priorAverage = (state.lastStruts.tail + state.lastStruts.left
                + state.lastStruts.right) / 3.0
            strutCompressionRate = (currentAverage - priorAverage) / elapsed
            if math.abs(strutCompressionRate) < 0.015 then strutCompressionRate = 0.0 end
        end
    end
    local speedFactor = clamp01((groundSpeed or 0.0) / 20.0)
    local groundRollIntensity = onGround and clamp01(speedFactor * (0.35 + suspensionMovement)) or 0.0
    local brakePosition = clamp01(wheelBrakes or 0.0)
    local brakeDecelerationRaw = 0.0
    local groundElapsed = state.lastGroundSampleAt ~= nil and now - state.lastGroundSampleAt or 0.0
    if onGround and brakePosition > 0.05 and state.lastGroundSpeed ~= nil
        and groundElapsed > 0.0 and groundElapsed < 1.0 then
        local deceleration = math.max(0.0, (state.lastGroundSpeed - (groundSpeed or 0.0)) / groundElapsed)
        brakeDecelerationRaw = clamp01((deceleration - 0.12) / 2.88)
            * clamp01((brakePosition - 0.05) / 0.45)
    end
    local brakeSmoothing = brakeDecelerationRaw > state.filteredBrakeDeceleration
        and (1.0 - math.exp(-math.max(0.0, groundElapsed) / 0.08))
        or (1.0 - math.exp(-math.max(0.0, groundElapsed) / 0.22))
    state.filteredBrakeDeceleration = state.filteredBrakeDeceleration
        + (brakeDecelerationRaw - state.filteredBrakeDeceleration) * brakeSmoothing
    if not onGround or (groundSpeed or 0.0) < 0.18 then
        state.filteredBrakeDeceleration = 0.0
    end
    local brakeDeceleration = clamp01(state.filteredBrakeDeceleration)
    local brakeMotion = (groundSpeed or 0.0) >= 0.18
        and (0.28 + 0.72 * clamp01((groundSpeed or 0.0) / 8.0)) or 0.0
    local brakingIntensity = onGround and clamp01(
        math.sqrt(brakePosition) * brakeMotion + brakeDeceleration * 0.35) or 0.0
    local pitchRock = 0.0
    local rollRock = 0.0
    if hasStruts and state.lastStruts ~= nil then
        local priorPitch = state.lastStruts.tail - ((state.lastStruts.left + state.lastStruts.right) / 2.0)
        local pitch = tailStrut - ((leftStrut + rightStrut) / 2.0)
        pitchRock = clamp01(math.abs(pitch - priorPitch) * 20.0)
        local priorRoll = state.lastStruts.left - state.lastStruts.right
        local roll = leftStrut - rightStrut
        rollRock = clamp01(math.abs(roll - priorRoll) * 20.0)
    end
    if not onGround then
        suspensionMovement = 0.0
        pitchRock = 0.0
        rollRock = 0.0
    end
    local lateralMagnitude = math.abs(lateralSpeed or 0.0)
    -- DCS does not publish a wheel-lock or tire-slip flag. This is therefore
    -- deliberately limited to a derived *lateral wheel-skid risk*: meaningful
    -- brake demand, rolling speed, and measured body-axis lateral motion must
    -- all coincide. Normal straight-line braking remains separate.
    local wheelSkid = onGround and not hasSkids and not hasSkidWeight
        and brakePosition >= 0.12 and (groundSpeed or 0.0) >= 2.0
        and clamp01((lateralMagnitude - 0.55) / 2.45)
            * clamp01((brakePosition - 0.12) / 0.55)
            * clamp01(((groundSpeed or 0.0) - 2.0) / 6.0) or 0.0
    local skidScrub = onGround and (hasSkids or hasSkidWeight) and clamp01(lateralMagnitude / 4.0) or 0.0
    local wheelShimmy = onGround and not hasSkids and not hasSkidWeight
        and clamp01(((groundSpeed or 0.0) / 18.0) * (0.25 + rollRock)) or 0.0
    local tailCaster = onGround and hasTailRotation and clamp01(lateralMagnitude / 3.0) or 0.0
    local weightOnGround = onGround and 1.0 or 0.0
    if hasStruts then
        if isUh60 then
            local normalizedTail = normalizedAboveBaseline(
                tailStrut, UH60_TAIL_STRUT_AIRBORNE_BASELINE)
            weightOnGround = onGround and clamp01((normalizedTail + clamp01(leftStrut)
                + clamp01(rightStrut)) / 3.0) or 0.0
        else
            weightOnGround = clamp01((tailStrut + leftStrut + rightStrut) / 3.0)
        end
    end
    -- A quiet continuous body resonance for a loaded helicopter on its gear.
    -- This is deliberately not an impact cue and remains present at a standstill.
    local groundResonance = onGround and clamp01(0.08 + weightOnGround * 0.16
        + speedFactor * 0.12 + suspensionMovement * 0.20) or 0.0

    if hasStruts then
        state.lastStruts = { tail = tailStrut, left = leftStrut, right = rightStrut }
    end
    if contacts ~= nil then state.lastContacts = contacts end
    state.lastGroundSampleAt = now
    state.lastGroundSpeed = groundSpeed or 0.0
    if verticalVelocity ~= nil then state.lastVerticalVelocity = verticalVelocity end

    return {
        groundSpeedMps = groundSpeed,
        altitudeAglM = altitudeAgl,
        wheelBrakesPosition = wheelBrakes,
        brakeSource = brakeSource,
        parkingBrakeOn = parkingBrakeOn,
        tailStrutCompression = tailStrut,
        leftStrutCompression = leftStrut,
        rightStrutCompression = rightStrut,
        strutCompressionRate = hasStruts and strutCompressionRate or nil,
        tailWheelRotation = tailRotation,
        leftWheelRotation = leftRotation,
        rightWheelRotation = rightRotation,
        skidFrontLeftCompression = skidFrontLeft,
        skidFrontRightCompression = skidFrontRight,
        skidRearLeftCompression = skidRearLeft,
        skidRearRightCompression = skidRearRight,
        leftSkidCompression = hasSkids and (skidFrontLeft + skidRearLeft) / 2.0 or nil,
        rightSkidCompression = hasSkids and (skidFrontRight + skidRearRight) / 2.0 or nil,
        skidFrontLeftContact = skidContacts and skidContacts.frontLeft and 1.0 or 0.0,
        skidFrontRightContact = skidContacts and skidContacts.frontRight and 1.0 or 0.0,
        skidRearLeftContact = skidContacts and skidContacts.rearLeft and 1.0 or 0.0,
        skidRearRightContact = skidContacts and skidContacts.rearRight and 1.0 or 0.0,
        skidFrontLeftContactPulse = now < state.skidFrontLeftPulseUntil and 1.0 or 0.0,
        skidFrontRightContactPulse = now < state.skidFrontRightPulseUntil and 1.0 or 0.0,
        skidRearLeftContactPulse = now < state.skidRearLeftPulseUntil and 1.0 or 0.0,
        skidRearRightContactPulse = now < state.skidRearRightPulseUntil and 1.0 or 0.0,
        strutSource = source,
        onGround = onGround and 1.0 or 0.0,
        tailWheelContact = contacts and contacts.tail and 1.0 or 0.0,
        leftWheelContact = contacts and contacts.left and 1.0 or 0.0,
        rightWheelContact = contacts and contacts.right and 1.0 or 0.0,
        tailWheelContactPulse = now < state.tailContactPulseUntil and 1.0 or 0.0,
        leftWheelContactPulse = now < state.leftContactPulseUntil and 1.0 or 0.0,
        rightWheelContactPulse = now < state.rightContactPulseUntil and 1.0 or 0.0,
        touchdownPulse = now < state.touchdownPulseUntil and 1.0 or 0.0,
        touchdownVerticalSpeedMps = state.touchdownVerticalSpeedMps,
        touchdownStrength = clamp01(state.touchdownVerticalSpeedMps / 4.0),
        suspensionMovementIntensity = suspensionMovement,
        groundRollIntensity = groundRollIntensity,
        groundResonanceIntensity = groundResonance,
        brakingIntensity = brakingIntensity,
        brakeDecelerationIntensity = brakeDeceleration,
        wheelSkidIntensity = wheelSkid,
        groundPitchRockIntensity = pitchRock,
        groundRollRockIntensity = rollRock,
        longitudinalGroundSpeedMps = longitudinalSpeed,
        lateralGroundSlipMps = lateralSpeed,
        skidScrubIntensity = skidScrub,
        wheelShimmyIntensity = wheelShimmy,
        tailWheelCasterIntensity = tailCaster,
        liftoffPulse = now < state.liftoffPulseUntil and 1.0 or 0.0,
        weightOnGroundIntensity = weightOnGround
    }, hasStruts, groundSpeed ~= nil,
        hasTailRotation and hasLeftRotation and hasRightRotation, hasSkids,
        contacts ~= nil and longitudinalSpeed ~= nil and lateralSpeed ~= nil
end

local function readUh60DoorValues(now, aircraftType)
    if aircraftType ~= "UH-60L" and aircraftType ~= "UH-60L_DAP" then return nil, false end
    local pilot, hasPilot = readDrawArgument(38)
    local copilot, hasCopilot = readDrawArgument(400)
    local leftCargo, hasLeftCargo = readDrawArgument(401)
    local rightCargo, hasRightCargo = readDrawArgument(402)
    local leftGunner, hasLeftGunner = readDrawArgument(403)
    local rightGunner, hasRightGunner = readDrawArgument(404)
    if not (hasPilot and hasCopilot and hasLeftCargo and hasRightCargo and hasLeftGunner and hasRightGunner) then
        return nil, false
    end
    local doors = { pilot = pilot, copilot = copilot, leftCargo = leftCargo, rightCargo = rightCargo,
        leftGunner = leftGunner, rightGunner = rightGunner }
    local movement = 0.0
    if state.lastDoors ~= nil and state.lastDoorSampleAt ~= nil then
        local elapsed = now - state.lastDoorSampleAt
        if elapsed > 0.0 then
            movement = clamp01((math.abs(pilot - state.lastDoors.pilot) + math.abs(copilot - state.lastDoors.copilot)
                + math.abs(leftCargo - state.lastDoors.leftCargo) + math.abs(rightCargo - state.lastDoors.rightCargo)
                + math.abs(leftGunner - state.lastDoors.leftGunner) + math.abs(rightGunner - state.lastDoors.rightGunner))
                / elapsed / 2.0)
        end
    end
    state.lastDoors, state.lastDoorSampleAt = doors, now
    return {
        pilotDoorPosition = clamp01(pilot), copilotDoorPosition = clamp01(copilot),
        leftCargoDoorPosition = clamp01(leftCargo), rightCargoDoorPosition = clamp01(rightCargo),
        leftGunnerDoorPosition = clamp01(leftGunner), rightGunnerDoorPosition = clamp01(rightGunner),
        doorMovementIntensity = movement
    }, true
end

local function readCountermeasureValues(now)
    local snares, available = readExport("LoGetSnares")
    if not available or type(snares) ~= "table" then
        state.lastChaff = nil
        state.lastFlare = nil
        return nil, false
    end

    local chaff = finiteNumber(snares.chaff)
    local flare = finiteNumber(snares.flare)
    if chaff == nil or flare == nil then return nil, false end

    if state.lastChaff ~= nil and chaff < state.lastChaff then
        state.chaffReleasedCount = state.lastChaff - chaff
        state.chaffPulseUntil = now + EVENT_PULSE_SECONDS
    elseif now >= state.chaffPulseUntil then
        state.chaffReleasedCount = 0.0
    end
    if state.lastFlare ~= nil and flare < state.lastFlare then
        state.flareReleasedCount = state.lastFlare - flare
        state.flarePulseUntil = now + EVENT_PULSE_SECONDS
    elseif now >= state.flarePulseUntil then
        state.flareReleasedCount = 0.0
    end
    state.lastChaff, state.lastFlare = chaff, flare

    return {
        chaffRemaining = chaff,
        flareRemaining = flare,
        chaffReleasePulse = now < state.chaffPulseUntil and 1.0 or 0.0,
        flareReleasePulse = now < state.flarePulseUntil and 1.0 or 0.0,
        chaffReleasedCount = state.chaffReleasedCount,
        flareReleasedCount = state.flareReleasedCount
    }, true
end

local function readCockpitArgument(argumentNumber)
    local getter = exportApi and exportApi.GetDevice or globalGetDevice
    if type(getter) ~= "function" then return nil, false end
    local okDevice, device = pcall(getter, 0)
    if not okDevice or device == nil then
        return nil, false
    end
    local okMethod, method = pcall(function() return device.get_argument_value end)
    if not okMethod or type(method) ~= "function" then return nil, false end
    local okValue, value = pcall(method, device, argumentNumber)
    value = finiteNumber(value)
    return value, okValue and value ~= nil
end

local function readUh60CockpitValues(aircraftType)
    if aircraftType ~= "UH-60L" and aircraftType ~= "UH-60L_DAP" then return nil, false end
    local masterArm, hasMasterArm = readCockpitArgument(488)
    local selector, hasSelector = readCockpitArgument(489)
    local rocketPair, hasRocketPair = readCockpitArgument(486)
    local rocketRipple, hasRocketRipple = readCockpitArgument(487)
    local cmArm, hasCmArm = readCockpitArgument(559)
    local cmMode, hasCmMode = readCockpitArgument(560)
    if not (hasMasterArm and hasSelector and hasRocketPair and hasRocketRipple and hasCmArm and hasCmMode) then
        return nil, false
    end
    local selectorIndex = math.floor(selector * 4.0 + 0.5)
    local names = { [0] = "OFF", [1] = "GUN", [2] = "RKT", [3] = "MSL", [4] = "ATAS" }
    return {
        masterArmOn = masterArm > 0.5,
        weaponSelectorPosition = selectorIndex,
        weaponSelectorName = names[selectorIndex] or "UNKNOWN",
        rocketPairMode = rocketPair > 0.5,
        rocketRippleMode = rocketRipple > 0.5,
        countermeasureArmed = cmArm > 0.5,
        countermeasureMode = cmMode
    }, true
end

local function readAh64CockpitValues(aircraftType)
    if aircraftType ~= "AH-64D_BLK_II" then return nil, false end
    local armed, hasArmed = readCockpitArgument(413)
    local safe, hasSafe = readCockpitArgument(414)
    local weaponUd, hasWeaponUd = readCockpitArgument(526)
    local weaponLr, hasWeaponLr = readCockpitArgument(527)
    local cmwsArm, hasCmwsArm = readCockpitArgument(614)
    local cmwsMode, hasCmwsMode = readCockpitArgument(615)
    if not (hasArmed and hasSafe and hasWeaponUd and hasWeaponLr and hasCmwsArm and hasCmwsMode) then
        return nil, false
    end

    -- The Apache cyclic weapon-action switch is momentary. Publish the action
    -- that is physically held, never a fabricated persistent selection.
    local selectorPosition, selectorName = 0.0, "NONE"
    if weaponUd > 0.5 then selectorPosition, selectorName = 1.0, "GUN ACTION"
    elseif weaponUd < -0.5 then selectorPosition, selectorName = 4.0, "ATA ACTION"
    elseif weaponLr > 0.5 then selectorPosition, selectorName = 2.0, "RKT ACTION"
    elseif weaponLr < -0.5 then selectorPosition, selectorName = 3.0, "MSL ACTION" end

    return {
        masterArmOn = armed > 0.5 and safe < 0.5,
        weaponSelectorPosition = selectorPosition,
        weaponSelectorName = selectorName,
        rocketPairMode = false,
        rocketRippleMode = false,
        countermeasureArmed = cmwsArm > 0.5,
        countermeasureMode = cmwsMode
    }, true
end

local function readUh1CockpitValues(aircraftType)
    if aircraftType ~= "UH-1H" then return nil, false end
    local masterArm, hasMasterArm = readCockpitArgument(252)
    local gunSelector, hasGunSelector = readCockpitArgument(253)
    if not (hasMasterArm and hasGunSelector) then return nil, false end
    local selectorIndex = math.floor(math.max(-1.0, math.min(1.0, gunSelector)) + 1.5)
    local names = { [0] = "LEFT GUN", [1] = "BOTH GUNS", [2] = "RIGHT GUN" }
    return {
        masterArmOn = masterArm > 0.75,
        weaponSelectorPosition = selectorIndex,
        weaponSelectorName = names[selectorIndex] or "UNKNOWN",
        rocketPairMode = false,
        rocketRippleMode = false,
        countermeasureArmed = false,
        countermeasureMode = 0.0
    }, true
end

local function readCockpitValues(aircraftType)
    local values, available = readUh60CockpitValues(aircraftType)
    if available then return values, true end
    values, available = readAh64CockpitValues(aircraftType)
    if available then return values, true end
    return readUh1CockpitValues(aircraftType)
end

local function readDynamicsValues(now, modelTime, aircraftType, engineRpm, acceleration, angularVelocity,
    trueAirSpeed, verticalVelocity, shakeAmplitude, ground, weapons)
    local nominalRpm, bladeCount = nil, nil
    local rotorRpm, rotorSource = nil, ""
    local collective, hasCollective = nil, false
    local hasRotor = false
    local lifecycleAvailable = false
    local torqueGauge, hasTorqueGauge = nil, false
    local rotorWarningAvailable = false
    local apuRunning, engine1Starter, engine2Starter, lowRotorWarning = false, false, false, false

    if aircraftType == "UH-60L" or aircraftType == "UH-60L_DAP" then
        nominalRpm, bladeCount = 258.0, 4.0
        local phase, hasPhase = readDrawArgument(37)
        local rotorSampleAt = modelTime or now
        if hasPhase and state.lastRotorPhase ~= nil and state.lastRotorSampleAt ~= nil then
            local elapsed = rotorSampleAt - state.lastRotorSampleAt
            if elapsed > 0.002 and elapsed < 0.25 then
                local delta = phase - state.lastRotorPhase
                if delta < -0.5 then delta = delta + 1.0 end
                if delta > 0.5 then delta = delta - 1.0 end
                local measuredRpm = math.abs(delta) * 60.0 / elapsed
                if measuredRpm <= nominalRpm * 1.35 then
                    state.filteredRotorRpm = state.filteredRotorRpm == nil and measuredRpm
                        or (state.filteredRotorRpm * 0.82 + measuredRpm * 0.18)
                    rotorRpm = state.filteredRotorRpm
                    rotorSource = "UH60RotorPhase"
                end
            end
        end
        if hasPhase and (state.lastRotorSampleAt == nil or rotorSampleAt > state.lastRotorSampleAt) then
            state.lastRotorPhase = phase
            state.lastRotorSampleAt = rotorSampleAt
        end
        if hasPhase and rotorRpm == nil and state.filteredRotorRpm ~= nil then
            -- DCS draw arguments stop advancing when simulation model time is
            -- paused. Hold the last valid phase-derived RPM instead of
            -- interpreting the frozen phase as a rotor shutdown.
            rotorRpm = state.filteredRotorRpm
            rotorSource = "UH60RotorPhase"
        end
        if rotorRpm == nil and engineRpm ~= nil then
            rotorRpm = clamp01(((engineRpm.left + engineRpm.right) / 2.0) / 100.0) * nominalRpm
            rotorSource = "UH60EngineRpmProxy"
        end

        collective, hasCollective = readCockpitArgument(3)
        if not hasCollective then collective, hasCollective = readDrawArgument(9) end

        local apu, hasApu = readCockpitArgument(383)
        local starter1, hasStarter1 = readCockpitArgument(339)
        local starter2, hasStarter2 = readCockpitArgument(342)
        local lowRotor, hasLowRotor = readCockpitArgument(314)
        lifecycleAvailable = hasApu and hasStarter1 and hasStarter2 and hasLowRotor
        if lifecycleAvailable then
            apuRunning = apu > 0.5
            engine1Starter = starter1 > 0.5
            engine2Starter = starter2 > 0.5
            lowRotorWarning = lowRotor > 0.5
        end
    elseif aircraftType == "AH-6J" or aircraftType == "MH-6J" then
        nominalRpm, bladeCount = 470.0, 5.0
        local n1, hasN1 = readCockpitArgument(311)
        if hasN1 then
            rotorRpm = clamp01(n1) * nominalRpm
            rotorSource = "AH6N1Proxy"
        elseif engineRpm ~= nil then
            rotorRpm = clamp01(((engineRpm.left + engineRpm.right) / 2.0) / 100.0) * nominalRpm
            rotorSource = "AH6EngineRpmProxy"
        end
        collective, hasCollective = readCockpitArgument(210)
    elseif aircraftType == "AH-64D_BLK_II" then
        nominalRpm, bladeCount = 289.0, 4.0
        -- The external rotor animation phase is not a stable one-revolution
        -- signal on the current AH-64D build; recorded derivatives can report
        -- less than 60% Nr while both engines are governed. LoGetEngineInfo
        -- provides a stable power-turbine RPM proxy and degrades cleanly when
        -- the module does not expose a dedicated rotor gauge.
        if engineRpm ~= nil then
            rotorRpm = clamp01(((engineRpm.left + engineRpm.right) / 2.0) / 100.0) * nominalRpm
            rotorSource = "AH64EngineRpmProxy"
        end
        collective, hasCollective = readCockpitArgument(474)
        if not hasCollective then collective, hasCollective = readCockpitArgument(475) end
    elseif aircraftType == "UH-1H" then
        nominalRpm, bladeCount = 324.0, 2.0
        local phase, hasPhase = readDrawArgument(40)
        if hasPhase and state.lastRotorPhase ~= nil and state.lastRotorSampleAt ~= nil then
            local elapsed = now - state.lastRotorSampleAt
            if elapsed > 0.002 and elapsed < 0.25 then
                local delta = phase - state.lastRotorPhase
                if delta < -0.5 then delta = delta + 1.0 end
                if delta > 0.5 then delta = delta - 1.0 end
                local measuredRpm = math.abs(delta) * 60.0 / elapsed
                if measuredRpm <= nominalRpm * 1.35 then
                    state.filteredRotorRpm = state.filteredRotorRpm == nil and measuredRpm
                        or (state.filteredRotorRpm * 0.82 + measuredRpm * 0.18)
                    rotorRpm = state.filteredRotorRpm
                    rotorSource = "UH1RotorPhase"
                end
            end
        end
        if hasPhase then
            state.lastRotorPhase = phase
            state.lastRotorSampleAt = now
        end
        if rotorRpm == nil then
            local rotorGauge, hasRotorGauge = readCockpitArgument(123)
            if hasRotorGauge then
                rotorRpm = clamp01(rotorGauge) * nominalRpm
                rotorSource = "UH1RotorGaugeProxy"
            end
        end
        collective, hasCollective = readCockpitArgument(200)
        if not hasCollective then collective, hasCollective = readCockpitArgument(207) end
        torqueGauge, hasTorqueGauge = readCockpitArgument(124)
        local lowRotor, hasLowRotor = readCockpitArgument(276)
        rotorWarningAvailable = hasLowRotor
        if hasLowRotor then lowRotorWarning = lowRotor > 0.5 end
    elseif aircraftType == "OH58D" then
        -- The compiled cockpit exposes relative NR but no stable public draw
        -- argument. Engine RPM is a bounded proxy pending live-log validation.
        nominalRpm, bladeCount = 395.0, 4.0
        if engineRpm ~= nil then
            rotorRpm = clamp01(((engineRpm.left + engineRpm.right) / 2.0) / 100.0) * nominalRpm
            rotorSource = "OH58EngineRpmProxy"
        end
        collective, hasCollective = readCockpitArgument(3)
    elseif aircraftType == "CH-47Fbl1" then
        -- The module SFM definition provides 225 RPM and six configured
        -- blades; a recorded CH-47F feed confirms both engine RPM channels.
        -- Until a direct rotor sensor is exposed, use mean engine RPM as an
        -- explicitly identified rotor-speed proxy. The aircraft definition
        -- identifies draw argument 396 as collective.
        nominalRpm, bladeCount = 225.0, 6.0
        if engineRpm ~= nil then
            rotorRpm = clamp01(((engineRpm.left + engineRpm.right) / 2.0) / 100.0) * nominalRpm
            rotorSource = "CH47EngineRpmProxy"
        end
        collective, hasCollective = readDrawArgument(396)
    end

    -- Draw-argument phase differentiation can produce isolated one-frame
    -- overspeed spikes. Preserve a small real overspeed margin, but prevent a
    -- single sample from becoming an audible blade-pass frequency jump.
    if rotorRpm ~= nil and nominalRpm ~= nil then
        rotorRpm = math.min(rotorRpm, nominalRpm * 1.02)
    end
    hasRotor = rotorRpm ~= nil and nominalRpm ~= nil and bladeCount ~= nil
    local rotorPct = hasRotor and clamp01(rotorRpm / nominalRpm) or nil
    local elapsed = state.lastDynamicsSampleAt ~= nil and math.max(0.001, now - state.lastDynamicsSampleAt) or SEND_INTERVAL_SECONDS
    local collectiveRate = 0.0
    if hasCollective and state.lastCollective ~= nil then
        collectiveRate = math.abs(collective - state.lastCollective) / elapsed
    end

    local rotorSpool = 0.0
    local spoolSampleAt = modelTime or now
    if rotorPct ~= nil and (state.lastSpoolSampleAt == nil or spoolSampleAt > state.lastSpoolSampleAt) then
        if state.spoolRotorRpmPct == nil then
            state.spoolRotorRpmPct = rotorPct
        else
            local spoolElapsed = spoolSampleAt - state.lastSpoolSampleAt
            if spoolElapsed > 0.0 then
                local smoothing = 1.0 - math.exp(-spoolElapsed / ROTOR_SPOOL_SMOOTHING_SECONDS)
                local previousSmoothedPct = state.spoolRotorRpmPct
                state.spoolRotorRpmPct = previousSmoothedPct
                    + (rotorPct - previousSmoothedPct) * smoothing
                local smoothedRate = math.abs(
                    state.spoolRotorRpmPct - previousSmoothedPct) / spoolElapsed
                rotorSpool = clamp01((smoothedRate - ROTOR_SPOOL_RATE_DEADBAND)
                    / ROTOR_SPOOL_RATE_FULL_SCALE)
            end
        end
        state.lastSpoolSampleAt = spoolSampleAt
    end
    local onGround = ground ~= nil and ground.onGround == 1.0
    local weaponBurstActive = weapons ~= nil and weapons.weaponBurstActive == 1.0
    local weaponRecoilActive = weapons ~= nil and (weaponBurstActive
        or (weapons.lightGunIntensity or 0.0) > 0.01
        or (weapons.heavyGunIntensity or 0.0) > 0.01)
    local normalG = acceleration and (acceleration.y or 1.0) or nil
    local normalGDeviation = normalG ~= nil and normalG - 1.0 or nil
    local positiveGIntensity = normalGDeviation ~= nil and clamp01(normalGDeviation / 0.70) or 0.0
    local negativeGIntensity = normalGDeviation ~= nil and clamp01(-normalGDeviation / 0.50) or 0.0
    local accelerationDeparture = normalGDeviation ~= nil and math.abs(normalGDeviation) or 0.0
    local maneuverLoad = acceleration and clamp01(accelerationDeparture / 1.5) or 0.0
    local maneuverDeadband = 0.08
    local maneuverFullScale = (aircraftType == "UH-60L" or aircraftType == "UH-60L_DAP") and 0.70 or 1.20
    local maneuverNormalized = clamp01((accelerationDeparture - maneuverDeadband)
        / math.max(0.01, maneuverFullScale - maneuverDeadband))
    local maneuverResponseRaw = maneuverNormalized * maneuverNormalized * (3.0 - 2.0 * maneuverNormalized)
    -- Preserve a prompt onset while preventing normal-load noise from making
    -- sustained maneuver feedback feel scratchy.
    local maneuverTimeConstant = maneuverResponseRaw > state.filteredManeuverResponse and 0.08 or 0.22
    local maneuverSmoothing = 1.0 - math.exp(-elapsed / maneuverTimeConstant)
    state.filteredManeuverResponse = state.filteredManeuverResponse
        + (maneuverResponseRaw - state.filteredManeuverResponse) * maneuverSmoothing
    local maneuverResponse = clamp01(state.filteredManeuverResponse)

    -- A separate, restrained lateral cue covers roll, sideslip, and pedal
    -- maneuvers without altering the normal-G response.
    local lateralAcceleration = acceleration and math.abs(acceleration.z or 0.0) or 0.0
    local lateralNormalized = clamp01((lateralAcceleration - 0.025) / 0.095)
    local rollRate = angularVelocity and math.abs(angularVelocity.x or 0.0) or 0.0
    local yawRate = angularVelocity and math.abs(angularVelocity.y or 0.0) or 0.0
    local rollNormalized = clamp01((rollRate - 0.07) / 0.21)
    local yawNormalized = clamp01((yawRate - 0.09) / 0.23)
    local lateralRaw = not onGround and not weaponRecoilActive
        and math.max(lateralNormalized, rollNormalized * 0.82, yawNormalized * 0.45) or 0.0
    lateralRaw = lateralRaw * lateralRaw * (3.0 - 2.0 * lateralRaw)
    local lateralTimeConstant = lateralRaw > state.filteredLateralManeuver and 0.09 or 0.24
    local lateralSmoothing = 1.0 - math.exp(-elapsed / lateralTimeConstant)
    state.filteredLateralManeuver = state.filteredLateralManeuver
        + (lateralRaw - state.filteredLateralManeuver) * lateralSmoothing
    local lateralManeuver = clamp01(state.filteredLateralManeuver)
    local engineGovernorResponse = 0.0
    if engineRpm ~= nil then
        local engineAverage = ((engineRpm.left or 0.0) + (engineRpm.right or 0.0)) / 2.0
        if state.lastEngineRpmAverage ~= nil then
            engineGovernorResponse = clamp01(math.abs(engineAverage - state.lastEngineRpmAverage)
                / math.max(0.01, elapsed) / 8.0)
        end
        state.lastEngineRpmAverage = engineAverage
    end
    local turbulence = 0.0
    local rawDisturbance = 0.0
    local controlResponse = hasCollective and clamp01(collectiveRate / 1.25) or 0.0
    if not onGround and acceleration and angularVelocity and state.lastAcceleration and state.lastAngularVelocity then
        local accelerationChange = math.abs(acceleration.x - state.lastAcceleration.x)
            + math.abs(acceleration.y - state.lastAcceleration.y)
            + math.abs(acceleration.z - state.lastAcceleration.z)
        local angularChange = math.abs(angularVelocity.x - state.lastAngularVelocity.x)
            + math.abs(angularVelocity.y - state.lastAngularVelocity.y)
            + math.abs(angularVelocity.z - state.lastAngularVelocity.z)
        -- Telemetry deltas contain a small frame-to-frame noise floor. Remove
        -- it before normalizing so stable flight does not turn into a scratchy
        -- turbulence layer.
        local accelerationDisturbance = math.max(0.0, accelerationChange - 0.035) / 0.265
        local angularDisturbance = math.max(0.0, angularChange - 0.012) / 0.168
        rawDisturbance = clamp01(accelerationDisturbance + angularDisturbance)
        -- Separate a deliberate collective-driven response from residual
        -- turbulence/buffet.  We retain the raw disturbance for hard impacts
        -- because those are not accompanied by a collective transition.
        if not weaponRecoilActive then
            controlResponse = clamp01(controlResponse + angularChange / 2.5)
            turbulence = rawDisturbance * (1.0 - math.min(0.55, controlResponse * 0.55))
        else
            -- Gun recoil is already represented by the weapon effects. Keep
            -- real collective response, but do not reinterpret recoil motion
            -- as weather turbulence, buffet, or an airframe impact.
            rawDisturbance = 0.0
        end
    end
    local airspeed = trueAirSpeed or 0.0
    if turbulence < 0.035 then turbulence = 0.0 end
    local buffetRaw = math.max(shakeAmplitude or 0.0, turbulence * clamp01(airspeed / 35.0))
    local airframeBuffet = not onGround and not weaponRecoilActive
        and clamp01(math.max(0.0, buffetRaw - 0.03) / 0.97) or 0.0
    local rotorLoad = hasCollective and clamp01(collective * 0.72 + maneuverLoad * 0.28) or maneuverLoad
    local transmissionLoad = rotorPct ~= nil and clamp01(rotorLoad * rotorPct) or 0.0
    local droopRaw = rotorPct ~= nil and hasCollective
        and clamp01(collective * math.max(0.0, 1.0 - rotorPct) * 4.0) or 0.0
    local droop = droopRaw > 0.025 and clamp01((droopRaw - 0.025) / 0.975) or 0.0
    -- This is a derived VRS-risk model, not a DCS vortex-ring-state export.
    -- It deliberately requires the coupled flight state: a sustained low-speed
    -- descent, high collective/rotor load, a governed rotor, and airframe
    -- instability. Ground effect attenuates the cue when AGL is available.
    local descentRate = math.max(0.0, -(verticalVelocity or 0.0))
    local vrsRaw = 0.0
    if not onGround and hasCollective and rotorPct ~= nil and rotorPct >= 0.85 then
        local speedRisk = clamp01((10.0 - airspeed) / 6.0)
        local descentRisk = clamp01((descentRate - 1.2) / 3.8)
        local collectiveDemand = clamp01((collective - 0.55) / 0.30)
        local loadDemand = clamp01((rotorLoad - 0.45) / 0.40)
        local powerRisk = clamp01(collectiveDemand * 0.65 + loadDemand * 0.35)
        local rotorGoverned = clamp01((rotorPct - 0.85) / 0.10)
        local groundEffect = 1.0
        local agl = ground and ground.altitudeAglM or nil
        if agl ~= nil then groundEffect = 0.30 + 0.70 * clamp01((agl - 2.0) / 8.0) end
        local instability = 0.0
        if acceleration ~= nil then
            local heave = math.abs((acceleration.y or 1.0) - 1.0)
            local lateral = math.sqrt((acceleration.x or 0.0) ^ 2 + (acceleration.z or 0.0) ^ 2)
            instability = math.max(instability, clamp01(heave / 0.20), clamp01(lateral / 0.16))
        end
        if angularVelocity ~= nil then
            local angular = math.sqrt((angularVelocity.x or 0.0) ^ 2
                + (angularVelocity.y or 0.0) ^ 2 + (angularVelocity.z or 0.0) ^ 2)
            instability = math.max(instability, clamp01(angular / 0.35))
        end
        vrsRaw = speedRisk * descentRisk * powerRisk * rotorGoverned * groundEffect
            * (0.70 + instability * 0.30)
    end
    if vrsRaw >= 0.12 then
        state.vrsExposureSeconds = math.min(3.0, state.vrsExposureSeconds + elapsed)
    else
        state.vrsExposureSeconds = math.max(0.0, state.vrsExposureSeconds - elapsed * 1.8)
    end
    local vrsQualified = vrsRaw * clamp01((state.vrsExposureSeconds - 0.45) / 1.20)
    local vrsTimeConstant = vrsQualified > state.filteredVrsRisk and 0.30 or 0.85
    local vrsSmoothing = 1.0 - math.exp(-elapsed / vrsTimeConstant)
    state.filteredVrsRisk = state.filteredVrsRisk
        + (vrsQualified - state.filteredVrsRisk) * vrsSmoothing
    local settling = clamp01(state.filteredVrsRisk)
    local airspeedTrend = 0.0
    if state.lastEtlAirspeed ~= nil then
        airspeedTrend = math.abs(airspeed - state.lastEtlAirspeed) / math.max(0.01, elapsed)
    end
    state.lastEtlAirspeed = airspeed
    local liftResponse = acceleration and clamp01(math.max(0.0, (acceleration.y or 1.0) - 1.0) / 0.18) or 0.0
    local transitionTrend = clamp01(math.max(liftResponse, airspeedTrend / 5.0, collectiveRate / 0.9))
    local translational = not onGround and clamp01(1.0 - math.abs(airspeed - 11.0) / 4.0)
        * clamp01((collective or 0.0) + 0.25) * (0.35 + transitionTrend * 0.65) or 0.0

    -- Blade slap is represented as a conservative blade-vortex-interaction
    -- cue, not as a direct measurement.  Gate it to an airborne, forward,
    -- partial-power descent with the rotor near governed speed. This keeps
    -- ordinary collective changes, turbulence, hover, and climb from being
    -- mislabeled as blade slap.
    local bladeSlapRaw = 0.0
    if not onGround and rotorPct ~= nil and rotorPct > 0.80
        and airspeed > 7.0 and descentRate > 0.75 then
        local descentWindow = clamp01((descentRate - 0.75) / 1.75)
            * clamp01((7.0 - descentRate) / 2.0)
        local speedWindow = clamp01((airspeed - 7.0) / 13.0)
            * clamp01((60.0 - airspeed) / 20.0)
        local governedRotor = clamp01((rotorPct - 0.80) / 0.15)
        local vortexStrength = 0.40 + rotorLoad * 0.60
        local partialPower = hasCollective
            and (0.65 + clamp01((0.85 - collective) / 0.35) * 0.35) or 0.75
        bladeSlapRaw = clamp01(descentWindow * speedWindow * governedRotor
            * vortexStrength * partialPower)
    end
    local bladeSlapSmoothing = 1.0 - math.exp(-elapsed / 0.18)
    state.filteredBladeSlap = state.filteredBladeSlap
        + (bladeSlapRaw - state.filteredBladeSlap) * bladeSlapSmoothing
    local bladeSlap = clamp01(state.filteredBladeSlap)

    if not onGround and not weaponRecoilActive and maneuverResponse > 0.82
        and state.lastManeuverLoad <= 0.82 and state.lastAcceleration ~= nil then
        state.hardManeuverPulseUntil = now + EVENT_PULSE_SECONDS
    end
    if rawDisturbance > 0.82 and state.lastTurbulence <= 0.82
        and not onGround and not weaponRecoilActive then
        state.airframeImpactPulseUntil = now + EVENT_PULSE_SECONDS
    end

    if hasCollective then state.lastCollective = collective end
    if acceleration ~= nil then state.lastAcceleration = acceleration end
    if angularVelocity ~= nil then state.lastAngularVelocity = angularVelocity end
    state.lastManeuverLoad = maneuverResponse
    state.lastTurbulence = rawDisturbance
    state.lastDynamicsSampleAt = now

    return {
        mainRotorRpmPct = rotorPct ~= nil and rotorPct * 100.0 or nil,
        mainRotorRpm = rotorRpm,
        mainRotorHz = rotorRpm ~= nil and rotorRpm / 60.0 or nil,
        bladePassHz = rotorRpm ~= nil and rotorRpm * bladeCount / 60.0 or nil,
        rotorSource = rotorSource,
        collectivePosition = hasCollective and clamp01(collective) or nil,
        collectiveMovementRate = hasCollective and collectiveRate or nil,
        engineTorqueLeftPct = nil,
        engineTorqueRightPct = nil,
        torqueGaugeNormalized = hasTorqueGauge and clamp01(torqueGauge) or nil,
        rotorLoadIntensity = rotorLoad,
        transmissionLoadIntensity = transmissionLoad,
        engineGovernorResponseIntensity = engineGovernorResponse,
        rotorDroopIntensity = droop,
        rotorSpoolIntensity = rotorSpool,
        turbulenceIntensity = turbulence,
        controlResponseIntensity = controlResponse,
        airframeBuffetIntensity = airframeBuffet,
        maneuverLoadIntensity = maneuverLoad,
        normalG = normalG,
        normalGDeviation = normalGDeviation,
        positiveGIntensity = positiveGIntensity,
        negativeGIntensity = negativeGIntensity,
        maneuverResponseIntensity = maneuverResponse,
        lateralManeuverIntensity = lateralManeuver,
        settlingWithPowerCue = settling,
        vrsRiskIntensity = settling,
        translationalLiftTransition = translational,
        bladeSlapIntensity = bladeSlap,
        hardManeuverPulse = now < state.hardManeuverPulseUntil and 1.0 or 0.0,
        airframeImpactPulse = now < state.airframeImpactPulseUntil and 1.0 or 0.0,
        rotorRunning = rotorPct ~= nil and rotorPct > 0.15,
        apuRunning = apuRunning,
        engine1StarterActive = engine1Starter,
        engine2StarterActive = engine2Starter,
        lowRotorRpmWarning = lowRotorWarning
    }, hasRotor, hasCollective, false, hasRotor and hasCollective,
        acceleration ~= nil and angularVelocity ~= nil, lifecycleAvailable,
        hasTorqueGauge, rotorWarningAvailable
end

local function weaponTypeKey(weapon)
    if type(weapon) ~= "table" then
        return "unknown"
    end

    return table.concat({
        tostring(weapon.level1 or ""),
        tostring(weapon.level2 or ""),
        tostring(weapon.level3 or ""),
        tostring(weapon.level4 or "")
    }, ":")
end

local function weaponName(weapon, aircraftType, stationIndex)
    if type(weapon) ~= "table" then
        return ""
    end

    local key = weaponTypeKey(weapon)
    local numericStation = tonumber(stationIndex)
    local cacheKey = tostring(aircraftType or "") .. "|" .. tostring(numericStation or "") .. "|" .. key
    local cached = state.weaponNameCache[cacheKey]
    if cached ~= nil then
        return cached
    end

    -- The UH-60L DAP mod reports its M134 installation with this shell-type
    -- identifier, while LoGetNameByType returns UNKNOWN_4,6,10,46. Preserve
    -- the exported station and ammunition behavior but give the effect
    -- classifier the real weapon family.
    if (aircraftType == "UH-60L" or aircraftType == "UH-60L_DAP")
        and key == "4:6:10:46" then
        state.weaponNameCache[cacheKey] = "M134 Minigun"
        return "M134 Minigun"
    end

    -- The DAP's gun stations share type 4,6,6,0. The station determines the
    -- actual installation: paired M230s on station 2 and M134 door guns on
    -- stations 3 and 5. Keep this adapter-local so another aircraft cannot
    -- inherit the mod-specific names.
    if aircraftType == "UH-60L_DAP" and key == "4:6:6:0" then
        if numericStation == 2 then
            state.weaponNameCache[cacheKey] = "Twin M230 30 mm Cannons"
            return "Twin M230 30 mm Cannons"
        end
        if numericStation == 3 or numericStation == 5 then
            state.weaponNameCache[cacheKey] = "M134 Door Gun"
            return "M134 Door Gun"
        end
    end

    if not exportApi then
        return ""
    end

    local nameFunction = exportApi.LoGetNameByType
    if type(nameFunction) ~= "function" then
        return ""
    end

    local ok, name = pcall(nameFunction,
        weapon.level1, weapon.level2, weapon.level3, weapon.level4)
    if ok and type(name) == "string" then
        state.weaponNameCache[cacheKey] = name
        return name
    end

    state.weaponNameCache[cacheKey] = ""
    return ""
end

local function isStationGunWeapon(weapon)
    return type(weapon) == "table" and finiteNumber(weapon.level2) == WS_TYPE_SHELL
end

local function crewGunSide(aircraftType, stationIndex, name)
    local numericIndex = tonumber(stationIndex)
    if (aircraftType == "UH-60L" or aircraftType == "UH-60L_DAP") and numericIndex == 3 then
        return "left"
    end
    if (aircraftType == "UH-60L" or aircraftType == "UH-60L_DAP") and numericIndex == 5 then
        return "right"
    end

    if aircraftType == "UH-1H" then
        local lower = string.lower(name or "")
        if string.find(lower, "m134_l", 1, true) or string.find(lower, "left", 1, true)
            or numericIndex == 1 then return "left" end
        if string.find(lower, "m134_r", 1, true) or string.find(lower, "right", 1, true)
            or numericIndex == 4 then return "right" end
    end

    return nil
end

local function classifyWeapon(name, isGun, crewSide)
    if crewSide ~= nil then return "LIGHT_GUN" end
    local lower = string.lower(name or "")
    if isGun and (string.find(lower, "m134", 1, true)
        or string.find(lower, "minigun", 1, true)
        or string.find(lower, "m60", 1, true)
        or string.find(lower, "m240", 1, true)) then
        return "LIGHT_GUN"
    end
    if isGun then return "HEAVY_GUN" end
    if string.find(lower, "hellfire", 1, true) or string.find(lower, "missile", 1, true)
        or string.find(lower, "agm", 1, true) or string.find(lower, "aim", 1, true)
        or string.find(lower, "atas", 1, true) then
        return "MISSILE"
    end
    if string.find(lower, "hydra", 1, true) or string.find(lower, "rocket", 1, true)
        or string.find(lower, "m151", 1, true) or string.find(lower, "m229", 1, true)
        or string.find(lower, "m257", 1, true) or string.find(lower, "lau", 1, true) then
        return "ROCKET"
    end
    return "PAYLOAD"
end

local function nominalWeaponRateHz(name)
    local lower = string.lower(name or "")
    if string.find(lower, "twin m230", 1, true) then return 625.0 * 2.0 / 60.0 end
    if string.find(lower, "m230", 1, true) then return 625.0 / 60.0 end
    if string.find(lower, "m134 door gun", 1, true) then return 6000.0 / 60.0 end
    if string.find(lower, "gau-19", 1, true) or string.find(lower, "gau19", 1, true) then
        return 1300.0 / 60.0
    end
    if string.find(lower, "m3p", 1, true) then return 1100.0 / 60.0 end
    -- Keep rate limiting and error throttling alive even on an unexpected
    -- DCS-time failure. LuaSocket is required before this Hook starts.
    if state.socketLibrary and state.socketLibrary.gettime then
        local ok, value = pcall(state.socketLibrary.gettime)
        if ok and type(value) == "number" then return value end
    end

    -- Final fallback: prevent the sender from latching after one packet.
    return os.clock()
end

local function resetWeaponTracking()
    state.lastCannonShells = nil
    state.lastStations = nil
    state.weaponFirePulseUntil = -1.0
    state.gunFirePulseUntil = -1.0
    state.stationGunFirePulseUntil = -1.0
    state.crewGunFirePulseUntil = -1.0
    state.leftCrewGunFirePulseUntil = -1.0
    state.rightCrewGunFirePulseUntil = -1.0
    state.payloadReleasePulseUntil = -1.0
    state.gunRoundsFired = 0.0
    state.stationGunRoundsFired = 0.0
    state.crewGunRoundsFired = 0.0
    state.leftCrewGunRoundsFired = 0.0
    state.rightCrewGunRoundsFired = 0.0
    state.payloadReleasedCount = 0.0
    state.lastFiredWeaponStation = 0.0
    state.lastFiredWeaponName = ""
    state.weaponBurstStartedAt = nil
    state.weaponBurstLastAt = -1.0
    state.weaponBurstClass = "NONE"
    state.weaponRateHz = 0.0
    state.weaponImpulseStrength = 0.0
end

local function readWeaponValues(now, payloadInfo, aircraftType)
    if type(payloadInfo) ~= "table" then
        resetWeaponTracking()
        return nil, false, false, false, false
    end

    local currentStation = finiteNumber(payloadInfo.CurrentStation)
    local cannonShells = nil
    if type(payloadInfo.Cannon) == "table" then
        cannonShells = finiteNumber(payloadInfo.Cannon.shells)
    end

    local stations = {}
    local totalPayloadCount = 0.0
    local stationGunAmmoRemaining = 0.0
    local leftCrewGunAmmoRemaining = nil
    local rightCrewGunAmmoRemaining = nil
    local selectedWeaponName = ""
    local hasStationData = type(payloadInfo.Stations) == "table"
    local hasStationGuns = false
    local hasCrewGuns = false
    local availableClasses = {}

    if hasStationData then
        for stationIndex, station in pairs(payloadInfo.Stations) do
            if type(station) == "table" then
                local count = finiteNumber(station.count)
                if count ~= nil and count >= 0.0 then
                    local key = tostring(stationIndex)
                    local resolvedName = weaponName(station.weapon, aircraftType, stationIndex)
                    local stationIsGun = isStationGunWeapon(station.weapon)
                    stations[key] = {
                        count = count,
                        weaponType = weaponTypeKey(station.weapon),
                        weaponName = resolvedName,
                        isGun = stationIsGun,
                        crewSide = stationIsGun and crewGunSide(aircraftType, stationIndex, resolvedName) or nil
                    }
                    stations[key].weaponClass = classifyWeapon(
                        stations[key].weaponName, stations[key].isGun, stations[key].crewSide)
                    availableClasses[stations[key].weaponClass] = true
                    totalPayloadCount = totalPayloadCount + count

                    if stations[key].isGun then
                        hasStationGuns = true
                        stationGunAmmoRemaining = stationGunAmmoRemaining + count
                        if stations[key].crewSide == "left" then
                            hasCrewGuns = true
                            leftCrewGunAmmoRemaining = count
                        elseif stations[key].crewSide == "right" then
                            hasCrewGuns = true
                            rightCrewGunAmmoRemaining = count
                        end
                    end

                    if currentStation ~= nil and tonumber(stationIndex) == currentStation then
                        selectedWeaponName = stations[key].weaponName
                    end
                end
            end
        end
    end

    local gunRoundsFired = 0.0
    if cannonShells ~= nil and state.lastCannonShells ~= nil
        and cannonShells < state.lastCannonShells then
        gunRoundsFired = state.lastCannonShells - cannonShells
    end

    local stationGunRoundsFired = 0.0
    local crewGunRoundsFired = 0.0
    local leftCrewGunRoundsFired = 0.0
    local rightCrewGunRoundsFired = 0.0
    local payloadReleasedCount = 0.0
    local firedStation = nil
    local firedWeaponName = ""
    local firedWeaponClass = "NONE"
    if state.lastStations ~= nil then
        for key, station in pairs(stations) do
            local previous = state.lastStations[key]
            if previous ~= nil and previous.weaponType == station.weaponType
                and station.count < previous.count then
                local decrease = previous.count - station.count
                if station.isGun then
                    stationGunRoundsFired = stationGunRoundsFired + decrease
                    if station.crewSide == "left" then
                        crewGunRoundsFired = crewGunRoundsFired + decrease
                        leftCrewGunRoundsFired = leftCrewGunRoundsFired + decrease
                    elseif station.crewSide == "right" then
                        crewGunRoundsFired = crewGunRoundsFired + decrease
                        rightCrewGunRoundsFired = rightCrewGunRoundsFired + decrease
                    end
                else
                    payloadReleasedCount = payloadReleasedCount + decrease
                end

                local numericStation = tonumber(key)
                if numericStation ~= nil and (firedStation == nil or numericStation < firedStation) then
                    firedStation = numericStation
                    firedWeaponName = station.weaponName
                    firedWeaponClass = station.weaponClass
                end
            end
        end
    end

    local totalGunRoundsFired = gunRoundsFired + stationGunRoundsFired
    if totalGunRoundsFired > 0.0 then
        state.gunFirePulseUntil = now + WEAPON_PULSE_SECONDS
        state.weaponFirePulseUntil = now + WEAPON_PULSE_SECONDS
        state.gunRoundsFired = totalGunRoundsFired
        state.weaponEventCounter = state.weaponEventCounter + 1

        if firedStation == nil then
            state.lastFiredWeaponStation = 0.0
            state.lastFiredWeaponName = "Cannon"
            firedWeaponClass = "HEAVY_GUN"
        end
    end

    if stationGunRoundsFired > 0.0 then
        state.stationGunFirePulseUntil = now + WEAPON_PULSE_SECONDS
        state.stationGunRoundsFired = stationGunRoundsFired
    end

    if crewGunRoundsFired > 0.0 then
        state.crewGunFirePulseUntil = now + WEAPON_PULSE_SECONDS
        state.crewGunRoundsFired = crewGunRoundsFired
    end

    if leftCrewGunRoundsFired > 0.0 then
        state.leftCrewGunFirePulseUntil = now + WEAPON_PULSE_SECONDS
        state.leftCrewGunRoundsFired = leftCrewGunRoundsFired
    end

    if rightCrewGunRoundsFired > 0.0 then
        state.rightCrewGunFirePulseUntil = now + WEAPON_PULSE_SECONDS
        state.rightCrewGunRoundsFired = rightCrewGunRoundsFired
    end

    if payloadReleasedCount > 0.0 then
        state.payloadReleasePulseUntil = now + WEAPON_PULSE_SECONDS
        state.weaponFirePulseUntil = now + WEAPON_PULSE_SECONDS
        state.payloadReleasedCount = payloadReleasedCount
        state.weaponEventCounter = state.weaponEventCounter + 1
    end

    local eventAmount = totalGunRoundsFired + payloadReleasedCount
    if eventAmount > 0.0 then
        if state.weaponBurstStartedAt == nil or now - state.weaponBurstLastAt > 0.20
            or state.weaponBurstClass ~= firedWeaponClass then
            state.weaponBurstStartedAt = now
        end
        local sampleElapsed = state.weaponBurstLastAt >= 0.0 and math.max(0.01, now - state.weaponBurstLastAt)
            or SEND_INTERVAL_SECONDS
        local instantRate = totalGunRoundsFired > 0.0 and totalGunRoundsFired / sampleElapsed or 0.0
        state.weaponRateHz = state.weaponRateHz * 0.65 + instantRate * 0.35
        state.weaponBurstLastAt = now
        state.weaponBurstClass = firedWeaponClass
        if firedWeaponClass == "LIGHT_GUN" then
            state.weaponImpulseStrength = clamp01(0.30 + totalGunRoundsFired / 12.0)
        elseif firedWeaponClass == "HEAVY_GUN" then
            state.weaponImpulseStrength = clamp01(0.62 + totalGunRoundsFired / 8.0)
        elseif firedWeaponClass == "ROCKET" then
            state.weaponImpulseStrength = clamp01(0.72 + payloadReleasedCount * 0.12)
        elseif firedWeaponClass == "MISSILE" then
            state.weaponImpulseStrength = 1.0
        else
            state.weaponImpulseStrength = clamp01(0.55 + payloadReleasedCount * 0.10)
        end
    end

    if firedStation ~= nil then
        state.lastFiredWeaponStation = firedStation
        if firedWeaponName ~= "" then
            state.lastFiredWeaponName = firedWeaponName
        elseif crewGunSide(aircraftType, firedStation, firedWeaponName) ~= nil then
            state.lastFiredWeaponName = aircraftType == "UH-1H" and "UH-1H Crew Gun" or "UH-60L Door Gun"
        else
            state.lastFiredWeaponName = "Station " .. tostring(firedStation)
        end
    end

    if now >= state.gunFirePulseUntil then
        state.gunRoundsFired = 0.0
    end
    if now >= state.stationGunFirePulseUntil then
        state.stationGunRoundsFired = 0.0
    end
    if now >= state.crewGunFirePulseUntil then
        state.crewGunRoundsFired = 0.0
    end
    if now >= state.leftCrewGunFirePulseUntil then
        state.leftCrewGunRoundsFired = 0.0
    end
    if now >= state.rightCrewGunFirePulseUntil then
        state.rightCrewGunRoundsFired = 0.0
    end
    if now >= state.payloadReleasePulseUntil then
        state.payloadReleasedCount = 0.0
    end

    state.lastCannonShells = cannonShells
    state.lastStations = stations

    local burstActive = state.weaponBurstStartedAt ~= nil and now - state.weaponBurstLastAt <= 0.20
    local burstDurationMs = burstActive and math.max(0.0, now - state.weaponBurstStartedAt) * 1000.0 or 0.0
    if not burstActive then
        state.weaponRateHz = 0.0
        state.weaponImpulseStrength = 0.0
    end
    local activeClass = burstActive and state.weaponBurstClass or "NONE"
    local nominalRateHz = burstActive and nominalWeaponRateHz(state.lastFiredWeaponName) or 0.0
    local lightGunIntensity = activeClass == "LIGHT_GUN"
        and clamp01(0.25 + state.weaponRateHz / 70.0) or 0.0
    local heavyGunIntensity = activeClass == "HEAVY_GUN"
        and clamp01(0.55 + state.weaponRateHz / 35.0) or 0.0
    local rocketIntensity = activeClass == "ROCKET" and state.weaponImpulseStrength or 0.0
    local missileIntensity = activeClass == "MISSILE" and state.weaponImpulseStrength or 0.0

    local hasPayload = hasStationData or cannonShells ~= nil
    local loadoutParts = {}
    for _, className in ipairs({ "LIGHT_GUN", "HEAVY_GUN", "ROCKET", "MISSILE", "OTHER" }) do
        if availableClasses[className] then loadoutParts[#loadoutParts + 1] = className end
    end
    return {
        selectedStation = currentStation,
        selectedWeaponName = selectedWeaponName,
        totalPayloadCount = hasStationData and totalPayloadCount or nil,
        cannonShells = cannonShells,
        weaponFirePulse = now < state.weaponFirePulseUntil and 1.0 or 0.0,
        gunFirePulse = now < state.gunFirePulseUntil and 1.0 or 0.0,
        stationGunFirePulse = now < state.stationGunFirePulseUntil and 1.0 or 0.0,
        crewGunFirePulse = now < state.crewGunFirePulseUntil and 1.0 or 0.0,
        leftCrewGunFirePulse = now < state.leftCrewGunFirePulseUntil and 1.0 or 0.0,
        rightCrewGunFirePulse = now < state.rightCrewGunFirePulseUntil and 1.0 or 0.0,
        payloadReleasePulse = now < state.payloadReleasePulseUntil and 1.0 or 0.0,
        gunRoundsFired = state.gunRoundsFired,
        stationGunRoundsFired = state.stationGunRoundsFired,
        crewGunRoundsFired = state.crewGunRoundsFired,
        leftCrewGunRoundsFired = state.leftCrewGunRoundsFired,
        rightCrewGunRoundsFired = state.rightCrewGunRoundsFired,
        stationGunAmmoRemaining = hasStationGuns and stationGunAmmoRemaining or nil,
        leftCrewGunAmmoRemaining = leftCrewGunAmmoRemaining,
        rightCrewGunAmmoRemaining = rightCrewGunAmmoRemaining,
        payloadReleasedCount = state.payloadReleasedCount,
        lastFiredWeaponStation = state.lastFiredWeaponStation,
        lastFiredWeaponName = state.lastFiredWeaponName,
        weaponEventCounter = state.weaponEventCounter,
        weaponClass = activeClass,
        weaponBurstActive = burstActive and 1.0 or 0.0,
        weaponBurstDurationMs = burstDurationMs,
        weaponRateHz = state.weaponRateHz,
        nominalWeaponRateHz = nominalRateHz,
        weaponImpulseStrength = state.weaponImpulseStrength,
        lightGunIntensity = lightGunIntensity,
        heavyGunIntensity = heavyGunIntensity,
        rocketLaunchIntensity = rocketIntensity,
        missileLaunchIntensity = missileIntensity,
        loadoutSummary = table.concat(loadoutParts, ",")
    }, hasPayload, hasPayload, hasStationGuns, hasCrewGuns, hasPayload
end

local function aircraftFamily(aircraftType)
    if aircraftType == "UH-60L" or aircraftType == "UH-60L_DAP" then return "UH60" end
    if aircraftType == "AH-6J" or aircraftType == "MH-6J" then return "AH6" end
    if aircraftType == "AH-64D_BLK_II" then return "AH64" end
    if aircraftType == "UH-1H" then return "UH1" end
    if aircraftType == "OH58D" then return "OH58" end
    if aircraftType == "CH-47Fbl1" then return "CH47" end
    return aircraftType ~= "" and "OTHER" or ""
end

local function loadSocketLibrary()
    local originalPath = luaPackage.path
    local originalCPath = luaPackage.cpath

    luaPackage.path = originalPath .. ";.\\LuaSocket\\?.lua"
    luaPackage.cpath = originalCPath .. ";.\\LuaSocket\\?.dll"

    local ok, result = pcall(require, "socket")

    luaPackage.path = originalPath
    luaPackage.cpath = originalCPath

    if not ok then
        error(result)
    end

    return result
end

local function closeSender()
    if state.sender then
        pcall(state.sender.close, state.sender)
        state.sender = nil
    end
    if state.recorderSender then
        pcall(state.recorderSender.close, state.recorderSender)
        state.recorderSender = nil
    end
end

local function createPacket(now)
    local selfData = readExport("LoGetSelfData")
    local acceleration, hasAcceleration = readVectorExport("LoGetAccelerationUnits")
    local angularVelocity, hasAngularVelocity = readVectorExport("LoGetAngularVelocity")
    local modelTime = readNumberExport("LoGetModelTime")
    local indicatedAirSpeed, hasIndicatedAirSpeed = readNumberExport("LoGetIndicatedAirSpeed")
    local trueAirSpeed, hasTrueAirSpeed = readNumberExport("LoGetTrueAirSpeed")
    local verticalVelocity, hasVerticalVelocity = readNumberExport("LoGetVerticalVelocity")
    local angleOfAttack, hasAngleOfAttack = readNumberExport("LoGetAngleOfAttack")
    local mach, hasMach = readNumberExport("LoGetMachNumber")
    local shakeAmplitude, hasShakeAmplitude = readNumberExport("LoGetShakeAmplitude")
    local engineInfo = readExport("LoGetEngineInfo")
    local mechInfo = readExport("LoGetMechInfo")
    local payloadInfo = readExport("LoGetPayloadInfo")

    local engineRpm = nil
    if type(engineInfo) == "table" and type(engineInfo.RPM) == "table" then
        local left = finiteNumber(engineInfo.RPM.left)
        local right = finiteNumber(engineInfo.RPM.right)
        if left ~= nil and right ~= nil then
            engineRpm = { left = left, right = right }
        end
    end

    local gearPosition = componentValue(mechInfo, "gear")
    local flapsPosition = componentValue(mechInfo, "flaps")
    local speedbrakePosition = componentValue(mechInfo, "speedbrakes")
    local hasAirData = hasIndicatedAirSpeed and hasTrueAirSpeed and hasVerticalVelocity
        and hasAngleOfAttack and hasMach
    local aircraftType = selfData and selfData.Name or ""
    if aircraftType ~= state.lastAircraftType then
        resetWeaponTracking()
        resetGroundTracking()
        resetDynamicsTracking()
        state.lastChaff, state.lastFlare = nil, nil
        state.lastAircraftType = aircraftType
    end
    local weapons, hasWeaponPayload, hasWeaponEvents, hasStationGuns, hasCrewGuns, hasWeaponEffects =
        readWeaponValues(now, payloadInfo, aircraftType)
    local ground, hasGearStruts, hasGroundMotion, hasWheelRotation, hasSkids, hasDetailedGroundEffects =
        readGroundValues(now, aircraftType, mechInfo, verticalVelocity)
    local countermeasures, hasCountermeasures = readCountermeasureValues(now)
    local cockpit, hasCockpitStates = readCockpitValues(aircraftType)
    local doors, hasDoors = readUh60DoorValues(now, aircraftType)
    if cockpit ~= nil then cockpit.doors = doors end
    local dynamics, hasRotorDynamics, hasControlPositions, hasEngineTorque,
        hasPowertrainEffects, hasAerodynamicEffects, hasEngineLifecycle,
        hasTorqueGauge, hasRotorWarning =
        readDynamicsValues(now, modelTime, aircraftType, engineRpm, acceleration, angularVelocity,
            trueAirSpeed, verticalVelocity, shakeAmplitude, ground, weapons)

    state.sequence = state.sequence + 1

    return {
        protocol = PROTOCOL_VERSION,
        sessionId = state.sessionId,
        sequence = state.sequence,
        sentAtMs = now * 1000.0,
        modelTime = modelTime or 0.0,
        aircraft = {
            dcsType = aircraftType,
            family = aircraftFamily(aircraftType),
            groundType = hasSkids and "SKIDS" or (hasGearStruts and "WHEELS" or "UNKNOWN")
        },
        capabilities = {
            acceleration = hasAcceleration,
            angularVelocity = hasAngularVelocity,
            airData = hasAirData,
            engineRpm = engineRpm ~= nil,
            gear = gearPosition ~= nil,
            flaps = flapsPosition ~= nil,
            speedbrake = speedbrakePosition ~= nil,
            shakeAmplitude = hasShakeAmplitude,
            weaponPayload = hasWeaponPayload,
            weaponEvents = hasWeaponEvents,
            stationGuns = hasStationGuns,
            crewGuns = hasCrewGuns,
            gearStruts = hasGearStruts,
            groundMotion = hasGroundMotion,
            wheelRotation = hasWheelRotation,
            skids = hasSkids,
            countermeasures = hasCountermeasures,
            cockpitStates = hasCockpitStates,
            rotorDynamics = hasRotorDynamics,
            controlPositions = hasControlPositions,
            engineTorque = hasEngineTorque,
            torqueGauge = hasTorqueGauge,
            rotorWarning = hasRotorWarning,
            powertrainEffects = hasPowertrainEffects,
            aerodynamicEffects = hasAerodynamicEffects,
            bladeSlap = hasRotorDynamics and hasAirData,
            detailedGroundEffects = hasDetailedGroundEffects,
            brakes = ground ~= nil and ground.brakeSource ~= "",
            doors = hasDoors,
            weaponEffects = hasWeaponEffects,
            engineLifecycle = hasEngineLifecycle
        },
        values = {
            acceleration = acceleration,
            angularVelocity = angularVelocity,
            indicatedAirSpeedMps = indicatedAirSpeed,
            trueAirSpeedMps = trueAirSpeed,
            verticalVelocityMps = verticalVelocity,
            angleOfAttackRad = angleOfAttack,
            mach = mach,
            shakeAmplitude = shakeAmplitude,
            engineRpmPct = engineRpm,
            weapons = weapons,
            ground = ground,
            countermeasures = countermeasures,
            cockpit = cockpit,
            dynamics = dynamics,
            effects = {
                touchdownEnvelope = ground and ground.touchdownPulse * ground.touchdownStrength or 0.0,
                combatDamageEnvelope = dynamics and dynamics.airframeImpactPulse or 0.0,
                doorMotionEnvelope = cockpit and cockpit.doors and cockpit.doors.doorMovementIntensity or 0.0,
                brakeEnvelope = ground and ground.brakingIntensity or 0.0,
                weaponRecoilEnvelope = weapons and math.max(weapons.lightGunIntensity or 0.0, weapons.heavyGunIntensity or 0.0) or 0.0
            },
            mechanical = {
                gearPosition = gearPosition,
                flapsPosition = flapsPosition,
                speedbrakePosition = speedbrakePosition
            }
        }
    }
end

function callbacks.onSimulationStart()
    runSafely("onSimulationStart", function()
        closeSender()
        state.destinationPort, state.recorderDestinationPort = readPortConfiguration()
        state.socketLibrary = loadSocketLibrary()
        state.sender = assert(state.socketLibrary.udp())
        state.sender:settimeout(0)
        assert(state.sender:setpeername(DESTINATION_IP, state.destinationPort))
        -- A second best-effort local stream lets the flight recorder run at
        -- the same time as SimHub. Failure to create it must never interrupt
        -- the primary SimHub telemetry stream.
        local recorderOk, recorderSender = pcall(function()
            local socket = assert(state.socketLibrary.udp())
            socket:settimeout(0)
            return socket
        end)
        if recorderOk then
            state.recorderSender = recorderSender
        elseif logger and logger.write then
            pcall(logger.write, PLUGIN_NAME, logger.WARNING,
                "Recorder mirror unavailable: " .. tostring(recorderSender))
        end
        local sessionTime = state.socketLibrary.gettime and state.socketLibrary.gettime() or nowSeconds()
        state.sessionId = string.format("%.3f", sessionTime)
        state.sequence = 0
        state.lastSentAt = -1.0
        state.lastAircraftType = ""
        state.weaponEventCounter = 0
        resetWeaponTracking()
        resetGroundTracking()
        resetDynamicsTracking()
        state.lastChaff, state.lastFlare = nil, nil

        if logger and logger.write then
            pcall(logger.write, PLUGIN_NAME, logger.INFO,
                "Started; sending to " .. DESTINATION_IP .. ":" .. tostring(state.destinationPort)
                    .. " and recorder mirror " .. tostring(state.recorderDestinationPort))
        end
    end)
end

function callbacks.onSimulationFrame()
    runSafely("onSimulationFrame", function()
        if not state.sender then
            return
        end

        local now = nowSeconds()
        if state.lastSentAt >= 0.0 and now - state.lastSentAt < SEND_INTERVAL_SECONDS then
            return
        end

        state.lastSentAt = now
        local packet = createPacket(now)
        local payload = netApi.lua2json(packet)

        if type(payload) ~= "string" or #payload == 0 or #payload > 8192 then
            error("Telemetry serialization produced an invalid packet")
        end

        local bytesSent, sendError = state.sender:send(payload)
        if not bytesSent and sendError then
            error(sendError)
        end
        if state.recorderSender then
            -- UDP on localhost is intentionally best-effort. With no recorder
            -- listening, the mirrored datagram is simply discarded.
            pcall(state.recorderSender.sendto, state.recorderSender, payload,
                DESTINATION_IP, state.recorderDestinationPort)
        end
    end)
end

function callbacks.onSimulationStop()
    runSafely("onSimulationStop", function()
        closeSender()
        state.socketLibrary = nil
        state.lastAircraftType = ""
        resetWeaponTracking()
        resetGroundTracking()
        resetDynamicsTracking()
        state.lastChaff, state.lastFlare = nil, nil

        if logger and logger.write then
            pcall(logger.write, PLUGIN_NAME, logger.INFO, "Stopped")
        end
    end)
end

if not dcs or type(dcs.setUserCallbacks) ~= "function" then
    logError("registration", "DCS.setUserCallbacks is unavailable")
else
    local registered, registrationError = pcall(dcs.setUserCallbacks, callbacks)
    if not registered then
        logError("registration", registrationError)
    end
end
