using GradingSystem.Api.Controllers;
using GradingSystem.Application.DTOs;
using GradingSystem.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace GradingSystem.Tests.Api.Controllers;

public class AssignmentsControllerBulkUploadTests
{
    [Theory]
    [InlineData("class-submissions.zip")]
    [InlineData("PE_2026_ROUND_2.ZIP")]
    public async Task BulkUploadAsync_ArbitraryZipName_IsAccepted(string fileName)
    {
        var assignmentId = Guid.NewGuid();
        var bulkUpload = new Mock<IBulkUploadService>();
        bulkUpload
            .Setup(service => service.ParseAndCreateForLatestRoundAsync(
                assignmentId, It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BulkUploadResultDto());
        var controller = CreateController(bulkUpload.Object);

        var result = await controller.BulkUploadAsync(
            assignmentId, CreateFile(fileName), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        bulkUpload.Verify(service => service.ParseAndCreateForLatestRoundAsync(
            assignmentId, It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateRoundAsync_ArbitraryZipName_IsAccepted()
    {
        var assignmentId = Guid.NewGuid();
        var bulkUpload = new Mock<IBulkUploadService>();
        bulkUpload
            .Setup(service => service.CreateNewRoundAsync(
                assignmentId, It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BulkUploadResultDto());
        var controller = CreateController(bulkUpload.Object);

        var result = await controller.CreateRoundAsync(
            assignmentId, CreateFile("anything.zip"), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        bulkUpload.Verify(service => service.CreateNewRoundAsync(
            assignmentId, It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BulkUploadAsync_NonZipExtension_IsRejected()
    {
        var bulkUpload = new Mock<IBulkUploadService>();
        var controller = CreateController(bulkUpload.Object);

        var result = await controller.BulkUploadAsync(
            Guid.NewGuid(), CreateFile("submissions.rar"), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        bulkUpload.Verify(service => service.ParseAndCreateForLatestRoundAsync(
            It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static AssignmentsController CreateController(IBulkUploadService bulkUploadService)
    {
        var controller = new AssignmentsController(
            Mock.Of<IAssignmentService>(), bulkUploadService);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
        return controller;
    }

    private static IFormFile CreateFile(string fileName)
    {
        var content = new MemoryStream([1, 2, 3]);
        return new FormFile(content, 0, content.Length, "file", fileName);
    }
}
