using Microsoft.AspNetCore.Mvc;
using PRN232.LMS.API.Controllers.Helpers;
using PRN232.LMS.API.Models.Requests;
using PRN232.LMS.API.Models.Responses;
using PRN232.LMS.Services.Interfaces;
using PRN232.LMS.Services.Models;

namespace PRN232.LMS.API.Controllers;

/// <summary>Manages Course resources</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CoursesController : ControllerBase
{
    private readonly ICourseService _service;

    public CoursesController(ICourseService service) => _service = service;

    /// <summary>Get all courses with search, sort, paging and expansion</summary>
    /// <param name="search">Filter by course name</param>
    /// <param name="sort">Sort by field (prefix - for DESC)</param>
    /// <param name="page">Page number</param>
    /// <param name="size">Page size</param>
    /// <param name="fields">Comma-separated fields to return</param>
    /// <param name="expand">Include related: semester, enrollments</param>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<System.Dynamic.ExpandoObject>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? sort,
        [FromQuery] int page = 1,
        [FromQuery] int size = 10,
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

    /// <summary>Get course by ID with semester and enrollments</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<CourseResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CourseResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result == null)
            return NotFound(ApiResponse<CourseResponse>.NotFound($"Course with id {id} not found"));

        return Ok(ApiResponse<CourseResponse>.Ok(ResponseMapper.ToResponse(result)));
    }

    /// <summary>Get enrollments of a specific course with expansion option</summary>
    /// <param name="id">Course ID</param>
    /// <param name="expand">Include related: student</param>
    [HttpGet("{id:int}/enrollments")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<EnrollmentResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<EnrollmentResponse>>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEnrollments(int id, [FromQuery] string? expand = null)
    {
        var course = await _service.GetByIdAsync(id);
        if (course == null)
            return NotFound(ApiResponse<IEnumerable<EnrollmentResponse>>.NotFound($"Course with id {id} not found"));

        var expandList = expand?.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim().ToLower()).ToList() ?? [];

        var enrollments = course.Enrollments ?? new List<EnrollmentBusinessModel>();

        var responseModels = enrollments.Select(e => {
            var res = ResponseMapper.ToResponse(e);
            if (!expandList.Contains("student"))
            {
                res.Student = null;
            }
            res.Course = null; // Remove nested course redundancy
            return res;
        }).ToList();

        return Ok(ApiResponse<IEnumerable<EnrollmentResponse>>.Ok(responseModels));
    }

    /// <summary>Create a new course</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CourseResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<CourseResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CourseRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<CourseResponse>.BadRequest("Validation failed", ModelState));

        var model = new CourseBusinessModel
        {
            CourseName = request.CourseName,
            SemesterId = request.SemesterId
        };

        var created = await _service.CreateAsync(model);
        return CreatedAtAction(nameof(GetById), new { id = created.CourseId },
            ApiResponse<CourseResponse>.Created(ResponseMapper.ToResponse(created)));
    }

    /// <summary>Update an existing course</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<CourseResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CourseResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] CourseRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<CourseResponse>.BadRequest("Validation failed", ModelState));

        var model = new CourseBusinessModel
        {
            CourseName = request.CourseName,
            SemesterId = request.SemesterId
        };

        var updated = await _service.UpdateAsync(id, model);
        if (updated == null)
            return NotFound(ApiResponse<CourseResponse>.NotFound($"Course with id {id} not found"));

        return Ok(ApiResponse<CourseResponse>.Ok(ResponseMapper.ToResponse(updated),
            "Course updated successfully"));
    }

    /// <summary>Delete a course</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _service.DeleteAsync(id);
        if (!deleted)
            return NotFound(ApiResponse<object>.NotFound($"Course with id {id} not found"));

        return Ok(ApiResponse<object>.Ok(new { }, "Course deleted successfully"));
    }
}
