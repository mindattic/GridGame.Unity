// Thermal state bridge for the thermal step-down (GG-T1548). The C# side is Assets/GridGame/App/Boot/ThermalStepDown.cs,
// which decides what to do with the reading; this file only returns NSProcessInfo.thermalState
// (0 nominal, 1 fair, 2 serious, 3 critical).
#import <Foundation/Foundation.h>

extern "C" int GGThermal_State(void)
{
    return (int)[[NSProcessInfo processInfo] thermalState];
}
