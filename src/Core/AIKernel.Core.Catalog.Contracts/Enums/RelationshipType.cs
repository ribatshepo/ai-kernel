namespace AIKernel.Core.Catalog.Contracts.Enums;

public enum RelationshipType
{
    Unknown = 0,
    DependsOn = 1,
    Produces = 2,
    Consumes = 3,
    Contains = 4,
    TrainedWith = 5,
    HasAccess = 6,
    DerivesFrom = 7,
    References = 8,
    Extends = 9
}
