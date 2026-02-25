namespace RazorSlices.Samples.WebApp.Slices.Ipsum;

public abstract class IpsumBase : RazorSlices.RazorSlice<IpsumModel>;

public record IpsumModel(string Content);