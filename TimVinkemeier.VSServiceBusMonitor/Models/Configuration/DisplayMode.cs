namespace TimVinkemeier.VSServiceBusMonitor.Models.Configuration
{
    public enum DisplayMode
    {
        /// <summary>
        /// Show entity if it has active or DLQ messages.
        /// </summary>
        Default,

        /// <summary>
        /// Show entity always.
        /// </summary>
        Always,

        /// <summary>
        /// Show entity only if it has DLQ messages.
        /// </summary>
        OnlyDlq,

        /// <summary>
        /// Only show entity in tooltip.
        /// </summary>
        TooltipOnly
    }
}
