using DogEatDog.DependencyExplorer.Graph.Model;

namespace DogEatDog.DependencyExplorer.Roslyn;

internal sealed record MediatRMetadata(
    string DispatchFramework,
    MediatRDispatchKind DispatchKind,
    string RequestType,
    string? MediatorMethod,
    string? Resolution)
{
    public static MediatRMetadata From(MediatRDispatchResolution dispatchResolution, GraphEdgeType edgeType)
    {
        return edgeType switch
        {
            GraphEdgeType.DISPATCHES => new MediatRMetadata(
                DispatchFramework: "MediatR",
                DispatchKind: dispatchResolution.DispatchKind,
                RequestType: dispatchResolution.RequestTypeDisplayName,
                MediatorMethod: dispatchResolution.MediatorMethodDisplayName,
                Resolution: null),
            GraphEdgeType.HANDLED_BY => new MediatRMetadata(
                DispatchFramework: "MediatR",
                DispatchKind: dispatchResolution.DispatchKind,
                RequestType: dispatchResolution.RequestTypeDisplayName,
                MediatorMethod: null,
                Resolution: "request-handler"),
            _ => throw new ArgumentOutOfRangeException(nameof(edgeType), edgeType, "Only MediatR dispatch edges are supported.")
        };
    }

    public IReadOnlyDictionary<string, string?> ToDictionary()
    {
        var metadata = new Dictionary<string, string?>
        {
            ["dispatchFramework"] = DispatchFramework,
            ["dispatchKind"] = DispatchKind.ToMetadataValue(),
            ["requestType"] = RequestType
        };

        if (!string.IsNullOrWhiteSpace(MediatorMethod))
        {
            metadata["mediatorMethod"] = MediatorMethod;
        }

        if (!string.IsNullOrWhiteSpace(Resolution))
        {
            metadata["resolution"] = Resolution;
        }

        return metadata;
    }
}
