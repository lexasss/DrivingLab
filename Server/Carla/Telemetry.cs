using System.Buffers.Binary;
using System.Diagnostics;

namespace Server.Carla;

public record Telemetry
{
    public float TotalTime { get; init; }
    public float LapTime { get; init; }
    public float LapDistance { get; init; }
    public float TotalDistance { get; init; }
    public float PositionY { get; init; }
    public float PositionZ { get; init; }
    public float PositionX { get; init; }
    public float Speed { get; init; }
    public float VelocityX { get; init; }
    public float VelocityY { get; init; }
    public float VelocityZ { get; init; }
    public float LeftDirX { get; init; }
    public float LeftDirY { get; init; }
    public float LeftDirZ { get; init; }
    public float ForwardDirX { get; init; }
    public float ForwardDirY { get; init; }
    public float ForwardDirZ { get; init; }
    public float SuspensionPositionBl { get; init; }
    public float SuspensionPositionBr { get; init; }
    public float SuspensionPositionFl { get; init; }
    public float SuspensionPositionFr { get; init; }
    public float SuspensionVelocityBl { get; init; }
    public float SuspensionVelocityBr { get; init; }
    public float SuspensionVelocityFl { get; init; }
    public float SuspensionVelocityFr { get; init; }
    public float WheelPatchSpeedBl { get; init; }
    public float WheelPatchSpeedBr { get; init; }
    public float WheelPatchSpeedFl { get; init; }
    public float WheelPatchSpeedFr { get; init; }
    public float ThrottleInput { get; init; }
    public float SteeringInput { get; init; }
    public float BrakeInput { get; init; }
    public float ClutchInput { get; init; }
    public float Gear { get; init; }
    public float GforceLateral { get; init; }
    public float GforceLongitudinal { get; init; }
    public float Lap { get; init; }
    public float EngineRate { get; init; }
    public float NativeSliSupport { get; init; }
    public float RacePosition { get; init; }
    public float KersLevel { get; init; }
    public float KersLevelMax { get; init; }
    public float Drs { get; init; }
    public float TractionControl { get; init; }
    public float Abs { get; init; }
    public float FuelInTank { get; init; }
    public float FuelCapacity { get; init; }
    public float InPits { get; init; }
    public float RaceSector { get; init; }
    public float SectorTime1 { get; init; }
    public float SectorTime2 { get; init; }
    public float BrakeTempBl { get; init; }
    public float BrakeTempBr { get; init; }
    public float BrakeTempFl { get; init; }
    public float BrakeTempFr { get; init; }
    public float TyrePressureBl { get; init; }
    public float TyrePressureBr { get; init; }
    public float TyrePressureFl { get; init; }
    public float TyrePressureFr { get; init; }
    public float LapsCompleted { get; init; }
    public float TotalLaps { get; init; }
    public float TrackLength { get; init; }
    public float LastLapTime { get; init; }
    public float MaxRpm { get; init; }
    public float IdleRpm { get; init; }
    public float MaxGears { get; init; }

    public static readonly string[] Fields =
    [
        "total_time",
        "lap_time",
        "lap_distance",
        "total_distance",
        "position_y",
        "position_z",
        "position_x",
        "speed",
        "velocity_x",
        "velocity_y",
        "velocity_z",
        "left_dir_x",
        "left_dir_y",
        "left_dir_z",
        "forward_dir_x",
        "forward_dir_y",
        "forward_dir_z",
        "suspension_position_bl",
        "suspension_position_br",
        "suspension_position_fl",
        "suspension_position_fr",
        "suspension_velocity_bl",
        "suspension_velocity_br",
        "suspension_velocity_fl",
        "suspension_velocity_fr",
        "wheel_patch_speed_bl",
        "wheel_patch_speed_br",
        "wheel_patch_speed_fl",
        "wheel_patch_speed_fr",
        "throttle_input",
        "steering_input",
        "brake_input",
        "clutch_input",
        "gear",
        "gforce_lateral",
        "gforce_longitudinal",
        "lap",
        "engine_rate",
        "native_sli_support",
        "race_position",
        "kers_level",
        "kers_level_max",
        "drs",
        "traction_control",
        "abs",
        "fuel_in_tank",
        "fuel_capacity",
        "in_pits",
        "race_sector",
        "sector_time_1",
        "sector_time_2",
        "brake_temp_bl",
        "brake_temp_br",
        "brake_temp_fl",
        "brake_temp_fr",
        "tyre_pressure_bl",
        "tyre_pressure_br",
        "tyre_pressure_fl",
        "tyre_pressure_fr",
        "laps_completed",
        "total_laps",
        "track_length",
        "last_lap_time",
        "max_rpm",
        "idle_rpm",
        "max_gears"
    ];

