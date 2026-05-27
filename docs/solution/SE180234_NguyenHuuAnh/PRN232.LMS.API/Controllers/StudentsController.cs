using Microsoft.AspNetCore.Mvc;
using PRN232.LMS.API.Controllers.Helpers;
using PRN232.LMS.API.Models.Requests;
using PRN232.LMS.API.Models.Responses;
using PRN232.LMS.Services.Interfaces;
using PRN232.LMS.Services.Models;

namespace PRN232.LMS.API.Controllers;

/// <summary>Manages Student resources</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class StudentsController : ControllerBase
{
    private readonly IStudentService _service;

    public StudentsController(IStudentService service) => _service = service;

    /// <summary>Get all students with search, sort, paging and expansion</summary>
    /// <param name="search">Filter by full name or email</param>
    /// <param name="sort">Sort by field (prefix - for DESC). Example: fullName,-dateOfBirth</param>
    /// <param name="page">Page number</param>
    /// <param name="size">Page size</param>
    /// <param name="fields">Comma-separated fields to return</param>
    /// <param name="expand">Include related: enrollments</param>
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

    /// <summary>Get student by ID with all enrollments</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<StudentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<StudentResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result == null)
            return NotFound(ApiResponse<StudentResponse>.NotFound($"Student with id {id} not found"));

        return Ok(ApiResponse<StudentResponse>.Ok(ResponseMapper.ToResponse(result)));
    }

    /// <summary>Create a new student</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<StudentResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<StudentResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] StudentRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<StudentResponse>.BadRequest("Validation failed", ModelState));

        var model = new StudentBusinessModel
        {
            FullName    = request.FullName,
            Email       = request.Email,
            DateOfBirth = request.DateOfBirth
        };

        var created = await _service.CreateAsync(model);
        return CreatedAtAction(nameof(GetById), new { id = created.StudentId },
            ApiResponse<StudentResponse>.Created(ResponseMapper.ToResponse(created)));
    }

    /// <summary>Update an existing student</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<StudentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<StudentResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] StudentRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<StudentResponse>.BadRequest("Validation failed", ModelState));

        var model = new StudentBusinessModel
        {
            FullName    = request.FullName,
            Email       = request.Email,
            DateOfBirth = request.DateOfBirth
        };

        var updated = await _service.UpdateAsync(id, model);
        if (updated == null)
            return NotFound(ApiResponse<StudentResponse>.NotFound($"Student with id {id} not found"));

        return Ok(ApiResponse<StudentResponse>.Ok(ResponseMapper.ToResponse(updated),
            "Student updated successfully"));
    }

    /// <summary>Delete a student</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _service.DeleteAsync(id);
        if (!deleted)
            return NotFound(ApiResponse<object>.NotFound($"Student with id {id} not found"));

        return Ok(ApiResponse<object>.Ok(new { }, "Student deleted successfully"));
    }
}
