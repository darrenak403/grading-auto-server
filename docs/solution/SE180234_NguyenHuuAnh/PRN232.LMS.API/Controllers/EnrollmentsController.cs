using Microsoft.AspNetCore.Mvc;
using PRN232.LMS.API.Controllers.Helpers;
using PRN232.LMS.API.Models.Requests;
using PRN232.LMS.API.Models.Responses;
using PRN232.LMS.Services.Interfaces;
using PRN232.LMS.Services.Models;

namespace PRN232.LMS.API.Controllers;

/// <summary>Manages Enrollment resources</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class EnrollmentsController : ControllerBase
{
    private readonly IEnrollmentService _service;

    public EnrollmentsController(IEnrollmentService service) => _service = service;

    /// <summary>Get all enrollments with search, sort, paging and expansion</summary>
    /// <param name="search">Filter by status, student name or course name</param>
    /// <param name="sort">Sort by field (prefix - for DESC). Example: -enrollDate,status</param>
    /// <param name="page">Page number</param>
    /// <param name="size">Page size</param>
    /// <param name="fields">Comma-separated fields to return</param>
    /// <param name="expand">Include related: student, course</param>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<System.Dynamic.ExpandoObject>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? sort,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        [FromQuery] string? fields = null,
        [FromQuery] string? expand = null)
    {
        var query = new QueryParameters
        {
            Search = search, Sort = sort, Page = page, Size = size,
            Fields = fields, Expand = expand
        };

        var result = await _service.GetAllAsync(query);
        var responseModels = result.Items.Select(ResponseMapper.ToResponse);
        var shapedItems = DataShaper.ShapeData(responseModels, fields);

        var response = new PagedResult<System.Dynamic.ExpandoObject>
        {
            Items      = shapedItems,
            Pagination = result.Pagination
        };

        return Ok(ApiResponse<PagedResult<System.Dynamic.ExpandoObject>>.Ok(response));
    }

    /// <summary>Get enrollment by ID with full student and course details</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<EnrollmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<EnrollmentResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result == null)
            return NotFound(ApiResponse<EnrollmentResponse>.NotFound($"Enrollment with id {id} not found"));

        return Ok(ApiResponse<EnrollmentResponse>.Ok(ResponseMapper.ToResponse(result)));
    }

    /// <summary>Create a new enrollment</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<EnrollmentResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<EnrollmentResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] EnrollmentRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<EnrollmentResponse>.BadRequest(
                "Validation failed", ModelState));

        var model = new EnrollmentBusinessModel
        {
            StudentId  = request.StudentId,
            CourseId   = request.CourseId,
            EnrollDate = request.EnrollDate,
            Status     = request.Status
        };

        var created = await _service.CreateAsync(model);
        return CreatedAtAction(nameof(GetById), new { id = created.EnrollmentId },
            ApiResponse<EnrollmentResponse>.Created(ResponseMapper.ToResponse(created)));
    }

    /// <summary>Update an existing enrollment</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<EnrollmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<EnrollmentResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] EnrollmentRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<EnrollmentResponse>.BadRequest(
                "Validation failed", ModelState));

        var model = new EnrollmentBusinessModel
        {
            StudentId  = request.StudentId,
            CourseId   = request.CourseId,
            EnrollDate = request.EnrollDate,
            Status     = request.Status
        };

        var updated = await _service.UpdateAsync(id, model);
        if (updated == null)
            return NotFound(ApiResponse<EnrollmentResponse>.NotFound($"Enrollment with id {id} not found"));

        return Ok(ApiResponse<EnrollmentResponse>.Ok(ResponseMapper.ToResponse(updated),
            "Enrollment updated successfully"));
    }

    /// <summary>Delete an enrollment</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _service.DeleteAsync(id);
        if (!deleted)
            return NotFound(ApiResponse<object>.NotFound($"Enrollment with id {id} not found"));

        return Ok(ApiResponse<object>.Ok(new { }, "Enrollment deleted successfully"));
    }
}