    public static Dictionary<string, float> AsDictionary(byte[] data)
    {
        var result = new Dictionary<string, float>(Fields.Length);

        Parse(data, (i, value) => result[Fields[i]] = value);

        return result;
    }

    public static Telemetry AsRecord(byte[] data)
    {
        float[] v = new float[Fields.Length];

        Parse(data, (i, value) => v[i] = value);

        return new Telemetry
        {
            TotalTime = v[0],
            LapTime = v[1],
            LapDistance = v[2],
            TotalDistance = v[3],
            PositionY = v[4],
            PositionZ = v[5],
            PositionX = v[6],
            Speed = v[7],
            VelocityX = v[8],
            VelocityY = v[9],
            VelocityZ = v[10],
            LeftDirX = v[11],
            LeftDirY = v[12],
            LeftDirZ = v[13],
            ForwardDirX = v[14],
            ForwardDirY = v[15],
            ForwardDirZ = v[16],
            SuspensionPositionBl = v[17],
            SuspensionPositionBr = v[18],
            SuspensionPositionFl = v[19],
            SuspensionPositionFr = v[20],
            SuspensionVelocityBl = v[21],
            SuspensionVelocityBr = v[22],
            SuspensionVelocityFl = v[23],
            SuspensionVelocityFr = v[24],
            WheelPatchSpeedBl = v[25],
            WheelPatchSpeedBr = v[26],
            WheelPatchSpeedFl = v[27],
            WheelPatchSpeedFr = v[28],
            ThrottleInput = v[29],
            SteeringInput = v[30],
            BrakeInput = v[31],
            ClutchInput = v[32],
            Gear = v[33],
            GforceLateral = v[34],
            GforceLongitudinal = v[35],
            Lap = v[36],
            EngineRate = v[37],
            NativeSliSupport = v[38],
            RacePosition = v[39],
            KersLevel = v[40],
            KersLevelMax = v[41],
            Drs = v[42],
            TractionControl = v[43],
            Abs = v[44],
            FuelInTank = v[45],
            FuelCapacity = v[46],
            InPits = v[47],
            RaceSector = v[48],
            SectorTime1 = v[49],
            SectorTime2 = v[50],
            BrakeTempBl = v[51],
            BrakeTempBr = v[52],
            BrakeTempFl = v[53],
            BrakeTempFr = v[54],
            TyrePressureBl = v[55],
            TyrePressureBr = v[56],
            TyrePressureFl = v[57],
            TyrePressureFr = v[58],
            LapsCompleted = v[59],
            TotalLaps = v[60],
            TrackLength = v[61],
            LastLapTime = v[62],
            MaxRpm = v[63],
            IdleRpm = v[64],
            MaxGears = v[65]
        };
    }

    public static Telemetry ToRecord(Dictionary<string, float> data)
    {
        return new Telemetry
        {
            TotalTime = data[Fields[0]],
            LapTime = data[Fields[1]],
            LapDistance = data[Fields[2]],
            TotalDistance = data[Fields[3]],
            PositionY = data[Fields[4]],
            PositionZ = data[Fields[5]],
            PositionX = data[Fields[6]],
            Speed = data[Fields[7]],
            VelocityX = data[Fields[8]],
            VelocityY = data[Fields[9]],
            VelocityZ = data[Fields[10]],
            LeftDirX = data[Fields[11]],
            LeftDirY = data[Fields[12]],
            LeftDirZ = data[Fields[13]],
            ForwardDirX = data[Fields[14]],
            ForwardDirY = data[Fields[15]],
            ForwardDirZ = data[Fields[16]],
            SuspensionPositionBl = data[Fields[17]],
            SuspensionPositionBr = data[Fields[18]],
            SuspensionPositionFl = data[Fields[19]],
            SuspensionPositionFr = data[Fields[20]],
            SuspensionVelocityBl = data[Fields[21]],
            SuspensionVelocityBr = data[Fields[22]],
            SuspensionVelocityFl = data[Fields[23]],
            SuspensionVelocityFr = data[Fields[24]],
            WheelPatchSpeedBl = data[Fields[25]],
            WheelPatchSpeedBr = data[Fields[26]],
            WheelPatchSpeedFl = data[Fields[27]],
            WheelPatchSpeedFr = data[Fields[28]],
            ThrottleInput = data[Fields[29]],
            SteeringInput = data[Fields[30]],
            BrakeInput = data[Fields[31]],
            ClutchInput = data[Fields[32]],
            Gear = data[Fields[33]],
            GforceLateral = data[Fields[34]],
            GforceLongitudinal = data[Fields[35]],
            Lap = data[Fields[36]],
            EngineRate = data[Fields[37]],
            NativeSliSupport = data[Fields[38]],
            RacePosition = data[Fields[39]],
            KersLevel = data[Fields[40]],
            KersLevelMax = data[Fields[41]],
            Drs = data[Fields[42]],
            TractionControl = data[Fields[43]],
            Abs = data[Fields[44]],
            FuelInTank = data[Fields[45]],
            FuelCapacity = data[Fields[46]],
            InPits = data[Fields[47]],
            RaceSector = data[Fields[48]],
            SectorTime1 = data[Fields[49]],
            SectorTime2 = data[Fields[50]],
            BrakeTempBl = data[Fields[51]],
            BrakeTempBr = data[Fields[52]],
            BrakeTempFl = data[Fields[53]],
            BrakeTempFr = data[Fields[54]],
            TyrePressureBl = data[Fields[55]],
            TyrePressureBr = data[Fields[56]],
            TyrePressureFl = data[Fields[57]],
            TyrePressureFr = data[Fields[58]],
            LapsCompleted = data[Fields[59]],
            TotalLaps = data[Fields[60]],
            TrackLength = data[Fields[61]],
            LastLapTime = data[Fields[62]],
            MaxRpm = data[Fields[63]],
            IdleRpm = data[Fields[64]],
            MaxGears = data[Fields[65]]
        };
    }

