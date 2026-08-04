using GradingSystem.Application.Messaging;
using GradingSystem.Worker.Services.Lab;
using MassTransit;

namespace GradingSystem.Worker.Consumers;

public class LabGradeJobConsumer(LabGradingPipeline pipeline, ILogger<LabGradeJobConsumer> logger) : IConsumer<LabGradeJobMessage>
{
    public async Task Consume(ConsumeContext<LabGradeJobMessage> context)
    {
        logger.LogInformation("Received LabGradeJobMessage for job {JobId}", context.Message.JobId);
        await pipeline.ProcessAsync(context.Message.JobId, context.CancellationToken);
    }
}
