namespace GradingSystem.Application.Options;

public class AwsOptions
{
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public string Region { get; set; } = "us-east-1";
    public string BedrockModelId { get; set; } = "anthropic.claude-3-5-sonnet-20241022-v2:0";
}