    public static byte[] Emulate()
    {
        var ts = (float)_stopwatch.Elapsed.TotalSeconds;
        var dt = ts - _time;
        _targetSpeed = _currentDrivingAction.TargetSpeed;

        var ds = (_targetSpeed - _speed) * 0.5f * dt;
        var newSpeed = Math.Clamp(_speed + ds, 0, MAX_SPEED);
        ds = newSpeed - _speed;
        _speed = newSpeed;

        _acceleration = MOTOR_INERTION * _acceleration + (1f - MOTOR_INERTION) * ds / dt;

        _distance += _speed * dt;
        _lapDistance += _speed * dt;

        if (_lapDistance > LAP_DISTANCE)
        {
            _laps++;
            _lapDistance %= LAP_DISTANCE;
            _lapTime = 0;
        }

        float[] values = [
            _time,
            _lapTime,
            _lapDistance,
            _distance,
            0, 0,
            _distance,
            _speed,
            _speed,
            0, 0, 0, 1, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            Math.Max(0, Math.Clamp(_acceleration / 6, 0, 1)),
            0,
            Math.Max(0, Math.Clamp(_acceleration / -10, 0, 1)),
            0, 0, 0,
            _acceleration,
            _laps + 1,
            1000 + _speed * 200,
            0, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1,
            _laps,
            int.MaxValue,
            0, 0,
            MAX_SPEED * 200,
            1000, 0
        ];

        _time = ts;
        _lapTime += dt;
        _currentDrivingActionTime += dt;

        if (_currentDrivingActionTime > _currentDrivingAction.Duration)
        {
            _currentDrivingActionIndex = (_currentDrivingActionIndex + 1) % _drivingActions.Length;
            _currentDrivingAction = _drivingActions[_currentDrivingActionIndex];
            _currentDrivingActionTime = 0;
        }

        int length = Fields.Length * sizeof(float);
        byte[] b = new byte[length];

        for (int i = 0; i < Fields.Length; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(
                b.AsSpan(i * sizeof(float), sizeof(float)),
                BitConverter.SingleToInt32Bits(values[i]));
        }

        return b;
    }

    #region Internal

    // Emulator
    const float LAP_DISTANCE = 2000;    // m
    const float MAX_SPEED = 40;         // m/s
    const float MOTOR_INERTION = 0.95f;

    static float _time = 0;
    static float _speed = 0;
    static float _distance = 0;
    static float _lapTime = 0;
    static float _lapDistance = 0;
    static int _laps = 0;
    static int _currentDrivingActionIndex = -1;
    static float _currentDrivingActionTime = 0;
    static DrivingAction _currentDrivingAction = new(5, 0);
    
    static float _targetSpeed = 0;
    static float _acceleration = 0;

    static Stopwatch _stopwatch = new();

    record class DrivingAction(
        float Duration,         // s
        float TargetSpeed);     // m/s, max 150
    static readonly DrivingAction[] _drivingActions = [
        new DrivingAction(30, 20),
        new DrivingAction(12, 15),
        new DrivingAction(7, 0),
    ];

    static Telemetry()
    {
        _stopwatch.Start();
    }

    private static void Parse(byte[] data, Action<int, float> cb)
    {
        int expectedLength = Fields.Length * sizeof(float);

        if (data.Length != expectedLength)
            throw new ArgumentException(
                $"Expected {expectedLength} bytes, got {data.Length}.");

        for (int i = 0; i < Fields.Length; i++)
        {
            int bits = BinaryPrimitives.ReadInt32LittleEndian(
                data.AsSpan(i * sizeof(float), sizeof(float)));

            float value = BitConverter.Int32BitsToSingle(bits);

            cb(i, value);
        }
    }

    #endregion
}