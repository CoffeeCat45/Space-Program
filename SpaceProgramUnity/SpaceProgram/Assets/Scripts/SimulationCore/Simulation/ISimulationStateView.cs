interface ISimulationStateView

{

    string ActivePrimaryBodyName { get; }



    double GetShipSpeed();

    double GetShipHudSpeed();

    string GetShipHudSpeedModeLabel();

    double GetShipAcceleration();

    double GetShipMass();

    double GetShipFuelMass();

    double GetShipFuelRatio();

    double GetShipThrustAcceleration();

    double GetShipAvailableDeltaV();

    double GetShipAltitudeAboveActivePrimary();

    double GetShipAltitudeAbovePlanet();

    string GetActiveStageName();

    int GetActiveStageNumber();

    int GetRemainingStageCount();

    string GetLastStagingMessage();

    FlightStatus GetFlightStatus();

    string GetFlightStatusName();

    string GetLandedOnBodyName();

    string GetLastFlightEventMessage();

    double GetShipVerticalSpeedRelativeToActivePrimary();

    double GetShipSurfaceSpeedRelativeToActivePrimary();

    double GetShipGlideRatio();

    double GetShipReentryIntensity();

    bool TryGetDescentImpactPoint(out SimVector3 worldPosition);

    double GetShipRelativeSpeedToActivePrimary();

    double GetShipLocalGravityRelativeToActivePrimary();

    double GetShipWeightRelativeToActivePrimary();

    double GetShipAppliedThrustForce();

    double GetShipThrustToWeightRatio();

    double GetShipMaxThrustToWeightRatio();

    bool IsShipInAtmosphere();

    double GetShipAtmosphericDensity();

    double GetShipDragAcceleration();

    double GetShipDragForce();

    double GetShipLiftForce();

    double GetShipEffectiveDragArea();

    double GetShipDynamicPressure();

    double GetShipAngleOfAttackDegrees();

    double GetShipAtmosphereDepthFraction();

    double GetShipHeatingLevel();

    bool IsShipHeatingWarningActive();

    bool IsShipHeatingCritical();

    bool ShouldShowDescentPreview();

    bool IsAtmosphericPreviewEnabled();

    double GetRecommendedMaxTimeScale();

    string GetShipOrbitTypeRelativeToActivePrimary();

    double GetShipPeriapsisAltitudeRelativeToActivePrimary();

    double GetShipApoapsisAltitudeRelativeToActivePrimary();

    SimVector3 GetShipCircularizeDeltaVRelativeToActivePrimary();

    double GetShipOrbitalPeriodRelativeToActivePrimary();

    double GetShipTimeToPeriapsisRelativeToActivePrimary();

    double GetShipTimeToApoapsisRelativeToActivePrimary();

    double GetShipDistanceToBody(string bodyName);

    double GetDockingPortSeparationToTarget();

    double GetShipRelativeSpeedToBody(string bodyName);

    double GetShipDistanceToMoon();

    double GetShipDistanceToPlanetCenter();

    BodyState[] GetBodyStates();

    ShipBlockView[] GetShipBlocks();

    int GetShipLayoutRevision();

    int[] ConsumePendingDestroyedBlockIds();

    SimVector3 GetShipCenterOfMassWorld();

    SimVector3 GetShipCenterOfMassLocal();

    double GetShipEnvelopeRadius();

}

