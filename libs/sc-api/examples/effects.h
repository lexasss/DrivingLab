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
	torque_Nm = 0,
	torque_relative,
	force_N,
	force_relative,
	position_mm
};

extern "C" {
	__declspec(dllexport) Pedal Init();
	__declspec(dllexport) void  Configure(Pedal pedal, OffsetType offset_type);
	__declspec(dllexport) void  Run(Pedal pedal, EffectType effectType, int durationMs, float amplitude);
}

}