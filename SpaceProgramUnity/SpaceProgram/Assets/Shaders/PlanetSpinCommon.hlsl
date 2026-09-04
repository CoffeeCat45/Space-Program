#ifndef PLANET_SPIN_COMMON_INCLUDED
#define PLANET_SPIN_COMMON_INCLUDED

float2 ApplyPlanetSpinToGeographicUv(float2 geographicUv, float spinLongitudeRadians)
{
    float spinU = spinLongitudeRadians * (0.5 / 3.14159265);
    return float2(geographicUv.x + spinU, geographicUv.y);
}

float2 FracGeographicUv(float2 geographicUv)
{
    return float2(frac(geographicUv.x), geographicUv.y);
}

float2 ApplyPlanetSpinToWrappedGeographicUv(float2 geographicUv, float spinLongitudeRadians)
{
    return FracGeographicUv(ApplyPlanetSpinToGeographicUv(geographicUv, spinLongitudeRadians));
}

#endif
