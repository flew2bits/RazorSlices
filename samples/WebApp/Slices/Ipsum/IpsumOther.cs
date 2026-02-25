namespace RazorSlices.Samples.WebApp.Slices.Ipsum;

public abstract class IpsumOtherBase<TOther, TModel> : RazorSlices.RazorSlice<TModel> where TOther: class
{
    public TOther? OtherContent { get; set; } = null;
} 

public record IpsumOther(string Name);