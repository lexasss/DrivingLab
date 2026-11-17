using System.Buffers.Binary;

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

    static float _time = 0;
    static float _v = 0;
    static float _delta = 0.5f;
    static Random _rnd = new();
    public static byte[] Emulate()
    {
        int length = Fields.Length * sizeof(float);
        byte[] b = new byte[length];

        var rv = (_rnd.NextDouble() - 0.5) * 2;
        rv = Math.Pow(rv, 2) * (rv < 0 ? -1 : 1);
        _delta += (float)(rv / 2);
        _v += _delta;

        for (int i = 0; i < Fields.Length; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(
                b.AsSpan(i * sizeof(float), sizeof(float)),
                BitConverter.SingleToInt32Bits(i == 0 ? _time : _v));
        }

        _time += 0.02f;
        return b;
    }
}