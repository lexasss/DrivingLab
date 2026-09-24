#pragma once

namespace sc_api::usage {

enum Pedal {
	None = 0,
	Brake = 1,
	Throttle = 2,
	Both = Brake | Throttle
};

enum EffectType {
    Constant = 0,
    Periodic = 1
};

enum OffsetType {
	TorqueNm = 0,
	TorqueRelative = 1,
	ForceN = 2,
	ForceRelative = 3,
	PositionMm = 4
};

enum PeriodicEffectType {
	Sine = 0,
	Triangle = 1,
	Square = 2,
	SawTooth = 3
};

extern "C" {
	__declspec(dllexport) Pedal Init();
	__declspec(dllexport) void  Configure(Pedal pedal, OffsetType offset_type);
	__declspec(dllexport) void  ConfigurePeriodic(PeriodicEffectType type, float frequency);
	__declspec(dllexport) void  Run(Pedal pedal, EffectType effectType, int durationMs, float amplitude);
    __declspec(dllexport) void  Stop(Pedal pedal);
}

}