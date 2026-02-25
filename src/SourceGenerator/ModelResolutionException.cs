using System;

namespace RazorSlices.SourceGenerator;

/// <summary>
/// Represents an exception that occurs during the resolution of a model in the RazorSlices source generation process.
/// </summary>
/// <remarks>
/// This exception is used to indicate that the source generator failed to resolve the expected model
/// necessary for code generation. It can be used to provide additional context about the failure
/// during development or debugging.
/// </remarks>
public class ModelResolutionException: Exception
{
    /// <summary>
    /// Gets the name or identifier of the model that could not be resolved during the
    /// RazorSlices source generation process.
    /// </summary>
    /// <remarks>
    /// This property provides additional details about the specific resolution failure,
    /// enabling easier debugging and analysis when handling model resolution issues.
    /// </remarks>
    public string FailedResolution { get; }

    /// <summary>
    /// Represents an exception thrown when the source generator fails to resolve a required model.
    /// </summary>
    /// <remarks>
    /// This exception is specifically used within the RazorSlices source generation context to indicate
    /// issues related to model resolution during execution. It provides additional information about
    /// the failed resolution to facilitate debugging and error analysis.
    /// </remarks>
    public ModelResolutionException(string failedResolution)
    {
        FailedResolution = failedResolution;
    }
}