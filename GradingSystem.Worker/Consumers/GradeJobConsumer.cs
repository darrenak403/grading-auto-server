using GradingSystem.Application.Messaging;
using GradingSystem.Worker.Services;
using MassTransit;

namespace GradingSystem.Worker.Consumers;

public class GradeJobConsumer(GradingPipeline pipeline, ILogger<GradeJobConsumer> logger) : IConsumer<GradeJobMessage>
{
    public async Task Consume(ConsumeContext<GradeJobMessage> context)
    {
        var jobId = context.Message.GradingJobId;
        logger.LogInformation("Received GradeJobMessage for job {JobId}", jobId);
        await pipeline.ProcessAsync(jobId, context.CancellationToken);
    }
}
