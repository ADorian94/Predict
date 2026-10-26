namespace Predict.Services;

// Keep pending slots measurable; collapse only after a real load failure.
internal sealed class BannerSlotState
{
    private readonly double reservedHeight;

    public BannerSlotState(double reservedHeight)
    {
        this.reservedHeight = reservedHeight;
        Height = reservedHeight;
    }

    public double Height { get; private set; }
    public bool IsCollapsed { get; private set; }

    public void OnFailed()
    {
        Height = 0;
        IsCollapsed = true;
    }

    public void OnLoaded()
    {
        Height = reservedHeight;
        IsCollapsed = false;
    }
}
