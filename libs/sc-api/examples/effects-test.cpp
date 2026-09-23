#include <iostream>

#include "effects.h"

using namespace sc_api::usage;

int main()
{
    std::cout << "Initializing...\n";

    Pedal pedals = Init();

    std::cout << "Init returned: " << static_cast<int>(pedals) << '\n';

    if (pedals & Brake) std::cout << "Brake available\n";

    if (pedals & Throttle) std::cout << "Throttle available\n";

    Configure(Pedal::Both, OffsetType::force_N);

    std::cout << "Running...\n";

    Run(Pedal::Both, EffectType::Periodic, 1000, 2.0f);

    std::cout << "Done.\n";

	return 0;
}
