public readonly struct BuildingConsumeResult
{
    public bool Success { get; }
    public string Message { get; }
    public BuildingPaymentSource PaymentSource { get; }

    public BuildingConsumeResult(
        bool success,
        string message,
        BuildingPaymentSource paymentSource)
    {
        Success = success;
        Message = message;
        PaymentSource = paymentSource;
    }

    public static BuildingConsumeResult Succeed(
        BuildingPaymentSource source,
        string message)
    {
        return new BuildingConsumeResult(true, message, source);
    }

    public static BuildingConsumeResult Fail(string message)
    {
        return new BuildingConsumeResult(
            false,
            message,
            BuildingPaymentSource.Unknown);
    }
}